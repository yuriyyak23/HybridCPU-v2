using System.Collections.Generic;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.Core.Decoder;

/// <summary>
/// RF-05 compatibility boundary. It consumes only frozen declarative decode
/// output and projects the retained public <see cref="InstructionIR"/> adapter.
/// It never creates executable work, admission identity or replay state.
/// </summary>
internal static class CanonicalInstructionIrCompatibilityProjector
{
    internal static DecodedInstructionBundle ProjectBundle(
        ReadOnlySpan<VLIW_Instruction> rawSlots,
        VliwBundleAnnotations? bundleAnnotations,
        DeclarativeDecodedBundle declarativeBundle,
        ulong bundleAddress,
        ulong bundleSerial)
    {
        ArgumentNullException.ThrowIfNull(declarativeBundle);
        if (rawSlots.Length != BundleMetadata.BundleSlotCount ||
            declarativeBundle.Slots.Length != BundleMetadata.BundleSlotCount ||
            declarativeBundle.CanonicalBundle.Slots.Length != BundleMetadata.BundleSlotCount)
        {
            throw new InvalidOperationException("Compatibility projection requires one normalized eight-slot canonical bundle.");
        }

        var projected = new List<DecodedInstruction>(BundleMetadata.BundleSlotCount);
        for (int slotIndex = 0; slotIndex < BundleMetadata.BundleSlotCount; slotIndex++)
        {
            InstructionSlotMetadata slotMetadata = ResolveSlotMetadata(bundleAnnotations, slotIndex);
            CanonicalDecodedInstruction canonicalSlot = declarativeBundle.CanonicalBundle.GetSlot(slotIndex);
            DeclarativeDecodedSlot? declarativeSlot = declarativeBundle.Slots[slotIndex];
            if (!canonicalSlot.IsOccupied)
            {
                if (declarativeSlot is not null)
                {
                    throw new InvalidOperationException($"Empty canonical slot {slotIndex} has declarative semantics.");
                }

                projected.Add(DecodedInstruction.CreateEmpty(slotIndex, slotMetadata));
                continue;
            }

            if (declarativeSlot is null)
            {
                throw new InvalidOperationException($"Occupied canonical slot {slotIndex} lacks declarative semantics.");
            }

            RuntimeSidebandProjection runtimeSideband = DecoderRuntimeSidebandValidator.ValidateAndResolve(
                in rawSlots[slotIndex],
                slotIndex,
                slotMetadata);
            projected.Add(DecodedInstruction.CreateOccupied(
                slotIndex,
                ProjectInstruction(canonicalSlot, declarativeSlot, runtimeSideband),
                slotMetadata));
        }

        BundleMetadata bundleMetadata = bundleAnnotations?.BundleMetadata ?? BundleMetadata.Default;
        return new DecodedInstructionBundle(
            bundleAddress,
            bundleSerial,
            projected,
            bundleMetadata,
            canonicalBundle: declarativeBundle.CanonicalBundle);
    }

    internal static InstructionIR ProjectInstruction(
        CanonicalDecodedInstruction canonicalSlot,
        DeclarativeDecodedSlot declarativeSlot,
        RuntimeSidebandProjection runtimeSideband)
    {
        ArgumentNullException.ThrowIfNull(canonicalSlot);
        ArgumentNullException.ThrowIfNull(declarativeSlot);
        ArgumentNullException.ThrowIfNull(runtimeSideband);
        if (!canonicalSlot.IsOccupied || canonicalSlot.SlotIndex != declarativeSlot.CanonicalInstruction.SlotIndex ||
            canonicalSlot.Opcode != declarativeSlot.CanonicalInstruction.Opcode)
        {
            throw new InvalidOperationException("Compatibility projection received inconsistent canonical slot identity.");
        }

        return ProjectInstruction(
            canonicalSlot,
            declarativeSlot.VectorPayload,
            runtimeSideband);
    }

