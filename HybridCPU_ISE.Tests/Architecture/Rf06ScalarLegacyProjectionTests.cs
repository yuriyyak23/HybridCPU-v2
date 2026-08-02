using System.IO;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf06ScalarLegacyProjectionTests
{
    [Theory]
    [InlineData(IsaOpcodeValues.ADD, InternalOpKind.Add)]
    [InlineData(IsaOpcodeValues.SUB, InternalOpKind.Sub)]
    [InlineData(IsaOpcodeValues.AND, InternalOpKind.And)]
    [InlineData(IsaOpcodeValues.OR, InternalOpKind.Or)]
    [InlineData(IsaOpcodeValues.XOR, InternalOpKind.Xor)]
    public void CheckedProjection_MatchesInternalOpBuilderAndExistingScalarCarrier(
        ushort opcode,
        InternalOpKind expectedKind)
    {
        (CanonicalDecodedInstruction canonical, InstructionIR instruction) = Decode(opcode);
        ExecutionContract contract = Rf06ScalarLegacyProjection.CreateContract(canonical);
        CheckedScalarLegacyProjection projection = Rf06ScalarLegacyProjection.Project(canonical, contract);

        DecoderContext context = new()
        {
            OpCode = opcode,
            Reg1ID = canonical.Rd,
            Reg2ID = canonical.Rs1,
            Reg3ID = canonical.Rs2,
        };
        ScalarALUMicroOp existing = Assert.IsType<ScalarALUMicroOp>(
            InstructionRegistry.CreateMicroOp(opcode, context));
        InternalOp internalOp = new InternalOpBuilder().Build(instruction);

        projection.EnsureCurrent();
        Assert.Same(canonical.StaticBinding, contract.GeneratedBinding);
        Assert.Equal(expectedKind, internalOp.Kind);
        Assert.Equal(instruction.Rd, internalOp.Rd);
        Assert.Equal(instruction.Rs1, internalOp.Rs1);
        Assert.Equal(instruction.Rs2, internalOp.Rs2);

        Assert.Equal(existing.OpCode, projection.Carrier.OpCode);
        Assert.Equal(existing.DestRegID, projection.Carrier.DestRegID);
        Assert.Equal(existing.Src1RegID, projection.Carrier.Src1RegID);
        Assert.Equal(existing.Src2RegID, projection.Carrier.Src2RegID);
        Assert.Equal(existing.Immediate, projection.Carrier.Immediate);
        Assert.Equal(existing.UsesImmediate, projection.Carrier.UsesImmediate);
        Assert.Equal(existing.WritesRegister, projection.Carrier.WritesRegister);
        Assert.Equal(existing.ReadRegisters, projection.Carrier.ReadRegisters);
        Assert.Equal(existing.WriteRegisters, projection.Carrier.WriteRegisters);
        Assert.Equal(existing.ResourceMask, projection.Carrier.ResourceMask);
        Assert.Equal(contract.ReadRegisters, projection.Carrier.ReadRegisters);
        Assert.Equal(contract.WriteRegisters, projection.Carrier.WriteRegisters);
        Assert.Equal(contract.ResourceMask, projection.Carrier.ResourceMask);
    }

    [Fact]
    public void CheckedProjection_RejectsCarrierMutation()
    {
        (CanonicalDecodedInstruction canonical, _) = Decode(IsaOpcodeValues.ADD);
        CheckedScalarLegacyProjection projection = Rf06ScalarLegacyProjection.Project(
            canonical,
            Rf06ScalarLegacyProjection.CreateContract(canonical));

        projection.Carrier.Src1RegID = 7;

        Assert.Throws<InvalidOperationException>(() => projection.EnsureCurrent());
    }

    [Fact]
    public void CheckedProjection_RejectsContractFromAnotherGeneratedBinding()
    {
        (CanonicalDecodedInstruction add, _) = Decode(IsaOpcodeValues.ADD);
        (CanonicalDecodedInstruction sub, _) = Decode(IsaOpcodeValues.SUB);
        ExecutionContract subContract = Rf06ScalarLegacyProjection.CreateContract(sub);

        Assert.Throws<InvalidOperationException>(() =>
            Rf06ScalarLegacyProjection.Project(add, subContract));
    }

    [Fact]
    public void CheckedRegisterMaskCutover_PreservesEveryValidCanonicalOperandValue()
    {
        CanonicalDecodedInstruction canonical = Decode(IsaOpcodeValues.ADD).Canonical;

        for (int rawValue = 0; rawValue <= 31; rawValue++)
        {
            byte value = (byte)rawValue;
            AssertContractMaskParity(canonical with { Rd = value, Rs1 = 0, Rs2 = 0 });
            AssertContractMaskParity(canonical with { Rd = 0, Rs1 = value, Rs2 = 0 });
            AssertContractMaskParity(canonical with { Rd = 0, Rs1 = 0, Rs2 = value });
        }
    }

    [Fact]
    public void DirectCanonicalProjection_RejectsEveryInvalidOrAbsentOperandBeforeContractUse()
    {
        CanonicalDecodedInstruction canonical = Decode(IsaOpcodeValues.ADD).Canonical;
        ExecutionContract validContract = Rf06ScalarLegacyProjection.CreateContract(canonical);

        for (int rawValue = 32; rawValue <= byte.MaxValue; rawValue++)
        {
            byte value = (byte)rawValue;
            AssertInvalidCanonicalOperand(canonical with { Rd = value, Rs1 = 0, Rs2 = 0 }, validContract);
            AssertInvalidCanonicalOperand(canonical with { Rd = 0, Rs1 = value, Rs2 = 0 }, validContract);
            AssertInvalidCanonicalOperand(canonical with { Rd = 0, Rs1 = 0, Rs2 = value }, validContract);
        }
    }

    [Fact]
    public void ScalarProjection_IsNotASecondSchedulerOrCompatibilityProjector()
    {
        DirectoryInfo root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            root.FullName,
            "HybridCPU_ISE",
            "Legacy",
            "CloseToHSL",
            "Core",
            "Decoder",
            "Rf06ScalarLegacyProjection.cs"));

        Assert.DoesNotContain("OpcodeRegistry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetInfo(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateMicroOp(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AdmissionRecord", source, StringComparison.Ordinal);
        Assert.DoesNotContain("VliwOperationId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplayEntry", source, StringComparison.Ordinal);
    }

    private static void AssertContractMaskParity(CanonicalDecodedInstruction canonical)
    {
        ResourceBitset expected =
            ResourceMaskBuilder.ForRegisterRead(canonical.Rs1) |
            ResourceMaskBuilder.ForRegisterRead(canonical.Rs2) |
            ResourceMaskBuilder.ForRegisterWrite(canonical.Rd);

        Assert.Equal(expected, Rf06ScalarLegacyProjection.CreateContract(canonical).ResourceMask);
    }

    private static void AssertInvalidCanonicalOperand(
        CanonicalDecodedInstruction canonical,
        ExecutionContract validContract)
    {
        InvalidOperationException contractFailure = Assert.Throws<InvalidOperationException>(
            () => Rf06ScalarLegacyProjection.CreateContract(canonical));
        InvalidOperationException projectionFailure = Assert.Throws<InvalidOperationException>(
            () => Rf06ScalarLegacyProjection.Project(canonical, validContract));

        Assert.Equal(contractFailure.Message, projectionFailure.Message);
        Assert.Contains("requires present rd, rs1 and rs2 architectural registers in x0..x31",
            contractFailure.Message, StringComparison.Ordinal);
    }

    private static (CanonicalDecodedInstruction Canonical, InstructionIR Instruction) Decode(ushort opcode)
    {
        var raw = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        raw[0] = new VLIW_Instruction
        {
            OpCode = opcode,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(3, 1, 2),
        };

        var decoded = new VliwDecoderV4().DecodeInstructionBundle(raw, 0x4000, 9);
        CanonicalBundle canonicalBundle = Assert.IsType<CanonicalBundle>(decoded.CanonicalBundle);
        return (canonicalBundle.GetSlot(0), decoded.GetDecodedSlot(0).RequireInstruction());
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests")))
            {
                return current;
            }
        }

        throw new DirectoryNotFoundException("HybridCPU ISE repository root was not found.");
    }
}
