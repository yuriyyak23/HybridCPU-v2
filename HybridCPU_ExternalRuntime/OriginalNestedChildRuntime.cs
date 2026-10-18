using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime;

public sealed partial class HybridCpuExternalRuntime : IHybridCpuOriginalNestedChildRuntimeV1
{
    private sealed class OriginalNestedChildFact
    {
        public OriginalNestedChildFact(ExternalOriginalNestedChildRequest request, ChildRecord parent, DomainRecord root)
        { OriginalOwner = this; Request = request; Parent = parent; Root = root; }
        public OriginalNestedChildFact OriginalOwner { get; }
        public ExternalOriginalNestedChildRequest Request { get; }
        public ChildRecord Parent { get; }
        public DomainRecord Root { get; }
        public ChildRecord? Child { get; set; }
        public ExternalOriginalNestedChildObservation? Observation { get; set; }
    }

    private static bool NestedHistory(ChildRecord owner)
    {
        if (owner.NestedChildrenIssued != (ulong)owner.OriginalNestedChildren.Count) return false;
        var facts = new HashSet<OriginalNestedChildFact>(ReferenceEqualityComparer.Instance);
        var requests = new HashSet<ExternalOriginalNestedChildRequest>(ReferenceEqualityComparer.Instance);
        var operations = new HashSet<ExternalOperationIdentity>();
        foreach (var fact in owner.OriginalNestedChildren)
            if (fact is null || fact.Request?.CreationRequest is null ||
                !ReferenceEquals(fact.OriginalOwner, fact) || !ReferenceEquals(fact.Parent, owner) ||
                !ReferenceEquals(fact.Root, owner.ParentSource) || !ReferenceEquals(fact.Request.ParentAdmission, owner.OriginalAdmission) ||
                !facts.Add(fact) || !requests.Add(fact.Request) || !operations.Add(fact.Request.CreationRequest.Operation) ||
                fact.Observation is { } raw && !ReferenceEquals(raw.Request, fact.Request)) return false;
        return true;
    }
    private bool ChildFactoryIdentity(ChildRecord child, DomainRecord root)
    {
        if (!ReferenceEquals(child.OriginalOwner, child) || !ReferenceEquals(child.ParentSource, root) ||
            !root.Children.TryGetValue(child.Lease.Handle, out var inventory) || !ReferenceEquals(inventory, child) ||
            child.OriginalAdmission is not { } admission || admission.Lease != child.Lease ||
            admission.ContractVersion != manifest.ContractVersion || admission.ManifestGeneration != manifest.Generation)
            return false;
        lock (childRegistrySync)
            if (!childRegistry.TryGetValue(child.Lease.Handle, out var route) || !ReferenceEquals(route, child)) return false;
        if (child.ImmediateParent is { } parent)
            return child.CreateFactory is null && child.NestedFactory is { Observation: { } observation } nested &&
                ReferenceEquals(nested.OriginalOwner, nested) && ReferenceEquals(nested.Parent, parent) && ReferenceEquals(nested.Root, root) &&
                ReferenceEquals(nested.Child, child) && ReferenceEquals(observation.Request, nested.Request) &&
                !observation.NoEffect && ReferenceEquals(observation.Raw.Receipt, admission) &&
                observation.NestedCreationAllowed == child.NestedCreationAllowed && NestedHistory(parent) && parent.OriginalNestedChildren.Contains(nested);
        return child.NestedFactory is null && child.CreateFactory is { Observation: { } raw } factory &&
            ReferenceEquals(factory.OriginalOwner, factory) && ReferenceEquals(factory.Parent, root) && ReferenceEquals(factory.Child, child) &&
            OriginalChildCreateHistoryIntact(root) && root.OriginalChildCreates.Contains(factory) && ReferenceEquals(raw.Request, factory.Request) &&
            ReferenceEquals(factory.Request.Admission, root.OriginalAdmission) && !raw.NoEffect &&
            ReferenceEquals(raw.Raw.Receipt, admission) && raw.NestedCreationAllowed == child.NestedCreationAllowed;
    }

    private bool TryNestedLineage(ChildRecord parent, DomainRecord root, out List<ChildRecord> lineage)
    {
        lineage = [];
        var seen = new HashSet<ChildRecord>();
        for (ChildRecord? current = parent; current is not null; current = current.ImmediateParent)
        {
            if (lineage.Count >= ExternalOriginalNestedChildContract.MaximumDepth || !seen.Add(current) || !ReferenceEquals(current.ParentSource, root)) return false;
            lineage.Add(current);
        }
        lineage.Reverse();
        return true;
    }

    // Root then immutable ancestor chain. There are no external callbacks in this factory.
    private static T WithNestedLineage<T>(IReadOnlyList<ChildRecord> lineage, int index, Func<T> commit)
    {
        if (index == lineage.Count) return commit();
        lock (lineage[index].Sync) return WithNestedLineage(lineage, index + 1, commit);
    }

    private bool NestedSources(DomainRecord root, IReadOnlyList<ChildRecord> lineage) =>
        TryFind(root.Lease, out var currentRoot, out _, out _) && ReferenceEquals(currentRoot, root) &&
        root.OriginalAdmission is { } admission && admission.Lease == root.Lease &&
        admission.ContractVersion == manifest.ContractVersion && admission.ManifestGeneration == manifest.Generation &&
        lineage.All(child => ChildFactoryIdentity(child, root) && NestedHistory(child));

