using System.Text.Json;
using System.Collections.Immutable;

namespace HybridCpu.ShadowFrontend;

public sealed record TraceRow(int Sequence, string Workload, string Provenance, BundleRequest Request,
    int Occupancy, string Contour, FrontendEvent? Event, bool Committed, bool ReplayObserved);
public sealed record LatencyModel(double Lookup = 1, double Cold = 40, double Warm = 2,
    double Hot = 1, double Fill = 1, double Retry = 0, double FetchFault = 1, double TerminalFault = 1,
    double Permission = 1, double Guard = .1, double Restore = 1, double IntegrityByte = .001)
{
    // Attempt work survives a retry/fault: count actual decode calls and successful fills.
    // Cold is the combined successful-fetch/decode assumption; Retry is only extra control overhead.
    // These coefficients remain uncalibrated and do not model physical guard/restore traffic.
    public double Evaluate(FrontendTelemetry t, bool enabled) => Evaluate(t.Snapshot(), enabled);
    public double Evaluate(TelemetrySnapshot t, bool enabled) => t.DecoderCalls * Cold +
        t.SemanticHits * Warm + t.ValidatedHotHits * Hot + t.Retries * Retry +
        (t.FetchAttempts - t.RawBundleFetches) * FetchFault + t.AccessFaults * TerminalFault +
        t.PermissionChecks * Permission + t.HotGuardChecks * Guard + t.RestoreAttempts * Restore + t.IntegrityBytesRead * IntegrityByte +
        (enabled ? (t.DbcLookups + t.HotCandidates) * Lookup + t.Fills * Fill : 0);
}

// Observations are independent of generator conventions: complete pre-request
// state plus expected terminal facts. Raw bytes are oracle input, never a lookup tag.
public sealed record ReplayObservation(int Version, int Seed, int Sequence, string Provenance,
    BundleRequest Request, DecodeContext Context, FreshnessToken Freshness, ulong ReplayGeneration,
    FrontendEvent? Event, ulong CommittedOrder, bool Committed, bool Clear, string RawHex,
    bool Allowed, string? FetchFault, HotBinding? CandidateBinding, HotBinding? CurrentBinding,
    string? CandidateRawHex, ImmutableArray<HotGuard> RejectedGuards, string? ExpectedEncodedHex,
    string? ExpectedAccessFault, IngressOutcome ExpectedOutcome,
    int Capacity = 64, int Ways = 4, int ByteBudget = int.MaxValue, bool Enabled = true,
    ReplacementPolicy Policy = ReplacementPolicy.QuotaBorrow, byte ActiveVts = 15,
    LookupTag? CandidateTag = null, ulong? CandidateReplayGeneration = null,
    string SidebandContour = "default-no-annotations", string ReplayProtocol = "absolute-state-prime-once-v1");
