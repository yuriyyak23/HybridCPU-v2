using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Arch.Generated;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace YAKSys_Hybrid_CPU.Core.Decoder;

/// <summary>
/// RF-05 raw frontend value contracts. These types perform no semantic
/// reconstruction and deliberately contain no mutable decoder context.
/// </summary>
public readonly record struct RawSlot(
    int SlotIndex,
    VLIW_Instruction Instruction,
    ushort Opcode,
    ulong Word0,
    ulong Word1,
    ulong Word2,
    ulong Word3);

/// <summary>Fixed-width raw bundle passed between the first two decoder stages.</summary>
public sealed class RawBundle
{
    public RawBundle(ImmutableArray<RawSlot> slots)
    {
        if (slots.Length != BundleMetadata.BundleSlotCount)
        {
            throw new ArgumentException(
                $"Raw bundle must contain exactly {BundleMetadata.BundleSlotCount} slots.",
                nameof(slots));
        }

        Slots = slots;
    }

    public ImmutableArray<RawSlot> Slots { get; }
}

/// <summary>Raw bundle reader: copies the eight frontend slots without legality decisions.</summary>
public static class RawBundleReader
{
    public static RawBundle Read(ReadOnlySpan<VLIW_Instruction> bundle)
    {
        if (bundle.Length != BundleMetadata.BundleSlotCount)
        {
            throw new ArgumentException(
                $"Decoder bundles must contain exactly {BundleMetadata.BundleSlotCount} slots.",
                nameof(bundle));
        }

        var slots = ImmutableArray.CreateBuilder<RawSlot>(BundleMetadata.BundleSlotCount);
        for (int slotIndex = 0; slotIndex < bundle.Length; slotIndex++)
        {
            slots.Add(RawSlotReader.Read(in bundle[slotIndex], slotIndex));
        }

        return new RawBundle(slots.MoveToImmutable());
    }
}

/// <summary>Raw-slot reader: field extraction only.</summary>
public static class RawSlotReader
{
    public static RawSlot Read(in VLIW_Instruction instruction, int slotIndex)
    {
        if ((uint)slotIndex >= BundleMetadata.BundleSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        }

        return new RawSlot(
            slotIndex,
            instruction,
            unchecked((ushort)instruction.OpCode),
            instruction.Word0,
            instruction.Word1,
            instruction.Word2,
            instruction.Word3);
    }
}

/// <summary>Generated static descriptor lookup; dynamic legality remains with RF-05 validators.</summary>
public static class OpcodeDescriptorLookup
{
    // These optional contours deliberately have no generated executable row.
    // Keep their raw identities explicit so that a known fail-closed contour is
    // never reported as an unknown encoding during the RF-05 shadow period.
    private static readonly IReadOnlyDictionary<ushort, string> UnsupportedOptionalContours =
        new Dictionary<ushort, string>
        {
            [45] = "XSQRT",
            [52] = "NOT",
            [55] = "XFMAC",
        };

    public static bool TryLookup(in RawSlot slot, out GeneratedIsaDescriptor descriptor, out DecodeFailure? failure)
    {
        if (slot.Opcode == 0)
        {
            descriptor = default;
            failure = null;
            return true;
        }

        if (IsaV4Surface.ProhibitedOpcodes.Contains(
                slot.Opcode.ToString(CultureInfo.InvariantCulture)))
        {
            descriptor = default;
            failure = DecodeFailure.Create(
                DecodeFailureCode.ProhibitedOpcode,
                slot.SlotIndex,
                "opcode",
                RawSlotBytes(in slot),
                $"Opcode 0x{slot.Opcode:X4} belongs to the prohibited ISA v4 decode surface.");
            return false;
        }

        if (UnsupportedOptionalContours.TryGetValue(slot.Opcode, out string? contour))
        {
            descriptor = default;
            failure = DecodeFailure.Create(
                DecodeFailureCode.UnsupportedOpcode,
                slot.SlotIndex,
                "opcode",
                RawSlotBytes(in slot),
                $"Opcode 0x{slot.Opcode:X4} belongs to the unsupported optional {contour} contour.");
            return false;
        }

        if (GeneratedIsaCatalog.TryGetDescriptor(slot.Opcode, out descriptor))
        {
            failure = null;
            return true;
        }

        failure = DecodeFailure.Create(
            DecodeFailureCode.UnknownOpcode,
            slot.SlotIndex,
            "opcode",
            RawSlotBytes(in slot),
            $"Opcode 0x{slot.Opcode:X4} has no generated ISA descriptor.");
        return false;
    }

    private static byte[] RawSlotBytes(in RawSlot slot)
    {
        byte[] bytes = new byte[sizeof(ulong) * 4];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, sizeof(ulong)), slot.Word0);
        BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(ulong), sizeof(ulong)), slot.Word1);
        BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(ulong) * 2, sizeof(ulong)), slot.Word2);
        BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(ulong) * 3, sizeof(ulong)), slot.Word3);
        return bytes;
    }
}

/// <summary>
/// Frozen canonical operand fields obtained directly from the raw slot ABI.
/// This stage has no sideband, dynamic-legality, or materialization knowledge.
/// </summary>
public readonly record struct DecodedOperandFields(byte Rd, byte Rs1, byte Rs2, long Immediate);

