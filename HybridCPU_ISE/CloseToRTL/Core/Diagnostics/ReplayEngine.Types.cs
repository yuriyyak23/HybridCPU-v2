using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Core
{
    /// <summary>
    /// Result of repeated-run replay-phase determinism validation.
    /// </summary>
    public readonly struct ReplayDeterminismReport
    {
        public ReplayDeterminismReport(
            bool isDeterministic,
            int comparedEvents,
            int comparedReplayEvents,
            int comparedTimelineSamples,
            int comparedInvalidationEvents,
            int comparedEpochs,
            int mismatchThreadId,
            long mismatchCycle,
            string mismatchField,
            string expectedValue,
            string actualValue)
        {
            IsDeterministic = isDeterministic;
            ComparedEvents = comparedEvents;
            ComparedReplayEvents = comparedReplayEvents;
            ComparedTimelineSamples = comparedTimelineSamples;
            ComparedInvalidationEvents = comparedInvalidationEvents;
            ComparedEpochs = comparedEpochs;
            MismatchThreadId = mismatchThreadId;
            MismatchCycle = mismatchCycle;
            MismatchField = mismatchField;
            ExpectedValue = expectedValue;
            ActualValue = actualValue;
            TypedSlotAgreementValid = true;
        }

        /// <summary>
        /// Phase 09: extended constructor with typed-slot agreement validation field.
        /// </summary>
        public ReplayDeterminismReport(
            bool isDeterministic,
            int comparedEvents,
            int comparedReplayEvents,
            int comparedTimelineSamples,
            int comparedInvalidationEvents,
            int comparedEpochs,
            int mismatchThreadId,
            long mismatchCycle,
            string mismatchField,
            string expectedValue,
            string actualValue,
            bool typedSlotAgreementValid)
        {
            IsDeterministic = isDeterministic;
            ComparedEvents = comparedEvents;
            ComparedReplayEvents = comparedReplayEvents;
            ComparedTimelineSamples = comparedTimelineSamples;
            ComparedInvalidationEvents = comparedInvalidationEvents;
            ComparedEpochs = comparedEpochs;
            MismatchThreadId = mismatchThreadId;
            MismatchCycle = mismatchCycle;
            MismatchField = mismatchField;
            ExpectedValue = expectedValue;
            ActualValue = actualValue;
            TypedSlotAgreementValid = typedSlotAgreementValid;
        }

        public bool IsDeterministic { get; }

        public int ComparedEvents { get; }

        public int ComparedReplayEvents { get; }

        public int ComparedTimelineSamples { get; }

        public int ComparedInvalidationEvents { get; }

        public int ComparedEpochs { get; }

        public int MismatchThreadId { get; }

        public long MismatchCycle { get; }

        public string MismatchField { get; }

        public string ExpectedValue { get; }

        public string ActualValue { get; }

        /// <summary>
        /// Phase 09: indicates whether typed-slot compiler/runtime agreement
        /// was valid across the replay comparison. <see langword="true"/> when
        /// compiler facts → runtime decisions → same lane bindings across runs.
        /// Defaults to <see langword="true"/> for backward compatibility.
        /// </summary>
        public bool TypedSlotAgreementValid { get; }

        public string Describe()
        {
            if (IsDeterministic)
            {
                return $"Deterministic across {ComparedEvents} events, {ComparedReplayEvents} replay-phase events, {ComparedTimelineSamples} dense timeline samples and {ComparedInvalidationEvents} invalidation samples.";
            }

            return $"Diverged at thread {MismatchThreadId}, cycle {MismatchCycle}, field '{MismatchField}': expected '{ExpectedValue}', actual '{ActualValue}'.";
        }
    }

    public enum ReplayEnvelopeKind : byte
    {
        TimingNoise,
        ResourcePressure,
        Mixed
    }

    /// <summary>
    /// Bounded perturbation envelope for repeated-run replay comparison.
    /// </summary>
    public readonly struct ReplayEnvelopeConfiguration
    {
        public ReplayEnvelopeConfiguration(
            ReplayEnvelopeKind kind,
            long maxCycleDrift,
            long maxMemorySubsystemCycleDrift,
            int maxActiveMemoryRequestDelta,
            int maxBankQueueDepthDelta,
            int maxReadyQueueDepthDelta)
        {
            Kind = kind;
            MaxCycleDrift = maxCycleDrift;
            MaxMemorySubsystemCycleDrift = maxMemorySubsystemCycleDrift;
            MaxActiveMemoryRequestDelta = maxActiveMemoryRequestDelta;
            MaxBankQueueDepthDelta = maxBankQueueDepthDelta;
            MaxReadyQueueDepthDelta = maxReadyQueueDepthDelta;
        }

        public ReplayEnvelopeKind Kind { get; }

        public long MaxCycleDrift { get; }

        public long MaxMemorySubsystemCycleDrift { get; }

        public int MaxActiveMemoryRequestDelta { get; }

        public int MaxBankQueueDepthDelta { get; }

        public int MaxReadyQueueDepthDelta { get; }

        public static ReplayEnvelopeConfiguration CreateTimingNoise(long maxCycleDrift)
        {
            return new ReplayEnvelopeConfiguration(ReplayEnvelopeKind.TimingNoise, maxCycleDrift, maxCycleDrift, 0, 0, 0);
        }

        public static ReplayEnvelopeConfiguration CreateResourcePressure(
            int maxActiveMemoryRequestDelta,
            int maxBankQueueDepthDelta,
            int maxReadyQueueDepthDelta)
        {
            return new ReplayEnvelopeConfiguration(
                ReplayEnvelopeKind.ResourcePressure,
                0,
                0,
                maxActiveMemoryRequestDelta,
                maxBankQueueDepthDelta,
                maxReadyQueueDepthDelta);
        }

        public static ReplayEnvelopeConfiguration CreateMixed(
            long maxCycleDrift,
            long maxMemorySubsystemCycleDrift,
            int maxActiveMemoryRequestDelta,
            int maxBankQueueDepthDelta,
            int maxReadyQueueDepthDelta)
        {
            return new ReplayEnvelopeConfiguration(
                ReplayEnvelopeKind.Mixed,
                maxCycleDrift,
                maxMemorySubsystemCycleDrift,
                maxActiveMemoryRequestDelta,
                maxBankQueueDepthDelta,
                maxReadyQueueDepthDelta);
        }
    }

    /// <summary>
    /// Result of bounded perturbation replay-envelope validation.
    /// </summary>
    public readonly struct ReplayEnvelopeReport
    {
        public ReplayEnvelopeReport(
            bool isWithinEnvelope,
            ReplayEnvelopeKind kind,
            int comparedEvents,
            int comparedReplayEvents,
            int comparedEnvelopeFields,
            int mismatchThreadId,
            long mismatchCycle,
            string mismatchField,
            string expectedValue,
            string actualValue,
            string allowedEnvelope)
        {
            IsWithinEnvelope = isWithinEnvelope;
            Kind = kind;
            ComparedEvents = comparedEvents;
            ComparedReplayEvents = comparedReplayEvents;
            ComparedEnvelopeFields = comparedEnvelopeFields;
            MismatchThreadId = mismatchThreadId;
            MismatchCycle = mismatchCycle;
            MismatchField = mismatchField;
            ExpectedValue = expectedValue;
            ActualValue = actualValue;
            AllowedEnvelope = allowedEnvelope;
        }

        public bool IsWithinEnvelope { get; }

        public ReplayEnvelopeKind Kind { get; }

        public int ComparedEvents { get; }

        public int ComparedReplayEvents { get; }

        public int ComparedEnvelopeFields { get; }

        public int MismatchThreadId { get; }

        public long MismatchCycle { get; }

        public string MismatchField { get; }

        public string ExpectedValue { get; }

        public string ActualValue { get; }

        public string AllowedEnvelope { get; }

        public string Describe()
        {
            if (IsWithinEnvelope)
            {
                return $"Within {Kind} envelope across {ComparedEvents} events, {ComparedReplayEvents} replay-phase events and {ComparedEnvelopeFields} bounded timing/resource comparisons.";
            }

            return $"Out of {Kind} envelope at thread {MismatchThreadId}, cycle {MismatchCycle}, field '{MismatchField}': expected '{ExpectedValue}', actual '{ActualValue}', allowed {AllowedEnvelope}.";
        }
    }

    /// <summary>
    /// Aggregated replay-heavy evidence extracted from a single trace.
    /// </summary>
    public readonly struct ReplayTraceEvidenceSummary
    {
        public ReplayTraceEvidenceSummary(
            int totalEvents,
            int replayPhaseEvents,
            int denseTimelineSamples,
            int writeBackSamples,
            int replayEpochCount,
            ulong totalEpochLength,
            ulong stableDonorSlotSamples,
            ulong totalReplaySlotSamples,
            long phaseCertificateReadyHits,
            long phaseCertificateReadyMisses,
            long estimatedPhaseCertificateChecksSaved,
            long phaseCertificateInvalidations,
            int invalidationObservations,
            int mutationInvalidationObservations,
            int phaseMismatchInvalidationObservations,
            long eligibilityMaskedCycles,
            long eligibilityMaskedReadyCandidates,
            int invalidationBursts,
            int longestInvalidationBurst)
        {
            TotalEvents = totalEvents;
            ReplayPhaseEvents = replayPhaseEvents;
            DenseTimelineSamples = denseTimelineSamples;
            WriteBackSamples = writeBackSamples;
            ReplayEpochCount = replayEpochCount;
            TotalEpochLength = totalEpochLength;
            StableDonorSlotSamples = stableDonorSlotSamples;
            TotalReplaySlotSamples = totalReplaySlotSamples;
            PhaseCertificateReadyHits = phaseCertificateReadyHits;
            PhaseCertificateReadyMisses = phaseCertificateReadyMisses;
            EstimatedPhaseCertificateChecksSaved = estimatedPhaseCertificateChecksSaved;
            PhaseCertificateInvalidations = phaseCertificateInvalidations;
            InvalidationObservations = invalidationObservations;
            MutationInvalidationObservations = mutationInvalidationObservations;
            PhaseMismatchInvalidationObservations = phaseMismatchInvalidationObservations;
            EligibilityMaskedCycles = eligibilityMaskedCycles;
            EligibilityMaskedReadyCandidates = eligibilityMaskedReadyCandidates;
            InvalidationBursts = invalidationBursts;
            LongestInvalidationBurst = longestInvalidationBurst;
        }

        public int TotalEvents { get; }

        public int ReplayPhaseEvents { get; }

        public int DenseTimelineSamples { get; }

        public int WriteBackSamples { get; }

        public int ReplayEpochCount { get; }

        public ulong TotalEpochLength { get; }

        public ulong StableDonorSlotSamples { get; }

        public ulong TotalReplaySlotSamples { get; }

        public long PhaseCertificateReadyHits { get; }

        public long PhaseCertificateReadyMisses { get; }

        public long EstimatedPhaseCertificateChecksSaved { get; }

        public long PhaseCertificateInvalidations { get; }

        public int InvalidationObservations { get; }

        public int MutationInvalidationObservations { get; }

        public int PhaseMismatchInvalidationObservations { get; }

        public long EligibilityMaskedCycles { get; }

        public long EligibilityMaskedReadyCandidates { get; }

        public int InvalidationBursts { get; }

        public int LongestInvalidationBurst { get; }

        public double AverageEpochLength => ReplayEpochCount == 0 ? 0.0 : (double)TotalEpochLength / ReplayEpochCount;

        public double StableDonorSlotRatio => TotalReplaySlotSamples == 0 ? 0.0 : (double)StableDonorSlotSamples / TotalReplaySlotSamples;

        public double DenseTimelineCoverage => ReplayPhaseEvents == 0 ? 0.0 : (double)DenseTimelineSamples / ReplayPhaseEvents;

        public double PhaseCertificateReuseHitRate
        {
            get
            {
                long totalReadinessChecks = PhaseCertificateReadyHits + PhaseCertificateReadyMisses;
                if (totalReadinessChecks == 0) return 0.0;
                return (double)PhaseCertificateReadyHits / totalReadinessChecks;
            }
        }

        public string Describe()
        {
            return $"Replay-heavy trace: {ReplayEpochCount} epochs, {ReplayPhaseEvents} replay events, {DenseTimelineSamples} dense timeline samples, reuse hit-rate {PhaseCertificateReuseHitRate:P2}, checks saved {EstimatedPhaseCertificateChecksSaved:N0}, eligibility-masked cycles {EligibilityMaskedCycles:N0}, masked ready candidates {EligibilityMaskedReadyCandidates:N0}, invalidation bursts {InvalidationBursts} (max {LongestInvalidationBurst}).";
        }
    }

    /// <summary>
    /// Per-epoch replay-heavy evidence for long practical kernel traces.
    /// </summary>
    public readonly struct ReplayEpochEvidenceSummary
    {
        public ReplayEpochEvidenceSummary(
            int threadId,
            ulong epochId,
            ulong epochLength,
            int eventCount,
            int denseTimelineSamples,
            int writeBackSamples,
            ulong stableDonorSlotSamples,
            ulong totalReplaySlotSamples,
            long phaseCertificateReadyHits,
            long phaseCertificateReadyMisses,
            long estimatedPhaseCertificateChecksSaved,
            long phaseCertificateInvalidations,
            int invalidationObservations,
            long eligibilityMaskedCycles,
            long eligibilityMaskedReadyCandidates,
            int longestInvalidationBurst,
            ReplayPhaseInvalidationReason terminalInvalidationReason)
        {
            ThreadId = threadId;
            EpochId = epochId;
            EpochLength = epochLength;
            EventCount = eventCount;
            DenseTimelineSamples = denseTimelineSamples;
            WriteBackSamples = writeBackSamples;
            StableDonorSlotSamples = stableDonorSlotSamples;
            TotalReplaySlotSamples = totalReplaySlotSamples;
            PhaseCertificateReadyHits = phaseCertificateReadyHits;
            PhaseCertificateReadyMisses = phaseCertificateReadyMisses;
            EstimatedPhaseCertificateChecksSaved = estimatedPhaseCertificateChecksSaved;
            PhaseCertificateInvalidations = phaseCertificateInvalidations;
            InvalidationObservations = invalidationObservations;
            EligibilityMaskedCycles = eligibilityMaskedCycles;
            EligibilityMaskedReadyCandidates = eligibilityMaskedReadyCandidates;
            LongestInvalidationBurst = longestInvalidationBurst;
            TerminalInvalidationReason = terminalInvalidationReason;
        }

        public int ThreadId { get; }

        public ulong EpochId { get; }

        public ulong EpochLength { get; }

        public int EventCount { get; }

        public int DenseTimelineSamples { get; }

        public int WriteBackSamples { get; }

        public ulong StableDonorSlotSamples { get; }

        public ulong TotalReplaySlotSamples { get; }

        public long PhaseCertificateReadyHits { get; }

        public long PhaseCertificateReadyMisses { get; }

        public long EstimatedPhaseCertificateChecksSaved { get; }

        public long PhaseCertificateInvalidations { get; }

        public int InvalidationObservations { get; }

        public long EligibilityMaskedCycles { get; }

        public long EligibilityMaskedReadyCandidates { get; }

        public int LongestInvalidationBurst { get; }

        public ReplayPhaseInvalidationReason TerminalInvalidationReason { get; }

        public double DenseTimelineCoverage => EventCount == 0 ? 0.0 : (double)DenseTimelineSamples / EventCount;

        public double StableDonorSlotRatio => TotalReplaySlotSamples == 0 ? 0.0 : (double)StableDonorSlotSamples / TotalReplaySlotSamples;

        public double PhaseCertificateReuseHitRate
        {
            get
            {
                long totalReadinessChecks = PhaseCertificateReadyHits + PhaseCertificateReadyMisses;
                if (totalReadinessChecks == 0) return 0.0;
                return (double)PhaseCertificateReadyHits / totalReadinessChecks;
            }
        }

        public string Describe()
        {
            return $"Epoch {EpochId} (thread {ThreadId}): {EventCount} events, dense coverage {DenseTimelineCoverage:P2}, reuse hit-rate {PhaseCertificateReuseHitRate:P2}, checks saved {EstimatedPhaseCertificateChecksSaved:N0}, eligibility-masked cycles {EligibilityMaskedCycles:N0}, masked ready candidates {EligibilityMaskedReadyCandidates:N0}, invalidation burst max {LongestInvalidationBurst}, terminal reason {TerminalInvalidationReason}.";
        }
    }
}