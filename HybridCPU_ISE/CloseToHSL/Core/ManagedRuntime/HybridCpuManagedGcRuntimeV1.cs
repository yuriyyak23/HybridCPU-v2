using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace HybridCPU.ManagedRuntime;

public enum HybridCpuManagedGcRuntimeStatusV1 : byte
{
    Collected = 0,
    Disabled = 1,
    Unsupported = 2,
    InvalidMetadata = 3,
    InvalidState = 4,
    BudgetExhausted = 5
}

public enum HybridCpuManagedRootLocationKindV1 : byte
{
    Register = 0,
    Stack = 1
}

public sealed record HybridCpuManagedGcRuntimeBudgetsV1(
    int MaximumFrames,
    int MaximumSafepoints,
    int MaximumRoots,
    int MaximumObjects,
    int MaximumObjectReferences)
{
    public static HybridCpuManagedGcRuntimeBudgetsV1 Production { get; } =
        new(1024, 4096, 16384, 65536, 262144);
}

public sealed record HybridCpuManagedGcRuntimeOptionsV1(
    bool EnableMovingCollection,
    HybridCpuManagedGcRuntimeBudgetsV1 Budgets,
    string OptionsDigest)
{
    public static HybridCpuManagedGcRuntimeOptionsV1 Production { get; } =
        Create(false, HybridCpuManagedGcRuntimeBudgetsV1.Production);

    public static HybridCpuManagedGcRuntimeOptionsV1 Qualification { get; } =
        Create(true, HybridCpuManagedGcRuntimeBudgetsV1.Production);

    public static HybridCpuManagedGcRuntimeOptionsV1 Create(
        bool enableMovingCollection,
        HybridCpuManagedGcRuntimeBudgetsV1 budgets)
    {
        ArgumentNullException.ThrowIfNull(budgets);
        string digest = HybridCpuManagedGcRuntimeContractV1.Hash(string.Join('|',
            "hybridcpu.managed-gc-runtime-options/v1", enableMovingCollection,
            budgets.MaximumFrames, budgets.MaximumSafepoints, budgets.MaximumRoots,
            budgets.MaximumObjects, budgets.MaximumObjectReferences));
        return new(enableMovingCollection, budgets, digest);
    }
}

public sealed record HybridCpuManagedMethodRegistrationV1(
    string MethodIdentity,
    byte[] GcInfo,
    byte[] CodeManagerMetadata,
    string ManagedAbiDigest,
    string TargetPlatformDigest,
    string NativeAbiDigest,
    string RuntimePackRevision);

public sealed record HybridCpuManagedRuntimeFrameV1(
    string MethodIdentity,
    int InstructionPointer,
    IReadOnlyList<ulong> Registers,
    IReadOnlyDictionary<int, ulong> StackSlots);

public sealed record HybridCpuManagedThreadStateV1(IReadOnlyList<HybridCpuManagedRuntimeFrameV1> Frames);

public sealed record HybridCpuManagedHeapObjectV1(
    ulong Address,
    byte[] Payload,
    IReadOnlyList<ulong> References);

public sealed record HybridCpuManagedHeapV1(IReadOnlyList<HybridCpuManagedHeapObjectV1> Objects);

public sealed record HybridCpuManagedGcCollectionRequestV1(
    IReadOnlyList<HybridCpuManagedMethodRegistrationV1> Registrations,
    HybridCpuManagedThreadStateV1 Thread,
    HybridCpuManagedHeapV1 Heap);

public sealed record HybridCpuManagedGcCollectionResultV1(
    HybridCpuManagedGcRuntimeStatusV1 Status,
    string Reason,
    HybridCpuManagedThreadStateV1 Thread,
    HybridCpuManagedHeapV1 Heap,
    IReadOnlyDictionary<ulong, ulong> ForwardingAddresses,
    int RootCount,
    int ReclaimedObjectCount,
    string ResultDigest);

