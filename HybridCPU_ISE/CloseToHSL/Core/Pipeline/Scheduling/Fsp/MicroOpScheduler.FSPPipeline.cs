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
        //  2-Stage Pipelined FSP (HLS Timing Closure §1)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// SCHED1: Nomination and source capture.
        ///
        /// Captures the exact ready non-owner candidate reference. No legality or
        /// placement work is performed here; SCHED2 rejects a replaced VT port
        /// instead of issuing a different operation under this nomination.
        ///
        /// HLS: 4-way ready-mask fanout → 4 × D-flip-flop writes.
        /// Single-cycle, minimal LUT depth.
        /// </summary>
        /// <param name="ownerVirtualThreadId">VT that owns the current bundle.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PipelineFspStage1_Nominate(int ownerVirtualThreadId, SmtNominationState nominationState)
        {
            // Latch owner VT for SCHED2 (Phase 03: TryClassAdmission)
            _fspOwnerVirtualThreadId = ownerVirtualThreadId;

            for (int vt = 0; vt < SMT_WAYS; vt++)
            {
                // Clear pipeline register entry
                _fspPipelineReg[vt].Valid = false;
                _fspPipelineReg[vt].VirtualThreadId = vt;
                _fspPipelineReg[vt].Candidate = null;
                _fspPipelineReg[vt].IdentityTemplate = null;
                if (nominationState.IsReadyNonOwnerCandidate(vt, ownerVirtualThreadId))
                {
                    MicroOp? candidate = _smtPorts[vt];
                    if (candidate is not null)
                    {
                        _fspPipelineReg[vt].Candidate = candidate;
                        _fspPipelineReg[vt].IdentityTemplate = candidate.PostStageBIdentityTemplate;
                        _fspPipelineReg[vt].Valid = true;
                    }
                }
            }

            _fspCurrentStage = FspPipelineStage.SCHED2;
            FspPipelineLatencyCycles++;
        }

        /// <summary>
        /// SCHED2: Intersection &amp; priority-encoded commit.
        ///
        /// Reads the pipeline register bank from SCHED1, performs two-stage
        /// admission (Phase 03): TryClassAdmission → TryMaterializeLane → Commit.
        /// SCHED1 latched the exact candidate. SCHED2 requires the live port to
        /// retain that same reference before evaluating admission.
        ///
        /// HLS: 4-iteration loop, each with Stage A (~3 LUT) + Stage B (~2 LUT).
        /// Single-cycle with parallel reduction tree.
        /// </summary>
        /// <param name="bundle">Working copy of VLIW bundle (8 slots)</param>
        /// <param name="bundleMask">Cumulative safety mask of existing bundle ops.</param>
        /// <param name="nextEmptySlot">First empty slot index (or -1). Retained for API compat.</param>
        /// <returns>Updated (bundleMask, nextEmptySlot) after injections.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (BundleResourceCertificate4Way mask, int nextSlot) PipelineFspStage2_Intersect(
            MicroOp[] bundle,
            BundleResourceCertificate4Way bundleMask,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard,
            int nextEmptySlot)
        {
            if (IsSmtBundleBlockedByBoundaryGuard(bundleMask, bundleMetadata, boundaryGuard))
            {
                _fspCurrentStage = FspPipelineStage.SCHED1;
                return (bundleMask, nextEmptySlot);
            }

            _runtimeLegalityService.PrepareSmt(
                _currentReplayPhase,
                bundleMask,
                bundleMetadata,
                boundaryGuard);

            BundleOpportunityState opportunityState = BundleOpportunityState.Create(bundle);
            int templateDomainScopeId = ResolveClassTemplateDomainScopeId(bundleMetadata);
            ClassTemplateAdmissionState templateState = TypedSlotEnabled
                ? PrepareClassTemplateAdmissionState(templateDomainScopeId)
                : default;
            byte bundleOccupancy = opportunityState.OccupancyMask;
            int realInjectedCount = 0;
            PrepareProjectedMemoryIssuePass(bundle);

            for (int vt = 0; vt < SMT_WAYS; vt++)
            {
                if (!_fspPipelineReg[vt].Valid) continue;

                ref FspPipelineRegister pipelineEntry = ref _fspPipelineReg[vt];
                int candidateVt = pipelineEntry.VirtualThreadId;
                MicroOp? candidate = pipelineEntry.Candidate;
                if (candidate is null ||
                    !ReferenceEquals(_smtPorts[candidateVt], candidate))
                {
                    continue;
                }

                // RF-06.4b diagnostic seam: FSP SCHED2 accepts only a carrier
                // whose bank/direction/footprint can be represented by the
                // same immutable memory capability used by ShadowOracle.
                // This is opt-in and does not alter the production decision.
                AssertRf06MemoryContractProjection(candidate);

                if (TypedSlotEnabled)
                {
                    // Phase 06: Two-stage class-admission + lane-materialization path
                    if (!TryClassAdmission(candidate, ref bundleMask, bundleMetadata, boundaryGuard, ref templateState, _fspOwnerVirtualThreadId,
                                           realInjectedCount, out var rejectA))
                    {
                        RecordTypedSlotReject(rejectA, candidate);
                        continue;
                    }

                    if (!TryMaterializeLane(candidate, bundleOccupancy, out int lane, out var rejectB))
                    {
                        RecordTypedSlotReject(rejectB, candidate);
                        continue;
                    }

                    // Commit
                    bundle[lane] = candidate;
                    MaterializePostStageBIssuedAttempt(
                        candidate,
                        pipelineEntry.IdentityTemplate,
                        lane);
                    candidate.IsFspInjected = true;
                    bundleMask.AddOperation(candidate);
                    opportunityState = opportunityState.WithOccupiedSlot(lane);
                    bundleOccupancy |= (byte)(1 << lane);
                    SmtInjectionsCount++;
                    realInjectedCount++;
                    _perVtInjections[candidateVt]++;
                    RecordTypedSlotInject(candidate, lane);
                    bundleMetadata = bundleMetadata.WithOperation(candidate);
                    boundaryGuard = boundaryGuard.WithOperation(candidate);
                    _runtimeLegalityService.RefreshSmtAfterMutation(
                        _currentReplayPhase,
                        bundleMask,
                        bundleMetadata,
                        boundaryGuard);

                    _classCapacity.IncrementOccupancy(candidate.Placement.RequiredSlotClass);
                    ConsumeProjectedMemoryIssueStateIfNeeded(candidate);

                    if (candidate.Placement.PinningKind == SlotPinningKind.ClassFlexible)
                    {
                        RecordPhaseLane(candidate.Placement.RequiredSlotClass, lane);
                    }

                    // Phase 07: Capture class template on first successful typed-slot injection (pipelined)
                    if (_currentReplayPhase.IsActive && !_classTemplateValid)
                    {
                        CaptureClassTemplate(templateDomainScopeId);
                    }
                }
                else
                {
                    // Legacy path: exact slot search + CanInject
                    int slot = ResolveNextInjectableSlot(opportunityState, 0);
                    if (slot < 0) continue;

                    LegalityDecision legalityDecision = EvaluateSmtLegality(
                        bundleMask,
                        bundleMetadata,
                        boundaryGuard,
                        candidate);
                    if (!legalityDecision.IsAllowed)
                    {
                        RecordPerVtRejection(candidate.VirtualThreadId);
                        SmtRejectionsCount++;
                        continue;
                    }

                    int candidateBankId = candidate is LoadStoreMicroOp ls ? ls.MemoryBankId : -1;
                    if (!TryPassOuterCap(candidate, candidate.VirtualThreadId,
                                         candidate.IsMemoryOp, candidateBankId, _fspOwnerVirtualThreadId, out _))
                    {
                        RecordPerVtRejection(candidate.VirtualThreadId);
                        continue;
                    }

                    // Commit (legacy)
                    bundle[slot] = candidate;
                    candidate.IsFspInjected = true;
                    bundleMask.AddOperation(candidate);
                    opportunityState = opportunityState.WithOccupiedSlot(slot);
                    bundleOccupancy = opportunityState.OccupancyMask;
                    SmtInjectionsCount++;
                    realInjectedCount++;
                    _perVtInjections[candidateVt]++;
                    RecordTypedSlotInject(candidate, slot);
                    bundleMetadata = bundleMetadata.WithOperation(candidate);
                    boundaryGuard = boundaryGuard.WithOperation(candidate);
                    _runtimeLegalityService.RefreshSmtAfterMutation(
                        _currentReplayPhase,
                        bundleMask,
                        bundleMetadata,
                        boundaryGuard);
                    ConsumeProjectedMemoryIssueStateIfNeeded(candidate);
                }

                // Consume the SMT port
                _smtPorts[candidateVt] = null;
                _smtPortValid[candidateVt] = false;
            }

            // Update nextEmptySlot for API compatibility
            nextEmptySlot = ResolveNextInjectableSlot(opportunityState, 0);

            _fspCurrentStage = FspPipelineStage.SCHED1;
            return (bundleMask, nextEmptySlot);
        }

        /// <summary>
        /// Get the latched inter-core nomination ready mask for FspPowerController.
        /// HLS: direct wire tap on the latched ready-mask register.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort GetLatchedInterCoreNominationReadyMask()
        {
            return _latchedInterCoreNominationReadyMask;
        }

    }
}
