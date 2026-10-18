using System.Text.Json;
using System.Security.Cryptography;
using System.Diagnostics;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using HybridCpu.ShadowFrontend;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Arch.Generated;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace ShadowFrontend.Tests;

internal static class Program
{
    private static int assertions;
    private static readonly List<object> tests = [];
    private static readonly BundleRequest Request = new(0x1000, 0, 1);
    private static string output = "";
    public static int Main(string[] args)
    {
        if(args.Length>=2 && args[0]=="--replay") return ObservationExperiments.ReplayFile(args[1],args.Contains("--minimize"));
        output = Path.GetFullPath(args.Length > 0 ? args[0] : "HybridCPU_ISE/Frontend/docs/ShadowFrontend-Refactoring-Plan/evidence");
        Directory.CreateDirectory(output);
        Run("codec/full-fields/all-slots/negative", CodecTests);
        Run("catalog/vector-stream-tile/sideband-support", SupportCorpus);
        Run("cold/warm/fault/permission/invariant", IngressTests);
        Run("invalidation/race/wrap/ordering", InvalidationTests);
        Run("SMT/quota/borrow/context/domain", SmtTests);
        Run("HOT/production-LoopBuffer/every-guard", HotTests);
        Run("seeded-OFF-WARM-HOT-differential", Differential);
        Run("versioned-observation-replay-minimization-graph", () => ObservationExperiments.Run(output, Check));
        Run("canonical-unaligned-source-permission-contract", () => {
            var s=Setup(); var r=Request with { Pc=4090 };
            var cold=s.cache.Request(r); var warm=s.cache.Request(r with { TransportSerial=99 });
            Check(cold.Outcome==IngressOutcome.Cold && warm.Outcome==IngressOutcome.Warm && cold.Facts!.SameFacts(warm.Facts!),"Canonical oracle accepts address-qualified unaligned input; exact PC tag");
            s.source.DeniedExecutablePages.Add(1);
            Check(s.cache.Request(r).Outcome==IngressOutcome.AccessFault,"Unaligned carrier access spans both executable regions");
            s.cache.Telemetry.VerifyAccounting();
        });
        if (!args.Contains("--tests-only")) Run("synthetic-model-capacity-budget-sweep", Sweep);
        File.WriteAllText(Path.Combine(output, args.Contains("--tests-only") ? "correctness-results.json" : "test-results.json"), JsonSerializer.Serialize(new { tests, assertions, scope = "standalone canonical facts and event ordering; no CPU execution" }, JsonOptions));
        return tests.Any(t => JsonSerializer.Serialize(t).Contains("FAIL")) ? 1 : 0;
    }
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, IncludeFields = true };
    private static void Check(bool condition, string message)
    { assertions++; if (!condition) throw new InvalidOperationException(message); }
    private static void Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T) { assertions++; return; } throw new InvalidOperationException($"Expected {typeof(T).Name}"); }
    private static void Run(string name, Action action)
    {
        try { action(); tests.Add(new { name, status = "PASS" }); Console.WriteLine($"PASS {name}"); }
        catch (Exception e) { tests.Add(new { name, status = "FAIL", error = e.ToString() }); Console.WriteLine($"FAIL {name}: {e}"); }
    }
    private static (SemanticBundleCache cache, FreshnessState state, TestSource source, OracleBridge oracle) Setup(int capacity = 64,
        Func<BundleRequest, DecodeContext>? contexts = null, FreshnessState? state = null, int budget = int.MaxValue)
    {
        state ??= new(); var source = new TestSource(); var oracle = new OracleBridge();
        return (new(state, contexts ?? (_ => OracleBridge.Context()), source, oracle, capacity, byteBudget: budget), state, source, oracle);
    }
    private static void CodecTests()
    {
        var oracle = new OracleBridge(); var c = OracleBridge.Context();
        for (int i = 0; i < 8; i++)
        {
            var facts = oracle.Decode(OracleBridge.Fixture(1, offset: i), Request, c);
            Check(facts.IsSuccess && facts.Slots[i].IsOccupied, "fixture accepted"); RoundTrip(facts);
            // Non-default static fields and arbitrary opaque rich descriptors survive serialization.
            var slots = facts.Slots.ToArray();
            slots[i] = slots[i] with { Immediate = long.MinValue, CsrAddress = ushort.MaxValue,
                AcquireOrdering = true, ReleaseOrdering = true,
                InstructionPayload = new("rich-vector-stream-tile", new(Enumerable.Range(0, 8192).Select(n => (byte)n).ToArray())) };
            RoundTrip(DecodeResult.Success(facts.Raw, slots, facts.BundleSideband!));
        }
        byte[] mutable = OracleBridge.Fixture().Copy(); var frozen = new FrozenBytes(mutable); mutable[0] ^= 255;
        Check(!frozen.Equals(new FrozenBytes(mutable)), "deep raw copy");
        var illegal = oracle.Decode(OracleBridge.Fixture(2, ushort.MaxValue), Request, c);
        Check(!illegal.IsSuccess && illegal.Failure!.SlotIndex == 0, "fault order"); RoundTrip(illegal);
        var nop = OracleBridge.Fixture(0).Copy(); nop[31] = 17;
        RoundTrip(oracle.Decode(new(nop), Request, c)); // success or expected-illegal both preserve bytes
        var encoded = CompactCodec.Encode(illegal)!;
        for (int n = 0; n < encoded.Length; n++) Check(!CompactCodec.TryDecode(encoded.AsSpan(0, n), out _), "truncated codec");
        byte[] unknown = encoded.ToArray(); unknown[4] = 2;
        Check(!CompactCodec.TryDecode(unknown, out _), "schema rejected");
        Check(!CompactCodec.TryDecode(encoded.Concat(new byte[] { 0 }).ToArray(), out _), "trailing bytes rejected");
        Check(!CompactCodec.TryDecode(new byte[CompactCodec.MaxEncodedBytes + 1], out _), "encoded bound");
        foreach (int mutation in new[] { 0, 1, 2 })
        {
            using var decodedBody = new MemoryStream();
            using (var stream = new System.IO.Compression.BrotliStream(new MemoryStream(encoded[12..]), System.IO.Compression.CompressionMode.Decompress)) stream.CopyTo(decodedBody);
            byte[] body = decodedBody.ToArray();
            if (mutation == 0) body[0] = 2; // noncanonical bool
            if (mutation == 1) BitConverter.GetBytes(int.MaxValue).CopyTo(body, 1); // oversized raw length
            if (mutation == 2) body[261] = 9; // unsupported failure code
            using var malformed = new MemoryStream(); malformed.Write(encoded.AsSpan(0, 12));
            using (var stream = new System.IO.Compression.BrotliStream(malformed, System.IO.Compression.CompressionLevel.SmallestSize, true)) stream.Write(body);
            Check(!CompactCodec.TryDecode(malformed.ToArray(), out _), "malformed body rejected with valid compressed framing");
        }
        var rnd = new Random(42);
        for (int i = 0; i < 1000; i++) { byte[] fuzz = new byte[rnd.Next(512)]; rnd.NextBytes(fuzz); Check(!CompactCodec.TryDecode(fuzz, out _), "random corrupt"); }
        var original = oracle.Decode(OracleBridge.Fixture(), Request, c);
        Check(CompactCodec.Encode(DecodeResult.Success(original.Raw, original.Slots, original.BundleSideband!, 2)) is null, "unknown fill schema");
        var large = original.Slots.ToArray(); large[0] = large[0] with { SlotSideband = new("oversized", new(new byte[140000])) };
        Check(CompactCodec.Encode(DecodeResult.Success(original.Raw, large, original.BundleSideband!)) is null, "oversized fill");
        // This locks field coverage against future additions to live canonical contracts.
        Check(typeof(CanonicalDecodedInstruction).GetProperties().Select(p => p.Name).Order().SequenceEqual(new[] {
            "SlotIndex", "IsOccupied", "Opcode", "InstructionClass", "SerializationClass", "Rd", "Rs1", "Rs2", "Immediate", "CsrAddress", "AcquireOrdering", "ReleaseOrdering", "RawSlot", "InstructionPayload", "SlotSideband", "StaticBinding", "ScalarLoadAddressPlan" }.Order()), "canonical field coverage drift");
        File.WriteAllText(Path.Combine(output, "codec-sizes.json"), JsonSerializer.Serialize(new { decodedRawBytes = 256,
            scalarSerializedBytes = CompactCodec.Encode(original)!.Length, illegalSerializedBytes = encoded.Length,
            noNopElision = true, maxDecodedBytes = CompactCodec.MaxDecodedBytes }, JsonOptions));
    }
    private static void RoundTrip(DecodeResult facts)
    {
        var encoded = CompactCodec.Encode(facts);
        Check(encoded is not null && CompactCodec.TryDecode(encoded, out var restored) && facts.SameFacts(restored!), "full canonical codec roundtrip");
    }
    private static void SupportCorpus()
    {
        var reports = new List<object>(); var oracle = new OracleBridge();
        var context = OracleBridge.Context();
        foreach (var descriptor in GeneratedIsaCatalog.Descriptors)
        {
            foreach (bool rich in new[] { false, true })
            {
                var raw = new VLIW_Instruction[8];
                raw[3] = rich ? new VLIW_Instruction {
                    OpCode = checked((ushort)descriptor.Opcode), DestSrc1Pointer = 0x1000,
                    Src2Pointer = 0x2000, StreamLength = 8, Stride = 4, RowStride = 32,
                    PredicateMask = 3, DataType = (byte)DataTypeEnum.INT32
                } : new VLIW_Instruction { OpCode = checked((ushort)descriptor.Opcode) };
                var bytes = OracleBridge.Bytes(raw);
                var facts = oracle.Decode(bytes, Request, context); RoundTrip(facts);
                var setup = Setup(); setup.source.Raw = bytes;
                var cold = setup.cache.Request(Request); var warm = setup.cache.Request(Request);
                Check(warm.Outcome == IngressOutcome.Warm && facts.SameFacts(cold.Facts!) && facts.SameFacts(warm.Facts!), "all-descriptor complete ingress equivalence");
                reports.Add(new { descriptor.Opcode, descriptor.Mnemonic, rich, success = facts.IsSuccess, failure = facts.Failure });
            }
        }
        foreach (var vector in YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile.MatrixTilePositiveGoldenArtifactManifest.ExecutionVectors)
        {
            var raw = new VLIW_Instruction[8]; raw[0] = vector.Carrier.CreateInstruction();
            var facts = oracle.Decode(OracleBridge.Bytes(raw), Request, context);
            Check(facts.IsSuccess, $"golden tile {vector.Id} canonical decode"); RoundTrip(facts);
            reports.Add(new { tileGolden = vector.Id, success = facts.IsSuccess });
        }
        // Default and absent hints both take canonical paths; semantically significant
        // sideband identity remains in the tag even when it happens to decode equally.
        var sb = Setup();
        sb.oracle.Annotations = HybridCPU_ISE.Arch.VliwBundleAnnotations.Empty;
        var def = sb.cache.Request(Request); sb.cache.Clear(); sb.oracle.Annotations = null;
        Check(def.Facts!.SameFacts(sb.cache.Request(Request).Facts!), "absent/default hints canonical parity");
        var contexts = new[] {
            context with { ManifestVersion = "2" }, context with { ManifestHash = "b" },
            context with { Extensions = "b" }, context with { DecoderEpoch = "b" }, context with { DecoderVersion = "b" },
            context with { Privilege = "b" }, context with { Domain = "b" }, context with { AddressSpace = "b" },
            context with { VectorConfiguration = "b" }, context with { ExecutableEpoch = 1 }, context with { CodeEpoch = 1 },
            context with { ContextIncarnation = "b" }, context with { SidebandIdentity = "b" }, context with { ReplayEligible = false }
        };
        var current = context; var tags = Setup(contexts: _ => current);
        tags.cache.Request(Request);
        foreach (var c in contexts) { current = c; Check(tags.cache.Request(Request).Outcome == IngressOutcome.Cold, "every interpretation field changes lookup"); }
        var rawDma = new VLIW_Instruction[8]; rawDma[6] = new() { OpCode = IsaOpcodeValues.DmaStreamCompute };
        var metadata = Enumerable.Repeat(InstructionSlotMetadata.Default, 8).ToArray();
        metadata[6] = InstructionSlotMetadata.Default with { DmaStreamComputeDescriptor = HybridCPU_ISE.Tests.TestHelpers.DmaStreamComputeTestDescriptorFactory.CreateDescriptor() };
        var dma = Setup(); dma.source.Raw = OracleBridge.Bytes(rawDma); dma.oracle.Annotations = new(metadata);
        var dmaFacts = dma.cache.Request(Request).Facts!; Check(dmaFacts.IsSuccess, "valid DMA descriptor canonical success");
        RoundTrip(dmaFacts); Check(dma.cache.Request(Request).Facts!.SameFacts(dmaFacts), "DMA descriptor WARM parity");
        dma.cache.Clear(); metadata[0] = metadata[6]; dma.oracle.Annotations = new(metadata);
        var rejectedSideband = dma.cache.Request(Request).Facts!;
        Check(!rejectedSideband.IsSuccess && rejectedSideband.Failure!.SlotIndex == 0, "descriptor on NOP typed failure priority");
        RoundTrip(rejectedSideband); Check(dma.cache.Request(Request).Facts!.SameFacts(rejectedSideband), "rejected metadata cached failure parity");
        reports.Add(new { dmaDescriptor = true, accepted = dmaFacts.IsSuccess, rejectedSideband = rejectedSideband.Failure });
        File.WriteAllText(Path.Combine(output, "support-corpus.json"), JsonSerializer.Serialize(reports, JsonOptions));
    }
    private static void IngressTests()
    {
        var (cache, state, source, oracle) = Setup();
        var cold = cache.Request(Request); var warm = cache.Request(Request with { TransportSerial = 22, Cursor = 3 });
        Check(cold.Outcome == IngressOutcome.Cold && warm.Outcome == IngressOutcome.Warm && cold.Facts!.SameFacts(warm.Facts!), "cold/warm equality");
        Check(source.Calls == 1 && oracle.Calls == 1 && source.AccessCalls == 4, "actual bypass counters and access preserved");
        Check(warm.Placement.TransportSerial == 22 && warm.Placement.Cursor == 3, "fresh placement");
        source.Allowed = false;
        Check(cache.Request(Request).Outcome == IngressOutcome.AccessFault && source.Calls == 1, "revocation denies cached semantics");
        source.Allowed = true; cache.CorruptForExperiment(Request.Pc);
        Check(cache.Request(Request).Outcome == IngressOutcome.Cold && cache.Telemetry.CorruptEntries == 1, "corrupt fallback");
        cache.Enabled = false; Check(cache.Request(Request).Outcome == IngressOutcome.Cold, "off"); cache.Enabled = true;
        cache.Clear(); source.Raw = OracleBridge.Fixture(2, ushort.MaxValue);
        var bad = cache.Request(Request); var cachedBad = cache.Request(Request);
        Check(!bad.Facts!.IsSuccess && bad.Facts.SameFacts(cachedBad.Facts!) && cachedBad.Outcome == IngressOutcome.Warm, "typed negative cached");
        cache.Telemetry.VerifyAccounting();
        oracle.ThrowInvariant = true; cache.Clear(); Throws<InvalidOperationException>(() => cache.Request(Request));
        var unbound = Setup(contexts: _ => OracleBridge.Context() with { ReplayEligible = false });
        Check(unbound.cache.Request(Request).Outcome == IngressOutcome.Cold && unbound.cache.Request(Request).Outcome == IngressOutcome.Cold, "unknown identity miss");
        var unknown = Setup(); unknown.oracle.AfterDecode = () => { };
        Throws<ArgumentOutOfRangeException>(() => unknown.cache.Request(Request with { VirtualThread = 4 }));
        unknown.source.FetchFault = "dynamic-fetch-fault";
        Check(unknown.cache.Request(Request).Outcome == IngressOutcome.AccessFault && unknown.oracle.Calls == 0, "dynamic fetch fault does not decode/fill");
        unknown.source.FetchFault = null;
        Check(unknown.cache.Request(Request).Outcome == IngressOutcome.Cold, "transient fetch fault not cached");
        unknown.cache.Telemetry.VerifyAccounting();
        var unsupportedSource = new TestSource(); var unsupportedState = new FreshnessState();
        var unsupportedCache = new SemanticBundleCache(unsupportedState, _ => OracleBridge.Context(), unsupportedSource, new UnsupportedSchemaOracle());
        Check(unsupportedCache.Request(Request).Facts!.Schema == 2 && unsupportedCache.Request(Request).Outcome == IngressOutcome.Cold &&
            unsupportedCache.Telemetry.UnsupportedFills == 2 && unsupportedCache.ResidentBytes == 0, "unsupported schema preserves canonical COLD without fill");
    }
    private static void InvalidationTests()
    {
        var (cache, state, source, oracle) = Setup(); ulong order = 0;
        cache.Request(Request);
        foreach (var e in Enum.GetValues<FrontendEvent>())
        {
            state.Apply(e, ++order, false);
            Check(cache.Request(Request).Outcome == IngressOutcome.Warm, "squashed event invisible");
            state.Apply(e, ++order);
            bool semantic = e is not (FrontendEvent.Redirect or FrontendEvent.Trap or FrontendEvent.Rollback or FrontendEvent.ReplayPhase or FrontendEvent.Serializing);
            Check(cache.Request(Request).Outcome == (semantic ? IngressOutcome.Cold : IngressOutcome.Warm), "event invalidation contour");
        }
        Throws<InvalidOperationException>(() => state.Apply(FrontendEvent.FenceI, order));
        // Mutation DURING decode invalidates old result and old fill; next read gets new bytes.
        cache.Clear(); int mutations = 0;
        oracle.AfterDecode = () => { if (mutations++ == 0) { source.Raw = OracleBridge.Fixture(1, IsaOpcodeValues.SUB); state.Apply(FrontendEvent.ExecutableWrite, ++order); } };
        var result = cache.Request(Request);
        Check(result.Facts!.Slots[0].Opcode == IsaOpcodeValues.SUB && cache.Telemetry.Retries == 1, "late fill prevented");
        oracle.AfterDecode = null; Check(cache.Request(Request).Outcome == IngressOutcome.Warm, "new generation fill");
        cache.Clear(); oracle.AfterDecode = () => state.Apply(FrontendEvent.FenceI, ++order);
        Check(cache.Request(Request).Outcome == IngressOutcome.RetryExhausted, "bounded retry storm");
        oracle.AfterDecode = null; cache.Telemetry.VerifyAccounting();
        var wrap = Setup(state: FreshnessState.OverflowExperiment()); var old = wrap.state.Capture(); wrap.cache.Request(Request);
        wrap.state.Apply(FrontendEvent.ExecutableWrite, 1);
        Check(wrap.state.Capture().Incarnation != old.Incarnation && wrap.cache.Request(Request).Outcome == IngressOutcome.Cold, "overflow new incarnation");
        // Cross-region request is never trimmed to one page; test port validates whole range.
        var cross = Setup(); Check(cross.cache.Request(Request with { Pc = 0x1ff0 }).Facts!.Raw.Length == 256, "cross-page full carrier");
        cross.source.OnAccess = () => { if (cross.source.AccessCalls == 4) cross.source.Allowed = false; };
        Check(cross.cache.Request(Request with { Pc = 0x1ff0 }).Outcome == IngressOutcome.AccessFault, "revocation between checks");
        cross.source.OnAccess = null; cross.source.Allowed = true; cross.source.DeniedExecutablePages.Add(2);
        Check(cross.cache.Request(Request with { Pc = 0x1ff0 }).Outcome == IngressOutcome.AccessFault, "second covered executable page permission denied on hit");
        Check(cross.cache.Request(Request with { Pc = 0x1e00 }).Facts is not null, "unaffected single-page access");
    }
    private static void SmtTests()
    {
        var c = OracleBridge.Context();
        var x = Setup(contexts: r => c with { ContextIncarnation = $"vt-{r.VirtualThread}" });
        for (int v = 0; v < 4; v++) Check(x.cache.Request(Request with { VirtualThread = v }).Outcome == IngressOutcome.Cold, "no cross VT sharing");
        for (int v = 0; v < 4; v++) Check(x.cache.Request(Request with { VirtualThread = v }).Outcome == IngressOutcome.Warm, "isolated VT hits");
        c = c with { Domain = "domain-b" }; Check(x.cache.Request(Request).Outcome == IngressOutcome.Cold, "domain collision miss");
        c = c with { ContextIncarnation = "new-asid", AddressSpace = "reused-asid" }; Check(x.cache.Request(Request).Outcome == IngressOutcome.Cold, "ASID incarnation");
        // One resident per VT in each set, then hostile VT0 accesses identical sets.
        var q = Setup();
        for (int set = 0; set < 16; set++) for (int vt = 0; vt < 4; vt++) q.cache.Request(new((ulong)(set * 256), vt, 1));
        for (int n = 0; n < 300; n++) q.cache.Request(new((ulong)((n + 32) * 256), 0, 1));
        Check(q.cache.Occupancy.Skip(1).All(n => n == 16), "own-way quota protects other active VTs in this contour");
        q.cache.SetActiveVirtualThreads(1);
        for (int n = 0; n < 128; n++) q.cache.Request(new((ulong)((n + 512) * 256), 0, 1));
        Check(q.cache.Occupancy[0] == 64, "idle borrowing");
        q.cache.SetActiveVirtualThreads(15);
        for (int set = 0; set < 16; set++) for (int vt = 1; vt < 4; vt++) q.cache.Request(new((ulong)((set + 1024) * 256), vt, 1));
        Check(q.cache.Occupancy.All(n => n == 16), "reactivation reclaims loans");
        q.cache.SetPolicy(ReplacementPolicy.Lru, 15); Check(q.cache.Occupancy.Sum() == 0, "policy switch clear");
        q.cache.Telemetry.VerifyAccounting();
    }
    private static HotBinding Binding(int cursor = 0) => new("structural-a", "certificate-a", "owner-a", "boundary-a", 1, 1, cursor);
    private static void HotTests()
    {
        var (cache, state, source, oracle) = Setup(); var cold = cache.Request(Request);
        var candidate = new HotCandidate(cache.CaptureTag(Request), new(OracleBridge.Context(), cold.Facts!), Binding(), state.ReplayGeneration);
        var hot = new HotInputs(Binding(), _ => true, candidate.Identity);
        Check(cache.Request(Request, candidate, hot).Outcome == IngressOutcome.Hot && source.Calls == 1, "valid supplied HOT evidence");
        foreach (var guard in Enum.GetValues<HotGuard>())
            Check(cache.Request(Request, candidate, new(Binding(), g => g != guard, candidate.Identity)).Outcome == IngressOutcome.Warm, $"reject {guard} to WARM");
        Check(cache.Request(Request with { Cursor = 2 }, candidate, new(Binding(2), _ => true, candidate.Identity)).Outcome == IngressOutcome.Warm, "partial cursor mismatch");
        Check(cache.Request(Request, candidate, new(Binding(), _ => true)).Outcome == IngressOutcome.Warm, "unbound semantic witness rejects");
        var wrong = new OracleBridge().Decode(OracleBridge.Fixture(1, IsaOpcodeValues.SUB), Request, OracleBridge.Context());
        var collidingIdentity = new SemanticIdentity(OracleBridge.Context(), wrong);
        Check(candidate.Identity.GetHashCode() == collidingIdentity.GetHashCode() && !candidate.Identity.Equals(collidingIdentity), "forced hash-code collision cannot create semantic equality");
        Check(cache.Request(Request, candidate with { Identity = new(OracleBridge.Context(), wrong) }, hot).Outcome == IngressOutcome.Warm, "full semantic mismatch rejects without digest authority");
        Check(cache.Request(Request, candidate with { Binding = Binding() with { Owner = "other" } }, hot).Outcome == IngressOutcome.Warm, "wrong owner");
        Check(cache.Request(Request, candidate with { Binding = Binding() with { Certificate = "mutated" } }, hot).Outcome == IngressOutcome.Warm, "certificate mutation");
        state.Apply(FrontendEvent.Serializing, 1); Check(cache.Request(Request, candidate, hot).Outcome == IngressOutcome.Warm, "serializing invalidation");
        cache.Telemetry.VerifyAccounting();
        // Real production LoopBuffer reference, no legacy mutable replay API.
        var buffer = OracleBridge.Load(oracle.LastBundle!, 20);
        Check(buffer.TryGetReplayEntry(Request.Pc, OracleBridge.NativeContext(OracleBridge.Context()), out var entry) && entry!.HasValidFrozenContent(), "production semantic replay");
        Check(OracleBridge.Project(entry!.CanonicalBundle).SameFacts(cold.Facts!), "production replay oracle full facts");
        Check(!buffer.TryGetReplayEntry(Request.Pc, OracleBridge.NativeContext(OracleBridge.Context() with { Domain = "other" }), out _), "production context negative");
        foreach (var t in new[] { typeof(DecodeResult), typeof(HotCandidate), typeof(LookupTag), typeof(SemanticIdentity) })
            Check(!t.GetProperties().Any(p => p.Name.Contains("OperationId") || p.Name.Contains("Attempt")), "no issued identity API");
    }
    private static void Differential()
    {
        var seeds = new[] { 7, 42, 113, 20261005 }; var reports = new List<object>();
        foreach (int seed in seeds)
        {
            var rnd = new Random(seed); var c = OracleBridge.Context();
            var on = Setup(contexts: r => c with { ContextIncarnation = $"ctx-{r.VirtualThread}" });
            var off = Setup(contexts: r => c with { ContextIncarnation = $"ctx-{r.VirtualThread}" }); off.cache.Enabled = false;
            var images = Enumerable.Range(0, 12).ToDictionary(i => (ulong)(i * 256), i => OracleBridge.Fixture(i % 8 + 1,
                i % 7 == 0 ? ushort.MaxValue : IsaOpcodeValues.ADD));
            on.source.Select = off.source.Select = r => images[r.Pc];
            var trace = new List<object>(); ulong order = 0;
            var replayCandidates = new Dictionary<(ulong,int),HotCandidate>();
            var observations = new List<ReplayObservation>();
            try
            {
                for (int n = 0; n < 2000; n++)
                {
                    if (rnd.Next(37) == 0)
                    {
                        var e = (FrontendEvent)rnd.Next(Enum.GetValues<FrontendEvent>().Length);
                        on.state.Apply(e, ++order); off.state.Apply(e, order); trace.Add(new { n, e, order });
                    }
                    if (rnd.Next(131) == 0) { on.cache.Clear(); trace.Add(new { n, clear = true }); }
                    if (rnd.Next(101) == 0) c = c with { Domain = $"domain-{rnd.Next(3)}", SidebandIdentity = $"metadata-{rnd.Next(4)}" };
                    var r = new BundleRequest((ulong)(rnd.Next(12) * 256), rnd.Next(4), (ulong)n, rnd.Next(9));
                    bool permission = rnd.Next(20) != 0; on.source.Allowed = off.source.Allowed = permission;
                    var raw = images[r.Pc];
                    if (rnd.Next(53) == 0)
                    {
                        raw = OracleBridge.Fixture(1, IsaOpcodeValues.SUB);
                        on.state.Apply(FrontendEvent.ExecutableWrite, ++order, publish: () => images[r.Pc] = raw);
                        off.state.Apply(FrontendEvent.ExecutableWrite, order);
                    }
                    // Test image changes at any PC require publication. Normal PC selection
                    // selects a stable image, not a fictitious global mutation at every fetch.
                    on.source.Raw = off.source.Raw = raw;
                    if (rnd.Next(4) == 0) on.cache.CorruptForExperiment(r.Pc);
                    var expected = off.cache.Request(r);
                    HotCandidate? candidate = null;
                    if (rnd.Next(2) == 0) replayCandidates.TryGetValue((r.Pc,r.VirtualThread),out candidate);
                    var currentIdentity = expected.Facts is null ? null : new SemanticIdentity(on.cache.CaptureTag(r).Context,expected.Facts);
                    var actual = on.cache.Request(r, candidate, new(Binding(r.Cursor), g => n % 11 != 0 || g != HotGuard.Vdsa, currentIdentity));
                    observations.Add(new(1,seed,n,"synthetic",r,on.cache.CaptureTag(r).Context,on.state.Capture(),on.state.ReplayGeneration,
                        null,order,true,actual.Outcome==IngressOutcome.Cold,Convert.ToHexString(raw.Copy()),permission,null,
                        candidate?.Binding,candidate is null?null:Binding(r.Cursor),candidate is null?null:Convert.ToHexString(candidate.Identity.Result.Raw.Copy()),
                        n%11==0?[HotGuard.Vdsa]:[],expected.Facts is null?null:Convert.ToHexString(CompactCodec.Encode(expected.Facts)!),expected.AccessFault,actual.Outcome,
                        CandidateTag:candidate?.Tag,CandidateReplayGeneration:candidate?.ReplayGeneration));
                    if(actual.Facts?.IsSuccess==true)
                        replayCandidates[(r.Pc,r.VirtualThread)]=new(on.cache.CaptureTag(r),new(on.cache.CaptureTag(r).Context,actual.Facts),Binding(r.Cursor),on.state.ReplayGeneration);
                    trace.Add(new { n, r, permission, raw = Convert.ToHexString(raw.Copy()), context = c, actual.Outcome });
                    Check(actual.Placement == expected.Placement && actual.AccessFault == expected.AccessFault &&
                        ((actual.Facts is null && expected.Facts is null) || actual.Facts!.SameFacts(expected.Facts!)), $"seed {seed} step {n}");
                }
                on.cache.Telemetry.VerifyAccounting(); off.cache.Telemetry.VerifyAccounting();
                reports.Add(new { seed, requests = 2000, mismatches = 0, on = on.cache.Telemetry, off = off.cache.Telemetry });
                File.WriteAllLines(Path.Combine(output, $"differential-{seed}.jsonl"), trace.Select(t => JsonSerializer.Serialize(t)));
                ObservationTrace.Write(Path.Combine(output,$"differential-observations-v1-{seed}.jsonl"),observations);
            }
            catch
            {
                File.WriteAllLines(Path.Combine(output, $"failing-seed-{seed}.jsonl"), trace.Select(t => JsonSerializer.Serialize(t)));
                ObservationTrace.Write(Path.Combine(output,$"failing-observations-v1-{seed}.jsonl"),observations);
                if(observations.Any(ObservationExperiments.Mismatch))
                    ObservationTrace.Write(Path.Combine(output,$"minimized-observations-v1-{seed}.jsonl"),TraceMinimizer.Minimize(observations,t=>t.Any(ObservationExperiments.Mismatch)));
                throw;
            }
        }
        File.WriteAllText(Path.Combine(output, "differential.json"), JsonSerializer.Serialize(reports, JsonOptions));
    }
    private static void Sweep() => ModelExperiments.Run(output, Check);
    private sealed class UnsupportedSchemaOracle : ICanonicalDecoder
    {
        private readonly OracleBridge oracle = new();
        public DecodeResult Decode(FrozenBytes raw, BundleRequest request, DecodeContext context)
        {
            var canonical = oracle.Decode(raw, request, context);
            return DecodeResult.Success(canonical.Raw, canonical.Slots, canonical.BundleSideband!, 2);
        }
    }
}
