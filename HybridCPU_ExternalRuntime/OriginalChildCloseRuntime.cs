using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime;

public sealed partial class HybridCpuExternalRuntime : IHybridCpuOriginalChildCloseRuntimeV1
{
    private sealed class OriginalChildCloseFact
    {
        public OriginalChildCloseFact(ExternalOriginalChildCloseRequest request, ChildRecord owner, DomainRecord parent)
        { OriginalOwner = this; Request = request; Owner = owner; Parent = parent; Execution = owner.Execution; }
        public OriginalChildCloseFact OriginalOwner { get; }
        public ExternalOriginalChildCloseRequest Request { get; }
        public ChildRecord Owner { get; }
        public DomainRecord Parent { get; }
        public CpuBackedChildExecution? Execution { get; }
        public ExternalChildResult<ExternalChildDomainCloseReceipt>? Raw { get; set; }
        public ExternalOriginalChildCloseObservation? Observation { get; set; }
    }

    public ExternalOriginalChildCloseObservation? CloseOriginalChild(ExternalOriginalChildCloseRequest request)
    {
        if (request is null || request.Admission is null || !Valid(request.Operation) ||
            !TryFindChild(request.Admission.Lease, out var child, out _, out _, allowOriginalClose: true) || child!.ParentSource is not { } parent) return null;
        OriginalChildCloseFact fact;
        lock (parent.Sync)
        lock (child.Sync)
        {
            if (!TryFind(parent.Lease, out var currentParent, out _, out _) || !ReferenceEquals(currentParent, parent) ||
                !TryFindChild(child.Lease, out var currentChild, out _, out _, allowOriginalClose: true) || !ReferenceEquals(currentChild, child) ||
                !ReferenceEquals(child.OriginalAdmission, request.Admission) || child.Lease.Parent != parent.Lease ||
                request.Admission.ContractVersion != manifest.ContractVersion || request.Admission.ManifestGeneration != manifest.Generation ||
                child.OriginalClose is { Observation: not { NoEffect: true } } ||
                child.OriginalClose is { } prior && ReferenceEquals(prior.Request, request) ||
                !ReserveChildOperation(child, request.Operation, out _)) return null;
            fact = new(request, child, parent);
            child.OriginalClose = fact; // Existing owner retains request/backing before possible CPU Close.
            if (child.State == ExternalChildDomainState.Closed || parent.Lifecycle is not Lifecycle.Ready ||
                child.Mappings.Values.Any(mapping => !HasOriginalGuestClosure(mapping, child)) || HasNestedChildPins(child) || HasOriginalIoPins(child) || HasOriginalEventPins(child) || child.VirtualIo.Values.Any(static x => !x.Closed))
            {
                fact.Raw = Denied<ExternalChildDomainCloseReceipt>("Original child source/backing users refuse Close before effect.");
                fact.Observation = new(request, fact.Raw, true, false);
                return fact.Observation;
            }
            // OriginalClose without an observation fences further admission; no permission or closure.
        }
        bool completed;
        string reason = string.Empty;
        try { completed = fact.Execution is null || fact.Execution.Close(out reason); }
        catch (Exception exception) when (exception is not StackOverflowException)
        { completed = false; reason = exception.GetType().Name; }
        var raw = completed
            ? Success(ExternalRuntimeOutcome.Closed, new ExternalChildDomainCloseReceipt(child.Lease, request.Operation,
                manifest.ContractVersion, manifest.Generation, ExternalChildDomainState.Closed, true, child.ClosedMappings))
            : Result<ExternalChildDomainCloseReceipt>(ExternalRuntimeOutcome.Unknown, reason);
        lock (parent.Sync)
        lock (child.Sync)
        {
            fact.Raw = raw; // Raw precedes any final routing/publication checks.
            if (!completed || !ReferenceEquals(child.OriginalClose, fact) || !ReferenceEquals(fact.OriginalOwner, fact) ||
                !ReferenceEquals(child.OriginalAdmission, request.Admission) || !ReferenceEquals(child.ParentSource, parent) ||
                !ReferenceEquals(child.Execution, fact.Execution) || child.Mappings.Values.Any(mapping => !HasOriginalGuestClosure(mapping, child)) ||
                HasNestedChildPins(child) || HasOriginalEventPins(child) || HasOriginalIoPins(child) || child.VirtualIo.Values.Any(static x => !x.Closed)) return null;
            child.State = ExternalChildDomainState.Closed;
            child.Execution = null;
            child.OriginalClosure = new(child, raw.Receipt!);
            fact.Observation = new(request, raw, false, true);
            return TryFindChild(child.Lease, out var current, out _, out _, allowOriginalClose: true) && ReferenceEquals(current, child) &&
                TryFind(parent.Lease, out var currentParent, out _, out _) && ReferenceEquals(currentParent, parent) ? fact.Observation : null;
        }
    }

    public ExternalOriginalChildCloseObservation? ResolveOriginalChildClose(ExternalOriginalChildCloseRequest originalRequest)
    {
        if (originalRequest is null || originalRequest.Admission is null ||
            !TryFindChild(originalRequest.Admission.Lease, out var child, out _, out _, allowOriginalClose: true) || child!.ParentSource is not { } parent) return null;
        lock (parent.Sync)
        lock (child.Sync)
        {
            if (!TryFind(parent.Lease, out var current, out _, out _) || !ReferenceEquals(current, parent) ||
                child.OriginalClose is not { Observation: { } observation } fact || !ReferenceEquals(fact.OriginalOwner, fact) ||
                !ReferenceEquals(fact.Owner, child) || !ReferenceEquals(fact.Parent, parent) ||
                !ReferenceEquals(fact.Request, originalRequest) || !ReferenceEquals(observation.Request, originalRequest) ||
                !ReferenceEquals(child.OriginalAdmission, originalRequest.Admission)) return null;
            if (observation.NoEffect) return observation;
            return observation.NoFurtherUse && child.State == ExternalChildDomainState.Closed && child.Execution is null &&
                child.OriginalClosure is { } closed && ReferenceEquals(closed.OriginalOwner, closed) && ReferenceEquals(closed.Owner, child) &&
                ReferenceEquals(closed.Receipt, observation.Raw.Receipt) && ReferenceEquals(fact.Raw, observation.Raw) ? observation : null;
        }
    }
}
