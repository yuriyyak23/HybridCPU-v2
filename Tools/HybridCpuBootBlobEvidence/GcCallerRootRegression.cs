using System.Buffers.Binary;
using HybridCPU.ManagedRuntime;
using HybridCPU.Platform.Contracts;
using HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;

// Uses the failing Doom image's exact maps. This is a component reproduction,
// not a CPU snapshot or proof that a native caller walker has been implemented.
public static class GcCallerRootRegression
{
    public static void Verify(HybridCpuIseManagedImageLoadResultV1 load, ulong wad, Action<bool, string> check)
    {
        var maps = load.StackMaps!;
        var caller = maps.Single(row => row.MethodIdentity.Contains("WadFile..ctorinstance", StringComparison.Ordinal));
        var callee = maps.Single(row => row.MethodIdentity.Contains("WadFile.ReadInt32(", StringComparison.Ordinal));
        int Site(HybridCpuManagedStackMapRegistrationV1 map, int offset) =>
            checked(BinaryPrimitives.ReadInt32LittleEndian(map.CodeManagerMetadata.AsSpan(20)) + offset);
        var descriptor = load.TypeSystem!.Descriptors.Single(row => row.StableIdentity == "DoomSharp.Core.Data.WadFile");
        ulong handle = load.TypeSystem.TypeHandle(descriptor.TypeId)!.Value;
        ulong NewReceiver()
        {
            var allocation = load.Heap!.Allocate(handle);
            check(allocation.IsSuccess, "allocate exact image WadFile descriptor");
            return allocation.ObjectReference;
        }
        var registers = new ulong[64];
        registers[10] = registers[18] = wad;
        ulong first = NewReceiver();
        var omitted = load.Gc!.CollectRetiredSafepoint(maps, Site(callee, 0x1100), registers,
            0x200ffee0, (_, _) => null, load.ProcessRoots!, load.Strings);
        check(omitted.IsSuccess && omitted.Collection!.ReclaimedObjects.Contains(first),
            "negative reproduction: current-frame-only retired collection reclaims suspended WadFile receiver");
        var callerRegisters = new ulong[64];
        callerRegisters[10] = callerRegisters[20] = wad;
        callerRegisters[18] = first;
        var rejected = load.Gc.CollectRetiredSafepoint(maps, Site(caller, 0x7900), callerRegisters,
            0x200fff00, (_, _) => null, load.ProcessRoots!, load.Strings);
        check(rejected.Status == HybridCpuManagedRetiredSafepointStatusV1.Rejected &&
            rejected.Reason.Contains("non-active heap address", StringComparison.Ordinal),
            "caller resumption reproduces CPU40 non-active root refusal");
        ulong second = NewReceiver();
        callerRegisters[18] = second;
        HybridCpuManagedGcFrameSnapshotV1 Frame(string method, int pc, ulong[] bank) =>
            new(method, pc, bank, new Dictionary<int, ulong>());
        var retained = load.Gc.Collect(new(maps,
            [Frame(callee.MethodIdentity, Site(callee, 0x1100), registers),
             Frame(caller.MethodIdentity, Site(caller, 0x7100), callerRegisters)],
            load.ProcessRoots!, load.Strings), HybridCpuManagedNonMovingGcOptionsV1.Qualification);
        check(retained.IsSuccess && retained.ReachableObjects.Contains(second),
            "positive control: exact suspended caller map retains receiver across callee collection");
        var resumed = load.Gc.CollectRetiredSafepoint(maps, Site(caller, 0x7900), callerRegisters,
            0x200fff00, (_, _) => null, load.ProcessRoots!, load.Strings);
        check(resumed.IsSuccess, "caller resumption succeeds when its root was enumerated");
        const ulong imageBase = 0x10000, callerSp = 0x200fff00, top = callerSp + 64, rootLink = 0x10004;
        var words = new Dictionary<ulong, ulong> { [callerSp] = rootLink };
        byte[]? Read(ulong address, int count) => count == 8 && words.TryGetValue(address, out ulong value)
            ? BitConverter.GetBytes(value) : null;
        var session = new HybridCpuManagedGcStackBoundaryV1(imageBase, top, rootLink);
        callerRegisters[2] = callerSp;
        callerRegisters[1] = imageBase + (ulong)Site(caller, 0x7100) + 4;
        ulong volatileRoot = NewReceiver();
        callerRegisters[10] = volatileRoot;
        var entered = load.Gc.CollectRetiredSafepoint(maps, Site(caller, 0x7100), callerRegisters,
            callerSp, Read, load.ProcessRoots!, load.Strings, session);
        check(entered.IsSuccess, "native root-frame boundary captures the exact suspended caller");
        ulong calleeSp = callerSp - 32;
        words[calleeSp] = callerRegisters[1];
        words[calleeSp + 8] = callerRegisters[18];
        words[calleeSp + 16] = callerRegisters[20];
        words[calleeSp + 24] = callerRegisters[24];
        registers[2] = calleeSp;
        var native = load.Gc.CollectRetiredSafepoint(maps, Site(callee, 0x1100), registers,
            calleeSp, Read, load.ProcessRoots!, load.Strings, session);
        check(native.IsSuccess && native.Collection!.ReachableObjects.Contains(second) &&
            native.Collection.ReachableObjects.Contains(volatileRoot),
            "HCW2 caller walk retains saved and caller-volatile roots using the exact suspended snapshot");
        ulong garbage = NewReceiver();
        words[calleeSp] += 4;
        var malformed = load.Gc.CollectRetiredSafepoint(maps, Site(callee, 0x1100), registers,
            calleeSp, Read, load.ProcessRoots!, load.Strings, session);
        check(malformed.Status == HybridCpuManagedRetiredSafepointStatusV1.Rejected &&
            load.Heap!.ActiveAllocations().Any(row => row.ObjectAddress == garbage),
            "malformed link is rejected before sweep; arbitrary PCs are not rounded");
        words[calleeSp] -= 4;
        var missing = load.Gc.CollectRetiredSafepoint(maps, Site(callee, 0x1100), registers,
            calleeSp, Read, load.ProcessRoots!, load.Strings,
            new HybridCpuManagedGcStackBoundaryV1(imageBase, top, rootLink));
        check(missing.Status == HybridCpuManagedRetiredSafepointStatusV1.Rejected,
            "missing suspended snapshot fails closed instead of guessing volatile roots");
        words[calleeSp + 8] = wad;
        var forged = load.Gc.CollectRetiredSafepoint(maps, Site(callee, 0x1100), registers,
            calleeSp, Read, load.ProcessRoots!, load.Strings, session);
        check(forged.Status == HybridCpuManagedRetiredSafepointStatusV1.Rejected,
            "saved-register mismatch rejects a stale or forged caller snapshot");
        words[calleeSp + 8] = second;
        callerRegisters[1] = imageBase + (ulong)Site(caller, 0x7900) + 4;
        var nativeResumed = load.Gc.CollectRetiredSafepoint(maps, Site(caller, 0x7900), callerRegisters,
            callerSp, Read, load.ProcessRoots!, load.Strings, session);
        check(nativeResumed.IsSuccess, "native caller resumes and expires the completed callee snapshot");
    }
}