public sealed class HybridCpuManagedGcRuntimeContractV1
{
    public const string SchemaId = "hybridcpu.managed-gc-runtime/v1";
    public const string ManagedAbiDigest = "2ef28903d8b273f848283c0940899e0390183d8736980ad0c251b27e121704d9";
    public const string TargetPlatformDigest = "3b3714582295e0a0daa111841b2fe96aae610788ec7e27b222b7552dd5879431";
    public const string NativeAbiDigest = "3a4d4a4aebed5e5df40a3615b4e40c301181cb71bf281d958470fbee308317a4";
    public const string RuntimePackRevision = "hybridcpu.runtime-pack/managed-contract-v1.5";
    public const ulong FirstMovedAddress = 0x0000_0000_1000_0000UL;
    public const ulong ObjectAddressStride = 0x100UL;

    public static HybridCpuManagedGcRuntimeContractV1 Default { get; } = new();

    private HybridCpuManagedGcRuntimeContractV1()
    {
        ProductionOptionsDigest = HybridCpuManagedGcRuntimeOptionsV1.Production.OptionsDigest;
        QualificationOptionsDigest = HybridCpuManagedGcRuntimeOptionsV1.Qualification.OptionsDigest;
        ContractDigest = Hash(string.Join('|', SchemaId, ManagedAbiDigest, TargetPlatformDigest,
            NativeAbiDigest, RuntimePackRevision, ProductionOptionsDigest, QualificationOptionsDigest,
            "hcmg=1.0", "hcmm=1.0", "object-reference-only", "transactional", "publication-authority=false"));
    }

    public string ProductionOptionsDigest { get; }
    public string QualificationOptionsDigest { get; }
    public string ContractDigest { get; }

    internal static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}

/// <summary>
/// Runtime-owned, default-disabled moving collector for the qualified Phase 25 object-reference subset.
/// It consumes compiler metadata but grants no compiler authority over suspension, movement or root validity.
/// </summary>
public sealed class HybridCpuManagedGcRuntimeV1
{
    private const uint GcInfoMagic = 0x474d4348;
    private const uint CodeManagerMagic = 0x4d4d4348;

