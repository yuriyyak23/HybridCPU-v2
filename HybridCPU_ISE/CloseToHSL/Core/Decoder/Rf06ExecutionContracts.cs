using System;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Arch.Generated;
using YAKSys_Hybrid_CPU.Core.Pipeline.Scheduling;

namespace YAKSys_Hybrid_CPU.Core.Decoder;

/// <summary>
/// Immutable typed identity for a generated materializer binding.
/// </summary>
public readonly record struct MaterializerId
{
    public MaterializerId(string value)
    {
        Value = RequireIdentity(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;

    private static string RequireIdentity(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }
}

/// <summary>
/// Immutable typed identity for a runtime execution provider binding.
/// </summary>
public readonly record struct RuntimeExecutionProviderId
{
    public RuntimeExecutionProviderId(string value)
    {
        Value = RequireIdentity(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;

    private static string RequireIdentity(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }
}

/// <summary>
/// Immutable typed identity for a latency/completion model. RF-06 does not own
/// observed completion or mutable latency state.
/// </summary>
public readonly record struct LatencyModelId
{
    public LatencyModelId(string value)
    {
        Value = RequireIdentity(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;

    private static string RequireIdentity(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }
}

/// <summary>
/// The one generated static binding handed from decode to materialization.
/// It contains no provider instance and no live execution state.
/// </summary>
public sealed record GeneratedStaticBinding(
    uint Opcode,
    MaterializerId MaterializerId,
    RuntimeExecutionProviderId RuntimeExecutionProviderId,
    LatencyModelId LatencyModelId,
    string CatalogVersion,
    string CatalogSha256,
    string DescriptorFingerprint)
{
    public static GeneratedStaticBinding FromDescriptor(in GeneratedIsaDescriptor descriptor)
    {
        string catalogVersion = GeneratedIsaCatalog.CatalogVersion;
        string catalogSha256 = GeneratedIsaCatalog.CatalogSha256;
        string descriptorFingerprint = ComputeDescriptorFingerprint(in descriptor);

        return new GeneratedStaticBinding(
            descriptor.Opcode,
            new MaterializerId(descriptor.MaterializerId),
            new RuntimeExecutionProviderId(descriptor.ProviderId),
            new LatencyModelId(descriptor.LatencyModelId),
            catalogVersion,
            catalogSha256,
            descriptorFingerprint);
    }

    public static bool TryFromOpcode(uint opcode, out GeneratedStaticBinding binding)
    {
        if (GeneratedIsaCatalog.TryGetDescriptor(opcode, out GeneratedIsaDescriptor descriptor))
        {
            binding = FromDescriptor(in descriptor);
            return true;
        }

        binding = default!;
        return false;
    }

    private static string ComputeDescriptorFingerprint(in GeneratedIsaDescriptor descriptor)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(descriptor);
        return Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }
}

/// <summary>Frozen physical placement capability. It is not a selected lane.</summary>
public readonly record struct ExecutionPlacement
{
    private ExecutionPlacement(SlotClass requiredSlotClass, SlotPinningKind pinningKind, byte pinnedLaneId, ulong domainTag)
    {
        RequiredSlotClass = requiredSlotClass;
        PinningKind = pinningKind;
        PinnedLaneId = pinnedLaneId;
        DomainTag = domainTag;
    }

    public SlotClass RequiredSlotClass { get; }
    public SlotPinningKind PinningKind { get; }
    public byte PinnedLaneId { get; }
    public ulong DomainTag { get; }

    /// <summary>
    /// Constructs a valid hard-pinned placement from the checked physical-lane
    /// representation. The retained placement carrier remains a raw byte.
    /// </summary>
    public static ExecutionPlacement CreateHardPinned(
        SlotClass requiredSlotClass,
        LaneId laneId,
        ulong domainTag = 0) => new(
            requiredSlotClass,
            SlotPinningKind.HardPinned,
            laneId.ToRawValue(),
            domainTag);

    /// <summary>
    /// Projects a hard-pinned raw placement to a checked physical lane. A
    /// flexible placement has no pinned lane; this does not decide placement
    /// legality or scheduler admission.
    /// </summary>
    public bool TryGetHardPinnedLaneId(out LaneId laneId)
    {
        if (PinningKind == SlotPinningKind.HardPinned &&
            LaneId.TryCreate(PinnedLaneId, out laneId))
        {
            return true;
        }

        laneId = default;
        return false;
    }

    public static ExecutionPlacement Create(
        SlotClass requiredSlotClass,
        SlotPinningKind pinningKind,
        byte pinnedLaneId = 0,
        ulong domainTag = 0)
    {
        if (pinningKind == SlotPinningKind.HardPinned && pinnedLaneId >= BundleMetadata.BundleSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pinnedLaneId), "A hard-pinned lane must be in the physical eight-lane bundle.");
        }

        return new ExecutionPlacement(
            requiredSlotClass,
            pinningKind,
            pinningKind == SlotPinningKind.HardPinned ? pinnedLaneId : (byte)0,
            domainTag);
    }
}

public enum MemoryCapabilityKind : byte
{
    NoMemory = 0,
    Load = 1,
    Store = 2,
    Atomic = 3,
}

/// <summary>
/// Typed identity of a scheduler-visible memory bank. The current hardware
/// contract exposes sixteen banks; unresolved legacy values (for example -1)
/// are deliberately not representable here.
/// </summary>
public readonly record struct MemoryBankId
{
    public const int BankCount = 16;

    public MemoryBankId(int value)
    {
        if ((uint)value >= BankCount)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value,
                $"A memory bank identity must be in the range [0, {BankCount - 1}].");
        }

        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString();
}

/// <summary>
/// Static memory direction carried by a capability. This is not a request
/// state or a completion state; it describes only the access contract.
/// </summary>
[Flags]
public enum MemoryAccessDirection : byte
{
    None = 0,
    Read = 1,
    Write = 2,
    ReadWrite = Read | Write,
}

public readonly record struct FrozenMemoryRange
{
    public FrozenMemoryRange(ulong address, ulong length)
    {
        if (length == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "A frozen memory range must have non-zero length.");
        }

        if (length - 1 > ulong.MaxValue - address)
        {
            throw new ArgumentOutOfRangeException(nameof(length),
                "A frozen memory range must remain within the address space.");
        }

        Address = address;
        Length = length;
    }

    public ulong Address { get; }
    public ulong Length { get; }
    public ulong LastAddress => Address + Length - 1;
}

/// <summary>
/// Coherent memory capability. Mutable timing, MSHR and allocation state are
/// deliberately absent.
/// </summary>
public sealed record MemoryCapability
{
    private MemoryCapability(
        MemoryCapabilityKind kind,
        MemoryAccessDirection direction,
        MemoryBankId? bank,
        ImmutableArray<FrozenMemoryRange> footprint)
    {
        Kind = kind;
        Direction = direction;
        Bank = bank;
        Footprint = footprint;
    }

    public MemoryCapabilityKind Kind { get; }
    public MemoryAccessDirection Direction { get; }
    public MemoryBankId? Bank { get; }
    public MemoryBankId? BankId => Bank;
    public ImmutableArray<FrozenMemoryRange> Footprint { get; }

    public static MemoryCapability None { get; } = new(
        MemoryCapabilityKind.NoMemory,
        MemoryAccessDirection.None,
        bank: null,
        ImmutableArray<FrozenMemoryRange>.Empty);

    public static MemoryCapability Create(
        MemoryCapabilityKind kind,
        IEnumerable<FrozenMemoryRange>? footprint = null,
        MemoryBankId? bank = null,
        MemoryAccessDirection? direction = null)
    {
        MemoryAccessDirection expectedDirection = kind switch
        {
            MemoryCapabilityKind.NoMemory => MemoryAccessDirection.None,
            MemoryCapabilityKind.Load => MemoryAccessDirection.Read,
            MemoryCapabilityKind.Store => MemoryAccessDirection.Write,
            MemoryCapabilityKind.Atomic => MemoryAccessDirection.ReadWrite,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown memory capability kind."),
        };

        if (kind == MemoryCapabilityKind.NoMemory && footprint is not null && footprint.Any())
        {
            throw new ArgumentException("NoMemory cannot carry a memory footprint.", nameof(footprint));
        }

        if (kind == MemoryCapabilityKind.NoMemory && bank.HasValue)
        {
            throw new ArgumentException("NoMemory cannot carry a memory bank identity.", nameof(bank));
        }

        if (kind == MemoryCapabilityKind.NoMemory && direction.HasValue && direction.Value != MemoryAccessDirection.None)
        {
            throw new ArgumentException("NoMemory cannot carry a read or write direction.", nameof(direction));
        }

        if (kind != MemoryCapabilityKind.NoMemory && footprint is null)
        {
            throw new ArgumentNullException(nameof(footprint), "A memory capability must declare its frozen footprint.");
        }

        if (kind != MemoryCapabilityKind.NoMemory && !bank.HasValue)
        {
            throw new ArgumentNullException(nameof(bank), "A memory capability must declare its typed bank identity.");
        }

        if (direction.HasValue && direction.Value != expectedDirection)
        {
            throw new ArgumentException(
                $"Memory capability kind {kind} requires direction {expectedDirection}.",
                nameof(direction));
        }

        ImmutableArray<FrozenMemoryRange> frozen = footprint is null
            ? ImmutableArray<FrozenMemoryRange>.Empty
            : NormalizeFootprint(footprint);
        if (kind != MemoryCapabilityKind.NoMemory && frozen.IsDefaultOrEmpty)
        {
            throw new ArgumentException("A memory capability must carry at least one frozen range.", nameof(footprint));
        }

        return new MemoryCapability(kind, expectedDirection, bank, frozen);
    }

    private static ImmutableArray<FrozenMemoryRange> NormalizeFootprint(
        IEnumerable<FrozenMemoryRange> footprint)
    {
        FrozenMemoryRange[] ordered = footprint
            .OrderBy(range => range.Address)
            .ThenBy(range => range.Length)
            .ToArray();

        for (int index = 1; index < ordered.Length; index++)
        {
            FrozenMemoryRange previous = ordered[index - 1];
            FrozenMemoryRange current = ordered[index];
            if (current.Address <= previous.LastAddress)
            {
                throw new ArgumentException(
                    "A frozen memory capability footprint cannot contain overlapping ranges.",
                    nameof(footprint));
            }
        }

        return ImmutableArray.Create(ordered);
    }
}

/// <summary>
/// Immutable companion for a canonical unresolved scalar-load shape. This is
/// deliberately distinct from <see cref="MemoryCapability"/>: it has no
/// effective address, bank, footprint or live request/completion state.
/// </summary>
public sealed class StaticMemoryAccessPlan
{
    private StaticMemoryAccessPlan(CanonicalScalarLoadAddressPlan scalarLoadPlan)
    {
        ScalarLoadPlan = scalarLoadPlan;
    }

    public CanonicalScalarLoadAddressPlan ScalarLoadPlan { get; }
    public GeneratedStaticBinding GeneratedBinding => ScalarLoadPlan.GeneratedBinding;
    public MemoryAccessDirection Direction => MemoryAccessDirection.Read;

    public static StaticMemoryAccessPlan UnresolvedScalarLoad(
        CanonicalScalarLoadAddressPlan scalarLoadPlan)
    {
        ArgumentNullException.ThrowIfNull(scalarLoadPlan);
        return new StaticMemoryAccessPlan(scalarLoadPlan);
    }
}

/// <summary>Resolved provider identity carried by the immutable contract.</summary>
public sealed record RuntimeExecutionProviderBinding
{
    public RuntimeExecutionProviderBinding(RuntimeExecutionProviderId id, string payloadSchema)
    {
        Id = id;
        PayloadSchema = RequireSchema(payloadSchema);
    }

    public RuntimeExecutionProviderId Id { get; }
    public string PayloadSchema { get; }

    private static string RequireSchema(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }
}

/// <summary>
/// Immutable materialization/capability envelope. RF-07 owns all live outcome
/// and completion semantics, so this type intentionally has no mutable runtime state.
/// </summary>
public sealed class ExecutionContract
{
    private ExecutionContract(
        GeneratedStaticBinding generatedBinding,
        RuntimeExecutionProviderBinding runtimeProvider,
        InstructionClass instructionClass,
        SerializationClass serializationClass,
        ExecutionPlacement placement,
        string staticEffectContract,
        MemoryCapability memory,
        StaticMemoryAccessPlan? staticMemoryPlan,
        ImmutableArray<int> readRegisters,
        ImmutableArray<int> writeRegisters,
        ResourceBitset resourceMask,
        bool isStealable,
        bool isRetireVisible,
        bool isAssist)
    {
        GeneratedBinding = generatedBinding;
        RuntimeProvider = runtimeProvider;
        InstructionClass = instructionClass;
        SerializationClass = serializationClass;
        Placement = placement;
        StaticEffectContract = staticEffectContract;
        Memory = memory;
        StaticMemoryPlan = staticMemoryPlan;
        ReadRegisters = readRegisters;
        WriteRegisters = writeRegisters;
        ResourceMask = resourceMask;
        IsStealable = isStealable;
        IsRetireVisible = isRetireVisible;
        IsAssist = isAssist;
    }

    public GeneratedStaticBinding GeneratedBinding { get; }
    public RuntimeExecutionProviderBinding RuntimeProvider { get; }
    public InstructionClass InstructionClass { get; }
    public SerializationClass SerializationClass { get; }
    public ExecutionPlacement Placement { get; }
    public string StaticEffectContract { get; }
    public MemoryCapability Memory { get; }
    public StaticMemoryAccessPlan? StaticMemoryPlan { get; }
    public ImmutableArray<int> ReadRegisters { get; }
    public ImmutableArray<int> WriteRegisters { get; }
    public ResourceBitset ResourceMask { get; }
    public bool IsStealable { get; }
    public bool IsRetireVisible { get; }
    public bool IsAssist { get; }

    public static ExecutionContract Create(
        GeneratedStaticBinding generatedBinding,
        RuntimeExecutionProviderBinding runtimeProvider,
        InstructionClass instructionClass,
        SerializationClass serializationClass,
        ExecutionPlacement placement,
        string staticEffectContract,
        MemoryCapability memory,
        IEnumerable<int>? readRegisters = null,
        IEnumerable<int>? writeRegisters = null,
        ResourceBitset resourceMask = default,
        bool isStealable = true,
        bool isRetireVisible = true,
        bool isAssist = false,
        StaticMemoryAccessPlan? staticMemoryPlan = null)
    {
        ArgumentNullException.ThrowIfNull(generatedBinding);
        ArgumentNullException.ThrowIfNull(runtimeProvider);
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentException.ThrowIfNullOrWhiteSpace(staticEffectContract);
        if (runtimeProvider.Id != generatedBinding.RuntimeExecutionProviderId)
        {
            throw new ArgumentException("Runtime provider binding does not match the generated descriptor binding.", nameof(runtimeProvider));
        }

        if (isAssist && isRetireVisible)
        {
            throw new ArgumentException("An assist capability cannot be retire-visible.", nameof(isRetireVisible));
        }

        if (staticMemoryPlan is not null)
        {
            if (memory.Kind != MemoryCapabilityKind.NoMemory)
            {
                throw new ArgumentException(
                    "An unresolved static memory plan cannot be mixed with a concrete memory capability.",
                    nameof(staticMemoryPlan));
            }

            if (!ReferenceEquals(staticMemoryPlan.GeneratedBinding, generatedBinding))
            {
                throw new ArgumentException(
                    "Static memory plan must retain the exact generated binding instance of its execution contract.",
                    nameof(staticMemoryPlan));
            }
        }

        ImmutableArray<int> frozenReads = FreezeRegisters(readRegisters, nameof(readRegisters));
        ImmutableArray<int> frozenWrites = FreezeRegisters(writeRegisters, nameof(writeRegisters));
        return new ExecutionContract(
            generatedBinding,
            runtimeProvider,
            instructionClass,
            serializationClass,
            placement,
            staticEffectContract.Trim(),
            memory,
            staticMemoryPlan,
            frozenReads,
            frozenWrites,
            resourceMask,
            isStealable,
            isRetireVisible,
            isAssist);
    }

    private static ImmutableArray<int> FreezeRegisters(IEnumerable<int>? registers, string parameterName)
    {
        if (registers is null)
        {
            return ImmutableArray<int>.Empty;
        }

        int[] copy = registers.ToArray();
        if (copy.Any(register => register < 0))
        {
            throw new ArgumentOutOfRangeException(parameterName, "Register identifiers cannot be negative.");
        }

        return ImmutableArray.Create(copy);
    }
}

/// <summary>Source/fetch provenance, separate from working retirement identity.</summary>
public sealed record SourceOperationProvenance
{
    [JsonConstructor]
    public SourceOperationProvenance(
        SemanticInstructionKey semanticKey,
        int sourceVirtualThreadId,
        ulong sourceBundleSerial,
        int sourceSlotIndex,
        ulong fetchEpoch)
        : this(
            semanticKey,
            sourceVirtualThreadId,
            sourceBundleSerial,
            ValidateRawSourceSlotIndex(sourceSlotIndex),
            fetchEpoch)
    {
    }

    public SourceOperationProvenance(
        SemanticInstructionKey semanticKey,
        int sourceVirtualThreadId,
        ulong sourceBundleSerial,
        SlotId sourceSlotId,
        ulong fetchEpoch)
    {
        if (sourceVirtualThreadId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceVirtualThreadId));
        }

        SemanticKey = semanticKey;
        SourceVirtualThreadId = sourceVirtualThreadId;
        SourceBundleSerial = sourceBundleSerial;
        SourceSlotId = sourceSlotId;
        FetchEpoch = fetchEpoch;
    }

    public SemanticInstructionKey SemanticKey { get; }
    public int SourceVirtualThreadId { get; }
    public ulong SourceBundleSerial { get; }
    [JsonIgnore]
    public SlotId SourceSlotId { get; }
    public int SourceSlotIndex => SourceSlotId;
    public ulong FetchEpoch { get; }

    private static SlotId ValidateRawSourceSlotIndex(int sourceSlotIndex)
    {
        if ((uint)sourceSlotIndex >= BundleMetadata.BundleSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceSlotIndex));
        }

        return SlotId.Create(sourceSlotIndex);
    }
}

