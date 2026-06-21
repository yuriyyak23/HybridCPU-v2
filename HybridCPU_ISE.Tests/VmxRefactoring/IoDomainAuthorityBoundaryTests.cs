using HybridCPU_ISE.CloseToRTL.Memory.MMU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests;

public sealed class IoDomainAuthorityBoundaryTests
{
    [Fact]
    public void IotlbInvalidation_DeniesMissingAuthorityOrBlockBeforeHostBackend()
    {
        var service = new IotlbInvalidationService();
        var backend = new CountingIoVirtualizationBackend();
        var block = new IoVirtualizationBlock(backend);

        IotlbInvalidationResult missingIo = service.Invalidate(
            new DomainRuntimeContext(null, null, io: null, CapabilityDescriptorSet.Empty),
            IotlbInvalidationScope.All);

        Assert.False(missingIo.IsAllowed);
        Assert.Equal(IotlbInvalidationDecision.MissingIoDomain, missingIo.Decision);

        var noAuthority = new IoDomainDescriptor(
            block,
            dmaWindow: null,
            ownsDmaAuthority: false,
            ownsIommuAuthority: false,
            compatibilityProjectionEnabled: true);

        IotlbInvalidationResult missingAuthority = service.Invalidate(
            new DomainRuntimeContext(null, null, noAuthority, CapabilityDescriptorSet.Empty),
            IotlbInvalidationScope.All);

        Assert.False(missingAuthority.IsAllowed);
        Assert.Equal(IotlbInvalidationDecision.RuntimeAuthorityMissing, missingAuthority.Decision);
        Assert.Equal(0, backend.TotalCalls);

        var missingBlock = new IoDomainDescriptor(
            virtualizationBlock: null,
            dmaWindow: null,
            ownsDmaAuthority: true,
            ownsIommuAuthority: true,
            compatibilityProjectionEnabled: true);

        IotlbInvalidationResult missingVirtualizationBlock = service.Invalidate(
            new DomainRuntimeContext(null, null, missingBlock, CapabilityDescriptorSet.Empty),
            IotlbInvalidationScope.All);

        Assert.False(missingVirtualizationBlock.IsAllowed);
        Assert.Equal(
            IotlbInvalidationDecision.MissingVirtualizationBlock,
            missingVirtualizationBlock.Decision);
        Assert.Equal(0, backend.TotalCalls);
    }