    public HybridCpuManagedGcCollectionResultV1 Collect(
        HybridCpuManagedGcCollectionRequestV1 request,
        HybridCpuManagedGcRuntimeOptionsV1? options = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        options ??= HybridCpuManagedGcRuntimeOptionsV1.Production;
        HybridCpuManagedThreadStateV1 originalThread = request.Thread ?? new(Array.Empty<HybridCpuManagedRuntimeFrameV1>());
        HybridCpuManagedHeapV1 originalHeap = request.Heap ?? new(Array.Empty<HybridCpuManagedHeapObjectV1>());
        if (!ValidOptions(options)) return Failure(HybridCpuManagedGcRuntimeStatusV1.InvalidState,
            "Managed GC runtime options do not bind valid deterministic budgets.", originalThread, originalHeap);
        if (!options.EnableMovingCollection) return Failure(HybridCpuManagedGcRuntimeStatusV1.Disabled,
            "Production moving collection is disabled.", originalThread, originalHeap);
        if (request.Registrations is null || request.Thread is null || request.Heap is null ||
            request.Thread.Frames is null || request.Heap.Objects is null)
            return Failure(HybridCpuManagedGcRuntimeStatusV1.InvalidState,
                "Registrations, thread frames and heap objects are required.", originalThread, originalHeap);
        if (request.Thread.Frames.Count > options.Budgets.MaximumFrames ||
            request.Heap.Objects.Count > options.Budgets.MaximumObjects ||
            request.Heap.Objects.Sum(static item => (long)(item?.References?.Count ?? 0)) >
                options.Budgets.MaximumObjectReferences)
            return Failure(HybridCpuManagedGcRuntimeStatusV1.BudgetExhausted,
                "Managed GC runtime deterministic budgets were exceeded.", originalThread, originalHeap);

        Dictionary<string, Registration> registrations = [];
        foreach (HybridCpuManagedMethodRegistrationV1 item in request.Registrations)
        {
            RegistrationParse parsed = ParseRegistration(item, options.Budgets);
            if (parsed.Status != HybridCpuManagedGcRuntimeStatusV1.Collected || parsed.Registration is null)
                return Failure(parsed.Status, parsed.Reason, originalThread, originalHeap);
            if (!registrations.TryAdd(item.MethodIdentity, parsed.Registration))
                return Failure(HybridCpuManagedGcRuntimeStatusV1.InvalidMetadata,
                    "Managed method registration identities must be unique.", originalThread, originalHeap);
        }

        var rootBindings = new List<RootBinding>();
        for (int frameIndex = 0; frameIndex < request.Thread.Frames.Count; frameIndex++)
        {
            HybridCpuManagedRuntimeFrameV1 frame = request.Thread.Frames[frameIndex];
            if (frame is null || string.IsNullOrWhiteSpace(frame.MethodIdentity) || frame.Registers is null ||
                frame.StackSlots is null || frame.Registers.Count != 64 ||
                !registrations.TryGetValue(frame.MethodIdentity, out Registration? registration))
                return Failure(HybridCpuManagedGcRuntimeStatusV1.InvalidState,
                    "Every frame requires 64 registers and one matching method registration.", originalThread, originalHeap);
            int relativeIp = checked(frame.InstructionPointer - registration.CodeStart);
            if (relativeIp < 0 || relativeIp >= registration.CodeSize ||
                !registration.Safepoints.TryGetValue(relativeIp, out RootLocation[]? locations))
                return Failure(HybridCpuManagedGcRuntimeStatusV1.InvalidState,
                    "A frame instruction pointer is not a registered safepoint.", originalThread, originalHeap);
            foreach (RootLocation location in locations)
            {
                ulong value;
                if (location.Kind == HybridCpuManagedRootLocationKindV1.Register)
                {
                    if (location.Index is < 0 or >= 64)
                        return Failure(HybridCpuManagedGcRuntimeStatusV1.InvalidMetadata,
                            "GC metadata names an invalid register.", originalThread, originalHeap);
                    value = frame.Registers[location.Index];
                }
                else if (!frame.StackSlots.TryGetValue(location.Index, out value))
                {
                    return Failure(HybridCpuManagedGcRuntimeStatusV1.InvalidState,
                        "A GC stack root location is absent from the runtime frame.", originalThread, originalHeap);
                }
                rootBindings.Add(new(frameIndex, location.Kind, location.Index, value));
            }
        }
        if (rootBindings.Count > options.Budgets.MaximumRoots)
            return Failure(HybridCpuManagedGcRuntimeStatusV1.BudgetExhausted,
                "The runtime root budget was exceeded.", originalThread, originalHeap);

        Dictionary<ulong, HybridCpuManagedHeapObjectV1> objects = [];
        foreach (HybridCpuManagedHeapObjectV1 item in request.Heap.Objects)
        {
            if (item is null || item.Address == 0 || item.Address % 8 != 0 || item.Payload is null ||
                item.References is null || !objects.TryAdd(item.Address, item))
                return Failure(HybridCpuManagedGcRuntimeStatusV1.InvalidState,
                    "Heap object addresses and payload/reference collections must be valid and unique.", originalThread, originalHeap);
        }
        var reachable = new SortedSet<ulong>();
        var pending = new Queue<ulong>(rootBindings.Select(static root => root.Value).Where(static value => value != 0));
        while (pending.Count != 0)
        {
            ulong address = pending.Dequeue();
            if (!reachable.Add(address)) continue;
            if (!objects.TryGetValue(address, out HybridCpuManagedHeapObjectV1? item))
                return Failure(HybridCpuManagedGcRuntimeStatusV1.InvalidState,
                    "A root or object field references an address outside the managed heap.", originalThread, originalHeap);
            foreach (ulong child in item.References.Where(static value => value != 0)) pending.Enqueue(child);
        }

        Dictionary<ulong, ulong> forwarding = reachable.Select((address, index) =>
                (address, moved: checked(HybridCpuManagedGcRuntimeContractV1.FirstMovedAddress +
                    (ulong)index * HybridCpuManagedGcRuntimeContractV1.ObjectAddressStride)))
            .ToDictionary(static item => item.address, static item => item.moved);
        HybridCpuManagedHeapObjectV1[] movedObjects = reachable.Select(address =>
        {
            HybridCpuManagedHeapObjectV1 item = objects[address];
            return new HybridCpuManagedHeapObjectV1(forwarding[address], item.Payload.ToArray(),
                item.References.Select(reference => reference == 0 ? 0 : forwarding[reference]).ToArray());
        }).ToArray();
        HybridCpuManagedRuntimeFrameV1[] movedFrames = request.Thread.Frames.Select(frame => new HybridCpuManagedRuntimeFrameV1(
            frame.MethodIdentity, frame.InstructionPointer, frame.Registers.ToArray(),
            frame.StackSlots.ToDictionary(static pair => pair.Key, static pair => pair.Value))).ToArray();
        foreach (RootBinding root in rootBindings)
        {
            ulong moved = root.Value == 0 ? 0 : forwarding[root.Value];
            HybridCpuManagedRuntimeFrameV1 frame = movedFrames[root.FrameIndex];
            if (root.Kind == HybridCpuManagedRootLocationKindV1.Register)
            {
                ulong[] registers = frame.Registers.ToArray();
                registers[root.Index] = moved;
                movedFrames[root.FrameIndex] = frame with { Registers = registers };
            }
            else
            {
                Dictionary<int, ulong> stack = frame.StackSlots.ToDictionary(static pair => pair.Key, static pair => pair.Value);
                stack[root.Index] = moved;
                movedFrames[root.FrameIndex] = frame with { StackSlots = stack };
            }
        }
        string digest = DigestResult(movedFrames, movedObjects, forwarding);
        return new(HybridCpuManagedGcRuntimeStatusV1.Collected,
            "Runtime stack walking and deterministic moving collection completed.",
            new(movedFrames), new(movedObjects), forwarding, rootBindings.Count,
            objects.Count - movedObjects.Length, digest);
    }