    public ExternalOriginalNestedChildObservation? CreateOriginalNestedChild(ExternalOriginalNestedChildRequest request)
    {
        if (request?.ParentAdmission is null || request.CreationRequest is null ||
            !TryFindChild(request.ParentAdmission.Lease, out var parent, out _, out _, allowOriginalClose: true)) return null;
        var root = parent!.ParentSource;
        if (!TryNestedLineage(parent, root, out var lineage)) return null;
        lock (root.Sync)
        return WithNestedLineage<ExternalOriginalNestedChildObservation?>(lineage, 0, () =>
        {
            if (!NestedSources(root, lineage) || !ReferenceEquals(parent.OriginalAdmission, request.ParentAdmission) ||
                parent.NestedChildrenIssued == ulong.MaxValue ||
                parent.OriginalNestedChildren.Any(f => f.Observation is null || ReferenceEquals(f.Request, request) ||
                    f.Request.CreationRequest.Operation == request.CreationRequest.Operation)) return null;
            var fact = new OriginalNestedChildFact(request, parent, root);
            parent.OriginalNestedChildren.Add(fact); parent.NestedChildrenIssued++; // Intent before possible child effect.
            var creation = request.CreationRequest;
            if (!parent.NestedCreationAllowed || root.Lifecycle != Lifecycle.Ready ||
                lineage.Any(x => x.State is not (ExternalChildDomainState.Ready or ExternalChildDomainState.Running or ExternalChildDomainState.Parked) ||
                    x.OriginalClose is { Observation: not { NoEffect: true } }) ||
                !Valid(creation.Operation) || creation.RequestId == Guid.Empty || !ValidAuthority(creation.RequestedAuthority) ||
                (creation.RequestedAuthority & ~parent.Authority) != 0 ||
                creation.GuestMemoryLimitBytes == 0 || creation.GuestMemoryLimitBytes > parent.GuestMemoryLimitBytes)
                return fact.Observation = new(request, Denied<ExternalChildDomainCreateReceipt>("Original nested permission, lineage or authority subset refused before effect."), true, true, false);
            var raw = CreateChildDomainCore(root.Lease, creation, null, fact);
            fact.Child = raw.Receipt is { } receipt && root.Children.TryGetValue(receipt.Lease.Handle, out var child) ? child : null;
            return fact.Observation = new(request, raw, raw.Receipt is null && raw.Outcome != ExternalRuntimeOutcome.Bound,
                raw.Receipt is null && raw.Outcome != ExternalRuntimeOutcome.Bound, fact.Child?.NestedCreationAllowed == true);
        });
    }

    public ExternalOriginalNestedChildObservation? ResolveOriginalNestedChild(ExternalOriginalNestedChildRequest request)
    {
        if (request?.ParentAdmission is null || !TryFindChild(request.ParentAdmission.Lease, out var parent, out _, out _, allowOriginalClose: true)) return null;
        var root = parent!.ParentSource;
        if (!TryNestedLineage(parent, root, out var lineage)) return null;
        lock (root.Sync)
        return WithNestedLineage<ExternalOriginalNestedChildObservation?>(lineage, 0, () =>
        {
            if (!NestedSources(root, lineage) || !ReferenceEquals(parent.OriginalAdmission, request.ParentAdmission)) return null;
            var fact = parent.OriginalNestedChildren.SingleOrDefault(f => ReferenceEquals(f.Request, request));
            if (fact?.Observation is not { } observation || !ReferenceEquals(fact.OriginalOwner, fact) || !ReferenceEquals(observation.Request, request)) return null;
            if (observation.NoEffect) return observation.NoFurtherAdmission && observation.Raw.Receipt is null ? observation : null;
            return fact.Child is { } child && ChildFactoryIdentity(child, root) &&
                ReferenceEquals(child.ImmediateParent, parent) && ReferenceEquals(child.NestedFactory, fact) ? observation : null;
        });
    }

    private bool HasNestedChildPins(ChildRecord parent)
    {
        if (!NestedHistory(parent)) return true;
        foreach (var fact in parent.OriginalNestedChildren)
        {
            if (fact.Observation is not { } observation || !ReferenceEquals(observation.Request, fact.Request)) return true;
            if (observation.NoEffect)
            { if (!observation.NoFurtherAdmission || observation.Raw.Receipt is not null) return true; continue; }
            if (fact.Child is not { } child || !ReferenceEquals(child.NestedFactory, fact) ||
                !ReferenceEquals(child.ImmediateParent, parent) || !ChildFactoryIdentity(child, parent.ParentSource)) return true;
            lock (child.Sync)
                if (child.State != ExternalChildDomainState.Closed || child.Execution is not null || child.OriginalClosure is not { } closed ||
                    !ReferenceEquals(closed.OriginalOwner, closed) || !ReferenceEquals(closed.Owner, child) || closed.Receipt.Lease != child.Lease ||
                    !closed.Receipt.IsTerminal || closed.Receipt.ResultingState != ExternalChildDomainState.Closed) return true;
        }
        return false;
    }
}
