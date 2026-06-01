using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.CloseToRTL.Memory.MMU
{
    public readonly record struct IotlbTag(
        ushort IoDomainTag,
        uint DomainId,
        uint DeviceId,
        ulong IoVirtualPageNumber,
        IOMMUAccessPermissions Permissions,
        ulong DomainEpoch,
        ulong MappingEpoch)
    {
        public static IotlbTag Create(
            IommuDomainBinding binding,
            ulong ioVirtualAddress,
            IOMMUAccessPermissions permissions,
            ulong mappingEpoch) =>
            new(
                binding.IoDomainTag,
                binding.DomainId,
                binding.DeviceId,
                ioVirtualAddress >> 12,
                permissions,
                binding.DomainEpoch,
                mappingEpoch == 0 ? 1 : mappingEpoch);

        public bool Matches(
            IommuDomainBinding binding,
            ulong ioVirtualAddress,
            IOMMUAccessPermissions requestedPermissions,
            ulong mappingEpoch) =>
            IoDomainTag == binding.IoDomainTag &&
            DomainId == binding.DomainId &&
            DeviceId == binding.DeviceId &&
            IoVirtualPageNumber == (ioVirtualAddress >> 12) &&
            DomainEpoch == binding.DomainEpoch &&
            MappingEpoch == (mappingEpoch == 0 ? 1 : mappingEpoch) &&
            (Permissions & requestedPermissions) == requestedPermissions;
    }
}
