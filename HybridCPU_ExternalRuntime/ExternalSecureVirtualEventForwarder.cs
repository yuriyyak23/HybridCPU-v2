using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime;

/// <summary>
/// Forwards secure virtual events exclusively through the established child
/// runtime contract. Provider authorization happens before event injection and
/// a stale or ambiguous authorization produces no guest-visible event.
/// </summary>
public sealed class ExternalSecureVirtualEventForwarder
{
    private readonly IExternalSecureVirtualEventProvider provider;
    private readonly IHybridCpuChildDomainRuntimeV1 childRuntime;

    public ExternalSecureVirtualEventForwarder(
        IExternalSecureVirtualEventProvider provider,
        IHybridCpuChildDomainRuntimeV1 childRuntime)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        this.childRuntime = childRuntime ?? throw new ArgumentNullException(nameof(childRuntime));
    }

    public ExternalChildResult<ExternalChildEventReceipt> Forward(
        ExternalSecureGuestRegionBinding binding,
        ExternalOperationPublicationReceipt publication,
        ExternalChildEventRequest childEvent)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(publication);
        ArgumentNullException.ThrowIfNull(childEvent);
        ExternalSecureVirtualEventAuthorizationResult authorization;
        try { authorization = provider.AuthorizeChildEvent(binding, publication, childEvent); }
        catch { return Denied(ExternalRuntimeOutcome.Unknown, "Secure event provider was unavailable."); }
        if (authorization.Status != ExternalSecureVirtualEventAuthorizationStatus.Receipt ||
            authorization.Receipt is not { } receipt || receipt.Binding != binding ||
            receipt.Publication != publication || receipt.ChildEvent != childEvent)
            return Denied(Map(authorization.Status), "Secure event authorization was absent, stale, or malformed.");
        return childRuntime.InjectChildEvent(binding.Execution.Child, childEvent);
    }

    private static ExternalRuntimeOutcome Map(ExternalSecureVirtualEventAuthorizationStatus status) => status switch
    {
        ExternalSecureVirtualEventAuthorizationStatus.Stale => ExternalRuntimeOutcome.Stale,
        ExternalSecureVirtualEventAuthorizationStatus.Faulted => ExternalRuntimeOutcome.Faulted,
        ExternalSecureVirtualEventAuthorizationStatus.Unavailable => ExternalRuntimeOutcome.Unknown,
        _ => ExternalRuntimeOutcome.Denied,
    };

    private static ExternalChildResult<ExternalChildEventReceipt> Denied(ExternalRuntimeOutcome outcome, string reason) =>
        new(outcome, null, reason);
}
