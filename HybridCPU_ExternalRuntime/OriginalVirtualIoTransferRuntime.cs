using HybridCPU.ExternalRuntime.Contracts;
namespace HybridCPU.ExternalRuntime;
public sealed partial class HybridCpuExternalRuntime : IHybridCpuOriginalVirtualIoTransferRuntimeV1,IHybridCpuOriginalVirtualIoSnapshotReleaseRuntimeV1
{
    private sealed class OriginalIoTransferFact(ExternalOriginalVirtualIoTransferRequest request, OriginalVirtualIoFact owner)
    {
        public readonly ExternalOriginalVirtualIoTransferRequest Request = request;
        public readonly OriginalVirtualIoFact Owner = owner;
        public OriginalIoTransferFact? Self;
        public ExternalOriginalVirtualIoTransferReceipt? Raw;
        public ExternalOriginalVirtualIoTransferReceipt? OriginalRaw; // Produced receipt identity, retained through reply loss and cleanup.
        public ExternalOriginalVirtualIoSnapshotReleaseRequest? ReleaseRequest;
        public ExternalOriginalVirtualIoSnapshotRelease? Release;
    }
    private static bool TransferHistory(OriginalVirtualIoFact owner) => (ulong)owner.Transfers.Count == owner.TransfersIssued &&
        owner.Transfers.All(f => f is not null && ReferenceEquals(f.Self,f) && ReferenceEquals(f.Owner,owner) && ReferenceEquals(f.Request.Admission,owner.Request) &&
            ReferenceEquals(f.Raw,f.OriginalRaw) && (f.Raw is null || ReferenceEquals(f.Raw.Request,f.Request))) &&
        owner.Transfers.Distinct(ReferenceEqualityComparer.Instance).Count() == owner.Transfers.Count &&
        owner.Transfers.Select(f => f.Request).Distinct(ReferenceEqualityComparer.Instance).Count() == owner.Transfers.Count &&
        owner.Transfers.Select(f => f.Request.Operation).Distinct().Count() == owner.Transfers.Count;
    private static void ReleaseTransferSnapshots(OriginalVirtualIoFact owner)
    { foreach(var f in owner.Transfers) { f.Request.ReleaseInput(); f.Raw?.ReleaseData(); } }
    // New byte admission only. Historical reads and original cleanup have separate closure evidence.
    private bool CanAdmitOriginalIoLineage(OriginalVirtualIoFact io,IReadOnlyList<ChildRecord> lineage) =>
        HasOriginalNestedGuestSources(io.Child) && lineage.All(child=>
            child.State is ExternalChildDomainState.Ready or ExternalChildDomainState.Running or ExternalChildDomainState.Parked &&
            child.OriginalClose is not { Observation: not { NoEffect: true } } &&
            (child.ArtifactReleasesIssued==0 && child.ArtifactRelease is null ||
             child.ArtifactReleasesIssued==1 && child.ArtifactRelease is { } released && OriginalArtifactReleased(child,released.Mapping)));
    public ExternalOriginalVirtualIoSnapshotRelease? ReleaseOriginalVirtualIoSnapshot(ExternalOriginalVirtualIoSnapshotReleaseRequest request)
        => SnapshotRelease(request,false);
    public ExternalOriginalVirtualIoSnapshotRelease? ResolveOriginalVirtualIoSnapshotRelease(ExternalOriginalVirtualIoSnapshotReleaseRequest request)
        => SnapshotRelease(request,true);
    private ExternalOriginalVirtualIoSnapshotRelease? SnapshotRelease(ExternalOriginalVirtualIoSnapshotReleaseRequest request,bool lookup)
    {
        if(request is not {IsOriginal:true} || request.Transfer is not {Completed:true,NoEffect:false,Request.Write:false} raw ||
            FindIo(raw.Request.Admission) is not { } io)return null;
        lock(io.Device.Owner.Sync) lock(io.Child.Sync)
        {
            if(!IoSource(io) || !TransferHistory(io))return null;
            var fact=io.Transfers.SingleOrDefault(f=>ReferenceEquals(f.Request,raw.Request));
            if(fact is null || !ReferenceEquals(fact.Raw,raw) || raw.ContractVersion!=manifest.ContractVersion || raw.ManifestGeneration!=manifest.Generation)return null;
            if(fact.ReleaseRequest is { } prior)
                return ReferenceEquals(prior,request) && fact.Release is {IsOriginal:true} done && ReferenceEquals(done.Request,request) ? done : null;
            if(lookup)return null;
            fact.ReleaseRequest=request; // Existing transfer owner retains original before clearing snapshot.
            raw.ReleaseData(); // Serializes with CopyReadData; previously returned copies are detached.
            return fact.Release=new(request);
        }
    }
    public ExternalOriginalVirtualIoTransferReceipt? TransferOriginalVirtualIo(ExternalOriginalVirtualIoTransferRequest request)
    {
        if(request is null || !Valid(request.Operation) || FindIo(request.Admission) is not { } io) return null;
        lock(io.Device.Owner.Sync)
        {
            if(!IoSource(io)) return null;
            var prior = io.Transfers.SingleOrDefault(f => f.Request.Operation == request.Operation);
            if(prior is not null) return ReferenceEquals(prior.Request,request) ? prior.Raw : null;
            if(!TryGuestMapLineage(io.Child,out var lineage))return null;
            return WithNestedLineage<ExternalOriginalVirtualIoTransferReceipt?>(lineage,0,()=>
            {
            // Only a new byte effect needs current ancestor admission. Retained raw
            // lookup and exact cleanup above/below keep their original owner semantics.
            if(!IoSource(io) || !CanAdmitOriginalIoLineage(io,lineage))return null;
            if(io.CloseRequest is not null || io.Closure is not null || io.Device.ContainmentRequest is not null || io.Device.Closure is not null ||
                io.Binding is not { Closed: false } binding || !io.Child.VirtualIo.TryGetValue(binding.Receipt.IoHandle,out var canonical) || !ReferenceEquals(canonical,binding) ||
                io.Device.Backing is not { } buffer || io.Child.State == ExternalChildDomainState.Closed || io.Device.Owner.Lifecycle != Lifecycle.Ready ||
                io.Transfers.Count >= 256 || io.TransfersIssued == ulong.MaxValue) return null;
            io.Transfers.EnsureCapacity(io.Transfers.Count + 1);
            if(!ReserveChildOperation(io.Child,request.Operation,out _)) return null;
            var fact = new OriginalIoTransferFact(request,io); fact.Self = fact;
            io.Transfers.Add(fact); io.TransfersIssued++; // Original intent before any allocation or byte effect.
            try
            {
                if(!io.Device.Request.Rights.HasFlag(request.Write ? ExternalChildVirtualIoRights.Write : ExternalChildVirtualIoRights.Read) ||
                    request.Offset > (ulong)buffer.Length || (ulong)request.Length > (ulong)buffer.Length - request.Offset)
                    return fact.Raw = fact.OriginalRaw = new(request,false,true,null,manifest.ContractVersion,manifest.Generation);
                byte[]? snapshot = request.Write ? null : new byte[request.Length];
                if(request.Write) request.Input.CopyTo(buffer.AsSpan((int)request.Offset,request.Length));
                else buffer.AsSpan((int)request.Offset,request.Length).CopyTo(snapshot!);
                return fact.Raw = fact.OriginalRaw = new(request,true,false,snapshot,manifest.ContractVersion,manifest.Generation);
            }
            finally { request.ReleaseInput(); }
            });
        }
    }
    public ExternalOriginalVirtualIoTransferReceipt? ResolveOriginalVirtualIoTransfer(ExternalOriginalVirtualIoTransferRequest request)
    {
        if(request is null || FindIo(request.Admission) is not { } io) return null;
        lock(io.Device.Owner.Sync) lock(io.Child.Sync)
        {
            if(!IoSource(io)) return null;
            var fact = io.Transfers.SingleOrDefault(f => ReferenceEquals(f.Request,request));
            return fact?.Raw;
        }
    }
}
