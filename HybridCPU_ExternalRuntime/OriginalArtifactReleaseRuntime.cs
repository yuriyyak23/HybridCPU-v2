using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime;

public sealed partial class HybridCpuExternalRuntime
{
    private sealed class ArtifactReleaseFact(ExternalArtifactReleaseRequest request,
        ChildRecord child, GuestMappingRecord mapping, CpuBackedChildExecution execution)
    {
        public ArtifactReleaseFact? OwnerSelf { get; private set; }
        public void EstablishSelf() => OwnerSelf = this;
        public ExternalChildDomainCreateReceipt? ChildAdmission { get; } = child.OriginalAdmission;
        public ExternalGuestMemoryMapReceipt? MappingAdmission { get; } = mapping.OriginalAdmission;
        public ArtifactBindFact? Bind { get; } = child.ArtifactBind;
        public ExternalArtifactReleaseRequest Request { get; } = request;
        public ChildRecord Child { get; } = child;
        public GuestMappingRecord Mapping { get; } = mapping;
        public CpuBackedChildExecution Execution { get; } = execution;
        public ExternalArtifactReleaseReceipt? Observation { get; set; }
        public ExternalArtifactReleaseReceipt? OriginalObservation { get; set; }
    }

    private bool OriginalArtifactReleaseSources(ChildRecord child, ArtifactReleaseFact fact) =>
        HasOriginalNestedGuestSources(child) && OriginalBindHistoryIntact(child) &&
        child.ArtifactReleasesIssued == 1 && ReferenceEquals(child.ArtifactRelease, fact) &&
        ReferenceEquals(fact.OwnerSelf, fact) && ReferenceEquals(fact.Child, child) &&
        ReferenceEquals(child.OriginalOwner, child) && ReferenceEquals(fact.Mapping.OriginalOwner, fact.Mapping) &&
        ReferenceEquals(child.OriginalAdmission, fact.ChildAdmission) && ReferenceEquals(fact.Mapping.OriginalAdmission, fact.MappingAdmission) &&
        ReferenceEquals(child.ArtifactBind, fact.Bind) && fact.Bind is not null && ReferenceEquals(fact.Bind.OwnerSelf, fact.Bind) &&
        ReferenceEquals(fact.Bind.Child, child) && ReferenceEquals(fact.Bind.Mapping, fact.Mapping) &&
        (ReferenceEquals(fact.Bind.Observation, fact.Request.Artifact) ||
            ReferenceEquals(fact.Bind.CleanupArtifact, fact.Request.Artifact) && ReferenceEquals(fact.Bind.Backing, fact.Execution)) &&
        TryFind(child.Lease.Parent, out var parent, out _, out _) && ReferenceEquals(parent, child.ParentSource) &&
        ReferenceEquals(child.ArtifactAdmissionOwner, child) && ReferenceEquals(child.ArtifactAdmissionMapping, fact.Mapping) &&
        ReferenceEquals(child.Artifact, fact.Request.Artifact) &&
        child.Mappings.TryGetValue(fact.Mapping.Lease.Handle, out var owned) && ReferenceEquals(owned, fact.Mapping) &&
        TryFindMapping(fact.Mapping.Lease, out var current, out _, out _) && ReferenceEquals(current, fact.Mapping);

    private bool OriginalArtifactReleased(ChildRecord child, GuestMappingRecord mapping) =>
        child.ArtifactRelease is { Observation: { NoFurtherUse: true } receipt } fact &&
        ReferenceEquals(receipt,fact.OriginalObservation) &&
        ReferenceEquals(fact.Mapping, mapping) && ReferenceEquals(receipt.Request, fact.Request) &&
        child.Execution is null && OriginalArtifactReleaseSources(child, fact);

