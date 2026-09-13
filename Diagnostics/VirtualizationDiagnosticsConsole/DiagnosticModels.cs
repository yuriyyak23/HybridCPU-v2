namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal enum DiagnosticSurfaceKind
{
    RuntimeContract,
    ModelContract,
}

internal sealed record ScenarioProfile(
    string SchemaVersion,
    string ScenarioId,
    DiagnosticSurfaceKind SurfaceKind,
    int Iterations,
    int Seed);

internal sealed record ScenarioResult(
    string SchemaVersion,
    string ScenarioId,
    DiagnosticSurfaceKind SurfaceKind,
    bool Succeeded,
    int RequestedIterations,
    int CompletedIterations,
    long AssertionCount,
    string? FailureMessage,
    string? FailureType,
    string TraceSha256,
    TimeSpan Elapsed,
    IReadOnlyDictionary<string, long> Counters);

internal sealed record ChildRunResult(
    string ScenarioId,
    DiagnosticSurfaceKind SurfaceKind,
    bool Succeeded,
    bool TimedOut,
    int? ExitCode,
    TimeSpan Elapsed,
    string ArtifactDirectory,
    string? FailureMessage);

internal sealed record BatchRunResult(
    string SchemaVersion,
    string Command,
    bool Succeeded,
    DateTimeOffset StartedUtc,
    DateTimeOffset FinishedUtc,
    string ArtifactDirectory,
    IReadOnlyList<ChildRunResult> Children);

internal sealed record HeartbeatRecord(
    string SchemaVersion,
    string ScenarioId,
    string State,
    int ProcessId,
    DateTimeOffset TimestampUtc,
    int CompletedIterations,
    long AssertionCount,
    string? Detail);

internal sealed record TraceRecord(
    int Iteration,
    string Event,
    IReadOnlyDictionary<string, object?> Data);
