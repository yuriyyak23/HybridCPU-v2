using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime;

public sealed partial class HybridCpuExternalRuntime
{
    private sealed class OriginalDomainCloseFact
    {
        public OriginalDomainCloseFact(ExternalOriginalDomainCloseRequest request, DomainRecord owner)
        { OriginalOwner = this; Request = request; Owner = owner; }
        public OriginalDomainCloseFact OriginalOwner { get; }
        public ExternalOriginalDomainCloseRequest Request { get; }
        public DomainRecord Owner { get; }
        public BackendCloseResult? Raw { get; set; }
        public ExternalOriginalDomainCloseObservation? Observation { get; set; }
    }

    public ExternalOriginalDomainCloseObservation? CloseOriginalDomain(ExternalOriginalDomainCloseRequest request)
    {
        if (request is null || request.Admission is null || !Valid(request.Operation) ||
            !TryFind(request.Admission.Lease, out var owner, out _, out _)) return null;
        OriginalDomainCloseFact fact;
        lock (owner!.Sync)
        {
            if (!TryFind(request.Admission.Lease, out var current, out _, out _) || !ReferenceEquals(current, owner) ||
                !ReferenceEquals(owner.OriginalAdmission, request.Admission) ||
                request.Admission.ContractVersion != manifest.ContractVersion || request.Admission.ManifestGeneration != manifest.Generation ||
                owner.OriginalClose is { Observation: not { NoEffect: true } } ||
                owner.OriginalClose is { } prior && ReferenceEquals(prior.Request, request) ||
                !TryReserveOperation(request.Operation)) return null;
            fact = new(request, owner);
            owner.OriginalClose = fact; // Original request/source before possible backend effect.
            if (owner.Lifecycle is Lifecycle.Closed or Lifecycle.Draining or Lifecycle.Revoked or Lifecycle.Faulted or Lifecycle.Quarantined or Lifecycle.Opening ||
                owner.InFlight is not null || HasLiveChildren(owner))
            {
                fact.Observation = new(request, new(ExternalRuntimeOutcome.Denied, null,
                    "Original parent lifecycle/dependents refuse Close before effect."), true, false);
                return fact.Observation;
            }
            owner.Lifecycle = Lifecycle.Draining;
            owner.InFlight = request.Operation;
        }
        BackendCloseResult? backend;
        var invocation = InvokeBackend(() => owner.Owner.Close(owner.Lease, request.Operation, manifest.ContractVersion, manifest.Generation),
            owner, request.Operation, out backend, out Exception? backendError);
        lock (owner.Sync)
        {
            fact.Raw = backend; // Original raw before source/routing checks and diagnostics.
            if (invocation != BackendInvocation.Completed)
            {
                owner.Lifecycle = Lifecycle.Quarantined;
                if (invocation == BackendInvocation.Faulted) { owner.InFlight = null; Monitor.PulseAll(owner.Sync); }
                return null; // Unknown is pinned and cannot repeat Close.
            }
            owner.InFlight = null;
            if (!ReferenceEquals(owner.OriginalClose, fact) || !ReferenceEquals(fact.Owner, owner) ||
                !ReferenceEquals(owner.OriginalAdmission, request.Admission) || backend is null ||
                backend.Outcome != BackendOutcome.Accepted || !ExactClose(backend.Receipt, owner.Lease, request.Operation) || HasLiveChildren(owner))
            {
                owner.Lifecycle = Lifecycle.Quarantined;
                return null;
            }
            owner.Lifecycle = Lifecycle.Closed;
            owner.Completed.Add(request.Operation);
            owner.Context = null; owner.RootAuthority = null; owner.Binding = null;
            fact.Observation = new(request, new(ExternalRuntimeOutcome.Closed, backend.Receipt, string.Empty), false, true);
        }
        TryDiagnostics(() => diagnostics?.Detach(owner.Lease, manifest.Generation));
        lock (owner.Sync)
            return TryFind(owner.Lease, out var current, out _, out _) && ReferenceEquals(current, owner) ? fact.Observation : null;
    }

    public ExternalOriginalDomainCloseObservation? ResolveOriginalDomainClose(ExternalOriginalDomainCloseRequest originalRequest)
    {
        if (originalRequest is null || originalRequest.Admission is null ||
            !TryFind(originalRequest.Admission.Lease, out var owner, out _, out _)) return null;
        lock (owner!.Sync)
        {
            if (owner.OriginalClose is not { Observation: { } observation } fact ||
                !ReferenceEquals(fact.Request, originalRequest) || !ReferenceEquals(observation.Request, originalRequest) ||
                !ReferenceEquals(fact.OriginalOwner, fact) ||
                !ReferenceEquals(fact.Owner, owner) || !ReferenceEquals(owner.OriginalAdmission, originalRequest.Admission)) return null;
            if (observation.NoEffect) return observation;
            return observation.NoFurtherUse && observation.Raw.Outcome == ExternalRuntimeOutcome.Closed &&
                ReferenceEquals(observation.Raw.Receipt, fact.Raw?.Receipt) && ExactClose(observation.Raw.Receipt, owner.Lease, originalRequest.Operation) &&
                owner.Lifecycle == Lifecycle.Closed && owner.Context is null && owner.RootAuthority is null && owner.Binding is null
                ? observation : null;
        }
    }
}
