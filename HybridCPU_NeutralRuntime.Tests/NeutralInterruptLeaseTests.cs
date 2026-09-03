using System.Reflection;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_NeutralRuntime.Tests;

public sealed class NeutralInterruptLeaseTests
{
    [Fact]
    public void ExactDeviceSourceCanSignalPollCompleteAndClose()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = AssertBound(runtime.Bind(NeutralDomainProfile.OrdinaryService));
        var device = Assert.True(runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/net0"),
            NeutralDeviceRights.Configure).IsBound);
        var deviceLease = runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/net1"),
            NeutralDeviceRights.Configure).Lease;
        Assert.True(deviceLease.IsMaterialized);

        var source = new NeutralInterruptSourceIdentity(
            "rx-ready",
            NeutralInterruptTrigger.Edge);
        var bound = runtime.BindInterrupt(deviceLease, source);

        Assert.True(bound.IsBound, bound.Reason);
        Assert.Equal(deviceLease, bound.Lease.DeviceLease);
        Assert.Equal(source, bound.Lease.Source);
        Assert.Equal(1, runtime.ActiveInterruptLeaseCount);

        var empty = runtime.PollInterrupt(bound.Lease);
        Assert.True(empty.IsObserved, empty.Reason);
        Assert.False(empty.DeliveryAvailable);
        Assert.Equal(0UL, empty.Sequence.Value);

        var signal = runtime.SignalInterrupt(bound.Lease);
        Assert.True(signal.IsSignaled, signal.Reason);
        Assert.NotEqual(0UL, signal.Sequence.Value);

        var duplicateSignal = runtime.SignalInterrupt(bound.Lease);
        Assert.Equal(NeutralInterruptSignalDecision.AlreadyPending, duplicateSignal.Decision);
        Assert.Equal(signal.Sequence, duplicateSignal.Sequence);

        var observed = runtime.PollInterrupt(bound.Lease);
        Assert.True(observed.IsObserved, observed.Reason);
        Assert.True(observed.DeliveryAvailable);
        Assert.Equal(signal.Sequence, observed.Sequence);

        var completed = runtime.CompleteInterruptDelivery(bound.Lease, observed.Sequence);
        Assert.True(completed.IsCompleted, completed.Reason);

        var drained = runtime.PollInterrupt(bound.Lease);
        Assert.True(drained.IsObserved, drained.Reason);
        Assert.False(drained.DeliveryAvailable);

        Assert.True(runtime.CloseInterrupt(bound.Lease).IsClosed);
        Assert.Equal(0, runtime.ActiveInterruptLeaseCount);
        Assert.True(runtime.CloseDevice(deviceLease).IsClosed);
    }

    [Fact]
    public void InterruptBindingRequiresValidSourceConfigureAndUniqueRoute()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = AssertBound(runtime.Bind(NeutralDomainProfile.OrdinaryService));
        var readOnlyDevice = runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/audio0"),
            NeutralDeviceRights.Read).Lease;
        Assert.True(readOnlyDevice.IsMaterialized);

        var insufficient = runtime.BindInterrupt(
            readOnlyDevice,
            new NeutralInterruptSourceIdentity("period", NeutralInterruptTrigger.Level));
        Assert.Equal(NeutralInterruptBindDecision.InsufficientDeviceRights, insufficient.Decision);

        Assert.True(runtime.CloseDevice(readOnlyDevice).IsClosed);
        var configurable = runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/audio1"),
            NeutralDeviceRights.Configure).Lease;
        Assert.True(configurable.IsMaterialized);

        var invalid = runtime.BindInterrupt(
            configurable,
            new NeutralInterruptSourceIdentity("", NeutralInterruptTrigger.Edge));
        Assert.Equal(NeutralInterruptBindDecision.InvalidSource, invalid.Decision);

        var source = new NeutralInterruptSourceIdentity("period", NeutralInterruptTrigger.Level);
        var first = runtime.BindInterrupt(configurable, source);
        Assert.True(first.IsBound, first.Reason);

        var duplicate = runtime.BindInterrupt(configurable, source);
        Assert.Equal(NeutralInterruptBindDecision.AlreadyBound, duplicate.Decision);
        Assert.Equal(1, runtime.ActiveInterruptLeaseCount);
    }

    [Fact]
    public void StaleForgedAndWrongDeliveryIdentityFailClosed()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = AssertBound(runtime.Bind(NeutralDomainProfile.OrdinaryService));
        var device = runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/storage0"),
            NeutralDeviceRights.Configure).Lease;
        var live = runtime.BindInterrupt(
            device,
            new NeutralInterruptSourceIdentity("completion", NeutralInterruptTrigger.Edge)).Lease;
        Assert.True(live.IsMaterialized);

        var stale = live with
        {
            Epoch = new NeutralInterruptLeaseEpoch(live.Epoch.Value + 1),
        };
        Assert.Equal(NeutralInterruptSignalDecision.Stale, runtime.SignalInterrupt(stale).Decision);

        var forged = live with
        {
            Source = new NeutralInterruptSourceIdentity("other", NeutralInterruptTrigger.Edge),
        };
        Assert.Equal(NeutralInterruptCloseDecision.Faulted, runtime.CloseInterrupt(forged).Decision);

        var signal = runtime.SignalInterrupt(live);
        Assert.True(signal.IsSignaled, signal.Reason);
        var wrong = new NeutralInterruptDeliverySequence(signal.Sequence.Value + 1);
        Assert.Equal(
            NeutralInterruptCompleteDecision.WrongSequence,
            runtime.CompleteInterruptDelivery(live, wrong).Decision);

        var stillPending = runtime.PollInterrupt(live);
        Assert.True(stillPending.DeliveryAvailable);
        Assert.Equal(signal.Sequence, stillPending.Sequence);
        Assert.True(runtime.CompleteInterruptDelivery(live, signal.Sequence).IsCompleted);
    }

    [Fact]
    public void LiveInterruptRouteBlocksDeviceCloseAndCloseDropsPendingDelivery()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = AssertBound(runtime.Bind(NeutralDomainProfile.OrdinaryService));
        var device = runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/gpu0"),
            NeutralDeviceRights.Configure).Lease;
        var route = runtime.BindInterrupt(
            device,
            new NeutralInterruptSourceIdentity("doorbell", NeutralInterruptTrigger.Level)).Lease;
        Assert.True(route.IsMaterialized);
        Assert.True(runtime.SignalInterrupt(route).IsSignaled);

        var blocked = runtime.CloseDevice(device);
        Assert.Equal(NeutralDeviceCloseDecision.ActiveDependents, blocked.Decision);
        Assert.Equal(1, runtime.ActiveInterruptLeaseCount);

        Assert.True(runtime.CloseInterrupt(route).IsClosed);
        Assert.Equal(0, runtime.ActiveInterruptLeaseCount);
        Assert.Equal(NeutralInterruptPollDecision.Revoked, runtime.PollInterrupt(route).Decision);
        Assert.True(runtime.CloseDevice(device).IsClosed);
    }

    [Fact]
    public void PublicInterruptSurfaceContainsNoRawRoutingOrHardwareAuthority()
    {
        var surface = new[]
        {
            typeof(NeutralInterruptTrigger),
            typeof(NeutralInterruptSourceIdentity),
            typeof(NeutralInterruptLeaseHandle),
            typeof(NeutralInterruptLeaseEpoch),
            typeof(NeutralInterruptDeliverySequence),
            typeof(NeutralInterruptLease),
            typeof(NeutralInterruptBindResult),
            typeof(NeutralInterruptSignalResult),
            typeof(NeutralInterruptPollResult),
            typeof(NeutralInterruptCompleteResult),
            typeof(NeutralInterruptCloseResult),
        };
        var forbidden = new[]
        {
            "Vector",
            "Controller",
            "Apic",
            "Gic",
            "Msi",
            "Gsi",
            "Physical",
            "Address",
            "Pte",
            "PageTable",
            "Iommu",
            "Dma",
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

        var methods = typeof(NeutralDomainRuntimeFacade)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(static method => method.Name)
            .ToArray();
        Assert.Contains(nameof(NeutralDomainRuntimeFacade.BindInterrupt), methods);
        Assert.Contains(nameof(NeutralDomainRuntimeFacade.SignalInterrupt), methods);
        Assert.Contains(nameof(NeutralDomainRuntimeFacade.PollInterrupt), methods);
        Assert.Contains(nameof(NeutralDomainRuntimeFacade.CompleteInterruptDelivery), methods);
        Assert.Contains(nameof(NeutralDomainRuntimeFacade.CloseInterrupt), methods);
        Assert.DoesNotContain(methods, static name =>
            name.Contains("Dma", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Iommu", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Vector", StringComparison.OrdinalIgnoreCase));
    }

    private static NeutralDomainBindingLease AssertBound(NeutralDomainBindResult result)
    {
        Assert.True(result.IsBound, result.Reason);
        return result.Lease;
    }
}
