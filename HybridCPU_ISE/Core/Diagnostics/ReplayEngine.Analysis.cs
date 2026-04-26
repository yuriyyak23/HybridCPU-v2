using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Core
{
    public partial class ReplayEngine
    {
        /// </summary>
        public ReplayEnvelopeReport CompareReplayPhaseBehaviorWithinEnvelope(ReplayEngine other, ReplayEnvelopeConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(other);

            int comparedEvents = 0;
            int comparedReplayEvents = 0;
            int comparedEnvelopeFields = 0;

            for (int threadId = 0; threadId < 16; threadId++)
            {
                var baselineTrace = _perThreadTraces[threadId];
                var candidateTrace = other._perThreadTraces[threadId];

                if (baselineTrace.Count != candidateTrace.Count)
                {
                    return new ReplayEnvelopeReport(
                        false,
                        configuration.Kind,
                        comparedEvents,
                        comparedReplayEvents,
                        comparedEnvelopeFields,
                        threadId,
                        -1,
                        "EventCount",
                        baselineTrace.Count.ToString(),
                        candidateTrace.Count.ToString(),
                        "exact match");
                }

                for (int index = 0; index < baselineTrace.Count; index++)
                {
                    FullStateTraceEvent baselineEvent = baselineTrace[index];
                    FullStateTraceEvent candidateEvent = candidateTrace[index];

                    if (!TryCompareReplayContractEvent(baselineEvent, candidateEvent, out string mismatchField, out string expectedValue, out string actualValue))
                    {
                        return new ReplayEnvelopeReport(
                            false,
                            configuration.Kind,
                            comparedEvents,
                            comparedReplayEvents,
                            comparedEnvelopeFields,
                            threadId,
                            baselineEvent.CycleNumber,
                            mismatchField,
                            expectedValue,
                            actualValue,
                            "exact match");
                    }

                    if (!TryCompareEventWithinEnvelope(
                        baselineEvent,
                        candidateEvent,
                        configuration,
                        out mismatchField,
                        out expectedValue,
                        out actualValue,
                        out string allowedEnvelope,
                        out int envelopeChecks))
                    {
                        return new ReplayEnvelopeReport(
                            false,
                            configuration.Kind,
                            comparedEvents,
                            comparedReplayEvents,
                            comparedEnvelopeFields,
                            threadId,
                            baselineEvent.CycleNumber,
                            mismatchField,
                            expectedValue,
                            actualValue,
                            allowedEnvelope);
                    }

                    comparedEvents++;
                    comparedEnvelopeFields += envelopeChecks;
                    if (baselineEvent.ReplayEpochId != 0)
                    {
                        comparedReplayEvents++;
                    }
                }
            }

            return new ReplayEnvelopeReport(
                true,
                configuration.Kind,
                comparedEvents,
                comparedReplayEvents,
                comparedEnvelopeFields,
                -1,
                -1,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        /// <summary>
        /// Aggregate replay-heavy trace evidence for practical Phase 1 validation passes.
        /// </summary>
        public ReplayTraceEvidenceSummary SummarizeReplayPhaseEvidence()
        {
            int totalEvents = 0;
            int replayPhaseEvents = 0;
            int denseTimelineSamples = 0;
            int writeBackSamples = 0;
            int replayEpochCount = 0;
            ulong totalEpochLength = 0;
            ulong stableDonorSlotSamples = 0;
            ulong totalReplaySlotSamples = 0;
            long phaseCertificateReadyHits = 0;
            long phaseCertificateReadyMisses = 0;
            long estimatedPhaseCertificateChecksSaved = 0;
            long phaseCertificateInvalidations = 0;
            int invalidationObservations = 0;
            int mutationInvalidationObservations = 0;
            int phaseMismatchInvalidationObservations = 0;
            long eligibilityMaskedCycles = 0;
            long eligibilityMaskedReadyCandidates = 0;
            int invalidationBursts = 0;
            int longestInvalidationBurst = 0;

            foreach (var trace in _perThreadTraces.Values)
            {
                ulong lastEpochId = 0;
                int currentInvalidationBurst = 0;

                foreach (var evt in trace)
                {
                    totalEvents++;

                    if (evt.ReplayEpochId != 0)
                    {
                        replayPhaseEvents++;
                        stableDonorSlotSamples += (ulong)CountBits(evt.StableDonorMask);
                        totalReplaySlotSamples += 8;

                        if (evt.ReplayEpochId != lastEpochId)
                        {
                            replayEpochCount++;
                            totalEpochLength += evt.ReplayEpochLength;
                            lastEpochId = evt.ReplayEpochId;
                        }
                    }
                    else
                    {
                        lastEpochId = 0;
                    }

                    if (IsDensePhaseTimelineSample(evt))
                    {
                        denseTimelineSamples++;
                    }

                    if (string.Equals(evt.PipelineStage, "WB", StringComparison.Ordinal))
                    {
                        writeBackSamples++;
                    }

                    phaseCertificateReadyHits = Math.Max(phaseCertificateReadyHits, evt.PhaseCertificateReadyHits);
                    phaseCertificateReadyMisses = Math.Max(phaseCertificateReadyMisses, evt.PhaseCertificateReadyMisses);
                    estimatedPhaseCertificateChecksSaved = Math.Max(estimatedPhaseCertificateChecksSaved, evt.EstimatedPhaseCertificateChecksSaved);
                    phaseCertificateInvalidations = Math.Max(phaseCertificateInvalidations, evt.PhaseCertificateInvalidations);
                    eligibilityMaskedCycles = Math.Max(eligibilityMaskedCycles, evt.EligibilityMaskedCycles);
                    eligibilityMaskedReadyCandidates = Math.Max(eligibilityMaskedReadyCandidates, evt.EligibilityMaskedReadyCandidates);

                    bool hasInvalidation = HasInvalidationSignal(evt);
                    if (hasInvalidation)
                    {
                        invalidationObservations++;
                        currentInvalidationBurst++;

                        ReplayPhaseInvalidationReason dominantReason = evt.PhaseCertificateInvalidationReason != ReplayPhaseInvalidationReason.None
                            ? evt.PhaseCertificateInvalidationReason
                            : evt.ReplayInvalidationReason;

                        if (dominantReason == ReplayPhaseInvalidationReason.CertificateMutation)
                        {
                            mutationInvalidationObservations++;
                        }
                        else if (dominantReason == ReplayPhaseInvalidationReason.PhaseMismatch)
                        {
                            phaseMismatchInvalidationObservations++;
                        }
                    }
                    else if (currentInvalidationBurst > 0)
                    {
                        invalidationBursts++;
                        longestInvalidationBurst = Math.Max(longestInvalidationBurst, currentInvalidationBurst);
                        currentInvalidationBurst = 0;
                    }
                }

                if (currentInvalidationBurst > 0)
                {
                    invalidationBursts++;
                    longestInvalidationBurst = Math.Max(longestInvalidationBurst, currentInvalidationBurst);
                }
            }

            return new ReplayTraceEvidenceSummary(
                totalEvents,
                replayPhaseEvents,
                denseTimelineSamples,
                writeBackSamples,
                replayEpochCount,
                totalEpochLength,
                stableDonorSlotSamples,
                totalReplaySlotSamples,
                phaseCertificateReadyHits,
                phaseCertificateReadyMisses,
                estimatedPhaseCertificateChecksSaved,
                phaseCertificateInvalidations,
                invalidationObservations,
                mutationInvalidationObservations,
                phaseMismatchInvalidationObservations,
                eligibilityMaskedCycles,
                eligibilityMaskedReadyCandidates,
                invalidationBursts,
                longestInvalidationBurst);
        }

        /// <summary>
        /// Aggregate replay-heavy evidence per epoch for long practical trace analysis.
        /// </summary>
        public ReplayEpochEvidenceSummary[] SummarizeReplayEpochEvidence()
        {
            var epochSummaries = new List<ReplayEpochEvidenceSummary>();

            foreach (var kvp in _perThreadTraces)
            {
                int threadId = kvp.Key;
                var trace = kvp.Value;

                ulong currentEpochId = 0;
                ulong currentEpochLength = 0;
                int eventCount = 0;
                int denseTimelineSamples = 0;
                int writeBackSamples = 0;
                ulong stableDonorSlotSamples = 0;
                ulong totalReplaySlotSamples = 0;
                long phaseCertificateReadyHits = 0;
                long phaseCertificateReadyMisses = 0;
                long estimatedPhaseCertificateChecksSaved = 0;
                long phaseCertificateInvalidations = 0;
                int invalidationObservations = 0;
                long eligibilityMaskedCycles = 0;
                long eligibilityMaskedReadyCandidates = 0;
                int longestInvalidationBurst = 0;
                int currentInvalidationBurst = 0;
                ReplayPhaseInvalidationReason terminalInvalidationReason = ReplayPhaseInvalidationReason.None;

                void FinalizeEpoch()
                {
                    if (currentEpochId == 0)
                    {
                        return;
                    }

                    if (currentInvalidationBurst > 0)
                    {
                        longestInvalidationBurst = Math.Max(longestInvalidationBurst, currentInvalidationBurst);
                        currentInvalidationBurst = 0;
                    }

                    epochSummaries.Add(new ReplayEpochEvidenceSummary(
                        threadId,
                        currentEpochId,
                        currentEpochLength,
                        eventCount,
                        denseTimelineSamples,
                        writeBackSamples,
                        stableDonorSlotSamples,
                        totalReplaySlotSamples,
                        phaseCertificateReadyHits,
                        phaseCertificateReadyMisses,
                        estimatedPhaseCertificateChecksSaved,
                        phaseCertificateInvalidations,
                        invalidationObservations,
                        eligibilityMaskedCycles,
                        eligibilityMaskedReadyCandidates,
                        longestInvalidationBurst,
                        terminalInvalidationReason));

                    currentEpochId = 0;
                    currentEpochLength = 0;
                    eventCount = 0;
                    denseTimelineSamples = 0;
                    writeBackSamples = 0;
                    stableDonorSlotSamples = 0;
                    totalReplaySlotSamples = 0;
                    phaseCertificateReadyHits = 0;
                    phaseCertificateReadyMisses = 0;
                    estimatedPhaseCertificateChecksSaved = 0;
                    phaseCertificateInvalidations = 0;
                    invalidationObservations = 0;
                    eligibilityMaskedCycles = 0;
                    eligibilityMaskedReadyCandidates = 0;
                    longestInvalidationBurst = 0;
                    terminalInvalidationReason = ReplayPhaseInvalidationReason.None;
                }

                foreach (var evt in trace)
                {
                    if (evt.ReplayEpochId == 0)
                    {
                        FinalizeEpoch();
                        continue;
                    }

                    if (currentEpochId != evt.ReplayEpochId)
                    {
                        FinalizeEpoch();
                        currentEpochId = evt.ReplayEpochId;
                        currentEpochLength = evt.ReplayEpochLength;
                    }

                    eventCount++;
                    stableDonorSlotSamples += (ulong)CountBits(evt.StableDonorMask);
                    totalReplaySlotSamples += 8;

                    if (IsDensePhaseTimelineSample(evt))
                    {
                        denseTimelineSamples++;
                    }

                    if (string.Equals(evt.PipelineStage, "WB", StringComparison.Ordinal))
                    {
                        writeBackSamples++;
                    }

                    phaseCertificateReadyHits = Math.Max(phaseCertificateReadyHits, evt.PhaseCertificateReadyHits);
                    phaseCertificateReadyMisses = Math.Max(phaseCertificateReadyMisses, evt.PhaseCertificateReadyMisses);
                    estimatedPhaseCertificateChecksSaved = Math.Max(estimatedPhaseCertificateChecksSaved, evt.EstimatedPhaseCertificateChecksSaved);
                    phaseCertificateInvalidations = Math.Max(phaseCertificateInvalidations, evt.PhaseCertificateInvalidations);
                    eligibilityMaskedCycles = Math.Max(eligibilityMaskedCycles, evt.EligibilityMaskedCycles);
                    eligibilityMaskedReadyCandidates = Math.Max(eligibilityMaskedReadyCandidates, evt.EligibilityMaskedReadyCandidates);

                    if (HasInvalidationSignal(evt))
                    {
                        invalidationObservations++;
                        currentInvalidationBurst++;
                        terminalInvalidationReason = GetDominantInvalidationReason(evt);
                    }
                    else if (currentInvalidationBurst > 0)
                    {
                        longestInvalidationBurst = Math.Max(longestInvalidationBurst, currentInvalidationBurst);
                        currentInvalidationBurst = 0;
                    }
                }

                FinalizeEpoch();
            }

            return epochSummaries.ToArray();
        }

        private static bool HasInvalidationSignal(FullStateTraceEvent evt)
        {
            return evt.ReplayInvalidationReason != ReplayPhaseInvalidationReason.None ||
                   evt.PhaseCertificateInvalidationReason != ReplayPhaseInvalidationReason.None;
        }

        private static ReplayPhaseInvalidationReason GetDominantInvalidationReason(FullStateTraceEvent evt)
        {
            return evt.PhaseCertificateInvalidationReason != ReplayPhaseInvalidationReason.None
                ? evt.PhaseCertificateInvalidationReason
                : evt.ReplayInvalidationReason;
        }

        private static bool IsDensePhaseTimelineSample(FullStateTraceEvent evt)
        {
            return HasBasePolicy(evt.CurrentFSPPolicy, DenseTimelinePolicyName) &&
                   (string.Equals(evt.PipelineStage, "CYCLE", StringComparison.Ordinal) ||
                    string.Equals(evt.PipelineStage, "STALL", StringComparison.Ordinal));
        }

        private static bool HasBasePolicy(string? policy, string expectedPolicy)
        {
            if (string.IsNullOrEmpty(policy))
            {
                return false;
            }

            ReadOnlySpan<char> policySpan = policy.AsSpan();
            int separatorIndex = policySpan.IndexOf('|');
            if (separatorIndex >= 0)
            {
                policySpan = policySpan[..separatorIndex];
            }

            return policySpan.Equals(expectedPolicy.AsSpan(), StringComparison.Ordinal);
        }

        private static bool TryCompareReplayContractEvent(
            FullStateTraceEvent expected,
            FullStateTraceEvent actual,
            out string mismatchField,
            out string expectedValue,
            out string actualValue)
        {
            if (expected.ThreadId != actual.ThreadId)
            {
                return Mismatch("ThreadId", expected.ThreadId, actual.ThreadId, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.PC != actual.PC)
            {
                return Mismatch("PC", expected.PC, actual.PC, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.BundleId != actual.BundleId)
            {
                return Mismatch("BundleId", expected.BundleId, actual.BundleId, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.OpIndex != actual.OpIndex)
            {
                return Mismatch("OpIndex", expected.OpIndex, actual.OpIndex, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.Opcode != actual.Opcode)
            {
                return Mismatch("Opcode", expected.Opcode, actual.Opcode, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.WasStolenSlot != actual.WasStolenSlot)
            {
                return Mismatch("WasStolenSlot", expected.WasStolenSlot, actual.WasStolenSlot, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.OriginalThreadId != actual.OriginalThreadId)
            {
                return Mismatch("OriginalThreadId", expected.OriginalThreadId, actual.OriginalThreadId, out mismatchField, out expectedValue, out actualValue);
            }

            if (!string.Equals(expected.PipelineStage, actual.PipelineStage, StringComparison.Ordinal))
            {
                return Mismatch("PipelineStage", expected.PipelineStage, actual.PipelineStage, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.Stalled != actual.Stalled)
            {
                return Mismatch("Stalled", expected.Stalled, actual.Stalled, out mismatchField, out expectedValue, out actualValue);
            }

            if (!string.Equals(expected.StallReason, actual.StallReason, StringComparison.Ordinal))
            {
                return Mismatch("StallReason", expected.StallReason, actual.StallReason, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.DecodedBundleStateOwnerKind != actual.DecodedBundleStateOwnerKind)
            {
                return Mismatch("DecodedBundleStateOwnerKind", expected.DecodedBundleStateOwnerKind, actual.DecodedBundleStateOwnerKind, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.DecodedBundleStateEpoch != actual.DecodedBundleStateEpoch)
            {
                return Mismatch("DecodedBundleStateEpoch", expected.DecodedBundleStateEpoch, actual.DecodedBundleStateEpoch, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.DecodedBundleStateVersion != actual.DecodedBundleStateVersion)
            {
                return Mismatch("DecodedBundleStateVersion", expected.DecodedBundleStateVersion, actual.DecodedBundleStateVersion, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.DecodedBundleStateKind != actual.DecodedBundleStateKind)
            {
                return Mismatch("DecodedBundleStateKind", expected.DecodedBundleStateKind, actual.DecodedBundleStateKind, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.DecodedBundleStateOrigin != actual.DecodedBundleStateOrigin)
            {
                return Mismatch("DecodedBundleStateOrigin", expected.DecodedBundleStateOrigin, actual.DecodedBundleStateOrigin, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.DecodedBundlePc != actual.DecodedBundlePc)
            {
                return Mismatch("DecodedBundlePc", expected.DecodedBundlePc, actual.DecodedBundlePc, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.DecodedBundleValidMask != actual.DecodedBundleValidMask)
            {
                return Mismatch("DecodedBundleValidMask", expected.DecodedBundleValidMask, actual.DecodedBundleValidMask, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.DecodedBundleNopMask != actual.DecodedBundleNopMask)
            {
                return Mismatch("DecodedBundleNopMask", expected.DecodedBundleNopMask, actual.DecodedBundleNopMask, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.DecodedBundleHasCanonicalDecode != actual.DecodedBundleHasCanonicalDecode)
            {
                return Mismatch("DecodedBundleHasCanonicalDecode", expected.DecodedBundleHasCanonicalDecode, actual.DecodedBundleHasCanonicalDecode, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.DecodedBundleHasCanonicalLegality != actual.DecodedBundleHasCanonicalLegality)
            {
                return Mismatch("DecodedBundleHasCanonicalLegality", expected.DecodedBundleHasCanonicalLegality, actual.DecodedBundleHasCanonicalLegality, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.DecodedBundleHasDecodeFault != actual.DecodedBundleHasDecodeFault)
            {
                return Mismatch("DecodedBundleHasDecodeFault", expected.DecodedBundleHasDecodeFault, actual.DecodedBundleHasDecodeFault, out mismatchField, out expectedValue, out actualValue);
            }

            if (!string.Equals(expected.CurrentFSPPolicy, actual.CurrentFSPPolicy, StringComparison.Ordinal))
            {
                return Mismatch("CurrentFSPPolicy", expected.CurrentFSPPolicy, actual.CurrentFSPPolicy, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.ReplayEpochId != actual.ReplayEpochId)
            {
                return Mismatch("ReplayEpochId", expected.ReplayEpochId, actual.ReplayEpochId, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.ReplayPhaseCachedPc != actual.ReplayPhaseCachedPc)
            {
                return Mismatch("ReplayPhaseCachedPc", expected.ReplayPhaseCachedPc, actual.ReplayPhaseCachedPc, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.ReplayEpochLength != actual.ReplayEpochLength)
            {
                return Mismatch("ReplayEpochLength", expected.ReplayEpochLength, actual.ReplayEpochLength, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.ReplayPhaseValidSlotCount != actual.ReplayPhaseValidSlotCount)
            {
                return Mismatch("ReplayPhaseValidSlotCount", expected.ReplayPhaseValidSlotCount, actual.ReplayPhaseValidSlotCount, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.StableDonorMask != actual.StableDonorMask)
            {
                return Mismatch("StableDonorMask", expected.StableDonorMask, actual.StableDonorMask, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.ReplayInvalidationReason != actual.ReplayInvalidationReason)
            {
                return Mismatch("ReplayInvalidationReason", expected.ReplayInvalidationReason, actual.ReplayInvalidationReason, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.PhaseCertificateTemplateReusable != actual.PhaseCertificateTemplateReusable)
            {
                return Mismatch("PhaseCertificateTemplateReusable", expected.PhaseCertificateTemplateReusable, actual.PhaseCertificateTemplateReusable, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.PhaseCertificateReadyHits != actual.PhaseCertificateReadyHits)
            {
                return Mismatch("PhaseCertificateReadyHits", expected.PhaseCertificateReadyHits, actual.PhaseCertificateReadyHits, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.PhaseCertificateReadyMisses != actual.PhaseCertificateReadyMisses)
            {
                return Mismatch("PhaseCertificateReadyMisses", expected.PhaseCertificateReadyMisses, actual.PhaseCertificateReadyMisses, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.EstimatedPhaseCertificateChecksSaved != actual.EstimatedPhaseCertificateChecksSaved)
            {
                return Mismatch("EstimatedPhaseCertificateChecksSaved", expected.EstimatedPhaseCertificateChecksSaved, actual.EstimatedPhaseCertificateChecksSaved, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.PhaseCertificateInvalidations != actual.PhaseCertificateInvalidations)
            {
                return Mismatch("PhaseCertificateInvalidations", expected.PhaseCertificateInvalidations, actual.PhaseCertificateInvalidations, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.PhaseCertificateInvalidationReason != actual.PhaseCertificateInvalidationReason)
            {
                return Mismatch("PhaseCertificateInvalidationReason", expected.PhaseCertificateInvalidationReason, actual.PhaseCertificateInvalidationReason, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.DeterminismReferenceOpportunitySlots != actual.DeterminismReferenceOpportunitySlots)
            {
                return Mismatch("DeterminismReferenceOpportunitySlots", expected.DeterminismReferenceOpportunitySlots, actual.DeterminismReferenceOpportunitySlots, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.DeterminismReplayEligibleSlots != actual.DeterminismReplayEligibleSlots)
            {
                return Mismatch("DeterminismReplayEligibleSlots", expected.DeterminismReplayEligibleSlots, actual.DeterminismReplayEligibleSlots, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.DeterminismMaskedSlots != actual.DeterminismMaskedSlots)
            {
                return Mismatch("DeterminismMaskedSlots", expected.DeterminismMaskedSlots, actual.DeterminismMaskedSlots, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.DeterminismEstimatedLostSlots != actual.DeterminismEstimatedLostSlots)
            {
                return Mismatch("DeterminismEstimatedLostSlots", expected.DeterminismEstimatedLostSlots, actual.DeterminismEstimatedLostSlots, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.DeterminismConstrainedCycles != actual.DeterminismConstrainedCycles)
            {
                return Mismatch("DeterminismConstrainedCycles", expected.DeterminismConstrainedCycles, actual.DeterminismConstrainedCycles, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.DomainIsolationProbeAttempts != actual.DomainIsolationProbeAttempts)
            {
                return Mismatch("DomainIsolationProbeAttempts", expected.DomainIsolationProbeAttempts, actual.DomainIsolationProbeAttempts, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.DomainIsolationBlockedAttempts != actual.DomainIsolationBlockedAttempts)
            {
                return Mismatch("DomainIsolationBlockedAttempts", expected.DomainIsolationBlockedAttempts, actual.DomainIsolationBlockedAttempts, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.DomainIsolationCrossDomainBlocks != actual.DomainIsolationCrossDomainBlocks)
            {
                return Mismatch("DomainIsolationCrossDomainBlocks", expected.DomainIsolationCrossDomainBlocks, actual.DomainIsolationCrossDomainBlocks, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.DomainIsolationKernelToUserBlocks != actual.DomainIsolationKernelToUserBlocks)
            {
                return Mismatch("DomainIsolationKernelToUserBlocks", expected.DomainIsolationKernelToUserBlocks, actual.DomainIsolationKernelToUserBlocks, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.EligibilityMaskedCycles != actual.EligibilityMaskedCycles)
            {
                return Mismatch("EligibilityMaskedCycles", expected.EligibilityMaskedCycles, actual.EligibilityMaskedCycles, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.EligibilityMaskedReadyCandidates != actual.EligibilityMaskedReadyCandidates)
            {
                return Mismatch("EligibilityMaskedReadyCandidates", expected.EligibilityMaskedReadyCandidates, actual.EligibilityMaskedReadyCandidates, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.LastEligibilityRequestedMask != actual.LastEligibilityRequestedMask)
            {
                return Mismatch("LastEligibilityRequestedMask", expected.LastEligibilityRequestedMask, actual.LastEligibilityRequestedMask, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.LastEligibilityNormalizedMask != actual.LastEligibilityNormalizedMask)
            {
                return Mismatch("LastEligibilityNormalizedMask", expected.LastEligibilityNormalizedMask, actual.LastEligibilityNormalizedMask, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.LastEligibilityReadyPortMask != actual.LastEligibilityReadyPortMask)
            {
                return Mismatch("LastEligibilityReadyPortMask", expected.LastEligibilityReadyPortMask, actual.LastEligibilityReadyPortMask, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.LastEligibilityVisibleReadyMask != actual.LastEligibilityVisibleReadyMask)
            {
                return Mismatch("LastEligibilityVisibleReadyMask", expected.LastEligibilityVisibleReadyMask, actual.LastEligibilityVisibleReadyMask, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.LastEligibilityMaskedReadyMask != actual.LastEligibilityMaskedReadyMask)
            {
                return Mismatch("LastEligibilityMaskedReadyMask", expected.LastEligibilityMaskedReadyMask, actual.LastEligibilityMaskedReadyMask, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistNominations != actual.AssistNominations)
            {
                return Mismatch("AssistNominations", expected.AssistNominations, actual.AssistNominations, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistInjections != actual.AssistInjections)
            {
                return Mismatch("AssistInjections", expected.AssistInjections, actual.AssistInjections, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistRejects != actual.AssistRejects)
            {
                return Mismatch("AssistRejects", expected.AssistRejects, actual.AssistRejects, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistBoundaryRejects != actual.AssistBoundaryRejects)
            {
                return Mismatch("AssistBoundaryRejects", expected.AssistBoundaryRejects, actual.AssistBoundaryRejects, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistInvalidations != actual.AssistInvalidations)
            {
                return Mismatch("AssistInvalidations", expected.AssistInvalidations, actual.AssistInvalidations, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistInterCoreNominations != actual.AssistInterCoreNominations)
            {
                return Mismatch("AssistInterCoreNominations", expected.AssistInterCoreNominations, actual.AssistInterCoreNominations, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistInterCoreInjections != actual.AssistInterCoreInjections)
            {
                return Mismatch("AssistInterCoreInjections", expected.AssistInterCoreInjections, actual.AssistInterCoreInjections, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistInterCoreRejects != actual.AssistInterCoreRejects)
            {
                return Mismatch("AssistInterCoreRejects", expected.AssistInterCoreRejects, actual.AssistInterCoreRejects, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistInterCoreDomainRejects != actual.AssistInterCoreDomainRejects)
            {
                return Mismatch("AssistInterCoreDomainRejects", expected.AssistInterCoreDomainRejects, actual.AssistInterCoreDomainRejects, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistInterCorePodLocalInjections != actual.AssistInterCorePodLocalInjections)
            {
                return Mismatch("AssistInterCorePodLocalInjections", expected.AssistInterCorePodLocalInjections, actual.AssistInterCorePodLocalInjections, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistInterCoreCrossPodInjections != actual.AssistInterCoreCrossPodInjections)
            {
                return Mismatch("AssistInterCoreCrossPodInjections", expected.AssistInterCoreCrossPodInjections, actual.AssistInterCoreCrossPodInjections, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistInterCorePodLocalRejects != actual.AssistInterCorePodLocalRejects)
            {
                return Mismatch("AssistInterCorePodLocalRejects", expected.AssistInterCorePodLocalRejects, actual.AssistInterCorePodLocalRejects, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistInterCoreCrossPodRejects != actual.AssistInterCoreCrossPodRejects)
            {
                return Mismatch("AssistInterCoreCrossPodRejects", expected.AssistInterCoreCrossPodRejects, actual.AssistInterCoreCrossPodRejects, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistInterCorePodLocalDomainRejects != actual.AssistInterCorePodLocalDomainRejects)
            {
                return Mismatch("AssistInterCorePodLocalDomainRejects", expected.AssistInterCorePodLocalDomainRejects, actual.AssistInterCorePodLocalDomainRejects, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistInterCoreCrossPodDomainRejects != actual.AssistInterCoreCrossPodDomainRejects)
            {
                return Mismatch("AssistInterCoreCrossPodDomainRejects", expected.AssistInterCoreCrossPodDomainRejects, actual.AssistInterCoreCrossPodDomainRejects, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistQuotaRejects != actual.AssistQuotaRejects)
            {
                return Mismatch("AssistQuotaRejects", expected.AssistQuotaRejects, actual.AssistQuotaRejects, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistQuotaIssueRejects != actual.AssistQuotaIssueRejects)
            {
                return Mismatch("AssistQuotaIssueRejects", expected.AssistQuotaIssueRejects, actual.AssistQuotaIssueRejects, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistQuotaLineRejects != actual.AssistQuotaLineRejects)
            {
                return Mismatch("AssistQuotaLineRejects", expected.AssistQuotaLineRejects, actual.AssistQuotaLineRejects, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistQuotaLinesReserved != actual.AssistQuotaLinesReserved)
            {
                return Mismatch("AssistQuotaLinesReserved", expected.AssistQuotaLinesReserved, actual.AssistQuotaLinesReserved, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistBackpressureRejects != actual.AssistBackpressureRejects)
            {
                return Mismatch("AssistBackpressureRejects", expected.AssistBackpressureRejects, actual.AssistBackpressureRejects, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistBackpressureOuterCapRejects != actual.AssistBackpressureOuterCapRejects)
            {
                return Mismatch("AssistBackpressureOuterCapRejects", expected.AssistBackpressureOuterCapRejects, actual.AssistBackpressureOuterCapRejects, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistBackpressureMshrRejects != actual.AssistBackpressureMshrRejects)
            {
                return Mismatch("AssistBackpressureMshrRejects", expected.AssistBackpressureMshrRejects, actual.AssistBackpressureMshrRejects, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistBackpressureDmaSrfRejects != actual.AssistBackpressureDmaSrfRejects)
            {
                return Mismatch("AssistBackpressureDmaSrfRejects", expected.AssistBackpressureDmaSrfRejects, actual.AssistBackpressureDmaSrfRejects, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistDonorPrefetchInjects != actual.AssistDonorPrefetchInjects)
            {
                return Mismatch("AssistDonorPrefetchInjects", expected.AssistDonorPrefetchInjects, actual.AssistDonorPrefetchInjects, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistLdsaInjects != actual.AssistLdsaInjects)
            {
                return Mismatch("AssistLdsaInjects", expected.AssistLdsaInjects, actual.AssistLdsaInjects, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistVdsaInjects != actual.AssistVdsaInjects)
            {
                return Mismatch("AssistVdsaInjects", expected.AssistVdsaInjects, actual.AssistVdsaInjects, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistSameVtInjects != actual.AssistSameVtInjects)
            {
                return Mismatch("AssistSameVtInjects", expected.AssistSameVtInjects, actual.AssistSameVtInjects, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistDonorVtInjects != actual.AssistDonorVtInjects)
            {
                return Mismatch("AssistDonorVtInjects", expected.AssistDonorVtInjects, actual.AssistDonorVtInjects, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistInterCoreSameVtVectorInjects != actual.AssistInterCoreSameVtVectorInjects)
            {
                return Mismatch(
                    "AssistInterCoreSameVtVectorInjects",
                    expected.AssistInterCoreSameVtVectorInjects,
                    actual.AssistInterCoreSameVtVectorInjects,
                    out mismatchField,
                    out expectedValue,
                    out actualValue);
            }

            if (expected.AssistInterCoreDonorVtVectorInjects != actual.AssistInterCoreDonorVtVectorInjects)
            {
                return Mismatch(
                    "AssistInterCoreDonorVtVectorInjects",
                    expected.AssistInterCoreDonorVtVectorInjects,
                    actual.AssistInterCoreDonorVtVectorInjects,
                    out mismatchField,
                    out expectedValue,
                    out actualValue);
            }

            if (expected.AssistInterCoreSameVtVectorWritebackInjects != actual.AssistInterCoreSameVtVectorWritebackInjects)
            {
                return Mismatch(
                    "AssistInterCoreSameVtVectorWritebackInjects",
                    expected.AssistInterCoreSameVtVectorWritebackInjects,
                    actual.AssistInterCoreSameVtVectorWritebackInjects,
                    out mismatchField,
                    out expectedValue,
                    out actualValue);
            }

            if (expected.AssistInterCoreDonorVtVectorWritebackInjects != actual.AssistInterCoreDonorVtVectorWritebackInjects)
            {
                return Mismatch(
                    "AssistInterCoreDonorVtVectorWritebackInjects",
                    expected.AssistInterCoreDonorVtVectorWritebackInjects,
                    actual.AssistInterCoreDonorVtVectorWritebackInjects,
                    out mismatchField,
                    out expectedValue,
                    out actualValue);
            }

            if (expected.AssistInterCoreLane6DefaultStoreDonorPrefetchInjects != actual.AssistInterCoreLane6DefaultStoreDonorPrefetchInjects)
            {
                return Mismatch(
                    "AssistInterCoreLane6DefaultStoreDonorPrefetchInjects",
                    expected.AssistInterCoreLane6DefaultStoreDonorPrefetchInjects,
                    actual.AssistInterCoreLane6DefaultStoreDonorPrefetchInjects,
                    out mismatchField,
                    out expectedValue,
                    out actualValue);
            }

            if (expected.AssistInterCoreLane6HotLoadDonorPrefetchInjects != actual.AssistInterCoreLane6HotLoadDonorPrefetchInjects)
            {
                return Mismatch(
                    "AssistInterCoreLane6HotLoadDonorPrefetchInjects",
                    expected.AssistInterCoreLane6HotLoadDonorPrefetchInjects,
                    actual.AssistInterCoreLane6HotLoadDonorPrefetchInjects,
                    out mismatchField,
                    out expectedValue,
                    out actualValue);
            }

            if (expected.AssistInterCoreLane6HotStoreDonorPrefetchInjects != actual.AssistInterCoreLane6HotStoreDonorPrefetchInjects)
            {
                return Mismatch(
                    "AssistInterCoreLane6HotStoreDonorPrefetchInjects",
                    expected.AssistInterCoreLane6HotStoreDonorPrefetchInjects,
                    actual.AssistInterCoreLane6HotStoreDonorPrefetchInjects,
                    out mismatchField,
                    out expectedValue,
                    out actualValue);
            }

            if (expected.AssistInterCoreLane6ColdStoreLdsaInjects != actual.AssistInterCoreLane6ColdStoreLdsaInjects)
            {
                return Mismatch(
                    "AssistInterCoreLane6ColdStoreLdsaInjects",
                    expected.AssistInterCoreLane6ColdStoreLdsaInjects,
                    actual.AssistInterCoreLane6ColdStoreLdsaInjects,
                    out mismatchField,
                    out expectedValue,
                    out actualValue);
            }

            if (expected.AssistInterCoreLane6LdsaInjects != actual.AssistInterCoreLane6LdsaInjects)
            {
                return Mismatch(
                    "AssistInterCoreLane6LdsaInjects",
                    expected.AssistInterCoreLane6LdsaInjects,
                    actual.AssistInterCoreLane6LdsaInjects,
                    out mismatchField,
                    out expectedValue,
                    out actualValue);
            }

            if (expected.AssistInterCoreLane6DonorPrefetchInjects != actual.AssistInterCoreLane6DonorPrefetchInjects)
            {
                return Mismatch(
                    "AssistInterCoreLane6DonorPrefetchInjects",
                    expected.AssistInterCoreLane6DonorPrefetchInjects,
                    actual.AssistInterCoreLane6DonorPrefetchInjects,
                    out mismatchField,
                    out expectedValue,
                    out actualValue);
            }

            if (expected.AssistInvalidationReason != actual.AssistInvalidationReason)
            {
                return Mismatch("AssistInvalidationReason", expected.AssistInvalidationReason, actual.AssistInvalidationReason, out mismatchField, out expectedValue, out actualValue);
            }

            if (expected.AssistOwnershipSignature != actual.AssistOwnershipSignature)
            {
                return Mismatch(
                    "AssistOwnershipSignature",
                    $"0x{expected.AssistOwnershipSignature:X16}",
                    $"0x{actual.AssistOwnershipSignature:X16}",
                    out mismatchField,
                    out expectedValue,
                    out actualValue);
            }

            mismatchField = string.Empty;
            expectedValue = string.Empty;
            actualValue = string.Empty;
            return true;
        }

        private static bool TryCompareReplayEvent(
            FullStateTraceEvent expected,
            FullStateTraceEvent actual,
            out string mismatchField,
            out string expectedValue,
            out string actualValue)
        {
            if (!TryCompareReplayContractEvent(expected, actual, out mismatchField, out expectedValue, out actualValue))
            {
                return false;
            }

            if (expected.CycleNumber != actual.CycleNumber)
            {
                return Mismatch("CycleNumber", expected.CycleNumber, actual.CycleNumber, out mismatchField, out expectedValue, out actualValue);
            }

            mismatchField = string.Empty;
            expectedValue = string.Empty;
            actualValue = string.Empty;
            return true;
        }

        private static bool TryCompareEventWithinEnvelope(
            FullStateTraceEvent expected,
            FullStateTraceEvent actual,
            ReplayEnvelopeConfiguration configuration,
            out string mismatchField,
            out string expectedValue,
            out string actualValue,
            out string allowedEnvelope,
            out int envelopeChecks)
        {
            envelopeChecks = 0;

            if (!TryCompareWithinDelta("CycleNumber", expected.CycleNumber, actual.CycleNumber, configuration.MaxCycleDrift, out mismatchField, out expectedValue, out actualValue, out allowedEnvelope))
            {
                return false;
            }
            envelopeChecks++;

            if (!TryCompareWithinDelta("ActiveMemoryRequests", expected.ActiveMemoryRequests, actual.ActiveMemoryRequests, configuration.MaxActiveMemoryRequestDelta, out mismatchField, out expectedValue, out actualValue, out allowedEnvelope))
            {
                return false;
            }
            envelopeChecks++;

            if (!TryCompareWithinDelta("MemorySubsystemCycle", expected.MemorySubsystemCycle, actual.MemorySubsystemCycle, configuration.MaxMemorySubsystemCycleDrift, out mismatchField, out expectedValue, out actualValue, out allowedEnvelope))
            {
                return false;
            }
            envelopeChecks++;

            if (!TryCompareArrayWithinDelta("BankQueueDepths", expected.BankQueueDepths, actual.BankQueueDepths, configuration.MaxBankQueueDepthDelta, out mismatchField, out expectedValue, out actualValue, out allowedEnvelope, out int bankChecks))
            {
                return false;
            }
            envelopeChecks += bankChecks;

            if (!TryCompareArrayWithinDelta("ThreadReadyQueueDepths", expected.ThreadReadyQueueDepths, actual.ThreadReadyQueueDepths, configuration.MaxReadyQueueDepthDelta, out mismatchField, out expectedValue, out actualValue, out allowedEnvelope, out int queueChecks))
            {
                return false;
            }
            envelopeChecks += queueChecks;

            mismatchField = string.Empty;
            expectedValue = string.Empty;
            actualValue = string.Empty;
            allowedEnvelope = string.Empty;
            return true;
        }

        private static bool TryCompareWithinDelta(
            string field,
            long expected,
            long actual,
            long maxDelta,
            out string mismatchField,
            out string expectedValue,
            out string actualValue,
            out string allowedEnvelope)
        {
            if (Math.Abs(expected - actual) > maxDelta)
            {
                mismatchField = field;
                expectedValue = expected.ToString();
                actualValue = actual.ToString();
                allowedEnvelope = $"+/-{maxDelta}";
                return false;
            }

            mismatchField = string.Empty;
            expectedValue = string.Empty;
            actualValue = string.Empty;
            allowedEnvelope = string.Empty;
            return true;
        }

        private static bool TryCompareArrayWithinDelta(
            string field,
            int[]? expected,
            int[]? actual,
            int maxDelta,
            out string mismatchField,
            out string expectedValue,
            out string actualValue,
            out string allowedEnvelope,
            out int envelopeChecks)
        {
            envelopeChecks = 0;

            if (expected == null || actual == null)
            {
                if (expected == actual)
                {
                    mismatchField = string.Empty;
                    expectedValue = string.Empty;
                    actualValue = string.Empty;
                    allowedEnvelope = string.Empty;
                    return true;
                }

                mismatchField = field;
                expectedValue = FormatArray(expected);
                actualValue = FormatArray(actual);
                allowedEnvelope = "matching shape";
                return false;
            }

            if (expected.Length != actual.Length)
            {
                mismatchField = field;
                expectedValue = FormatArray(expected);
                actualValue = FormatArray(actual);
                allowedEnvelope = "matching shape";
                return false;
            }

            for (int i = 0; i < expected.Length; i++)
            {
                envelopeChecks++;
                if (Math.Abs(expected[i] - actual[i]) > maxDelta)
                {
                    mismatchField = $"{field}[{i}]";
                    expectedValue = expected[i].ToString();
                    actualValue = actual[i].ToString();
                    allowedEnvelope = $"+/-{maxDelta}";
                    return false;
                }
            }

            mismatchField = string.Empty;
            expectedValue = string.Empty;
            actualValue = string.Empty;
            allowedEnvelope = string.Empty;
            return true;
        }

        private static bool Mismatch(
            string field,
            object? expected,
            object? actual,
            out string mismatchField,
            out string expectedValue,
            out string actualValue)
        {
            mismatchField = field;
            expectedValue = FormatValue(expected);
            actualValue = FormatValue(actual);
            return false;
        }

        private static string FormatValue(object? value)
        {
            return value?.ToString() ?? "<null>";
        }

        private static string FormatArray(int[]? values)
        {
            return values == null ? "<null>" : $"[{string.Join(",", values)}]";
        }

        private static int CountBits(byte value)
        {
            int count = 0;
            byte remaining = value;
            while (remaining != 0)
            {
                count += remaining & 1;
                remaining >>= 1;
            }

            return count;
        }
    }
}
