using System;

namespace YAKSys_Hybrid_CPU.Core
{
    public partial class MicroOpScheduler
    {
        private const int AssistBundleQuota = 1;

        private readonly MicroOp?[] _smtAssistPorts = new MicroOp?[SMT_WAYS];
        private readonly bool[] _smtAssistPortValid = new bool[SMT_WAYS];
        private readonly AssistInterCoreTransport[] _interCoreAssistPorts = new AssistInterCoreTransport[NUM_PORTS];
        private readonly bool[] _interCoreAssistPortValid = new bool[NUM_PORTS];

        public bool AssistInjectionEnabled { get; set; } = true;

        public AssistMemoryQuota AssistMemoryQuotaPolicy { get; set; } =
            AssistMemoryQuota.Default;

        public AssistBackpressurePolicy AssistBackpressurePolicy { get; set; } =
            AssistBackpressurePolicy.Default;

        public long AssistNominationCount { get; private set; }

        public long AssistInjectionsCount { get; private set; }

        public long AssistRejects { get; private set; }

        public long AssistBoundaryRejects { get; private set; }

        public long AssistInvalidations { get; private set; }

        public long AssistInterCoreNominations { get; private set; }

        public long AssistInterCoreInjections { get; private set; }

        public long AssistInterCoreRejects { get; private set; }

        public long AssistInterCoreDomainRejects { get; private set; }

        public long AssistInterCorePodLocalInjections { get; private set; }

        public long AssistInterCoreCrossPodInjections { get; private set; }

        public long AssistInterCorePodLocalRejects { get; private set; }

        public long AssistInterCoreCrossPodRejects { get; private set; }

        public long AssistInterCorePodLocalDomainRejects { get; private set; }

        public long AssistInterCoreCrossPodDomainRejects { get; private set; }

        public long AssistInterCoreSameVtVectorInjects { get; private set; }

        public long AssistInterCoreDonorVtVectorInjects { get; private set; }

        public long AssistInterCoreSameVtVectorWritebackInjects { get; private set; }

        public long AssistInterCoreDonorVtVectorWritebackInjects { get; private set; }

        public long AssistInterCoreLane6DefaultStoreDonorPrefetchInjects { get; private set; }

        public long AssistInterCoreLane6HotLoadDonorPrefetchInjects { get; private set; }

        public long AssistInterCoreLane6HotStoreDonorPrefetchInjects { get; private set; }

        public long AssistInterCoreLane6DonorPrefetchInjects { get; private set; }

        public long AssistInterCoreLane6ColdStoreLdsaInjects { get; private set; }

        public long AssistInterCoreLane6LdsaInjects { get; private set; }

        public long AssistQuotaRejects { get; private set; }

        public long AssistQuotaIssueRejects { get; private set; }

        public long AssistQuotaLineRejects { get; private set; }

        public long AssistQuotaLinesReserved { get; private set; }

        public long AssistBackpressureRejects { get; private set; }

        public long AssistBackpressureOuterCapRejects { get; private set; }

        public long AssistBackpressureMshrRejects { get; private set; }

        public long AssistBackpressureDmaSrfRejects { get; private set; }

        public long AssistDonorPrefetchInjects { get; private set; }

        public long AssistLdsaInjects { get; private set; }

        public long AssistVdsaInjects { get; private set; }

        public long AssistSameVtInjects { get; private set; }

        public long AssistDonorVtInjects { get; private set; }

        public AssistInvalidationReason LastAssistInvalidationReason { get; private set; } =
            AssistInvalidationReason.None;

        public ulong LastAssistOwnershipSignature { get; private set; }

        public void NominateAssistCandidate(int vtId, MicroOp? op)
        {
            if ((uint)vtId >= SMT_WAYS)
                return;

            if (op == null || !op.IsAssist)
            {
                _smtAssistPorts[vtId] = null;
                _smtAssistPortValid[vtId] = false;
                return;
            }

            _smtAssistPorts[vtId] = op;
            _smtAssistPortValid[vtId] = true;
            AssistNominationCount++;
        }

        public void ClearAssistNominationPorts()
        {
            for (int vtId = 0; vtId < SMT_WAYS; vtId++)
            {
                _smtAssistPorts[vtId] = null;
                _smtAssistPortValid[vtId] = false;
            }
        }