/// <summary>
/// Declarative operand decoder. The descriptor selects the static operand ABI;
/// expected malformed packed-register encodings become typed failures.
/// </summary>
public static class OperandDecoder
{
    public static bool TryDecode(
        in RawSlot slot,
        in GeneratedIsaDescriptor descriptor,
        out DecodedOperandFields operands,
        out DecodeFailure? failure)
    {
        ushort opcode = slot.Opcode;
        if (opcode == Processor.CPU_Core.IsaOpcodeValues.DmaStreamCompute)
        {
            operands = new DecodedOperandFields(
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg,
                (short)slot.Instruction.Immediate);
            failure = null;
            return true;
        }

        if (opcode == Processor.CPU_Core.IsaOpcodeValues.VPOPC)
        {
            operands = new DecodedOperandFields(
                (byte)((slot.Instruction.Immediate >> 8) & 0x0F),
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg,
                (short)slot.Instruction.Immediate);
            failure = null;
            return true;
        }

        if (OpcodeRegistry.IsVectorOp(slot.Instruction.OpCode) &&
            descriptor.StaticClass is InstructionClass.ScalarAlu or InstructionClass.Memory)
        {
            operands = new DecodedOperandFields(
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg,
                (short)slot.Instruction.Immediate);
            failure = null;
            return true;
        }

        if (OpcodeRegistry.UsesPackedArchRegisterWord1(opcode))
        {
            if (!VLIW_Instruction.TryUnpackArchRegs(
                    slot.Instruction.Word1,
                    out byte rd,
                    out byte rs1,
                    out byte rs2))
            {
                operands = default;
                failure = DecodeFailure.Create(
                    DecodeFailureCode.OperandEncoding,
                    slot.SlotIndex,
                    "Word1",
                    RawSlotBytes(in slot),
                    $"Opcode '{descriptor.Mnemonic}' uses a non-canonical packed architectural-register encoding.");
                return false;
            }

            if (!TryValidateRequiredRf06RegisterOperands(
                    in slot,
                    in descriptor,
                    rd,
                    rs1,
                    rs2,
                    out failure))
            {
                operands = default;
                return false;
            }

            operands = new DecodedOperandFields(rd, rs1, rs2, (short)slot.Instruction.Immediate);
            failure = null;
            return true;
        }

        operands = new DecodedOperandFields(
            (byte)(slot.Instruction.DestSrc1Pointer != 0
                ? slot.Instruction.DestSrc1Pointer & 0x1F
                : slot.Instruction.Reg1ID),
            (byte)(slot.Instruction.Src2Pointer != 0
                ? slot.Instruction.Src2Pointer & 0x1F
                : slot.Instruction.Reg2ID),
            (byte)slot.Instruction.Reg3ID,
            (short)slot.Instruction.Immediate);
        failure = null;
        return true;
    }

    private static bool TryValidateRequiredRf06RegisterOperands(
        in RawSlot slot,
        in GeneratedIsaDescriptor descriptor,
        byte rd,
        byte rs1,
        byte rs2,
        out DecodeFailure? failure)
    {
        bool isRf06RegisterRegisterOpcode = slot.Opcode is
            Processor.CPU_Core.IsaOpcodeValues.ADD or
            Processor.CPU_Core.IsaOpcodeValues.SUB or
            Processor.CPU_Core.IsaOpcodeValues.AND or
            Processor.CPU_Core.IsaOpcodeValues.OR or
            Processor.CPU_Core.IsaOpcodeValues.XOR;
        if (!isRf06RegisterRegisterOpcode ||
            (rd != VLIW_Instruction.NoArchReg &&
             rs1 != VLIW_Instruction.NoArchReg &&
             rs2 != VLIW_Instruction.NoArchReg))
        {
            failure = null;
            return true;
        }

        var absentRoles = new List<string>(3);
        if (rd == VLIW_Instruction.NoArchReg)
        {
            absentRoles.Add("rd");
        }

        if (rs1 == VLIW_Instruction.NoArchReg)
        {
            absentRoles.Add("rs1");
        }

        if (rs2 == VLIW_Instruction.NoArchReg)
        {
            absentRoles.Add("rs2");
        }

        failure = DecodeFailure.Create(
            DecodeFailureCode.OperandEncoding,
            slot.SlotIndex,
            "Word1",
            RawSlotBytes(in slot),
            $"Opcode '{descriptor.Mnemonic}' requires present rd, rs1 and rs2 architectural-register operands; NoArchReg is not legal in required role(s): {string.Join(", ", absentRoles)}.");
        return false;
    }

    private static byte[] RawSlotBytes(in RawSlot slot)
    {
        byte[] bytes = new byte[sizeof(ulong) * 4];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, sizeof(ulong)), slot.Word0);
        BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(ulong), sizeof(ulong)), slot.Word1);
        BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(ulong) * 2, sizeof(ulong)), slot.Word2);
        BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(ulong) * 3, sizeof(ulong)), slot.Word3);
        return bytes;
    }
}

/// <summary>
/// RF-05 named static encoding-legality registry.  These rules intentionally
/// describe only raw, opcode-local ABI constraints: they do not own sideband,
/// privilege, resource, or other runtime-dependent legality.
/// </summary>
public static class EncodingConstraintValidator
{
    private static readonly RawEncodingRule[] RawFormConstraints =
    {
        new("reserved-word0-zero", ValidateReservedWord0),
        new("retired-policy-gap-zero", ValidateRetiredPolicyGap),
        new("opcode-flag-legality", ValidateOpcodeFlags),
        new("fence-zero-payload", ValidateFencePayload),
    };