    private static RegistrationParse ParseRegistration(
        HybridCpuManagedMethodRegistrationV1 item,
        HybridCpuManagedGcRuntimeBudgetsV1 budgets)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.MethodIdentity) || item.GcInfo is null ||
            item.CodeManagerMetadata is null ||
            !string.Equals(item.ManagedAbiDigest, HybridCpuManagedGcRuntimeContractV1.ManagedAbiDigest, StringComparison.Ordinal) ||
            !string.Equals(item.TargetPlatformDigest, HybridCpuManagedGcRuntimeContractV1.TargetPlatformDigest, StringComparison.Ordinal) ||
            !string.Equals(item.NativeAbiDigest, HybridCpuManagedGcRuntimeContractV1.NativeAbiDigest, StringComparison.Ordinal) ||
            !string.Equals(item.RuntimePackRevision, HybridCpuManagedGcRuntimeContractV1.RuntimePackRevision, StringComparison.Ordinal))
            return RegistrationParse.Fail(HybridCpuManagedGcRuntimeStatusV1.Unsupported,
                "Managed ABI, target, native ABI or runtime-pack registration does not match the runtime.");
        if (item.CodeManagerMetadata.Length != 92 ||
            BinaryPrimitives.ReadUInt32LittleEndian(item.CodeManagerMetadata) != CodeManagerMagic ||
            BinaryPrimitives.ReadUInt16LittleEndian(item.CodeManagerMetadata.AsSpan(4)) != 1 ||
            BinaryPrimitives.ReadUInt16LittleEndian(item.CodeManagerMetadata.AsSpan(6)) != 0 ||
            BinaryPrimitives.ReadInt32LittleEndian(item.CodeManagerMetadata.AsSpan(8)) != 1)
            return RegistrationParse.Fail(HybridCpuManagedGcRuntimeStatusV1.InvalidMetadata,
                "Code-manager metadata must contain one HCMM 1.0 method record.");
        ReadOnlySpan<byte> record = item.CodeManagerMetadata.AsSpan(12, 80);
        if (!record[..8].SequenceEqual(SHA256.HashData(Encoding.UTF8.GetBytes(item.MethodIdentity)).AsSpan(0, 8)))
            return RegistrationParse.Fail(HybridCpuManagedGcRuntimeStatusV1.InvalidMetadata,
                "Code-manager method identity does not match the registration.");
        int codeStart = BinaryPrimitives.ReadInt32LittleEndian(record[8..]);
        int codeSize = BinaryPrimitives.ReadInt32LittleEndian(record[12..]);
        if (codeStart < 0 || codeSize <= 0 || record[48..80].ToArray().Any(static value => value != 0))
            return RegistrationParse.Fail(HybridCpuManagedGcRuntimeStatusV1.Unsupported,
                "Code range is invalid or unwind metadata is not supported by this GC runtime gate.");
        string gcDigest = HybridCpuManagedGcRuntimeContractV1.Hash(
            $"gc-info|{HybridCpuManagedGcRuntimeContractV1.ManagedAbiDigest}|{Convert.ToHexString(item.GcInfo)}");
        if (!record[16..48].SequenceEqual(Convert.FromHexString(gcDigest)))
            return RegistrationParse.Fail(HybridCpuManagedGcRuntimeStatusV1.InvalidMetadata,
                "Code-manager GC-info digest does not match the registered HCMG bytes.");
        SafepointParse safepoints = ParseSafepoints(item.GcInfo, budgets);
        return safepoints.Status == HybridCpuManagedGcRuntimeStatusV1.Collected
            ? new(safepoints.Status, safepoints.Reason,
                new(codeStart, codeSize, safepoints.Safepoints!))
            : RegistrationParse.Fail(safepoints.Status, safepoints.Reason);
    }

    private static SafepointParse ParseSafepoints(byte[] bytes, HybridCpuManagedGcRuntimeBudgetsV1 budgets)
    {
        if (bytes.Length < 12 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != GcInfoMagic ||
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4)) != 1 ||
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6)) != 0)
            return SafepointParse.Fail("GC metadata is not HCMG 1.0.");
        int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8));
        if (count < 0 || count > budgets.MaximumSafepoints)
            return new(HybridCpuManagedGcRuntimeStatusV1.BudgetExhausted,
                "GC safepoint count exceeds the runtime budget.", null);
        var result = new Dictionary<int, RootLocation[]>();
        int offset = 12;
        int roots = 0;
        for (int index = 0; index < count; index++)
        {
            if (bytes.Length - offset < 8) return SafepointParse.Fail("GC safepoint header is truncated.");
            int codeOffset = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
            byte category = bytes[offset + 4];
            int referenceCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 6));
            offset += 8;
            roots = checked(roots + referenceCount);
            if (codeOffset < 0 || category > 1 || roots > budgets.MaximumRoots ||
                bytes.Length - offset < referenceCount * 16)
                return SafepointParse.Fail("GC safepoint or root collection is malformed.");
            var locations = new RootLocation[referenceCount];
            for (int reference = 0; reference < referenceCount; reference++)
            {
                byte referenceKind = bytes[offset + 8];
                byte locationKind = bytes[offset + 9];
                int register = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset + 10));
                int stack = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + 12));
                if (referenceKind != 0 || locationKind > 1 ||
                    locationKind == 0 && (register is < 0 or >= 64 || stack != -1) ||
                    locationKind == 1 && (register != -1 || stack < 0 || stack % 8 != 0))
                    return SafepointParse.Fail("GC root location is unsupported or malformed.");
                locations[reference] = locationKind == 0
                    ? new(HybridCpuManagedRootLocationKindV1.Register, register)
                    : new(HybridCpuManagedRootLocationKindV1.Stack, stack);
                offset += 16;
            }
            if (!result.TryAdd(codeOffset, locations))
                return SafepointParse.Fail("GC safepoint offsets must be unique.");
        }
        return offset == bytes.Length
            ? new(HybridCpuManagedGcRuntimeStatusV1.Collected, "valid", result)
            : SafepointParse.Fail("GC metadata has trailing bytes.");
    }

    private static bool ValidOptions(HybridCpuManagedGcRuntimeOptionsV1 options)
    {
        HybridCpuManagedGcRuntimeBudgetsV1 budgets = options.Budgets;
        if (budgets is null || budgets.MaximumFrames <= 0 || budgets.MaximumSafepoints <= 0 ||
            budgets.MaximumRoots <= 0 || budgets.MaximumObjects <= 0 || budgets.MaximumObjectReferences <= 0)
            return false;
        return string.Equals(options.OptionsDigest,
            HybridCpuManagedGcRuntimeOptionsV1.Create(options.EnableMovingCollection, budgets).OptionsDigest,
            StringComparison.Ordinal);
    }

    private static string DigestResult(
        IReadOnlyList<HybridCpuManagedRuntimeFrameV1> frames,
        IReadOnlyList<HybridCpuManagedHeapObjectV1> objects,
        IReadOnlyDictionary<ulong, ulong> forwarding) =>
        HybridCpuManagedGcRuntimeContractV1.Hash(string.Join('|',
            HybridCpuManagedGcRuntimeContractV1.Default.ContractDigest,
            string.Join(';', frames.Select(frame => $"{frame.MethodIdentity}:{frame.InstructionPointer}:" +
                string.Join(',', frame.Registers) + ":" +
                string.Join(',', frame.StackSlots.OrderBy(static pair => pair.Key).Select(static pair => $"{pair.Key}={pair.Value}")))),
            string.Join(';', objects.OrderBy(static item => item.Address).Select(item =>
                $"{item.Address}:{Convert.ToHexString(item.Payload)}:{string.Join(',', item.References)}")),
            string.Join(';', forwarding.OrderBy(static pair => pair.Key).Select(static pair => $"{pair.Key}>{pair.Value}"))));

    private static HybridCpuManagedGcCollectionResultV1 Failure(
        HybridCpuManagedGcRuntimeStatusV1 status,
        string reason,
        HybridCpuManagedThreadStateV1 thread,
        HybridCpuManagedHeapV1 heap) => new(status, reason, thread, heap,
            new Dictionary<ulong, ulong>(), 0, 0,
            HybridCpuManagedGcRuntimeContractV1.Hash($"failure|{status}|{reason}"));

    private sealed record RootLocation(HybridCpuManagedRootLocationKindV1 Kind, int Index);
    private sealed record RootBinding(int FrameIndex, HybridCpuManagedRootLocationKindV1 Kind, int Index, ulong Value);
    private sealed record Registration(int CodeStart, int CodeSize, IReadOnlyDictionary<int, RootLocation[]> Safepoints);
    private sealed record RegistrationParse(HybridCpuManagedGcRuntimeStatusV1 Status, string Reason, Registration? Registration)
    {
        public static RegistrationParse Fail(HybridCpuManagedGcRuntimeStatusV1 status, string reason) =>
            new(status, reason, null);
    }
    private sealed record SafepointParse(
        HybridCpuManagedGcRuntimeStatusV1 Status,
        string Reason,
        IReadOnlyDictionary<int, RootLocation[]>? Safepoints)
    {
        public static SafepointParse Fail(string reason) =>
            new(HybridCpuManagedGcRuntimeStatusV1.InvalidMetadata, reason, null);
    }
}
