namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            /// <summary>
            /// Immutable authoritative observation snapshot exported for diagnostics,
            /// state-accessor snapshots, trace emission, and bridge projection.
            /// It flattens the current runtime-visible decode/pipeline contour so
            /// consumers do not need to infer truth from adjacent canonical/residual fields.
            /// </summary>
            internal readonly struct PipelineObservationSnapshot
            {
                internal PipelineObservationSnapshot(
                    int activeVirtualThreadId,
                    ulong activeLivePc,
                    Core.CoreTimebaseSnapshot timebase,
                    Core.PipelineState currentVirtualThreadPipelineState,
                    PipelineControl pipelineControl,
                    byte pipelineBundleSlotIndex,
                    ulong decodedBundlePc,
                    Core.DecodedBundleStateOwnerKind decodedBundleStateOwnerKind,
                    ulong decodedBundleStateEpoch,
                    ulong decodedBundleStateVersion,
                    Core.DecodedBundleStateKind decodedBundleStateKind,
                    Core.DecodedBundleStateOrigin decodedBundleStateOrigin,
                    byte decodedBundleValidMask,
                    byte decodedBundleNopMask,
                    bool decodedBundleHasCanonicalDecode,
                    bool decodedBundleHasCanonicalLegality,
                    bool decodedBundleHasDecodeFault,
                    Core.PipelineContourCertificate decodePublicationCertificate,
                    Core.PipelineContourCertificate executeCompletionCertificate,
                    Core.PipelineContourCertificate retireVisibilityCertificate,
                    Core.RuntimeClusterAdmissionPreparation admissionPreparation,
                    Core.RuntimeClusterAdmissionCandidateView admissionCandidateView,
                    Core.RuntimeClusterAdmissionDecisionDraft admissionDecisionDraft,
                    Core.RuntimeClusterAdmissionHandoff admissionHandoff)
                {
                    ActiveVirtualThreadId = activeVirtualThreadId;
                    ActiveLivePc = activeLivePc;
                    Timebase = timebase;
                    CurrentVirtualThreadPipelineState = currentVirtualThreadPipelineState;
                    PipelineControl = pipelineControl;
                    PipelineBundleSlotIndex = pipelineBundleSlotIndex;
                    DecodedBundlePc = decodedBundlePc;
                    DecodedBundleStateOwnerKind = decodedBundleStateOwnerKind;
                    DecodedBundleStateEpoch = decodedBundleStateEpoch;
                    DecodedBundleStateVersion = decodedBundleStateVersion;
                    DecodedBundleStateKind = decodedBundleStateKind;
                    DecodedBundleStateOrigin = decodedBundleStateOrigin;
                    DecodedBundleValidMask = decodedBundleValidMask;
                    DecodedBundleNopMask = decodedBundleNopMask;
                    DecodedBundleHasCanonicalDecode = decodedBundleHasCanonicalDecode;
                    DecodedBundleHasCanonicalLegality = decodedBundleHasCanonicalLegality;
                    DecodedBundleHasDecodeFault = decodedBundleHasDecodeFault;
                    DecodePublicationCertificate = decodePublicationCertificate;
                    ExecuteCompletionCertificate = executeCompletionCertificate;
                    RetireVisibilityCertificate = retireVisibilityCertificate;
                    AdmissionPreparation = admissionPreparation;
                    AdmissionCandidateView = admissionCandidateView;
                    AdmissionDecisionDraft = admissionDecisionDraft;
                    AdmissionHandoff = admissionHandoff;
                }

                public int ActiveVirtualThreadId { get; }
                public ulong ActiveLivePc { get; }
                public Core.CoreTimebaseSnapshot Timebase { get; }
                public Core.PipelineState CurrentVirtualThreadPipelineState { get; }
                public PipelineControl PipelineControl { get; }
                public byte PipelineBundleSlotIndex { get; }
                public ulong DecodedBundlePc { get; }
                public Core.DecodedBundleStateOwnerKind DecodedBundleStateOwnerKind { get; }
                public ulong DecodedBundleStateEpoch { get; }
                public ulong DecodedBundleStateVersion { get; }
                public Core.DecodedBundleStateKind DecodedBundleStateKind { get; }
                public Core.DecodedBundleStateOrigin DecodedBundleStateOrigin { get; }
                public byte DecodedBundleValidMask { get; }
                public byte DecodedBundleNopMask { get; }
                public bool DecodedBundleHasCanonicalDecode { get; }
                public bool DecodedBundleHasCanonicalLegality { get; }
                public bool DecodedBundleHasDecodeFault { get; }
                public Core.PipelineContourCertificate DecodePublicationCertificate { get; }
                public Core.PipelineContourCertificate ExecuteCompletionCertificate { get; }
                public Core.PipelineContourCertificate RetireVisibilityCertificate { get; }
                public Core.RuntimeClusterAdmissionPreparation AdmissionPreparation { get; }
                public Core.RuntimeClusterAdmissionCandidateView AdmissionCandidateView { get; }
                public Core.RuntimeClusterAdmissionDecisionDraft AdmissionDecisionDraft { get; }
                public Core.RuntimeClusterAdmissionHandoff AdmissionHandoff { get; }
            }

            /// <summary>
            /// Export the authoritative pipeline/decode observation contour for UI, trace,
            /// and bridge consumers. This is a read-only copy of the current runtime contour.
            /// </summary>
            internal PipelineObservationSnapshot GetPipelineObservationSnapshot()
            {
                ReadCurrentForegroundDecodedBundleRuntimeState(
                    out Core.DecodedBundleRuntimeState observationState);
                Core.DecodedBundleTransportFacts observationTransportFacts =
                    observationState.TransportFacts;
                Core.CoreTimebaseSnapshot timebase = new(
                    cycleCount: pipeCtrl.CycleCount,
                    isStalled: pipeCtrl.Stalled,
                    isAvailable: pipeCtrl.Enabled ||
                                 pipeCtrl.CycleCount != 0 ||
                                 pipeCtrl.InstructionsRetired != 0 ||
                                 pipeCtrl.StallCycles != 0 ||
                                 pipeCtrl.Stalled,
                    unavailableReason: pipeCtrl.Enabled ||
                                       pipeCtrl.CycleCount != 0 ||
                                       pipeCtrl.InstructionsRetired != 0 ||
                                       pipeCtrl.StallCycles != 0 ||
                                       pipeCtrl.Stalled
                        ? string.Empty
                        : "Pipeline timebase has not published any live timing facts yet.");

                return new PipelineObservationSnapshot(
                    activeVirtualThreadId: ReadActiveVirtualThreadId(),
                    activeLivePc: ReadActiveLivePc(),
                    timebase: timebase,
                    currentVirtualThreadPipelineState: ReadActiveVirtualThreadPipelineState(),
                    pipelineControl: pipeCtrl,
                    pipelineBundleSlotIndex: pipelineBundleSlot,
                    decodedBundlePc: observationTransportFacts.PC,
                    decodedBundleStateOwnerKind: observationState.StateOwnerKind,
                    decodedBundleStateEpoch: observationState.StateEpoch,
                    decodedBundleStateVersion: observationState.StateVersion,
                    decodedBundleStateKind: observationTransportFacts.StateKind,
                    decodedBundleStateOrigin: observationTransportFacts.StateOrigin,
                    decodedBundleValidMask: observationTransportFacts.ValidMask,
                    decodedBundleNopMask: observationTransportFacts.NopMask,
                    decodedBundleHasCanonicalDecode: observationState.HasCanonicalDecode,
                    decodedBundleHasCanonicalLegality: observationState.HasCanonicalLegality,
                    decodedBundleHasDecodeFault: observationState.HasDecodeFault,
                    decodePublicationCertificate: decodePublicationCertificate,
                    executeCompletionCertificate: executeCompletionCertificate,
                    retireVisibilityCertificate: retireVisibilityCertificate,
                    admissionPreparation: pipeIDAdmissionPreparation,
                    admissionCandidateView: pipeIDAdmissionCandidateView,
                    admissionDecisionDraft: pipeIDAdmissionDecisionDraft,
                    admissionHandoff: pipeIDAdmissionHandoff);
            }
        }
    }
}
