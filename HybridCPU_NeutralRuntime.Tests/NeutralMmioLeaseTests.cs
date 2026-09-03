using System.Reflection;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_NeutralRuntime.Tests;

public sealed class NeutralMmioLeaseTests
{
    [Fact]
    public void ExactBoundedMmioLeaseBlocksDeviceCloseUntilExactClosure()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = runtime.Bind(NeutralDomainProfile.OrdinaryService).Lease;
        var device = runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/uart0"),
            NeutralDeviceRights.Read | NeutralDeviceRights.Write | NeutralDeviceRights.Configure).Lease;
        var region = new NeutralMmioRegionIdentity("uart0/registers", 4096);
        var range = new NeutralMmioRange(256, 128);

        var mapped = runtime.MapMmio(
            device,
            region,
            range,
            NeutralMmioAccess.Read | NeutralMmioAccess.Write);

        Assert.True(mapped.IsMapped, mapped.Reason);
        Assert.Equal(device, mapped.Lease.DeviceLease);
        Assert.Equal(region, mapped.Lease.Region);
        Assert.Equal(range, mapped.Lease.Range);
        Assert.Equal(1, runtime.ActiveMmioLeaseCount);

        var earlyDeviceClose = runtime.CloseDevice(device);
        Assert.Equal(NeutralDeviceCloseDecision.ActiveDependents, earlyDeviceClose.Decision);
        Assert.Equal(1, runtime.ActiveDeviceLeaseCount);

        Assert.True(runtime.CloseMmio(mapped.Lease).IsClosed);
        Assert.Equal(0, runtime.ActiveMmioLeaseCount);
        Assert.True(runtime.CloseDevice(device).IsClosed);
    }

    [Fact]
    public void RangeAndAccessAreValidatedBeforeMaterialization()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = runtime.Bind(NeutralDomainProfile.OrdinaryService).Lease;
        var device = runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/net0"),
            NeutralDeviceRights.Read | NeutralDeviceRights.Write | NeutralDeviceRights.Configure).Lease;
        var region = new NeutralMmioRegionIdentity("net0/control", 256);

        Assert.Equal(
            NeutralMmioMapDecision.InvalidRange,
            runtime.MapMmio(device, region, new NeutralMmioRange(-1, 1), NeutralMmioAccess.Read).Decision);
        Assert.Equal(
            NeutralMmioMapDecision.InvalidRange,
            runtime.MapMmio(device, region, new NeutralMmioRange(240, 32), NeutralMmioAccess.Read).Decision);
        Assert.Equal(
            NeutralMmioMapDecision.InvalidAccess,
            runtime.MapMmio(device, region, new NeutralMmioRange(0, 16), NeutralMmioAccess.None).Decision);
        Assert.Equal(0, runtime.ActiveMmioLeaseCount);
    }

    [Fact]
    public void DeviceRightsMustCoverConfigureAndRequestedAccess()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = runtime.Bind(NeutralDomainProfile.OrdinaryService).Lease;
        var readOnly = runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/readonly0"),
            NeutralDeviceRights.Read).Lease;
        var region = new NeutralMmioRegionIdentity("readonly0/status", 64);

        var result = runtime.MapMmio(
            readOnly,
            region,
            new NeutralMmioRange(0, 8),
            NeutralMmioAccess.Read);

        Assert.Equal(NeutralMmioMapDecision.InsufficientDeviceRights, result.Decision);
        Assert.Equal(0, runtime.ActiveMmioLeaseCount);
    }

    [Fact]
    public void DuplicateSemanticRegionIsDeniedWithinOneDeviceLifetime()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = runtime.Bind(NeutralDomainProfile.OrdinaryService).Lease;
        var device = runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/storage0"),
            NeutralDeviceRights.Read | NeutralDeviceRights.Configure).Lease;
        var region = new NeutralMmioRegionIdentity("storage0/admin", 1024);

        var first = runtime.MapMmio(
            device, region, new NeutralMmioRange(0, 64), NeutralMmioAccess.Read);
        var second = runtime.MapMmio(
            device, region, new NeutralMmioRange(64, 64), NeutralMmioAccess.Read);

        Assert.True(first.IsMapped, first.Reason);
        Assert.Equal(NeutralMmioMapDecision.AlreadyMapped, second.Decision);
        Assert.Equal(1, runtime.ActiveMmioLeaseCount);
    }

    [Fact]
    public void StaleOrForgedMmioIdentityCannotCloseLiveAuthority()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = runtime.Bind(NeutralDomainProfile.OrdinaryService).Lease;
        var device = runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/gpu0"),
            NeutralDeviceRights.Read | NeutralDeviceRights.Configure).Lease;
        var mapped = runtime.MapMmio(
            device,
            new NeutralMmioRegionIdentity("gpu0/status", 512),
            new NeutralMmioRange(32, 32),
            NeutralMmioAccess.Read).Lease;

        var stale = mapped with
        {
            Epoch = new NeutralMmioLeaseEpoch(mapped.Epoch.Value + 1),
        };
        var forged = mapped with
        {
            Range = new NeutralMmioRange(64, 32),
        };

        Assert.Equal(NeutralMmioCloseDecision.Stale, runtime.CloseMmio(stale).Decision);
        Assert.Equal(NeutralMmioCloseDecision.Faulted, runtime.CloseMmio(forged).Decision);
        Assert.Equal(1, runtime.ActiveMmioLeaseCount);
        Assert.True(runtime.CloseMmio(mapped).IsClosed);
    }

    [Fact]
    public void PublicMmioSurfaceContainsNoHardwareAuthorityIdentity()
    {
        var surface = new[]
        {
            typeof(NeutralMmioAccess),
            typeof(NeutralMmioRegionIdentity),
            typeof(NeutralMmioRange),
            typeof(NeutralMmioLeaseHandle),
            typeof(NeutralMmioLeaseEpoch),
            typeof(NeutralMmioLease),
        };
        var forbidden = new[]
        {
            "Physical",
            "PageTable",
            "Pte",
            "BarNumber",
            "InterruptVector",
            "Iommu",
            "DmaWindow",
            "Vmcs",
            "Vmx",
            "Lane",
            "Opcode",
        };

        foreach (var type in surface)
        foreach (var member in type.GetMembers(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            var signature = member.ToString() ?? member.Name;
            foreach (var term in forbidden)
                Assert.DoesNotContain(term, signature, StringComparison.OrdinalIgnoreCase);
        }
    }
}
