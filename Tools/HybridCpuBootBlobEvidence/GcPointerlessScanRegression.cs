using HybridCPU.ManagedRuntime;
using HybridCPU.Platform.Contracts;
using HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;

public static class GcPointerlessScanRegression
{
    public static void Verify(HybridCpuIseManagedImageLoadResultV1 load, ulong wad, Action<bool, string> check)
    {
        var types = load.TypeSystem!;
        var heap = load.Heap!;
        var bytes = types.Descriptors.Single(row => row.StableIdentity == "System.Byte[]");
        int offset = bytes.ArrayShape!.LengthOffsetBytes;
        var saved = new byte[4];
        check(heap.TryReadObjectBytes(wad, offset, saved), "read exact WAD array length");
        var arrays = new HybridCpuManagedArrayRuntimeV1(types, heap);
        var garbage = arrays.NewArray(types.TypeHandle(bytes.TypeId)!.Value, 512);
        check(garbage.IsSuccess, "allocate garbage for no-sweep negative controls");
        try
        {
            foreach (int invalid in new[] { -1, int.MaxValue })
            {
                check(heap.WriteObjectBytes(wad, offset, BitConverter.GetBytes(invalid)).IsSuccess,
                    "inject invalid scalar-array length in the fixture");
                var failed = load.Gc!.Collect(new([], [], load.ProcessRoots!, load.Strings),
                    HybridCpuManagedNonMovingGcOptionsV1.Qualification);
                check(!failed.IsSuccess && heap.ActiveAllocations().Any(row => row.ObjectAddress == garbage.ObjectReference),
                    "negative/oversize scalar-array bounds reject before sweep");
            }
        }
        finally
        {
            check(heap.WriteObjectBytes(wad, offset, saved).IsSuccess, "restore exact WAD length");
        }
        var childType = types.Descriptors.Single(row => row.StableIdentity == "DoomSharp.Core.Data.WadLump");
        var refsType = types.Descriptors.Single(row => row.StableIdentity == "DoomSharp.Core.Data.WadLump[]");
        var child = heap.Allocate(types.TypeHandle(childType.TypeId)!.Value);
        var refs = arrays.NewArray(types.TypeHandle(refsType.TypeId)!.Value, 1);
        check(child.IsSuccess && refs.IsSuccess && arrays.StoreReference(refs.ObjectReference, 0, child.ObjectReference).IsSuccess,
            "reference-array fixture contains a live child");
        var roots = load.ProcessRoots!.Concat(new[] { new HybridCpuManagedGcRootV1("reference-array-control",
            HybridCpuManagedGcRootSourceV1.Handle, refs.ObjectReference) }).ToArray();
        var result = load.Gc!.Collect(new([], [], roots, load.Strings), HybridCpuManagedNonMovingGcOptionsV1.Qualification);
        check(result.IsSuccess && result.ReachableObjects.Contains(wad) && result.ReachableObjects.Contains(child.ObjectReference) &&
            result.ReclaimedObjects.Contains(garbage.ObjectReference),
            "scalar WAD survives, reference-array payload is scanned, unrelated garbage is reclaimed");
    }
}
