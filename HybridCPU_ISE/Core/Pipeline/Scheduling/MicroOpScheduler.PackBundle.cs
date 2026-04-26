using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Diagnostics;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace YAKSys_Hybrid_CPU.Core
{
    public partial class MicroOpScheduler
    {
        // ══════════════════════════════════════════════════════════════
        //  PackBundle — Inter-Core FSP
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Pack a VLIW bundle with FSP slot stealing.
        /// Scans the bundle for NOP / null / stealable slots and fills them
        /// with nominated candidates that pass legality-checker checks.
        ///
        /// Supports both direct test calls (4-param) and PodController calls (7-param).
        /// The globalResourceLocks parameter is forwarded from GRLB for structural
        /// conflict detection. domainCertificate gates Singularity-style isolation.
        /// </summary>
        public MicroOp[] PackBundle(
            System.Collections.Generic.IReadOnlyList<MicroOp?> originalBundle,
            int currentThreadId,
            bool stealEnabled,
            byte stealMask,
            ResourceBitset globalResourceLocks = default,
            byte donorMask = 0,
            ulong domainCertificate = 0,
            int localCoreId = -1,
            ushort assistPodId = ushort.MaxValue,
            int bundleOwnerContextId = -1,
            ulong bundleOwnerDomainTag = 0,
            ulong assistRuntimeEpoch = 0,
            YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memSub = null,
            PodController?[]? pods = null)
        {
            TotalSchedulerCycles++;

            if (!stealEnabled || originalBundle == null || originalBundle.Count != 8)
                return originalBundle as MicroOp[] ?? Array.Empty<MicroOp>();

            // Work on a copy so the original is not mutated
            var result = new MicroOp[8];
            for (int slot = 0; slot < result.Length; slot++)
            {
                result[slot] = originalBundle[slot];
            }

            // Phase 02: Compute per-class occupancy from the current bundle
            _classCapacity = SlotClassCapacity.ComputeFromBundle(result);
            RecordLoopPhaseSample();
            PrepareProjectedMemoryIssuePass(result);

            BundleResourceCertificate bundleCert =
                BundleResourceCertificate.Create(result, currentThreadId, (ulong)TotalSchedulerCycles);
            _runtimeLegalityService.PrepareInterCore(
                _currentReplayPhase,
                bundleCert);
            byte replayAwareStealMask = GetReplayAwareStealMask(stealMask, donorMask);
            BundleOpportunityState opportunityState = BundleOpportunityState.Create(result);
            RecordDeterminismTaxWindow(
                opportunityState.ReferenceOpportunityCount,
                opportunityState.CountReplayEligibleReferenceOpportunities(replayAwareStealMask),
                CountPendingInterCoreCandidates(domainCertificate));
            int currentPassInjectedCount = 0;

            for (int slot = 0; slot < 8; slot++)
            {
                if (!opportunityState.IsReplayEligibleReferenceOpportunity(slot, replayAwareStealMask))
                    continue;

                // Try to find a candidate via priority encoder
                var candidate = TryStealSlot(currentThreadId, domainCertificate);
                if (candidate == null)
                    break; // No more candidates

                // Safety verification: domain cert + safety mask + hardware state
                // Convert ResourceBitset → SafetyMask128 (same bit layout, zero-cost)
                var hwMask = new SafetyMask128(globalResourceLocks.Low, globalResourceLocks.High);

                LegalityDecision legalityDecision = EvaluateInterCoreLegality(
                    result,
                    bundleCert,
                    currentThreadId,
                    domainCertificate,
                    candidate,
                    hwMask);
                bool safe = legalityDecision.IsAllowed;
                TypedSlotRejectReason outerCapReject = TypedSlotRejectReason.None;

                if (safe)
                {
                    int candidateBankId = candidate is LoadStoreMicroOp loadStoreCandidate
                        ? loadStoreCandidate.MemoryBankId
                        : -1;
                    safe = TryPassOuterCap(
                        candidate,
                        candidate.VirtualThreadId,
                        candidate.IsMemoryOp,
                        candidateBankId,
                        currentThreadId,
                        out outerCapReject);
                }

                if (safe)
                {
                    // Mark stolen memory ops as speculative (Phase 7)
                    if (candidate.IsMemoryOp && candidate.OwnerThreadId != currentThreadId)
                    {
                        candidate.IsSpeculative = true;
                        SuccessfulSpeculativeSteals++;

                        // Phase 2C: Spend speculation budget
                        if (SpeculationBudgetEnabled)
                        {
                            _speculationBudget--;
                            int concurrent = (SpeculationBudgetMax - _speculationBudget);
                            if (concurrent > PeakConcurrentSpeculativeOps)
                                PeakConcurrentSpeculativeOps = concurrent;
                        }
                    }

                    result[slot] = candidate;
                    candidate.IsFspInjected = true;
                    SuccessfulInjectionsCount++;
                    currentPassInjectedCount++;
                    bundleCert = bundleCert.WithOperation(candidate);
                    opportunityState = opportunityState.WithOccupiedSlot(slot);
                    _runtimeLegalityService.RefreshInterCoreAfterMutation(
                        _currentReplayPhase,
                        bundleCert);

                    // Phase 02: Update class-capacity after successful placement
                    _classCapacity.IncrementOccupancy(candidate.Placement.RequiredSlotClass);
                    ConsumeProjectedMemoryIssueStateIfNeeded(candidate);
                }
                else
                {
                    RejectedInjectionsCount++;

                    if (outerCapReject != TypedSlotRejectReason.None)
                    {
                        RecordPerVtRejection(candidate.VirtualThreadId);
#if TESTING
                        TestRecordRejection(
                            $"Inter-core outer-cap reject at slot {slot}: {outerCapReject}",
                            RejectionType.MemoryConflict);
#endif
                        continue;
                    }

                    string rejectDetail =
                        legalityDecision.CertificateDetail != CertificateRejectDetail.None
                            ? $", {legalityDecision.CertificateDetail}"
                            : string.Empty;
#if TESTING
                    TestRecordRejection(
                        $"Inter-core legality reject at slot {slot}: {legalityDecision.RejectKind} via {legalityDecision.AuthoritySource}{rejectDetail}",
                        RejectionType.SafetyMask);
#endif
                }
            }

            if (localCoreId >= 0)
            {
                TryInjectInterCoreAssistCandidates(
                    result,
                    ref bundleCert,
                    ref opportunityState,
                    currentThreadId,
                    localCoreId,
                    assistPodId,
                    bundleOwnerContextId,
                    bundleOwnerDomainTag,
                    domainCertificate,
                    assistRuntimeEpoch,
                    globalResourceLocks,
                    currentPassInjectedCount,
                    memSub,
                    pods);
            }

            return result;
        }

    }
}