    private static readonly IEncodingConstraint[] Constraints =
    {
        RegisterConstraint.RequireRs2Zero(
            "scalar-word-shift-immediate-rs2-zero",
            Processor.CPU_Core.IsaOpcodeValues.SLLIW,
            Processor.CPU_Core.IsaOpcodeValues.SRLIW,
            Processor.CPU_Core.IsaOpcodeValues.SRAIW),
        RegisterConstraint.RequireRs2AndImmediateZero(
            "scalar-unary-rs2-and-immediate-zero",
            Processor.CPU_Core.IsaOpcodeValues.SEXT_W,
            Processor.CPU_Core.IsaOpcodeValues.ZEXT_W,
            Processor.CPU_Core.IsaOpcodeValues.CLZ,
            Processor.CPU_Core.IsaOpcodeValues.CTZ,
            Processor.CPU_Core.IsaOpcodeValues.CPOP,
            Processor.CPU_Core.IsaOpcodeValues.SEXT_B,
            Processor.CPU_Core.IsaOpcodeValues.SEXT_H,
            Processor.CPU_Core.IsaOpcodeValues.ZEXT_H,
            Processor.CPU_Core.IsaOpcodeValues.REV8,
            Processor.CPU_Core.IsaOpcodeValues.BREV8),
        RegisterConstraint.RequireImmediateZero(
            "scalar-address-generation-immediate-zero",
            Processor.CPU_Core.IsaOpcodeValues.SH1ADD,
            Processor.CPU_Core.IsaOpcodeValues.SH2ADD,
            Processor.CPU_Core.IsaOpcodeValues.SH3ADD,
            Processor.CPU_Core.IsaOpcodeValues.ADD_UW,
            Processor.CPU_Core.IsaOpcodeValues.SH1ADD_UW,
            Processor.CPU_Core.IsaOpcodeValues.SH2ADD_UW,
            Processor.CPU_Core.IsaOpcodeValues.SH3ADD_UW),
        RegisterConstraint.RequireRs2ZeroAndImmediateAtMost(
            "scalar-address-generation-imm6",
            0x3F,
            Processor.CPU_Core.IsaOpcodeValues.SLLI_UW),
        RegisterConstraint.RequireImmediateZero(
            "scalar-carry-less-immediate-zero",
            Processor.CPU_Core.IsaOpcodeValues.CLMUL,
            Processor.CPU_Core.IsaOpcodeValues.CLMULH,
            Processor.CPU_Core.IsaOpcodeValues.CLMULR),
        RegisterConstraint.RequireImmediateZero(
            "scalar-rotate-immediate-zero",
            Processor.CPU_Core.IsaOpcodeValues.ROL,
            Processor.CPU_Core.IsaOpcodeValues.ROR),
        RegisterConstraint.RequireRs2ZeroAndImmediateAtMost(
            "scalar-rotate-imm6",
            0x3F,
            Processor.CPU_Core.IsaOpcodeValues.ROLI,
            Processor.CPU_Core.IsaOpcodeValues.RORI),
        RegisterConstraint.RequireImmediateZero(
            "scalar-boolean-invert-immediate-zero",
            Processor.CPU_Core.IsaOpcodeValues.ANDN,
            Processor.CPU_Core.IsaOpcodeValues.ORN,
            Processor.CPU_Core.IsaOpcodeValues.XNOR),
        RegisterConstraint.RequireImmediateZero(
            "scalar-bitfield-immediate-zero",
            Processor.CPU_Core.IsaOpcodeValues.BSET,
            Processor.CPU_Core.IsaOpcodeValues.BCLR,
            Processor.CPU_Core.IsaOpcodeValues.BINV,
            Processor.CPU_Core.IsaOpcodeValues.BEXT),
        RegisterConstraint.RequireRs2ZeroAndImmediateAtMost(
            "scalar-bitfield-imm6",
            0x3F,
            Processor.CPU_Core.IsaOpcodeValues.BSETI,
            Processor.CPU_Core.IsaOpcodeValues.BCLRI,
            Processor.CPU_Core.IsaOpcodeValues.BINVI,
            Processor.CPU_Core.IsaOpcodeValues.BEXTI),
        RegisterConstraint.RequireImmediateZero(
            "scalar-minmax-immediate-zero",
            Processor.CPU_Core.IsaOpcodeValues.MIN,
            Processor.CPU_Core.IsaOpcodeValues.MAX,
            Processor.CPU_Core.IsaOpcodeValues.MINU,
            Processor.CPU_Core.IsaOpcodeValues.MAXU),
        RegisterConstraint.RequireImmediateZero(
            "scalar-zeroing-select-immediate-zero",
            Processor.CPU_Core.IsaOpcodeValues.CZERO_NEZ),
        RegisterConstraint.RequireRs1Rs2AndImmediateZero(
            "counter-read-source-and-immediate-zero",
            Processor.CPU_Core.IsaOpcodeValues.RDCYCLE),
        new DscQueueControlConstraint(),
        new ControlFlowTargetTransportConstraint(),
    };

