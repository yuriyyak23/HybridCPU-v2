using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YAKSys_Hybrid_CPU.Core.Diagnostics;

/// <summary>
/// Collects existing runtime telemetry counters from <see cref="MicroOpScheduler"/>
/// and serializes them as <see cref="TypedSlotTelemetryProfile"/> JSON.
/// <para>
/// This is an <b>additive-only</b> ISE-side class — no existing scheduling,
/// admission, or fairness logic is modified.
/// </para>
/// </summary>
public static class TelemetryExporter
{
    private const int SmtWays = 4;

    /// <summary>
    /// Builds a <see cref="TypedSlotTelemetryProfile"/> from the current scheduler state.
    /// </summary>
    /// <param name="scheduler">The micro-op scheduler with accumulated counters.</param>
    /// <param name="programHash">Hash of the compiled program for staleness detection.</param>
    /// <param name="workerMetrics">Optional per-worker metrics from Phase 07 parallel execution.</param>
    public static TypedSlotTelemetryProfile BuildProfile(
        MicroOpScheduler scheduler,
        string programHash,
        IReadOnlyDictionary<string, WorkerPerformanceMetrics>? workerMetrics = null)
    {
        return BuildProfile(scheduler, programHash, pipelineControl: null, workerMetrics);
    }

    /// <summary>
    /// Builds a <see cref="TypedSlotTelemetryProfile"/> from the current scheduler state and optional pipeline telemetry.
    /// </summary>
    public static TypedSlotTelemetryProfile BuildProfile(
        MicroOpScheduler scheduler,
        string programHash,
        Processor.CPU_Core.PipelineControl? pipelineControl,
        IReadOnlyDictionary<string, WorkerPerformanceMetrics>? workerMetrics = null)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(programHash);

        SchedulerPhaseMetrics metrics = scheduler.GetPhaseMetrics();
        var certificateRejectsPerClass = BuildCertificateRejectsPerClass(metrics);
        var smtLegalityRejectsPerClass =
            BuildSmtLegalityRejectsPerClass(metrics) ?? certificateRejectsPerClass;
        var perVtRejectionCounts = BuildPerVtRejectionCounts(metrics);
        var perVtRegGroupConflicts = BuildPerVtRegGroupConflicts(metrics);
        var bankPendingRejectsPerBank = BuildBankPendingRejectsPerBank(metrics);
        long? memoryClusteringEventCount = metrics.MemoryClusteringEvents > 0 ? metrics.MemoryClusteringEvents : null;
        long? bankConflictStallCycles = pipelineControl is { BankConflictStallCycles: > 0UL }
            ? checked((long)pipelineControl.Value.BankConflictStallCycles)
            : null;
        long? hazardRegisterDataCount = pipelineControl is { HazardRegisterDataCount: > 0UL }
            ? checked((long)pipelineControl.Value.HazardRegisterDataCount)
            : null;
        long? hazardMemoryBankCount = pipelineControl is { HazardMemoryBankCount: > 0UL }
            ? checked((long)pipelineControl.Value.HazardMemoryBankCount)
            : null;
        long? hazardControlFlowCount = pipelineControl is { HazardControlFlowCount: > 0UL }
            ? checked((long)pipelineControl.Value.HazardControlFlowCount)
            : null;
        long? hazardSystemBarrierCount = pipelineControl is { HazardSystemBarrierCount: > 0UL }
            ? checked((long)pipelineControl.Value.HazardSystemBarrierCount)
            : null;
        long? hazardPinnedLaneCount = pipelineControl is { HazardPinnedLaneCount: > 0UL }
            ? checked((long)pipelineControl.Value.HazardPinnedLaneCount)
            : null;
        long? crossDomainRejectCount = metrics.DomainIsolationCrossDomainBlocks > 0
            ? metrics.DomainIsolationCrossDomainBlocks
            : null;
        bool hasEligibilityTelemetry =
            scheduler.TotalSchedulerCycles > 0 ||
            metrics.EligibilityMaskedCycles > 0 ||
            metrics.EligibilityMaskedReadyCandidates > 0 ||
            metrics.LastEligibilityRequestedMask != 0 ||
            metrics.LastEligibilityNormalizedMask != 0 ||
            metrics.LastEligibilityReadyPortMask != 0 ||
            metrics.LastEligibilityVisibleReadyMask != 0 ||
            metrics.LastEligibilityMaskedReadyMask != 0;
        long? eligibilityMaskedCycles = hasEligibilityTelemetry
            ? metrics.EligibilityMaskedCycles
            : null;
        long? eligibilityMaskedReadyCandidates = hasEligibilityTelemetry
            ? metrics.EligibilityMaskedReadyCandidates
            : null;
        byte? lastEligibilityRequestedMask = hasEligibilityTelemetry
            ? metrics.LastEligibilityRequestedMask
            : null;
        byte? lastEligibilityNormalizedMask = hasEligibilityTelemetry
            ? metrics.LastEligibilityNormalizedMask
            : null;
        byte? lastEligibilityReadyPortMask = hasEligibilityTelemetry
            ? metrics.LastEligibilityReadyPortMask
            : null;
        byte? lastEligibilityVisibleReadyMask = hasEligibilityTelemetry
            ? metrics.LastEligibilityVisibleReadyMask
            : null;
        byte? lastEligibilityMaskedReadyMask = hasEligibilityTelemetry
            ? metrics.LastEligibilityMaskedReadyMask
            : null;
        IReadOnlyList<LoopPhaseClassProfile>? loopPhaseProfiles = metrics.LoopPhaseProfiles is { Count: > 0 }
            ? metrics.LoopPhaseProfiles
            : null;

