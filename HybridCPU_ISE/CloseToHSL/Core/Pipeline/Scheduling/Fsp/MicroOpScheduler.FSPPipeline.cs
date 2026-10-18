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
        private void PipelineFspStage1_Nominate(
            int ownerVirtualThreadId,
            SmtNominationState nominationState,
            IReadOnlyList<MicroOp?> bundle,
            BundleResourceCertificate4Way bundleMask,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard)
        {
            // Latch the exact scheduling generation consumed by SCHED2. SCHED1
            // performs no admission, but its nomination must not be combined with
            // a different owner, bundle, replay epoch, or mutable carrier state.
            _fspOwnerVirtualThreadId = ownerVirtualThreadId;
            CaptureFspBundleReferences(bundle);
            _fspBundleFingerprint = ComputeFspBundleFingerprint(bundle);
            _fspBundleCertificateIdentity = bundleMask.StructuralIdentity;
            _fspBundleMetadata = bundleMetadata;
            _fspBoundaryGuard = boundaryGuard;
            _fspReplayPhase = _currentReplayPhase;

            for (int vt = 0; vt < SMT_WAYS; vt++)
            {
                // Clear pipeline register entry
                _fspPipelineReg[vt].Valid = false;
                _fspPipelineReg[vt].VirtualThreadId = vt;
                _fspPipelineReg[vt].Candidate = null;
                _fspPipelineReg[vt].IdentityTemplate = null;
                _fspPipelineReg[vt].CandidateFingerprint = default;
                if (nominationState.IsReadyNonOwnerCandidate(vt, ownerVirtualThreadId))
                {
                    MicroOp? candidate = _smtPorts[vt];
                    if (candidate is not null)
                    {
                        _fspPipelineReg[vt].Candidate = candidate;
                        _fspPipelineReg[vt].IdentityTemplate = candidate.PostStageBIdentityTemplate;
                        _fspPipelineReg[vt].CandidateFingerprint =
                            ComputeFspCandidateFingerprint(candidate);
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
            if (_fspOwnerVirtualThreadId != bundleMetadata.OwnerVirtualThreadId ||
                !FspBundleReferencesMatch(bundle) ||
                !_fspBundleFingerprint.Equals(ComputeFspBundleFingerprint(bundle)) ||
                !_fspBundleCertificateIdentity.Equals(bundleMask.StructuralIdentity) ||
                !_fspBundleMetadata.Equals(bundleMetadata) ||
                !_fspBoundaryGuard.Equals(boundaryGuard) ||
                !ReplayPhaseMatches(_fspReplayPhase, _currentReplayPhase))
            {
                _fspCurrentStage = FspPipelineStage.SCHED1;
                return (bundleMask, nextEmptySlot);
            }

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
                    !ReferenceEquals(_smtPorts[candidateVt], candidate) ||
                    !ReferenceEquals(
                        candidate.PostStageBIdentityTemplate,
                        pipelineEntry.IdentityTemplate) ||
                    candidate.PostStageBIssuedAttempt is not null ||
                    !pipelineEntry.CandidateFingerprint.Equals(
                        ComputeFspCandidateFingerprint(candidate)))
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
                        lane,
                        bundleMetadata);
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

        private void RebuildFspStageContext(
            IReadOnlyList<MicroOp?> bundle,
            int ownerVirtualThreadId,
            out BundleResourceCertificate4Way bundleMask,
            out SmtBundleMetadata4Way bundleMetadata,
            out BoundaryGuardState boundaryGuard)
        {
            bundleMask = BundleResourceCertificate4Way.Empty;
            bundleMetadata = SmtBundleMetadata4Way.Empty(ownerVirtualThreadId);
            boundaryGuard = BoundaryGuardState.Open(_serializingEpochCounter);
            for (int slot = 0; slot < bundle.Count; slot++)
            {
                MicroOp? operation = bundle[slot];
                if (operation is null)
                {
                    continue;
                }

                bundleMask.AddOperation(operation);
                bundleMetadata = bundleMetadata.WithOperation(operation);
                boundaryGuard = boundaryGuard.WithOperation(operation);
            }
        }

        private static bool ReplayPhaseMatches(
            ReplayPhaseContext expected,
            ReplayPhaseContext actual) =>
            expected.IsActive == actual.IsActive &&
            expected.EpochId == actual.EpochId &&
            expected.CachedPc == actual.CachedPc &&
            expected.EpochLength == actual.EpochLength &&
            expected.CompletedReplays == actual.CompletedReplays &&
            expected.ValidSlotCount == actual.ValidSlotCount &&
            expected.StableDonorMask == actual.StableDonorMask &&
            expected.LastInvalidationReason == actual.LastInvalidationReason;

        private void CaptureFspBundleReferences(IReadOnlyList<MicroOp?> bundle)
        {
            for (int slot = 0; slot < _fspBundleReferences.Length; slot++)
            {
                _fspBundleReferences[slot] = slot < bundle.Count
                    ? bundle[slot]
                    : null;
            }
        }

        private bool FspBundleReferencesMatch(IReadOnlyList<MicroOp?> bundle)
        {
            if (bundle.Count != _fspBundleReferences.Length)
            {
                return false;
            }

            for (int slot = 0; slot < _fspBundleReferences.Length; slot++)
            {
                if (!ReferenceEquals(_fspBundleReferences[slot], bundle[slot]))
                {
                    return false;
                }
            }

            return true;
        }

        private static FspSnapshotFingerprint ComputeFspBundleFingerprint(
            IReadOnlyList<MicroOp?> bundle)
        {
            (ulong low, ulong high) = CreateFspFingerprintSeed();
            AddFspFingerprintValue(ref low, ref high, (ulong)bundle.Count);
            for (int slot = 0; slot < bundle.Count; slot++)
            {
                AddFspFingerprintValue(ref low, ref high, (ulong)(uint)slot);
                MicroOp? operation = bundle[slot];
                if (operation is null)
                {
                    AddFspFingerprintValue(ref low, ref high, 0);
                    continue;
                }

                AddFspFingerprintValue(ref low, ref high, 1);
                AddFspFingerprintValue(
                    ref low,
                    ref high,
                    unchecked((ulong)(uint)RuntimeHelpers.GetHashCode(operation)));
                FspSnapshotFingerprint operationFingerprint =
                    ComputeFspCandidateFingerprint(operation);
                AddFspFingerprintValue(ref low, ref high, operationFingerprint.Low);
                AddFspFingerprintValue(ref low, ref high, operationFingerprint.High);
            }

            return new FspSnapshotFingerprint(low, high);
        }

        private static FspSnapshotFingerprint ComputeFspCandidateFingerprint(
            MicroOp candidate)
        {
            (ulong low, ulong high) = CreateFspFingerprintSeed();
            MicroOpAdmissionMetadata admission = candidate.AdmissionMetadata;

            AddFspFingerprintValue(ref low, ref high, candidate.OpCode);
            AddFspFingerprintValue(ref low, ref high, candidate.PredicateMask);
            AddFspFingerprintValue(ref low, ref high, candidate.DestRegID);
            AddFspFingerprintValue(ref low, ref high, candidate.WritesRegister ? 1UL : 0UL);
            AddFspFingerprintValue(ref low, ref high, candidate.Latency);
            AddFspFingerprintValue(ref low, ref high, candidate.IsMemoryOp ? 1UL : 0UL);
            AddFspFingerprintValue(ref low, ref high, candidate.IsControlFlow ? 1UL : 0UL);
            AddFspFingerprintValue(ref low, ref high, candidate.IsStealable ? 1UL : 0UL);
            AddFspFingerprintValue(ref low, ref high, (ulong)candidate.MemoryLocalityHint);
            AddFspFingerprintValue(ref low, ref high, unchecked((ulong)(uint)candidate.OwnerThreadId));
            AddFspFingerprintValue(ref low, ref high, unchecked((ulong)(uint)candidate.OwnerContextId));
            AddFspFingerprintValue(ref low, ref high, unchecked((ulong)(uint)candidate.VirtualThreadId));
            AddFspFingerprintValue(ref low, ref high, (ulong)candidate.Class);
            AddFspFingerprintValue(ref low, ref high, (ulong)candidate.InstructionClass);
            AddFspFingerprintValue(ref low, ref high, (ulong)candidate.SerializationClass);
            AddFspFingerprintValue(ref low, ref high, candidate.HasSideEffects ? 1UL : 0UL);
            AddFspFingerprintValue(ref low, ref high, candidate.SafetyMask.Low);
            AddFspFingerprintValue(ref low, ref high, candidate.SafetyMask.High);
            AddFspFingerprintValue(ref low, ref high, candidate.ResourceMask.Low);
            AddFspFingerprintValue(ref low, ref high, candidate.ResourceMask.High);
            AddFspPlacementFingerprint(ref low, ref high, candidate.Placement);
            AddFspFingerprintValue(
                ref low,
                ref high,
                candidate is LoadStoreMicroOp loadStore
                    ? unchecked((ulong)(uint)loadStore.MemoryBankId)
                    : ulong.MaxValue);

            AddFspFingerprintValue(ref low, ref high, admission.IsStealable ? 1UL : 0UL);
            AddFspFingerprintValue(ref low, ref high, admission.IsControlFlow ? 1UL : 0UL);
            AddFspFingerprintValue(ref low, ref high, admission.IsMemoryOp ? 1UL : 0UL);
            AddFspFingerprintValue(ref low, ref high, admission.WritesRegister ? 1UL : 0UL);
            AddFspFingerprintValue(ref low, ref high, admission.HasSideEffects ? 1UL : 0UL);
            AddFspFingerprintValue(ref low, ref high, unchecked((ulong)(uint)admission.OwnerContextId));
            AddFspFingerprintValue(ref low, ref high, admission.DomainTag);
            AddFspPlacementFingerprint(ref low, ref high, admission.Placement);
            AddFspFingerprintValue(ref low, ref high, admission.RegisterHazardMask);
            AddFspFingerprintValue(ref low, ref high, admission.StructuralSafetyMask.Low);
            AddFspFingerprintValue(ref low, ref high, admission.StructuralSafetyMask.High);
            AddFspFingerprintValue(
                ref low,
                ref high,
                unchecked((ulong)(uint)admission.AssistCoalescingDescriptor.GetHashCode()));
            AddFspRegisterListFingerprint(ref low, ref high, admission.ReadRegisters);
            AddFspRegisterListFingerprint(ref low, ref high, admission.WriteRegisters);
            AddFspRangeListFingerprint(ref low, ref high, admission.ReadMemoryRanges);
            AddFspRangeListFingerprint(ref low, ref high, admission.NormalizedReadMemoryRanges);
            AddFspRangeListFingerprint(ref low, ref high, admission.WriteMemoryRanges);
            return new FspSnapshotFingerprint(low, high);
        }

        private static (ulong Low, ulong High) CreateFspFingerprintSeed() =>
            (14695981039346656037UL, 7809847782465536322UL);

        private static void AddFspPlacementFingerprint(
            ref ulong low,
            ref ulong high,
            SlotPlacementMetadata placement)
        {
            AddFspFingerprintValue(ref low, ref high, (ulong)placement.RequiredSlotClass);
            AddFspFingerprintValue(ref low, ref high, (ulong)placement.PinningKind);
            AddFspFingerprintValue(ref low, ref high, placement.PinnedLaneId);
            AddFspFingerprintValue(ref low, ref high, placement.DomainTag);
        }

        private static void AddFspRegisterListFingerprint(
            ref ulong low,
            ref ulong high,
            IReadOnlyList<int> values)
        {
            AddFspFingerprintValue(ref low, ref high, (ulong)values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                AddFspFingerprintValue(
                    ref low,
                    ref high,
                    unchecked((ulong)(uint)values[index]));
            }
        }

        private static void AddFspRangeListFingerprint(
            ref ulong low,
            ref ulong high,
            IReadOnlyList<(ulong Address, ulong Length)> ranges)
        {
            AddFspFingerprintValue(ref low, ref high, (ulong)ranges.Count);
            for (int index = 0; index < ranges.Count; index++)
            {
                AddFspFingerprintValue(ref low, ref high, ranges[index].Address);
                AddFspFingerprintValue(ref low, ref high, ranges[index].Length);
            }
        }

        private static void AddFspFingerprintValue(
            ref ulong low,
            ref ulong high,
            ulong value)
        {
            unchecked
            {
                low = (low ^ value) * 1099511628211UL;
                high = (high + value + 0x9E3779B97F4A7C15UL) * 14029467366897019727UL;
                high ^= high >> 29;
            }
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
