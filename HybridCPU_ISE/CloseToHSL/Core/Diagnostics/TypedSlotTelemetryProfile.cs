using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Telemetry;

namespace YAKSys_Hybrid_CPU.Core.Diagnostics;

/// <summary>
/// Per-worker performance metrics captured during parallel decomposition execution (Phase 07).
/// </summary>
/// <param name="WorkerName">Identifier for the worker function (e.g., "worker_vt1").</param>
/// <param name="TotalCycles">Total scheduling cycles consumed by this worker.</param>
/// <param name="NopDensity">Fraction of NOP slots in worker bundles (0.0–1.0).</param>
/// <param name="RejectRate">Fraction of injection attempts rejected at runtime (0.0–1.0).</param>
/// <param name="BundlesExecuted">Number of bundles executed by this worker.</param>
/// <param name="NopsExecuted">Number of NOP slots in this worker's execution.</param>
public sealed record WorkerPerformanceMetrics(
    string WorkerName,
    long TotalCycles,
    double NopDensity,
    double RejectRate,
    long BundlesExecuted,
    long NopsExecuted);

/// <summary>
/// Certificate-pressure telemetry exported from the runtime scheduler.
/// </summary>
/// <param name="RejectsPerClass">Per-class structural-certificate rejection counts keyed by <see cref="SlotClass"/>.</param>
/// <param name="RegisterGroupConflictsPerVt">Per-VT register-group certificate conflicts keyed by VT ID.</param>
public sealed record CertificatePressureMetrics(
    IReadOnlyDictionary<SlotClass, long>? RejectsPerClass,
    IReadOnlyDictionary<int, long>? RegisterGroupConflictsPerVt);

