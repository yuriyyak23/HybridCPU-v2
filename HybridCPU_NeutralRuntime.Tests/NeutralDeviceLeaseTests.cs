using System.Reflection;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_NeutralRuntime.Tests;

public sealed class NeutralDeviceLeaseTests
{
    [Fact]
    public void ExactLiveDomainCanMaterializeAndCloseSemanticDeviceLease()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = AssertBound(runtime.Bind(NeutralDomainProfile.OrdinaryService));
        var device = new NeutralDeviceIdentity("device/net0");
        var rights = NeutralDeviceRights.Read | NeutralDeviceRights.Configure;

        var bound = runtime.BindDevice(domain, device, rights);

        Assert.True(bound.IsBound, bound.Reason);
        Assert.Equal(domain, bound.Lease.DomainLease);
        Assert.Equal(device, bound.Lease.Device);
        Assert.Equal(rights, bound.Lease.Rights);
        Assert.NotEqual(0UL, bound.Lease.Handle.Value);
        Assert.NotEqual(0UL, bound.Lease.Epoch.Value);
        Assert.Equal(1, runtime.ActiveDeviceLeaseCount);

        var closed = runtime.CloseDevice(bound.Lease);
        Assert.True(closed.IsClosed, closed.Reason);
        Assert.Equal(bound.Lease, closed.Lease);
        Assert.Equal(0, runtime.ActiveDeviceLeaseCount);