    static EncodingConstraintValidator()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (IEncodingConstraint constraint in Constraints)
        {
            if (!ids.Add(constraint.Id))
            {
                throw new InvalidOperationException(
                    $"Duplicate RF-05 encoding constraint identifier '{constraint.Id}'.");
            }
        }
    }

    public static IReadOnlyList<string> RegisteredConstraintIds { get; } =
        Array.ConvertAll(Constraints, static constraint => constraint.Id);

    public static IReadOnlyList<string> RegisteredRawFormConstraintIds { get; } =
        Array.ConvertAll(RawFormConstraints, static constraint => constraint.Id);

    /// <summary>
    /// Validates raw encoding fields that are independent of decoded operands.
    /// These named checks retain typed failure semantics while the legacy body is
    /// still the public reject authority.
    /// </summary>
    public static bool TryValidateRawForm(
        in RawSlot slot,
        in GeneratedIsaDescriptor descriptor,
        out DecodeFailure? failure)
    {
        foreach (RawEncodingRule constraint in RawFormConstraints)
        {
            failure = constraint.Validate(in slot, in descriptor);
            if (failure is not null)
            {
                return false;
            }
        }

        failure = null;
        return true;
    }

    public static bool TryValidate(
        in RawSlot slot,
        in GeneratedIsaDescriptor descriptor,
        in DecodedOperandFields operands,
        out DecodeFailure? failure)
    {
        foreach (IEncodingConstraint constraint in Constraints)
        {
            if (!constraint.AppliesTo(slot.Opcode) ||
                constraint.TryValidate(in slot, in descriptor, in operands, out failure))
            {
                continue;
            }

            return false;
        }

        failure = null;
        return true;
    }

    private interface IEncodingConstraint
    {
        string Id { get; }

        bool AppliesTo(ushort opcode);

        bool TryValidate(
            in RawSlot slot,
            in GeneratedIsaDescriptor descriptor,
            in DecodedOperandFields operands,
            out DecodeFailure? failure);
    }

    private delegate DecodeFailure? RawEncodingRuleValidator(
        in RawSlot slot,
        in GeneratedIsaDescriptor descriptor);

    private sealed record RawEncodingRule(string Id, RawEncodingRuleValidator Validate);

    private static DecodeFailure? ValidateReservedWord0(
        in RawSlot slot,
        in GeneratedIsaDescriptor descriptor) =>
        slot.Instruction.Reserved == 0
            ? null
            : CreateFailure(
                in slot,
                descriptor.Mnemonic,
                "Reserved",
                "reserved Word0 bits [47:40] must be zero.");

    private static DecodeFailure? ValidateRetiredPolicyGap(
        in RawSlot slot,
        in GeneratedIsaDescriptor descriptor) =>
        (slot.Word3 & (1UL << 50)) == 0
            ? null
            : CreateFailure(
                in slot,
                descriptor.Mnemonic,
                "Word3[50]",
                "the retired legacy policy-gap bit must be zero.");

    private static DecodeFailure? ValidateOpcodeFlags(
        in RawSlot slot,
        in GeneratedIsaDescriptor descriptor)
    {
        OpcodeInfo? info = OpcodeRegistry.GetInfo(slot.Opcode);
        bool isAtomic = info?.InstructionClass == InstructionClass.Atomic ||
                        (info?.Flags & InstructionFlags.Atomic) != 0;
        bool hasVectorPayload = OpcodeRegistry.RequiresVectorPayloadProjection(slot.Opcode);
        VLIW_Instruction instruction = slot.Instruction;

        if ((instruction.Acquire || instruction.Release) && !isAtomic)
        {
            return CreateFailure(in slot, descriptor.Mnemonic, "AcquireRelease", "Acquire/Release flags are legal only for atomic opcodes.");
        }

        if (instruction.Saturating && !OpcodeRegistry.SupportsSaturatingAddPolicy(slot.Opcode))
        {
            return CreateFailure(in slot, descriptor.Mnemonic, "Saturating", "the Saturating flag is outside the scoped VADD contour.");
        }

        if (instruction.Reduction && !OpcodeRegistry.IsReductionOp(slot.Opcode))
        {
            return CreateFailure(in slot, descriptor.Mnemonic, "Reduction", "the Reduction flag requires a reduction opcode.");
        }

        if (instruction.Indexed && !hasVectorPayload)
        {
            return CreateFailure(in slot, descriptor.Mnemonic, "Indexed", "the Indexed flag requires a vector payload opcode.");
        }

        if (instruction.Is2D && !hasVectorPayload)
        {
            return CreateFailure(in slot, descriptor.Mnemonic, "Is2D", "the Is2D flag requires a vector payload opcode.");
        }

        if ((instruction.TailAgnostic || instruction.MaskAgnostic) && !hasVectorPayload)
        {
            return CreateFailure(in slot, descriptor.Mnemonic, "TailMaskAgnostic", "tail/mask agnostic flags require a vector payload opcode.");
        }

        return null;
    }

    private static DecodeFailure? ValidateFencePayload(
        in RawSlot slot,
        in GeneratedIsaDescriptor descriptor)
    {
        if (slot.Opcode is not (
                Processor.CPU_Core.IsaOpcodeValues.FENCE or
                Processor.CPU_Core.IsaOpcodeValues.FENCE_I))
        {
            return null;
        }

        ulong flags = (slot.Word0 >> 16) & 0xFFUL;
        ulong word3Payload = slot.Word3 & ~(0x3UL << 48);
        if (slot.Instruction.Immediate == 0 &&
            slot.Instruction.PredicateMask == 0 &&
            flags == 0 &&
            slot.Word1 == 0 &&
            slot.Word2 == 0 &&
            word3Payload == 0)
        {
            return null;
        }

        return CreateFailure(
            in slot,
            descriptor.Mnemonic,
            "FencePayload",
            "FENCE/FENCE.I accepts only the canonical zero-payload form.");
    }

    private sealed class RegisterConstraint : IEncodingConstraint
    {
        private readonly HashSet<ushort> _opcodes;
        private readonly bool _requireRs1Zero;
        private readonly bool _requireRs2Zero;
        private readonly bool _requireImmediateZero;
        private readonly ushort? _maximumImmediate;

        private RegisterConstraint(
            string id,
            bool requireRs1Zero,
            bool requireRs2Zero,
            bool requireImmediateZero,
            ushort? maximumImmediate,
            params ushort[] opcodes)
        {
            Id = id;
            _requireRs1Zero = requireRs1Zero;
            _requireRs2Zero = requireRs2Zero;
            _requireImmediateZero = requireImmediateZero;
            _maximumImmediate = maximumImmediate;
            _opcodes = new HashSet<ushort>(opcodes);
        }

        public string Id { get; }

        public static RegisterConstraint RequireRs2Zero(string id, params ushort[] opcodes) =>
            new(id, false, true, false, null, opcodes);

        public static RegisterConstraint RequireRs2AndImmediateZero(string id, params ushort[] opcodes) =>
            new(id, false, true, true, null, opcodes);

        public static RegisterConstraint RequireImmediateZero(string id, params ushort[] opcodes) =>
            new(id, false, false, true, null, opcodes);

        public static RegisterConstraint RequireRs2ZeroAndImmediateAtMost(
            string id,
            ushort maximumImmediate,
            params ushort[] opcodes) =>
            new(id, false, true, false, maximumImmediate, opcodes);

        public static RegisterConstraint RequireRs1Rs2AndImmediateZero(string id, params ushort[] opcodes) =>
            new(id, true, true, true, null, opcodes);

        public bool AppliesTo(ushort opcode) => _opcodes.Contains(opcode);

        public bool TryValidate(
            in RawSlot slot,
            in GeneratedIsaDescriptor descriptor,
            in DecodedOperandFields operands,
            out DecodeFailure? failure)
        {
            if (_requireRs1Zero && operands.Rs1 != 0)
            {
                failure = CreateFailure(in slot, descriptor.Mnemonic, "rs1", "requires rs1=x0.");
                return false;
            }

            if (_requireRs2Zero && operands.Rs2 != 0)
            {
                failure = CreateFailure(in slot, descriptor.Mnemonic, "rs2", "requires rs2=x0.");
                return false;
            }

            ushort immediate = slot.Instruction.Immediate;
            if (_requireImmediateZero && immediate != 0)
            {
                failure = CreateFailure(in slot, descriptor.Mnemonic, "Immediate", "requires Immediate=0.");
                return false;
            }

            if (_maximumImmediate.HasValue && immediate > _maximumImmediate.Value)
            {
                failure = CreateFailure(
                    in slot,
                    descriptor.Mnemonic,
                    "Immediate",
                    $"requires Immediate in [0, {_maximumImmediate.Value}].");
                return false;
            }

            failure = null;
            return true;
        }
    }

    private sealed class ControlFlowTargetTransportConstraint : IEncodingConstraint
    {
        public string Id => "control-flow-no-legacy-src2-target";

        public bool AppliesTo(ushort opcode) => OpcodeRegistry.IsControlFlowOp(opcode);

        public bool TryValidate(
            in RawSlot slot,
            in GeneratedIsaDescriptor descriptor,
            in DecodedOperandFields operands,
            out DecodeFailure? failure)
        {
            if (slot.Instruction.Src2Pointer == 0)
            {
                failure = null;
                return true;
            }

            failure = CreateFailure(
                in slot,
                descriptor.Mnemonic,
                "Src2Pointer",
                "uses the retired legacy control-flow target transport; target displacement is encoded in Immediate.");
            return false;
        }
    }

    /// <summary>
    /// The queue-control DSC opcodes are register ABI variants, not descriptor
    /// carriers. Their fixed source-zero and clean-carrier fields are static
    /// encoding legality, while queue state remains runtime authority.
    /// </summary>
    private sealed class DscQueueControlConstraint : IEncodingConstraint
    {
        public string Id => "dsc-queue-control-clean-register-abi";

        public bool AppliesTo(ushort opcode) => opcode is
            Processor.CPU_Core.IsaOpcodeValues.DSC_STATUS or
            Processor.CPU_Core.IsaOpcodeValues.DSC_QUERY_CAPS;

        public bool TryValidate(
            in RawSlot slot,
            in GeneratedIsaDescriptor descriptor,
            in DecodedOperandFields operands,
            out DecodeFailure? failure)
        {
            if (slot.Opcode == Processor.CPU_Core.IsaOpcodeValues.DSC_STATUS && operands.Rs2 != 0)
            {
                failure = CreateFailure(in slot, descriptor.Mnemonic, "rs2", "DSC_STATUS requires rs2=x0.");
                return false;
            }

            if (slot.Opcode == Processor.CPU_Core.IsaOpcodeValues.DSC_QUERY_CAPS &&
                (operands.Rs1 != 0 || operands.Rs2 != 0))
            {
                failure = CreateFailure(in slot, descriptor.Mnemonic, "rs1/rs2", "DSC_QUERY_CAPS requires rs1=x0 and rs2=x0.");
                return false;
            }

            VLIW_Instruction instruction = slot.Instruction;
            if (instruction.VirtualThreadId != 0 ||
                instruction.Immediate != 0 ||
                instruction.PredicateMask != 0 ||
                instruction.Acquire ||
                instruction.Release ||
                instruction.Saturating ||
                instruction.MaskAgnostic ||
                instruction.TailAgnostic ||
                instruction.Indexed ||
                instruction.Is2D ||
                instruction.Reduction)
            {
                failure = CreateFailure(
                    in slot,
                    descriptor.Mnemonic,
                    "QueueControlPayload",
                    "DSC queue-control opcodes require a clean packed-register carrier.");
                return false;
            }

            failure = null;
            return true;
        }
    }

    private static DecodeFailure CreateFailure(
        in RawSlot slot,
        string mnemonic,
        string field,
        string requirement) =>
        DecodeFailure.Create(
            DecodeFailureCode.ReservedEncoding,
            slot.SlotIndex,
            field,
            RawSlotBytes(in slot),
            $"Opcode '{mnemonic}' (slot {slot.SlotIndex}) violates '{field}' encoding constraint: {requirement}");

    private static byte[] RawSlotBytes(in RawSlot slot)
    {
        byte[] bytes = new byte[sizeof(ulong) * 4];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, sizeof(ulong)), slot.Word0);
        BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(ulong), sizeof(ulong)), slot.Word1);
        BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(ulong) * 2, sizeof(ulong)), slot.Word2);
        BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(ulong) * 3, sizeof(ulong)), slot.Word3);
        return bytes;
    }
}

