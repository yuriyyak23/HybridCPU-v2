using System.Security.Cryptography;
using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime;

public sealed partial class HybridCpuExternalRuntime : IHybridCpuOriginalArtifactBindDispositionRuntimeV1
{
    private sealed class ArtifactBindFact(ExternalChildArtifactBindRequest request,
        ChildRecord child, GuestMappingRecord mapping, byte[] package)
    {
        public ArtifactBindFact? OwnerSelf { get; private set; }
        public void EstablishSelf() => OwnerSelf = this;
        public ExternalChildArtifactBindRequest Request { get; } = request;
        public ExternalChildDomainCreateReceipt? ChildAdmission { get; } = child.OriginalAdmission;
        public ExternalGuestMemoryMapReceipt? MappingAdmission { get; } = mapping.OriginalAdmission;
        public ChildRecord Child { get; } = child;
        public GuestMappingRecord Mapping { get; } = mapping;
        public byte[] Package { get; } = package;
        public string Digest { get; } = Convert.ToHexStringLower(SHA256.HashData(package));
        public ExternalChildArtifactBindReceipt? Observation { get; set; }
        public ExternalChildResult<ExternalChildArtifactBindReceipt>? Raw { get; set; }
        public bool NoEffect { get; set; }
        public CpuBackedChildExecution? Backing { get; set; }
        public ExternalChildArtifactBindReceipt? CleanupArtifact { get; set; }
    }

    private static bool OriginalBindHistoryIntact(ChildRecord child)
    {
        if (child.ArtifactBindsIssued != (ulong)child.OriginalArtifactBinds.Count) return false;
        var facts = new HashSet<ArtifactBindFact>(ReferenceEqualityComparer.Instance);
        var requests = new HashSet<ExternalChildArtifactBindRequest>(ReferenceEqualityComparer.Instance);
        var operations = new HashSet<ExternalOperationIdentity>();
        foreach (var fact in child.OriginalArtifactBinds)
        {
            if (fact is null || fact.Request is null || !ReferenceEquals(fact.OwnerSelf, fact) ||
                !ReferenceEquals(fact.Child, child) || !facts.Add(fact) ||
                !requests.Add(fact.Request) || !operations.Add(fact.Request.Operation)) return false;
            if (fact.NoEffect ? fact.Raw is not { Outcome: ExternalRuntimeOutcome.Denied, Receipt: null } :
                !(fact.Raw is { Outcome: ExternalRuntimeOutcome.Succeeded, Receipt: { } receipt } &&
                    ReferenceEquals(receipt, fact.Observation) || ReferenceEquals(child.ArtifactBind, fact))) return false;
        }
        return true;
    }
    private bool HasUnknownArtifactBind(ChildRecord child, GuestMappingRecord mapping) =>
        !OriginalBindHistoryIntact(child) || child.OriginalArtifactBinds.Any(fact =>
            ReferenceEquals(fact.Mapping, mapping) && !fact.NoEffect && fact.Observation is null &&
            !(fact.CleanupArtifact is not null && OriginalArtifactReleased(child, mapping)));

    public ExternalOriginalArtifactBindDisposition? ResolveOriginalArtifactBindDisposition(
        ExternalChildDomainLease child, ExternalChildArtifactBindRequest originalRequest)
    {
        if (originalRequest?.PackageBytes is null || !TryFindChild(child, out var record, out _, out _)) return null;
        lock (record!.ParentSource.Sync)
        lock (record.Sync)
        {
            if (!HasOriginalNestedGuestSources(record) || !OriginalBindHistoryIntact(record)) return null;
            var fact = record.OriginalArtifactBinds.SingleOrDefault(item => ReferenceEquals(item.Request, originalRequest));
            if (fact?.Raw is not { } raw || !ReferenceEquals(record.OriginalOwner, record) ||
                fact.ChildAdmission is null || fact.MappingAdmission is null ||
                !ReferenceEquals(record.OriginalAdmission, fact.ChildAdmission) ||
                !ReferenceEquals(fact.Mapping.OriginalOwner, fact.Mapping) || !ReferenceEquals(fact.Mapping.OriginalAdmission, fact.MappingAdmission) ||
                !TryFind(record.Lease.Parent, out var parent, out _, out _) || !ReferenceEquals(parent, record.ParentSource) ||
                !record.Mappings.TryGetValue(fact.Mapping.Lease.Handle, out var owned) || !ReferenceEquals(owned, fact.Mapping) ||
                !TryFindMapping(originalRequest.GuestMapping, out var current, out _, out _) || !ReferenceEquals(current, fact.Mapping) ||
                Convert.ToHexStringLower(SHA256.HashData(originalRequest.PackageBytes)) != fact.Digest) return null;
            return new(child, originalRequest, raw, fact.NoEffect, manifest.ContractVersion, manifest.Generation, fact.Digest)
            { CleanupArtifact = fact.CleanupArtifact };
        }
    }

    public ExternalChildArtifactBindReceipt? ResolveChildExecutableArtifactBind(
        ExternalChildDomainLease child, ExternalChildArtifactBindRequest originalRequest)
    {
        if (originalRequest is null || originalRequest.PackageBytes is null ||
            !TryFindChild(child, out var record, out _, out _)) return null;
        lock (record!.ParentSource.Sync)
        lock (record.Sync)
        {
            if (!HasOriginalNestedGuestSources(record) || !OriginalBindHistoryIntact(record) || record.ArtifactBind is not { Observation: { } receipt } fact ||
                !ReferenceEquals(fact.OwnerSelf, fact) || !ReferenceEquals(fact.Request, originalRequest) || !ReferenceEquals(fact.Child, record) ||
                !ReferenceEquals(record.OriginalOwner, record) || fact.ChildAdmission is null || fact.MappingAdmission is null ||
                !ReferenceEquals(fact.ChildAdmission, record.OriginalAdmission) ||
                !ReferenceEquals(fact.Mapping.OriginalOwner, fact.Mapping) || !ReferenceEquals(fact.MappingAdmission, fact.Mapping.OriginalAdmission) ||
                !TryFind(record.Lease.Parent, out var parent, out _, out _) || !ReferenceEquals(parent, record.ParentSource) ||
                !TryFindMapping(originalRequest.GuestMapping, out var mapping, out _, out _) || !ReferenceEquals(mapping, fact.Mapping) ||
                !record.Mappings.TryGetValue(mapping!.Lease.Handle, out var owned) || !ReferenceEquals(owned, mapping) ||
                !ReferenceEquals(record.ArtifactAdmissionOwner, record) || !ReferenceEquals(record.ArtifactAdmissionMapping, mapping) ||
                !ReferenceEquals(record.Artifact, receipt) || receipt.Operation != originalRequest.Operation ||
                receipt.MaximumPipelineCycles != originalRequest.MaximumPipelineCycles ||
                receipt.PackageSha256 != fact.Digest ||
                Convert.ToHexStringLower(SHA256.HashData(originalRequest.PackageBytes)) != fact.Digest)
                return null;
            return receipt; // Completed original admission; no effect and no fresh execution permission.
        }
    }
}
