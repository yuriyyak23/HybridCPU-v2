using HybridCPU.ExternalRuntime.Contracts;
namespace HybridCPU.ExternalRuntime;
public sealed partial class HybridCpuExternalRuntime : IHybridCpuOriginalGuestMapRuntimeV1
{
    private sealed class OriginalGuestMapFact
    {
        public OriginalGuestMapFact(ExternalOriginalGuestMapRequest request, ChildRecord child)
        { OriginalOwner = this; Request = request; Child = child; Parent = child.ParentSource; }
        public OriginalGuestMapFact OriginalOwner { get; }
        public ExternalOriginalGuestMapRequest Request { get; }
        public ChildRecord Child { get; }
        public DomainRecord Parent { get; }
        public GuestMappingRecord? Mapping { get; set; }
        public ExternalOriginalGuestMapObservation? Observation { get; set; }
    }
    private static bool OriginalGuestMapHistoryIntact(ChildRecord child)
    {
        if ((ulong)child.OriginalGuestMaps.Count != child.OriginalGuestMapsIssued) return false;
        var facts = new HashSet<OriginalGuestMapFact>(ReferenceEqualityComparer.Instance);
        var requests = new HashSet<ExternalOriginalGuestMapRequest>(ReferenceEqualityComparer.Instance);
        var operations = new HashSet<ExternalOperationIdentity>();
        foreach (var fact in child.OriginalGuestMaps)
        {
            if (fact is null || fact.Request?.MappingRequest is null ||
                !ReferenceEquals(fact.OriginalOwner, fact) || !ReferenceEquals(fact.Child, child) ||
                !ReferenceEquals(fact.Parent, child.ParentSource) || !facts.Add(fact) ||
                !requests.Add(fact.Request) || !operations.Add(fact.Request.MappingRequest.Operation) ||
                fact.Observation is { } observation && !ReferenceEquals(observation.Request, fact.Request)) return false;
        }
        return true;
    }
    private bool OriginalGuestMapSources(ChildRecord child) =>
        TryFindChild(child.Lease, out var current, out _, out _) && ReferenceEquals(current, child) &&
        TryFind(child.ParentSource.Lease, out var parent, out _, out _) && ReferenceEquals(parent, child.ParentSource) &&
        HasOriginalNestedGuestSources(child);
    private bool HasOriginalNestedGuestSources(ChildRecord child) => child.ImmediateParent is null ||
        TryNestedLineage(child, child.ParentSource, out var lineage) && NestedSources(child.ParentSource, lineage);
    private bool TryGuestMapLineage(ChildRecord child, out List<ChildRecord> lineage)
    {
        if (child.ImmediateParent is not null) return TryNestedLineage(child, child.ParentSource, out lineage);
        lineage = [child]; return true; // Existing non-nested V1 factories keep their contract.
    }
    public ExternalOriginalGuestMapObservation? MapOriginalGuest(ExternalOriginalGuestMapRequest request)
    {
        if (request?.Admission is null || request.MappingRequest is null ||
            !TryFindChild(request.Admission.Lease, out var child, out _, out _)) return null;
        if (!TryGuestMapLineage(child!, out var lineage)) return null;
        lock (child!.ParentSource.Sync)
        return WithNestedLineage<ExternalOriginalGuestMapObservation?>(lineage, 0, () =>
        {
            if (!OriginalGuestMapHistoryIntact(child) || child.OriginalGuestMapsIssued == ulong.MaxValue ||
                !OriginalGuestMapSources(child) || child.ParentSource.Lifecycle != Lifecycle.Ready ||
                !ReferenceEquals(child.OriginalAdmission, request.Admission) ||
                request.Admission.ContractVersion != manifest.ContractVersion || request.Admission.ManifestGeneration != manifest.Generation ||
                child.OriginalGuestMaps.Any(f => ReferenceEquals(f.Request, request) || f.Request.MappingRequest.Operation == request.MappingRequest.Operation)) return null;
            var fact = new OriginalGuestMapFact(request, child);
            child.OriginalGuestMaps.Add(fact); // Existing admitted child owns intent before mapping effect.
            child.OriginalGuestMapsIssued++;
            if (child.ImmediateParent is not null && lineage.Any(x =>
                x.State is not (ExternalChildDomainState.Ready or ExternalChildDomainState.Running or ExternalChildDomainState.Parked) ||
                x.OriginalClose is { Observation: not { NoEffect: true } }))
                return fact.Observation = new(request, Denied<ExternalGuestMemoryMapReceipt>("Original nested guest lineage refuses Map before effect."), true);
            var raw = MapChildGuestMemoryCore(child.Lease, request.MappingRequest, fact);
            GuestMappingRecord? mapping = null;
            if (raw.Outcome == ExternalRuntimeOutcome.Succeeded && raw.Receipt is { } receipt)
                child.Mappings.TryGetValue(receipt.Mapping.Handle, out mapping);
            fact.Mapping = mapping;
            return fact.Observation = new(request, raw, raw.Outcome != ExternalRuntimeOutcome.Succeeded && raw.Receipt is null);
        });
    }
    public ExternalOriginalGuestMapObservation? ResolveOriginalGuestMap(ExternalOriginalGuestMapRequest request)
    {
        if (request?.Admission is null || !TryFindChild(request.Admission.Lease, out var child, out _, out _)) return null;
        if (!TryGuestMapLineage(child!, out var lineage)) return null;
        lock (child!.ParentSource.Sync)
        return WithNestedLineage<ExternalOriginalGuestMapObservation?>(lineage, 0, () =>
        {
            if (!OriginalGuestMapHistoryIntact(child) || !OriginalGuestMapSources(child) || !ReferenceEquals(child.OriginalAdmission, request.Admission)) return null;
            var fact = child.OriginalGuestMaps.SingleOrDefault(f => ReferenceEquals(f.Request, request));
            if (fact is null || !ReferenceEquals(fact.OriginalOwner, fact) || !ReferenceEquals(fact.Child, child) ||
                !ReferenceEquals(fact.Parent, child.ParentSource) || fact.Observation is not { } observation ||
                !ReferenceEquals(observation.Request, request)) return null;
            if (observation.NoEffect) return observation.Raw.Receipt is null ? observation : null;
            if (fact.Mapping is not { } mapping || !OriginalGuestSources(mapping, child) ||
                !ReferenceEquals(mapping.OriginalAdmission, observation.Raw.Receipt)) return null;
            return observation;
        });
    }
}
