namespace YAKSys_Hybrid_CPU.SecureComputeDiagnostics;

internal enum DiagnosticSurfaceKind
{
    RuntimeContract,
    PolicyClassifier,
    StaticInspection,
}

internal enum DiagnosticSeverity
{
    Information,
    Warning,
    Blocker,
}

internal sealed record DiagnosticFinding(
    string Code,
    DiagnosticSeverity Severity,
    string Title,
    string Detail);

internal sealed record TraceObservation(
    int Iteration,
    string Event,
    IReadOnlyDictionary<string, string> Data);

internal sealed record ScenarioProfile(
    string SchemaVersion,
    string ScenarioId,
    string Description,
    DiagnosticSurfaceKind SurfaceKind,
    int Iterations,
    int Seed,
    string AuthorityCeiling);

internal sealed record ScenarioResult(
    string SchemaVersion,
    string ScenarioId,
    string Description,
    DiagnosticSurfaceKind SurfaceKind,
    bool Succeeded,
    int RequestedIterations,
    int CompletedIterations,
    long AssertionCount,
    string AuthorityCeiling,
    string? FailureMessage,
    string? FailureType,
    string TraceSha256,
    long TraceEventCount,
    IReadOnlyDictionary<string, long> TraceEventsByName,
    TraceObservation? LastTraceObservation,
    TimeSpan Elapsed,
    IReadOnlyDictionary<string, long> Counters,
    IReadOnlyList<DiagnosticFinding> Findings);

internal sealed record ChildRunResult(
    string ScenarioId,
    string Description,
    DiagnosticSurfaceKind SurfaceKind,
    bool Succeeded,
    bool TimedOut,
    int? ExitCode,
    TimeSpan Elapsed,
    string ArtifactDirectory,
    string? FailureMessage,
    ScenarioResult? Result);

internal sealed record RepositorySnapshot(
    string CommitSha,
    bool IsDirty,
    string DotnetSdk,
    string Runtime,
    string OperatingSystem);

internal sealed record BatchRunResult(
    string SchemaVersion,
    string Command,
    bool Succeeded,
    DateTimeOffset StartedUtc,
    DateTimeOffset FinishedUtc,
    string RepositoryRoot,
    RepositorySnapshot Repository,
    string ArtifactDirectory,
    int RequestedScenarioCount,
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
