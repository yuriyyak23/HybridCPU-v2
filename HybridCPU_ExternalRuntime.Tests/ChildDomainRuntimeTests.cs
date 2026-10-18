using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime.Tests;

public sealed class ChildDomainRuntimeTests
{
    [Fact]
    public void Manifest_SeparatesAdmissionFamiliesFromExecutableImageCapability()
    {
        var runtime = new HybridCpuExternalRuntime();
        HybridCpuExternalFeatureManifest manifest = runtime.QueryFeatures();

        Assert.Equal(HybridCpuExternalContractVersion.V1_3, manifest.ContractVersion);
        Assert.Equal(HybridCpuExternalFeatureAvailability.RuntimeAdmission,
            manifest.GetFeature(HybridCpuExternalFeatureFamily.ChildDomainLifecycle).Availability);
        Assert.Equal(HybridCpuExternalFeatureAvailability.RuntimeAdmission,
            manifest.GetFeature(HybridCpuExternalFeatureFamily.ChildGuestMemory).Availability);
        Assert.Equal(HybridCpuExternalFeatureAvailability.RuntimeAdmission,
            manifest.GetFeature(HybridCpuExternalFeatureFamily.ChildEventDelivery).Availability);
        Assert.Equal(HybridCpuExternalFeatureAvailability.RuntimeAdmission,
            manifest.GetFeature(HybridCpuExternalFeatureFamily.ChildTrapDelivery).Availability);
        Assert.Equal(HybridCpuExternalFeatureAvailability.Executable,
            manifest.GetFeature(HybridCpuExternalFeatureFamily.ChildExecutableImage).Availability);
        Assert.Equal(HybridCpuExternalFeatureAvailability.Executable,
            manifest.GetFeature(HybridCpuExternalFeatureFamily.ChildVirtualIo).Availability);
    }

