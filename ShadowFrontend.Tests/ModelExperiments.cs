using System.Text.Json;
using System.Diagnostics;
using HybridCpu.ShadowFrontend;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace ShadowFrontend.Tests;
internal static class ModelExperiments
{
    public static void Run(string output, Action<bool, string> check)
    {
        foreach (string workload in TraceModel.Workloads)
            TraceModel.WriteJsonLines(Path.Combine(output, $"trace-{workload}.jsonl"), TraceModel.Generate(workload));
        var results = ModelHarness.Sweep((trace, mode, capacity, policy, budget) => RunTrace(trace, mode, capacity, policy, budget, check));
        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
        File.WriteAllText(Path.Combine(output, "model-results.json"), JsonSerializer.Serialize(results, options));
        File.WriteAllText(Path.Combine(output, "cost-and-graph-accounting.json"), JsonSerializer.Serialize(results.Select(r => new {
            r.Workload,r.Mode,r.Capacity,r.Policy,r.Budget,r.RetainedGraph,
            r.Counters.PermissionChecks,r.Counters.HotGuardChecks,r.Counters.RestoreAttempts,r.Counters.HotIdentityBytesRead,
            r.Counters.IntegrityBytesRead,r.Counters.Fills,
            r.Counters.DbcLookups,r.Counters.LookupIntegrityChecks,r.Counters.HostIngressWaitTicks,r.Counters.HostIngressHoldTicks,
            identityTrafficScope="logical binary snapshot content traversed for compared identities; strings/CLR headers/caches excluded; conservative full comparison contour",
            costs="uncalibrated coefficients; host CPU time and allocation traffic recorded separately"
        }),options));
        var lines = new List<string> { "workload,mode,capacity,policy,budget,requests,cold,warm,hot,raw_fetches,decoder_calls,canonical_bytes,payload_read,tag_guard_read,integrity_read,fill_write,resident_bytes,modeled_cycles,host_ms,host_allocated_bytes" };
        foreach (var r in results)
        {
            var t = r.Counters;
            lines.Add(FormattableString.Invariant($"{r.Workload},{r.Mode},{r.Capacity},{r.Policy},{r.Budget},{t.Requests},{t.ColdIngress},{t.SemanticHits},{t.ValidatedHotHits},{t.RawBundleFetches},{t.DecoderCalls},{t.CanonicalBytesRead},{t.SemanticPayloadBytesRead},{t.TagGuardBytesRead},{t.IntegrityBytesRead},{t.FillBytesWritten},{r.ResidentBytes},{r.ModeledCycles},{r.HostMilliseconds:F3},{r.HostAllocatedBytes}"));
        }
        File.WriteAllLines(Path.Combine(output, "model-results.csv"), lines);
        var selected = results.Where(r => r.Capacity == 64 && r.Policy == "QuotaBorrow" && r.Budget == int.MaxValue && TraceModel.WeightedNonLoop.Contains(r.Workload)).ToArray();
        double baselineDecode = selected.Where(r => r.Mode == "existing-loop").Sum(r => r.Counters.DecoderCalls);
        double avoidance = 1 - selected.Where(r => r.Mode == "dbc-existing-loop").Sum(r => r.Counters.DecoderCalls) / baselineDecode;
        double baselineCycles = selected.Where(r => r.Mode == "existing-loop").Sum(r => r.ModeledCycles);
        double regression = selected.Where(r => r.Mode == "dbc-existing-loop").Sum(r => r.ModeledCycles) / baselineCycles - 1;
        File.WriteAllText(Path.Combine(output, "performance-decision.json"), JsonSerializer.Serialize(new {
            decision = "INCONCLUSIVE for production activation", syntheticDecodeAvoidance = avoidance,
            syntheticCanonicalByteReduction = avoidance, syntheticLatencyRegression = regression,
            syntheticBandwidthGate = avoidance >= .4 ? "PASS" : "NO-GO", syntheticLatencyGate = regression <= .02 ? "PASS" : "NO-GO",
            energy = "NOT EVALUATED: no calibrated energy model", realWorkloads = "NOT AVAILABLE",
            weightedNonLoop = TraceModel.WeightedNonLoop,
            latency = new LatencyModel(), separateTemplateCache = "FUTURE-GATED; not implemented"
        }, options));
        // Deterministic CPU model/counters; host stopwatch and allocation measurements intentionally excluded.
        var repeat = RunTrace(TraceModel.Generate("branchy").ToArray(), "dbc", 64, ReplacementPolicy.QuotaBorrow, int.MaxValue, check);
        var first = results.Single(r => r.Workload == "branchy" && r.Mode == "dbc" && r.Capacity == 64 && r.Policy == "QuotaBorrow" && r.Budget == int.MaxValue);
        check(repeat.ModeledCycles == first.ModeledCycles && JsonSerializer.Serialize(repeat.Counters.Deterministic(), options) == JsonSerializer.Serialize(first.Counters.Deterministic(), options), "deterministic trace accounting (host lock timing excluded)");
    }
    private static ModelRun RunTrace(TraceRow[] trace, string mode, int capacity, ReplacementPolicy policy, int budget, Action<bool, string> check)
    {
        var state = new FreshnessState(); var source = new TestSource(); var oracle = new OracleBridge();
        var context = OracleBridge.Context();
        var cache = new SemanticBundleCache(state, _ => context, source, oracle, capacity, byteBudget: budget);
        cache.SetPolicy(policy, 15); cache.Enabled = mode is "dbc" or "dbc-existing-loop";
        var buffers = new LoopBuffer[4]; for (int i = 0; i < 4; i++) buffers[i].Initialize();
        bool useLoop = mode is "existing-loop" or "dbc-existing-loop";
        ulong order = 0; long allocStart = GC.GetAllocatedBytesForCurrentThread(); var timer = Stopwatch.StartNew();
        var images = new Dictionary<ulong, FrozenBytes>();
        var captured = new HotCandidate?[4];
        int writes = 0;
        foreach (var row in trace)
        {
            if (!images.ContainsKey(row.Request.Pc)) images[row.Request.Pc] = row.Contour == "vector" ? OracleBridge.Fixture(1, IsaOpcodeValues.VADD) : OracleBridge.Fixture(row.Occupancy);
            if (row.Event is { } e)
            {
                FrozenBytes add = OracleBridge.Fixture(row.Occupancy, IsaOpcodeValues.ADD);
                FrozenBytes changed = images[row.Request.Pc].Equals(add) ? OracleBridge.Fixture(row.Occupancy, IsaOpcodeValues.SUB) : add;
                if (e == FrontendEvent.ExecutableWrite)
                {
                    check(!images[row.Request.Pc].Equals(changed), "SMC changes carrier bytes");
                    state.Apply(e, ++order, row.Committed, () => images[row.Request.Pc] = changed);
                    if(row.Committed) writes++;
                }
                else state.Apply(e, ++order, row.Committed);
                if (row.Committed) { foreach (ref var b in buffers.AsSpan()) b.Invalidate(); Array.Clear(captured); }
            }
            if (row.Workload == "context-churn") context = OracleBridge.Context() with { ContextIncarnation = $"ctx-{row.Sequence / 13}" };
            source.Raw = images[row.Request.Pc];
            HotCandidate? candidate = null;
            var binding = new HotBinding("model-structural", "model-certificate", $"vt-{row.Request.VirtualThread}", "model-boundary", 0, 0, row.Request.Cursor);
            SemanticIdentity? currentIdentity = null;
            if (useLoop && buffers[row.Request.VirtualThread].TryGetReplayEntry(row.Request.Pc, OracleBridge.NativeContext(context), out var replay))
            {
                candidate = captured[row.Request.VirtualThread];
                // Fresh current production LoopBuffer observation, independent of cached candidate.
                currentIdentity = new(context, OracleBridge.Project(replay!.CanonicalBundle));
            }
            // Enabled=false means all-COLD. Existing-loop baseline supplies HOT directly
            // via a separate enabled semantic cache with zero fill budget (no WARM hits).
            if (mode == "existing-loop") cache.Enabled = candidate is not null;
            var result = cache.Request(row.Request, candidate, new(binding, _ => true, currentIdentity));
            if(row.Workload=="smc-jit")
            {
                var authoritative=new OracleBridge().Decode(images[row.Request.Pc],row.Request,context);
                check(result.Facts is not null && result.Facts.SameFacts(authoritative),"SMC current bytes canonical equivalence");
            }
            if (row.ReplayObserved && result.Outcome == IngressOutcome.Cold && result.Facts?.IsSuccess == true && useLoop)
            {
                buffers[row.Request.VirtualThread] = OracleBridge.Load(oracle.LastBundle!, 100000, context);
                captured[row.Request.VirtualThread] = new(cache.CaptureTag(row.Request), new(context, result.Facts), binding, state.ReplayGeneration);
            }
            buffers[row.Request.VirtualThread].EndCycle();
        }
        timer.Stop(); cache.Telemetry.VerifyAccounting();
        check(source.Calls == cache.Telemetry.RawBundleFetches && oracle.Calls == cache.Telemetry.DecoderCalls, "actual oracle/source counters");
        check(cache.ResidentBytes <= budget, "byte-budget bounded");
        check(source.AccessCalls == cache.Telemetry.PermissionChecks, "permission source counters");
        return new(trace[0].Workload, mode, capacity, policy.ToString(), budget, cache.Telemetry, cache.ResidentBytes,
            new LatencyModel().Evaluate(cache.Telemetry, mode is "dbc" or "dbc-existing-loop"),
            timer.Elapsed.TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - allocStart, new(state.Invalidations),cache.RetainedGraph());
    }
}
