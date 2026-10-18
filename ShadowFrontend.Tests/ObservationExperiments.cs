using HybridCpu.ShadowFrontend;
using System.Collections.Immutable;
using System.Text.Json;
using YAKSys_Hybrid_CPU.Arch;
using HybridCPU_ISE.Arch;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace ShadowFrontend.Tests;
internal static class ObservationExperiments
{
    public static bool Mismatch(ReplayObservation row)
    {
        ObservationTrace.Validate(row);
        var state = FreshnessState.ObservationExperiment(row.Freshness, row.ReplayGeneration, row.CommittedOrder);
        var source = new TestSource { Raw=new(Convert.FromHexString(row.RawHex)), Allowed=row.Allowed, FetchFault=row.FetchFault };
        var oracle = new OracleBridge(); var cache = new SemanticBundleCache(state, _ => row.Context, source, oracle,row.Capacity,row.Ways,row.ByteBudget);
        cache.SetPolicy(row.Policy,row.ActiveVts); cache.Enabled=row.Enabled;
        // Reproduce COLD and WARM within this complete observation. No dependency
        // on deleted earlier rows: the minimizer preserves absolute input state.
        var first = cache.Request(row.Request);
        if (row.Clear) cache.Clear();
        HotCandidate? candidate = null;
        if (row.CandidateRawHex is { } raw)
        {
            var candidateContext=row.CandidateTag?.Context??row.Context;
            var historical = new OracleBridge().Decode(new(Convert.FromHexString(raw)), row.Request, candidateContext);
            candidate = new(row.CandidateTag??cache.CaptureTag(row.Request), new(candidateContext, historical), row.CandidateBinding!, row.CandidateReplayGeneration??row.ReplayGeneration);
        }
        // Independent current canonical capture; deliberately not candidate.Identity.
        var current = new OracleBridge().Decode(source.Raw, row.Request, row.Context);
        var actual = cache.Request(row.Request, candidate, row.CurrentBinding is null ? null :
            new(row.CurrentBinding, g => !row.RejectedGuards.Contains(g), new(row.Context,current)));
        cache.Telemetry.VerifyAccounting();
        DecodeResult? expected = null;
        if (row.ExpectedEncodedHex is { } encoded) CompactCodec.TryDecode(Convert.FromHexString(encoded), out expected);
        return actual.AccessFault != row.ExpectedAccessFault || actual.Outcome != row.ExpectedOutcome ||
            ((actual.Facts is null) != (expected is null)) || (actual.Facts is not null && !actual.Facts.SameFacts(expected!));
    }
    public static void Run(string output, Action<bool,string> check)
    {
        var context = OracleBridge.Context(); var state = new FreshnessState();
        var rows = new List<ReplayObservation>(); ulong order=0; var oracle = new OracleBridge();
        FrozenBytes raw = OracleBridge.Fixture();
        for(int n=0;n<64;n++)
        {
            FrontendEvent? ev = n%8==0 ? FrontendEvent.ExecutableWrite : n%8==1 ? FrontendEvent.FenceI : null;
            if(ev.HasValue)
            {
                var next=OracleBridge.Fixture(2, n%16==0 ? IsaOpcodeValues.SUB : IsaOpcodeValues.ADD);
                state.Apply(ev.Value,++order,publish:()=> { if(ev==FrontendEvent.ExecutableWrite) raw=next; });
            }
            var request=new BundleRequest(0x1000,n%4,(ulong)n,n%9);
            var facts=oracle.Decode(raw,request,context);
            var binding=new HotBinding("observed-structure","observed-certificate",$"vt-{n%4}","observed-boundary",0,0,request.Cursor);
            bool hot=n%3==0, deny=n%11==0;
            rows.Add(new(1,42,n,"synthetic",request,context,state.Capture(),state.ReplayGeneration,ev,order,true,false,
                Convert.ToHexString(raw.Copy()),!deny,null,hot?binding:null,hot?binding:null,hot?Convert.ToHexString(raw.Copy()):null,
                [],deny?null:Convert.ToHexString(CompactCodec.Encode(facts)!),deny?"execute-permission":null,
                deny?IngressOutcome.AccessFault:hot?IngressOutcome.Hot:IngressOutcome.Warm));
        }
        string path=Path.Combine(output,"observations-v1.jsonl"); ObservationTrace.Write(path,rows);
        var restored=ObservationTrace.Read(path);
        if(restored.Any(Mismatch))
        {
            ObservationTrace.Write(Path.Combine(output,"failing-observations-seed-42.jsonl"),restored);
            ObservationTrace.Write(Path.Combine(output,"minimized-observations-seed-42.jsonl"),TraceMinimizer.Minimize(restored,t=>t.Any(Mismatch)));
            throw new InvalidOperationException("Observation mismatch: failing seed and minimal trace saved");
        }
        check(restored.Length==64,"Versioned observations roundtrip and independent replay");
        // Historical content candidate must reject against independently decoded current bytes.
        var stale=restored[3] with { CandidateRawHex=Convert.ToHexString(OracleBridge.Fixture(2,IsaOpcodeValues.ADD).Copy()), ExpectedOutcome=IngressOutcome.Warm };
        check(!Mismatch(stale),"Independent replay identity rejects changed content");
        check(!Mismatch(restored[3] with { CandidateReplayGeneration=ulong.MaxValue, ExpectedOutcome=IngressOutcome.Warm }),"Stale replay generation observation rejects HOT");
        check(!Mismatch(restored[3] with { Enabled=false,ExpectedOutcome=IngressOutcome.Cold }),"OFF replay protocol");
        check(!Mismatch(restored[3] with { RejectedGuards=[HotGuard.Vdsa],ExpectedOutcome=IngressOutcome.Warm }),"Recorded live guard rejection replay");
        bool rejected=false; try { ObservationTrace.Validate(restored[0] with { Version=2 }); } catch(InvalidDataException){rejected=true;}
        check(rejected,"Unknown observation version rejected");
        // Deliberately corrupt expected facts to prove minimization/reproduction machinery.
        var bad=restored.ToArray(); bad[17]=bad[17] with { ExpectedAccessFault="injected-comparator-mismatch" };
        bool Fails(ImmutableArray<ReplayObservation> trace)=>trace.Any(Mismatch);
        var minimal=TraceMinimizer.Minimize(bad,Fails);
        check(minimal.Length==1 && Fails(minimal),"Failing trace minimized to necessary row");
        for(int i=0;i<minimal.Length;i++) check(!Fails(minimal.RemoveAt(i)),"1-minimal failing subsequence");
        ObservationTrace.Write(Path.Combine(output,"injected-failing-seed-42.jsonl"),bad);
        ObservationTrace.Write(Path.Combine(output,"injected-minimized-seed-42.jsonl"),minimal);
        var source=new TestSource(); var cache=new SemanticBundleCache(new(),_=>context,source,new OracleBridge());
        cache.Request(new(0x1000,0,1)); cache.Request(new(0x1000,0,2));
        var graph=cache.RetainedGraph();
        check(graph.Entries==1 && graph.EncodedContentBytes>0 && graph.IntegrityContentBytes==32 && graph.UniqueContexts==1,"Retained graph content inventory");
        File.WriteAllText(Path.Combine(output,"retained-graph.json"),JsonSerializer.Serialize(graph,new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(Path.Combine(output,"observation-validation.json"),JsonSerializer.Serialize(new {version=1,seed=42,rows=64,mismatches=0,minimalRows=minimal.Length,
            injectedFailure=true, scope="absolute snapshot replay; events recorded as committed provenance, not event-transition CPU proof"}));
    }
    public static int ReplayFile(string path, bool minimize)
    {
        var rows=ObservationTrace.Read(path);
        bool failed=rows.Any(Mismatch);
        if(failed && minimize)
            ObservationTrace.Write(path+".minimized.jsonl",TraceMinimizer.Minimize(rows,t=>t.Any(Mismatch)));
        Console.WriteLine(JsonSerializer.Serialize(new {version=1,rows=rows.Length,mismatch=failed,minimized=failed&&minimize}));
        return failed?1:0;
    }
}