        // Post-phase-05: heterogeneous retired width breakdown
        long? scalarLanesRetired = pipelineControl is { ScalarLanesRetired: > 0UL }
            ? checked((long)pipelineControl.Value.ScalarLanesRetired)
            : null;
        long? nonScalarLanesRetired = pipelineControl is { NonScalarLanesRetired: > 0UL }
            ? checked((long)pipelineControl.Value.NonScalarLanesRetired)
            : null;
        long? retireCycleCount = pipelineControl is { RetireCycleCount: > 0UL }
            ? checked((long)pipelineControl.Value.RetireCycleCount)
            : null;
        double? scalarIPC = pipelineControl.HasValue
            ? NullIfZero(pipelineControl.Value.GetScalarIPC())
            : null;
        double? averageRetiredWidth = pipelineControl.HasValue
            ? NullIfZero(pipelineControl.Value.GetAverageRetiredWidth())
            : null;

        // Per-class injection counts
        var injectionsPerClass = new Dictionary<SlotClass, long>
        {
            [SlotClass.AluClass] = metrics.AluClassInjects,
            [SlotClass.LsuClass] = metrics.LsuClassInjects,
            [SlotClass.DmaStreamClass] = metrics.DmaStreamClassInjects,
            [SlotClass.BranchControl] = metrics.BranchControlInjects,
            [SlotClass.SystemSingleton] = 0 // no dedicated counter; derived below
        };

        // Per-class reject counts (derived from disaggregated counters)
        var rejectsPerClass = new Dictionary<SlotClass, long>
        {
            [SlotClass.AluClass] = GetValueOrDefault(smtLegalityRejectsPerClass, SlotClass.AluClass),
            [SlotClass.LsuClass] = GetValueOrDefault(smtLegalityRejectsPerClass, SlotClass.LsuClass),
            [SlotClass.DmaStreamClass] = GetValueOrDefault(smtLegalityRejectsPerClass, SlotClass.DmaStreamClass),
            [SlotClass.BranchControl] = GetValueOrDefault(smtLegalityRejectsPerClass, SlotClass.BranchControl),
            [SlotClass.SystemSingleton] = GetValueOrDefault(smtLegalityRejectsPerClass, SlotClass.SystemSingleton)
        };

