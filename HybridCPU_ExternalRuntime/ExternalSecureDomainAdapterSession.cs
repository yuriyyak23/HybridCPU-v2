using HybridCPU.ExternalRuntime.Contracts;
using YAKSys_Hybrid_CPU.Core;

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
public sealed partial class ExternalSecureDomainAdapterSession
{
    private sealed class CreationState(ExternalSecureDomainCreateRequest request)
    {
        public ExternalSecureDomainCreateRequest Request { get; } = request;
        public bool CallbackActive { get; set; }
        public bool Resolved { get; set; }
    }

    private sealed class DomainState
    {
        public required ExternalSecureDomainCreateRequest Request { get; init; }
        public bool IsQuarantined { get; set; }
        public bool IsRevoked { get; set; }
        public ExternalOperationIdentity? CloseOperation { get; set; }
        public bool CloseCallbackActive { get; set; }
        public bool BindingQueryActive { get; set; }
    }

    private readonly IExternalSecureDomainProvider provider;
    private readonly SecureRegisterLocalAdmissionReservations admissionReservations;
    private readonly object sync = new();
    private readonly Dictionary<ExternalSecureDomainLease, DomainState> domains = [];
    // A callback can establish provider state before its reply is received. Keep
    // exact request reservations even on failure and after terminal close.
    private readonly Dictionary<ExternalSecureDomainCreateRequest, CreationState> attemptedCreations = [];
    private readonly HashSet<ExternalOperationIdentity> attemptedClosures = [];
    // Correlation names immutable original work for the whole session lifetime.
    // Failed observation, revoke and closure never make it reusable for new work.
    // Null is a terminal no-issue denial tombstone; it retains no operand custody.
    private readonly Dictionary<ExternalOperationIdentity, ExternalSecureRegisterLocalRequest?> registerLocalIntents = [];
    private readonly Dictionary<ExternalSecureDomainHandle, ulong> observedGenerations = [];
    private readonly HashSet<ExternalSecureEvidenceContextHandle> quarantinedEvidenceContexts = [];

    public ExternalSecureDomainAdapterSession(IExternalSecureDomainProvider provider)
        : this(provider, maximumConcurrentAdmissions: 8) { }

