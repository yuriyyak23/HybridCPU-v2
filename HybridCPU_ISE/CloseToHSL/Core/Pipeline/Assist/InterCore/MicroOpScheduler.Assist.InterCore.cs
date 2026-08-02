using System;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Core
{
    public partial class MicroOpScheduler
    {
        public void NominateInterCoreAssistCandidate(int coreId, AssistInterCoreTransport transport)
        {
            if ((uint)coreId >= NUM_PORTS ||
                !transport.IsValid ||
                transport.DonorCoreId != coreId ||
                _coreStalled[coreId])
            {
                return;
            }

            _interCoreAssistPorts[coreId] = transport;
            _interCoreAssistPortValid[coreId] = true;
            AssistNominationCount++;
            AssistInterCoreNominations++;
        }

        public void ClearInterCoreAssistNominationPorts()
        {
            for (int coreId = 0; coreId < NUM_PORTS; coreId++)
            {
                ClearInterCoreAssistNominationPort(coreId);
            }
        }

        public bool IsCoreStalled(int coreId)
        {
            return (uint)coreId < NUM_PORTS &&
                   _coreStalled[coreId];
        }

        internal void ClearInterCoreAssistNominationPort(int coreId)
        {
            if ((uint)coreId >= NUM_PORTS)
            {
                return;
            }

            _interCoreAssistPorts[coreId] = default;
            _interCoreAssistPortValid[coreId] = false;
        }

        internal void ReconcileInterCoreAssistTransportOwnerSnapshot(
            int coreId,
            ushort podId,
            bool ownerSnapshotValid,
            in PodController.AssistOwnerSnapshot ownerSnapshot)
        {
            if (!TryPeekInterCoreAssistTransport(coreId, out AssistInterCoreTransport transport))
            {
                return;
            }

            if (IsInterCoreAssistTransportStale(
                    podId,
                    coreId,
                    ownerSnapshotValid,
                    ownerSnapshot,
                    transport))
            {
                ClearInterCoreAssistNominationPort(coreId);
            }
        }

        internal bool TryPeekInterCoreAssistTransport(
            int coreId,
            out AssistInterCoreTransport transport)
        {
            transport = default;
            if ((uint)coreId >= NUM_PORTS ||
                !_interCoreAssistPortValid[coreId] ||
                !_interCoreAssistPorts[coreId].IsValid)
            {
                return false;
            }

            transport = _interCoreAssistPorts[coreId];
            return true;
        }

        internal bool TryConsumeInterCoreAssistTransport(
            int coreId,
            out AssistInterCoreTransport transport)
        {
            if (!TryPeekInterCoreAssistTransport(coreId, out transport))
            {
                return false;
            }

            _interCoreAssistPorts[coreId] = default;
            _interCoreAssistPortValid[coreId] = false;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInterCoreAssistTransportStale(
            ushort donorPodId,
            int donorCoreId,
            bool ownerSnapshotValid,
            in PodController.AssistOwnerSnapshot ownerSnapshot,
            in AssistInterCoreTransport transport)
        {
            return !transport.IsValid ||
                   transport.DonorPodId != donorPodId ||
                   transport.DonorCoreId != donorCoreId ||
                   !ownerSnapshotValid ||
                   !ownerSnapshot.Matches(
                       transport.DonorVirtualThreadId,
                       transport.DonorOwnerContextId,
                       transport.DonorDomainTag) ||
                   ownerSnapshot.AssistEpochId != transport.DonorAssistEpochId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInterCoreAssistTransportStale(
            PodController? donorPod,
            int donorCoreId,
            in AssistInterCoreTransport transport)
        {
            return donorPod == null ||
                   donorPod.PodId != transport.DonorPodId ||
                   donorPod.Scheduler == null ||
                   donorPod.Scheduler.IsCoreStalled(donorCoreId) ||
                   !donorPod.TryGetAssistOwnerSnapshot(
                       donorCoreId,
                       out PodController.AssistOwnerSnapshot ownerSnapshot) ||
                   IsInterCoreAssistTransportStale(
                       donorPod.PodId,
                       donorCoreId,
                       ownerSnapshotValid: true,
                       ownerSnapshot: ownerSnapshot,
                       transport: transport);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryStealPodLocalInterCoreAssistTransport(
            int requestingCoreId,
            ushort requestingPodId,
            ulong requestedDomainTag,
            out AssistInterCoreTransport transport)
        {
            transport = default;
            for (int coreId = 0; coreId < NUM_PORTS; coreId++)
            {
                if (coreId == requestingCoreId ||
                    !_interCoreAssistPortValid[coreId] ||
                    !_interCoreAssistPorts[coreId].IsValid)
                {
                    continue;
                }

                AssistInterCoreTransport candidateTransport = _interCoreAssistPorts[coreId];
                PodController? donorPod = Processor.GetPodById(candidateTransport.DonorPodId);
                if (IsInterCoreAssistTransportStale(donorPod, coreId, candidateTransport))
                {
                    RecordAssistInterCoreReject(candidateTransport, requestingPodId);
                    ClearInterCoreAssistNominationPort(coreId);
                    continue;
                }

                if (requestedDomainTag != 0)
                {
                    InterCoreDomainGuardDecision domainGuard =
                        _runtimeLegalityService.EvaluateInterCoreDomainGuard(
                            candidateTransport.Seed,
                            requestedDomainTag);
                    RecordDomainIsolationProbe(domainGuard.ProbeResult);
                    if (!domainGuard.IsAllowed)
                    {
                        RecordAssistInterCoreReject(
                            candidateTransport,
                            requestingPodId,
                            isDomainReject: true);
                        continue;
                    }
                }

                _interCoreAssistPorts[coreId] = default;
                _interCoreAssistPortValid[coreId] = false;
                transport = candidateTransport;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryStealInterCoreAssistTransport(
            int requestingCoreId,
            ushort requestingPodId,
            ulong requestedDomainTag,
            out AssistInterCoreTransport transport,
            PodController?[]? pods = null)
        {
            return TryStealPodLocalInterCoreAssistTransport(
                       requestingCoreId,
                       requestingPodId,
                       requestedDomainTag,
                       out transport) ||
                   TryStealCrossPodAssistTransport(
                       requestingPodId,
                       requestedDomainTag,
                       out transport,
                       pods);
        }

        private int TryInjectInterCoreAssistCandidates(
            MicroOp[] result,
            ref BundleResourceCertificate bundleCert,
            ref BundleOpportunityState opportunityState,
            int ownerVirtualThreadId,
            int requestingCoreId,
            ushort requestingPodId,
            int bundleOwnerContextId,
            ulong bundleOwnerDomainTag,
            ulong requestedDomainTag,
            ulong assistRuntimeEpoch,
            ResourceBitset globalResourceLocks,
            int currentPassInjections,
            YAKSys_Hybrid_CPU.Memory.MemorySubsystem? memSub = null,
            PodController?[]? pods = null)
        {
            if (!AssistInjectionEnabled ||
                result == null ||
                result.Length != 8 ||
                requestingCoreId < 0 ||
                bundleOwnerContextId < 0 ||
                ResolveNextInjectableSlot(opportunityState, 0) < 0)
            {
                return 0;
            }

            AssistMemoryQuotaState assistQuotaState = AssistMemoryQuotaPolicy.CreateState();
            AssistBackpressureState assistBackpressureState =
                AssistBackpressurePolicy.CreateState(CaptureAssistBackpressureSnapshot());
            int injectedCount = 0;
            var hwMask = new SafetyMask128(globalResourceLocks.Low, globalResourceLocks.High);

            while (injectedCount < AssistBundleQuota &&
                   TryStealInterCoreAssistTransport(
                       requestingCoreId,
                       requestingPodId,
                       requestedDomainTag,
                       out AssistInterCoreTransport transport,
                       pods))
            {
                ushort targetPodId =
                    requestingPodId == ushort.MaxValue
                        ? transport.DonorPodId
                        : requestingPodId;

                if (!AssistMicroOp.TryCreateFromInterCoreTransport(
                        transport,
                        podId: targetPodId,
                        carrierCoreId: requestingCoreId,
                        carrierVirtualThreadId: ownerVirtualThreadId,
                        targetCoreId: requestingCoreId,
                        targetVirtualThreadId: ownerVirtualThreadId,
                        targetOwnerContextId: bundleOwnerContextId,
                        targetDomainTag: bundleOwnerDomainTag,
                        replayEpochId: _currentReplayPhase.IsActive ? _currentReplayPhase.EpochId : 0,
                        assistEpochId: assistRuntimeEpoch,
                        out AssistMicroOp assistCandidate))
                {
                    AssistRejects++;
                    RecordAssistInterCoreReject(transport, requestingPodId);
                    continue;
                }

                LegalityDecision legalityDecision = EvaluateInterCoreLegality(
                    result,
                    bundleCert,
                    ownerVirtualThreadId,
                    requestedDomainTag,
                    assistCandidate,
                    hwMask);
                if (!legalityDecision.IsAllowed)
                {
                    AssistRejects++;
                    RecordAssistInterCoreReject(transport, requestingPodId);
                    continue;
                }

                if (!TryMaterializeLane(
                        assistCandidate,
                        opportunityState.OccupancyMask,
                        out int lane,
                        out TypedSlotRejectReason rejectReason))
                {
                    RecordAssistInterCoreReject(transport, requestingPodId);
                    RecordAssistReject(assistCandidate, rejectReason);
                    continue;
                }

                if (!TryReserveAssistBackpressure(
                        assistCandidate,
                        ref assistBackpressureState,
                        memSub))
                {
                    RecordAssistInterCoreReject(transport, requestingPodId);
                    continue;
                }

                if (!TryReserveAssistQuota(assistCandidate, ref assistQuotaState))
                {
                    RecordAssistInterCoreReject(transport, requestingPodId);
                    continue;
                }

                result[lane] = assistCandidate;
                assistCandidate.IsFspInjected = true;
                bundleCert = bundleCert.WithOperation(assistCandidate);
                opportunityState = opportunityState.WithOccupiedSlot(lane);
                _runtimeLegalityService.RefreshInterCoreAfterMutation(
                    _currentReplayPhase,
                    bundleCert);
                _classCapacity.IncrementOccupancy(assistCandidate.Placement.RequiredSlotClass);
                ConsumeProjectedMemoryIssueStateIfNeeded(assistCandidate);
                RecordTypedSlotInject(assistCandidate, lane);
                RecordAssistInjection(assistCandidate);
                RecordAssistInterCoreInjection(assistCandidate);
                injectedCount++;
            }

            return injectedCount;
        }
    }
}
