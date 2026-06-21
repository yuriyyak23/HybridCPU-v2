using HybridCPU_ISE.CloseToRTL.Memory.MMU;
using System;

namespace YAKSys_Hybrid_CPU.Memory
{
    public enum DmaFaultKind : byte
    {
        None = 0,
        MissingDomainBinding = 1,
        DeviceDomainMismatch = 2,
        DescriptorFault = 3,
        PermissionFault = 4,
        IommuTranslationFault = 5,
        NonCoherentFenceRequired = 6,
        QueueOwnershipFault = 7,
        Lane6QueuePressure = 8,
        ValidationEvidenceMissing = 9,
        ValidationEvidenceStale = 10,
        EpochExhausted = 11,
        ResourceIdentityExhausted = 12,
    }

    public enum DmaFaultDisposition : byte
    {
        None = 0,
        Abort = 1,
        Replay = 2,
    }

    public readonly record struct IommuDomainBinding(
        ushort IoDomainTag,
        uint DomainId,
        ulong DomainTag,
        uint DeviceId,
        IOMMUAccessPermissions Permissions,
        ulong DomainEpoch,
        bool NonCoherent,
        bool RequiresFence)
    {
        public bool IsValid =>
            IoDomainTag != 0 &&
            DomainId != 0 &&
            DomainTag != 0 &&
            DeviceId != 0 &&
            Permissions != IOMMUAccessPermissions.None &&
            DomainEpoch != 0;

        public bool Allows(IOMMUAccessPermissions requested) =>
            requested != IOMMUAccessPermissions.None &&
            (Permissions & requested) == requested;

        public IommuDomainBinding WithEpoch(ulong epoch) =>
            this with { DomainEpoch = epoch == 0 ? 1 : epoch };

        public static IommuDomainBinding Create(
            ushort ioDomainTag,
            uint domainId,
            ulong domainTag,
            uint deviceId,
            IOMMUAccessPermissions permissions,
            ulong domainEpoch = 1,
            bool nonCoherent = false,
            bool requiresFence = false) =>
            new(
                ioDomainTag,
                domainId,
                domainTag,
                deviceId,
                permissions,
                domainEpoch == 0 ? 1 : domainEpoch,
                nonCoherent,
                requiresFence);
    }

    public readonly record struct DmaFault(
        DmaFaultKind Kind,
        DmaFaultDisposition Disposition,
        ulong FaultAddress,
        bool IsWrite,
        string Message)
    {
        public bool IsFaulted => Kind != DmaFaultKind.None;

        public static DmaFault None { get; } =
            new(DmaFaultKind.None, DmaFaultDisposition.None, 0, false, string.Empty);

        public static DmaFault Abort(
            DmaFaultKind kind,
            ulong faultAddress,
            bool isWrite,
            string message) =>
            new(kind, DmaFaultDisposition.Abort, faultAddress, isWrite, message);

        public static DmaFault Replay(
            DmaFaultKind kind,
            ulong faultAddress,
            bool isWrite,
            string message) =>
            new(kind, DmaFaultDisposition.Replay, faultAddress, isWrite, message);
    }

    public readonly record struct DmaTranslationResult(
        bool Succeeded,
        bool IotlbHit,
        ulong IoVirtualAddress,
        ulong HostPhysicalAddress,
        ulong AccessSize,
        IOMMUAccessPermissions Permissions,
        IotlbTag Tag,
        DmaFault Fault)
    {
        public static DmaTranslationResult Success(
            bool iotlbHit,
            ulong ioVirtualAddress,
            ulong hostPhysicalAddress,
            ulong accessSize,
            IOMMUAccessPermissions permissions,
            IotlbTag tag) =>
            new(
                true,
                iotlbHit,
                ioVirtualAddress,
                hostPhysicalAddress,
                accessSize,
                permissions,
                tag,
                DmaFault.None);

        public static DmaTranslationResult Fail(
            ulong ioVirtualAddress,
            ulong accessSize,
            IOMMUAccessPermissions permissions,
            DmaFault fault) =>
            new(
                false,
                false,
                ioVirtualAddress,
                0,
                accessSize,
                permissions,
                default,
                fault);
    }
}
