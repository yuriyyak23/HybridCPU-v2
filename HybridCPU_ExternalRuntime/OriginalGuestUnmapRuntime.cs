using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime;

public sealed partial class HybridCpuExternalRuntime : IHybridCpuOriginalGuestUnmapRuntimeV1
{
    private sealed class GuestClosureFact
    {
        public GuestClosureFact(GuestMappingRecord mapping, ChildRecord child, OriginalGuestUnmapFact invocation, ExternalGuestMemoryUnmapReceipt receipt)
        { OriginalOwner = this; Mapping = mapping; Child = child; Invocation = invocation; Receipt = receipt; }
        public GuestClosureFact OriginalOwner { get; }
        public GuestMappingRecord Mapping { get; }
        public ChildRecord Child { get; }
        public OriginalGuestUnmapFact Invocation { get; }
        public ExternalGuestMemoryUnmapReceipt Receipt { get; }
    }
    private sealed class OriginalGuestUnmapFact
    {
        public OriginalGuestUnmapFact(ExternalOriginalGuestUnmapRequest request, GuestMappingRecord mapping, ChildRecord child)
        { OriginalOwner = this; Request = request; Mapping = mapping; Child = child; Parent = child.ParentSource; }
        public OriginalGuestUnmapFact OriginalOwner { get; }
        public ExternalOriginalGuestUnmapRequest Request { get; }
        public GuestMappingRecord Mapping { get; }
        public ChildRecord Child { get; }
        public DomainRecord Parent { get; }
        public ExternalOriginalGuestUnmapObservation? Observation { get; set; }
    }

    private bool OriginalGuestSources(GuestMappingRecord mapping, ChildRecord child) =>
        TryFindMapping(mapping.Lease, out var routed, out _, out _) && ReferenceEquals(routed, mapping) &&
        TryFindChild(child.Lease, out var current, out _, out _) && ReferenceEquals(current, child) &&
        child.Mappings.TryGetValue(mapping.Lease.Handle, out var original) && ReferenceEquals(original, mapping) &&
        TryFind(child.ParentSource.Lease, out var parent, out _, out _) && ReferenceEquals(parent, child.ParentSource) &&
        HasOriginalNestedGuestSources(child);

    private bool HasOriginalGuestClosure(GuestMappingRecord mapping, ChildRecord child) =>
        ReferenceEquals(mapping.OriginalOwner, mapping) && mapping.Closed && mapping.OriginalClosure is { } closure &&
        ReferenceEquals(closure.OriginalOwner, closure) && ReferenceEquals(closure.Mapping, mapping) && ReferenceEquals(closure.Child, child) &&
        closure.Invocation is { } fact && ReferenceEquals(closure.Receipt, fact.Observation?.Raw.Receipt) &&
        ReferenceEquals(fact.OriginalOwner, fact) && ReferenceEquals(fact.Mapping, mapping) && ReferenceEquals(fact.Child, child) &&
        ReferenceEquals(fact.Parent, child.ParentSource) && ReferenceEquals(fact.Request.Admission, mapping.OriginalAdmission) &&
        fact.Observation is { NoEffect: false, NoFurtherUse: true, Raw.Outcome: ExternalRuntimeOutcome.Closed, Raw.Receipt: { } receipt } &&
        ReferenceEquals(fact.Observation.Request, fact.Request) && receipt.Mapping == mapping.Lease && receipt.Operation == fact.Request.Operation &&
        receipt.ContractVersion == manifest.ContractVersion && receipt.ManifestGeneration == manifest.Generation && receipt.IsTerminal;

    public ExternalOriginalGuestUnmapObservation? UnmapOriginalGuest(ExternalOriginalGuestUnmapRequest request)
    {
        if (request?.Admission is null || !Valid(request.Operation) ||
            !TryFindMapping(request.Admission.Mapping, out var mapping, out _, out _) ||
            !TryFindChild(request.Admission.Mapping.Child, out var child, out _, out _)) return null;
        lock (child!.ParentSource.Sync)
        lock (child.Sync)
        {
            if (!OriginalGuestSources(mapping!, child) || !ReferenceEquals(mapping!.OriginalAdmission, request.Admission) ||
                request.Admission.ContractVersion != manifest.ContractVersion || request.Admission.ManifestGeneration != manifest.Generation ||
                mapping.OriginalUnmap is { Observation: not { NoEffect: true } } ||
                mapping.OriginalUnmap is { } prior && ReferenceEquals(prior.Request, request) ||
                !ReserveChildOperation(child, request.Operation, out _)) return null;
            var fact = new OriginalGuestUnmapFact(request, mapping, child);
            mapping.OriginalUnmap = fact;
            if (mapping.Closed || child.ParentSource.Lifecycle != Lifecycle.Ready || HasUnknownArtifactBind(child, mapping) ||
                child.Artifact is { } artifact && artifact.MappingHandle == mapping.Lease.Handle &&
                !OriginalArtifactReleased(child, mapping))
                return fact.Observation = new(request, Denied<ExternalGuestMemoryUnmapReceipt>("Original backing/source refuses Unmap before effect."), true, false);
            mapping.Closed = true;
            child.ClosedMappings++;
            var receipt = new ExternalGuestMemoryUnmapReceipt(mapping.Lease, request.Operation, manifest.ContractVersion, manifest.Generation, true);
            fact.Observation = new(request, Success(ExternalRuntimeOutcome.Closed, receipt), false, true);
            mapping.OriginalClosure = new(mapping, child, fact, receipt);
            return fact.Observation;
        }
    }

    public ExternalOriginalGuestUnmapObservation? ResolveOriginalGuestUnmap(ExternalOriginalGuestUnmapRequest request)
    {
        if (request?.Admission is null || !TryFindMapping(request.Admission.Mapping, out var mapping, out _, out _) ||
            !TryFindChild(request.Admission.Mapping.Child, out var child, out _, out _)) return null;
        lock (child!.ParentSource.Sync)
        lock (child.Sync)
        {
            if (!OriginalGuestSources(mapping!, child) || mapping!.OriginalUnmap is not { } fact ||
                !ReferenceEquals(fact.OriginalOwner, fact) || !ReferenceEquals(fact.Request, request) ||
                !ReferenceEquals(fact.Mapping, mapping) || !ReferenceEquals(fact.Child, child) ||
                !ReferenceEquals(fact.Parent, child.ParentSource) || !ReferenceEquals(mapping.OriginalAdmission, request.Admission)) return null;
            return fact.Observation;
        }
    }
}