/// <summary>
/// RF-05 extension-payload projection.  It consumes only the frozen raw slot
/// and frozen compiler transport sideband; it neither grants execution nor
/// performs matrix/materialization work.
/// </summary>
public static class ExtensionPayloadDecoder
{
    public static bool TryDecode(
        in RawSlot slot,
        in GeneratedIsaDescriptor descriptor,
        InstructionSlotMetadata slotMetadata,
        out VectorInstructionPayload? vectorPayload,
        out DecodeFailure? failure)
    {
        if (!OpcodeRegistry.RequiresVectorPayloadProjection(slot.Opcode))
        {
            vectorPayload = null;
            failure = null;
            return true;
        }

        vectorPayload = new VectorInstructionPayload(
            slot.Instruction.DestSrc1Pointer,
            slot.Instruction.Src2Pointer,
            slot.Instruction.StreamLength,
            slot.Instruction.Stride,
            slot.Instruction.RowStride,
            slot.Instruction.Indexed,
            slot.Instruction.Is2D,
            slot.Instruction.TailAgnostic,
            slot.Instruction.MaskAgnostic,
            slot.Instruction.Saturating,
            slot.Instruction.PredicateMask,
            slot.Instruction.DataType)
        {
            MatrixTileNumericPolicy = slotMetadata.MatrixTileNumericPolicy,
            MatrixTileLayoutPolicy = slotMetadata.MatrixTileLayoutPolicy,
        };
        failure = null;
        return true;
    }
}

