// Description: Host I/O virtualization backend used after I/O-domain descriptor admission has selected a generic backend boundary.
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.CloseToRTL.Memory.MMU;

public sealed class IoVirtualizationHostBackend : IIoVirtualizationBackend
{
    public static IoVirtualizationHostBackend Instance { get; } = new();

    private IoVirtualizationHostBackend()
    {
    }

    public ulong LastIotlbInvalidationEpoch => IOMMU.IotlbInvalidationEpoch;

    public IommuDomainBinding BindDomain(IommuDomainBinding binding) =>
        IOMMU.BindIoDomain(binding);

    public void UnbindDomain(ushort ioDomainTag, uint domainId, uint deviceId) =>
        IOMMU.UnbindIoDomain(ioDomainTag, domainId, deviceId);

    public int InvalidateIotlbAll() =>
        IOMMU.InvalidateIotlbAll();

    public int InvalidateIotlbByIoDomainTag(ushort ioDomainTag) =>
        IOMMU.InvalidateIotlbByIoDomainTag(ioDomainTag);

    public int InvalidateIotlbByIoDomain(ushort ioDomainTag, uint domainId) =>
        IOMMU.InvalidateIotlbByIoDomain(ioDomainTag, domainId);

    public int InvalidateIotlbByDevice(ushort ioDomainTag, uint domainId, uint deviceId) =>
        IOMMU.InvalidateIotlbByDevice(ioDomainTag, domainId, deviceId);
}