/// <summary>
/// Offline telemetry profile from runtime execution.
/// Compiler uses this for heuristic tuning in subsequent compilations.
/// Serialized as JSON with convention: <c>{program_name}.typed_slot_profile.json</c>.
/// </summary>
/// <param name="ProgramHash">Hash of the source program for staleness detection.</param>
/// <param name="TotalInjectionsPerClass">Per-class successful injection counts.</param>
/// <param name="TotalRejectsPerClass">Per-class total rejection counts.</param>
/// <param name="RejectsByReason">Rejection counts keyed by <see cref="TypedSlotRejectReason"/>.</param>
/// <param name="AverageNopDensity">Average NOP density across all bundles (0.0–1.0).</param>
/// <param name="AverageBundleUtilization">Average bundle utilization (0.0–1.0).</param>
/// <param name="TotalBundlesExecuted">Total bundles executed at runtime.</param>
/// <param name="TotalNopsExecuted">Total NOP slots observed at runtime.</param>
/// <param name="ReplayTemplateHits">Level-1 class-template reuse hits.</param>
/// <param name="ReplayTemplateMisses">Class-template invalidations (misses).</param>
/// <param name="ReplayHitRate">Replay hit rate (0.0–1.0).</param>
/// <param name="FairnessStarvationEvents">Number of fairness starvation events.</param>
/// <param name="PerVtInjectionCounts">Per-VT injection distribution (keyed by VT ID).</param>
/// <param name="WorkerMetrics">Per-worker performance data from Phase 07 parallel execution.</param>
public sealed record TypedSlotTelemetryProfile(
    string ProgramHash,
    IReadOnlyDictionary<SlotClass, long> TotalInjectionsPerClass,
    IReadOnlyDictionary<SlotClass, long> TotalRejectsPerClass,
    IReadOnlyDictionary<TypedSlotRejectReason, long> RejectsByReason,
    double AverageNopDensity,
    double AverageBundleUtilization,
    long TotalBundlesExecuted,
    long TotalNopsExecuted,
    long ReplayTemplateHits,
    long ReplayTemplateMisses,
    double ReplayHitRate,
    long FairnessStarvationEvents,
    IReadOnlyDictionary<int, long> PerVtInjectionCounts,
    IReadOnlyDictionary<string, WorkerPerformanceMetrics>? WorkerMetrics)
{
    /// <summary>
    /// Optional certificate-pressure breakdown for per-class rejects and per-VT register-group conflicts.
    /// Null for older profiles and runs without certificate-pressure telemetry.
    /// </summary>
    public CertificatePressureMetrics? CertificatePressure { get; init; }

    /// <summary>
    /// Optional per-VT total rejection breakdown keyed by runtime VT ID.
    /// Null for older profiles and runs without Phase 3 per-VT rejection telemetry.
    /// </summary>
    public IReadOnlyDictionary<int, long>? PerVtRejectionCounts { get; init; }

    /// <summary>
    /// Optional per-VT register-group conflict breakdown keyed by runtime VT ID.
    /// Advisory-only telemetry for compiler diagnostics; not a fairness policy signal.
    /// Null for older profiles and runs without Phase 3 per-VT conflict telemetry.
    /// </summary>
    public IReadOnlyDictionary<int, long>? PerVtRegGroupConflicts { get; init; }

    /// <summary>
    /// Optional per-class SMT legality reject breakdown, including guard-plane and
    /// structural-certificate rejects hidden behind the compatibility ResourceConflict reason.
    /// Null for older profiles and runs without SMT legality rejects.
    /// </summary>
    public IReadOnlyDictionary<SlotClass, long>? SmtLegalityRejectsPerClass { get; init; }

    /// <summary>
    /// Optional bank-pending rejection breakdown keyed by runtime bank ID.
    /// Null for older profiles and runs without per-bank telemetry.
    /// </summary>
    public IReadOnlyDictionary<int, long>? BankPendingRejectsPerBank { get; init; }

    /// <summary>
    /// Optional count of decoded bundles where multiple memory ops targeted the same bank.
    /// Null for older profiles and runs without decode-side clustering telemetry.
    /// </summary>
    public long? MemoryClusteringEventCount { get; init; }

    /// <summary>
    /// Optional total cycles lost to same-bank outstanding memory conflicts in the pipeline.
    /// Null when pipeline-side telemetry was not supplied to the exporter.
    /// </summary>
    public long? BankConflictStallCycles { get; init; }

    /// <summary>
    /// Optional total hazard pairs dominated by register-data effects.
    /// Null when pipeline-side typed-effect telemetry was not supplied to the exporter.
    /// </summary>
    public long? HazardRegisterDataCount { get; init; }

    /// <summary>
    /// Optional total hazard pairs dominated by same-bank memory effects.
    /// Null when pipeline-side typed-effect telemetry was not supplied to the exporter.
    /// </summary>
    public long? HazardMemoryBankCount { get; init; }

    /// <summary>
    /// Optional total hazard pairs dominated by control-flow effects.
    /// Null when pipeline-side typed-effect telemetry was not supplied to the exporter.
    /// </summary>
    public long? HazardControlFlowCount { get; init; }

    /// <summary>
    /// Optional total hazard pairs dominated by system/barrier effects.
    /// Null when pipeline-side typed-effect telemetry was not supplied to the exporter.
    /// </summary>
    public long? HazardSystemBarrierCount { get; init; }

    /// <summary>
    /// Optional total hazard pairs dominated by pinned-lane collisions.
    /// Null when pipeline-side typed-effect telemetry was not supplied to the exporter.
    /// </summary>
    public long? HazardPinnedLaneCount { get; init; }

    /// <summary>
    /// Optional total cross-domain FSP rejections captured from runtime domain-isolation telemetry.
    /// Null for older profiles and runs without cross-domain breakdown export.
    /// </summary>
    public long? CrossDomainRejectCount { get; init; }

    /// <summary>
    /// Most recent SMT legality reject kind observed by typed-slot admission.
    /// Null for older profiles.
    /// </summary>
    public string? LastSmtLegalityRejectKind { get; init; }

    /// <summary>
    /// Most recent SMT legality authority source observed by typed-slot admission.
    /// Null for older profiles.
    /// </summary>
    public string? LastSmtLegalityAuthoritySource { get; init; }

    /// <summary>SMT owner-context guard-plane rejects hidden behind ResourceConflict.</summary>
    public long SmtOwnerContextGuardRejects { get; init; }

    /// <summary>SMT domain guard-plane rejects hidden behind ResourceConflict.</summary>
    public long SmtDomainGuardRejects { get; init; }

    /// <summary>SMT boundary guard-plane rejects.</summary>
    public long SmtBoundaryGuardRejects { get; init; }

    /// <summary>SMT shared-resource certificate rejects hidden behind ResourceConflict.</summary>
    public long SmtSharedResourceCertificateRejects { get; init; }

    /// <summary>SMT register-group certificate rejects hidden behind ResourceConflict.</summary>
    public long SmtRegisterGroupCertificateRejects { get; init; }

    /// <summary>
    /// Optional count of scheduler cycles where ready SMT candidates were masked out by the FSM-provided eligibility mask.
    /// Null for older profiles and runs without explicit eligibility export.
    /// </summary>
    public long? EligibilityMaskedCycles { get; init; }

    /// <summary>
    /// Optional count of ready SMT candidates hidden by the FSM-provided eligibility mask.
    /// Null for older profiles and runs without explicit eligibility export.
    /// </summary>
    public long? EligibilityMaskedReadyCandidates { get; init; }

    /// <summary>
    /// Optional last requested VT eligibility mask captured by the scheduler snapshot.
    /// Null for older profiles and runs without explicit eligibility export.
    /// </summary>
    public byte? LastEligibilityRequestedMask { get; init; }

    /// <summary>
    /// Optional last normalized VT eligibility mask captured by the scheduler snapshot.
    /// Null for older profiles and runs without explicit eligibility export.
    /// </summary>
    public byte? LastEligibilityNormalizedMask { get; init; }

    /// <summary>
    /// Optional last ready-port mask before eligibility filtering.
    /// Null for older profiles and runs without explicit eligibility export.
    /// </summary>
    public byte? LastEligibilityReadyPortMask { get; init; }

    /// <summary>
    /// Optional last visible-ready mask after eligibility filtering.
    /// Null for older profiles and runs without explicit eligibility export.
    /// </summary>
    public byte? LastEligibilityVisibleReadyMask { get; init; }

    /// <summary>
    /// Optional last masked-ready subset removed by eligibility filtering.
    /// Null for older profiles and runs without explicit eligibility export.
    /// </summary>
    public byte? LastEligibilityMaskedReadyMask { get; init; }

    /// <summary>
    /// Optional per-loop replay/class-capacity variance profiles for the hottest observed loop phases.
    /// Null for older profiles and runs without loop-phase sampling enabled.
    /// </summary>
    public IReadOnlyList<LoopPhaseClassProfile>? LoopPhaseProfiles { get; init; }

    /// <summary>
    /// Optional descriptor-backed lane6 DmaStreamCompute telemetry.
    /// Observation only: exported values cannot authorize replay, execution, or commit.
    /// </summary>
    public DmaStreamComputeTelemetrySnapshot? DmaStreamComputeTelemetry { get; init; }

    /// <summary>
    /// Optional descriptor-backed lane7 L7-SDC accelerator telemetry.
    /// Observation only: snapshots are not guard credentials and cannot authorize
    /// descriptor, capability, submit, execution, commit, or exception publication.
    /// </summary>
    public AcceleratorTelemetrySnapshot? AcceleratorTelemetry { get; init; }

    // ── Post-phase-05: heterogeneous retired width breakdown ──────────────

    /// <summary>
    /// Total lanes retired from scalar positions (lanes 0..3 + legacy early-exit control-flow).
    /// Null when pipeline-side telemetry was not supplied to the exporter.
    /// </summary>
    public long? ScalarLanesRetired { get; init; }

    /// <summary>
    /// Total lanes retired from widened non-scalar positions (lanes 4..5 LSU).
    /// Null when pipeline-side telemetry was not supplied to the exporter.
    /// </summary>
    public long? NonScalarLanesRetired { get; init; }

    /// <summary>
    /// Number of cycles where at least one lane retired (for computing average retired width).
    /// Null when pipeline-side telemetry was not supplied to the exporter.
    /// </summary>
    public long? RetireCycleCount { get; init; }

    /// <summary>
    /// Scalar-only IPC — <see cref="ScalarLanesRetired"/> / total cycles.
    /// Secondary metric; total IPC still uses <see cref="TypedSlotTelemetryProfile"/> TotalBundlesExecuted path.
    /// Null when pipeline-side telemetry was not supplied to the exporter.
    /// </summary>
    public double? ScalarIPC { get; init; }

    /// <summary>
    /// Average retired lane count per retire-active cycle. Reflects true heterogeneous retired width.
    /// Null when pipeline-side telemetry was not supplied to the exporter.
    /// </summary>
    public double? AverageRetiredWidth { get; init; }
}