    public ExternalChildResult<ExternalArtifactReleaseReceipt> ReleaseChildExecutableArtifact(ExternalArtifactReleaseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Valid(request.Operation) || request.Artifact is null ||
            !TryFindChild(request.Child, out var child, out _, out _) ||
            !TryFindMapping(new(request.Artifact.MappingHandle, request.Artifact.MappingEpoch, request.Child), out var mapping, out _, out _))
            return Denied<ExternalArtifactReleaseReceipt>("Original artifact release source is unavailable.");
        ArtifactReleaseFact fact;
        lock (child!.ParentSource.Sync)
        lock (child.Sync)
        {
            if (!HasOriginalNestedGuestSources(child) || !OriginalBindHistoryIntact(child) || !TryFind(child.Lease.Parent, out var parent, out _, out _) || !ReferenceEquals(parent, child.ParentSource) ||
                parent!.Lifecycle is Lifecycle.Closed or Lifecycle.Draining or Lifecycle.Revoked or Lifecycle.Faulted or Lifecycle.Quarantined or Lifecycle.Opening ||
                !ReferenceEquals(child.ArtifactAdmissionOwner, child) || !ReferenceEquals(child.ArtifactAdmissionMapping, mapping) ||
                !ReferenceEquals(child.Artifact, request.Artifact) || child.Execution is null || child.ArtifactRelease is not null || child.ArtifactReleasesIssued != 0 ||
                child.ArtifactBind is not { } bind || !ReferenceEquals(bind.OwnerSelf, bind) ||
                !ReferenceEquals(child.OriginalOwner, child) || !ReferenceEquals(mapping!.OriginalOwner, mapping) ||
                !ReferenceEquals(bind.Child, child) || !ReferenceEquals(bind.Mapping, mapping) ||
                !ReferenceEquals(bind.ChildAdmission, child.OriginalAdmission) || !ReferenceEquals(bind.MappingAdmission, mapping!.OriginalAdmission) ||
                !(ReferenceEquals(bind.Observation, request.Artifact) ||
                    ReferenceEquals(bind.CleanupArtifact, request.Artifact) && ReferenceEquals(bind.Backing, child.Execution)) ||
                request.Artifact.ChildHandle != child.Lease.Handle || request.Artifact.ChildEpoch != child.Lease.Epoch ||
                request.Artifact.Parent != child.Lease.Parent || request.Artifact.ContractVersion != manifest.ContractVersion ||
                request.Artifact.ManifestGeneration != manifest.Generation || mapping!.Closed ||
                !child.Mappings.TryGetValue(mapping.Lease.Handle, out var ownedMapping) || !ReferenceEquals(mapping, ownedMapping) ||
                !ReserveChildOperation(child, request.Operation, out _))
                return Denied<ExternalArtifactReleaseReceipt>("Original artifact release owner/operation is absent or already invoked.");
            fact = new(request, child, mapping, child.Execution);
            fact.EstablishSelf();
            child.ArtifactReleasesIssued = 1;
            child.ArtifactRelease = fact; // Intent and no-further-Start interlock before joining execution.
        }
        if (!fact.Execution.Close(out var reason))
            return Result<ExternalArtifactReleaseReceipt>(ExternalRuntimeOutcome.Faulted, reason);
        lock (child.Sync)
        {
            var receipt = new ExternalArtifactReleaseReceipt(request, manifest.ContractVersion, manifest.Generation, true);
            fact.Observation = receipt; // Joined original lifetime retained even when final sources are lost.
            fact.OriginalObservation = receipt;
            if (!OriginalArtifactReleaseSources(child, fact) || !ReferenceEquals(child.Execution, fact.Execution))
                return Result<ExternalArtifactReleaseReceipt>(ExternalRuntimeOutcome.Faulted, "Original artifact release owner changed; backing remains pinned.");
            child.Execution = null;
            if (!TryFindChild(request.Child, out var current, out _, out _) || !ReferenceEquals(current, child) ||
                !TryFind(child.Lease.Parent, out var parent, out _, out _) || !ReferenceEquals(parent, child.ParentSource) ||
                !OriginalArtifactReleaseSources(child, fact) || !ReferenceEquals(child.Artifact, request.Artifact) ||
                !child.Mappings.TryGetValue(mapping!.Lease.Handle, out var ownedMapping) || !ReferenceEquals(ownedMapping, mapping))
                return Result<ExternalArtifactReleaseReceipt>(ExternalRuntimeOutcome.Faulted, "Original artifact release continuity changed; outcome remains pinned.");
            return Success(ExternalRuntimeOutcome.Closed, receipt);
        }
    }

    public ExternalArtifactReleaseReceipt? ResolveChildExecutableArtifactRelease(ExternalArtifactReleaseRequest request)
    {
        if (request is null || !TryFindChild(request.Child, out var child, out _, out _)) return null;
        lock (child!.ParentSource.Sync)
        lock (child.Sync)
        {
            if (child.ArtifactRelease is not { } fact || !ReferenceEquals(fact.Request, request) || !OriginalArtifactReleaseSources(child, fact) ||
                fact.Observation is not { } receipt || !ReferenceEquals(receipt,fact.OriginalObservation) || !ReferenceEquals(receipt.Request, request) || !ReferenceEquals(fact.Child, child) ||
                !TryFind(child.Lease.Parent, out var parent, out _, out _) || !ReferenceEquals(parent, child.ParentSource) ||
                !ReferenceEquals(child.ArtifactAdmissionOwner, child) || !ReferenceEquals(child.ArtifactAdmissionMapping, fact.Mapping) ||
                !ReferenceEquals(child.Artifact, request.Artifact) || child.Execution is not null ||
                !child.Mappings.TryGetValue(fact.Mapping.Lease.Handle, out var mapping) || !ReferenceEquals(fact.Mapping, mapping)) return null;
            return fact.Observation;
        }
    }
}