/// <summary>Frozen scheduler-facing owner/domain and hazard facts.</summary>
public sealed record AdmissionRecord
{
    private AdmissionRecord(
        SourceOperationProvenance sourceProvenance,
        ExecutionContract executionContract,
        int virtualThreadId,
        int ownerContextId,
        ulong domainTag,
        ImmutableArray<int> readRegisters,
        ImmutableArray<int> writeRegisters,
        bool isStealable,
        bool isRetireVisible,
        ResourceBitset resourceMask)
    {
        SourceProvenance = sourceProvenance;
        ExecutionContract = executionContract;
        VirtualThreadId = virtualThreadId;
        OwnerContextId = ownerContextId;
        DomainTag = domainTag;
        ReadRegisters = readRegisters;
        WriteRegisters = writeRegisters;
        IsStealable = isStealable;
        IsRetireVisible = isRetireVisible;
        ResourceMask = resourceMask;
    }

    public SourceOperationProvenance SourceProvenance { get; }
    public ExecutionContract ExecutionContract { get; }
    public int VirtualThreadId { get; }
    public int OwnerContextId { get; }
    public ulong DomainTag { get; }
    public ImmutableArray<int> ReadRegisters { get; }
    public ImmutableArray<int> WriteRegisters { get; }
    public bool IsStealable { get; }
    public bool IsRetireVisible { get; }
    public ResourceBitset ResourceMask { get; }

