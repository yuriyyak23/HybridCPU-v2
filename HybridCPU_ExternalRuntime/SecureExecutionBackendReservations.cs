using HybridCPU.ExternalRuntime.Contracts;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU.ExternalRuntime;

public sealed partial class ExternalSecureDomainAdapterSession
{
    private sealed class BackendReservationState(ExternalSecureRegisterLocalRequest request)
    {
        public ExternalSecureRegisterLocalRequest? Request { get; set; } = request;
        public ExternalSecureDomainLease Domain { get; } = request.Binding.SecureDomain;
        public ExternalSecureExecutionReservation? Reservation { get; set; }
        public bool CallbackActive { get; set; } = true;
        public bool ReleaseIssued { get; set; }
        public ExternalSecureExecutionReservation? ObservedReservation { get; set; }
        public bool Released { get; set; }
    }

    private readonly Dictionary<ExternalOperationIdentity, BackendReservationState> backendReservations = [];
    private readonly HashSet<Guid> backendReservationHandles = [];

    private SecureComputeCanonicalAdmissionResult ReserveBackendAndDeny(
        ExternalSecureRegisterLocalRequest request, DomainState domain)
    {
        if(CleanupDenialFactory is null)
        {
            // A legacy no-issue verifier cannot describe unknown backend holds. Do not call Reserve.
            lock(sync)
            {
                if(!IsLiveBackendBinding(request,domain))return SecureComputeCanonicalAdmissionResult.DeniedLocalBinding(request);
                var denied=new SafetyVerifier().AdmitSecureRegisterLocal(request);
                registerLocalIntents[request.Operation]=null;
                return denied;
            }
        }
        var state = new BackendReservationState(request);
        lock (sync)
        {
            if (!IsLiveBackendBinding(request, domain) || !backendReservations.TryAdd(request.Operation, state))
                return SecureComputeCanonicalAdmissionResult.DeniedLocalBinding(request);
        }
        try
        {
            var backend = (IExternalSecureExecutionReservationProvider)provider;
            ExternalSecureExecutionReservationResult? reserved;
            try { reserved = backend.Reserve(request); }
            catch { return BackendCleanupDenied(request); }
            lock (sync)
            {
                if (!AcceptBackendReservation(state, reserved))
                    return state.Released
                        ? SecureComputeCanonicalAdmissionResult.DeniedLocalBinding(request)
                        : BackendCleanupDenied(request);
            }
            SecureComputeCanonicalAdmissionResult result;
            ExternalSecureDomainAdapterStatus releaseStatus;
            try
            {
                lock (sync)
                {
                    // The only current branch is canonical no-issue denial. No
                    // reservation object can be converted into a machine permit.
                    result = IsLiveBackendBinding(request, domain)
                        ? new SafetyVerifier().AdmitSecureRegisterLocal(request)
                        : SecureComputeCanonicalAdmissionResult.DeniedLocalBinding(request);
                }
            }
            finally { releaseStatus = ReleaseBackendReservation(state, backend); }
            return releaseStatus == ExternalSecureDomainAdapterStatus.Accepted
                ? result
                : BackendCleanupDenied(request, result.Canonical);
        }
        finally { lock (sync) state.CallbackActive = false; }
    }

    private bool IsLiveBackendBinding(ExternalSecureRegisterLocalRequest request, DomainState domain) =>
        ReferenceEquals(domains.GetValueOrDefault(request.Binding.SecureDomain), domain) &&
        !domain.IsQuarantined && !domain.IsRevoked && !domain.CloseOperation.HasValue &&
        !quarantinedEvidenceContexts.Contains(domain.Request.EvidenceContext);