    public ExternalSecureDomainAdapterSession(IExternalSecureDomainProvider provider, int maximumConcurrentAdmissions)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        admissionReservations = new(maximumConcurrentAdmissions);
    }

    /// <summary>
    /// Actual bounded secure-intent ingress. Local lifecycle validation precedes
    /// canonical machine denial; a provider receipt is never machine permission.
    /// </summary>
    public SecureComputeCanonicalAdmissionResult AdmitRegisterLocal(ExternalSecureRegisterLocalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        DomainState state;
        SecureRegisterLocalAdmissionReservations.Reservation? reservation;
        lock (sync)
        {
            // Re-observe only this original unresolved obligation, including
            // after local revoke. Never retry provider observation/reservation.
            if (backendReservations.TryGetValue(request.Operation, out BackendReservationState? pending) &&
                !pending.Released && ReferenceEquals(pending.Request, request))
                return BackendCleanupDenied(request);
            if (!domains.TryGetValue(request.Binding.SecureDomain, out state!) ||
                state.IsQuarantined || state.IsRevoked || state.CloseOperation.HasValue ||
                state.BindingQueryActive ||
                quarantinedEvidenceContexts.Contains(state.Request.EvidenceContext) ||
                state.Request.Parent != request.Binding.Parent)
                return SecureComputeCanonicalAdmissionResult.DeniedLocalBinding(request);
            if (provider is not IExternalSecureExecutionBindingProvider)
                return SecureComputeCanonicalAdmissionResult.DeniedLocalBinding(request);
            if (provider is not IExternalSecureExecutionReservationProvider)
                return SecureComputeCanonicalAdmissionResult.DeniedLocalBinding(request);
            if (attemptedClosures.Contains(request.Operation))
                return SecureComputeCanonicalAdmissionResult.DeniedLocalBinding(request);
            if (registerLocalIntents.TryGetValue(request.Operation, out var original))
            {
                if (original is null)
                    return CompletedRegisterDenied(request);
                if (!ReferenceEquals(original, request))
                    return SecureComputeCanonicalAdmissionResult.DeniedLocalBinding(request);
            }
            else
            {
                registerLocalIntents.Add(request.Operation, request);
            }
            if (!admissionReservations.TryReserve(request, out reservation))
                return SecureComputeCanonicalAdmissionResult.DeniedLocalBinding(request);
            state.BindingQueryActive = true;
        }
        try
        {
            ExternalSecureExecutionBindingObservation? observation;
            try { observation = ((IExternalSecureExecutionBindingProvider)provider).Revalidate(request); }
            catch { return SecureComputeCanonicalAdmissionResult.DeniedLocalBinding(request); }
            lock (sync)
            {
                if (!ReferenceEquals(domains.GetValueOrDefault(request.Binding.SecureDomain), state) ||
                    state.IsQuarantined || state.IsRevoked || state.CloseOperation.HasValue ||
                    quarantinedEvidenceContexts.Contains(state.Request.EvidenceContext))
                    return SecureComputeCanonicalAdmissionResult.DeniedLocalBinding(request);

                if (observation is null || !ReferenceEquals(observation.Request, request) ||
                    observation.Status == ExternalSecureDomainProviderStatus.Stale)
                {
                    // Stale or malformed execution-binding evidence permanently
                    // kills ingress for this generation. It does not prove provider
                    // containment: lifecycle Close/query remain available for cleanup.
                    state.IsRevoked = true;
                    admissionReservations.Invalidate(request.Binding.SecureDomain);
                    return SecureComputeCanonicalAdmissionResult.DeniedLocalBinding(request);
                }
                if (observation.Status != ExternalSecureDomainProviderStatus.Receipt)
                    return SecureComputeCanonicalAdmissionResult.DeniedLocalBinding(request);

                if (!admissionReservations.TryConsume(reservation!, request))
                    return SecureComputeCanonicalAdmissionResult.DeniedLocalBinding(request);
            }
            return ReserveBackendAndDeny(request, state);
        }
        finally
        {
            lock (sync)
            {
                state.BindingQueryActive = false;
                admissionReservations.Release(reservation!, request);
            }
        }
    }

    /// <summary>
    /// Permanently quiesces this exact local binding generation. This establishes
    /// no provider containment or semantic revocation fact; Close/query must still
    /// resolve outstanding provider obligations. Cleanup remains available.
    /// </summary>
    public ExternalSecureDomainAdapterStatus RevokeLocalBinding(ExternalSecureDomainLease domain)
    {
        lock (sync)
        {
            if (!domains.TryGetValue(domain, out DomainState? state))
                return ExternalSecureDomainAdapterStatus.Rejected;
            if (state.IsQuarantined) return ExternalSecureDomainAdapterStatus.Quarantined;
            if (state.IsRevoked) return ExternalSecureDomainAdapterStatus.Rejected;
            state.IsRevoked = true;
            admissionReservations.Invalidate(domain);
            return ExternalSecureDomainAdapterStatus.Accepted;
        }
    }

    public ExternalSecureDomainAdapterStatus Create(ExternalSecureDomainCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CreationState state;
        lock (sync)
        {
            if (quarantinedEvidenceContexts.Contains(request.EvidenceContext))
                return ExternalSecureDomainAdapterStatus.Quarantined;
            state = new(request) { CallbackActive = true };
            if (!attemptedCreations.TryAdd(request, state))
                return ExternalSecureDomainAdapterStatus.Rejected;
        }

        try
        {
            ExternalSecureDomainCreateProviderResult result;
            try { result = provider.Create(request); }
            catch { return ExternalSecureDomainAdapterStatus.TransportUnavailable; }
            return AcceptCreateResult(state, result);
        }
        finally { lock (sync) state.CallbackActive = false; }
    }

    /// <summary>Observes an uncertain original creation without reissuing it.</summary>
    public ExternalSecureDomainAdapterStatus ReconcileCreation(ExternalSecureDomainCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (provider is not IExternalSecureDomainCreateQueryProvider queryProvider)
            return ExternalSecureDomainAdapterStatus.TransportUnavailable;
        CreationState state;
        lock (sync)
        {
            if (!attemptedCreations.TryGetValue(request, out state!) ||
                !ReferenceEquals(state.Request, request) || state.CallbackActive || state.Resolved)
                return ExternalSecureDomainAdapterStatus.Rejected;
            if (quarantinedEvidenceContexts.Contains(request.EvidenceContext))
                return ExternalSecureDomainAdapterStatus.Quarantined;
            state.CallbackActive = true;
        }
        try
        {
            ExternalSecureDomainCreateProviderResult result;
            try { result = queryProvider.QueryCreate(request); }
            catch { return ExternalSecureDomainAdapterStatus.TransportUnavailable; }
            return AcceptCreateResult(state, result);
        }
        finally { lock (sync) state.CallbackActive = false; }
    }

    private ExternalSecureDomainAdapterStatus AcceptCreateResult(
        CreationState state, ExternalSecureDomainCreateProviderResult? result)
    {
        ExternalSecureDomainCreateRequest request = state.Request;
        if (result is null)
        {
            lock (sync) quarantinedEvidenceContexts.Add(request.EvidenceContext);
            return ExternalSecureDomainAdapterStatus.Quarantined;
        }
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
            // A correlated receipt ends observation even when its generation
            // cannot be adopted. It cannot be exchanged for a different domain.
            state.Resolved = true;
            if (observedGenerations.GetValueOrDefault(receipt.Domain.Handle) >= receipt.Domain.Generation.Value)
                return ExternalSecureDomainAdapterStatus.Rejected;
            // Even a receipt we cannot adopt may describe established provider
            // state. Never resurrect that generation after the conflicting close.
            observedGenerations[receipt.Domain.Handle] = receipt.Domain.Generation.Value;
            if (quarantinedEvidenceContexts.Contains(request.EvidenceContext) ||
                domains.Keys.Any(domain => domain.Handle.Equals(receipt.Domain.Handle)) ||
                !domains.TryAdd(receipt.Domain, new DomainState { Request = request }))
                return ExternalSecureDomainAdapterStatus.Rejected;
        }
        return ExternalSecureDomainAdapterStatus.Accepted;
    }

    public ExternalSecureDomainAdapterStatus Close(ExternalSecureDomainLease domain, ExternalOperationIdentity operation)
    {
        if (operation.Handle.Value == Guid.Empty || operation.Generation.Value == 0)
            return ExternalSecureDomainAdapterStatus.Rejected;
        DomainState state;
        lock (sync)
        {
            if (!domains.TryGetValue(domain, out state!)) return ExternalSecureDomainAdapterStatus.Rejected;
            if (state.IsQuarantined) return ExternalSecureDomainAdapterStatus.Quarantined;
            if (state.CloseOperation.HasValue) return ExternalSecureDomainAdapterStatus.Rejected;
            if (backendReservations.Values.Any(hold => hold.Domain.Equals(domain) && !hold.Released))
                return ExternalSecureDomainAdapterStatus.Rejected;
            if (registerLocalIntents.ContainsKey(operation)) return ExternalSecureDomainAdapterStatus.Rejected;
            if (!attemptedClosures.Add(operation)) return ExternalSecureDomainAdapterStatus.Rejected;
            // One-shot consume precedes external effects. A lost/uncertain reply
            // leaves the domain pinned, never eligible for a second close call.
            state.CloseOperation = operation;
            admissionReservations.Invalidate(domain);
            state.CloseCallbackActive = true;
        }

        try
        {
            ExternalSecureDomainCloseProviderResult result;
            try { result = provider.Close(domain, operation); }
            catch { return ExternalSecureDomainAdapterStatus.TransportUnavailable; }
            return AcceptCloseResult(domain, operation, state, result);
        }
        finally
        {
            lock (sync) state.CloseCallbackActive = false;
        }
    }

    /// <summary>Reconciles one original close without reissuing its effect.</summary>
    public ExternalSecureDomainAdapterStatus ReconcileClose(
        ExternalSecureDomainLease domain, ExternalOperationIdentity operation)
    {
        if (provider is not IExternalSecureDomainCloseQueryProvider queryProvider)
            return ExternalSecureDomainAdapterStatus.TransportUnavailable;
        DomainState state;
        lock (sync)
        {
            if (!domains.TryGetValue(domain, out state!) ||
                !state.CloseOperation.HasValue || state.CloseOperation.Value != operation ||
                state.CloseCallbackActive)
                return ExternalSecureDomainAdapterStatus.Rejected;
            if (state.IsQuarantined) return ExternalSecureDomainAdapterStatus.Quarantined;
            state.CloseCallbackActive = true;
        }
        try
        {
            ExternalSecureDomainCloseProviderResult result;
            try { result = queryProvider.QueryClose(domain, operation); }
            catch { return ExternalSecureDomainAdapterStatus.TransportUnavailable; }
            return AcceptCloseResult(domain, operation, state, result);
        }
        finally
        {
            lock (sync) state.CloseCallbackActive = false;
        }
    }

    private ExternalSecureDomainAdapterStatus AcceptCloseResult(
        ExternalSecureDomainLease domain, ExternalOperationIdentity operation,
        DomainState state, ExternalSecureDomainCloseProviderResult? result)
    {
        if (result is null)
        {
            lock (sync) state.IsQuarantined = true;
            return ExternalSecureDomainAdapterStatus.Quarantined;
        }
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

