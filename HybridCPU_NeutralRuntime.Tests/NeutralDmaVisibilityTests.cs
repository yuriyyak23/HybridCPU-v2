using System.Reflection;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_NeutralRuntime.Tests;

public sealed class NeutralDmaVisibilityTests
{
    [Fact]
    public void PrepareAndPostWriteAcquireAreExactCycleScopedEvidence()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = Bound(runtime.Bind(NeutralDomainProfile.OrdinaryService));
        var device = Device(runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/storage0"),
            NeutralDeviceRights.Read | NeutralDeviceRights.Write | NeutralDeviceRights.Configure));
        var mapping = Mapping(runtime.MapOwnedRegion(
            domain,
            new NeutralOwnedRegionSlice(
                64,
                512,
                NeutralMemoryAccess.Read | NeutralMemoryAccess.Write)));
        var grant = Grant(runtime.BindDmaGrant(
            device,
            mapping,
            new NeutralDmaRange(32, 128),
            NeutralDmaDirection.Bidirectional));

        var prepare = runtime.PrepareDmaVisibility(grant);
        Assert.True(prepare.IsPrepared, prepare.Reason);
        Assert.Equal(grant.Handle, prepare.Evidence.GrantHandle);
        Assert.Equal(grant.Epoch, prepare.Evidence.GrantEpoch);
        Assert.Equal(grant.Direction, prepare.Evidence.Direction);
        Assert.Equal(NeutralMemoryVisibilityRequirement.PublicationFence, prepare.Evidence.Requirement);
        Assert.Equal(NeutralMemoryVisibilityOutcome.PublicationFenceSatisfied, prepare.Evidence.Outcome);
        Assert.NotEqual(0UL, prepare.Evidence.Cycle.Value);
        Assert.Equal(1UL, runtime.PublicationSequenceForTesting(mapping));
        Assert.True(runtime.HasPreparedUnacquiredDmaVisibilityCycle(grant));

        var acquire = runtime.AcquireDmaVisibility(grant);
        Assert.True(acquire.IsAcquired, acquire.Reason);
        Assert.Equal(prepare.Evidence.Cycle, acquire.Evidence.Cycle);
        Assert.Equal(NeutralMemoryAcquireRequirement.AcquisitionFence, acquire.Evidence.Requirement);
        Assert.Equal(NeutralMemoryAcquireOutcome.AcquisitionFenceSatisfied, acquire.Evidence.Outcome);
        Assert.Equal(1UL, runtime.AcquisitionSequenceForTesting(mapping));
        Assert.False(runtime.HasPreparedUnacquiredDmaVisibilityCycle(grant));
        Assert.Equal(1, runtime.ActiveDmaGrantCount);
        Assert.Equal(1, runtime.ActiveOwnedRegionMappingCount);
    }

    [Fact]
    public void AcquireRequiresPreparedCycleAndDeviceWriteDirection()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = Bound(runtime.Bind(NeutralDomainProfile.OrdinaryService));
        var writerDevice = Device(runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/net0"),
            NeutralDeviceRights.Write | NeutralDeviceRights.Configure));
        var writerMapping = Mapping(runtime.MapOwnedRegion(
            domain,
            new NeutralOwnedRegionSlice(0, 256, NeutralMemoryAccess.Write)));
        var writerGrant = Grant(runtime.BindDmaGrant(
            writerDevice,
            writerMapping,
            new NeutralDmaRange(0, 64),
            NeutralDmaDirection.DeviceWritesMemory));

        Assert.Equal(
            NeutralDmaAcquireDecision.NotPrepared,
            runtime.AcquireDmaVisibility(writerGrant).Decision);

        var readDevice = Device(runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/audio0"),
            NeutralDeviceRights.Read | NeutralDeviceRights.Configure));
        var readMapping = Mapping(runtime.MapOwnedRegion(
            domain,
            new NeutralOwnedRegionSlice(512, 128, NeutralMemoryAccess.Read)));
        var readGrant = Grant(runtime.BindDmaGrant(
            readDevice,
            readMapping,
            new NeutralDmaRange(0, 64),
            NeutralDmaDirection.DeviceReadsMemory));
        Assert.True(runtime.PrepareDmaVisibility(readGrant).IsPrepared);
        Assert.Equal(
            NeutralDmaAcquireDecision.NotRequired,
            runtime.AcquireDmaVisibility(readGrant).Decision);
        Assert.True(runtime.HasPreparedUnacquiredDmaVisibilityCycle(readGrant));
    }

    [Fact]
    public void AcquiredCycleIsConsumedAndReprepareCreatesFreshCycle()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = Bound(runtime.Bind(NeutralDomainProfile.OrdinaryService));
        var device = Device(runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/gpu0"),
            NeutralDeviceRights.Write | NeutralDeviceRights.Configure));
        var mapping = Mapping(runtime.MapOwnedRegion(
            domain,
            new NeutralOwnedRegionSlice(0, 512, NeutralMemoryAccess.Write)));
        var grant = Grant(runtime.BindDmaGrant(
            device,
            mapping,
            new NeutralDmaRange(128, 128),
            NeutralDmaDirection.DeviceWritesMemory));

        var first = runtime.PrepareDmaVisibility(grant);
        Assert.True(first.IsPrepared);
        Assert.True(runtime.AcquireDmaVisibility(grant).IsAcquired);
        Assert.Equal(
            NeutralDmaAcquireDecision.AlreadyAcquired,
            runtime.AcquireDmaVisibility(grant).Decision);

        var second = runtime.PrepareDmaVisibility(grant);
        Assert.True(second.IsPrepared);
        Assert.NotEqual(first.Evidence.Cycle, second.Evidence.Cycle);
        Assert.True(runtime.HasPreparedUnacquiredDmaVisibilityCycle(grant));
        Assert.True(runtime.AcquireDmaVisibility(grant).IsAcquired);
        Assert.Equal(2UL, runtime.PublicationSequenceForTesting(mapping));
        Assert.Equal(2UL, runtime.AcquisitionSequenceForTesting(mapping));
    }

    [Fact]
    public void StaleForgedAndRevokedGrantCannotProduceVisibilityEvidence()
    {
        var runtime = new NeutralDomainRuntimeFacade();
        var domain = Bound(runtime.Bind(NeutralDomainProfile.OrdinaryService));
        var device = Device(runtime.BindDevice(
            domain,
            new NeutralDeviceIdentity("device/camera0"),
            NeutralDeviceRights.Read | NeutralDeviceRights.Configure));
        var mapping = Mapping(runtime.MapOwnedRegion(
            domain,
            new NeutralOwnedRegionSlice(0, 128, NeutralMemoryAccess.Read)));
        var grant = Grant(runtime.BindDmaGrant(
            device,
            mapping,
            new NeutralDmaRange(0, 64),
            NeutralDmaDirection.DeviceReadsMemory));

        var stale = grant with { Epoch = new NeutralDmaGrantEpoch(grant.Epoch.Value + 1) };
        Assert.Equal(NeutralDmaPrepareDecision.Stale, runtime.PrepareDmaVisibility(stale).Decision);

        var forged = grant with { Range = new NeutralDmaRange(1, 63) };
        Assert.Equal(NeutralDmaPrepareDecision.Faulted, runtime.PrepareDmaVisibility(forged).Decision);

        Assert.True(runtime.CloseDmaGrant(grant).IsClosed);
        Assert.Equal(NeutralDmaPrepareDecision.Revoked, runtime.PrepareDmaVisibility(grant).Decision);
        Assert.Equal(NeutralDmaAcquireDecision.Revoked, runtime.AcquireDmaVisibility(grant).Decision);
    }

    [Fact]
    public void VisibilityEvidenceContainsNoCompletionOrHardwareAuthority()
    {
        var surface = new[]
        {
            typeof(NeutralDmaVisibilityCycle),
            typeof(NeutralDmaPrepareEvidence),
            typeof(NeutralDmaPrepareResult),
            typeof(NeutralDmaAcquireEvidence),
            typeof(NeutralDmaAcquireResult),
        };
        var forbidden = new[]
        {
            "Physical",
            "BusAddress",
            "Iommu",
            "PageTable",
            "Pte",
            "Descriptor",
            "Queue",
            "Vector",
            "Controller",
            "Completion",
            "Operation",
            "Submit",
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
        Assert.Contains(nameof(NeutralDomainRuntimeFacade.PrepareDmaVisibility), methods);
        Assert.Contains(nameof(NeutralDomainRuntimeFacade.AcquireDmaVisibility), methods);
        Assert.DoesNotContain(methods, static name =>
            name.Contains("Submit", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("CompleteDma", StringComparison.OrdinalIgnoreCase));
    }

    private static NeutralDomainBindingLease Bound(NeutralDomainBindResult result)
    {
        Assert.True(result.IsBound, result.Reason);
        return result.Lease;
    }

    private static NeutralDeviceLease Device(NeutralDeviceBindResult result)
    {
        Assert.True(result.IsBound, result.Reason);
        return result.Lease;
    }

    private static NeutralOwnedRegionMappingLease Mapping(NeutralOwnedRegionMapResult result)
    {
        Assert.True(result.IsMapped, result.Reason);
        return result.Lease;
    }

    private static NeutralDmaGrant Grant(NeutralDmaGrantResult result)
    {
        Assert.True(result.IsGranted, result.Reason);
        return result.Grant;
    }
}
