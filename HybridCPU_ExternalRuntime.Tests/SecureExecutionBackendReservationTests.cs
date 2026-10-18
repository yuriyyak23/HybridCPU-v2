using HybridCPU.ExternalRuntime.Contracts;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU.ExternalRuntime.Tests;

public sealed class SecureExecutionBackendReservationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void StaleBackendEvidence_PermanentlyQuiescesGenerationButPreservesOriginalCleanup(int route)
    {
        var (session, provider, request) = Setup();
        provider.ReserveStatus = route == 0 ? ExternalSecureDomainProviderStatus.Stale : null;
        provider.LoseReserve = route == 1;
        provider.ReleaseStatus = route == 2 ? ExternalSecureDomainProviderStatus.Stale : null;
        provider.LoseRelease = route == 3;
        session.AdmitRegisterLocal(request);
        if (route == 1)
        {
            provider.QueryReserveStatus = ExternalSecureDomainProviderStatus.Stale;
            Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileBackendReservation(request));
        }
        if (route == 3)
        {
            provider.QueryReleaseStatus = ExternalSecureDomainProviderStatus.Stale;
            Assert.Equal(ExternalSecureDomainAdapterStatus.Stale, session.ReconcileBackendReservation(request));
        }
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.RevokeLocalBinding(request.Binding.SecureDomain));
        var fresh = new ExternalSecureRegisterLocalRequest(request.Binding, Operation(), 31, 37);
        Assert.Null(session.AdmitRegisterLocal(fresh).Canonical);
        Assert.Null(session.AdmitRegisterLocal(request).Canonical);
        Assert.Equal(1, provider.Reserves);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(request.Binding.SecureDomain, Operation()));
        provider.QueryReserveStatus = provider.QueryReleaseStatus = null;
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.ReconcileBackendReservation(request));
        Assert.Equal(1, provider.Reserves);
        Assert.Equal(1, provider.Releases);
        Assert.Equal(0, provider.Held);
        Assert.Null(session.AdmitRegisterLocal(fresh).Canonical);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Close(request.Binding.SecureDomain, Operation()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CorrelatedRejectedHold_CannotBeRemintedByReadOnlyQuery(bool copyHandle)
    {
        var (session, provider, request) = Setup();
        Guid handle = Guid.NewGuid();
        provider.FixedHandle = handle;
        Assert.NotNull(session.AdmitRegisterLocal(request).Canonical);
        var next = new ExternalSecureRegisterLocalRequest(request.Binding, Operation(), 7, 11);
        Assert.Null(session.AdmitRegisterLocal(next).Canonical);
        Assert.Equal(1, provider.Held);
        provider.ReservationQueryOverride = current => new(ExternalSecureDomainProviderStatus.Receipt,
            new(current, copyHandle ? handle : Guid.NewGuid()));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileBackendReservation(next));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileBackendReservation(next));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(request.Binding.SecureDomain, Operation()));
        Assert.Equal(1, provider.Held);
        Assert.Equal(1, provider.Releases); // Only the first operation released.
        Assert.Equal(2, provider.Reserves);
        provider.ReservationQueryOverride = null;
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileBackendReservation(next));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.RevokeLocalBinding(request.Binding.SecureDomain));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileBackendReservation(next));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(request.Binding.SecureDomain, Operation()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactNoHoldRefusal_CompletesOriginalAdmissionWithoutReleaseOrMachineWork(bool lostReply)
    {
        var (session, provider, request) = Setup();
        provider.RefuseReserve = true;
        provider.LoseReserve = lostReply;
        Assert.Null(session.AdmitRegisterLocal(request).Canonical);
        Assert.Equal(0, provider.Held);
        if (lostReply)
        {
            Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(request.Binding.SecureDomain, Operation()));
            Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.ReconcileBackendReservation(request));
        }
        Assert.Equal(SecureComputeCanonicalAdmissionDecision.DeniedOperationCompleted, session.AdmitRegisterLocal(request).Decision);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileBackendReservation(request));
        Assert.Equal(1, provider.Reserves);
        Assert.Equal(0, provider.Releases);
        Assert.Equal(0, provider.ReleaseQueries);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Close(request.Binding.SecureDomain, Operation()));
    }

    [Fact]
    public void CopiedNoHoldRefusalCannotClearOriginalObligation()
    {
        var (session, provider, request) = Setup();
        provider.RefuseReserve = true;
        provider.LoseReserve = true;
        session.AdmitRegisterLocal(request);
        provider.ReservationQueryOverride = current => ExternalSecureExecutionReservationResult.RefusedWithoutReservation(
            new(current.Binding, current.Operation, current.Left, current.Right));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileBackendReservation(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(request.Binding.SecureDomain, Operation()));
        provider.ReservationQueryOverride = null;
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.ReconcileBackendReservation(request));
        Assert.Equal(0, provider.Releases);
    }

    [Fact]
    public void NoHoldRefusalCannotReplaceObservedHoldOrReleaseReceipt()
    {
        var (session, provider, request) = Setup();
        provider.FixedHandle = Guid.NewGuid();
        session.AdmitRegisterLocal(request);
        var next = new ExternalSecureRegisterLocalRequest(request.Binding, Operation(), 3, 5);
        Assert.Null(session.AdmitRegisterLocal(next).Canonical); // Reused handle, correlated hold observed.
        provider.ReservationQueryOverride = ExternalSecureExecutionReservationResult.RefusedWithoutReservation;
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileBackendReservation(next));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(request.Binding.SecureDomain, Operation()));

        var (other, otherProvider, otherRequest) = Setup();
        otherProvider.LoseRelease = true;
        other.AdmitRegisterLocal(otherRequest);
        otherProvider.ReleaseQueryOverride = hold => ExternalSecureExecutionReservationResult.RefusedWithoutReservation(hold.Request);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, other.ReconcileBackendReservation(otherRequest));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, other.Close(otherRequest.Binding.SecureDomain, Operation()));
        otherProvider.ReleaseQueryOverride = null;
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, other.ReconcileBackendReservation(otherRequest));
    }

    [Fact]
    public void BindingObservationWithoutReservationCapabilityCannotReachCanonicalAdmission()
    {
        var (session, provider, request) = Setup(omitReservation: true);
        Assert.Null(session.AdmitRegisterLocal(request).Canonical);
        Assert.Equal(0, provider.Reserves);
        Assert.Equal(0, provider.Releases);
    }

    [Theory]
    [InlineData(false, ExternalSecureDomainProviderStatus.Unavailable)]
    [InlineData(false, ExternalSecureDomainProviderStatus.Stale)]
    [InlineData(false, ExternalSecureDomainProviderStatus.Faulted)]
    [InlineData(false, ExternalSecureDomainProviderStatus.Rejected)]
    [InlineData(true, ExternalSecureDomainProviderStatus.Unavailable)]
    [InlineData(true, ExternalSecureDomainProviderStatus.Stale)]
    [InlineData(true, ExternalSecureDomainProviderStatus.Faulted)]
    [InlineData(true, ExternalSecureDomainProviderStatus.Rejected)]
    public void UncertainQueriesRetainObligationAndNeverRepeatMutation(bool releaseLost, ExternalSecureDomainProviderStatus status)
    {
        var (session, provider, request) = Setup();
        provider.LoseReserve = !releaseLost;
        provider.LoseRelease = releaseLost;
        session.AdmitRegisterLocal(request);
        provider.QueryReserveStatus = provider.QueryReleaseStatus = status;
        Assert.NotEqual(ExternalSecureDomainAdapterStatus.Accepted, session.ReconcileBackendReservation(request));
        Assert.NotEqual(ExternalSecureDomainAdapterStatus.Accepted, session.ReconcileBackendReservation(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(request.Binding.SecureDomain, Operation()));
        Assert.Equal(1, provider.Reserves);
        Assert.Equal(releaseLost ? 1 : 0, provider.Releases);
        provider.QueryReserveStatus = provider.QueryReleaseStatus = null;
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.ReconcileBackendReservation(request));
        Assert.Equal(1, provider.Reserves);
        Assert.Equal(1, provider.Releases);
        Assert.Equal(0, provider.Held);
    }

    [Fact]
    public void ReusedProviderHandleCannotAuthorizeAnotherOperationOrClearItsObligation()
    {
        var (session, provider, request) = Setup();
        provider.FixedHandle = Guid.NewGuid();
        Assert.NotNull(session.AdmitRegisterLocal(request).Canonical);
        var next = new ExternalSecureRegisterLocalRequest(request.Binding, Operation(), 3, 5);
        Assert.Null(session.AdmitRegisterLocal(next).Canonical);
        Assert.Equal(2, provider.Reserves);
        Assert.Equal(1, provider.Releases);
        Assert.Equal(1, provider.Held);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileBackendReservation(next));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(request.Binding.SecureDomain, Operation()));
    }

    [Fact]
    public void CanonicalDeny_ReservesAndReleasesExactHoldOnceWithoutSubmission()
    {
        var (session, provider, request) = Setup();
        var result = session.AdmitRegisterLocal(request);
        Assert.Equal(SecureComputeCanonicalAdmissionDecision.DeniedMachineChainUnavailable, result.Decision);
        Assert.False(result.IsAllowed);
        Assert.NotNull(result.Canonical);
        Assert.Equal(1, provider.Reserves);
        Assert.Equal(1, provider.Releases);
        Assert.Equal(0, provider.Held);
        Assert.Equal(SecureComputeCanonicalAdmissionDecision.DeniedOperationCompleted, session.AdmitRegisterLocal(request).Decision);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileBackendReservation(request));
        Assert.Equal(1, provider.Reserves);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Close(request.Binding.SecureDomain, Operation()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LostReserveOrReleaseReply_PinsCloseAndRecoversCleanupOnly(bool loseRelease)
    {
        var (session, provider, request) = Setup();
        provider.LoseReserve = !loseRelease;
        provider.LoseRelease = loseRelease;
        var denied = session.AdmitRegisterLocal(request);
        Assert.False(denied.IsAllowed);
        Assert.Equal(SecureComputeCanonicalAdmissionDecision.DeniedBackendCleanupRequired, denied.Decision);
        Assert.Same(request, denied.Request);
        Assert.Equal(!loseRelease, denied.Canonical is null);
        Assert.Equal(loseRelease ? 0 : 1, provider.Held);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(request.Binding.SecureDomain, Operation()));
        Assert.Null(session.AdmitRegisterLocal(request).Canonical);
        Assert.Equal(1, provider.Reserves);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.RevokeLocalBinding(request.Binding.SecureDomain));
        var copied = new ExternalSecureRegisterLocalRequest(request.Binding, request.Operation, request.Left, request.Right);
        Assert.Equal(SecureComputeCanonicalAdmissionDecision.DeniedBackendCleanupRequired,
            session.AdmitRegisterLocal(request).Decision);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileBackendReservation(copied));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.ReconcileBackendReservation(request));
        Assert.Equal(0, provider.Held);
        Assert.Equal(1, provider.Reserves);
        Assert.Equal(1, provider.Releases);
        Assert.Equal(loseRelease ? 0 : 1, provider.ReserveQueries);
        Assert.Equal(loseRelease ? 1 : 0, provider.ReleaseQueries);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileBackendReservation(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Close(request.Binding.SecureDomain, Operation()));
    }

    [Fact]
    public async Task RevokeDuringReserve_ReleasesLateHoldWithoutCanonicalAdmission()
    {
        var (session, provider, request) = Setup();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        provider.BeforeReserveReturn = () =>
        {
            entered.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
        };
        var pending = Task.Run(() => session.AdmitRegisterLocal(request));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected,
                await Task.Run(() => session.Close(request.Binding.SecureDomain, Operation())).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileBackendReservation(request));
            Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.RevokeLocalBinding(request.Binding.SecureDomain));
        }
        finally { release.Set(); }
        Assert.Null((await pending.WaitAsync(TimeSpan.FromSeconds(10))).Canonical);
        Assert.Equal(0, provider.Held);
        Assert.Equal(1, provider.Releases);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Close(request.Binding.SecureDomain, Operation()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ForeignReserveOrReleaseReceipt_DoesNotCloseObligation(bool foreignRelease)
    {
        var (session, provider, request) = Setup();
        provider.ForeignReserve = !foreignRelease;
        provider.ForeignRelease = foreignRelease;
        var denied = session.AdmitRegisterLocal(request);
        Assert.False(denied.IsAllowed);
        Assert.Equal(SecureComputeCanonicalAdmissionDecision.DeniedBackendCleanupRequired, denied.Decision);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(request.Binding.SecureDomain, Operation()));
        provider.ForeignReserve = provider.ForeignRelease = false;
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.ReconcileBackendReservation(request));
        Assert.Equal(0, provider.Held);
        Assert.Equal(1, provider.Reserves);
        Assert.Equal(1, provider.Releases);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Close(request.Binding.SecureDomain, Operation()));
    }

    private static (ExternalSecureDomainAdapterSession, Provider, ExternalSecureRegisterLocalRequest) Setup(bool omitReservation = false)
    {
        var parent = new ExternalDomainLease(new(Guid.NewGuid()), new(1));
        var create = new ExternalSecureDomainCreateRequest(parent, new(Guid.NewGuid()),
            new(ExternalSecureAssuranceClass.High, true, true), ExternalSecureComputeContract.Version);
        var domain = new ExternalSecureDomainLease(new(Guid.NewGuid()), new(1));
        var provider = new Provider(new(create, domain, create.RequiredProperties, new(Guid.NewGuid())));
        var session = new ExternalSecureDomainAdapterSession(omitReservation ? new BindingOnlyProvider(provider) : provider);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(create));
        var binding = new ExternalSecureExecutionBinding(new(new(Guid.NewGuid()), new(1), parent), domain,
            parent, 1, ExternalSecureComputeContract.Version);
        return (session, provider, new(binding, Operation(), 19, 23));
    }

    private static ExternalOperationIdentity Operation() => new(new(Guid.NewGuid()), new(1));

    private sealed class BindingOnlyProvider(Provider inner) : IExternalSecureDomainProvider, IExternalSecureExecutionBindingProvider
    {
        public ExternalSecureDomainCreateProviderResult Create(ExternalSecureDomainCreateRequest request) => inner.Create(request);
        public ExternalSecureDomainCloseProviderResult Close(ExternalSecureDomainLease domain, ExternalOperationIdentity operation) => inner.Close(domain, operation);
        public ExternalSecureExecutionBindingObservation Revalidate(ExternalSecureRegisterLocalRequest request) => inner.Revalidate(request);
    }

    private sealed class Provider(ExternalSecureDomainCreateReceipt creation) : IExternalSecureDomainProvider,
        IExternalSecureExecutionBindingProvider, IExternalSecureExecutionReservationProvider
    {
        public int Reserves, Releases, ReserveQueries, ReleaseQueries, Held;
        public bool LoseReserve, LoseRelease, ForeignReserve, ForeignRelease;
        public bool RefuseReserve;
        private ExternalSecureRegisterLocalRequest? refused;
        public Func<ExternalSecureRegisterLocalRequest, ExternalSecureExecutionReservationResult>? ReservationQueryOverride;
        public Func<ExternalSecureExecutionReservation, ExternalSecureExecutionReservationResult>? ReleaseQueryOverride;
        public Guid? FixedHandle;
        public ExternalSecureDomainProviderStatus? ReserveStatus, ReleaseStatus, QueryReserveStatus, QueryReleaseStatus;
        public Action? BeforeReserveReturn;
        private ExternalSecureExecutionReservation? original;
        public ExternalSecureDomainCreateProviderResult Create(ExternalSecureDomainCreateRequest request) =>
            new(ExternalSecureDomainProviderStatus.Receipt, creation);
        public ExternalSecureDomainCloseProviderResult Close(ExternalSecureDomainLease domain, ExternalOperationIdentity operation) =>
            new(ExternalSecureDomainProviderStatus.Receipt, new(domain, operation, true));
        public ExternalSecureExecutionBindingObservation Revalidate(ExternalSecureRegisterLocalRequest request) =>
            new(request, ExternalSecureDomainProviderStatus.Receipt);
        public ExternalSecureExecutionReservationResult Reserve(ExternalSecureRegisterLocalRequest request)
        {
            Reserves++;
            Assert.Equal(0, Held);
            if (RefuseReserve)
            {
                refused = request;
                if (LoseReserve) throw new InvalidOperationException("refusal reply lost");
                return ExternalSecureExecutionReservationResult.RefusedWithoutReservation(request);
            }
            Held++;
            original = new(request, FixedHandle ?? Guid.NewGuid());
            BeforeReserveReturn?.Invoke();
            if (LoseReserve) throw new InvalidOperationException("reserve reply lost");
            if (ReserveStatus is { } reserveStatus) return new(reserveStatus);
            return new(ExternalSecureDomainProviderStatus.Receipt, ForeignReserve
                ? new(new(request.Binding, Operation(), 0, 0), Guid.NewGuid()) : original);
        }
        public ExternalSecureExecutionReservationResult QueryReservation(ExternalSecureRegisterLocalRequest request)
        {
            ReserveQueries++;
            if (ReservationQueryOverride is not null) return ReservationQueryOverride(request);
            if (refused is not null)
            {
                Assert.Same(refused, request);
                return ExternalSecureExecutionReservationResult.RefusedWithoutReservation(request);
            }
            Assert.Same(original!.Request, request);
            if (QueryReserveStatus is { } status) return new(status);
            return new(ExternalSecureDomainProviderStatus.Receipt, original);
        }
        public ExternalSecureExecutionReservationResult Release(ExternalSecureExecutionReservation reservation)
        {
            Releases++;
            Assert.Same(original, reservation);
            Held--;
            if (LoseRelease) throw new InvalidOperationException("release reply lost");
            if (ReleaseStatus is { } releaseStatus) return new(releaseStatus);
            return new(ExternalSecureDomainProviderStatus.Receipt, ForeignRelease
                ? new(reservation.Request, reservation.Handle) : original);
        }
        public ExternalSecureExecutionReservationResult QueryRelease(ExternalSecureExecutionReservation reservation)
        {
            ReleaseQueries++;
            if (ReleaseQueryOverride is not null) return ReleaseQueryOverride(reservation);
            Assert.Same(original, reservation);
            Assert.Equal(0, Held);
            if (QueryReleaseStatus is { } status) return new(status);
            return new(ExternalSecureDomainProviderStatus.Receipt, original);
        }
    }
}
