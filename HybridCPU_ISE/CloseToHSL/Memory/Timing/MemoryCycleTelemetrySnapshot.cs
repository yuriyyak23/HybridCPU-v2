namespace HybridCPU_ISE.CloseToHSL.Memory.Timing;

/// <summary>
/// Additive, observation-only counters for the RF-10 timed-memory domain.
/// Classified request counts are properties of a request and are therefore
/// not a partition: a canonical vector transfer is both a data read and a
/// future data write, while total counts still count its identity once.
/// </summary>
public readonly record struct MemoryCycleTelemetrySnapshot(
    ulong ControllerCycles,
    ulong ReadServiceCycles,
    ulong StoreReadinessServiceCycles,
    ulong CompletionPublicationCycles,
    ulong AcceptedRequests,
    ulong CompletedRequests,
    ulong DataReadAcceptedRequests,
    ulong DataReadCompletedRequests,
    ulong DataWriteAcceptedRequests,
    ulong DataWriteCompletedRequests,
    ulong DataReadBytes,
    ulong CommittedDataWriteBytes,
    ulong InstructionFetchReadBytes,
    ulong QueueFullRejects)
{
    public const string SchemaVersion = "memory-cycle-telemetry-v1";

    // Fetch remains a synchronous cache/main-memory contour and has no
    // controller request-identity protocol to count truthfully.
    public const bool InstructionFetchRequestTelemetryAvailable = false;

    // Controller-native admission currently has capacity backpressure but no
    // bank-conflict rejection result. Scheduler bank conflicts and legacy
    // MemorySubsystem conflicts are different counters and must not be merged.
    public const bool BankConflictRejectTelemetryAvailable = false;
}