public static class ObservationTrace
{
    public const int Version = 1;
    public const int MaxRows = 100000, MaxLineBytes = 1048576;
    public static void Validate(ReplayObservation row)
    {
        row.Request.Validate();
        if (row.Version != Version || row.Sequence < 0 || row.Provenance is not ("synthetic" or "recorded-production") ||
            row.Context is null || row.RawHex is null || row.RawHex.Length != 512 || row.Freshness.Incarnation == 0 ||
            !Enum.IsDefined(row.ExpectedOutcome) || (row.Event.HasValue && !Enum.IsDefined(row.Event.Value)) ||
            row.RejectedGuards.IsDefault || row.RejectedGuards.Any(g => !Enum.IsDefined(g)))
            throw new InvalidDataException("Unsupported observation schema/state.");
        if(row.Capacity<row.Ways || row.Ways<1 || row.Capacity%row.Ways!=0 || row.Capacity>4096 || row.ByteBudget<=0 ||
            row.ActiveVts is 0 or >15 || !Enum.IsDefined(row.Policy) || row.SidebandContour!="default-no-annotations" ||
            row.ReplayProtocol!="absolute-state-prime-once-v1") throw new InvalidDataException("Unsupported replay configuration/sideband protocol.");
        try
        {
            _ = Convert.FromHexString(row.RawHex);
            if (row.CandidateRawHex is { } candidate && (candidate.Length != 512 || Convert.FromHexString(candidate).Length != 256))
                throw new InvalidDataException("Invalid candidate carrier.");
            if (row.ExpectedEncodedHex is { } expected && (expected.Length > CompactCodec.MaxEncodedBytes * 2 ||
                !CompactCodec.TryDecode(Convert.FromHexString(expected), out _))) throw new InvalidDataException("Invalid expected facts.");
        }
        catch (FormatException ex) { throw new InvalidDataException("Invalid hex observation.", ex); }
        if ((row.CandidateBinding is null) != (row.CandidateRawHex is null) ||
            (row.CandidateBinding is not null && row.CurrentBinding is null)) throw new InvalidDataException("Unpaired HOT observation.");
    }
    public static void Write(string path, IEnumerable<ReplayObservation> rows)
    {
        using var writer = new StreamWriter(path);
        int count = 0;
        foreach (var row in rows)
        {
            Validate(row);
            string line = JsonSerializer.Serialize(row);
            if (++count > MaxRows || System.Text.Encoding.UTF8.GetByteCount(line) > MaxLineBytes) throw new InvalidDataException("Trace bound exceeded.");
            writer.WriteLine(line);
        }
    }
    public static ImmutableArray<ReplayObservation> Read(string path)
    {
        var rows = ImmutableArray.CreateBuilder<ReplayObservation>();
        // Bound each line before allocating an unbounded string from external input.
        using var reader = new StreamReader(path, new System.Text.UTF8Encoding(false, true));
        var line = new System.Text.StringBuilder();
        int ch;
        while ((ch = reader.Read()) != -1)
        {
            if (ch != '\n') { if (line.Length >= MaxLineBytes) throw new InvalidDataException("Trace line bound exceeded."); line.Append((char)ch); continue; }
            Add();
        }
        if (line.Length != 0) Add();
        return rows.ToImmutable();
        void Add()
        {
            if (System.Text.Encoding.UTF8.GetByteCount(line.ToString()) > MaxLineBytes)
                throw new InvalidDataException("Trace UTF8 byte bound exceeded.");
            if (rows.Count >= MaxRows) throw new InvalidDataException("Trace row bound exceeded.");
            try
            {
                var row = JsonSerializer.Deserialize<ReplayObservation>(line.ToString()) ?? throw new InvalidDataException("Null observation.");
                Validate(row); rows.Add(row); line.Clear();
            }
            catch (JsonException ex) { throw new InvalidDataException("Invalid observation JSON.", ex); }
        }
    }
}
public static class TraceMinimizer
{
    // Predicate must rebuild its oracle/cache each time. Exceptions are not silently
    // classified as mismatches. Deterministic deletion gives a 1-minimal subsequence.
    public static ImmutableArray<T> Minimize<T>(IEnumerable<T> input, Func<ImmutableArray<T>, bool> stillFails, int maxEvaluations = 10000)
    {
        var current = input.ToImmutableArray();
        if (maxEvaluations < 1 || !stillFails(current)) throw new ArgumentException("Input is not a reproduced failure.");
        int evaluations = 1;
        for (int i = 0; i < current.Length;)
        {
            if (evaluations >= maxEvaluations) throw new InvalidOperationException("Minimization budget exhausted; no minimality claim.");
            var trial = current.RemoveAt(i); evaluations++;
            if (stillFails(trial)) { current = trial; i = 0; } else i++;
        }
        return current;
    }
}
public static class TraceModel
{
    public static ImmutableArray<string> Workloads { get; } = ["scalar-1", "scalar-2", "scalar-4", "scalar-8",
        "branchy", "streaming", "vector", "tile", "small-loop", "interleaved-loops",
        "smt-same-code", "smt-different-code", "context-churn", "smc-jit"];
    // Methodology fixed before measurements: equal weights over these six non-loop classes.
    public static ImmutableArray<string> WeightedNonLoop { get; } = ["scalar-1", "scalar-2", "scalar-4", "scalar-8", "branchy", "smt-different-code"];
    public static IEnumerable<TraceRow> Generate(string workload, int count = 256)
    {
        if (!Workloads.Contains(workload)) throw new ArgumentException("Unknown workload");
        for (int n = 0; n < count; n++)
        {
            int vt = workload.StartsWith("smt-") || workload == "interleaved-loops" ? n % 4 : 0;
            int bundle = workload switch {
                "streaming" => n, "small-loop" => 0, "interleaved-loops" => vt,
                "branchy" => (n * 17 + n / 9) % 24,
                "smt-different-code" => vt * 64 + (n / 4 % 12),
                "smt-same-code" => n / 4 % 12, _ => n % 16 };
            int occupancy = workload.StartsWith("scalar-") ? int.Parse(workload[7..]) : 4;
            FrontendEvent? ev = workload == "smc-jit" && n % 19 == 0 ? FrontendEvent.ExecutableWrite :
                workload == "smc-jit" && n % 19 == 1 ? FrontendEvent.FenceI :
                workload == "context-churn" && n % 13 == 0 ? FrontendEvent.Context : null;
            yield return new(n, workload, "synthetic", new((ulong)(bundle * 256), vt, (ulong)n), occupancy,
                workload is "vector" or "tile" ? workload : "scalar", ev, true,
                workload is "small-loop" or "interleaved-loops");
        }
    }
    public static IEnumerable<TraceRow> ReadJsonLines(string path)
    {
        foreach (string line in File.ReadLines(path))
        {
            if (line.Length > 65536) throw new InvalidDataException("Trace row exceeds bound.");
            var row = JsonSerializer.Deserialize<TraceRow>(line) ?? throw new InvalidDataException("Null trace row.");
            row.Request.Validate();
            if (row.Occupancy is < 0 or > 8 || row.Provenance is not ("synthetic" or "recorded-production"))
                throw new InvalidDataException("Invalid trace provenance/occupancy.");
            yield return row;
        }
    }
    public static void WriteJsonLines(string path, IEnumerable<TraceRow> trace) =>
        File.WriteAllLines(path, trace.Select(row => JsonSerializer.Serialize(row)));
}