/// <summary>
/// RF-05 static sideband-placement validator.  It owns only carrier-to-opcode
/// association and fixed lane placement. Descriptor parsing, owner/domain
/// guards and other state-dependent checks deliberately remain outside it.
/// </summary>
public static class SidebandValidator
{
    /// <summary>
    /// Stable identities for the static descriptor-carrier rules owned by this
    /// validator. They are diagnostic/test identities, not a configuration
    /// surface and not an authority for runtime descriptor admission.
    /// </summary>
    public static IReadOnlyList<string> RegisteredStaticRuleIds { get; } =
    [
        "dma-descriptor-opcode-association",
        "accelerator-descriptor-opcode-association",
        "dma-fixed-lane",
        "dma-required-descriptor",
        "dma-native-carrier-abi",
        "dsc-queue-control-fixed-lane",
        "accelerator-submit-required-descriptor",
        "accelerator-native-carrier-abi",
    ];

    public static bool TryValidate(
        in RawSlot slot,
        in GeneratedIsaDescriptor descriptor,
        InstructionSlotMetadata slotMetadata,
        out DecodeFailure? failure)
    {
        bool hasDmaDescriptor = slotMetadata.DmaStreamComputeDescriptor is not null;
        bool hasAcceleratorDescriptor = slotMetadata.AcceleratorCommandDescriptor is not null;
        ushort opcode = slot.Opcode;
        VLIW_Instruction instruction = slot.Instruction;

        if (hasDmaDescriptor && opcode != Processor.CPU_Core.IsaOpcodeValues.DmaStreamCompute)
        {
            failure = CreateFailure(
                in slot,
                descriptor.Mnemonic,
                "DmaStreamComputeDescriptor",
                "the descriptor sideband may accompany only the native DmaStreamCompute opcode.");
            return false;
        }

        if (hasAcceleratorDescriptor && opcode != Processor.CPU_Core.IsaOpcodeValues.ACCEL_SUBMIT)
        {
            failure = CreateFailure(
                in slot,
                descriptor.Mnemonic,
                "AcceleratorCommandDescriptor",
                "the descriptor sideband may accompany only ACCEL_SUBMIT.");
            return false;
        }

        if (opcode == Processor.CPU_Core.IsaOpcodeValues.DmaStreamCompute)
        {
            if (slot.SlotIndex != 6)
            {
                failure = CreateFailure(in slot, descriptor.Mnemonic, "slot", "DmaStreamCompute is fixed to lane 6.");
                return false;
            }

            if (!hasDmaDescriptor)
            {
                failure = CreateFailure(
                    in slot,
                    descriptor.Mnemonic,
                    "DmaStreamComputeDescriptor",
                    "the native carrier requires a typed descriptor sideband.");
                return false;
            }

            if (!DmaStreamComputeDescriptorParser.TryValidateNativeVliwCarrier(
                    in instruction,
                    slot.SlotIndex,
                    hasDmaDescriptor,
                    out DmaStreamComputeValidationResult? dmaFailure))
            {
                failure = CreateFailure(
                    in slot,
                    descriptor.Mnemonic,
                    "DmaStreamComputeCarrier",
                    dmaFailure!.Message);
                return false;
            }
        }

        if (opcode is Processor.CPU_Core.IsaOpcodeValues.DSC_STATUS or
            Processor.CPU_Core.IsaOpcodeValues.DSC_QUERY_CAPS)
        {
            if (slot.SlotIndex != 6)
            {
                failure = CreateFailure(in slot, descriptor.Mnemonic, "slot", "the DSC queue-control carrier is fixed to lane 6.");
                return false;
            }

            if (hasDmaDescriptor)
            {
                failure = CreateFailure(
                    in slot,
                    descriptor.Mnemonic,
                    "DmaStreamComputeDescriptor",
                    "the DSC queue-control carrier cannot carry a DmaStreamCompute descriptor.");
                return false;
            }
        }

        if (opcode == Processor.CPU_Core.IsaOpcodeValues.ACCEL_SUBMIT && !hasAcceleratorDescriptor)
        {
            failure = CreateFailure(
                in slot,
                descriptor.Mnemonic,
                "AcceleratorCommandDescriptor",
                "ACCEL_SUBMIT requires a typed descriptor sideband.");
            return false;
        }

        if (OpcodeRegistry.IsSystemDeviceCommandOpcode(opcode) &&
            !AcceleratorDescriptorParser.TryValidateNativeVliwCarrier(
                in instruction,
                opcode,
                slot.SlotIndex,
                hasAcceleratorDescriptor,
                out AcceleratorCarrierValidationResult? acceleratorFailure))
        {
            failure = CreateFailure(
                in slot,
                descriptor.Mnemonic,
                "AcceleratorCarrier",
                acceleratorFailure!.Message);
            return false;
        }

        failure = null;
        return true;
    }

    private static DecodeFailure CreateFailure(
        in RawSlot slot,
        string mnemonic,
        string field,
        string message) =>
        DecodeFailure.Create(
            DecodeFailureCode.Sideband,
            slot.SlotIndex,
            field,
            RawSlotBytes(in slot),
            $"Opcode '{mnemonic}' (slot {slot.SlotIndex}) has illegal sideband placement: {message}");

    private static byte[] RawSlotBytes(in RawSlot slot)
    {
        byte[] bytes = new byte[sizeof(ulong) * 4];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, sizeof(ulong)), slot.Word0);
        BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(ulong), sizeof(ulong)), slot.Word1);
        BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(ulong) * 2, sizeof(ulong)), slot.Word2);
        BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(ulong) * 3, sizeof(ulong)), slot.Word3);
        return bytes;
    }
}

/// <summary>
/// Immutable, pre-materialization semantic payload emitted by the RF-05
/// decoder stages. It is intentionally not an <see cref="InstructionIR"/>:
/// the legacy IR remains a compatibility projection until the cutover gate.
/// </summary>
public sealed record DeclarativeInstructionSemantics(
    ushort Opcode,
    InstructionClass InstructionClass,
    SerializationClass SerializationClass,
    byte Rd,
    byte Rs1,
    byte Rs2,
    long Immediate,
    VectorInstructionPayload? VectorPayload);

