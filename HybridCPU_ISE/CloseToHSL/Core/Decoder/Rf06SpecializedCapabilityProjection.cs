using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace YAKSys_Hybrid_CPU.Core.Decoder;

/// <summary>
/// RF-06.5 family identity. This is a static capability taxonomy, not an
/// execution-state or retirement taxonomy.
/// </summary>
internal enum Rf06CapabilityFamily : byte
{
    ControlFlow = 1,
    Serializing = 2,
    Assist = 3,
    Dma = 4,
    Accelerator = 5,
    MatrixTile = 6,
}

/// <summary>
/// Immutable control-flow capability. Target resolution and branch outcome are
/// deliberately absent; they belong to execution/retirement authorities.
/// </summary>
internal sealed record Rf06ControlCapability(
    ExecutionContract Contract,
    bool IsConditional,
    bool RedirectsProgramCounter,
    bool IsStealable,
    SlotClass RequiredSlotClass,
    SlotPinningKind PinningKind,
    byte PinnedLaneId);

/// <summary>Immutable serializing-boundary capability.</summary>
internal sealed record Rf06SerializingCapability(
    ExecutionContract Contract,
    InstructionClass InstructionClass,
    SerializationClass SerializationClass,
    bool IsControlFlow,
    bool HasStaticSideEffects,
    SlotClass RequiredSlotClass,
    SlotPinningKind PinningKind,
    byte PinnedLaneId);

/// <summary>
/// Immutable assist capability. Reserved line budgets, replay state, outcomes
/// and completion are intentionally not copied from the legacy carrier.
/// </summary>
internal sealed record Rf06AssistCapability(
    ExecutionContract Contract,
    AssistKind Kind,
    AssistExecutionMode ExecutionMode,
    AssistCarrierKind CarrierKind,
    AssistDonorSourceKind DonorSourceKind,
    int CarrierVirtualThreadId,
    int DonorVirtualThreadId,
    int TargetVirtualThreadId,
    int CarrierCoreId,
    int TargetCoreId,
    ushort PodId,
    ulong DomainTag,
    FrozenMemoryRange PrefetchFootprint,
    MemoryBankId Bank);

/// <summary>
/// DMA descriptor capability. DMA has independent read/write footprints, so
/// it is kept as a typed sideband beside the generic ExecutionContract rather
/// than being misrepresented as a scalar Atomic memory operation.
/// </summary>
internal sealed record Rf06DmaCapability(
    ExecutionContract Contract,
    DmaStreamComputeDescriptorReference DescriptorReference,
    ulong DescriptorIdentityHash,
    ulong CertificateInputHash,
    ulong NormalizedFootprintHash,
    DmaStreamComputeOperationKind Operation,
    DmaStreamComputeElementType ElementType,
    DmaStreamComputeShapeKind Shape,
    DmaStreamComputeRangeEncoding RangeEncoding,
    DmaStreamComputePartialCompletionPolicy PartialCompletionPolicy,
    DmaStreamComputeAliasPolicy AliasPolicy,
    ushort OwnerVirtualThreadId,
    uint OwnerContextId,
    uint OwnerCoreId,
    uint OwnerPodId,
    ulong OwnerDomainTag,
    ImmutableArray<FrozenMemoryRange> ReadFootprint,
    ImmutableArray<FrozenMemoryRange> WriteFootprint);

/// <summary>Immutable lane7 accelerator command capability.</summary>
internal sealed record Rf06AcceleratorCapability(
    ExecutionContract Contract,
    SystemDeviceCommandKind CommandKind,
    ushort DestinationRegister,
    ushort TokenRegister,
    bool HasDescriptorSideband,
    AcceleratorDescriptorReference? DescriptorReference,
    SlotClass RequiredSlotClass,
    SlotPinningKind PinningKind,
    byte PinnedLaneId);

/// <summary>Immutable MatrixTile placement/dependency capability.</summary>
internal sealed record Rf06MatrixTileCapability(
    ExecutionContract Contract,
    MatrixTileProjectedOperationKind OperationKind,
    MatrixTileRuntimeResourceClass RuntimeResourceClass,
    bool ReadsTileState,
    bool WritesTileState,
    bool ReadsAccumulator,
    bool WritesAccumulator,
    bool HasTransposePolicyDependency,
    SlotClass RequiredSlotClass,
    SlotPinningKind PinningKind,
    byte PinnedLaneId);