    [Fact]
    public void DmaAuthority_DeniesMissingDescriptorsBindingAndFenceFailClosed()
    {
        var service = new DmaAuthorityService();
        IommuDomainBinding binding = IommuDomainBinding.Create(
            ioDomainTag: 1,
            domainId: 2,
            domainTag: 0x100,
            deviceId: 3,
            permissions: IOMMUAccessPermissions.ReadWrite);
        var window = new DmaWindowDescriptor(
            DmaWindowAuthority.IoDomain,
            baseAddress: 0x1000,
            length: 0x1000,
            permissions: IOMMUAccessPermissions.ReadWrite,
            requiresFence: false,
            allowsCompatibilityProjection: true);

        DmaAuthorityResult missingIo = service.ValidateAccess(
            new DomainRuntimeContext(null, null, io: null, CapabilityDescriptorSet.Empty),
            binding,
            ioVirtualAddress: 0x1000,
            accessSize: 8,
            IOMMUAccessPermissions.Read,
            fenceSatisfied: true);

        Assert.False(missingIo.IsAllowed);
        Assert.Equal(DmaAuthorityDecision.MissingIoDomain, missingIo.Decision);

        var noAuthority = new IoDomainDescriptor(
            new IoVirtualizationBlock(new CountingIoVirtualizationBackend()),
            window,
            ownsDmaAuthority: false,
            ownsIommuAuthority: true,
            compatibilityProjectionEnabled: true);

        DmaAuthorityResult missingAuthority = service.ValidateAccess(
            new DomainRuntimeContext(null, null, noAuthority, CapabilityDescriptorSet.Empty),
            binding,
            ioVirtualAddress: 0x1000,
            accessSize: 8,
            IOMMUAccessPermissions.Read,
            fenceSatisfied: true);

        Assert.False(missingAuthority.IsAllowed);
        Assert.Equal(DmaAuthorityDecision.RuntimeAuthorityMissing, missingAuthority.Decision);

        var missingWindow = new IoDomainDescriptor(
            new IoVirtualizationBlock(new CountingIoVirtualizationBackend()),
            dmaWindow: null,
            ownsDmaAuthority: true,
            ownsIommuAuthority: true,
            compatibilityProjectionEnabled: true);

        DmaAuthorityResult missingDmaWindow = service.ValidateAccess(
            new DomainRuntimeContext(null, null, missingWindow, CapabilityDescriptorSet.Empty),
            binding,
            ioVirtualAddress: 0x1000,
            accessSize: 8,
            IOMMUAccessPermissions.Read,
            fenceSatisfied: true);

        Assert.False(missingDmaWindow.IsAllowed);
        Assert.Equal(DmaAuthorityDecision.MissingDmaWindow, missingDmaWindow.Decision);

        var validIo = new IoDomainDescriptor(
            new IoVirtualizationBlock(new CountingIoVirtualizationBackend()),
            window,
            ownsDmaAuthority: true,
            ownsIommuAuthority: true,
            compatibilityProjectionEnabled: true);

        DmaAuthorityResult invalidBinding = service.ValidateAccess(
            new DomainRuntimeContext(null, null, validIo, CapabilityDescriptorSet.Empty),
            default,
            ioVirtualAddress: 0x1000,
            accessSize: 8,
            IOMMUAccessPermissions.Read,
            fenceSatisfied: true);

        Assert.False(invalidBinding.IsAllowed);
        Assert.Equal(DmaAuthorityDecision.MissingIommuBinding, invalidBinding.Decision);

        var fencedWindow = new DmaWindowDescriptor(
            DmaWindowAuthority.IoDomain,
            baseAddress: 0x1000,
            length: 0x1000,
            permissions: IOMMUAccessPermissions.ReadWrite,
            requiresFence: true,
            allowsCompatibilityProjection: true);
        var fencedIo = new IoDomainDescriptor(
            new IoVirtualizationBlock(new CountingIoVirtualizationBackend()),
            fencedWindow,
            ownsDmaAuthority: true,
            ownsIommuAuthority: true,
            compatibilityProjectionEnabled: true);

        DmaAuthorityResult fenceRequired = service.ValidateAccess(
            new DomainRuntimeContext(null, null, fencedIo, CapabilityDescriptorSet.Empty),
            binding,
            ioVirtualAddress: 0x1000,
            accessSize: 8,
            IOMMUAccessPermissions.Write,
            fenceSatisfied: false);

        Assert.False(fenceRequired.IsAllowed);
        Assert.Equal(DmaAuthorityDecision.FenceRequired, fenceRequired.Decision);
        Assert.Equal(DmaFaultDisposition.Replay, fenceRequired.Fault.Disposition);
    }

    private sealed class CountingIoVirtualizationBackend : IIoVirtualizationBackend
    {
        public int TotalCalls { get; private set; }

        public ulong LastIotlbInvalidationEpoch
        {
            get
            {
                TotalCalls++;
                return 0;
            }
        }

        public IommuDomainBinding BindDomain(IommuDomainBinding binding)
        {
            TotalCalls++;
            return binding;
        }

        public void UnbindDomain(ushort ioDomainTag, uint domainId, uint deviceId)
        {
            TotalCalls++;
        }

        public int InvalidateIotlbAll()
        {
            TotalCalls++;
            return 0;
        }

        public int InvalidateIotlbByIoDomainTag(ushort ioDomainTag)
        {
            TotalCalls++;
            return 0;
        }

        public int InvalidateIotlbByIoDomain(ushort ioDomainTag, uint domainId)
        {
            TotalCalls++;
            return 0;
        }

        public int InvalidateIotlbByDevice(ushort ioDomainTag, uint domainId, uint deviceId)
        {
            TotalCalls++;
            return 0;
        }
    }
}