    [Fact]
    public void ChildLifecycle_IsParentBoundSubsetScopedAndTerminallyClosed()
    {
        var runtime = new HybridCpuExternalRuntime();
        ExternalDomainLease parent = Bind(runtime, 1);
        ExternalChildAuthority authority = ExternalChildAuthority.Execute |
            ExternalChildAuthority.GuestMemory |
            ExternalChildAuthority.EventInjection |
            ExternalChildAuthority.TrapDelivery;
        ExternalChildResult<ExternalChildDomainCreateReceipt> create = runtime.CreateChildDomain(
            parent, ChildRequest(authority, 4096, Op(2)));
        Assert.Equal(ExternalRuntimeOutcome.Bound, create.Outcome);
        Assert.Equal(authority, create.Receipt!.GrantedAuthority);
        Assert.Equal(parent, create.Receipt.Lease.Parent);
        ExternalChildDomainLease child = create.Receipt.Lease;

        ExternalChildResult<ExternalGuestMemoryMapReceipt> map = runtime.MapChildGuestMemory(
            child, new(0, 4096, Op(3)));
        Assert.Equal(ExternalRuntimeOutcome.Succeeded, map.Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Succeeded,
            runtime.TransitionChildDomain(child, ExternalChildDomainTransition.Start, Op(4)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Succeeded,
            runtime.InjectChildEvent(child, new(ExternalChildEventKind.Timer, 1, Op(5))).Outcome);

        ExternalChildResult<ExternalChildTrapReceipt> trap = runtime.ReportChildTrap(
            child, new(ExternalChildTrapKind.MemoryFault, 1, Op(6)));
        Assert.Equal(ExternalRuntimeOutcome.Succeeded, trap.Outcome);
        Assert.Equal(ExternalChildTrapDisposition.ResumePermitted, trap.Receipt!.Disposition);
        Assert.Equal(ExternalRuntimeOutcome.Succeeded,
            runtime.TransitionChildDomain(child, ExternalChildDomainTransition.Resume, Op(7)).Outcome);

        Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.CloseDomain(parent, Op(8)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Denied, runtime.CloseChildDomain(child, Op(9)).Outcome);
        ExternalChildResult<ExternalGuestMemoryUnmapReceipt> unmap =
            runtime.UnmapChildGuestMemory(map.Receipt!.Mapping, Op(10));
        Assert.Equal(ExternalRuntimeOutcome.Closed, unmap.Outcome);
        Assert.True(unmap.Receipt!.IsTerminal);

        ExternalChildResult<ExternalChildDomainCloseReceipt> close = runtime.CloseChildDomain(child, Op(11));
        Assert.Equal(ExternalRuntimeOutcome.Closed, close.Outcome);
        Assert.True(close.Receipt!.IsTerminal);
        Assert.Equal(child, close.Receipt.Lease);
        Assert.Equal((uint)1, close.Receipt.ClosedGuestMappings);
        Assert.Equal(ExternalRuntimeOutcome.Stale, runtime.CloseChildDomain(child, Op(12)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseDomain(parent, Op(13)).Outcome);
    }

    [Fact]
    public void ChildAdmission_RejectsAuthorityAmplificationAndInvalidRanges()
    {
        var runtime = new HybridCpuExternalRuntime();
        ExternalDomainLease parent = Bind(runtime, 1);

        Assert.Equal(ExternalRuntimeOutcome.Denied,
            runtime.CreateChildDomain(parent,
                ChildRequest((ExternalChildAuthority)(1u << 31), 4096, Op(2))).Outcome);

        ExternalChildDomainLease child = runtime.CreateChildDomain(parent,
            ChildRequest(ExternalChildAuthority.GuestMemory, 4096, Op(3))).Receipt!.Lease;
        Assert.Equal(ExternalRuntimeOutcome.Denied,
            runtime.MapChildGuestMemory(child, new(4090, 16, Op(4))).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Denied,
            runtime.MapChildGuestMemory(child, new(ulong.MaxValue - 4, 8, Op(5))).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Denied,
            runtime.TransitionChildDomain(child, ExternalChildDomainTransition.Start, Op(6)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Denied,
            runtime.InjectChildEvent(child, new(ExternalChildEventKind.Wake, 1, Op(7))).Outcome);
    }

    [Fact]
    public void ForgedParentEpochChildEpochAndMappingEpochFailClosed()
    {
        var runtime = new HybridCpuExternalRuntime();
        ExternalDomainLease parent = Bind(runtime, 1);
        ExternalChildDomainLease child = runtime.CreateChildDomain(parent,
            ChildRequest(ExternalChildAuthority.GuestMemory, 4096, Op(2))).Receipt!.Lease;
        ExternalGuestMappingLease mapping = runtime.MapChildGuestMemory(child, new(0, 1024, Op(3))).Receipt!.Mapping;

        Assert.Equal(ExternalRuntimeOutcome.Stale,
            runtime.CloseChildDomain(child with { Epoch = new(child.Epoch.Value + 1) }, Op(4)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Stale,
            runtime.CloseChildDomain(child with
            {
                Parent = parent with { Epoch = new(parent.Epoch.Value + 1) }
            }, Op(5)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Stale,
            runtime.UnmapChildGuestMemory(mapping with { Epoch = new(mapping.Epoch.Value + 1) }, Op(6)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.NotFound,
            runtime.CloseChildDomain(new(new(Guid.NewGuid()), child.Epoch, parent), Op(7)).Outcome);
    }

    [Fact]
    public void EventTrapAndOperationReplayAreRejected()
    {
        var runtime = new HybridCpuExternalRuntime();
        ExternalDomainLease parent = Bind(runtime, 1);
        ExternalChildDomainLease child = runtime.CreateChildDomain(parent,
            ChildRequest(ExternalChildAuthority.Execute | ExternalChildAuthority.EventInjection |
                ExternalChildAuthority.TrapDelivery, 4096, Op(2))).Receipt!.Lease;
        Assert.Equal(ExternalRuntimeOutcome.Succeeded,
            runtime.TransitionChildDomain(child, ExternalChildDomainTransition.Start, Op(3)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Succeeded,
            runtime.InjectChildEvent(child, new(ExternalChildEventKind.External, 4, Op(4))).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Stale,
            runtime.InjectChildEvent(child, new(ExternalChildEventKind.External, 4, Op(5))).Outcome);

        ExternalOperationIdentity trapOperation = Op(6);
        Assert.Equal(ExternalRuntimeOutcome.Succeeded,
            runtime.ReportChildTrap(child, new(ExternalChildTrapKind.Preemption, 2, trapOperation)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Denied,
            runtime.TransitionChildDomain(child, ExternalChildDomainTransition.Resume, trapOperation).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Denied,
            runtime.ReportChildTrap(child, new(ExternalChildTrapKind.Preemption, 3, Op(7))).Outcome);
    }

    [Fact]
    public async Task ConcurrentDoubleCloseProducesOneDefinitiveReceipt()
    {
        var runtime = new HybridCpuExternalRuntime();
        ExternalDomainLease parent = Bind(runtime, 1);
        ExternalChildDomainLease child = runtime.CreateChildDomain(parent,
            ChildRequest(ExternalChildAuthority.Execute, 4096, Op(2))).Receipt!.Lease;

        Task<ExternalChildResult<ExternalChildDomainCloseReceipt>> first =
            Task.Run(() => runtime.CloseChildDomain(child, Op(3)));
        Task<ExternalChildResult<ExternalChildDomainCloseReceipt>> second =
            Task.Run(() => runtime.CloseChildDomain(child, Op(4)));
        ExternalChildResult<ExternalChildDomainCloseReceipt>[] results = await Task.WhenAll(first, second);

        Assert.Single(results, static result => result.Outcome == ExternalRuntimeOutcome.Closed &&
            result.Receipt is { IsTerminal: true });
        Assert.Single(results, static result => result.Outcome == ExternalRuntimeOutcome.Stale &&
            result.Receipt is null);
    }

    private static ExternalDomainLease Bind(HybridCpuExternalRuntime runtime, ulong operationGeneration) =>
        runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain,
            Op(operationGeneration))).Receipt!.Lease;

    private static ExternalChildDomainCreateRequest ChildRequest(
        ExternalChildAuthority authority, ulong memoryLimit, ExternalOperationIdentity operation) =>
        new(Guid.NewGuid(), authority, memoryLimit, operation);

    private static ExternalOperationIdentity Op(ulong generation) =>
        new(new(Guid.NewGuid()), new(generation));
}
