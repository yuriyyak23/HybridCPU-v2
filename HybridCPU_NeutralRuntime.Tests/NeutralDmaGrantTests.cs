using System.Reflection;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_NeutralRuntime.Tests;

public sealed class NeutralDmaGrantTests
{
    [Fact]
    public void ExactDeviceAndMappingMaterializeAdmissionOnlyGrant()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = AssertBound(runtime.Bind(NeutralDomainProfile.OrdinaryService));
        var device = AssertDevice(runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/net0"),
            NeutralDeviceRights.Read | NeutralDeviceRights.Write | NeutralDeviceRights.Configure));
        var mapping = AssertMapped(runtime.MapOwnedRegion(
            domain,
            new NeutralOwnedRegionSlice(
                128,
                512,
                NeutralMemoryAccess.Read | NeutralMemoryAccess.Write)));

        var result = runtime.BindDmaGrant(
            device,
            mapping,
            new NeutralDmaRange(32, 128),
            NeutralDmaDirection.DeviceReadsMemory);

        Assert.True(result.IsGranted, result.Reason);
        Assert.Equal(device, result.Grant.DeviceLease);
        Assert.Equal(mapping, result.Grant.MappingLease);
        Assert.Equal(new NeutralDmaRange(32, 128), result.Grant.Range);
        Assert.Equal(NeutralDmaDirection.DeviceReadsMemory, result.Grant.Direction);
        Assert.NotEqual(0UL, result.Grant.Handle.Value);
        Assert.NotEqual(0UL, result.Grant.Epoch.Value);
        Assert.Equal(1, runtime.ActiveDmaGrantCount);

        Assert.Equal(
            NeutralOwnedRegionCloseDecision.ActiveDependents,
            runtime.CloseOwnedRegionMapping(mapping).Decision);
        Assert.Equal(
            NeutralDeviceCloseDecision.ActiveDependents,
            runtime.CloseDevice(device).Decision);

        Assert.True(runtime.CloseDmaGrant(result.Grant).IsClosed);
        Assert.Equal(0, runtime.ActiveDmaGrantCount);
        Assert.True(runtime.CloseOwnedRegionMapping(mapping).IsClosed);
        Assert.True(runtime.CloseDevice(device).IsClosed);
    }

    [Fact]
    public void GrantRejectsWrongDomainInvalidRangeDirectionAndAccess()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domainA = AssertBound(runtime.Bind(NeutralDomainProfile.OrdinaryService));
        var domainB = AssertBound(runtime.Bind(NeutralDomainProfile.OrdinaryService));
        var deviceA = AssertDevice(runtime.BindDevice(
            domainA,
            new NeutralDeviceIdentity("device/storage0"),
            NeutralDeviceRights.Read | NeutralDeviceRights.Write | NeutralDeviceRights.Configure));
        var mappingA = AssertMapped(runtime.MapOwnedRegion(
            domainA,
            new NeutralOwnedRegionSlice(0, 256, NeutralMemoryAccess.Read)));
        var mappingB = AssertMapped(runtime.MapOwnedRegion(
            domainB,
            new NeutralOwnedRegionSlice(0, 256, NeutralMemoryAccess.Read | NeutralMemoryAccess.Write)));

        Assert.Equal(
            NeutralDmaGrantDecision.WrongDomain,
            runtime.BindDmaGrant(
                deviceA,
                mappingB,
                new NeutralDmaRange(0, 64),
                NeutralDmaDirection.DeviceReadsMemory).Decision);

        Assert.Equal(
            NeutralDmaGrantDecision.InvalidRange,
            runtime.BindDmaGrant(
                deviceA,
                mappingA,
                new NeutralDmaRange(240, 32),
                NeutralDmaDirection.DeviceReadsMemory).Decision);

        Assert.Equal(
            NeutralDmaGrantDecision.InvalidDirection,
            runtime.BindDmaGrant(
                deviceA,
                mappingA,
                new NeutralDmaRange(0, 64),
                (NeutralDmaDirection)0xff).Decision);

        Assert.Equal(
            NeutralDmaGrantDecision.InsufficientMappingAccess,
            runtime.BindDmaGrant(
                deviceA,
                mappingA,
                new NeutralDmaRange(0, 64),
                NeutralDmaDirection.DeviceWritesMemory).Decision);

        Assert.Equal(0, runtime.ActiveDmaGrantCount);
    }

    [Fact]
    public void GrantRequiresConfigureAndDirectionSpecificDeviceRights()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = AssertBound(runtime.Bind(NeutralDomainProfile.OrdinaryService));
        var device = AssertDevice(runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/audio0"),
            NeutralDeviceRights.Configure));
        var mapping = AssertMapped(runtime.MapOwnedRegion(
            domain,
            new NeutralOwnedRegionSlice(
                0,
                128,
                NeutralMemoryAccess.Read | NeutralMemoryAccess.Write)));

        Assert.Equal(
            NeutralDmaGrantDecision.InsufficientDeviceRights,
            runtime.BindDmaGrant(
                device,
                mapping,
                new NeutralDmaRange(0, 32),
                NeutralDmaDirection.DeviceReadsMemory).Decision);
        Assert.Equal(0, runtime.ActiveDmaGrantCount);
    }

    [Fact]
    public void OneLiveGrantPerExactMappingAndStaleOrForgedCloseFailClosed()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = AssertBound(runtime.Bind(NeutralDomainProfile.OrdinaryService));
        var device = AssertDevice(runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/gpu0"),
            NeutralDeviceRights.Read | NeutralDeviceRights.Configure));
        var mapping = AssertMapped(runtime.MapOwnedRegion(
            domain,
            new NeutralOwnedRegionSlice(0, 1024, NeutralMemoryAccess.Read)));
        var grant = runtime.BindDmaGrant(
            device,
            mapping,
            new NeutralDmaRange(64, 128),
            NeutralDmaDirection.DeviceReadsMemory).Grant;
        Assert.True(grant.IsMaterialized);

        Assert.Equal(
            NeutralDmaGrantDecision.AlreadyGranted,
            runtime.BindDmaGrant(
                device,
                mapping,
                new NeutralDmaRange(256, 64),
                NeutralDmaDirection.DeviceReadsMemory).Decision);

        var stale = grant with
        {
            Epoch = new NeutralDmaGrantEpoch(grant.Epoch.Value + 1),
        };
        Assert.Equal(
            NeutralDmaGrantCloseDecision.Stale,
            runtime.CloseDmaGrant(stale).Decision);

        var forged = grant with
        {
            Range = new NeutralDmaRange(grant.Range.Offset + 1, grant.Range.Length),
        };
        Assert.Equal(
            NeutralDmaGrantCloseDecision.Faulted,
            runtime.CloseDmaGrant(forged).Decision);
        Assert.Equal(1, runtime.ActiveDmaGrantCount);
        Assert.True(runtime.CloseDmaGrant(grant).IsClosed);
    }

    [Fact]
    public void PublicDmaGrantSurfaceCarriesNoRawAddressOrIommuAuthorityAndHasNoSubmit()
    {
        var surface = new[]
        {
            typeof(NeutralDmaDirection),
            typeof(NeutralDmaRange),
            typeof(NeutralDmaGrantHandle),
            typeof(NeutralDmaGrantEpoch),
            typeof(NeutralDmaGrant),
            typeof(NeutralDmaGrantResult),
            typeof(NeutralDmaGrantCloseResult),
        };
        var forbidden = new[]
        {
            "Physical",
            "Address",
            "BusAddress",
            "Iommu",
            "PageTable",
            "Pte",
            "Descriptor",
            "ScatterGather",
            "Vector",
            "Controller",
            "Queue",
            "Vmcs",
            "Vmx",
            "Lane",
            "Opcode",
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
        Assert.Contains(nameof(NeutralDomainRuntimeFacade.BindDmaGrant), methods);
        Assert.Contains(nameof(NeutralDomainRuntimeFacade.CloseDmaGrant), methods);
        Assert.DoesNotContain(methods, static name =>
            name.Contains("Submit", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("CompleteDma", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Iommu", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("BusAddress", StringComparison.OrdinalIgnoreCase));
    }

    private static NeutralDomainBindingLease AssertBound(NeutralDomainBindResult result)
    {
        Assert.True(result.IsBound, result.Reason);
        return result.Lease;
    }

    private static NeutralDeviceLease AssertDevice(NeutralDeviceBindResult result)
    {
        Assert.True(result.IsBound, result.Reason);
        return result.Lease;
    }

    private static NeutralOwnedRegionMappingLease AssertMapped(NeutralOwnedRegionMapResult result)
    {
        Assert.True(result.IsMapped, result.Reason);
        return result.Lease;
    }
}