    // Caller holds sync. Only the original provider object is retained; a copied
    // receipt or reused provider handle cannot take custody of another hold.
    private bool AcceptBackendReservation(BackendReservationState state, ExternalSecureExecutionReservationResult? result)
    {
        QuiesceStaleBackendBinding(state, result);
        if (result?.Status == ExternalSecureDomainProviderStatus.Rejected &&
            state.ObservedReservation is null && state.Reservation is null && !state.ReleaseIssued &&
            ReferenceEquals(result.RefusedRequest, state.Request))
        {
            // Exact terminal no-hold fact, not an absence/status inference.
            // The reserve attempt remains dead and cannot be retried.
            state.Released = true;
            registerLocalIntents[state.Request!.Operation] = null;
            state.Request = null;
            return false;
        }
        if (result?.Status != ExternalSecureDomainProviderStatus.Receipt || result.Reservation is not { } reservation ||
            !ReferenceEquals(reservation.Request, state.Request))
            return false;
        // Even an unadoptable correlated receipt describes a provider hold.
        // Pin its original object before checking handle ownership. A later
        // query cannot exchange that obligation for a fresh handle or copy.
        if (state.ObservedReservation is not null && !ReferenceEquals(state.ObservedReservation, reservation))
            return false;
        state.ObservedReservation = reservation;
        if (!backendReservationHandles.Add(reservation.Handle)) return false;
        state.Reservation = reservation;
        return true;
    }

    // Caller holds sync. Stale backend evidence permanently closes ingress for
    // this exact generation, but never establishes release or containment.
    private void QuiesceStaleBackendBinding(BackendReservationState state,
        ExternalSecureExecutionReservationResult? result)
    {
        if (result?.Status != ExternalSecureDomainProviderStatus.Stale) return;
        if (domains.TryGetValue(state.Domain, out DomainState? domain))
            domain.IsRevoked = true;
        admissionReservations.Invalidate(state.Domain);
    }

    private ExternalSecureDomainAdapterStatus ReleaseBackendReservation(
        BackendReservationState state, IExternalSecureExecutionReservationProvider backend)
    {
        ExternalSecureExecutionReservation reservation;
        bool query;
        lock (sync)
        {
            reservation = state.Reservation!;
            query = state.ReleaseIssued;
            // One-shot mutation precedes the provider callback. Uncertain replies
            // permit only read-only QueryRelease, never a second release effect.
            state.ReleaseIssued = true;
        }
        ExternalSecureExecutionReservationResult? result;
        try { result = query ? backend.QueryRelease(reservation) : backend.Release(reservation); }
        catch { return ExternalSecureDomainAdapterStatus.TransportUnavailable; }
        lock (sync)
        {
            QuiesceStaleBackendBinding(state, result);
            if (result?.Status != ExternalSecureDomainProviderStatus.Receipt)
                return result is null ? ExternalSecureDomainAdapterStatus.Rejected : Classify(result.Status);
            if (!ReferenceEquals(result.Reservation, reservation))
                return ExternalSecureDomainAdapterStatus.Rejected;
            state.Released = true;
            registerLocalIntents[reservation.Request.Operation] = null;
            state.Request = null;
            state.Reservation = null;
            state.ObservedReservation = null;
            return ExternalSecureDomainAdapterStatus.Accepted;
        }
    }

    /// <summary>
    /// Cleanup-only recovery of the original reserve/release outcome. It cannot
    /// restart canonical admission or issue work, including after local revoke.
    /// Unknown obligations continue blocking domain Close.
    /// </summary>
    public ExternalSecureDomainAdapterStatus ReconcileBackendReservation(ExternalSecureRegisterLocalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (provider is not IExternalSecureExecutionReservationProvider backend)
            return ExternalSecureDomainAdapterStatus.TransportUnavailable;
        BackendReservationState state;
        lock (sync)
        {
            if (!backendReservations.TryGetValue(request.Operation, out state!) ||
                !ReferenceEquals(state.Request, request) || state.Released || state.CallbackActive)
                return ExternalSecureDomainAdapterStatus.Rejected;
            state.CallbackActive = true;
        }
        try
        {
            if (state.Reservation is null)
            {
                ExternalSecureExecutionReservationResult? result;
                try { result = backend.QueryReservation(request); }
                catch { return ExternalSecureDomainAdapterStatus.TransportUnavailable; }
                lock (sync)
                {
                    if (!AcceptBackendReservation(state, result))
                        return state.Released ? ExternalSecureDomainAdapterStatus.Accepted : ExternalSecureDomainAdapterStatus.Rejected;
                }
            }
            return ReleaseBackendReservation(state, backend);
        }
        finally { lock (sync) state.CallbackActive = false; }
    }
}

