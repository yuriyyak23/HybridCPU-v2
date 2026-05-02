using System;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute
{
    public enum DmaStreamComputeRetireObservationKind : byte
    {
        StallOrDrain = 0,
        CommitReadyMemoryNotVisible = 1,
        FaultPublicationCandidate = 2,
        CancellationSuppressesPublication = 3,
        IllegalCommittedBeforeRetireObservation = 4
    }

    public enum DmaStreamComputeRetirePublicationSurfaceKind : byte
    {
        ExplicitTestFutureSeam = 0
    }

    public sealed record DmaStreamComputeRetirePublicationMetadata
    {
        public DmaStreamComputeRetirePublicationMetadata(
            ulong tokenId,
            ulong issuingPc,
            ulong bundleId,
            ulong architecturalInstructionAge,
            byte slotIndex,
            byte laneIndex,
            SlotClass slotClass,
            SlotClass laneClass,
            int ownerThreadId,
            ushort ownerVirtualThreadId,
            uint ownerContextId,
            uint ownerCoreId,
            uint ownerPodId,
            ulong ownerDomainTag,
            ulong activeDomainCertificate,
            uint deviceId,
            ulong descriptorAddress,
            ulong descriptorIdentityHash,
            ulong normalizedFootprintHash,
            ulong issueCycle,
            ulong completionCycle,
            ulong replayEpoch)
        {
            TokenId = tokenId;
            IssuingPc = issuingPc;
            BundleId = bundleId;
            ArchitecturalInstructionAge = architecturalInstructionAge;
            SlotIndex = slotIndex;
            LaneIndex = laneIndex;
            SlotClass = slotClass;
            LaneClass = laneClass;
            OwnerThreadId = ownerThreadId;
            OwnerVirtualThreadId = ownerVirtualThreadId;
            OwnerContextId = ownerContextId;
            OwnerCoreId = ownerCoreId;
            OwnerPodId = ownerPodId;
            OwnerDomainTag = ownerDomainTag;
            ActiveDomainCertificate = activeDomainCertificate;
            DeviceId = deviceId;
            DescriptorAddress = descriptorAddress;
            DescriptorIdentityHash = descriptorIdentityHash;
            NormalizedFootprintHash = normalizedFootprintHash;
            IssueCycle = issueCycle;
            CompletionCycle = completionCycle;
            ReplayEpoch = replayEpoch;
        }

        public ulong TokenId { get; }

        public ulong IssuingPc { get; }

        public ulong BundleId { get; }

        public ulong ArchitecturalInstructionAge { get; }

        public byte SlotIndex { get; }

        public byte LaneIndex { get; }

        public SlotClass SlotClass { get; }

        public SlotClass LaneClass { get; }

        public int OwnerThreadId { get; }

        public ushort OwnerVirtualThreadId { get; }

        public uint OwnerContextId { get; }

        public uint OwnerCoreId { get; }

        public uint OwnerPodId { get; }

        public ulong OwnerDomainTag { get; }

        public ulong ActiveDomainCertificate { get; }

        public uint DeviceId { get; }

        public ulong DescriptorAddress { get; }

        public ulong DescriptorIdentityHash { get; }

        public ulong NormalizedFootprintHash { get; }

        public ulong IssueCycle { get; }

        public ulong CompletionCycle { get; }

        public ulong ReplayEpoch { get; }

        public bool HasRequiredFuturePreciseIdentity =>
            TokenId != 0 &&
            SlotIndex < 8 &&
            LaneIndex < 8 &&
            OwnerThreadId >= 0 &&
            DescriptorIdentityHash != 0 &&
            DescriptorAddress != 0;

        public static DmaStreamComputeRetirePublicationMetadata FromActiveEntry(
            DmaStreamComputeActiveTokenEntry entry,
            ulong architecturalInstructionAge,
            int ownerThreadId = -1,
            ulong completionCycle = 0)
        {
            ArgumentNullException.ThrowIfNull(entry);

            DmaStreamComputeDescriptor descriptor = entry.Token.Descriptor;
            DmaStreamComputeOwnerBinding owner = descriptor.OwnerBinding;
            DmaStreamComputeOwnerGuardContext guardContext =
                descriptor.OwnerGuardDecision.RuntimeOwnerContext;
            int resolvedOwnerThreadId = ownerThreadId >= 0
                ? ownerThreadId
                : owner.OwnerVirtualThreadId;

            return new DmaStreamComputeRetirePublicationMetadata(
                entry.Handle.TokenId,
                entry.Metadata.IssuingPc,
                entry.Metadata.BundleId,
                architecturalInstructionAge,
                entry.Metadata.SlotIndex,
                entry.Metadata.LaneIndex,
                SlotClass.DmaStreamClass,
                SlotClass.DmaStreamClass,
                resolvedOwnerThreadId,
                owner.OwnerVirtualThreadId,
                owner.OwnerContextId,
                owner.OwnerCoreId,
                owner.OwnerPodId,
                owner.OwnerDomainTag,
                guardContext.ActiveDomainCertificate,
                owner.DeviceId,
                descriptor.DescriptorReference.DescriptorAddress,
                descriptor.DescriptorIdentityHash,
                descriptor.NormalizedFootprintHash,
                entry.Metadata.IssueCycle,
                completionCycle,
                entry.Metadata.ReplayEpoch);
        }
    }

    public sealed record DmaStreamComputeRetireObservation
    {
        public DmaStreamComputeRetireObservation(
            DmaStreamComputeRetireObservationKind kind,
            DmaStreamComputeTokenState observedState,
            DmaStreamComputeRetirePublicationMetadata metadata,
            DmaStreamComputeFaultRecord? fault)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            Kind = kind;
            ObservedState = observedState;
            Metadata = metadata;
            Fault = fault;
        }

        public DmaStreamComputeRetireObservationKind Kind { get; }

        public DmaStreamComputeTokenState ObservedState { get; }

        public DmaStreamComputeRetirePublicationMetadata Metadata { get; }

        public DmaStreamComputeFaultRecord? Fault { get; }

        public DmaStreamComputeRetirePublicationSurfaceKind SurfaceKind =>
            DmaStreamComputeRetirePublicationSurfaceKind.ExplicitTestFutureSeam;

        public bool IsExplicitFuturePublicationSurface => true;

        public bool IsNormalPipelineExecutableLane6Path => false;

        public bool MemoryMayBeVisible =>
            Kind == DmaStreamComputeRetireObservationKind.IllegalCommittedBeforeRetireObservation;

        public bool RequiresRetireStallOrDrain =>
            Kind == DmaStreamComputeRetireObservationKind.StallOrDrain;

        public bool RequiresCommitAttempt =>
            Kind == DmaStreamComputeRetireObservationKind.CommitReadyMemoryNotVisible;

        public bool RequiresExceptionPublication =>
            Kind == DmaStreamComputeRetireObservationKind.FaultPublicationCandidate &&
            Fault?.RequiresRetireExceptionPublication == true;
    }

    public static class DmaStreamComputeRetirePublication
    {
        public static DmaStreamComputeRetireObservation ObserveFutureRetire(
            DmaStreamComputeActiveTokenEntry entry,
            ulong architecturalInstructionAge,
            int ownerThreadId = -1,
            ulong completionCycle = 0)
        {
            ArgumentNullException.ThrowIfNull(entry);

            DmaStreamComputeRetirePublicationMetadata metadata =
                DmaStreamComputeRetirePublicationMetadata.FromActiveEntry(
                    entry,
                    architecturalInstructionAge,
                    ownerThreadId,
                    completionCycle);
            DmaStreamComputeToken token = entry.Token;
            DmaStreamComputeRetireObservationKind kind = token.State switch
            {
                DmaStreamComputeTokenState.Admitted or
                DmaStreamComputeTokenState.Issued or
                DmaStreamComputeTokenState.ReadsComplete or
                DmaStreamComputeTokenState.ComputeComplete
                    => DmaStreamComputeRetireObservationKind.StallOrDrain,

                DmaStreamComputeTokenState.CommitPending
                    => DmaStreamComputeRetireObservationKind.CommitReadyMemoryNotVisible,

                DmaStreamComputeTokenState.Faulted
                    => DmaStreamComputeRetireObservationKind.FaultPublicationCandidate,

                DmaStreamComputeTokenState.Canceled
                    => DmaStreamComputeRetireObservationKind.CancellationSuppressesPublication,

                DmaStreamComputeTokenState.Committed
                    => DmaStreamComputeRetireObservationKind.IllegalCommittedBeforeRetireObservation,

                _ => DmaStreamComputeRetireObservationKind.StallOrDrain
            };

            return new DmaStreamComputeRetireObservation(
                kind,
                token.State,
                metadata,
                token.LastFault);
        }

        public static DmaStreamComputeCommitResult NormalizeBackendExceptionToTokenFault(
            DmaStreamComputeToken token,
            Exception exception,
            ulong faultAddress,
            bool isWrite,
            DmaStreamComputeFaultSourcePhase sourcePhase = DmaStreamComputeFaultSourcePhase.Backend)
        {
            ArgumentNullException.ThrowIfNull(token);
            ArgumentNullException.ThrowIfNull(exception);

            DmaStreamComputeFaultRecord fault =
                DmaStreamComputeFaultRecord.FromBackendException(
                    token.Descriptor,
                    exception,
                    faultAddress,
                    isWrite,
                    sourcePhase);
            return token.PublishFault(fault);
        }

        public static DmaStreamComputeFaultPriorityClass ResolveSameTokenFaultPriority(
            DmaStreamComputeTokenFaultKind faultKind,
            DmaStreamComputeFaultSourcePhase sourcePhase)
        {
            if (sourcePhase == DmaStreamComputeFaultSourcePhase.Cancellation)
            {
                return DmaStreamComputeFaultPriorityClass.PostIssueCancellation;
            }

            if (sourcePhase == DmaStreamComputeFaultSourcePhase.CarrierDecode)
            {
                return DmaStreamComputeFaultPriorityClass.CarrierDecodeAndTypedSlot;
            }

            if (sourcePhase is DmaStreamComputeFaultSourcePhase.DescriptorParse
                or DmaStreamComputeFaultSourcePhase.Admission)
            {
                return DmaStreamComputeFaultPriorityClass.DescriptorAdmission;
            }

            if (sourcePhase == DmaStreamComputeFaultSourcePhase.Commit)
            {
                return faultKind is DmaStreamComputeTokenFaultKind.DomainViolation
                    or DmaStreamComputeTokenFaultKind.OwnerContextViolation
                    or DmaStreamComputeTokenFaultKind.DmaDeviceFault
                    ? DmaStreamComputeFaultPriorityClass.CommitGuard
                    : DmaStreamComputeFaultPriorityClass.CommitPhysicalWriteRollback;
            }

            return faultKind switch
            {
                DmaStreamComputeTokenFaultKind.DescriptorDecodeFault or
                DmaStreamComputeTokenFaultKind.ExecutionDisabled
                    => DmaStreamComputeFaultPriorityClass.CarrierDecodeAndTypedSlot,

                DmaStreamComputeTokenFaultKind.UnsupportedAbiOrOperation or
                DmaStreamComputeTokenFaultKind.AlignmentFault or
                DmaStreamComputeTokenFaultKind.AliasOverlapFault or
                DmaStreamComputeTokenFaultKind.DomainViolation or
                DmaStreamComputeTokenFaultKind.OwnerContextViolation
                    => DmaStreamComputeFaultPriorityClass.DescriptorAdmission,

                DmaStreamComputeTokenFaultKind.TranslationFault or
                DmaStreamComputeTokenFaultKind.PermissionFault or
                DmaStreamComputeTokenFaultKind.DmaDeviceFault or
                DmaStreamComputeTokenFaultKind.MemoryFault
                    => DmaStreamComputeFaultPriorityClass.RuntimeReadBackendIommu,

                DmaStreamComputeTokenFaultKind.PartialCompletionFault
                    => DmaStreamComputeFaultPriorityClass.ComputeStageCoverage,

                DmaStreamComputeTokenFaultKind.ReplayInvalidationBeforeCommit
                    => DmaStreamComputeFaultPriorityClass.PostIssueCancellation,

                _ => DmaStreamComputeFaultPriorityClass.DescriptorAdmission
            };
        }

        public static DmaStreamComputeFaultPriorityClass ResolveValidationFaultPriority(
            DmaStreamComputeValidationFault validationFault)
        {
            return validationFault switch
            {
                DmaStreamComputeValidationFault.QuotaAdmissionReject or
                DmaStreamComputeValidationFault.BackpressureAdmissionReject or
                DmaStreamComputeValidationFault.TokenCapAdmissionReject
                    => DmaStreamComputeFaultPriorityClass.QuotaBackpressureTokenCap,

                DmaStreamComputeValidationFault.DescriptorCarrierDecodeFault
                    => DmaStreamComputeFaultPriorityClass.CarrierDecodeAndTypedSlot,

                DmaStreamComputeValidationFault.None
                    => throw new ArgumentException(
                        "Successful validation does not have a DSC fault priority.",
                        nameof(validationFault)),

                _ => DmaStreamComputeFaultPriorityClass.DescriptorAdmission
            };
        }
    }
}