/// <summary>
/// Named RF-06.5 legacy boundary. It consumes an existing materialized carrier
/// and a binding resolved by the canonical decode/materializer handoff. It does
/// not create carriers, scheduler admission objects, working operation IDs or
/// replay entries and
/// does not resolve anything by opcode or registry.
/// </summary>
internal static class Rf06SpecializedCapabilityProjection
{
    internal const string ControlPayloadSchema = "rf06.control-flow-v1";
    internal const string SerializingPayloadSchema = "rf06.serializing-v1";
    internal const string AssistPayloadSchema = "rf06.assist-v1";
    internal const string DmaPayloadSchema = "rf06.dma-v1";
    internal const string AcceleratorPayloadSchema = "rf06.accelerator-v1";
    internal const string MatrixTilePayloadSchema = "rf06.matrix-tile-v1";

    internal static Rf06ControlCapability ProjectControl(
        MicroOp carrier,
        GeneratedStaticBinding binding)
    {
        ValidateBinding(carrier, binding);
        if (carrier is not BranchMicroOp || !carrier.IsControlFlow ||
            carrier.InstructionClass != InstructionClass.ControlFlow)
        {
            throw new InvalidOperationException(
                "RF-06.5 control projection requires a BranchMicroOp control-flow carrier.");
        }

        ExecutionContract contract = CreateContract(
            carrier,
            binding,
            ControlPayloadSchema,
            staticEffectContract: "PcWrite",
            MemoryCapability.None,
            isAssist: false,
            isRetireVisible: carrier.IsRetireVisible,
            validateOpcode: false);
        BranchMicroOp branch = (BranchMicroOp)carrier;

        return new Rf06ControlCapability(
            contract,
            branch.IsConditional,
            RedirectsProgramCounter: true,
            carrier.IsStealable,
            carrier.Placement.RequiredSlotClass,
            carrier.Placement.PinningKind,
            carrier.Placement.PinnedLaneId);
    }

    internal static Rf06SerializingCapability ProjectSerializing(
        MicroOp carrier,
        GeneratedStaticBinding binding)
    {
        ValidateBinding(carrier, binding);
        if (carrier.IsAssist || carrier.IsControlFlow ||
            carrier.SerializationClass == SerializationClass.Free)
        {
            throw new InvalidOperationException(
                "RF-06.5 serializing projection requires a non-assist, non-control carrier with an explicit serial boundary.");
        }

        ExecutionContract contract = CreateContract(
            carrier,
            binding,
            SerializingPayloadSchema,
            staticEffectContract: "SerializingEffect",
            BuildMemoryCapability(carrier),
            isAssist: false,
            isRetireVisible: carrier.IsRetireVisible);

        return new Rf06SerializingCapability(
            contract,
            carrier.InstructionClass,
            carrier.SerializationClass,
            carrier.IsControlFlow,
            carrier.HasSideEffects,
            carrier.Placement.RequiredSlotClass,
            carrier.Placement.PinningKind,
            carrier.Placement.PinnedLaneId);
    }

    internal static Rf06AssistCapability ProjectAssist(
        AssistMicroOp carrier,
        GeneratedStaticBinding binding)
    {
        ArgumentNullException.ThrowIfNull(carrier);
        ArgumentNullException.ThrowIfNull(binding);
        if (!carrier.IsAssist || carrier.IsRetireVisible ||
            carrier.ReadMemoryRanges.Count != 1 ||
            !carrier.HasResolvedMemoryBankId)
        {
            throw new InvalidOperationException(
                "RF-06.5 assist projection requires one resolved read footprint and a non-retire-visible assist carrier.");
        }

        (ulong address, ulong length) = carrier.ReadMemoryRanges[0];
        FrozenMemoryRange footprint = new(address, length);
        MemoryBankResolution bankResolution =
            MemoryBankResolution.Resolved(
                new MemoryBankId(carrier.MemoryBankId));
        MemoryBankId bank = bankResolution.Bank!.Value;
        ExecutionContract contract = CreateContract(
            carrier,
            binding,
            AssistPayloadSchema,
            staticEffectContract: "AssistPrefetch",
            MemoryCapability.Create(
                MemoryCapabilityKind.Load,
                new[] { footprint },
                bank),
            isAssist: true,
            isRetireVisible: false,
            validateOpcode: false);

        return new Rf06AssistCapability(
            contract,
            carrier.Kind,
            carrier.ExecutionMode,
            carrier.CarrierKind,
            carrier.DonorSource.Kind,
            carrier.CarrierVirtualThreadId,
            carrier.DonorVirtualThreadId,
            carrier.TargetVirtualThreadId,
            carrier.CarrierCoreId,
            carrier.TargetCoreId,
            carrier.PodId,
            carrier.Placement.DomainTag,
            footprint,
            bank);
    }