    public static AdmissionRecord Create(
        SourceOperationProvenance sourceProvenance,
        ExecutionContract executionContract,
        int virtualThreadId,
        int ownerContextId,
        ulong domainTag,
        IEnumerable<int>? readRegisters = null,
        IEnumerable<int>? writeRegisters = null,
        bool? isStealable = null,
        bool? isRetireVisible = null,
        ResourceBitset? resourceMask = null)
    {
        ArgumentNullException.ThrowIfNull(sourceProvenance);
        ArgumentNullException.ThrowIfNull(executionContract);
        if (virtualThreadId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(virtualThreadId));
        }

        return new AdmissionRecord(
            sourceProvenance,
            executionContract,
            virtualThreadId,
            ownerContextId,
            domainTag,
            readRegisters is null ? executionContract.ReadRegisters : readRegisters.ToImmutableArray(),
            writeRegisters is null ? executionContract.WriteRegisters : writeRegisters.ToImmutableArray(),
            isStealable ?? executionContract.IsStealable,
            isRetireVisible ?? executionContract.IsRetireVisible,
            resourceMask ?? executionContract.ResourceMask);
    }

    /// <summary>
    /// Compares only scheduler policy facts. Source provenance and semantic cache
    /// identity are intentionally excluded from this equivalence relation.
    /// </summary>
    public bool HasEquivalentAdmissionFacts(AdmissionRecord other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return ExecutionContract == other.ExecutionContract &&
               VirtualThreadId == other.VirtualThreadId &&
               OwnerContextId == other.OwnerContextId &&
               DomainTag == other.DomainTag &&
               ReadRegisters.SequenceEqual(other.ReadRegisters) &&
               WriteRegisters.SequenceEqual(other.WriteRegisters) &&
               IsStealable == other.IsStealable &&
               IsRetireVisible == other.IsRetireVisible &&
               ResourceMask == other.ResourceMask;
    }
}

