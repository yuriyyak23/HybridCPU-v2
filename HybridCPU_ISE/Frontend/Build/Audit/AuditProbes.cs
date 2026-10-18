using HybridCpu.ShadowFrontend;
using System.Text.Json;

// Protocol/negative probes only. This fixture does not decode ISA opcodes and is
// not a substitute for the production canonical decoder in conformance tests.
var context = new DecodeContext("1", "m", "e", "d", "v", "p", "domain", "as", "vec", 0, 0, true, "inc", "sb");
var request = new BundleRequest(0x1000, 0, 1);
DecodeResult Fixture(byte marker)
{
    var raw = new byte[256]; raw[31] = marker;
    Snapshot Snap(string name, byte[] b) => new(name, new(b));
    var slots = Enumerable.Range(0, 8).Select(i => new SemanticSlot(i, false, 0, null, null, 0, 0, 0, 0, null, false, false,
        Snap("raw", raw.AsSpan(i * 32, 32).ToArray()), Snap("payload", [marker]), Snap("sideband", []), Snap("binding", []), Snap("plan", [])));
    return DecodeResult.Success(new(raw), slots, Snap("bundle", []));
}
var results = new List<object>();
void Probe(string id, Func<string> action)
{
    try { results.Add(new { id, status = "PASS", detail = action() }); }
    catch (Exception e) { results.Add(new { id, status = "FAIL", detail = e.ToString() }); }
}
Probe("F01-HOT-request-cursor", () => {
    var state = new FreshnessState(); var source = new Source(Fixture(1)); var cache = new SemanticBundleCache(state, _ => context, source, new Decoder(source));
    var facts = cache.Request(request).Facts!; var identity = new SemanticIdentity(context, facts);
    var binding = new HotBinding("s", "c", "o", "b", 0, 0, 0);
    var candidate = new HotCandidate(cache.CaptureTag(request), identity, binding, state.ReplayGeneration);
    var result = cache.Request(request with { Cursor = 3 }, candidate, new(binding, _ => true, identity));
    if (result.Outcome != IngressOutcome.Warm || cache.Telemetry.GuardRejects.GetValueOrDefault(HotGuard.Cursor) != 1) throw new Exception("Contradictory request cursor must reject HOT and use WARM");
    return $"request.Cursor={result.Placement.Cursor}; candidate/current witness Cursor=0; outcome={result.Outcome}";
});
Probe("F02-publication-throws", () => {
    var state = new FreshnessState(); var source = new Source(Fixture(1)); var cache = new SemanticBundleCache(state, _ => context, source, new Decoder(source));
    cache.Request(request); var before = state.Capture();
    try { state.Apply(FrontendEvent.ExecutableWrite, 1, publish: () => { source.Facts = Fixture(2); throw new InvalidOperationException("after code mutation"); }); }
    catch (InvalidOperationException) { }
    bool stopped = false;
    try { cache.Request(request); } catch (InvalidOperationException) { stopped = true; }
    if (!stopped) throw new Exception("Partial publication must permanently stop serving");
    stopped = false;
    try { state.Apply(FrontendEvent.Restore, 2); } catch (InvalidOperationException) { stopped = true; }
    if (!stopped) throw new Exception("Restore must not revive poisoned state");
    return "Original publication exception preserved; requests and restore fail closed after partial mutation";
});
Probe("F03-stale-fetch-fault", () => {
    var state = new FreshnessState(); var source = new Source(Fixture(1)); var cache = new SemanticBundleCache(state, _ => context, source, new Decoder(source));
    source.OnFetch = () => { source.OnFetch = null; state.Apply(FrontendEvent.Configuration, 1); return new(null, "fault-from-old-generation"); };
    var result = cache.Request(request);
    if (result.Outcome != IngressOutcome.Cold || cache.Telemetry.Retries != 1 || cache.Telemetry.AccessFaults != 0) throw new Exception("Stale fault must retry");
    cache.Telemetry.VerifyAccounting();
    return "Stale fetch fault discarded; current COLD success after one retry";
});
Probe("F04-unbound-HOT-binding", () => {
    var state = new FreshnessState(); var source = new Source(Fixture(1)); var cache = new SemanticBundleCache(state, _ => context, source, new Decoder(source));
    var facts = cache.Request(request).Facts!; var identity = new SemanticIdentity(context, facts);
    var binding = new HotBinding("", "", "", "", 0, 0, 0);
    var result = cache.Request(request, new(cache.CaptureTag(request), identity, binding, state.ReplayGeneration), new(binding, _ => true, identity));
    if (result.Outcome != IngressOutcome.Warm) throw new Exception("Unbound equal witnesses must reject HOT");
    foreach (var b in new[] { new HotBinding("s", "", "o", "b", 0, 0, 0), new HotBinding("s", "c", "unbound", "b", 0, 0, 0), new HotBinding("s", "c", "o", " ", 0, 0, 0) })
        if (cache.Request(request, new(cache.CaptureTag(request), identity, b, state.ReplayGeneration), new(b, _ => true, identity)).Outcome == IngressOutcome.Hot)
            throw new Exception("Unbound witness escaped a guard");
    return "Empty, whitespace and unbound witnesses rejected even with equal candidate/current strings";
});
Probe("F05-codec-invalid-UTF16", () => {
    var facts = DecodeResult.Illegal(new(new byte[256]), new(1, 0, "opcode", "hash", "bad-\uD800"));
    if (CompactCodec.Encode(facts) is not null) throw new Exception("Invalid UTF16 must prohibit fill");
    foreach (var f in new[] { new TypedDecodeFailure(1, 0, "\uDC00", "hash", "ok"), new TypedDecodeFailure(1, 0, "field", "\uD800", "ok") })
        if (CompactCodec.Encode(DecodeResult.Illegal(facts.Raw, f)) is not null) throw new Exception("Invalid field/hash must prohibit fill");
    var valid = DecodeResult.Illegal(facts.Raw, new(1, 0, "opcode", "hash", "\U0001F600"));
    if (!CompactCodec.TryDecode(CompactCodec.Encode(valid)!, out var restored) || !valid.SameFacts(restored!)) throw new Exception("Valid surrogate pair must roundtrip");
    return "Invalid UTF16 rejected without replacement; valid Unicode remains lossless";
});
Probe("F06-tag-integrity", () => {
    var state = new FreshnessState(); var source = new Source(Fixture(1)); var cache = new SemanticBundleCache(state, _ => context, source, new Decoder(source));
    cache.Request(request);
    if (!cache.CorruptTagForExperiment(request.Pc)) throw new Exception("Missing entry");
    var result = cache.Request(request with { VirtualThread = 1 });
    if (result.Outcome != IngressOutcome.Cold || cache.Telemetry.CorruptEntries != 1) throw new Exception("Tag mutation must invalidate before serving");
    cache.Telemetry.VerifyAccounting();
    return "VT tag-bit mutation with intact payload invalidates entry and uses canonical COLD";
});
Probe("F03-access-fault-revalidation", () => {
    foreach (int checkNumber in new[] { 1, 2 })
    {
        var state = new FreshnessState(); var source = new Source(Fixture(1)); var cache = new SemanticBundleCache(state, _ => context, source, new Decoder(source));
        int checks = 0;
        source.OnAccess = () => {
            if (++checks == checkNumber) { state.Apply(FrontendEvent.Configuration, 1); return new(false, "stale-deny"); }
            return AccessDecision.Permit;
        };
        if (cache.Request(request).Outcome != IngressOutcome.Cold || cache.Telemetry.Retries != 1 || cache.Telemetry.AccessFaults != 0)
            throw new Exception("Initial/final stale deny must retry");
        cache.Telemetry.VerifyAccounting();
    }
    return "Initial and final stale access denial discarded";
});
Probe("F02-parallel-publication", () => {
    var state = new FreshnessState(); var source = new Source(Fixture(1)); var cache = new SemanticBundleCache(state, _ => context, source, new Decoder(source));
    cache.Request(request);
    using var mutated = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
    var publisher = Task.Run(() => state.Apply(FrontendEvent.ExecutableWrite, 1, publish: () => { source.Facts = Fixture(2); mutated.Set(); if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException(); }));
    if (!mutated.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
    var reader = Task.Run(() => cache.Request(request));
    release.Set();
    if (!Task.WaitAll([publisher, reader], TimeSpan.FromSeconds(5))) throw new TimeoutException();
    if (!reader.Result.Facts!.SameFacts(source.Facts) || reader.Result.Outcome != IngressOutcome.Cold) throw new Exception("Concurrent publication leaked old facts");
    return "Real threads: committed publication holds freshness gate; reader serves new canonical facts";
});
Probe("F05-invalid-kind-no-fill", () => {
    var original = Fixture(1);
    var unsupported = DecodeResult.Success(original.Raw, original.Slots, new("\uD800", new([])));
    if (CompactCodec.Encode(unsupported) is not null) throw new Exception("Invalid snapshot kind must prohibit fill");
    var source = new Source(unsupported); var cache = new SemanticBundleCache(new(), _ => context, source, new Decoder(source));
    for (int i = 0; i < 2; i++)
        if (cache.Request(request).Outcome != IngressOutcome.Cold) throw new Exception("Unsupported text must execute canonically");
    if (cache.Telemetry.UnsupportedFills != 2 || cache.Telemetry.Fills != 0) throw new Exception("Unexpected fill");
    return "Invalid Snapshot.Kind rejects encoding; repeated requests preserve COLD oracle facts";
});
Probe("F07-independent-current-witness", () => {
    var state = new FreshnessState(); var source = new Source(Fixture(1)); var cache = new SemanticBundleCache(state, _ => context, source, new Decoder(source));
    var candidateIdentity = new SemanticIdentity(context, cache.Request(request).Facts!);
    var binding = new HotBinding("s", "c", "o", "b", 1, 1, 0);
    var candidate = new HotCandidate(cache.CaptureTag(request), candidateIdentity, binding, state.ReplayGeneration);
    foreach (var current in new[] { binding with { Structural="changed" }, binding with { Certificate="changed" },
        binding with { Owner="changed" }, binding with { Boundary="changed" }, binding with { Phase=2 }, binding with { Serializing=2 } })
        if (cache.Request(request, candidate, new(current, _ => true, new(context, Fixture(1)))).Outcome == IngressOutcome.Hot)
            throw new Exception("Independent current witness mutation accepted");
    if (cache.Request(request, candidate, new(binding, _ => true, new(context, Fixture(2)))).Outcome == IngressOutcome.Hot)
        throw new Exception("Independent semantic witness mutation accepted");
    return "Independently constructed current content and mutated current bindings reject; no live scheduler claim";
});
Probe("F10-fill-and-attempt-model", () => {
    var source = new Source(Fixture(1)); var cache = new SemanticBundleCache(new(), _ => context, source, new Decoder(source), byteBudget: 1);
    cache.Request(request); cache.Request(request);
    if (cache.Telemetry.Fills != 0 || cache.Telemetry.DecoderCalls != 2) throw new Exception("Budget rejected fill counted");
    // Isolate fetch/decode/fill costs; permission is tested and modeled separately.
    var model = new LatencyModel(Lookup: 0, Fill: 100, Permission: 0);
    if (model.Evaluate(cache.Telemetry, true) != 80) throw new Exception("Rejected fill charged latency");
    var attempts = new FrontendTelemetry { Requests=1, DecoderCalls=2, RawBundleFetches=2, FetchAttempts=2, ColdIngress=1, Retries=1 };
    if (model.Evaluate(attempts, true) != 80) throw new Exception("Discarded decode attempt omitted/double counted");
    return "Fill charged only on successful store; all decode attempts remain in modeled work";
});
Probe("F14-consistent-observer-snapshot", () => {
    var state = new FreshnessState(); var source = new Source(Fixture(1)); var cache = new SemanticBundleCache(state, _ => context, source, new Decoder(source));
    var writer = Task.Run(() => { for (int i=0; i<100; i++) cache.Request(request); });
    for (int i=0; i<100; i++) { var snap=cache.Snapshot(); snap.Counters.VerifyAccounting(); if (snap.Occupancy.Sum()>64) throw new Exception("Unbounded snapshot"); }
    if (!writer.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
    var copy=cache.Snapshot(); _=copy.Counters.HitsByVt.SetItem(0,-1); _=copy.Counters.GuardRejects.Clear(); _=copy.Occupancy.SetItem(0,-1);
    cache.Snapshot().Counters.VerifyAccounting();
    state.Apply(FrontendEvent.FenceI,1); state.Invalidations.Clear();
    if (state.Invalidations.GetValueOrDefault(FrontendEvent.FenceI)!=1) throw new Exception("Invalidation snapshot exposes state");
    return "Concurrent reader snapshots conserve accounting; arrays/maps are detached from owned state";
});
Probe("N01-publication-cache-lock-reentry", () => {
    foreach (string operation in new[]{"Clear","Policy","ActiveVT","Snapshot","Request","Enabled","Telemetry","Graph","Corrupt"})
    {
        var state=new FreshnessState(); var source=new Source(Fixture(1));
        var cache=new SemanticBundleCache(state,_=>context,source,new Decoder(source));
        using var ingressOwned=new ManualResetEventSlim(); using var publicationInside=new ManualResetEventSlim();
        source.OnAccess=()=> {
            ingressOwned.Set();
            if(!publicationInside.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
            return AccessDecision.Permit;
        };
        var reader=Task.Run(()=> { try { cache.Request(request); return false; } catch(InvalidOperationException){return true;} });
        if(!ingressOwned.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
        var publisher=Task.Run(()=> {
            try { state.Apply(FrontendEvent.ExecutableWrite,1,publish:()=> {
                // Reader provably owns ingress; publisher now owns freshness.
                // Without pre-lock rejection Clear/etc wait on ingress while
                // reader progresses to the final freshness guard: ABBA.
                publicationInside.Set();
                switch(operation) {
                    case "Clear": cache.Clear(); break;
                    case "Policy": cache.SetPolicy(ReplacementPolicy.Lru,15); break;
                    case "ActiveVT": cache.SetActiveVirtualThreads(15); break;
                    case "Snapshot": cache.Snapshot(); break;
                    case "Request": cache.Request(request); break;
                    case "Enabled": cache.Enabled=false; break;
                    case "Telemetry": _=cache.Telemetry; break;
                    case "Graph": cache.RetainedGraph(); break;
                    case "Corrupt": cache.CorruptForExperiment(request.Pc); break;
                }
            }); return false; } catch(InvalidOperationException e) { return e.Message.Contains("Publication callbacks"); }
        });
        if(!Task.WaitAll([publisher,reader],TimeSpan.FromSeconds(5)) || !publisher.Result || !reader.Result)
            throw new Exception("Publication/cache lock reentry did not fail closed without deadlock: "+operation);
    }
    return "Nine callback cache-entry paths reject before ingress lock; concurrent Request and publication finish with visible errors, no ABBA";
});
Probe("N06-tag-first-integrity", () => {
    var state=new FreshnessState(); var source=new Source(Fixture(1)); var cache=new SemanticBundleCache(state,_=>context,source,new Decoder(source),4,4);
    for(int i=0;i<4;i++) cache.Request(request with { Pc=(ulong)(i*256) });
    long before=cache.Telemetry.LookupIntegrityChecks;
    if(cache.Request(request with { Pc=768 }).Outcome!=IngressOutcome.Warm || cache.Telemetry.LookupIntegrityChecks-before!=1)
        throw new Exception("Four-way hit must verify exactly one payload");
    before=cache.Telemetry.LookupIntegrityChecks;
    cache.Request(request with { Pc=1024 });
    if(cache.Telemetry.LookupIntegrityChecks!=before) throw new Exception("Off-key miss must not hash payloads");
    cache.CorruptTagForExperiment(1024);
    if(cache.Request(request with { Pc=1024,VirtualThread=1 }).Outcome!=IngressOutcome.Cold || cache.Telemetry.CorruptEntries!=1)
        throw new Exception("False matching corrupt tag must reject");
    return "Filled four-way set: last-way hit hashes one entry; miss hashes zero lookup payloads; corrupt-tag potential hit rejects";
});
Probe("N04-immutable-live-observations", () => {
    var cache=new SemanticBundleCache(new(),_=>context,new Source(Fixture(1)),new Decoder(new Source(Fixture(1))));
    cache.Request(request);
    var before=cache.Telemetry;
    if(typeof(TelemetrySnapshot).GetProperties().Any(p=>p.SetMethod?.IsPublic==true) || typeof(TelemetrySnapshot).GetFields().Length!=0)
        throw new Exception("Public telemetry snapshot is mutable");
    _=before.HitsByVt.SetItem(0,-123); _=before.GuardRejects.Add(HotGuard.Cursor,100);
    cache.Request(request);
    if(before.Requests!=1 || cache.Telemetry.Requests!=2 || cache.Telemetry.HitsByVt[0]!=1)
        throw new Exception("Snapshot aliases live counters");
    var snapshot=cache.Snapshot(); snapshot.Counters.VerifyAccounting();
    if(snapshot.Freshness.Token.Incarnation!=1) throw new Exception("Missing coherent freshness snapshot");
    return "Counters have read-only getters and immutable arrays/maps; old snapshot stable, combined cache/freshness captured in fixed lock order";
});
Probe("N07-multibyte-reader-bound", () => {
    var row=new ReplayObservation(1,1,0,"synthetic",request,context,new(1,0),0,null,0,true,false,
        Convert.ToHexString(Fixture(1).Raw.Copy()),true,null,null,null,null,[],Convert.ToHexString(CompactCodec.Encode(Fixture(1))!),null,IngressOutcome.Warm);
    var options=new JsonSerializerOptions { Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    string path=Path.Combine(AppContext.BaseDirectory,"reaudit-reader-input.jsonl");
    File.WriteAllText(path,JsonSerializer.Serialize(row with { Context=context with { ManifestHash=new string('\u0416',600000) } },options)+"\n");
    bool rejected=false; try{ObservationTrace.Read(path);}catch(InvalidDataException){rejected=true;}
    if(!rejected) throw new Exception("UTF8 bytes above bound accepted");
    ObservationTrace.Write(path,[row with { Context=context with { ManifestHash="\u0416\U0001F600" } }]);
    if(ObservationTrace.Read(path).Length!=1) throw new Exception("Valid multibyte row rejected");
    return "600k Cyrillic chars (1.2MB UTF8, under character limit) reject; valid Cyrillic/surrogate-pair row roundtrips";
});
Probe("F10-denial-no-lookup-cost", () => {
    var source=new Source(Fixture(1)){OnAccess=()=>new(false,"deny")};
    var cache=new SemanticBundleCache(new(),_=>context,source,new Decoder(source));
    cache.Request(request);
    if(cache.Telemetry.DbcLookups!=0 || new LatencyModel(Lookup:100,Permission:0,TerminalFault:0).Evaluate(cache.Telemetry,true)!=0)
        throw new Exception("Prelookup permission denial charged lookup work");
    return "Permission-denied request performs no cache lookup and is not charged lookup cycles";
});
Probe("F01-fresh-transport-serial", () => {
    var state=new FreshnessState(); var source=new Source(Fixture(1)); var cache=new SemanticBundleCache(state,_=>context,source,new Decoder(source));
    var identity=new SemanticIdentity(context,cache.Request(request).Facts!); var binding=new HotBinding("s","c","o","b",1,1,0);
    var result=cache.Request(request with{TransportSerial=999},new(cache.CaptureTag(request),identity,binding,0),new(binding,_=>true,new(context,Fixture(1))));
    if(result.Outcome!=IngressOutcome.Hot || result.Placement.TransportSerial!=999) throw new Exception("Reuse must preserve fresh caller transport serial");
    return "HOT semantic reuse returns fresh serial=999; transport serial is placement, never issued-attempt/certificate authority";
});
Probe("N02-four-VT-host-contention", () => {
    var observations=new List<object>();
    foreach(int threads in new[]{1,4})
    {
        var state=new FreshnessState(); var source=new Source(Fixture(1));
        var cache=new SemanticBundleCache(state,_=>context,source,new Decoder(source)){Enabled=false};
        int active=0,maxActive=0;
        source.OnFetch=()=> {
            int now=Interlocked.Increment(ref active); maxActive=Math.Max(maxActive,now);
            try { Thread.Sleep(1); return new(source.Facts.Raw,null); }
            finally { Interlocked.Decrement(ref active); }
        };
        using var start=new Barrier(threads);
        var timer=System.Diagnostics.Stopwatch.StartNew();
        var jobs=Enumerable.Range(0,threads).Select(v=>Task.Run(()=> {
            start.SignalAndWait(TimeSpan.FromSeconds(5));
            for(int i=0;i<64/threads;i++)cache.Request(request with { VirtualThread=v,TransportSerial=(ulong)i });
        })).ToArray();
        if(!Task.WaitAll(jobs,TimeSpan.FromSeconds(10)))throw new TimeoutException();
        timer.Stop(); var t=cache.Telemetry; t.VerifyAccounting();
        if(t.Requests!=64 || maxActive!=1)throw new Exception("Unexpected concurrent ingress or request loss");
        observations.Add(new {threads,requests=t.Requests,hostMilliseconds=timer.Elapsed.TotalMilliseconds,
            hostWaitMilliseconds=1000.0*t.HostIngressWaitTicks/System.Diagnostics.Stopwatch.Frequency,
            hostHoldMilliseconds=1000.0*t.HostIngressHoldTicks/System.Diagnostics.Stopwatch.Frequency,maxSourceConcurrency=maxActive});
    }
    File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"contention-results.json"),JsonSerializer.Serialize(new {
        provenance="synthetic host experiment; OFF source adds 1ms Thread.Sleep; not CPU cycles or real workload",
        ingress="single global lock remains an explicit standalone limitation",observations},new JsonSerializerOptions{WriteIndented=true}));
    return "1-VT and 4-VT host wait/hold metrics saved; maximum source concurrency=1; no CPU throughput claim";
});
Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
return results.Any(r => JsonSerializer.Serialize(r).Contains("\"FAIL\"")) ? 1 : 0;

sealed class Source(DecodeResult initial) : ICanonicalSource
{
    public DecodeResult Facts = initial;
    public Func<CanonicalFetchResult>? OnFetch;
    public Func<AccessDecision>? OnAccess;
    public AccessDecision CheckAccess(BundleRequest request, DecodeContext context) => OnAccess?.Invoke() ?? AccessDecision.Permit;
    public CanonicalFetchResult FetchFullBundle(BundleRequest request, DecodeContext context) => OnFetch?.Invoke() ?? new(Facts.Raw, null);
}
sealed class Decoder(Source source) : ICanonicalDecoder
{
    public DecodeResult Decode(FrozenBytes raw, BundleRequest request, DecodeContext context) => source.Facts;
}