    internal static Rf06DmaCapability ProjectDma(
        DmaStreamComputeMicroOp carrier,
        GeneratedStaticBinding binding)
    {
        ArgumentNullException.ThrowIfNull(carrier);
        ArgumentNullException.ThrowIfNull(binding);
        if (carrier.Placement.RequiredSlotClass != SlotClass.DmaStreamClass ||
            carrier.Placement.PinningKind != SlotPinningKind.HardPinned ||
            carrier.Placement.PinnedLaneId != 6 ||
            carrier.ReadMemoryRanges.Count == 0 ||
            carrier.WriteMemoryRanges.Count == 0)
        {
            throw new InvalidOperationException(
                "RF-06.5 DMA projection requires the existing lane6 hard-pinned carrier and both immutable footprints.");
        }

        ExecutionContract contract = CreateContract(
            carrier,
            binding,
            DmaPayloadSchema,
            staticEffectContract: "DmaCommit",
            MemoryCapability.None,
            isAssist: false,
            isRetireVisible: carrier.IsRetireVisible,
            validateOpcode: false);

        return new Rf06DmaCapability(
            contract,
            carrier.DescriptorReference,
            carrier.DescriptorIdentityHash,
            carrier.CertificateInputHash,
            carrier.NormalizedFootprintHash,
            carrier.Operation,
            carrier.ElementType,
            carrier.Shape,
            carrier.RangeEncoding,
            carrier.PartialCompletionPolicy,
            carrier.Descriptor.AliasPolicy,
            carrier.OwnerBinding.OwnerVirtualThreadId,
            carrier.OwnerBinding.OwnerContextId,
            carrier.OwnerBinding.OwnerCoreId,
            carrier.OwnerBinding.OwnerPodId,
            carrier.OwnerBinding.OwnerDomainTag,
            FreezeRanges(carrier.ReadMemoryRanges),
            FreezeRanges(carrier.WriteMemoryRanges));
    }

    internal static Rf06AcceleratorCapability ProjectAccelerator(
        SystemDeviceCommandMicroOp carrier,
        GeneratedStaticBinding binding)
    {
        ValidateBinding(carrier, binding);
        if (carrier.Placement.RequiredSlotClass != SlotClass.SystemSingleton ||
            carrier.Placement.PinningKind != SlotPinningKind.HardPinned ||
            carrier.Placement.PinnedLaneId != 7 ||
            carrier.IsStealable)
        {
            throw new InvalidOperationException(
                "RF-06.5 accelerator projection requires the non-stealable lane7 system-singleton carrier.");
        }

        ExecutionContract contract = CreateContract(
            carrier,
            binding,
            AcceleratorPayloadSchema,
            staticEffectContract: "AcceleratorCommand",
            MemoryCapability.None,
            isAssist: false,
            isRetireVisible: carrier.IsRetireVisible);

        return new Rf06AcceleratorCapability(
            contract,
            carrier.CommandKind,
            carrier.DestinationRegister,
            carrier.TokenRegister,
            carrier.CommandDescriptor is not null,
            carrier.CommandDescriptorReference,
            carrier.Placement.RequiredSlotClass,
            carrier.Placement.PinningKind,
            carrier.Placement.PinnedLaneId);
    }

    internal static Rf06MatrixTileCapability ProjectMatrixTile(
        MatrixTileMicroOp carrier,
        GeneratedStaticBinding binding)
    {
        ValidateBinding(carrier, binding);
        if (carrier.OperationKind == MatrixTileProjectedOperationKind.Unspecified ||
            carrier.RuntimeResourceClass == MatrixTileRuntimeResourceClass.None ||
            carrier.Placement.RequiredSlotClass == SlotClass.Unclassified)
        {
            throw new InvalidOperationException(
                "RF-06.5 MatrixTile projection requires a legal typed MatrixTile carrier.");
        }

        ExecutionContract contract = CreateContract(
            carrier,
            binding,
            MatrixTilePayloadSchema,
            staticEffectContract: carrier.OperationKind is
                MatrixTileProjectedOperationKind.Load or MatrixTileProjectedOperationKind.Store
                ? "MatrixTileMemoryCommit"
                : "MatrixTileCommit",
            BuildMemoryCapability(carrier),
            isAssist: false,
            isRetireVisible: carrier.IsRetireVisible);
        MatrixTileMicroOpDependencyMetadata dependency = carrier.DependencyMetadata;

        return new Rf06MatrixTileCapability(
            contract,
            carrier.OperationKind,
            carrier.RuntimeResourceClass,
            dependency.ReadsTileState,
            dependency.WritesTileState,
            dependency.ReadsAccumulator,
            dependency.WritesAccumulator,
            dependency.HasTransposePolicyDependencyMetadata,
            carrier.Placement.RequiredSlotClass,
            carrier.Placement.PinningKind,
            carrier.Placement.PinnedLaneId);
    }