/// <summary>Frozen projection facts used to detect stale legacy admission carriers.</summary>
public sealed record LegacyAdmissionProjection(
    int VirtualThreadId,
    int OwnerContextId,
    ulong DomainTag,
    bool IsStealable,
    ImmutableArray<int> ReadRegisters,
    ImmutableArray<int> WriteRegisters,
    ExecutionPlacement Placement,
    ResourceBitset ResourceMask);

public readonly record struct AdmissionProjectionFingerprint
{
    public AdmissionProjectionFingerprint(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }
}

public static class LegacyProjectionGuard
{
    public static AdmissionProjectionFingerprint Capture(LegacyAdmissionProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        string canonical = string.Join(
            "|",
            projection.VirtualThreadId,
            projection.OwnerContextId,
            projection.DomainTag,
            projection.IsStealable,
            string.Join(",", projection.ReadRegisters),
            string.Join(",", projection.WriteRegisters),
            projection.Placement.RequiredSlotClass,
            projection.Placement.PinningKind,
            projection.Placement.PinnedLaneId,
            projection.Placement.DomainTag,
            projection.ResourceMask.Low,
            projection.ResourceMask.High);
        return new AdmissionProjectionFingerprint(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant());
    }

    public static void EnsureCurrent(
        AdmissionProjectionFingerprint expected,
        LegacyAdmissionProjection current)
    {
        AdmissionProjectionFingerprint actual = Capture(current);
        if (actual != expected)
        {
            throw new InvalidOperationException(
                "Legacy admission projection is stale after a scheduling-relevant carrier mutation.");
        }
    }
}

