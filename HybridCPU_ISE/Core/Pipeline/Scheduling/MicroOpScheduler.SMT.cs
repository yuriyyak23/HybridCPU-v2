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
        //  PackBundleIntraCoreSmt — Intra-Core 4-Way SMT
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Pack a VLIW bundle with intra-core SMT slot stealing.
        /// Scans SMT nomination ports (VT 0–3) and injects candidates from
        /// other virtual threads into empty/NOP slots, subject to safety mask checks.
        ///
        /// Supports two modes:
        /// - **Single-cycle** (default): Original combinational path for functional correctness.
        /// - **2-stage pipelined**: Call PipelineFspStage1_Nominate() on cycle N,
        ///   then PipelineFspStage2_Intersect() on cycle N+1 for reduced LUT depth.
        ///   This mode is used when PipelinedFspEnabled is true.
        ///
        /// HLS: 4-iteration loop (one per VT), each with a safety comparator.
        /// </summary>
        /// <summary>
        /// Pack a VLIW bundle with intra-core SMT slot stealing using an explicit
        /// FSM-provided runnable VT mask for the nomination snapshot.
        /// </summary>
        public MicroOp[] PackBundleIntraCoreSmt(
            System.Collections.Generic.IReadOnlyList<MicroOp?> bundle,
            int ownerVirtualThreadId,
            int localCoreId,
            byte eligibleVirtualThreadMask,
            YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memSub = null)
        {
            if (bundle == null || bundle.Count != 8)
                return bundle as MicroOp[] ?? Array.Empty<MicroOp>();

            // Increment TDM global counter for the cycle
            GlobalCycleCounter++;

            var result = new MicroOp[8];
            for (int slot = 0; slot < result.Length; slot++)
            {
                result[slot] = bundle[slot];
            }

            // Phase 02: Compute per-class occupancy from the current bundle
            _classCapacity = SlotClassCapacity.ComputeFromBundle(result);
            RecordLoopPhaseSample();

            // Compute cumulative safety mask of existing bundle operations
            // We use BundleResourceCertificate4Way to preserve per-VT register isolation!
            var bundleCert = BundleResourceCertificate4Way.Empty;
            var bundleMetadata = SmtBundleMetadata4Way.Empty(ownerVirtualThreadId);
            BoundaryGuardState boundaryGuard = BoundaryGuardState.Open(_serializingEpochCounter);
            for (int i = 0; i < 8; i++)
            {
                if (result[i] != null)
                {
                    bundleCert.AddOperation(result[i]);
                    bundleMetadata = bundleMetadata.WithOperation(result[i]);
                    boundaryGuard = boundaryGuard.WithOperation(result[i]);
                }
            }

            _runtimeLegalityService.PrepareSmt(
                _currentReplayPhase,
                bundleCert,
                bundleMetadata,
                boundaryGuard);
            byte replayAwareMask = _currentReplayPhase.HasStableDonorStructure
                ? _currentReplayPhase.StableDonorMask
                : byte.MaxValue;
            BundleOpportunityState opportunityState = BundleOpportunityState.Create(result);
            SmtNominationState nominationState = CreateEligibleSmtNominationState(eligibleVirtualThreadMask);
            RecordDeterminismTaxWindow(
                opportunityState.ReferenceOpportunityCount,
                opportunityState.CountReplayEligibleReferenceOpportunities(replayAwareMask),
                CountPendingSmtCandidates(nominationState, ownerVirtualThreadId));

            // Next empty slot cursor
            int nextEmptySlot = ResolveNextInjectableSlot(opportunityState, 0);

            // 2-Stage Pipelined FSP path (HLS Timing Closure §1):
            // When pipelined mode is active AND SCHED2 is ready, use the
            // pre-decoded pipeline register bank instead of re-decoding candidates.
            if (PipelinedFspEnabled && _fspCurrentStage == FspPipelineStage.SCHED2)
            {
                (bundleCert, nextEmptySlot) = PipelineFspStage2_Intersect(
                    result,
                    bundleCert,
                    bundleMetadata,
                    boundaryGuard,
                    nextEmptySlot);

                // Kick SCHED1 for the next cycle's candidates
                PipelineFspStage1_Nominate(
                    ownerVirtualThreadId,
                    CreateEligibleSmtNominationState(eligibleVirtualThreadMask));

                ClearAssistNominationPorts();
                TotalSchedulerCycles++;
                return result;
            }

            // If pipelined mode is active but SCHED1 hasn't run yet, prime the pipeline
            if (PipelinedFspEnabled && _fspCurrentStage == FspPipelineStage.SCHED1)
            {
                PipelineFspStage1_Nominate(ownerVirtualThreadId, nominationState);
                // First cycle: no injection yet (pipeline priming), return original bundle
                ClearAssistNominationPorts();
                TotalSchedulerCycles++;
                return result;
            }

            // Advance to SCHED2 stage for pipelined checking
            TotalSchedulerCycles++;
            PrepareProjectedMemoryIssuePass(result);

            // ── Single-cycle combinational path (original behavior) ──

            if (IsSmtBundleBlockedByBoundaryGuard(bundleCert, bundleMetadata, boundaryGuard))
            {
                AssistBoundaryRejects += CountReadyAssistCandidates(ownerVirtualThreadId);
                ClearAssistNominationPorts();
                TotalSchedulerCycles++;
                return result;
            }

            // TDM Logic: determine if this cycle is reserved for a background thread
            bool isTdmSlot = (GlobalCycleCounter % TDM_PERIOD) == 0;
            bool forceTdm = isTdmSlot && nominationState.HasReadyBackgroundCandidates;

            // Phase 2A: Credit-based fairness ranking or legacy TDM ordering
            Span<int> vtOrder = stackalloc int[SMT_WAYS];
            Span<bool> wasInjected = stackalloc bool[SMT_WAYS];
            wasInjected.Clear();

            if (CreditFairnessEnabled)
            {
                GetCreditRankedOrder(ownerVirtualThreadId, vtOrder);
            }
            else
            {
                for (int i = 0; i < SMT_WAYS; i++)
                    vtOrder[i] = GetRankedVirtualThread(i, forceTdm);
            }

            // Phase 2B: Collect candidates with pressure scores for tie-breaking.
            // Stage A / Stage B checks are deferred to Pass 2 (TryClassAdmission + TryMaterializeLane).
            Span<int> legalVts = stackalloc int[SMT_WAYS];
            Span<int> legalPressure = stackalloc int[SMT_WAYS];
            int legalCount = 0;

            for (int i = 0; i < SMT_WAYS; i++)
            {
                int vt = vtOrder[i];
                if (!nominationState.IsReadyNonOwnerCandidate(vt, ownerVirtualThreadId)) continue;

                var candidate = _smtPorts[vt];
                if (candidate == null) continue;

                int candidateBankId = candidate is LoadStoreMicroOp lsCandidate ? lsCandidate.MemoryBankId : -1;

                int pressure = 0;
                if (BankPressureTieBreakEnabled && candidate.IsMemoryOp && candidateBankId >= 0)
                {
                    pressure = GetBankPressureScore(candidateBankId);
                }

                legalVts[legalCount] = vt;
                legalPressure[legalCount] = pressure;
                legalCount++;
            }

            // Phase 2B: Sort legal candidates by pressure (ascending), preserving credit/vtId order on tie.
            if (BankPressureTieBreakEnabled && legalCount > 1)
            {
                for (int i = 0; i < legalCount - 1; i++)
                {
                    for (int j = i + 1; j < legalCount; j++)
                    {
                        if (legalPressure[j] < legalPressure[i])
                        {
                            (legalVts[i], legalVts[j]) = (legalVts[j], legalVts[i]);
                            (legalPressure[i], legalPressure[j]) = (legalPressure[j], legalPressure[i]);
                            BankPressureAvoidanceCount++;
                        }
                    }
                }
            }

            // Phase 5: Snapshot original bundle and SMT candidates for oracle gap analysis
            ShadowOracleCycleSnapshot oracleSnapshot = default;
            if (EnableShadowOracle)
            {
                oracleSnapshot = CaptureShadowOracleCycleSnapshot(result, nominationState);
            }

            int templateDomainScopeId = ResolveClassTemplateDomainScopeId(bundleMetadata);

            // Pass 2: Injection (guarded by TypedSlotEnabled)
            byte bundleOccupancy = opportunityState.OccupancyMask;
            int realInjectedCount = 0;

            // Phase 07: Level 1 class-template match at cycle start (gated by TypedSlotEnabled)
            ClassTemplateAdmissionState templateState = TypedSlotEnabled
                ? PrepareClassTemplateAdmissionState(templateDomainScopeId)
                : default;

            for (int i = 0; i < legalCount; i++)
            {
                int vt = legalVts[i];
                var candidate = _smtPorts[vt];
                if (candidate == null) continue;

                if (TypedSlotEnabled)
                {
                    // Phase 06: Two-stage class-admission + lane-materialization path
                    if (!TryClassAdmission(candidate, ref bundleCert, bundleMetadata, boundaryGuard, ref templateState, ownerVirtualThreadId,
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
                    result[lane] = candidate;
                    candidate.IsFspInjected = true;
                    bundleCert.AddOperation(candidate);
                    opportunityState = opportunityState.WithOccupiedSlot(lane);
                    bundleOccupancy |= (byte)(1 << lane);
                    SmtInjectionsCount++;
                    realInjectedCount++;
                    wasInjected[vt] = true;
                    _perVtInjections[vt]++;
                    RecordTypedSlotInject(candidate, lane);
                    bundleMetadata = bundleMetadata.WithOperation(candidate);
                    boundaryGuard = boundaryGuard.WithOperation(candidate);
                    _runtimeLegalityService.RefreshSmtAfterMutation(
                        _currentReplayPhase,
                        bundleCert,
                        bundleMetadata,
                        boundaryGuard);

                    _classCapacity.IncrementOccupancy(candidate.Placement.RequiredSlotClass);
                    ConsumeProjectedMemoryIssueStateIfNeeded(candidate);

                    if (candidate.Placement.PinningKind == SlotPinningKind.ClassFlexible)
                    {
                        RecordPhaseLane(candidate.Placement.RequiredSlotClass, lane);
                    }

                    // Phase 07: Capture class template on first successful typed-slot injection
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
                        bundleCert,
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
                                         candidate.IsMemoryOp, candidateBankId, ownerVirtualThreadId, out _))
                    {
                        RecordPerVtRejection(candidate.VirtualThreadId);
                        continue;
                    }

                    // Commit (legacy)
                    result[slot] = candidate;
                    candidate.IsFspInjected = true;
                    bundleCert.AddOperation(candidate);
                    opportunityState = opportunityState.WithOccupiedSlot(slot);
                    bundleOccupancy = opportunityState.OccupancyMask;
                    SmtInjectionsCount++;
                    realInjectedCount++;
                    wasInjected[vt] = true;
                    _perVtInjections[vt]++;
                    RecordTypedSlotInject(candidate, slot);
                    bundleMetadata = bundleMetadata.WithOperation(candidate);
                    boundaryGuard = boundaryGuard.WithOperation(candidate);
                    _runtimeLegalityService.RefreshSmtAfterMutation(
                        _currentReplayPhase,
                        bundleCert,
                        bundleMetadata,
                        boundaryGuard);
                    ConsumeProjectedMemoryIssueStateIfNeeded(candidate);
                }

                _smtPorts[vt] = null;
                _smtPortValid[vt] = false;
                nominationState = nominationState.WithoutCandidate(vt);
            }

            // Phase 2A: Update fairness credits after injection decisions
            if (CreditFairnessEnabled)
            {
                AccumulateFairnessCredits(nominationState, ownerVirtualThreadId, wasInjected);
            }

            // Phase 5: Record oracle gap after real injection is complete
            if (EnableShadowOracle)
            {
                RecordOracleGap(oracleSnapshot, ownerVirtualThreadId, realInjectedCount);
            }

            TryInjectAssistCandidates(
                result,
                ref bundleCert,
                ref bundleMetadata,
                ref boundaryGuard,
                ref templateState,
                ref opportunityState,
                ref bundleOccupancy,
                ownerVirtualThreadId,
                realInjectedCount,
                memSub);

            return result;
        }

        /// <summary>
        /// Enable 2-stage pipelined FSP arbitration for HLS timing closure.
        /// When true, PackBundleIntraCoreSmt uses SCHED1/SCHED2 pipeline stages
        /// instead of single-cycle combinational path.
        /// Default: false (single-cycle, backwards compatible).
        /// </summary>
        public bool PipelinedFspEnabled { get; set; }

        /// <summary>
        /// Master toggle for typed-slot admission pipeline.
        /// When false: scheduler uses legacy physical-slot path (Unclassified + exact slot search).
        /// When true: scheduler uses two-stage class-admission + lane-materialization.
        /// Default: false (backward compatible; opt-in for typed-slot rollout).
        /// <para>
        /// Takes effect on the next scheduling cycle. Disabling immediately reverts to legacy path.
        /// When disabled, all Phase 08 typed-slot counters remain at 0 for clean A/B comparison.
        /// </para>
        /// </summary>
        public bool TypedSlotEnabled { get; set; }

        /// <summary>
        /// Publish current replay-phase context to the scheduler.
        /// </summary>
        public void SetReplayPhaseContext(
            ReplayPhaseContext phase,
            bool invalidateAssistOnDeactivate = true)
        {
            _currentReplayPhase = phase;
            if (phase.IsActive)
            {
                ReplayAwareCycles++;
                RecordLoopPhaseEntry(phase);
            }

            if (!phase.IsActive)
            {
                ReplayPhaseInvalidationReason deactivationReason =
                    phase.LastInvalidationReason != ReplayPhaseInvalidationReason.None
                        ? phase.LastInvalidationReason
                        : ReplayPhaseInvalidationReason.InactivePhase;

                // Phase 04: Reset per-class lane records when replay phase deactivates.
                ResetPreviousPhaseLanes();
                if (invalidateAssistOnDeactivate)
                {
                    InvalidateAssistNominationState(AssistInvalidationReason.Replay);
                }

                // Phase 07: Invalidate class template when replay phase deactivates.
                InvalidateClassTemplate(deactivationReason);

                InvalidatePhaseCertificateTemplates(
                    deactivationReason,
                    invalidateInterCore: true,
                    invalidateFourWay: true);
                return;
            }

            // Phase 04: Reset per-class lane records when replay epoch changes.
            if (phase.EpochId != _previousPhaseEpochId)
            {
                ResetPreviousPhaseLanes();
                _previousPhaseEpochId = phase.EpochId;
                InvalidateAssistNominationState(AssistInvalidationReason.Replay);

                // Phase 07: Invalidate class template on epoch transition.
                InvalidateClassTemplate(ReplayPhaseInvalidationReason.ClassTemplateExpired);
            }

            _runtimeLegalityService.InvalidatePhaseMismatch(phase);
        }

        /// <summary>
        /// Publish the deterministic hardware-occupancy snapshot for the current packing pass.
        /// Callers should set this once before invoking PackBundle/PackBundleIntraCoreSmt.
        /// </summary>
        public void SetHardwareOccupancySnapshot(HardwareOccupancySnapshot128 snapshot)
        {
            _hardwareOccupancySnapshot = snapshot;
        }

        /// <summary>
        /// Notify the scheduler that a serialising instruction has been committed to
        /// architectural state (G34 — Deterministic Legality Alignment).
        ///
        /// <para>
        /// Each call creates a new serialising epoch boundary:
        /// <list type="bullet">
        ///   <item>Increments the serialising epoch counter.</item>
        ///   <item>Invalidates all phase-certificate templates (inter-core and 4-way)
        ///   so that no replay-phase certificate spans a serialising event (G35/G36).</item>
        ///   <item>Invalidates the class-capacity template with reason
        ///   <see cref="ReplayPhaseInvalidationReason.SerializingEvent"/>.</item>
        /// </list>
        /// </para>
        ///
        /// <para>
        /// Must be called by the commit stage after retiring a
        /// <see cref="Arch.SerializationClass.FullSerial"/> or
        /// <see cref="Arch.SerializationClass.VmxSerial"/> instruction.
        /// </para>
        /// </summary>
        public void NotifySerializingCommit(bool invalidateAssist = true)
        {
            _serializingEpochCounter++;
            if (invalidateAssist)
            {
                InvalidateAssistNominationState(AssistInvalidationReason.SerializingBoundary);
            }

            // G35/G36: Invalidate all phase-certificate templates on serialising event.
            // No replay certificate may span a barrier, trap, or VM-transition boundary.
            InvalidatePhaseCertificateTemplates(
                ReplayPhaseInvalidationReason.SerializingEvent,
                invalidateInterCore: true,
                invalidateFourWay: true);

            // G35: Invalidate class-capacity template so typed-slot fast-path cannot
            // carry stale capacity assumptions across a serialising boundary.
            InvalidateClassTemplate(ReplayPhaseInvalidationReason.SerializingEvent);
        }

        /// <summary>
        /// Get Phase 1 replay/certificate telemetry from the scheduler.
        /// </summary>
        public SchedulerPhaseMetrics GetPhaseMetrics()
        {
            return new SchedulerPhaseMetrics
            {
                ReplayAwareCycles = ReplayAwareCycles,
                PhaseCertificateReadyHits = PhaseCertificateReadyHits,
                PhaseCertificateReadyMisses = PhaseCertificateReadyMisses,
                EstimatedChecksSaved = EstimatedPhaseCertificateChecksSaved,
                PhaseCertificateInvalidations = PhaseCertificateInvalidations,
                PhaseCertificateMutationInvalidations = PhaseCertificateMutationInvalidations,
                PhaseCertificatePhaseMismatchInvalidations = PhaseCertificatePhaseMismatchInvalidations,
                LastCertificateInvalidationReason = LastPhaseCertificateInvalidationReason,
                DeterminismReferenceOpportunitySlots = DeterminismReferenceOpportunitySlots,
                DeterminismReplayEligibleSlots = DeterminismReplayEligibleSlots,
                DeterminismMaskedSlots = DeterminismMaskedSlots,
                DeterminismEstimatedLostSlots = DeterminismEstimatedLostSlots,
                DeterminismConstrainedCycles = DeterminismConstrainedCycles,
                DomainIsolationProbeAttempts = DomainIsolationProbeAttempts,
                DomainIsolationBlockedAttempts = DomainIsolationBlockedAttempts,
                DomainIsolationCrossDomainBlocks = DomainIsolationCrossDomainBlocks,
                DomainIsolationKernelToUserBlocks = DomainIsolationKernelToUserBlocks,
                EligibilityMaskedCycles = EligibilityMaskedCycles,
                EligibilityMaskedReadyCandidates = EligibilityMaskedReadyCandidates,
                LastEligibilityRequestedMask = _lastSmtEligibilitySnapshot.RequestedMask,
                LastEligibilityNormalizedMask = _lastSmtEligibilitySnapshot.NormalizedMask,
                LastEligibilityReadyPortMask = _lastSmtEligibilitySnapshot.ReadyPortMask,
                LastEligibilityVisibleReadyMask = _lastSmtEligibilitySnapshot.VisibleReadyMask,
                LastEligibilityMaskedReadyMask = _lastSmtEligibilitySnapshot.MaskedReadyMask,
                // Phase 08: typed-slot telemetry
                ClassTemplateReuseHits = ClassTemplateReuseHits,
                ClassTemplateInvalidations = ClassTemplateInvalidations,
                TypedSlotFastPathAccepts = TypedSlotFastPathAccepts,
                TypedSlotStandardPathAccepts = TypedSlotStandardPathAccepts,
                TotalLaneBindings = TotalLaneBindings,
                LaneReuseHits = LaneReuseHits,
                LaneReuseMisses = LaneReuseMisses,
                AluClassInjects = AluClassInjects,
                LsuClassInjects = LsuClassInjects,
                DmaStreamClassInjects = DmaStreamClassInjects,
                BranchControlInjects = BranchControlInjects,
                HardPinnedInjects = HardPinnedInjects,
                ClassFlexibleInjects = ClassFlexibleInjects,
                NopAvoided = NopAvoided,
                NopDueToPinnedConstraint = NopDueToPinnedConstraint,
                NopDueToNoClassCapacity = NopDueToNoClassCapacity,
                NopDueToResourceConflict = NopDueToResourceConflict,
                NopDueToDynamicState = NopDueToDynamicState,
                StaticClassOvercommitRejects = StaticClassOvercommitRejects,
                DynamicClassExhaustionRejects = DynamicClassExhaustionRejects,
                PinnedLaneConflicts = PinnedLaneConflicts,
                LateBindingConflicts = LateBindingConflicts,
                TypedSlotDomainRejects = TypedSlotDomainRejects,
                SmtOwnerContextGuardRejects = SmtOwnerContextGuardRejects,
                SmtDomainGuardRejects = SmtDomainGuardRejects,
                SmtBoundaryGuardRejects = SmtBoundaryGuardRejects,
                SmtSharedResourceCertificateRejects = SmtSharedResourceCertificateRejects,
                SmtRegisterGroupCertificateRejects = SmtRegisterGroupCertificateRejects,
                LastSmtLegalityRejectKind = LastSmtLegalityRejectKind,
                LastSmtLegalityAuthoritySource = LastSmtLegalityAuthoritySource,
                SmtLegalityRejectByAluClass = SmtLegalityRejectByAluClass,
                SmtLegalityRejectByLsuClass = SmtLegalityRejectByLsuClass,
                SmtLegalityRejectByDmaStreamClass = SmtLegalityRejectByDmaStreamClass,
                SmtLegalityRejectByBranchControl = SmtLegalityRejectByBranchControl,
                SmtLegalityRejectBySystemSingleton = SmtLegalityRejectBySystemSingleton,
                ClassTemplateDomainInvalidations = ClassTemplateDomainInvalidations,
                ClassTemplateCapacityMismatchInvalidations = ClassTemplateCapacityMismatchInvalidations,
                CertificateRejectByAluClass = CertificateRejectByAluClass,
                CertificateRejectByLsuClass = CertificateRejectByLsuClass,
                CertificateRejectByDmaStreamClass = CertificateRejectByDmaStreamClass,
                CertificateRejectByBranchControl = CertificateRejectByBranchControl,
                CertificateRejectBySystemSingleton = CertificateRejectBySystemSingleton,
                CertificateRegGroupConflictVT0 = CertificateRegGroupConflictVT0,
                CertificateRegGroupConflictVT1 = CertificateRegGroupConflictVT1,
                CertificateRegGroupConflictVT2 = CertificateRegGroupConflictVT2,
                CertificateRegGroupConflictVT3 = CertificateRegGroupConflictVT3,
                RejectionsVT0 = RejectionsVT0,
                RejectionsVT1 = RejectionsVT1,
                RejectionsVT2 = RejectionsVT2,
                RejectionsVT3 = RejectionsVT3,
                RegGroupConflictsVT0 = RegGroupConflictsVT0,
                RegGroupConflictsVT1 = RegGroupConflictsVT1,
                RegGroupConflictsVT2 = RegGroupConflictsVT2,
                RegGroupConflictsVT3 = RegGroupConflictsVT3,
                BankPendingRejectBank0 = BankPendingRejectBank0,
                BankPendingRejectBank1 = BankPendingRejectBank1,
                BankPendingRejectBank2 = BankPendingRejectBank2,
                BankPendingRejectBank3 = BankPendingRejectBank3,
                BankPendingRejectBank4 = BankPendingRejectBank4,
                BankPendingRejectBank5 = BankPendingRejectBank5,
                BankPendingRejectBank6 = BankPendingRejectBank6,
                BankPendingRejectBank7 = BankPendingRejectBank7,
                BankPendingRejectBank8 = BankPendingRejectBank8,
                BankPendingRejectBank9 = BankPendingRejectBank9,
                BankPendingRejectBank10 = BankPendingRejectBank10,
                BankPendingRejectBank11 = BankPendingRejectBank11,
                BankPendingRejectBank12 = BankPendingRejectBank12,
                BankPendingRejectBank13 = BankPendingRejectBank13,
                BankPendingRejectBank14 = BankPendingRejectBank14,
                BankPendingRejectBank15 = BankPendingRejectBank15,
                MemoryClusteringEvents = MemoryClusteringEvents,
                TypedSlotHardwareBudgetRejects = TypedSlotHardwareBudgetRejects,
                TypedSlotAssistQuotaRejects = TypedSlotAssistQuotaRejects,
                TypedSlotAssistBackpressureRejects = TypedSlotAssistBackpressureRejects,
                // Phase 04: serialising-event epoch telemetry
                SerializingBoundaryRejects = SerializingBoundaryRejects,
                SerializingEpochCount = SerializingEpochCount,
                AssistNominations = AssistNominationCount,
                AssistInjections = AssistInjectionsCount,
                AssistRejects = AssistRejects,
                AssistBoundaryRejects = AssistBoundaryRejects,
                AssistInvalidations = AssistInvalidations,
                AssistInterCoreNominations = AssistInterCoreNominations,
                AssistInterCoreInjections = AssistInterCoreInjections,
                AssistInterCoreRejects = AssistInterCoreRejects,
                AssistInterCoreDomainRejects = AssistInterCoreDomainRejects,
                AssistInterCorePodLocalInjections = AssistInterCorePodLocalInjections,
                AssistInterCoreCrossPodInjections = AssistInterCoreCrossPodInjections,
                AssistInterCorePodLocalRejects = AssistInterCorePodLocalRejects,
                AssistInterCoreCrossPodRejects = AssistInterCoreCrossPodRejects,
                AssistInterCorePodLocalDomainRejects = AssistInterCorePodLocalDomainRejects,
                AssistInterCoreCrossPodDomainRejects = AssistInterCoreCrossPodDomainRejects,
                AssistInterCoreSameVtVectorInjects = AssistInterCoreSameVtVectorInjects,
                AssistInterCoreDonorVtVectorInjects = AssistInterCoreDonorVtVectorInjects,
                AssistInterCoreSameVtVectorWritebackInjects = AssistInterCoreSameVtVectorWritebackInjects,
                AssistInterCoreDonorVtVectorWritebackInjects = AssistInterCoreDonorVtVectorWritebackInjects,
                AssistInterCoreLane6DefaultStoreDonorPrefetchInjects = AssistInterCoreLane6DefaultStoreDonorPrefetchInjects,
                AssistInterCoreLane6HotLoadDonorPrefetchInjects = AssistInterCoreLane6HotLoadDonorPrefetchInjects,
                AssistInterCoreLane6HotStoreDonorPrefetchInjects = AssistInterCoreLane6HotStoreDonorPrefetchInjects,
                AssistInterCoreLane6DonorPrefetchInjects = AssistInterCoreLane6DonorPrefetchInjects,
                AssistInterCoreLane6ColdStoreLdsaInjects = AssistInterCoreLane6ColdStoreLdsaInjects,
                AssistInterCoreLane6LdsaInjects = AssistInterCoreLane6LdsaInjects,
                AssistQuotaRejects = AssistQuotaRejects,
                AssistQuotaIssueRejects = AssistQuotaIssueRejects,
                AssistQuotaLineRejects = AssistQuotaLineRejects,
                AssistQuotaLinesReserved = AssistQuotaLinesReserved,
                AssistBackpressureRejects = AssistBackpressureRejects,
                AssistBackpressureOuterCapRejects = AssistBackpressureOuterCapRejects,
                AssistBackpressureMshrRejects = AssistBackpressureMshrRejects,
                AssistBackpressureDmaSrfRejects = AssistBackpressureDmaSrfRejects,
                AssistDonorPrefetchInjects = AssistDonorPrefetchInjects,
                AssistLdsaInjects = AssistLdsaInjects,
                AssistVdsaInjects = AssistVdsaInjects,
                AssistSameVtInjects = AssistSameVtInjects,
                AssistDonorVtInjects = AssistDonorVtInjects,
                LastAssistInvalidationReason = LastAssistInvalidationReason,
                LastAssistOwnershipSignature = LastAssistOwnershipSignature,
                LoopPhaseProfiles = BuildLoopPhaseProfiles()
            };
        }

        void ILegalityCertificateCacheTelemetrySink.RecordLegalityCertificateCacheHit(
            long estimatedChecksSaved)
        {
            PhaseCertificateReadyHits++;
            EstimatedPhaseCertificateChecksSaved += estimatedChecksSaved;
        }

        void ILegalityCertificateCacheTelemetrySink.RecordLegalityCertificateCacheMiss()
        {
            PhaseCertificateReadyMisses++;
        }

        void ILegalityCertificateCacheTelemetrySink.RecordLegalityCertificateCacheInvalidation(
            ReplayPhaseInvalidationReason reason)
        {
            if (reason == ReplayPhaseInvalidationReason.None)
                return;

            PhaseCertificateInvalidations++;
            if (reason == ReplayPhaseInvalidationReason.CertificateMutation)
            {
                PhaseCertificateMutationInvalidations++;
            }

            if (reason is ReplayPhaseInvalidationReason.PhaseMismatch
                or ReplayPhaseInvalidationReason.InactivePhase)
            {
                PhaseCertificatePhaseMismatchInvalidations++;
            }

            LastPhaseCertificateInvalidationReason = reason;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private LegalityDecision EvaluateInterCoreLegality(
            IReadOnlyList<MicroOp?> bundle,
            BundleResourceCertificate bundleCert,
            int bundleOwnerThreadId,
            ulong requestedDomainTag,
            MicroOp candidate,
            SafetyMask128 globalHardwareMask = default)
        {
            return _runtimeLegalityService.EvaluateInterCoreLegality(
                _currentReplayPhase,
                bundle,
                bundleCert,
                bundleOwnerThreadId,
                requestedDomainTag,
                candidate,
                globalHardwareMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int CountPendingInterCoreCandidates(ulong requestedDomainTag)
        {
            return CountPendingInterCoreCandidates(CaptureInterCoreNominationSnapshot(), requestedDomainTag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int CountPendingInterCoreCandidates(
            InterCoreNominationSnapshot nominationSnapshot,
            ulong requestedDomainTag)
        {
            int count = 0;
            for (int coreId = 0; coreId < NUM_PORTS; coreId++)
            {
                if (!nominationSnapshot.TryGetCandidate(coreId, out MicroOp candidate))
                    continue;

                if (requestedDomainTag != 0)
                {
                    InterCoreDomainGuardDecision domainGuard =
                        _runtimeLegalityService.EvaluateInterCoreDomainGuard(candidate, requestedDomainTag);
                    if (!domainGuard.IsAllowed)
                        continue;
                }

                count++;
            }

            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CountPendingSmtCandidates(SmtNominationState nominationState, int ownerVirtualThreadId)
        {
            return nominationState.CountReadyCandidatesExcluding(ownerVirtualThreadId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordDeterminismTaxWindow(int referenceSlots, int replayEligibleSlots, int readyCandidates)
        {
            if (!_currentReplayPhase.IsActive || referenceSlots <= 0)
                return;

            DeterminismReferenceOpportunitySlots += referenceSlots;
            DeterminismReplayEligibleSlots += replayEligibleSlots;

            int maskedSlots = Math.Max(0, referenceSlots - replayEligibleSlots);
            DeterminismMaskedSlots += maskedSlots;

            if (maskedSlots > 0)
            {
                DeterminismConstrainedCycles++;
                if (readyCandidates > 0)
                {
                    DeterminismEstimatedLostSlots += Math.Min(maskedSlots, readyCandidates);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordDomainIsolationProbe(DomainIsolationProbeResult probe)
        {
            DomainIsolationProbeAttempts++;

            if (!probe.IsAllowed)
            {
                DomainIsolationBlockedAttempts++;
            }

            if (probe.IsCrossDomainBlock)
            {
                DomainIsolationCrossDomainBlocks++;
            }

            if (probe.IsKernelToUserBlock)
            {
                DomainIsolationKernelToUserBlocks++;
            }
        }

        /// <summary>
        /// Record a typed-slot rejection for telemetry (Phase 03).
        /// Increments the appropriate per-reason counter and preserves
        /// backward-compatible <see cref="SmtRejectionsCount"/>.
        /// <para>
        /// Note: the outer-cap gate already increments SmtRejectionsCount
        /// for ScoreboardReject, BankPendingReject, HardwareBudgetReject,
        /// and SpeculationBudgetReject.
        /// This method only increments SmtRejectionsCount for reject types that
        /// do not originate from TryPassOuterCap.
        /// </para>
        /// <para>HLS: diagnostic counter bank — not on critical path.</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordTypedSlotReject(TypedSlotRejectReason reason)
        {
            switch (reason)
            {
                case TypedSlotRejectReason.StaticClassOvercommit:
                case TypedSlotRejectReason.DynamicClassExhaustion:
                    ClassCapacityRejects++;
                    SmtRejectionsCount++;
                    // Phase 08: disaggregated + NOP tracking
                    if (reason == TypedSlotRejectReason.StaticClassOvercommit)
                        StaticClassOvercommitRejects++;
                    else
                        DynamicClassExhaustionRejects++;
                    NopDueToNoClassCapacity++;
                    break;
                case TypedSlotRejectReason.ResourceConflict:
                    TypedSlotResourceConflictRejects++;
                    SmtRejectionsCount++;
                    NopDueToResourceConflict++; // Phase 08
                    break;
                // Scoreboard/Bank/HardwareBudget/SpecBudget: SmtRejectionsCount already
                // incremented inside IsOuterCapBlocking — only bump typed counters.
                case TypedSlotRejectReason.ScoreboardReject:
                    TypedSlotScoreboardRejects++;
                    NopDueToDynamicState++; // Phase 08
                    break;
                case TypedSlotRejectReason.BankPendingReject:
                    break;
                case TypedSlotRejectReason.HardwareBudgetReject:
                    NopDueToDynamicState++; // Phase 08
                    break;
                case TypedSlotRejectReason.AssistQuotaReject:
                    TypedSlotAssistQuotaRejects++;
                    SmtRejectionsCount++;
                    NopDueToDynamicState++;
                    break;
                case TypedSlotRejectReason.AssistBackpressureReject:
                    TypedSlotAssistBackpressureRejects++;
                    SmtRejectionsCount++;
                    NopDueToDynamicState++;
                    break;
                case TypedSlotRejectReason.SpeculationBudgetReject:
                    TypedSlotSpeculationBudgetRejects++;
                    NopDueToDynamicState++; // Phase 08
                    break;
                case TypedSlotRejectReason.PinnedLaneConflict:
                case TypedSlotRejectReason.LateBindingConflict:
                    LaneConflictRejects++;
                    SmtRejectionsCount++;
                    // Phase 08: disaggregated + NOP tracking
                    if (reason == TypedSlotRejectReason.PinnedLaneConflict)
                    {
                        PinnedLaneConflicts++;
                        NopDueToPinnedConstraint++;
                    }
                    else
                        LateBindingConflicts++;
                    break;
                case TypedSlotRejectReason.DomainReject:
                    TypedSlotDomainRejects++; // Phase 08
                    SmtRejectionsCount++;
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordCertificateRejectByClass(SlotClass slotClass)
        {
            switch (slotClass)
            {
                case SlotClass.AluClass:
                    CertificateRejectByAluClass++;
                    break;
                case SlotClass.LsuClass:
                    CertificateRejectByLsuClass++;
                    break;
                case SlotClass.DmaStreamClass:
                    CertificateRejectByDmaStreamClass++;
                    break;
                case SlotClass.BranchControl:
                    CertificateRejectByBranchControl++;
                    break;
                case SlotClass.SystemSingleton:
                    CertificateRejectBySystemSingleton++;
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordSmtLegalityRejectByClass(SlotClass slotClass)
        {
            switch (slotClass)
            {
                case SlotClass.AluClass:
                    SmtLegalityRejectByAluClass++;
                    break;
                case SlotClass.LsuClass:
                    SmtLegalityRejectByLsuClass++;
                    break;
                case SlotClass.DmaStreamClass:
                    SmtLegalityRejectByDmaStreamClass++;
                    break;
                case SlotClass.BranchControl:
                    SmtLegalityRejectByBranchControl++;
                    break;
                case SlotClass.SystemSingleton:
                    SmtLegalityRejectBySystemSingleton++;
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordCertificateRegisterGroupConflict(int virtualThreadId)
        {
            RecordPerVtRegGroupConflict(virtualThreadId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordSmtLegalityRejectBreakdown()
        {
            switch (_lastSmtLegalityRejectKind)
            {
                case RejectKind.OwnerMismatch:
                    SmtOwnerContextGuardRejects++;
                    break;
                case RejectKind.DomainMismatch:
                    SmtDomainGuardRejects++;
                    break;
                case RejectKind.Boundary:
                    SmtBoundaryGuardRejects++;
                    break;
            }

            switch (_lastCertificateRejectDetail)
            {
                case CertificateRejectDetail.SharedResourceConflict:
                    SmtSharedResourceCertificateRejects++;
                    break;
                case CertificateRejectDetail.RegisterGroupConflict:
                    SmtRegisterGroupCertificateRejects++;
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordPerVtRejection(int virtualThreadId)
        {
            switch (virtualThreadId)
            {
                case 0:
                    RejectionsVT0++;
                    break;
                case 1:
                    RejectionsVT1++;
                    break;
                case 2:
                    RejectionsVT2++;
                    break;
                case 3:
                    RejectionsVT3++;
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordPerVtRegGroupConflict(int virtualThreadId)
        {
            switch (virtualThreadId)
            {
                case 0:
                    RegGroupConflictsVT0++;
                    break;
                case 1:
                    RegGroupConflictsVT1++;
                    break;
                case 2:
                    RegGroupConflictsVT2++;
                    break;
                case 3:
                    RegGroupConflictsVT3++;
                    break;
            }
        }

        /// <summary>
        /// Phase 06: Extended reject recording with candidate metadata.
        /// Computes <see cref="TypedSlotRejectClassification"/> via
        /// the runtime legality service seam for structured diagnostics.
        /// Falls back to <see cref="RecordTypedSlotReject(TypedSlotRejectReason)"/>
        /// for counter updates.
        /// <para>HLS: diagnostic path — not on critical timing path.</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordTypedSlotReject(TypedSlotRejectReason reason, MicroOp candidate)
        {
            // Increment counters via base overload
            RecordTypedSlotReject(reason);
            RecordPerVtRejection(candidate.VirtualThreadId);

            if (reason == TypedSlotRejectReason.ResourceConflict)
            {
                RecordSmtLegalityRejectBreakdown();
                RecordSmtLegalityRejectByClass(candidate.Placement.RequiredSlotClass);

                if (_lastCertificateRejectDetail != CertificateRejectDetail.None)
                {
                    RecordCertificateRejectByClass(candidate.Placement.RequiredSlotClass);
                }

                if (_lastCertificateRejectDetail == CertificateRejectDetail.RegisterGroupConflict)
                {
                    RecordCertificateRegisterGroupConflict(candidate.VirtualThreadId);
                }
            }

            // Phase 06: Compute structured classification for telemetry
            _lastRejectClassification = _runtimeLegalityService.ClassifyReject(
                reason,
                _lastCertificateRejectDetail,
                candidate.Placement.RequiredSlotClass,
                candidate.Placement.PinningKind);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordBankPendingReject(int memoryBankId)
        {
            TypedSlotBankPendingRejects++;
            NopDueToDynamicState++;

            switch (memoryBankId)
            {
                case 0: BankPendingRejectBank0++; break;
                case 1: BankPendingRejectBank1++; break;
                case 2: BankPendingRejectBank2++; break;
                case 3: BankPendingRejectBank3++; break;
                case 4: BankPendingRejectBank4++; break;
                case 5: BankPendingRejectBank5++; break;
                case 6: BankPendingRejectBank6++; break;
                case 7: BankPendingRejectBank7++; break;
                case 8: BankPendingRejectBank8++; break;
                case 9: BankPendingRejectBank9++; break;
                case 10: BankPendingRejectBank10++; break;
                case 11: BankPendingRejectBank11++; break;
                case 12: BankPendingRejectBank12++; break;
                case 13: BankPendingRejectBank13++; break;
                case 14: BankPendingRejectBank14++; break;
                case 15: BankPendingRejectBank15++; break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RecordMemoryClusteringEvent()
        {
            MemoryClusteringEvents++;
        }

        /// <summary>
        /// Phase 08: Record a successful typed-slot injection with per-class distribution.
        /// Called in commit path after successful typed-slot lane materialization.
        /// <para>HLS: diagnostic counter bank — not on critical path.</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecordTypedSlotInject(MicroOp candidate, int lane)
        {
            TotalLaneBindings++;

            if (candidate.Placement.PinningKind == SlotPinningKind.HardPinned)
                HardPinnedInjects++;
            else
            {
                ClassFlexibleInjects++;
                NopAvoided++;
            }

            switch (candidate.Placement.RequiredSlotClass)
            {
                case SlotClass.AluClass:       AluClassInjects++; break;
                case SlotClass.LsuClass:       LsuClassInjects++; break;
                case SlotClass.DmaStreamClass: DmaStreamClassInjects++; break;
                case SlotClass.BranchControl:  BranchControlInjects++; break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ResolveNextInjectableSlot(BundleOpportunityState opportunityState, int startIndex)
        {
            return opportunityState.FindNextInjectableEmptySlot(
                startIndex,
                _currentReplayPhase.HasStableDonorStructure,
                _currentReplayPhase.StableDonorMask);
        }

        /// <summary>
        /// Compute an 8-bit bitmask of physically occupied slots in a VLIW bundle.
        /// Bit N is set when <paramref name="bundle"/>[N] is not null.
        /// <para>HLS: 8-bit wire-OR of non-null predicates — zero LUT depth.</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ComputeBundleOccupancy(MicroOp[] bundle)
        {
            return BundleOpportunityState.Create(bundle).OccupancyMask;
        }

        // ══════════════════════════════════════════════════════════════
    }
}
