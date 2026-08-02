namespace YAKSys_Hybrid_CPU.DiagnosticsConsole;

internal sealed record TelemetryMetric(
    string Availability,
    ulong? Value,
    string ProducerBoundary)
{
    internal static TelemetryMetric Available(ulong value, string producerBoundary) =>
        new("Available", value, producerBoundary);

    internal static TelemetryMetric Unavailable(string producerBoundary) =>
        new("Unavailable", null, producerBoundary);
}

/// <summary>
/// Versioned consumer projection of producer-owned timing and memory telemetry.
/// Existing v1 fields remain additive compatibility fields; metrics without a
/// truthful runtime producer carry Availability=Unavailable and Value=null.
/// </summary>
internal sealed record TimingMemoryReport(
    string SchemaVersion,
    string ProducerSchemaVersion,
    string FunctionalBaseline,
    string TimingBaseline,
    string TelemetryAvailabilityPolicy,
    string TimingComparisonPolicy,
    string SchedulerDiagnosticComparisonPolicy,
    string EligibilityMaskComparisonPolicy,
    ulong TotalCycles,
    ulong PipelineStallCycles,
    ulong MemoryStallCycles,
    ulong NonMemoryStallCycles,
    ulong InstructionsRetired,
    double RawCycleIpc,
    double RetireNormalizedIpc,
    string FineGrainedCycleBreakdownAvailability,
    string MemoryTelemetryDisposition,
    ulong LegacyTotalBursts,
    ulong LegacyBytesTransferred,
    bool MemoryWaitObserved,
    string MemoryTelemetryMessage,
    TelemetryMetric MemoryControllerCycles,
    TelemetryMetric MemoryReadServiceCycles,
    TelemetryMetric MemoryStoreReadinessServiceCycles,
    TelemetryMetric MemoryCompletionPublicationCycles,
    TelemetryMetric AcceptedMemoryRequests,
    TelemetryMetric CompletedMemoryRequests,
    TelemetryMetric DataReadAcceptedRequests,
    TelemetryMetric DataReadCompletedRequests,
    TelemetryMetric DataWriteAcceptedRequests,
    TelemetryMetric DataWriteCompletedRequests,
    TelemetryMetric DataReadBytes,
    TelemetryMetric CommittedDataWriteBytes,
    TelemetryMetric InstructionFetchAcceptedRequests,
    TelemetryMetric InstructionFetchCompletedRequests,
    TelemetryMetric InstructionFetchReadBytes,
    TelemetryMetric QueueFullRejects,
    TelemetryMetric BankConflictRejects)
{
    internal const string ArtifactFileName = "post_ref1_timing_memory_report.json";

    internal static TimingMemoryReport Create(SimpleAsmAppMetrics metrics)
    {
        bool memoryWaitObserved = metrics.MemoryStalls > 0;
        bool legacyTransferCountersEmpty = metrics.TotalBursts == 0 && metrics.BytesTransferred == 0;
        string memoryDisposition = metrics.MemoryCycleTelemetryAvailable
            ? "ProducerTelemetryAvailable"
            : memoryWaitObserved && legacyTransferCountersEmpty
                ? "UnavailableWhilePipelineReportsMemoryWait"
                : legacyTransferCountersEmpty
                    ? "NoLegacyTransferActivityObserved"
                    : "LegacyTransferCountersReported";
        string memoryMessage = metrics.MemoryCycleTelemetryAvailable
            ? "Controller request/completion telemetry is producer-owned. Legacy burst counters remain a separate compatibility surface and may be zero during controller-native activity."
            : memoryDisposition switch
            {
                "UnavailableWhilePipelineReportsMemoryWait" =>
                    "Pipeline memory waits are non-zero while legacy burst/byte counters are zero. Do not read zero as no memory activity; producer telemetry was unavailable.",
                "NoLegacyTransferActivityObserved" =>
                    "Legacy burst/byte counters are zero and this run did not report pipeline memory waits; producer telemetry was unavailable.",
                _ => "Legacy burst/byte counters were observed, but producer telemetry was unavailable."
            };

        TelemetryMetric ProducerMetric(ulong value, string boundary) =>
            metrics.MemoryCycleTelemetryAvailable
                ? TelemetryMetric.Available(value, boundary)
                : TelemetryMetric.Unavailable(boundary);

        return new TimingMemoryReport(
            SchemaVersion: "post-ref1-timing-memory-v2",
            ProducerSchemaVersion: metrics.MemoryCycleTelemetryAvailable
                ? metrics.MemoryCycleTelemetrySchemaVersion
                : "Unavailable",
            FunctionalBaseline: "Post-Ref1 functional parity; preserve pre-Ref1 results as historical evidence.",
            TimingBaseline: "Post-RF10 timing baseline; establish independently from historical pre-RF10 total cycles.",
            TelemetryAvailabilityPolicy: "Available zero means measured zero. Unavailable means the runtime has no truthful producer for that metric.",
            TimingComparisonPolicy: "Pre-RF10 and post-RF10 total-cycle values are not comparable until MemoryCycleController equivalence is demonstrated.",
            SchedulerDiagnosticComparisonPolicy: "LastSmtLegalityRejectKind is a current-observation field, not a stable last-rejection history across Ref1.",
            EligibilityMaskComparisonPolicy: "Eligibility masks use the current candidate-bit layout; compare pre-Ref1 values only with an explicit bit mapping.",
            TotalCycles: metrics.CycleCount,
            PipelineStallCycles: metrics.StallCycles,
            MemoryStallCycles: metrics.MemoryStalls,
            NonMemoryStallCycles: metrics.StallCycles > metrics.MemoryStalls
                ? metrics.StallCycles - metrics.MemoryStalls
                : 0,
            InstructionsRetired: metrics.InstructionsRetired,
            RawCycleIpc: metrics.Ipc,
            RetireNormalizedIpc: metrics.RetireIpc,
            FineGrainedCycleBreakdownAvailability:
                "Unavailable: fetch-wait, decode/admission-wait, memory-admission-wait, memory-completion-wait, execute-wait, writeback-wait, retire and hazard early-return do not have exact producer owners. Controller edge/service/publication-cycle boundaries are exposed separately and may overlap.",
            MemoryTelemetryDisposition: memoryDisposition,
            LegacyTotalBursts: metrics.TotalBursts,
            LegacyBytesTransferred: metrics.BytesTransferred,
            MemoryWaitObserved: memoryWaitObserved,
            MemoryTelemetryMessage: memoryMessage,
            MemoryControllerCycles: ProducerMetric(metrics.MemoryControllerCycles, "MemoryCycleController observed edges since telemetry reset"),
            MemoryReadServiceCycles: ProducerMetric(metrics.MemoryReadServiceCycles, "MemoryCycleController cycles selecting one controller-native read"),
            MemoryStoreReadinessServiceCycles: ProducerMetric(metrics.MemoryStoreReadinessServiceCycles, "MemoryCycleController cycles selecting one scalar-store readiness request"),
            MemoryCompletionPublicationCycles: ProducerMetric(metrics.MemoryCompletionPublicationCycles, "MemoryCycleController cycles publishing one or more prior-latched completions"),
            AcceptedMemoryRequests: ProducerMetric(metrics.MemoryAcceptedRequests, "MemoryCycleController unique accepted request identities"),
            CompletedMemoryRequests: ProducerMetric(metrics.MemoryCompletedRequests, "MemoryCycleController published completions"),
            DataReadAcceptedRequests: ProducerMetric(metrics.DataReadAcceptedRequests, "MemoryCycleController requests with a data-read property"),
            DataReadCompletedRequests: ProducerMetric(metrics.DataReadCompletedRequests, "MemoryCycleController published data-read completions"),
            DataWriteAcceptedRequests: ProducerMetric(metrics.DataWriteAcceptedRequests, "MemoryCycleController requests with a future data-write property"),
            DataWriteCompletedRequests: ProducerMetric(metrics.DataWriteCompletedRequests, "MemoryCycleController completed requests with a future data-write property; not byte publication"),
            DataReadBytes: ProducerMetric(metrics.DataReadBytes, "Successful MemoryCycleController data-read completions"),
            CommittedDataWriteBytes: ProducerMetric(metrics.CommittedDataWriteBytes, "Selected-retire physical publication owner"),
            InstructionFetchAcceptedRequests: TelemetryMetric.Unavailable(
                "Producer schema v1: fetch is synchronous cache/main-memory access and has no controller request admission"),
            InstructionFetchCompletedRequests: TelemetryMetric.Unavailable(
                "Producer schema v1: fetch is synchronous cache/main-memory access and has no controller request completion"),
            InstructionFetchReadBytes: metrics.InstructionFetchReadBytesTelemetryAvailable
                ? TelemetryMetric.Available(metrics.InstructionFetchReadBytes, "Instruction cache physical 256-byte materialization owner")
                : TelemetryMetric.Unavailable("Instruction-fetch physical-read producer"),
            QueueFullRejects: ProducerMetric(metrics.MemoryQueueFullRejects, "MemoryCycleController capacity backpressure attempts"),
            BankConflictRejects: metrics.MemoryBankConflictRejectTelemetryAvailable
                ? TelemetryMetric.Available(metrics.MemoryBankConflictRejects, "MemoryCycleController bank-conflict admission")
                : TelemetryMetric.Unavailable("Controller-native admission has no bank-conflict reject result; scheduler and legacy conflict counters are distinct"));
    }
}