        var duplicateClose = runtime.CloseDevice(bound.Lease);
        Assert.Equal(NeutralDeviceCloseDecision.Revoked, duplicateClose.Decision);
    }

    [Fact]
    public void DeviceBindRejectsInvalidRightsAndIdentityWithoutMaterialization()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = AssertBound(runtime.Bind(NeutralDomainProfile.OrdinaryService));

        var emptyDevice = runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity(""),
            NeutralDeviceRights.Read);
        Assert.Equal(NeutralDeviceBindDecision.InvalidDevice, emptyDevice.Decision);

        var noRights = runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/storage0"),
            NeutralDeviceRights.None);
        Assert.Equal(NeutralDeviceBindDecision.InvalidRights, noRights.Decision);

        var undefinedRights = runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/storage0"),
            (NeutralDeviceRights)0x80);
        Assert.Equal(NeutralDeviceBindDecision.InvalidRights, undefinedRights.Decision);
        Assert.Equal(0, runtime.ActiveDeviceLeaseCount);
    }

    [Fact]
    public void StaleOrRevokedDomainCannotMaterializeDeviceAuthority()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = AssertBound(runtime.Bind(NeutralDomainProfile.OrdinaryService));
        var stale = domain with
        {
            Epoch = new NeutralDomainBindingEpoch(domain.Epoch.Value + 1),
        };

        var staleBind = runtime.BindDevice(
            stale,
            new NeutralDeviceIdentity("device/net0"),
            NeutralDeviceRights.Read);
        Assert.Equal(NeutralDeviceBindDecision.Stale, staleBind.Decision);

        Assert.True(runtime.Close(domain).IsClosed);
        var revokedBind = runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/net0"),
            NeutralDeviceRights.Read);
        Assert.Equal(NeutralDeviceBindDecision.Revoked, revokedBind.Decision);
        Assert.Equal(0, runtime.ActiveDeviceLeaseCount);
    }

    [Fact]
    public void SameDeviceCannotBeReboundInsideSameLiveDomainLifetime()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = AssertBound(runtime.Bind(NeutralDomainProfile.OrdinaryService));
        var device = new NeutralDeviceIdentity("device/gpu0");

        var first = runtime.BindDevice(domain, device, NeutralDeviceRights.Configure);
        Assert.True(first.IsBound, first.Reason);

        var duplicate = runtime.BindDevice(
            domain,
            device,
            NeutralDeviceRights.Read | NeutralDeviceRights.Write);
        Assert.Equal(NeutralDeviceBindDecision.AlreadyBound, duplicate.Decision);
        Assert.Equal(1, runtime.ActiveDeviceLeaseCount);
    }

    [Fact]
    public void StaleOrForgedLeaseCannotCloseLiveDeviceAuthority()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = AssertBound(runtime.Bind(NeutralDomainProfile.OrdinaryService));
        var live = runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/nvme0"),
            NeutralDeviceRights.Read | NeutralDeviceRights.Write).Lease;

        var stale = live with
        {
            Epoch = new NeutralDeviceLeaseEpoch(live.Epoch.Value + 1),
        };
        Assert.Equal(NeutralDeviceCloseDecision.Stale, runtime.CloseDevice(stale).Decision);
        Assert.Equal(1, runtime.ActiveDeviceLeaseCount);

        var forgedDevice = live with
        {
            Device = new NeutralDeviceIdentity("device/nvme1"),
        };
        Assert.Equal(NeutralDeviceCloseDecision.Faulted, runtime.CloseDevice(forgedDevice).Decision);
        Assert.Equal(1, runtime.ActiveDeviceLeaseCount);

        var forgedRights = live with
        {
            Rights = NeutralDeviceRights.Configure,
        };
        Assert.Equal(NeutralDeviceCloseDecision.Faulted, runtime.CloseDevice(forgedRights).Decision);
        Assert.Equal(1, runtime.ActiveDeviceLeaseCount);

        Assert.True(runtime.CloseDevice(live).IsClosed);
    }

    [Fact]
    public void ClosingDomainKillsDeviceLeaseEffectsEvenBeforeExplicitDeviceClose()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = AssertBound(runtime.Bind(NeutralDomainProfile.OrdinaryService));
        var lease = runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/audio0"),
            NeutralDeviceRights.Configure).Lease;

        Assert.Equal(1, runtime.ActiveDeviceLeaseCount);
        Assert.True(runtime.Close(domain).IsClosed);
        Assert.Equal(0, runtime.ActiveDeviceLeaseCount);

        var close = runtime.CloseDevice(lease);
        Assert.Equal(NeutralDeviceCloseDecision.Revoked, close.Decision);
        Assert.Equal(0, runtime.ActiveDeviceLeaseCount);
    }

    [Fact]
    public void PublicDeviceLeaseSurfaceContainsNoHardwareShapedOrLaterPhaseAuthority()
    {
        var surface = new[]
        {
            typeof(NeutralDeviceIdentity),
            typeof(NeutralDeviceRights),
            typeof(NeutralDeviceLeaseHandle),
            typeof(NeutralDeviceLeaseEpoch),
            typeof(NeutralDeviceLease),
            typeof(NeutralDeviceBindResult),
            typeof(NeutralDeviceCloseResult),
        };
        var forbidden = new[]
        {
            "Physical",
            "Address",
            "Pte",
            "PageTable",
            "Iommu",
            "Dma",
            "Mmio",
            "Irq",
            "InterruptVector",
            "Vmx",
            "Vmcs",
            "Lane",
            "Opcode",
            "Queue",
        };

        foreach (var type in surface)
        foreach (var member in type.GetMembers(
                     BindingFlags.Public |
                     BindingFlags.Instance |
                     BindingFlags.Static))
        {
            var signature = member.ToString() ?? member.Name;
            foreach (var term in forbidden)
                Assert.DoesNotContain(term, signature, StringComparison.OrdinalIgnoreCase);
        }

        var publicFacadeMethods = typeof(NeutralDomainRuntimeFacade)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(static method => method.Name)
            .ToArray();
        Assert.Contains(nameof(NeutralDomainRuntimeFacade.BindDevice), publicFacadeMethods);
        Assert.Contains(nameof(NeutralDomainRuntimeFacade.CloseDevice), publicFacadeMethods);
        Assert.DoesNotContain(publicFacadeMethods, static name =>
            name.Contains("Dma", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Mmio", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Interrupt", StringComparison.OrdinalIgnoreCase));
    }

    private static NeutralDomainBindingLease AssertBound(NeutralDomainBindResult result)
    {
        Assert.True(result.IsBound, result.Reason);
        return result.Lease;
    }
}
