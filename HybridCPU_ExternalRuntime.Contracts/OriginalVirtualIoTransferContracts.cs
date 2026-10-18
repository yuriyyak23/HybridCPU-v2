using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("HybridCPU_ExternalRuntime")]
namespace HybridCPU.ExternalRuntime.Contracts;

/// <summary>Original bounded software transfer, not permission or closure authority.</summary>
public sealed class ExternalOriginalVirtualIoTransferRequest
{
    private byte[]? input;
    public ExternalOriginalVirtualIoTransferRequest(ExternalOriginalVirtualIoRequest admission, ExternalOperationIdentity operation,
        ulong offset, bool write, int length, byte[]? data = null)
    {
        ArgumentNullException.ThrowIfNull(admission);
        if (length is <= 0 or > 4096 || write && (data is null || data.Length != length) || !write && data is not null)
            throw new ArgumentException("Transfer requires a bounded exact input or read length.");
        Admission = admission; Operation = operation; Offset = offset; Write = write; Length = length;
        input = write ? data!.ToArray() : null;
    }
    public ExternalOriginalVirtualIoRequest Admission { get; }
    public ExternalOperationIdentity Operation { get; }
    public ulong Offset { get; }
    public bool Write { get; }
    public int Length { get; }
    internal ReadOnlySpan<byte> Input => input;
    internal void ReleaseInput() { if(input is { } bytes) Array.Clear(bytes); input = null; }
}
public sealed class ExternalOriginalVirtualIoTransferReceipt
{
    private readonly object snapshotGate = new();
    private byte[]? data;
    internal ExternalOriginalVirtualIoTransferReceipt(ExternalOriginalVirtualIoTransferRequest request, bool completed,
        bool noEffect, byte[]? snapshot, HybridCpuExternalContractVersion version, ulong generation)
    { Request = request; Completed = completed; NoEffect = noEffect; data = snapshot; ContractVersion = version; ManifestGeneration = generation; }
    public ExternalOriginalVirtualIoTransferRequest Request { get; }
    public bool Completed { get; }
    public bool NoEffect { get; }
    public bool NoFurtherTransferUse => true;
    public HybridCpuExternalContractVersion ContractVersion { get; }
    public ulong ManifestGeneration { get; }
    public byte[] CopyReadData()
    {
        lock(snapshotGate) return !Request.Write && Completed && data is { } bytes
            ? bytes.ToArray() : throw new InvalidOperationException("Original read snapshot unavailable.");
    }
    internal void ReleaseData() { lock(snapshotGate) { if(data is { } bytes) Array.Clear(bytes); data = null; } }
}
/// <summary>Request precedes bytes; same request lookup/replay never repeats a transfer.
/// Read snapshot private bytes equal Length and remain retained until exact IO closure.
/// The caller must account for this retention before invoking. Missing history is unknown.
/// Snapshot copies are data, never original receipt/closure authority.</summary>
public interface IHybridCpuOriginalVirtualIoTransferRuntimeV1
{
    ExternalOriginalVirtualIoTransferReceipt? TransferOriginalVirtualIo(ExternalOriginalVirtualIoTransferRequest request);
    ExternalOriginalVirtualIoTransferReceipt? ResolveOriginalVirtualIoTransfer(ExternalOriginalVirtualIoTransferRequest request);
}