/// <summary>
/// Final RF-05 stage for one occupied slot. It creates an immutable canonical
/// contract from prior stage outputs and never reconstructs a mutable legacy
/// <see cref="InstructionIR"/>.
/// </summary>
public static class CanonicalInstructionIrBuilder
{
    public static bool TryBuild(
        in RawSlot slot,
        in GeneratedIsaDescriptor descriptor,
        in DecodedOperandFields operands,
        VectorInstructionPayload? vectorPayload,
        InstructionSlotMetadata slotMetadata,
        out CanonicalDecodedInstruction canonicalInstruction,
        out DecodeFailure? failure)
    {
        if (slot.Opcode == 0)
        {
            canonicalInstruction = default!;
            failure = DecodeFailure.Create(
                DecodeFailureCode.BundleShape,
                slot.SlotIndex,
                "opcode",
                RawSlotBytes(in slot),
                "CanonicalInstructionIrBuilder accepts occupied slots only.");
            return false;
        }

        (InstructionClass instructionClass, SerializationClass serializationClass) =
            InstructionClassifier.Classify(slot.Opcode);
        var semantics = new DeclarativeInstructionSemantics(
            slot.Opcode,
            instructionClass,
            serializationClass,
            operands.Rd,
            operands.Rs1,
            operands.Rs2,
            operands.Immediate,
            vectorPayload);
        byte[] rawBytes = RawSlotBytes(in slot);
        canonicalInstruction = new CanonicalDecodedInstruction(
            SlotIndex: slot.SlotIndex,
            IsOccupied: true,
            Opcode: slot.Opcode,
            InstructionClass: instructionClass,
            SerializationClass: serializationClass,
            Rd: operands.Rd,
            Rs1: operands.Rs1,
            Rs2: operands.Rs2,
            Immediate: operands.Immediate,
            CsrAddress: ResolveCsrAddress(instructionClass, slot.Opcode, operands.Immediate),
            AcquireOrdering: slot.Instruction.Acquire,
            ReleaseOrdering: slot.Instruction.Release,
            RawSlot: CanonicalPayloadSnapshot.FromBytes("VLIW_Instruction", rawBytes),
            InstructionPayload: CanonicalPayloadSnapshot.FromObject("DeclarativeInstructionSemantics", semantics),
            SlotSideband: CanonicalPayloadSnapshot.FromObject("InstructionSlotMetadata", slotMetadata)) with
        {
            StaticBinding = GeneratedStaticBinding.FromDescriptor(in descriptor),
        };
        failure = null;
        return true;
    }

    private static byte[] RawSlotBytes(in RawSlot slot)
    {
        byte[] bytes = new byte[sizeof(ulong) * 4];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, sizeof(ulong)), slot.Word0);
        BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(ulong), sizeof(ulong)), slot.Word1);
        BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(ulong) * 2, sizeof(ulong)), slot.Word2);
        BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(ulong) * 3, sizeof(ulong)), slot.Word3);
        return bytes;
    }

    private static ushort? ResolveCsrAddress(
        InstructionClass instructionClass,
        ushort opcode,
        long immediate) =>
        opcode == Processor.CPU_Core.IsaOpcodeValues.RDCYCLE
            ? CsrAddresses.Cycle
            : instructionClass == InstructionClass.Csr
                ? (ushort)(immediate & 0xFFF)
                : null;
}

/// <summary>
/// Frozen output of the complete RF-05 occupied-slot pipeline. The public
/// facade consumes this only in shadow mode until the differential gate closes.
/// </summary>
public sealed record DeclarativeDecodedSlot(
    GeneratedIsaDescriptor Descriptor,
    DecodedOperandFields Operands,
    VectorInstructionPayload? VectorPayload,
    CanonicalDecodedInstruction CanonicalInstruction);

/// <summary>
/// Frozen result of the RF-05 bundle pipeline. Empty physical slots remain
/// explicit canonical slots and do not carry a synthetic semantic instruction.
/// </summary>
public sealed record DeclarativeDecodedBundle(
    ImmutableArray<DeclarativeDecodedSlot?> Slots,
    CanonicalBundle CanonicalBundle);

/// <summary>
/// One declarative, non-materializing decode path for a single occupied slot.
/// Expected illegal input is returned as <see cref="DecodeFailure"/>; only a
/// programming invariant may escape this boundary as an exception.
/// </summary>
public static class DeclarativeDecoderPipeline
{
    public static bool TryDecodeBundle(
        ReadOnlySpan<VLIW_Instruction> bundle,
        VliwBundleAnnotations? bundleAnnotations,
        ulong bundleAddress,
        ulong bundleSerial,
        out DeclarativeDecodedBundle? decodedBundle,
        out DecodeFailure? failure)
    {
        return TryDecodeBundle(
            bundle,
            bundleAnnotations,
            bundleAddress,
            bundleSerial,
            decodeContext: null,
            out decodedBundle,
            out failure);
    }