        public void InvalidateAssistNominationState(AssistInvalidationReason reason)
        {
            if (reason == AssistInvalidationReason.None)
                return;

            ClearAssistNominationPorts();
            ClearInterCoreAssistNominationPorts();
            AssistInvalidations++;
            LastAssistInvalidationReason = reason;
        }

        private int CountReadyAssistCandidates(int ownerVirtualThreadId)
        {
            int readyCount = 0;
            for (int vtId = 0; vtId < SMT_WAYS; vtId++)
            {
                if (vtId == ownerVirtualThreadId ||
                    !_smtAssistPortValid[vtId] ||
                    _smtAssistPorts[vtId] == null)
                {
                    continue;
                }

                readyCount++;
            }

            return readyCount;
        }

        private int TryInjectAssistCandidates(
            MicroOp[] result,
            ref BundleResourceCertificate4Way bundleCert,
            ref SmtBundleMetadata4Way bundleMetadata,
            ref BoundaryGuardState boundaryGuard,
            ref ClassTemplateAdmissionState templateState,
            ref BundleOpportunityState opportunityState,
            ref byte bundleOccupancy,
            int ownerVirtualThreadId,
            int currentPassInjections,
            YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memSub = null)
        {
            if (!AssistInjectionEnabled)
            {
                ClearAssistNominationPorts();
                return 0;
            }

            if (ResolveNextInjectableSlot(opportunityState, 0) < 0)
            {
                ClearAssistNominationPorts();
                return 0;
            }

            if (IsSmtBundleBlockedByBoundaryGuard(bundleCert, bundleMetadata, boundaryGuard))
            {
                AssistBoundaryRejects += CountReadyAssistCandidates(ownerVirtualThreadId);
                ClearAssistNominationPorts();
                return 0;
            }

            Span<int> vtOrder = stackalloc int[SMT_WAYS];
            if (CreditFairnessEnabled)
            {
                GetCreditRankedOrder(ownerVirtualThreadId, vtOrder);
            }
            else
            {
                for (int vt = 0; vt < SMT_WAYS; vt++)
                    vtOrder[vt] = vt;
            }

            int assistInjectedCount = 0;
            AssistMemoryQuotaState assistQuotaState = AssistMemoryQuotaPolicy.CreateState();
            AssistBackpressureState assistBackpressureState =
                AssistBackpressurePolicy.CreateState(CaptureAssistBackpressureSnapshot());

            for (int orderIndex = 0; orderIndex < SMT_WAYS && assistInjectedCount < AssistBundleQuota; orderIndex++)
            {
                int vtId = vtOrder[orderIndex];
                if (vtId == ownerVirtualThreadId ||
                    !_smtAssistPortValid[vtId] ||
                    _smtAssistPorts[vtId] == null)
                {
                    continue;
                }

                MicroOp candidate = _smtAssistPorts[vtId]!;

                if (TypedSlotEnabled)
                {
                    if (!TryClassAdmission(
                        candidate,
                        ref bundleCert,
                        bundleMetadata,
                        boundaryGuard,
                        ref templateState,
                        ownerVirtualThreadId,
                        currentPassInjections + assistInjectedCount,
                        out TypedSlotRejectReason rejectA))
                    {
                        RecordAssistReject(candidate, rejectA);
                        continue;
                    }

                    if (!TryMaterializeLane(candidate, bundleOccupancy, out int lane, out TypedSlotRejectReason rejectB))
                    {
                        RecordAssistReject(candidate, rejectB);
                        continue;
                    }

                    if (candidate is AssistMicroOp typedAssistBackpressureCandidate &&
                        !TryReserveAssistBackpressure(
                            typedAssistBackpressureCandidate,
                            ref assistBackpressureState,
                            memSub))
                    {
                        continue;
                    }

                    if (candidate is AssistMicroOp typedAssistCandidate &&
                        !TryReserveAssistQuota(typedAssistCandidate, ref assistQuotaState))
                    {
                        continue;
                    }

                    result[lane] = candidate;
                    candidate.IsFspInjected = true;
                    bundleCert.AddOperation(candidate);
                    bundleMetadata = bundleMetadata.WithOperation(candidate);
                    boundaryGuard = boundaryGuard.WithOperation(candidate);
                    opportunityState = opportunityState.WithOccupiedSlot(lane);
                    bundleOccupancy |= (byte)(1 << lane);
                    _runtimeLegalityService.RefreshSmtAfterMutation(
                        _currentReplayPhase,
                        bundleCert,
                        bundleMetadata,
                        boundaryGuard);
                    _classCapacity.IncrementOccupancy(candidate.Placement.RequiredSlotClass);
                    ConsumeProjectedMemoryIssueStateIfNeeded(candidate);
                    RecordTypedSlotInject(candidate, lane);
                    RecordAssistInjection(candidate);
                    assistInjectedCount++;
                    continue;
                }

                int slot = ResolveNextInjectableSlot(opportunityState, 0);
                if (slot < 0)
                    break;

                LegalityDecision legalityDecision = EvaluateSmtLegality(
                    bundleCert,
                    bundleMetadata,
                    boundaryGuard,
                    candidate);
                if (!legalityDecision.IsAllowed)
                {
                    AssistRejects++;
                    continue;
                }

                if (candidate is AssistMicroOp legacyPlacementSensitiveAssist &&
                    legacyPlacementSensitiveAssist.CarrierSlotClass == SlotClass.DmaStreamClass)
                {
                    // Lane6/DMA assist carriers must not silently degrade into the legacy
                    // slot-agnostic path, otherwise placement truth becomes implicit again.
                    AssistRejects++;
                    continue;
                }

                if (candidate is AssistMicroOp legacyAssistBackpressureCandidate &&
                    !TryReserveAssistBackpressure(
                        legacyAssistBackpressureCandidate,
                        ref assistBackpressureState,
                        memSub))
                {
                    continue;
                }

                if (candidate is AssistMicroOp legacyAssistCandidate &&
                    !TryReserveAssistQuota(legacyAssistCandidate, ref assistQuotaState))
                {
                    continue;
                }

                result[slot] = candidate;
                candidate.IsFspInjected = true;
                bundleCert.AddOperation(candidate);
                bundleMetadata = bundleMetadata.WithOperation(candidate);
                boundaryGuard = boundaryGuard.WithOperation(candidate);
                opportunityState = opportunityState.WithOccupiedSlot(slot);
                bundleOccupancy = opportunityState.OccupancyMask;
                _runtimeLegalityService.RefreshSmtAfterMutation(
                    _currentReplayPhase,
                    bundleCert,
                    bundleMetadata,
                    boundaryGuard);
                ConsumeProjectedMemoryIssueStateIfNeeded(candidate);
                RecordAssistInjection(candidate);
                assistInjectedCount++;
            }

            ClearAssistNominationPorts();
            return assistInjectedCount;
        }

