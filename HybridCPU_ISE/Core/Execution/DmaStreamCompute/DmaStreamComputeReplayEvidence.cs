using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute
{
    public enum DmaStreamComputeCarrierKind : byte
    {
        None = 0,
        CanonicalDecodedSideband = 1,
        CustomRegistryCarrier = 2,
        MissingDescriptorPayload = 3
    }

    public enum DmaStreamComputeTokenEvidenceKind : byte
    {
        NotCreated = 0,
        LifecycleSnapshot = 1
    }

    public readonly record struct DmaStreamComputeCarrierEvidence
    {
        private DmaStreamComputeCarrierEvidence(
            DmaStreamComputeCarrierKind carrierKind,
            ushort canonicalOpcode,
            DmaStreamComputeDescriptorReference descriptorReference,
            ulong carrierIdentityHash)
        {
            CarrierKind = carrierKind;
            CanonicalOpcode = canonicalOpcode;
            DescriptorReference = descriptorReference;
            CarrierIdentityHash = carrierIdentityHash;
        }

        public DmaStreamComputeCarrierKind CarrierKind { get; }

        public ushort CanonicalOpcode { get; }

        public DmaStreamComputeDescriptorReference DescriptorReference { get; }

        public ulong CarrierIdentityHash { get; }

        public bool HasDescriptorPayload =>
            DescriptorReference.DescriptorSize != 0 &&
            DescriptorReference.DescriptorIdentityHash != 0;

        public bool IsReplayReusable =>
            CarrierKind == DmaStreamComputeCarrierKind.CanonicalDecodedSideband &&
            HasDescriptorPayload &&
            CarrierIdentityHash != 0;

        public static DmaStreamComputeCarrierEvidence CanonicalDecodedSideband(
            DmaStreamComputeDescriptorReference descriptorReference,
            ushort canonicalOpcode = 0)
        {
            return new DmaStreamComputeCarrierEvidence(
                DmaStreamComputeCarrierKind.CanonicalDecodedSideband,
                canonicalOpcode,
                descriptorReference,
                ComputeHash(
                    DmaStreamComputeCarrierKind.CanonicalDecodedSideband,
                    canonicalOpcode,
                    descriptorReference));
        }

        public static DmaStreamComputeCarrierEvidence MissingDescriptorPayload(
            DmaStreamComputeDescriptorReference descriptorReference,
            ushort canonicalOpcode = 0)
        {
            return new DmaStreamComputeCarrierEvidence(
                DmaStreamComputeCarrierKind.MissingDescriptorPayload,
                canonicalOpcode,
                descriptorReference,
                ComputeHash(
                    DmaStreamComputeCarrierKind.MissingDescriptorPayload,
                    canonicalOpcode,
                    descriptorReference));
        }

        public static DmaStreamComputeCarrierEvidence CustomRegistryCarrier(
            DmaStreamComputeDescriptorReference descriptorReference,
            ushort customOpcode)
        {
            return new DmaStreamComputeCarrierEvidence(
                DmaStreamComputeCarrierKind.CustomRegistryCarrier,
                customOpcode,
                descriptorReference,
                ComputeHash(
                    DmaStreamComputeCarrierKind.CustomRegistryCarrier,
                    customOpcode,
                    descriptorReference));
        }

        private static ulong ComputeHash(
            DmaStreamComputeCarrierKind carrierKind,
            ushort canonicalOpcode,
            DmaStreamComputeDescriptorReference descriptorReference)
        {
            var hasher = new HardwareHash();
            hasher.Initialize();
            hasher.Compress((ulong)(byte)carrierKind);
            hasher.Compress(canonicalOpcode);
            hasher.Compress(descriptorReference.DescriptorAddress);
            hasher.Compress(descriptorReference.DescriptorSize);
            hasher.Compress(descriptorReference.DescriptorIdentityHash);
            return NormalizeHash(hasher.Finalize());
        }

        private static ulong NormalizeHash(ulong hash) =>
            hash == 0 ? 0xD5C0_CA22_1UL : hash;
    }

    public readonly record struct DmaStreamComputeFootprintEvidence
    {
        public DmaStreamComputeFootprintEvidence(
            ulong normalizedReadFootprintHash,
            ulong normalizedWriteFootprintHash,
            ulong normalizedFootprintHash,
            DmaStreamComputeAliasPolicy aliasPolicy,
            ushort normalizedReadRangeCount,
            ushort normalizedWriteRangeCount)
        {
            NormalizedReadFootprintHash = normalizedReadFootprintHash;
            NormalizedWriteFootprintHash = normalizedWriteFootprintHash;
            NormalizedFootprintHash = normalizedFootprintHash;
            AliasPolicy = aliasPolicy;
            NormalizedReadRangeCount = normalizedReadRangeCount;
            NormalizedWriteRangeCount = normalizedWriteRangeCount;
        }

        public ulong NormalizedReadFootprintHash { get; }

        public ulong NormalizedWriteFootprintHash { get; }

        public ulong NormalizedFootprintHash { get; }

        public DmaStreamComputeAliasPolicy AliasPolicy { get; }

        public ushort NormalizedReadRangeCount { get; }

        public ushort NormalizedWriteRangeCount { get; }

        public bool IsComplete =>
            NormalizedReadFootprintHash != 0 &&
            NormalizedWriteFootprintHash != 0 &&
            NormalizedFootprintHash != 0 &&
            NormalizedReadRangeCount != 0 &&
            NormalizedWriteRangeCount != 0;

        public static DmaStreamComputeFootprintEvidence FromDescriptor(
            DmaStreamComputeDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            return new DmaStreamComputeFootprintEvidence(
                ComputeRangeHash(descriptor.NormalizedReadMemoryRanges),
                ComputeRangeHash(descriptor.NormalizedWriteMemoryRanges),
                descriptor.NormalizedFootprintHash,
                descriptor.AliasPolicy,
                checked((ushort)descriptor.NormalizedReadMemoryRanges.Count),
                checked((ushort)descriptor.NormalizedWriteMemoryRanges.Count));
        }

        private static ulong ComputeRangeHash(
            IReadOnlyList<DmaStreamComputeMemoryRange> ranges)
        {
            var hasher = new HardwareHash();
            hasher.Initialize();
            hasher.Compress((ulong)(ranges?.Count ?? 0));
            if (ranges != null)
            {
                for (int i = 0; i < ranges.Count; i++)
                {
                    hasher.Compress(ranges[i].Address);
                    hasher.Compress(ranges[i].Length);
                }
            }

            return NormalizeHash(hasher.Finalize());
        }

        private static ulong NormalizeHash(ulong hash) =>
            hash == 0 ? 0xD5C0_F007UL : hash;
    }

    public readonly record struct DmaStreamComputeOwnerDomainEvidence
    {
        public DmaStreamComputeOwnerDomainEvidence(
            ushort ownerVirtualThreadId,
            uint ownerContextId,
            uint ownerCoreId,
            uint ownerPodId,
            ulong ownerDomainTag,
            uint deviceId,
            ushort runtimeOwnerVirtualThreadId,
            uint runtimeOwnerContextId,
            uint runtimeOwnerCoreId,
            uint runtimeOwnerPodId,
            ulong runtimeOwnerDomainTag,
            ulong activeDomainCertificate,
            uint runtimeDeviceId,
            bool guardAllowed,
            RejectKind rejectKind,
            LegalityAuthoritySource authoritySource,
            bool attemptedReplayCertificateReuse,
            ulong evidenceHash)
        {
            OwnerVirtualThreadId = ownerVirtualThreadId;
            OwnerContextId = ownerContextId;
            OwnerCoreId = ownerCoreId;
            OwnerPodId = ownerPodId;
            OwnerDomainTag = ownerDomainTag;
            DeviceId = deviceId;
            RuntimeOwnerVirtualThreadId = runtimeOwnerVirtualThreadId;
            RuntimeOwnerContextId = runtimeOwnerContextId;
            RuntimeOwnerCoreId = runtimeOwnerCoreId;
            RuntimeOwnerPodId = runtimeOwnerPodId;
            RuntimeOwnerDomainTag = runtimeOwnerDomainTag;
            ActiveDomainCertificate = activeDomainCertificate;
            RuntimeDeviceId = runtimeDeviceId;
            GuardAllowed = guardAllowed;
            RejectKind = rejectKind;
            AuthoritySource = authoritySource;
            AttemptedReplayCertificateReuse = attemptedReplayCertificateReuse;
            EvidenceHash = evidenceHash;
        }

        public ushort OwnerVirtualThreadId { get; }

        public uint OwnerContextId { get; }

        public uint OwnerCoreId { get; }

        public uint OwnerPodId { get; }

        public ulong OwnerDomainTag { get; }

        public uint DeviceId { get; }

        public ushort RuntimeOwnerVirtualThreadId { get; }

        public uint RuntimeOwnerContextId { get; }

        public uint RuntimeOwnerCoreId { get; }

        public uint RuntimeOwnerPodId { get; }

        public ulong RuntimeOwnerDomainTag { get; }

        public ulong ActiveDomainCertificate { get; }

        public uint RuntimeDeviceId { get; }

        public bool GuardAllowed { get; }

        public RejectKind RejectKind { get; }

        public LegalityAuthoritySource AuthoritySource { get; }

        public bool AttemptedReplayCertificateReuse { get; }

        public ulong EvidenceHash { get; }

        public bool IsComplete =>
            GuardAllowed &&
            AuthoritySource == LegalityAuthoritySource.GuardPlane &&
            !AttemptedReplayCertificateReuse &&
            EvidenceHash != 0;

        public static DmaStreamComputeOwnerDomainEvidence FromDecision(
            DmaStreamComputeOwnerBinding ownerBinding,
            DmaStreamComputeOwnerGuardDecision guardDecision)
        {
            ArgumentNullException.ThrowIfNull(ownerBinding);

            DmaStreamComputeOwnerGuardContext context = guardDecision.RuntimeOwnerContext;
            var hasher = new HardwareHash();
            hasher.Initialize();
            hasher.Compress(ownerBinding.OwnerVirtualThreadId);
            hasher.Compress(ownerBinding.OwnerContextId);
            hasher.Compress(ownerBinding.OwnerCoreId);
            hasher.Compress(ownerBinding.OwnerPodId);
            hasher.Compress(ownerBinding.OwnerDomainTag);
            hasher.Compress(ownerBinding.DeviceId);
            hasher.Compress(context.OwnerVirtualThreadId);
            hasher.Compress(context.OwnerContextId);
            hasher.Compress(context.OwnerCoreId);
            hasher.Compress(context.OwnerPodId);
            hasher.Compress(context.OwnerDomainTag);
            hasher.Compress(context.ActiveDomainCertificate);
            hasher.Compress(context.DeviceId);
            hasher.Compress(guardDecision.IsAllowed ? 1UL : 0UL);
            hasher.Compress((ulong)(byte)guardDecision.LegalityDecision.RejectKind);
            hasher.Compress((ulong)(byte)guardDecision.LegalityDecision.AuthoritySource);
            hasher.Compress(guardDecision.LegalityDecision.AttemptedReplayCertificateReuse ? 1UL : 0UL);

            return new DmaStreamComputeOwnerDomainEvidence(
                ownerBinding.OwnerVirtualThreadId,
                ownerBinding.OwnerContextId,
                ownerBinding.OwnerCoreId,
                ownerBinding.OwnerPodId,
                ownerBinding.OwnerDomainTag,
                ownerBinding.DeviceId,
                context.OwnerVirtualThreadId,
                context.OwnerContextId,
                context.OwnerCoreId,
                context.OwnerPodId,
                context.OwnerDomainTag,
                context.ActiveDomainCertificate,
                context.DeviceId,
                guardDecision.IsAllowed,
                guardDecision.LegalityDecision.RejectKind,
                guardDecision.LegalityDecision.AuthoritySource,
                guardDecision.LegalityDecision.AttemptedReplayCertificateReuse,
                NormalizeHash(hasher.Finalize()));
        }

        private static ulong NormalizeHash(ulong hash) =>
            hash == 0 ? 0xD5C0_0A11UL : hash;
    }

    public readonly record struct DmaStreamComputeTokenLifecycleEvidence
    {
        public DmaStreamComputeTokenLifecycleEvidence(
            DmaStreamComputeTokenEvidenceKind evidenceKind,
            ulong descriptorIdentityHash,
            ulong tokenId,
            DmaStreamComputeTokenState state,
            DmaStreamComputeTokenCancelReason cancelReason,
            DmaStreamComputeTokenFaultKind faultKind,
            int stagedWriteCount,
            bool hasCommitted,
            ulong evidenceHash)
        {
            EvidenceKind = evidenceKind;
            DescriptorIdentityHash = descriptorIdentityHash;
            TokenId = tokenId;
            State = state;
            CancelReason = cancelReason;
            FaultKind = faultKind;
            StagedWriteCount = stagedWriteCount;
            HasCommitted = hasCommitted;
            EvidenceHash = evidenceHash;
        }

        public DmaStreamComputeTokenEvidenceKind EvidenceKind { get; }

        public ulong DescriptorIdentityHash { get; }

        public ulong TokenId { get; }

        public DmaStreamComputeTokenState State { get; }

        public DmaStreamComputeTokenCancelReason CancelReason { get; }

        public DmaStreamComputeTokenFaultKind FaultKind { get; }

        public int StagedWriteCount { get; }

        public bool HasCommitted { get; }

        public ulong EvidenceHash { get; }

        public bool IsComplete =>
            DescriptorIdentityHash != 0 &&
            EvidenceHash != 0;

        public static DmaStreamComputeTokenLifecycleEvidence NotCreated(
            ulong descriptorIdentityHash)
        {
            return Create(
                DmaStreamComputeTokenEvidenceKind.NotCreated,
                descriptorIdentityHash,
                tokenId: 0,
                DmaStreamComputeTokenState.Admitted,
                DmaStreamComputeTokenCancelReason.None,
                DmaStreamComputeTokenFaultKind.None,
                stagedWriteCount: 0,
                hasCommitted: false);
        }

        public static DmaStreamComputeTokenLifecycleEvidence FromToken(
            DmaStreamComputeToken token)
        {
            ArgumentNullException.ThrowIfNull(token);
            return Create(
                DmaStreamComputeTokenEvidenceKind.LifecycleSnapshot,
                token.Descriptor.DescriptorIdentityHash,
                token.TokenId,
                token.State,
                token.CancelReason,
                token.LastFault?.FaultKind ?? DmaStreamComputeTokenFaultKind.None,
                token.StagedWriteCount,
                token.HasCommitted);
        }

        private static DmaStreamComputeTokenLifecycleEvidence Create(
            DmaStreamComputeTokenEvidenceKind evidenceKind,
            ulong descriptorIdentityHash,
            ulong tokenId,
            DmaStreamComputeTokenState state,
            DmaStreamComputeTokenCancelReason cancelReason,
            DmaStreamComputeTokenFaultKind faultKind,
            int stagedWriteCount,
            bool hasCommitted)
        {
            var hasher = new HardwareHash();
            hasher.Initialize();
            hasher.Compress((ulong)(byte)evidenceKind);
            hasher.Compress(descriptorIdentityHash);
            hasher.Compress(tokenId);
            hasher.Compress((ulong)(byte)state);
            hasher.Compress((ulong)(byte)cancelReason);
            hasher.Compress((ulong)(byte)faultKind);
            hasher.Compress((ulong)(uint)stagedWriteCount);
            hasher.Compress(hasCommitted ? 1UL : 0UL);
            return new DmaStreamComputeTokenLifecycleEvidence(
                evidenceKind,
                descriptorIdentityHash,
                tokenId,
                state,
                cancelReason,
                faultKind,
                stagedWriteCount,
                hasCommitted,
                NormalizeHash(hasher.Finalize()));
        }

        private static ulong NormalizeHash(ulong hash) =>
            hash == 0 ? 0xD5C0_70C0UL : hash;
    }

    public readonly record struct DmaStreamComputeLanePlacementEvidence
    {
        public DmaStreamComputeLanePlacementEvidence(
            SlotClass requiredSlotClass,
            SlotPinningKind pinningKind,
            int selectedLane,
            byte freeLaneMask,
            byte stableDonorMask,
            bool replayActive,
            ulong evidenceHash)
        {
            RequiredSlotClass = requiredSlotClass;
            PinningKind = pinningKind;
            SelectedLane = selectedLane;
            FreeLaneMask = freeLaneMask;
            StableDonorMask = stableDonorMask;
            ReplayActive = replayActive;
            EvidenceHash = evidenceHash;
        }

        public SlotClass RequiredSlotClass { get; }

        public SlotPinningKind PinningKind { get; }

        public int SelectedLane { get; }

        public byte FreeLaneMask { get; }

        public byte StableDonorMask { get; }

        public bool ReplayActive { get; }

        public ulong EvidenceHash { get; }

        public bool IsComplete =>
            RequiredSlotClass == SlotClass.DmaStreamClass &&
            PinningKind == SlotPinningKind.ClassFlexible &&
            (SelectedLane == -1 || SelectedLane == 6) &&
            EvidenceHash != 0;

        public static DmaStreamComputeLanePlacementEvidence DescriptorSurface()
        {
            return Create(
                SlotClass.DmaStreamClass,
                SlotPinningKind.ClassFlexible,
                selectedLane: -1,
                freeLaneMask: SlotClassLaneMap.GetLaneMask(SlotClass.DmaStreamClass),
                stableDonorMask: 0,
                replayActive: false);
        }

        public static DmaStreamComputeLanePlacementEvidence MaterializedLane6(
            int selectedLane,
            byte freeLaneMask,
            byte stableDonorMask,
            bool replayActive)
        {
            return Create(
                SlotClass.DmaStreamClass,
                SlotPinningKind.ClassFlexible,
                selectedLane,
                freeLaneMask,
                stableDonorMask,
                replayActive);
        }

        private static DmaStreamComputeLanePlacementEvidence Create(
            SlotClass requiredSlotClass,
            SlotPinningKind pinningKind,
            int selectedLane,
            byte freeLaneMask,
            byte stableDonorMask,
            bool replayActive)
        {
            var hasher = new HardwareHash();
            hasher.Initialize();
            hasher.Compress((ulong)(byte)requiredSlotClass);
            hasher.Compress((ulong)(byte)pinningKind);
            hasher.Compress((ulong)(uint)selectedLane);
            hasher.Compress(freeLaneMask);
            hasher.Compress(stableDonorMask);
            hasher.Compress(replayActive ? 1UL : 0UL);
            return new DmaStreamComputeLanePlacementEvidence(
                requiredSlotClass,
                pinningKind,
                selectedLane,
                freeLaneMask,
                stableDonorMask,
                replayActive,
                NormalizeHash(hasher.Finalize()));
        }

        private static ulong NormalizeHash(ulong hash) =>
            hash == 0 ? 0xD5C0_1A6EUL : hash;
    }

    public readonly record struct DmaStreamComputeReplayEvidence
    {
        public DmaStreamComputeReplayEvidence(
            DmaStreamComputeDescriptorReference descriptorReference,
            ushort abiVersion,
            ushort headerSize,
            uint totalSize,
            ulong descriptorIdentityHash,
            ulong certificateInputHash,
            DmaStreamComputeOperationKind operation,
            DmaStreamComputeElementType elementType,
            DmaStreamComputeShapeKind shape,
            DmaStreamComputeRangeEncoding rangeEncoding,
            DmaStreamComputePartialCompletionPolicy partialCompletionPolicy,
            DmaStreamComputeAliasPolicy aliasPolicy,
            DmaStreamComputeCarrierEvidence carrierEvidence,
            DmaStreamComputeFootprintEvidence footprintEvidence,
            DmaStreamComputeOwnerDomainEvidence ownerDomainEvidence,
            DmaStreamComputeTokenLifecycleEvidence tokenLifecycleEvidence,
            DmaStreamComputeLanePlacementEvidence lanePlacementEvidence,
            ulong envelopeHash)
        {
            DescriptorReference = descriptorReference;
            AbiVersion = abiVersion;
            HeaderSize = headerSize;
            TotalSize = totalSize;
            DescriptorIdentityHash = descriptorIdentityHash;
            CertificateInputHash = certificateInputHash;
            Operation = operation;
            ElementType = elementType;
            Shape = shape;
            RangeEncoding = rangeEncoding;
            PartialCompletionPolicy = partialCompletionPolicy;
            AliasPolicy = aliasPolicy;
            CarrierEvidence = carrierEvidence;
            FootprintEvidence = footprintEvidence;
            OwnerDomainEvidence = ownerDomainEvidence;
            TokenLifecycleEvidence = tokenLifecycleEvidence;
            LanePlacementEvidence = lanePlacementEvidence;
            EnvelopeHash = envelopeHash;
        }

        public DmaStreamComputeDescriptorReference DescriptorReference { get; init; }

        public ushort AbiVersion { get; init; }

        public ushort HeaderSize { get; init; }

        public uint TotalSize { get; init; }

        public ulong DescriptorIdentityHash { get; init; }

        public ulong CertificateInputHash { get; init; }

        public DmaStreamComputeOperationKind Operation { get; init; }

        public DmaStreamComputeElementType ElementType { get; init; }

        public DmaStreamComputeShapeKind Shape { get; init; }

        public DmaStreamComputeRangeEncoding RangeEncoding { get; init; }

        public DmaStreamComputePartialCompletionPolicy PartialCompletionPolicy { get; init; }

        public DmaStreamComputeAliasPolicy AliasPolicy { get; init; }

        public DmaStreamComputeCarrierEvidence CarrierEvidence { get; init; }

        public DmaStreamComputeFootprintEvidence FootprintEvidence { get; init; }

        public DmaStreamComputeOwnerDomainEvidence OwnerDomainEvidence { get; init; }

        public DmaStreamComputeTokenLifecycleEvidence TokenLifecycleEvidence { get; init; }

        public DmaStreamComputeLanePlacementEvidence LanePlacementEvidence { get; init; }

        public ulong EnvelopeHash { get; init; }

        public bool IsComplete =>
            DescriptorIdentityHash != 0 &&
            DescriptorReference.DescriptorSize != 0 &&
            DescriptorReference.DescriptorIdentityHash == DescriptorIdentityHash &&
            CarrierEvidence.IsReplayReusable &&
            FootprintEvidence.IsComplete &&
            OwnerDomainEvidence.IsComplete &&
            TokenLifecycleEvidence.IsComplete &&
            LanePlacementEvidence.IsComplete &&
            EnvelopeHash != 0;

        public static DmaStreamComputeReplayEvidence CreateForDescriptor(
            DmaStreamComputeDescriptor descriptor,
            DmaStreamComputeCarrierEvidence carrierEvidence = default,
            DmaStreamComputeTokenLifecycleEvidence tokenLifecycleEvidence = default,
            DmaStreamComputeLanePlacementEvidence lanePlacementEvidence = default)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            if (carrierEvidence.CarrierKind == DmaStreamComputeCarrierKind.None)
            {
                carrierEvidence =
                    DmaStreamComputeCarrierEvidence.CanonicalDecodedSideband(
                        descriptor.DescriptorReference);
            }

            if (tokenLifecycleEvidence.EvidenceHash == 0)
            {
                tokenLifecycleEvidence =
                    DmaStreamComputeTokenLifecycleEvidence.NotCreated(
                        descriptor.DescriptorIdentityHash);
            }

            if (lanePlacementEvidence.EvidenceHash == 0)
            {
                lanePlacementEvidence =
                    DmaStreamComputeLanePlacementEvidence.DescriptorSurface();
            }

            DmaStreamComputeFootprintEvidence footprintEvidence =
                DmaStreamComputeFootprintEvidence.FromDescriptor(descriptor);
            DmaStreamComputeOwnerDomainEvidence ownerDomainEvidence =
                DmaStreamComputeOwnerDomainEvidence.FromDecision(
                    descriptor.OwnerBinding,
                    descriptor.OwnerGuardDecision);

            ulong envelopeHash = ComputeEnvelopeHash(
                descriptor,
                carrierEvidence,
                footprintEvidence,
                ownerDomainEvidence,
                tokenLifecycleEvidence,
                lanePlacementEvidence);

            return new DmaStreamComputeReplayEvidence(
                descriptor.DescriptorReference,
                descriptor.AbiVersion,
                descriptor.HeaderSize,
                descriptor.TotalSize,
                descriptor.DescriptorIdentityHash,
                descriptor.CertificateInputHash,
                descriptor.Operation,
                descriptor.ElementType,
                descriptor.Shape,
                descriptor.RangeEncoding,
                descriptor.PartialCompletionPolicy,
                descriptor.AliasPolicy,
                carrierEvidence,
                footprintEvidence,
                ownerDomainEvidence,
                tokenLifecycleEvidence,
                lanePlacementEvidence,
                envelopeHash);
        }

        public static DmaStreamComputeReplayEvidence CreateForMicroOp(
            DmaStreamComputeMicroOp microOp,
            DmaStreamComputeTokenLifecycleEvidence tokenLifecycleEvidence = default,
            DmaStreamComputeLanePlacementEvidence lanePlacementEvidence = default)
        {
            ArgumentNullException.ThrowIfNull(microOp);
            return CreateForDescriptor(
                microOp.Descriptor,
                tokenLifecycleEvidence: tokenLifecycleEvidence,
                lanePlacementEvidence: lanePlacementEvidence);
        }

        private static ulong ComputeEnvelopeHash(
            DmaStreamComputeDescriptor descriptor,
            DmaStreamComputeCarrierEvidence carrierEvidence,
            DmaStreamComputeFootprintEvidence footprintEvidence,
            DmaStreamComputeOwnerDomainEvidence ownerDomainEvidence,
            DmaStreamComputeTokenLifecycleEvidence tokenLifecycleEvidence,
            DmaStreamComputeLanePlacementEvidence lanePlacementEvidence)
        {
            var hasher = new HardwareHash();
            hasher.Initialize();
            hasher.Compress(descriptor.DescriptorReference.DescriptorAddress);
            hasher.Compress(descriptor.DescriptorReference.DescriptorSize);
            hasher.Compress(descriptor.DescriptorReference.DescriptorIdentityHash);
            hasher.Compress(descriptor.AbiVersion);
            hasher.Compress(descriptor.HeaderSize);
            hasher.Compress(descriptor.TotalSize);
            hasher.Compress(descriptor.DescriptorIdentityHash);
            hasher.Compress(descriptor.CertificateInputHash);
            hasher.Compress((ulong)(ushort)descriptor.Operation);
            hasher.Compress((ulong)(ushort)descriptor.ElementType);
            hasher.Compress((ulong)(ushort)descriptor.Shape);
            hasher.Compress((ulong)(ushort)descriptor.RangeEncoding);
            hasher.Compress((ulong)(ushort)descriptor.PartialCompletionPolicy);
            hasher.Compress((ulong)(ushort)descriptor.AliasPolicy);
            hasher.Compress(carrierEvidence.CarrierIdentityHash);
            hasher.Compress(footprintEvidence.NormalizedReadFootprintHash);
            hasher.Compress(footprintEvidence.NormalizedWriteFootprintHash);
            hasher.Compress(footprintEvidence.NormalizedFootprintHash);
            hasher.Compress(ownerDomainEvidence.EvidenceHash);
            hasher.Compress(tokenLifecycleEvidence.EvidenceHash);
            hasher.Compress(lanePlacementEvidence.EvidenceHash);
            ulong hash = hasher.Finalize();
            return hash == 0 ? 0xD5C0_E8E0UL : hash;
        }
    }

    public readonly record struct DmaStreamComputeReplayEvidenceComparison(
        bool CanReuse,
        ReplayPhaseInvalidationReason InvalidationReason,
        string MismatchField)
    {
        public static DmaStreamComputeReplayEvidenceComparison ReuseHit { get; } =
            new(true, ReplayPhaseInvalidationReason.None, string.Empty);

        public static DmaStreamComputeReplayEvidenceComparison Miss(
            ReplayPhaseInvalidationReason reason,
            string mismatchField) =>
            new(false, reason, mismatchField);
    }

    public static class DmaStreamComputeReplayEvidenceComparer
    {
        public static bool CanReuse(
            DmaStreamComputeReplayEvidence expected,
            DmaStreamComputeReplayEvidence live,
            out DmaStreamComputeReplayEvidenceComparison comparison,
            DmaStreamComputeTelemetryCounters? telemetry = null)
        {
            comparison = Compare(expected, live);
            telemetry?.RecordReplayEvidenceComparison(comparison);
            return comparison.CanReuse;
        }

        public static DmaStreamComputeReplayEvidenceComparison Compare(
            DmaStreamComputeReplayEvidence expected,
            DmaStreamComputeReplayEvidence live,
            DmaStreamComputeTelemetryCounters? telemetry = null)
        {
            DmaStreamComputeReplayEvidenceComparison comparison;

            if (HasDescriptorPayloadLoss(expected) || HasDescriptorPayloadLoss(live))
            {
                comparison = DmaStreamComputeReplayEvidenceComparison.Miss(
                    ReplayPhaseInvalidationReason.DmaStreamComputeDescriptorPayloadLost,
                    "CarrierPayload");
                telemetry?.RecordReplayEvidenceComparison(comparison);
                return comparison;
            }

            if (HasUnsupportedCarrier(expected) || HasUnsupportedCarrier(live))
            {
                comparison = DmaStreamComputeReplayEvidenceComparison.Miss(
                    ReplayPhaseInvalidationReason.DmaStreamComputeCarrierMismatch,
                    "Carrier");
                telemetry?.RecordReplayEvidenceComparison(comparison);
                return comparison;
            }

            if (!expected.IsComplete || !live.IsComplete)
            {
                comparison = DmaStreamComputeReplayEvidenceComparison.Miss(
                    ReplayPhaseInvalidationReason.DmaStreamComputeIncompleteEvidence,
                    "IncompleteEvidence");
                telemetry?.RecordReplayEvidenceComparison(comparison);
                return comparison;
            }

            if (!DescriptorShapeMatches(expected, live, out string descriptorField))
            {
                comparison = DmaStreamComputeReplayEvidenceComparison.Miss(
                    ReplayPhaseInvalidationReason.DmaStreamComputeDescriptorMismatch,
                    descriptorField);
                telemetry?.RecordReplayEvidenceComparison(comparison);
                return comparison;
            }

            if (!expected.CarrierEvidence.Equals(live.CarrierEvidence))
            {
                comparison = DmaStreamComputeReplayEvidenceComparison.Miss(
                    ReplayPhaseInvalidationReason.DmaStreamComputeCarrierMismatch,
                    "Carrier");
                telemetry?.RecordReplayEvidenceComparison(comparison);
                return comparison;
            }

            if (expected.CertificateInputHash != live.CertificateInputHash)
            {
                comparison = DmaStreamComputeReplayEvidenceComparison.Miss(
                    ReplayPhaseInvalidationReason.DmaStreamComputeCertificateInputMismatch,
                    "CertificateInputHash");
                telemetry?.RecordReplayEvidenceComparison(comparison);
                return comparison;
            }

            if (!expected.FootprintEvidence.Equals(live.FootprintEvidence))
            {
                comparison = DmaStreamComputeReplayEvidenceComparison.Miss(
                    ReplayPhaseInvalidationReason.DmaStreamComputeFootprintMismatch,
                    "Footprint");
                telemetry?.RecordReplayEvidenceComparison(comparison);
                return comparison;
            }

            if (!expected.OwnerDomainEvidence.Equals(live.OwnerDomainEvidence))
            {
                comparison = DmaStreamComputeReplayEvidenceComparison.Miss(
                    ReplayPhaseInvalidationReason.DmaStreamComputeOwnerDomainMismatch,
                    "OwnerDomain");
                telemetry?.RecordReplayEvidenceComparison(comparison);
                return comparison;
            }

            if (!expected.TokenLifecycleEvidence.Equals(live.TokenLifecycleEvidence))
            {
                comparison = DmaStreamComputeReplayEvidenceComparison.Miss(
                    ReplayPhaseInvalidationReason.DmaStreamComputeTokenEvidenceMismatch,
                    "TokenLifecycle");
                telemetry?.RecordReplayEvidenceComparison(comparison);
                return comparison;
            }

            if (!expected.LanePlacementEvidence.Equals(live.LanePlacementEvidence))
            {
                comparison = DmaStreamComputeReplayEvidenceComparison.Miss(
                    ReplayPhaseInvalidationReason.DmaStreamComputeLanePlacementMismatch,
                    "LanePlacement");
                telemetry?.RecordReplayEvidenceComparison(comparison);
                return comparison;
            }

            if (expected.EnvelopeHash != live.EnvelopeHash)
            {
                comparison = DmaStreamComputeReplayEvidenceComparison.Miss(
                    ReplayPhaseInvalidationReason.DmaStreamComputeDescriptorMismatch,
                    "EnvelopeHash");
                telemetry?.RecordReplayEvidenceComparison(comparison);
                return comparison;
            }

            comparison = DmaStreamComputeReplayEvidenceComparison.ReuseHit;
            telemetry?.RecordReplayEvidenceComparison(comparison);
            return comparison;
        }

        private static bool HasDescriptorPayloadLoss(
            DmaStreamComputeReplayEvidence evidence)
        {
            return evidence.CarrierEvidence.CarrierKind ==
                       DmaStreamComputeCarrierKind.MissingDescriptorPayload ||
                   (evidence.CarrierEvidence.CarrierKind ==
                        DmaStreamComputeCarrierKind.CanonicalDecodedSideband &&
                    (evidence.CarrierEvidence.DescriptorReference.DescriptorSize == 0 ||
                     evidence.CarrierEvidence.DescriptorReference.DescriptorIdentityHash == 0));
        }

        private static bool HasUnsupportedCarrier(
            DmaStreamComputeReplayEvidence evidence)
        {
            return evidence.CarrierEvidence.CarrierKind is
                DmaStreamComputeCarrierKind.CustomRegistryCarrier;
        }

        private static bool DescriptorShapeMatches(
            DmaStreamComputeReplayEvidence expected,
            DmaStreamComputeReplayEvidence live,
            out string mismatchField)
        {
            mismatchField = string.Empty;

            if (!expected.DescriptorReference.Equals(live.DescriptorReference))
            {
                mismatchField = "DescriptorReference";
                return false;
            }

            if (expected.DescriptorIdentityHash != live.DescriptorIdentityHash)
            {
                mismatchField = "DescriptorIdentity";
                return false;
            }

            if (expected.AbiVersion != live.AbiVersion)
            {
                mismatchField = "AbiVersion";
                return false;
            }

            if (expected.HeaderSize != live.HeaderSize ||
                expected.TotalSize != live.TotalSize)
            {
                mismatchField = "DescriptorSize";
                return false;
            }

            if (expected.Operation != live.Operation)
            {
                mismatchField = "Operation";
                return false;
            }

            if (expected.ElementType != live.ElementType)
            {
                mismatchField = "ElementType";
                return false;
            }

            if (expected.Shape != live.Shape)
            {
                mismatchField = "Shape";
                return false;
            }

            if (expected.RangeEncoding != live.RangeEncoding)
            {
                mismatchField = "RangeEncoding";
                return false;
            }

            if (expected.PartialCompletionPolicy != live.PartialCompletionPolicy)
            {
                mismatchField = "PartialCompletionPolicy";
                return false;
            }

            if (expected.AliasPolicy != live.AliasPolicy)
            {
                mismatchField = "AliasPolicy";
                return false;
            }

            return true;
        }
    }
}
