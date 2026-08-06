namespace YAKSys_Hybrid_CPU
{
    public partial class PerformanceReport
    {
        /// <summary>
        /// Creates an additive interval for diagnostics that sample shared,
        /// cumulative producers more than once. Per-execution fields and
        /// last-value snapshots remain the current snapshot values.
        /// </summary>
        /// <remarks>
        /// Numeric-only compatibility counters have no availability carrier.
        /// A regression therefore fails closed instead of presenting a reset as
        /// measured zero or silently discarding pre-reset activity.
        /// </remarks>
        public PerformanceReport CreateAdditiveDiagnosticIntervalSince(PerformanceReport baseline)
        {
            ArgumentNullException.ThrowIfNull(baseline);

            PerformanceReport interval = CreateMemoryCycleTelemetryIntervalSince(baseline);

            // Legacy MemorySubsystem compatibility counters are cumulative
            // between Processor.ResetPerformanceCounters calls.
            interval.TotalBursts = SubtractMonotonicOrThrow(
                TotalBursts, baseline.TotalBursts, nameof(TotalBursts));
            interval.TotalBytesTransferred = SubtractMonotonicOrThrow(
                TotalBytesTransferred, baseline.TotalBytesTransferred, nameof(TotalBytesTransferred));

            // The pod scheduler survives CPU_Core.PrepareExecutionStart and
            // owns cumulative phase/typed-slot telemetry.
            interval.NopAvoided = SubtractMonotonicOrThrow(
                NopAvoided, baseline.NopAvoided, nameof(NopAvoided));
            interval.NopDueToNoClassCapacity = SubtractMonotonicOrThrow(
                NopDueToNoClassCapacity, baseline.NopDueToNoClassCapacity, nameof(NopDueToNoClassCapacity));
            interval.NopDueToPinnedConstraint = SubtractMonotonicOrThrow(
                NopDueToPinnedConstraint, baseline.NopDueToPinnedConstraint, nameof(NopDueToPinnedConstraint));
            interval.NopDueToResourceConflict = SubtractMonotonicOrThrow(
                NopDueToResourceConflict, baseline.NopDueToResourceConflict, nameof(NopDueToResourceConflict));
            interval.NopDueToDynamicState = SubtractMonotonicOrThrow(
                NopDueToDynamicState, baseline.NopDueToDynamicState, nameof(NopDueToDynamicState));
            interval.ClassFlexibleInjects = SubtractMonotonicOrThrow(
                ClassFlexibleInjects, baseline.ClassFlexibleInjects, nameof(ClassFlexibleInjects));
            interval.HardPinnedInjects = SubtractMonotonicOrThrow(
                HardPinnedInjects, baseline.HardPinnedInjects, nameof(HardPinnedInjects));
            interval.EligibilityMaskedCycles = SubtractMonotonicOrThrow(
                EligibilityMaskedCycles, baseline.EligibilityMaskedCycles, nameof(EligibilityMaskedCycles));
            interval.EligibilityMaskedReadyCandidates = SubtractMonotonicOrThrow(
                EligibilityMaskedReadyCandidates,
                baseline.EligibilityMaskedReadyCandidates,
                nameof(EligibilityMaskedReadyCandidates));
            interval.PhaseCertificateReadyHits = SubtractMonotonicOrThrow(
                PhaseCertificateReadyHits, baseline.PhaseCertificateReadyHits, nameof(PhaseCertificateReadyHits));
            interval.PhaseCertificateReadyMisses = SubtractMonotonicOrThrow(
                PhaseCertificateReadyMisses, baseline.PhaseCertificateReadyMisses, nameof(PhaseCertificateReadyMisses));
            interval.EstimatedPhaseCertificateChecksSaved = SubtractMonotonicOrThrow(
                EstimatedPhaseCertificateChecksSaved,
                baseline.EstimatedPhaseCertificateChecksSaved,
                nameof(EstimatedPhaseCertificateChecksSaved));
            interval.PhaseCertificateInvalidations = SubtractMonotonicOrThrow(
                PhaseCertificateInvalidations,
                baseline.PhaseCertificateInvalidations,
                nameof(PhaseCertificateInvalidations));
            interval.PhaseCertificateMutationInvalidations = SubtractMonotonicOrThrow(
                PhaseCertificateMutationInvalidations,
                baseline.PhaseCertificateMutationInvalidations,
                nameof(PhaseCertificateMutationInvalidations));
            interval.PhaseCertificatePhaseMismatchInvalidations = SubtractMonotonicOrThrow(
                PhaseCertificatePhaseMismatchInvalidations,
                baseline.PhaseCertificatePhaseMismatchInvalidations,
                nameof(PhaseCertificatePhaseMismatchInvalidations));
            interval.SmtOwnerContextGuardRejects = SubtractMonotonicOrThrow(
                SmtOwnerContextGuardRejects,
                baseline.SmtOwnerContextGuardRejects,
                nameof(SmtOwnerContextGuardRejects));
            interval.SmtDomainGuardRejects = SubtractMonotonicOrThrow(
                SmtDomainGuardRejects, baseline.SmtDomainGuardRejects, nameof(SmtDomainGuardRejects));
            interval.SmtBoundaryGuardRejects = SubtractMonotonicOrThrow(
                SmtBoundaryGuardRejects, baseline.SmtBoundaryGuardRejects, nameof(SmtBoundaryGuardRejects));
            interval.SmtSharedResourceCertificateRejects = SubtractMonotonicOrThrow(
                SmtSharedResourceCertificateRejects,
                baseline.SmtSharedResourceCertificateRejects,
                nameof(SmtSharedResourceCertificateRejects));
            interval.SmtRegisterGroupCertificateRejects = SubtractMonotonicOrThrow(
                SmtRegisterGroupCertificateRejects,
                baseline.SmtRegisterGroupCertificateRejects,
                nameof(SmtRegisterGroupCertificateRejects));
            interval.SmtLegalityRejectByAluClass = SubtractMonotonicOrThrow(
                SmtLegalityRejectByAluClass,
                baseline.SmtLegalityRejectByAluClass,
                nameof(SmtLegalityRejectByAluClass));
            interval.SmtLegalityRejectByLsuClass = SubtractMonotonicOrThrow(
                SmtLegalityRejectByLsuClass,
                baseline.SmtLegalityRejectByLsuClass,
                nameof(SmtLegalityRejectByLsuClass));
            interval.SmtLegalityRejectByDmaStreamClass = SubtractMonotonicOrThrow(
                SmtLegalityRejectByDmaStreamClass,
                baseline.SmtLegalityRejectByDmaStreamClass,
                nameof(SmtLegalityRejectByDmaStreamClass));
            interval.SmtLegalityRejectByBranchControl = SubtractMonotonicOrThrow(
                SmtLegalityRejectByBranchControl,
                baseline.SmtLegalityRejectByBranchControl,
                nameof(SmtLegalityRejectByBranchControl));
            interval.SmtLegalityRejectBySystemSingleton = SubtractMonotonicOrThrow(
                SmtLegalityRejectBySystemSingleton,
                baseline.SmtLegalityRejectBySystemSingleton,
                nameof(SmtLegalityRejectBySystemSingleton));

            // StreamRegisterFile statistics and warm-ingress telemetry are
            // cumulative for the lifetime of the shared memory subsystem.
            interval.L1BypassHits = SubtractMonotonicOrThrow(
                L1BypassHits, baseline.L1BypassHits, nameof(L1BypassHits));
            interval.ForegroundWarmAttempts = SubtractMonotonicOrThrow(
                ForegroundWarmAttempts, baseline.ForegroundWarmAttempts, nameof(ForegroundWarmAttempts));
            interval.ForegroundWarmSuccesses = SubtractMonotonicOrThrow(
                ForegroundWarmSuccesses, baseline.ForegroundWarmSuccesses, nameof(ForegroundWarmSuccesses));
            interval.ForegroundWarmReuseHits = SubtractMonotonicOrThrow(
                ForegroundWarmReuseHits, baseline.ForegroundWarmReuseHits, nameof(ForegroundWarmReuseHits));
            interval.ForegroundBypassHits = SubtractMonotonicOrThrow(
                ForegroundBypassHits, baseline.ForegroundBypassHits, nameof(ForegroundBypassHits));
            interval.AssistWarmAttempts = SubtractMonotonicOrThrow(
                AssistWarmAttempts, baseline.AssistWarmAttempts, nameof(AssistWarmAttempts));
            interval.AssistWarmSuccesses = SubtractMonotonicOrThrow(
                AssistWarmSuccesses, baseline.AssistWarmSuccesses, nameof(AssistWarmSuccesses));
            interval.AssistWarmReuseHits = SubtractMonotonicOrThrow(
                AssistWarmReuseHits, baseline.AssistWarmReuseHits, nameof(AssistWarmReuseHits));
            interval.AssistBypassHits = SubtractMonotonicOrThrow(
                AssistBypassHits, baseline.AssistBypassHits, nameof(AssistBypassHits));
            interval.StreamWarmTranslationRejects = SubtractMonotonicOrThrow(
                StreamWarmTranslationRejects,
                baseline.StreamWarmTranslationRejects,
                nameof(StreamWarmTranslationRejects));
            interval.StreamWarmBackendRejects = SubtractMonotonicOrThrow(
                StreamWarmBackendRejects,
                baseline.StreamWarmBackendRejects,
                nameof(StreamWarmBackendRejects));
            interval.AssistWarmResidentBudgetRejects = SubtractMonotonicOrThrow(
                AssistWarmResidentBudgetRejects,
                baseline.AssistWarmResidentBudgetRejects,
                nameof(AssistWarmResidentBudgetRejects));
            interval.AssistWarmLoadingBudgetRejects = SubtractMonotonicOrThrow(
                AssistWarmLoadingBudgetRejects,
                baseline.AssistWarmLoadingBudgetRejects,
                nameof(AssistWarmLoadingBudgetRejects));
            interval.AssistWarmNoVictimRejects = SubtractMonotonicOrThrow(
                AssistWarmNoVictimRejects,
                baseline.AssistWarmNoVictimRejects,
                nameof(AssistWarmNoVictimRejects));

            return interval;
        }

        private static long SubtractMonotonicOrThrow(long current, long baseline, string metricName)
        {
            if (current < baseline)
            {
                throw new InvalidOperationException(
                    $"Cumulative diagnostic metric '{metricName}' regressed from {baseline} to {current}; " +
                    "an exact interval cannot be reported because this compatibility field has no availability carrier.");
            }

            return current - baseline;
        }
    }
}
