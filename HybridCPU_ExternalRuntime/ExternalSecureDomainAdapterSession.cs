using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime;

/// <summary>Fail-closed runtime classification of an exact secure-domain provider call.</summary>
public enum ExternalSecureDomainAdapterStatus : byte
{
    Accepted = 1,
    TransportUnavailable = 2,
    ProviderFault = 3,
    Stale = 4,
    Rejected = 5,
    Quarantined = 6,
}

/// <summary>
/// Narrow session over provider-issued SecureCompute facts. It never creates a
/// secure domain itself. A malformed receipt quarantines its evidence context;
/// unavailability, fault, and stale state never imply creation or closure.
/// </summary>
public sealed class ExternalSecureDomainAdapterSession
{
    private sealed class DomainState
    {
        public required ExternalSecureDomainCreateRequest Request { get; init; }
        public bool IsQuarantined { get; set; }
    }

    private readonly IExternalSecureDomainProvider provider;
    private readonly object sync = new();
    private readonly Dictionary<ExternalSecureDomainLease, DomainState> domains = [];
    private readonly HashSet<ExternalSecureEvidenceContextHandle> quarantinedEvidenceContexts = [];

    public ExternalSecureDomainAdapterSession(IExternalSecureDomainProvider provider) =>
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));

    public ExternalSecureDomainAdapterStatus Create(ExternalSecureDomainCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (sync)
        {
            if (quarantinedEvidenceContexts.Contains(request.EvidenceContext))
                return ExternalSecureDomainAdapterStatus.Quarantined;
        }

        ExternalSecureDomainCreateProviderResult result;
        try { result = provider.Create(request); }
        catch { return ExternalSecureDomainAdapterStatus.TransportUnavailable; }
        if (result.Status != ExternalSecureDomainProviderStatus.Receipt)
            return Classify(result.Status);

        ExternalSecureDomainCreateReceipt? receipt = result.Receipt;
        if (receipt is null || receipt.Request != request)
        {
            lock (sync) quarantinedEvidenceContexts.Add(request.EvidenceContext);
            return ExternalSecureDomainAdapterStatus.Quarantined;
        }
        lock (sync)
        {
            if (quarantinedEvidenceContexts.Contains(request.EvidenceContext) ||
                !domains.TryAdd(receipt.Domain, new DomainState { Request = request }))
                return ExternalSecureDomainAdapterStatus.Rejected;
        }
        return ExternalSecureDomainAdapterStatus.Accepted;
    }

    public ExternalSecureDomainAdapterStatus Close(ExternalSecureDomainLease domain, ExternalOperationIdentity operation)
    {
        DomainState state;
        lock (sync)
        {
            if (!domains.TryGetValue(domain, out state!)) return ExternalSecureDomainAdapterStatus.Rejected;
            if (state.IsQuarantined) return ExternalSecureDomainAdapterStatus.Quarantined;
        }

        ExternalSecureDomainCloseProviderResult result;
        try { result = provider.Close(domain, operation); }
        catch { return ExternalSecureDomainAdapterStatus.TransportUnavailable; }
        if (result.Status != ExternalSecureDomainProviderStatus.Receipt)
            return Classify(result.Status);
        ExternalSecureDomainCloseReceipt? receipt = result.Receipt;
        if (receipt is null || !receipt.Domain.Equals(domain) || receipt.Operation != operation || !receipt.IsTerminal)
        {
            lock (sync) state.IsQuarantined = true;
            return ExternalSecureDomainAdapterStatus.Quarantined;
        }
        lock (sync)
        {
            if (!ReferenceEquals(domains.GetValueOrDefault(domain), state))
                return ExternalSecureDomainAdapterStatus.Rejected;
            domains.Remove(domain);
        }
        return ExternalSecureDomainAdapterStatus.Accepted;
    }

    private static ExternalSecureDomainAdapterStatus Classify(ExternalSecureDomainProviderStatus status) => status switch
    {
        ExternalSecureDomainProviderStatus.Unavailable => ExternalSecureDomainAdapterStatus.TransportUnavailable,
        ExternalSecureDomainProviderStatus.Faulted => ExternalSecureDomainAdapterStatus.ProviderFault,
        ExternalSecureDomainProviderStatus.Stale => ExternalSecureDomainAdapterStatus.Stale,
        _ => ExternalSecureDomainAdapterStatus.Rejected,
    };
}
