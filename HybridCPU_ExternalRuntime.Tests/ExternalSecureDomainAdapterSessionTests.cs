using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime.Tests;

public sealed class ExternalSecureDomainAdapterSessionTests
{
    [Fact]
    public void ExactProviderCreationAndTerminalClose_AreAcceptedOnce()
    {
        ExternalSecureDomainCreateRequest request = Request();
        ExternalSecureDomainLease domain = Domain();
        var provider = new FakeProvider(CreateReceipt(request, domain));
        var session = new ExternalSecureDomainAdapterSession(provider);
        ExternalOperationIdentity operation = Operation();
        provider.CloseResult = new ExternalSecureDomainCloseProviderResult(
            ExternalSecureDomainProviderStatus.Receipt,
            new ExternalSecureDomainCloseReceipt(domain, operation, true));

        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Close(domain, operation));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(domain, operation));
    }

    [Fact]
    public void MalformedCreationOrCloseReceipt_IsQuarantinedAndNeverBecomesSuccess()
    {
        ExternalSecureDomainCreateRequest request = Request();
        ExternalSecureDomainLease domain = Domain();
        var wrongRequest = Request();
        var malformedCreate = new FakeProvider(CreateReceipt(wrongRequest, domain));
        var createSession = new ExternalSecureDomainAdapterSession(malformedCreate);

        Assert.Equal(ExternalSecureDomainAdapterStatus.Quarantined, createSession.Create(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Quarantined, createSession.Create(request));

        var provider = new FakeProvider(CreateReceipt(request, domain));
        var closeSession = new ExternalSecureDomainAdapterSession(provider);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, closeSession.Create(request));
        provider.CloseResult = new ExternalSecureDomainCloseProviderResult(
            ExternalSecureDomainProviderStatus.Receipt,
            new ExternalSecureDomainCloseReceipt(domain, Operation(), true));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Quarantined, closeSession.Close(domain, Operation()));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Quarantined, closeSession.Close(domain, Operation()));
    }

    [Fact]
    public void TransportStaleAndFault_DoNotCreateOrClose()
    {
        ExternalSecureDomainCreateRequest request = Request();
        ExternalSecureDomainLease domain = Domain();
        var unavailable = new FakeProvider(CreateReceipt(request, domain))
        {
            CreateResult = new ExternalSecureDomainCreateProviderResult(ExternalSecureDomainProviderStatus.Unavailable)
        };
        Assert.Equal(ExternalSecureDomainAdapterStatus.TransportUnavailable,
            new ExternalSecureDomainAdapterSession(unavailable).Create(request));

        var provider = new FakeProvider(CreateReceipt(request, domain));
        var session = new ExternalSecureDomainAdapterSession(provider);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(request));
        provider.CloseResult = new ExternalSecureDomainCloseProviderResult(ExternalSecureDomainProviderStatus.Stale);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Stale, session.Close(domain, Operation()));
    }

    private static ExternalSecureDomainCreateRequest Request() => new(
        new ExternalDomainLease(new ExternalDomainLeaseHandle(Guid.NewGuid()), new ExternalDomainLeaseEpoch(1)),
        new ExternalSecureEvidenceContextHandle(Guid.NewGuid()),
        new ExternalSecureDomainProperties(ExternalSecureAssuranceClass.High, true, true),
        ExternalSecureComputeContract.Version);
    private static ExternalSecureDomainLease Domain() =>
        new(new ExternalSecureDomainHandle(Guid.NewGuid()), new ExternalSecureDomainGeneration(1));
    private static ExternalOperationIdentity Operation() =>
        new(new ExternalOperationHandle(Guid.NewGuid()), new ExternalOperationGeneration(1));
    private static ExternalSecureDomainCreateReceipt CreateReceipt(
        ExternalSecureDomainCreateRequest request, ExternalSecureDomainLease domain) => new(
            request, domain, request.RequiredProperties, new ExternalSecureDomainCreationProof(Guid.NewGuid()));

    private sealed class FakeProvider(ExternalSecureDomainCreateReceipt createReceipt) : IExternalSecureDomainProvider
    {
        public ExternalSecureDomainCreateProviderResult? CreateResult { get; init; }
        public ExternalSecureDomainCloseProviderResult? CloseResult { get; set; }
        public ExternalSecureDomainCreateProviderResult Create(ExternalSecureDomainCreateRequest request) =>
            CreateResult ?? new ExternalSecureDomainCreateProviderResult(ExternalSecureDomainProviderStatus.Receipt, createReceipt);
        public ExternalSecureDomainCloseProviderResult Close(ExternalSecureDomainLease domain, ExternalOperationIdentity operation) =>
            CloseResult ?? new ExternalSecureDomainCloseProviderResult(ExternalSecureDomainProviderStatus.Rejected);
    }
}