        // Rejects by reason
        var rejectsByReason = new Dictionary<TypedSlotRejectReason, long>
        {
            [TypedSlotRejectReason.StaticClassOvercommit] = metrics.StaticClassOvercommitRejects,
            [TypedSlotRejectReason.DynamicClassExhaustion] = metrics.DynamicClassExhaustionRejects,
            [TypedSlotRejectReason.ResourceConflict] = scheduler.TypedSlotResourceConflictRejects,
            [TypedSlotRejectReason.DomainReject] = metrics.TypedSlotDomainRejects,
            [TypedSlotRejectReason.ScoreboardReject] = scheduler.TypedSlotScoreboardRejects,
            [TypedSlotRejectReason.BankPendingReject] = scheduler.TypedSlotBankPendingRejects,
            [TypedSlotRejectReason.HardwareBudgetReject] = scheduler.TypedSlotHardwareBudgetRejects,
            [TypedSlotRejectReason.SpeculationBudgetReject] = scheduler.TypedSlotSpeculationBudgetRejects,
            [TypedSlotRejectReason.AssistQuotaReject] = scheduler.TypedSlotAssistQuotaRejects,
            [TypedSlotRejectReason.AssistBackpressureReject] = scheduler.TypedSlotAssistBackpressureRejects,
            [TypedSlotRejectReason.PinnedLaneConflict] = metrics.PinnedLaneConflicts,
            [TypedSlotRejectReason.LateBindingConflict] = metrics.LateBindingConflicts
        };

        // NOP density computation
        long totalNops = metrics.NopDueToPinnedConstraint
                       + metrics.NopDueToNoClassCapacity
                       + metrics.NopDueToResourceConflict
                       + metrics.NopDueToDynamicState;

        long totalBundles = scheduler.TotalSchedulerCycles;
        const int bundleWidth = 8;
        double nopDensity = totalBundles > 0
            ? (double)totalNops / (totalBundles * bundleWidth)
            : 0.0;
        double utilization = 1.0 - nopDensity;

        // Replay hit rate
        long replayHits = metrics.ClassTemplateReuseHits;
        long replayMisses = metrics.ClassTemplateInvalidations;
        double replayHitRate = (replayHits + replayMisses) > 0
            ? (double)replayHits / (replayHits + replayMisses)
            : 0.0;

        // Per-VT injection counts
        var perVtInjections = new Dictionary<int, long>(SmtWays);
        for (int vt = 0; vt < SmtWays; vt++)
        {
            perVtInjections[vt] = scheduler.GetPerVtInjectionCount(vt);
        }

        CertificatePressureMetrics? certificatePressure = null;
        if (certificateRejectsPerClass is not null || perVtRegGroupConflicts is not null)
        {
            certificatePressure = new CertificatePressureMetrics(certificateRejectsPerClass, perVtRegGroupConflicts);
        }