    private static ExecutionContract CreateContract(
        MicroOp carrier,
        GeneratedStaticBinding binding,
        string payloadSchema,
        string staticEffectContract,
        MemoryCapability memory,
        bool isAssist,
        bool isRetireVisible,
        bool validateOpcode = true)
    {
        ArgumentNullException.ThrowIfNull(carrier);
        ArgumentNullException.ThrowIfNull(binding);
        if (validateOpcode && binding.Opcode != carrier.OpCode)
        {
            throw new InvalidOperationException(
                "RF-06.5 projection received a generated binding for a different materialized carrier.");
        }

        return ExecutionContract.Create(
            binding,
            new RuntimeExecutionProviderBinding(binding.RuntimeExecutionProviderId, payloadSchema),
            carrier.InstructionClass,
            carrier.SerializationClass,
            ExecutionPlacement.Create(
                carrier.Placement.RequiredSlotClass,
                carrier.Placement.PinningKind,
                carrier.Placement.PinnedLaneId,
                carrier.Placement.DomainTag),
            staticEffectContract,
            memory,
            carrier.ReadRegisters,
            carrier.WriteRegisters,
            carrier.ResourceMask,
            carrier.IsStealable,
            isRetireVisible,
            isAssist);
    }

    private static void ValidateBinding(MicroOp carrier, GeneratedStaticBinding binding)
    {
        ArgumentNullException.ThrowIfNull(carrier);
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.Opcode != carrier.OpCode)
        {
            throw new InvalidOperationException(
                "RF-06.5 projection requires the exact generated binding carried by the canonical handoff.");
        }
    }

    private static MemoryCapability BuildMemoryCapability(MicroOp carrier)
    {
        IReadOnlyList<(ulong Address, ulong Length)> reads = carrier.ReadMemoryRanges;
        IReadOnlyList<(ulong Address, ulong Length)> writes = carrier.WriteMemoryRanges;
        if (reads.Count == 0 && writes.Count == 0)
        {
            return MemoryCapability.None;
        }

        MemoryCapabilityKind kind = reads.Count > 0 && writes.Count > 0
            ? MemoryCapabilityKind.Atomic
            : reads.Count > 0
                ? MemoryCapabilityKind.Load
                : MemoryCapabilityKind.Store;
        IReadOnlyList<(ulong Address, ulong Length)> selected = reads.Count > 0 ? reads : writes;
        ImmutableArray<FrozenMemoryRange> footprint = FreezeRanges(selected);
        int bank = MemoryBankRouting.ResolveSchedulerVisibleBankId(footprint[0].Address);
        MemoryBankResolution bankResolution =
            !MemoryBankRouting.IsResolvedSchedulerVisibleBankId(bank)
                ? Processor.Memory is null
                    ? MemoryBankResolution.UnavailableTopology
                    : MemoryBankResolution.InvalidGeometry
                : MemoryBankResolution.Resolved(new MemoryBankId(bank));
        if (!bankResolution.IsResolved)
        {
            throw new InvalidOperationException(
                "RF-06.5 memory-bearing family projection requires a resolved scheduler-visible bank.");
        }

        return MemoryCapability.Create(
            kind,
            footprint,
            bankResolution.Bank!.Value);
    }

    private static ImmutableArray<FrozenMemoryRange> FreezeRanges(
        IEnumerable<(ulong Address, ulong Length)> ranges)
    {
        FrozenMemoryRange[] frozen = ranges
            .Select(range => new FrozenMemoryRange(range.Address, range.Length))
            .OrderBy(range => range.Address)
            .ThenBy(range => range.Length)
            .ToArray();
        for (int index = 1; index < frozen.Length; index++)
        {
            if (frozen[index].Address <= frozen[index - 1].LastAddress)
            {
                throw new InvalidOperationException(
                    "RF-06.5 family footprint contains overlapping normalized ranges.");
            }
        }

        return ImmutableArray.Create(frozen);
    }
}