    internal static InstructionIR ProjectReplayInstruction(
        in VLIW_Instruction rawSlot,
        CanonicalDecodedInstruction canonicalSlot,
        InstructionSlotMetadata slotMetadata)
    {
        ArgumentNullException.ThrowIfNull(canonicalSlot);
        if (!canonicalSlot.IsOccupied ||
            !string.Equals(
                canonicalSlot.InstructionPayload.Kind,
                "DeclarativeInstructionSemantics",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Replay projection requires an occupied declarative canonical slot.");
        }

        DeclarativeInstructionSemantics semantics =
            canonicalSlot.InstructionPayload.Deserialize<DeclarativeInstructionSemantics>() ??
            throw new InvalidOperationException(
                "Replay projection could not restore frozen declarative semantics.");
        if (semantics.Opcode != canonicalSlot.Opcode ||
            semantics.InstructionClass != canonicalSlot.InstructionClass ||
            semantics.SerializationClass != canonicalSlot.SerializationClass ||
            semantics.Rd != canonicalSlot.Rd ||
            semantics.Rs1 != canonicalSlot.Rs1 ||
            semantics.Rs2 != canonicalSlot.Rs2 ||
            semantics.Immediate != canonicalSlot.Immediate)
        {
            throw new InvalidOperationException(
                "Frozen declarative semantics do not match the canonical replay slot.");
        }

        RuntimeSidebandProjection runtimeSideband =
            DecoderRuntimeSidebandValidator.ValidateAndResolve(
                in rawSlot,
                canonicalSlot.SlotIndex,
                slotMetadata);
        return ProjectInstruction(
            canonicalSlot,
            semantics.VectorPayload,
            runtimeSideband);
    }

    private static InstructionIR ProjectInstruction(
        CanonicalDecodedInstruction canonicalSlot,
        VectorInstructionPayload? vectorPayload,
        RuntimeSidebandProjection runtimeSideband)
    {
        InstructionClass instructionClass = canonicalSlot.InstructionClass ??
            throw new InvalidOperationException("Occupied canonical slot has no instruction class.");
        SerializationClass serializationClass = canonicalSlot.SerializationClass ??
            throw new InvalidOperationException("Occupied canonical slot has no serialization class.");
        ushort opcode = checked((ushort)canonicalSlot.Opcode);
        MatrixTileInstructionIrProjection? matrixProjection = CreateMatrixProjection(
            opcode,
            vectorPayload,
            canonicalSlot.Immediate);

        return new InstructionIR
        {
            CanonicalOpcode = new Processor.CPU_Core.IsaOpcode(opcode),
            Class = instructionClass,
            SerializationClass = serializationClass,
            Rd = canonicalSlot.Rd,
            Rs1 = canonicalSlot.Rs1,
            Rs2 = canonicalSlot.Rs2,
            Imm = canonicalSlot.Immediate,
            CsrAddress = canonicalSlot.CsrAddress,
            HasAbsoluteAddressing = false,
            AcquireOrdering = canonicalSlot.AcquireOrdering,
            ReleaseOrdering = canonicalSlot.ReleaseOrdering,
            VectorPayload = vectorPayload,
            MatrixTileProjection = matrixProjection,
            DmaStreamComputeDescriptorReference = runtimeSideband.DmaStreamComputeDescriptor?.DescriptorReference,
            DmaStreamComputeDescriptor = runtimeSideband.DmaStreamComputeDescriptor,
            AcceleratorCommandDescriptorReference = runtimeSideband.AcceleratorCommandDescriptor?.DescriptorReference,
            AcceleratorCommandDescriptor = runtimeSideband.AcceleratorCommandDescriptor,
        };
    }

    private static MatrixTileInstructionIrProjection? CreateMatrixProjection(
        ushort opcode,
        VectorInstructionPayload? vectorPayload,
        long immediate)
    {
        if (!MatrixTileRuntimeOwnedVlmRows.IsMatrixTileOpcode((InstructionsEnum)opcode) || !vectorPayload.HasValue)
        {
            return null;
        }

        return MatrixTileIrProjectionAndMaterializer.ProjectDecodedVectorPayload(
            (InstructionsEnum)opcode,
            vectorPayload.Value,
            immediate,
            requireExplicitNumericPolicy: true);
    }

    private static InstructionSlotMetadata ResolveSlotMetadata(
        VliwBundleAnnotations? bundleAnnotations,
        int slotIndex) =>
        bundleAnnotations is not null &&
        bundleAnnotations.TryGetInstructionSlotMetadata(slotIndex, out InstructionSlotMetadata metadata)
            ? metadata
            : InstructionSlotMetadata.Default;
}
