using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime.Tests;

public sealed class ExternalSecureVirtualEventForwarderTests
{
    [Fact]
    public void ExactPublishedAuthorization_ForwardsOnlyThroughChildEventContract()
    {
        (ExternalSecureGuestRegionBinding binding, ExternalOperationPublicationReceipt publication, ExternalChildEventRequest childEvent) input = Input();
        var provider = new FakeProvider(new ExternalSecureVirtualEventAuthorizationResult(
            ExternalSecureVirtualEventAuthorizationStatus.Receipt,
            new ExternalSecureVirtualEventAuthorizationReceipt(input.binding, input.publication, input.childEvent)));
        var child = new FakeChildRuntime();

        ExternalChildResult<ExternalChildEventReceipt> result =
            new ExternalSecureVirtualEventForwarder(provider, child).Forward(input.binding, input.publication, input.childEvent);

        Assert.Equal(ExternalRuntimeOutcome.Succeeded, result.Outcome);
        Assert.Equal(1, child.EventCalls);
    }

    [Fact]
    public void StaleOrMalformedAuthorization_NeverInjectsGuestEvent()
    {
        (ExternalSecureGuestRegionBinding binding, ExternalOperationPublicationReceipt publication, ExternalChildEventRequest childEvent) input = Input();
        var child = new FakeChildRuntime();
        var stale = new FakeProvider(new ExternalSecureVirtualEventAuthorizationResult(
            ExternalSecureVirtualEventAuthorizationStatus.Stale));

        ExternalChildResult<ExternalChildEventReceipt> result =
            new ExternalSecureVirtualEventForwarder(stale, child).Forward(input.binding, input.publication, input.childEvent);

        Assert.Equal(ExternalRuntimeOutcome.Stale, result.Outcome);
        Assert.Equal(0, child.EventCalls);
    }

    private static (ExternalSecureGuestRegionBinding binding, ExternalOperationPublicationReceipt publication, ExternalChildEventRequest childEvent) Input()
    {
        ExternalDomainLease parent = new(new ExternalDomainLeaseHandle(Guid.NewGuid()), new ExternalDomainLeaseEpoch(1));
        ExternalChildDomainLease child = new(new ExternalChildDomainHandle(Guid.NewGuid()), new ExternalChildDomainEpoch(1), parent);
        ExternalGuestMappingLease guest = new(new ExternalGuestMappingHandle(Guid.NewGuid()), new ExternalGuestMappingEpoch(1), child);
        ExternalSecureDomainLease secure = new(new ExternalSecureDomainHandle(Guid.NewGuid()), new ExternalSecureDomainGeneration(1));
        var execution = new ExternalSecureExecutionBinding(child, secure, parent, 1, ExternalSecureComputeContract.Version);
        var region = new ExternalSecureRegionBinding(secure, guest, new ExternalSecureRegionHandle(Guid.NewGuid()), new ExternalSecureRegionGeneration(1), ExternalSecureComputeContract.Version);
        ExternalOperationIdentity operation = new(new ExternalOperationHandle(Guid.NewGuid()), new ExternalOperationGeneration(1));
        ExternalOperationRequest request = new(operation, parent, ExternalOperationContract.Version,
            new ExternalGenerationSet(ExternalOperationContract.Version, [Guid.NewGuid()]), new ExternalRequestCorrelation(Guid.NewGuid()),
            ExternalEffectClass.Idempotent, ExternalVisibilityRequirement.StagedOutput, ExternalCancellationMode.ExactAcknowledgement);
        return (new ExternalSecureGuestRegionBinding(execution, region),
            new ExternalOperationPublicationReceipt(request, ExternalRuntimeOutcome.Succeeded),
            new ExternalChildEventRequest(ExternalChildEventKind.External, 1, operation));
    }

    private sealed class FakeProvider(ExternalSecureVirtualEventAuthorizationResult result) : IExternalSecureVirtualEventProvider
    {
        public ExternalSecureVirtualEventAuthorizationResult AuthorizeChildEvent(ExternalSecureGuestRegionBinding binding, ExternalOperationPublicationReceipt publication, ExternalChildEventRequest childEvent) => result;
    }

    private sealed class FakeChildRuntime : IHybridCpuChildDomainRuntimeV1
    {
        public int EventCalls { get; private set; }
        public ExternalChildResult<ExternalChildEventReceipt> InjectChildEvent(ExternalChildDomainLease child, ExternalChildEventRequest request)
        {
            EventCalls++;
            return new(ExternalRuntimeOutcome.Succeeded, new ExternalChildEventReceipt(child, request.Kind, request.Sequence, request.Operation, new HybridCpuExternalContractVersion(1, 0, 0), 1), string.Empty);
        }
        public ExternalChildResult<ExternalChildDomainCreateReceipt> CreateChildDomain(ExternalDomainLease parent, ExternalChildDomainCreateRequest request) => throw new NotSupportedException();
        public ExternalChildResult<ExternalChildDomainTransitionReceipt> TransitionChildDomain(ExternalChildDomainLease child, ExternalChildDomainTransition transition, ExternalOperationIdentity operation) => throw new NotSupportedException();
        public ExternalChildResult<ExternalGuestMemoryMapReceipt> MapChildGuestMemory(ExternalChildDomainLease child, ExternalGuestMemoryMapRequest request) => throw new NotSupportedException();
        public ExternalChildResult<ExternalGuestMemoryUnmapReceipt> UnmapChildGuestMemory(ExternalGuestMappingLease mapping, ExternalOperationIdentity operation) => throw new NotSupportedException();
        public ExternalChildResult<ExternalChildTrapReceipt> ReportChildTrap(ExternalChildDomainLease child, ExternalChildTrapRequest request) => throw new NotSupportedException();
        public ExternalChildResult<ExternalChildDomainCloseReceipt> CloseChildDomain(ExternalChildDomainLease child, ExternalOperationIdentity operation) => throw new NotSupportedException();
    }
}
