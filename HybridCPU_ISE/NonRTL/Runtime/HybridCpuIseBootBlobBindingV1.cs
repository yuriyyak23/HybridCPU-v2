using System.Buffers.Binary;
using HybridCPU.ManagedRuntime;
using HybridCPU.Platform.Contracts;
using HybridCPU.RuntimeKernel;
using HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;

namespace HybridCPU_ISE.NonRTL.Runtime;

/// <summary>Single-use, pre-CPU loader binding. Only fully copied, rooted guest arrays are published.</summary>
public sealed class HybridCpuIseBootBlobBindingV1 : IHybridCpuManagedBootBlobProviderV1
{
    private HybridCpuManagedBootBlobRegistryV1? _registry;
    private bool _attempted;

    public HybridCpuManagedBootBlobProviderResultV1 Resolve(int blobId) =>
        _registry?.Resolve(blobId) ?? new(false, 0, 0, string.Empty, "Boot blob has not been materialized and rooted.");

    public bool TryBind(HybridCpuIseManagedImageLoadResultV1 load, int blobId,
        ReadOnlySpan<byte> bytes, out HybridCpuIseManagedImageLoadResultV1 bound, out string reason)
    {
        bound = load;
        reason = "Boot blob binding requires a fresh successful loader, a valid id and bounded nonempty bytes.";
        if (_attempted) return false;
        _attempted = true;
        if (!load.IsSuccess || load.TypeSystem is not { } types || load.Heap is not { } heap ||
            load.Gc is null || load.ProcessRoots is { Count: > 0 } ||
            blobId is <= 0 or > HybridCpuBootBlobServiceContractV1.MaximumBlobId ||
            bytes.IsEmpty || bytes.Length > HybridCpuBootBlobServiceContractV1.MaximumBlobBytes) return false;
        var matches = types.Descriptors.Where(static type => type.StableIdentity == "System.Byte[]").ToArray();
        reason = "Boot blob requires one exact image-owned System.Byte[] descriptor.";
        if (matches.Length != 1 || matches[0] is not { Kind: HybridCpuManagedTypeKindV1.SzArray,
            ArrayShape: { IsSzArray: true, ElementStorageKind: HybridCpuManagedStorageKindV1.Primitive,
                ElementSizeBytes: 1, ElementAlignmentBytes: 1 } shape } type ||
            types.TypeHandle(type.TypeId) is not ulong handle) return false;
        int size;
        try { size = Math.Max(type.InstanceSizeBytes, checked(shape.DataOffsetBytes + bytes.Length)); }
        catch (OverflowException) { reason = "Boot blob array extent overflows."; return false; }
        var allocation = heap.TryAllocateVariable(handle, size);
        if (!allocation.IsSuccess) { reason = allocation.Reason; return false; }
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        if (!heap.WriteObjectBytes(allocation.ObjectReference, shape.LengthOffsetBytes, length).IsSuccess ||
            !heap.WriteObjectBytes(allocation.ObjectReference, shape.DataOffsetBytes, bytes).IsSuccess)
        { reason = "Boot blob heap copy failed; no reference was published."; return false; }
        var roots = new HybridCpuManagedGcRootRegistryV1();
        var registry = new HybridCpuManagedBootBlobRegistryV1(types, heap, roots);
        if (!registry.Register(blobId, allocation.ObjectReference))
        { reason = "Boot blob process root registration failed."; return false; }
        bound = load with { ProcessRoots = Array.AsReadOnly(roots.Snapshot().ToArray()) };
        _registry = registry;
        reason = string.Empty;
        return true;
    }
}
