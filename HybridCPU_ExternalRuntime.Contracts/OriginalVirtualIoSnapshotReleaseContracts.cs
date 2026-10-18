namespace HybridCPU.ExternalRuntime.Contracts;

public sealed class ExternalOriginalVirtualIoSnapshotReleaseRequest
{
    public ExternalOriginalVirtualIoSnapshotReleaseRequest(ExternalOriginalVirtualIoTransferReceipt transfer)
    { Transfer=transfer ?? throw new ArgumentNullException(nameof(transfer));Original=this; }
    private readonly ExternalOriginalVirtualIoSnapshotReleaseRequest Original;
    public bool IsOriginal=>ReferenceEquals(Original,this);
    public ExternalOriginalVirtualIoTransferReceipt Transfer {get;}
}
public sealed class ExternalOriginalVirtualIoSnapshotRelease
{
    internal ExternalOriginalVirtualIoSnapshotRelease(ExternalOriginalVirtualIoSnapshotReleaseRequest request)
    {Request=request;Original=this;}
    private readonly ExternalOriginalVirtualIoSnapshotRelease Original;
    public bool IsOriginal=>ReferenceEquals(Original,this);
    public ExternalOriginalVirtualIoSnapshotReleaseRequest Request {get;}
    public bool NoFurtherSnapshotRead=>true;
}
public interface IHybridCpuOriginalVirtualIoSnapshotReleaseRuntimeV1
{
    ExternalOriginalVirtualIoSnapshotRelease? ReleaseOriginalVirtualIoSnapshot(ExternalOriginalVirtualIoSnapshotReleaseRequest request);
    ExternalOriginalVirtualIoSnapshotRelease? ResolveOriginalVirtualIoSnapshotRelease(ExternalOriginalVirtualIoSnapshotReleaseRequest request);
}