        private void RecordAssistReject(MicroOp candidate, TypedSlotRejectReason rejectReason)
        {
            AssistRejects++;
            RecordTypedSlotReject(rejectReason, candidate);
        }

        private bool TryReserveAssistQuota(
            AssistMicroOp assistMicroOp,
            ref AssistMemoryQuotaState assistQuotaState)
        {
            if (assistQuotaState.TryReserve(
                assistMicroOp,
                out uint reservedLineDemand,
                out AssistQuotaRejectKind rejectKind))
            {
                assistMicroOp.BindReservedPrefetchLines(reservedLineDemand);
                AssistQuotaLinesReserved += reservedLineDemand;
                return true;
            }

            AssistQuotaRejects++;
            switch (rejectKind)
            {
                case AssistQuotaRejectKind.IssueCredits:
                    AssistQuotaIssueRejects++;
                    break;
                case AssistQuotaRejectKind.LineCredits:
                    AssistQuotaLineRejects++;
                    break;
            }

            RecordAssistReject(assistMicroOp, TypedSlotRejectReason.AssistQuotaReject);
            return false;
        }

        private void RecordAssistInjection(MicroOp candidate)
        {
            AssistInjectionsCount++;

            if (candidate is not AssistMicroOp assistMicroOp)
                return;

            LastAssistOwnershipSignature = AssistOwnershipFingerprint.Compute(assistMicroOp);

            if (assistMicroOp.DonorSource.IsCrossVirtualThread)
            {
                AssistDonorVtInjects++;
            }
            else
            {
                AssistSameVtInjects++;
            }

            switch (assistMicroOp.Kind)
            {
                case AssistKind.DonorPrefetch:
                    AssistDonorPrefetchInjects++;
                    break;
                case AssistKind.Ldsa:
                    AssistLdsaInjects++;
                    break;
                case AssistKind.Vdsa:
                    AssistVdsaInjects++;
                    break;
            }
        }