/// <summary>
/// Identity of one successful Stage-B materialization/issue attempt.
/// Construction is private and reachable only through ScheduledOperation.
/// </summary>
public readonly record struct VliwOperationId
{
    private VliwOperationId(int virtualThreadId, ulong workingBundleSequence, int workingSlotIndex, ulong operationAttempt)
    {
        VirtualThreadId = virtualThreadId;
        WorkingBundleSequence = workingBundleSequence;
        WorkingSlotIndex = workingSlotIndex;
        OperationAttempt = operationAttempt;
    }

    public int VirtualThreadId { get; }
    public ulong WorkingBundleSequence { get; }
    public int WorkingSlotIndex { get; }
    public ulong OperationAttempt { get; }

    internal static VliwOperationId Issue(
        int virtualThreadId,
        ulong workingBundleSequence,
        int workingSlotIndex,
        ulong operationAttempt) =>
        new(virtualThreadId, workingBundleSequence, workingSlotIndex, operationAttempt);
}

/// <summary>Non-cached issuer used only after successful Stage-B validation.</summary>
public sealed class OperationAttemptIssuer
{
    private ulong _nextAttempt;

    internal ulong IssueNextAttempt() => checked(++_nextAttempt);
}

/// <summary>
/// Issued operation created only after a physical lane has been selected.
/// It carries the already-resolved provider binding through the contract.
/// </summary>
public sealed class ScheduledOperation
{
    private ScheduledOperation(
        VliwOperationId operationId,
        AdmissionRecord admission,
        int physicalLane)
    {
        OperationId = operationId;
        Admission = admission;
        PhysicalLane = physicalLane;
    }

    public VliwOperationId OperationId { get; }
    public AdmissionRecord Admission { get; }
    public int PhysicalLane { get; }
    public RuntimeExecutionProviderBinding RuntimeProvider => Admission.ExecutionContract.RuntimeProvider;

    public static ScheduledOperation CreateAfterStageB(
        AdmissionRecord admission,
        ulong workingBundleSequence,
        int workingSlotIndex,
        int physicalLane,
        OperationAttemptIssuer attemptIssuer)
    {
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(attemptIssuer);
        if ((uint)workingSlotIndex >= BundleMetadata.BundleSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(workingSlotIndex));
        }

        if ((uint)physicalLane >= BundleMetadata.BundleSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(physicalLane), "A scheduled operation requires a selected physical lane.");
        }

        ulong attempt = attemptIssuer.IssueNextAttempt();
        VliwOperationId operationId = VliwOperationId.Issue(
            admission.VirtualThreadId,
            workingBundleSequence,
            workingSlotIndex,
            attempt);
        return new ScheduledOperation(operationId, admission, physicalLane);
    }
}