public sealed record ModelRun(string Workload, string Mode, int Capacity, string Policy, int Budget,
    TelemetrySnapshot Counters, int ResidentBytes, double ModeledCycles, double HostMilliseconds, long HostAllocatedBytes,
    Dictionary<FrontendEvent, long> Invalidations, RetainedGraphAccounting? RetainedGraph = null);
public static class ModelHarness
{
    // Test bridge supplies the real LoopBuffer observation adapter. This production
    // harness remains independent of ISE and does not manufacture HOT observations.
    public static List<ModelRun> Sweep(Func<TraceRow[], string, int, ReplacementPolicy, int, ModelRun> run)
    {
        var results = new List<ModelRun>();
        foreach (string workload in TraceModel.Workloads)
        {
            var trace = TraceModel.Generate(workload).ToArray();
            foreach (int capacity in new[] { 32, 64, 128 })
                foreach (var policy in Enum.GetValues<ReplacementPolicy>())
                    foreach (string mode in new[] { "all-cold", "existing-loop", "dbc", "dbc-existing-loop" })
                        results.Add(run(trace, mode, capacity, policy, int.MaxValue));
            foreach (int budget in new[] { 8192, 16384, 32768 })
                results.Add(run(trace, "dbc", 128, ReplacementPolicy.QuotaBorrow, budget));
        }
        return results;
    }
}