    public static bool TryDecodeBundle(
        ReadOnlySpan<VLIW_Instruction> bundle,
        VliwBundleAnnotations? bundleAnnotations,
        ulong bundleAddress,
        ulong bundleSerial,
        CanonicalDecodeContext? decodeContext,
        out DeclarativeDecodedBundle? decodedBundle,
        out DecodeFailure? failure)
    {
        if (bundle.Length != BundleMetadata.BundleSlotCount)
        {
            decodedBundle = null;
            failure = DecodeFailure.Create(
                DecodeFailureCode.BundleShape,
                -1,
                "bundle",
                ReadOnlySpan<byte>.Empty,
                $"Decoder bundles must contain exactly {BundleMetadata.BundleSlotCount} slots.");
            return false;
        }

        RawBundle rawBundle = RawBundleReader.Read(bundle);
        var decodedSlots = ImmutableArray.CreateBuilder<DeclarativeDecodedSlot?>(BundleMetadata.BundleSlotCount);
        var canonicalSlots = ImmutableArray.CreateBuilder<CanonicalDecodedInstruction>(BundleMetadata.BundleSlotCount);

        foreach (RawSlot slot in rawBundle.Slots)
        {
            InstructionSlotMetadata slotMetadata = ResolveSlotMetadata(bundleAnnotations, slot.SlotIndex);
            if (slot.Opcode == 0)
            {
                if (!TryDecodeEmptySlot(in slot, slotMetadata, out CanonicalDecodedInstruction emptySlot, out failure))
                {
                    decodedBundle = null;
                    return false;
                }

                decodedSlots.Add(null);
                canonicalSlots.Add(emptySlot);
                continue;
            }

            if (!TryDecodeOccupiedSlot(in slot, slotMetadata, out DeclarativeDecodedSlot? decodedSlot, out failure))
            {
                decodedBundle = null;
                return false;
            }

            decodedSlots.Add(decodedSlot);
            canonicalSlots.Add(decodedSlot!.CanonicalInstruction);
        }

        BundleMetadata bundleMetadata = bundleAnnotations?.BundleMetadata ?? BundleMetadata.Default;
        decodedBundle = new DeclarativeDecodedBundle(
            decodedSlots.MoveToImmutable(),
            CanonicalBundle.CreateFromCanonicalSlots(
                bundle,
                canonicalSlots.MoveToImmutable(),
                bundleMetadata,
                bundleAddress,
                bundleSerial,
                decodeContext));
        failure = null;
        return true;
    }

    public static bool TryDecodeOccupiedSlot(
        in RawSlot slot,
        InstructionSlotMetadata slotMetadata,
        out DeclarativeDecodedSlot? decodedSlot,
        out DecodeFailure? failure)
    {
        if (slot.Opcode == 0)
        {
            decodedSlot = null;
            failure = DecodeFailure.Create(
                DecodeFailureCode.BundleShape,
                slot.SlotIndex,
                "opcode",
                RawSlotBytes(in slot),
                "Declarative occupied-slot decode does not accept the NOP sentinel.");
            return false;
        }

        if (!OpcodeDescriptorLookup.TryLookup(in slot, out GeneratedIsaDescriptor descriptor, out failure) ||
            !EncodingConstraintValidator.TryValidateRawForm(in slot, in descriptor, out failure) ||
            !OperandDecoder.TryDecode(in slot, in descriptor, out DecodedOperandFields operands, out failure) ||
            !EncodingConstraintValidator.TryValidate(in slot, in descriptor, in operands, out failure) ||
            !ExtensionPayloadDecoder.TryDecode(in slot, in descriptor, slotMetadata, out VectorInstructionPayload? vectorPayload, out failure) ||
            !SidebandValidator.TryValidate(in slot, in descriptor, slotMetadata, out failure) ||
            !CanonicalInstructionIrBuilder.TryBuild(
                in slot,
                in descriptor,
                in operands,
                vectorPayload,
                slotMetadata,
                out CanonicalDecodedInstruction canonicalInstruction,
                out failure))
        {
            decodedSlot = null;
            return false;
        }

        decodedSlot = new DeclarativeDecodedSlot(descriptor, operands, vectorPayload, canonicalInstruction);
        failure = null;
        return true;
    }

    private static byte[] RawSlotBytes(in RawSlot slot)
    {
        byte[] bytes = new byte[sizeof(ulong) * 4];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, sizeof(ulong)), slot.Word0);
        BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(ulong), sizeof(ulong)), slot.Word1);
        BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(ulong) * 2, sizeof(ulong)), slot.Word2);
        BitConverter.TryWriteBytes(bytes.AsSpan(sizeof(ulong) * 3, sizeof(ulong)), slot.Word3);
        return bytes;
    }

    private static bool TryDecodeEmptySlot(
        in RawSlot slot,
        InstructionSlotMetadata slotMetadata,
        out CanonicalDecodedInstruction canonicalInstruction,
        out DecodeFailure? failure)
    {
        if (slot.Word0 != 0 || slot.Word1 != 0 || slot.Word2 != 0 || slot.Word3 != 0)
        {
            canonicalInstruction = default!;
            failure = DecodeFailure.Create(
                DecodeFailureCode.BundleShape,
                slot.SlotIndex,
                "empty-slot",
                RawSlotBytes(in slot),
                "Empty/NOP VLIW slots must use the canonical all-zero carrier.");
            return false;
        }

        if (slotMetadata.DmaStreamComputeDescriptor is not null ||
            slotMetadata.AcceleratorCommandDescriptor is not null)
        {
            canonicalInstruction = default!;
            failure = DecodeFailure.Create(
                DecodeFailureCode.Sideband,
                slot.SlotIndex,
                "DescriptorSideband",
                RawSlotBytes(in slot),
                "Descriptor sideband cannot accompany an empty/NOP VLIW slot.");
            return false;
        }

        canonicalInstruction = new CanonicalDecodedInstruction(
            SlotIndex: slot.SlotIndex,
            IsOccupied: false,
            Opcode: 0,
            InstructionClass: null,
            SerializationClass: null,
            Rd: 0,
            Rs1: 0,
            Rs2: 0,
            Immediate: 0,
            CsrAddress: null,
            AcquireOrdering: false,
            ReleaseOrdering: false,
            RawSlot: CanonicalPayloadSnapshot.FromBytes("VLIW_Instruction", RawSlotBytes(in slot)),
            InstructionPayload: CanonicalPayloadSnapshot.FromObject("DeclarativeInstructionSemantics", null),
            SlotSideband: CanonicalPayloadSnapshot.FromObject("InstructionSlotMetadata", slotMetadata));
        failure = null;
        return true;
    }

    private static InstructionSlotMetadata ResolveSlotMetadata(
        VliwBundleAnnotations? bundleAnnotations,
        int slotIndex)
    {
        if (bundleAnnotations is not null &&
            bundleAnnotations.TryGetInstructionSlotMetadata(slotIndex, out InstructionSlotMetadata metadata))
        {
            return metadata;
        }

        return InstructionSlotMetadata.Default;
    }
}