        return new TypedSlotTelemetryProfile(
            ProgramHash: programHash,
            TotalInjectionsPerClass: injectionsPerClass,
            TotalRejectsPerClass: rejectsPerClass,
            RejectsByReason: rejectsByReason,
            AverageNopDensity: nopDensity,
            AverageBundleUtilization: utilization,
            TotalBundlesExecuted: totalBundles,
            TotalNopsExecuted: totalNops,
            ReplayTemplateHits: replayHits,
            ReplayTemplateMisses: replayMisses,
            ReplayHitRate: replayHitRate,
            FairnessStarvationEvents: scheduler.FairnessStarvationEvents,
            PerVtInjectionCounts: perVtInjections,
            WorkerMetrics: workerMetrics)
        {
            CertificatePressure = certificatePressure,
            PerVtRejectionCounts = perVtRejectionCounts,
            PerVtRegGroupConflicts = perVtRegGroupConflicts,
            SmtLegalityRejectsPerClass = smtLegalityRejectsPerClass,
            BankPendingRejectsPerBank = bankPendingRejectsPerBank,
            MemoryClusteringEventCount = memoryClusteringEventCount,
            BankConflictStallCycles = bankConflictStallCycles,
            HazardRegisterDataCount = hazardRegisterDataCount,
            HazardMemoryBankCount = hazardMemoryBankCount,
            HazardControlFlowCount = hazardControlFlowCount,
            HazardSystemBarrierCount = hazardSystemBarrierCount,
            HazardPinnedLaneCount = hazardPinnedLaneCount,
            CrossDomainRejectCount = crossDomainRejectCount,
            LastSmtLegalityRejectKind = metrics.LastSmtLegalityRejectKind.ToString(),
            LastSmtLegalityAuthoritySource = metrics.LastSmtLegalityAuthoritySource.ToString(),
            SmtOwnerContextGuardRejects = metrics.SmtOwnerContextGuardRejects,
            SmtDomainGuardRejects = metrics.SmtDomainGuardRejects,
            SmtBoundaryGuardRejects = metrics.SmtBoundaryGuardRejects,
            SmtSharedResourceCertificateRejects = metrics.SmtSharedResourceCertificateRejects,
            SmtRegisterGroupCertificateRejects = metrics.SmtRegisterGroupCertificateRejects,
            EligibilityMaskedCycles = eligibilityMaskedCycles,
            EligibilityMaskedReadyCandidates = eligibilityMaskedReadyCandidates,
            LastEligibilityRequestedMask = lastEligibilityRequestedMask,
            LastEligibilityNormalizedMask = lastEligibilityNormalizedMask,
            LastEligibilityReadyPortMask = lastEligibilityReadyPortMask,
            LastEligibilityVisibleReadyMask = lastEligibilityVisibleReadyMask,
            LastEligibilityMaskedReadyMask = lastEligibilityMaskedReadyMask,
            LoopPhaseProfiles = loopPhaseProfiles,
            ScalarLanesRetired = scalarLanesRetired,
            NonScalarLanesRetired = nonScalarLanesRetired,
            RetireCycleCount = retireCycleCount,
            ScalarIPC = scalarIPC,
            AverageRetiredWidth = averageRetiredWidth
        };
    }

    private static Dictionary<SlotClass, long>? BuildCertificateRejectsPerClass(SchedulerPhaseMetrics metrics)
    {
        var rejectsPerClass = new Dictionary<SlotClass, long>(5);
        AddIfPositive(rejectsPerClass, SlotClass.AluClass, metrics.CertificateRejectByAluClass);
        AddIfPositive(rejectsPerClass, SlotClass.LsuClass, metrics.CertificateRejectByLsuClass);
        AddIfPositive(rejectsPerClass, SlotClass.DmaStreamClass, metrics.CertificateRejectByDmaStreamClass);
        AddIfPositive(rejectsPerClass, SlotClass.BranchControl, metrics.CertificateRejectByBranchControl);
        AddIfPositive(rejectsPerClass, SlotClass.SystemSingleton, metrics.CertificateRejectBySystemSingleton);
        return rejectsPerClass.Count == 0 ? null : rejectsPerClass;
    }

    private static Dictionary<SlotClass, long>? BuildSmtLegalityRejectsPerClass(SchedulerPhaseMetrics metrics)
    {
        var rejectsPerClass = new Dictionary<SlotClass, long>(5);
        AddIfPositive(rejectsPerClass, SlotClass.AluClass, metrics.SmtLegalityRejectByAluClass);
        AddIfPositive(rejectsPerClass, SlotClass.LsuClass, metrics.SmtLegalityRejectByLsuClass);
        AddIfPositive(rejectsPerClass, SlotClass.DmaStreamClass, metrics.SmtLegalityRejectByDmaStreamClass);
        AddIfPositive(rejectsPerClass, SlotClass.BranchControl, metrics.SmtLegalityRejectByBranchControl);
        AddIfPositive(rejectsPerClass, SlotClass.SystemSingleton, metrics.SmtLegalityRejectBySystemSingleton);
        return rejectsPerClass.Count == 0 ? null : rejectsPerClass;
    }

    private static Dictionary<int, long>? BuildPerVtRejectionCounts(SchedulerPhaseMetrics metrics)
    {
        var rejectsPerVt = new Dictionary<int, long>(SmtWays);
        AddIfPositive(rejectsPerVt, 0, metrics.RejectionsVT0);
        AddIfPositive(rejectsPerVt, 1, metrics.RejectionsVT1);
        AddIfPositive(rejectsPerVt, 2, metrics.RejectionsVT2);
        AddIfPositive(rejectsPerVt, 3, metrics.RejectionsVT3);
        return rejectsPerVt.Count == 0 ? null : rejectsPerVt;
    }

    private static Dictionary<int, long>? BuildPerVtRegGroupConflicts(SchedulerPhaseMetrics metrics)
    {
        var conflictsPerVt = new Dictionary<int, long>(SmtWays);
        AddIfPositive(conflictsPerVt, 0, metrics.RegGroupConflictsVT0 != 0 ? metrics.RegGroupConflictsVT0 : metrics.CertificateRegGroupConflictVT0);
        AddIfPositive(conflictsPerVt, 1, metrics.RegGroupConflictsVT1 != 0 ? metrics.RegGroupConflictsVT1 : metrics.CertificateRegGroupConflictVT1);
        AddIfPositive(conflictsPerVt, 2, metrics.RegGroupConflictsVT2 != 0 ? metrics.RegGroupConflictsVT2 : metrics.CertificateRegGroupConflictVT2);
        AddIfPositive(conflictsPerVt, 3, metrics.RegGroupConflictsVT3 != 0 ? metrics.RegGroupConflictsVT3 : metrics.CertificateRegGroupConflictVT3);
        return conflictsPerVt.Count == 0 ? null : conflictsPerVt;
    }

    private static Dictionary<int, long>? BuildBankPendingRejectsPerBank(SchedulerPhaseMetrics metrics)
    {
        var rejectsPerBank = new Dictionary<int, long>(16);
        AddIfPositive(rejectsPerBank, 0, metrics.BankPendingRejectBank0);
        AddIfPositive(rejectsPerBank, 1, metrics.BankPendingRejectBank1);
        AddIfPositive(rejectsPerBank, 2, metrics.BankPendingRejectBank2);
        AddIfPositive(rejectsPerBank, 3, metrics.BankPendingRejectBank3);
        AddIfPositive(rejectsPerBank, 4, metrics.BankPendingRejectBank4);
        AddIfPositive(rejectsPerBank, 5, metrics.BankPendingRejectBank5);
        AddIfPositive(rejectsPerBank, 6, metrics.BankPendingRejectBank6);
        AddIfPositive(rejectsPerBank, 7, metrics.BankPendingRejectBank7);
        AddIfPositive(rejectsPerBank, 8, metrics.BankPendingRejectBank8);
        AddIfPositive(rejectsPerBank, 9, metrics.BankPendingRejectBank9);
        AddIfPositive(rejectsPerBank, 10, metrics.BankPendingRejectBank10);
        AddIfPositive(rejectsPerBank, 11, metrics.BankPendingRejectBank11);
        AddIfPositive(rejectsPerBank, 12, metrics.BankPendingRejectBank12);
        AddIfPositive(rejectsPerBank, 13, metrics.BankPendingRejectBank13);
        AddIfPositive(rejectsPerBank, 14, metrics.BankPendingRejectBank14);
        AddIfPositive(rejectsPerBank, 15, metrics.BankPendingRejectBank15);
        return rejectsPerBank.Count == 0 ? null : rejectsPerBank;
    }

    private static long GetValueOrDefault(IReadOnlyDictionary<SlotClass, long>? values, SlotClass slotClass)
    {
        if (values is null)
        {
            return 0;
        }

        return values.TryGetValue(slotClass, out long value) ? value : 0;
    }

    private static void AddIfPositive<TKey>(IDictionary<TKey, long> dictionary, TKey key, long value) where TKey : notnull
    {
        if (value > 0)
        {
            dictionary[key] = value;
        }
    }

    private static double? NullIfZero(double value) => value > 0.0 ? value : null;

    /// <summary>
    /// Serializes a telemetry profile to a JSON string.
    /// </summary>
    public static string SerializeToJson(TypedSlotTelemetryProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return JsonSerializer.Serialize(profile, ProfileSerializerContext.Default.TypedSlotTelemetryProfile);
    }

    /// <summary>
    /// Deserializes a telemetry profile from a JSON string.
    /// Returns <c>null</c> if the JSON is invalid.
    /// </summary>
    public static TypedSlotTelemetryProfile? DeserializeFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize(json, ProfileSerializerContext.Default.TypedSlotTelemetryProfile);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// Source-generated JSON serialization context for telemetry profile types.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(TypedSlotTelemetryProfile))]
[JsonSerializable(typeof(CertificatePressureMetrics))]
[JsonSerializable(typeof(LoopPhaseClassProfile))]
[JsonSerializable(typeof(WorkerPerformanceMetrics))]
[JsonSerializable(typeof(Dictionary<SlotClass, long>))]
[JsonSerializable(typeof(Dictionary<TypedSlotRejectReason, long>))]
[JsonSerializable(typeof(Dictionary<int, long>))]
[JsonSerializable(typeof(List<LoopPhaseClassProfile>))]
[JsonSerializable(typeof(Dictionary<string, WorkerPerformanceMetrics>))]
internal partial class ProfileSerializerContext : JsonSerializerContext;