        private void RecordAssistInterCoreInjection(AssistMicroOp assistMicroOp)
        {
            AssistInterCoreInjections++;

            if (assistMicroOp.DonorSource.SourcePodId != assistMicroOp.PodId)
            {
                AssistInterCoreCrossPodInjections++;
            }
            else
            {
                AssistInterCorePodLocalInjections++;
            }

            if (assistMicroOp.Kind == AssistKind.Ldsa &&
                assistMicroOp.CarrierKind == AssistCarrierKind.Lane6Dma)
            {
                if (assistMicroOp.DonorSource.Kind == AssistDonorSourceKind.InterCoreSameVtColdStoreSeed ||
                    assistMicroOp.DonorSource.Kind == AssistDonorSourceKind.InterCoreVtDonorColdStoreSeed)
                {
                    AssistInterCoreLane6ColdStoreLdsaInjects++;
                    return;
                }

                AssistInterCoreLane6LdsaInjects++;
                return;
            }

            if (assistMicroOp.Kind == AssistKind.DonorPrefetch &&
                assistMicroOp.CarrierKind == AssistCarrierKind.Lane6Dma)
            {
                if (assistMicroOp.DonorSource.Kind == AssistDonorSourceKind.InterCoreSameVtHotLoadSeed ||
                    assistMicroOp.DonorSource.Kind == AssistDonorSourceKind.InterCoreVtDonorHotLoadSeed)
                {
                    AssistInterCoreLane6HotLoadDonorPrefetchInjects++;
                    return;
                }

                if (assistMicroOp.DonorSource.Kind == AssistDonorSourceKind.InterCoreSameVtHotStoreSeed ||
                    assistMicroOp.DonorSource.Kind == AssistDonorSourceKind.InterCoreVtDonorHotStoreSeed)
                {
                    AssistInterCoreLane6HotStoreDonorPrefetchInjects++;
                    return;
                }

                if (assistMicroOp.DonorSource.Kind == AssistDonorSourceKind.InterCoreSameVtDefaultStoreSeed ||
                    assistMicroOp.DonorSource.Kind == AssistDonorSourceKind.InterCoreVtDonorDefaultStoreSeed)
                {
                    AssistInterCoreLane6DefaultStoreDonorPrefetchInjects++;
                    return;
                }

                AssistInterCoreLane6DonorPrefetchInjects++;
                return;
            }

            if (assistMicroOp.Kind != AssistKind.Vdsa)
            {
                return;
            }

            switch (assistMicroOp.DonorSource.Kind)
            {
                case AssistDonorSourceKind.InterCoreSameVtVectorWriteback:
                    AssistInterCoreSameVtVectorWritebackInjects++;
                    break;
                case AssistDonorSourceKind.InterCoreVtDonorVectorWriteback:
                    AssistInterCoreDonorVtVectorWritebackInjects++;
                    break;
                case AssistDonorSourceKind.InterCoreSameVtVector:
                    AssistInterCoreSameVtVectorInjects++;
                    break;
                case AssistDonorSourceKind.InterCoreVtDonorVector:
                    AssistInterCoreDonorVtVectorInjects++;
                    break;
            }
        }

        private void RecordAssistInterCoreReject(
            in AssistInterCoreTransport transport,
            ushort requestingPodId,
            bool isDomainReject = false)
        {
            AssistInterCoreRejects++;

            bool isCrossPod =
                requestingPodId != ushort.MaxValue &&
                transport.DonorPodId != requestingPodId;

            if (isCrossPod)
            {
                AssistInterCoreCrossPodRejects++;
                if (isDomainReject)
                {
                    AssistInterCoreCrossPodDomainRejects++;
                }
            }
            else
            {
                AssistInterCorePodLocalRejects++;
                if (isDomainReject)
                {
                    AssistInterCorePodLocalDomainRejects++;
                }
            }

            if (isDomainReject)
            {
                AssistInterCoreDomainRejects++;
            }
        }
    }
}
