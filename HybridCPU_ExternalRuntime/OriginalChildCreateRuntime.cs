using HybridCPU.ExternalRuntime.Contracts;
namespace HybridCPU.ExternalRuntime;
public sealed partial class HybridCpuExternalRuntime : IHybridCpuOriginalChildCreateRuntimeV1
{
    private sealed class OriginalChildCreateFact
    {
        public OriginalChildCreateFact(ExternalOriginalChildCreateRequest request, DomainRecord parent)
        { OriginalOwner = this; Request = request; Parent = parent; }
        public OriginalChildCreateFact OriginalOwner { get; }
        public ExternalOriginalChildCreateRequest Request { get; }
        public DomainRecord Parent { get; }
        public ChildRecord? Child { get; set; }
        public ExternalOriginalChildCreateObservation? Observation { get; set; }
    }
    private static bool OriginalChildCreateHistoryIntact(DomainRecord parent)
    {
        if ((ulong)parent.OriginalChildCreates.Count != parent.OriginalChildCreatesIssued) return false;
        var facts = new HashSet<OriginalChildCreateFact>(ReferenceEqualityComparer.Instance);
        var requests = new HashSet<ExternalOriginalChildCreateRequest>(ReferenceEqualityComparer.Instance);
        var operations = new HashSet<ExternalOperationIdentity>();
        foreach (var fact in parent.OriginalChildCreates)
            if (fact is null || fact.Request?.CreationRequest is null ||
                !ReferenceEquals(fact.OriginalOwner, fact) || !ReferenceEquals(fact.Parent, parent) ||
                !facts.Add(fact) || !requests.Add(fact.Request) || !operations.Add(fact.Request.CreationRequest.Operation) ||
                fact.Observation is { } raw && !ReferenceEquals(raw.Request, fact.Request)) return false;
        return true;
    }
    private bool OriginalChildCreateSources(DomainRecord parent, ExternalOriginalChildCreateRequest request) =>
        TryFind(parent.Lease, out var current, out _, out _) && ReferenceEquals(current, parent) &&
        ReferenceEquals(parent.OriginalAdmission, request.Admission) && request.Admission.Lease == parent.Lease &&
        request.Admission.ContractVersion == manifest.ContractVersion && request.Admission.ManifestGeneration == manifest.Generation;
    public ExternalOriginalChildCreateObservation? CreateOriginalChild(ExternalOriginalChildCreateRequest request)
    {
        if (request?.Admission is null || request.CreationRequest is null || !TryFind(request.Admission.Lease, out var parent, out _, out _)) return null;
        lock (parent!.Sync)
        {
            if (!OriginalChildCreateHistoryIntact(parent) || parent.OriginalChildCreatesIssued == ulong.MaxValue || !OriginalChildCreateSources(parent, request) || parent.Lifecycle != Lifecycle.Ready ||
                parent.OriginalChildCreates.Any(f => f.Observation is null || ReferenceEquals(f.Request, request) || f.Request.CreationRequest.Operation == request.CreationRequest.Operation)) return null;
            var fact = new OriginalChildCreateFact(request, parent);
            parent.OriginalChildCreates.Add(fact); // Original parent owns intent before child effect.
            parent.OriginalChildCreatesIssued++;
            var raw = CreateChildDomainCore(parent.Lease, request.CreationRequest, fact);
            fact.Child = raw.Receipt is { } known && parent.Children.TryGetValue(known.Lease.Handle, out var original) ? original : null;
            return fact.Observation = new(request, raw, raw.Outcome != ExternalRuntimeOutcome.Bound && raw.Receipt is null)
                { NestedCreationAllowed = fact.Child?.NestedCreationAllowed == true };
        }
    }
    public ExternalOriginalChildCreateObservation? ResolveOriginalChildCreate(ExternalOriginalChildCreateRequest request)
    {
        if (request?.Admission is null || !TryFind(request.Admission.Lease, out var parent, out _, out _)) return null;
        lock (parent!.Sync)
        {
            if (!OriginalChildCreateHistoryIntact(parent) || !OriginalChildCreateSources(parent, request)) return null;
            var fact = parent.OriginalChildCreates.SingleOrDefault(f => ReferenceEquals(f.Request, request));
            if (fact is null || !ReferenceEquals(fact.OriginalOwner, fact) || !ReferenceEquals(fact.Parent, parent) ||
                fact.Observation is not { } observation || !ReferenceEquals(observation.Request, request)) return null;
            if (observation.NoEffect) return observation.Raw.Receipt is null ? observation : null;
            if (fact.Child is not { } child || !ReferenceEquals(child.OriginalOwner, child) || !ReferenceEquals(child.ParentSource, parent) ||
                !parent.Children.TryGetValue(child.Lease.Handle, out var dependent) || !ReferenceEquals(dependent, child) ||
                !TryFindChild(child.Lease, out var route, out _, out _) || !ReferenceEquals(route, child) ||
                !ReferenceEquals(child.OriginalAdmission, observation.Raw.Receipt)) return null;
            return observation;
        }
    }
}
