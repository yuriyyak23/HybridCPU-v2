using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Arch;
using System;
using System.Linq;
using System.Reflection;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.InstructionsRefactor;

public sealed class OpcodeEnumValueParityTests
{
    [Fact]
    public void IsaOpcodeValues_MirrorEveryInstructionEnumValue()
    {
        FieldInfo[] valueFields = typeof(IsaOpcodeValues)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } &&
                            field.FieldType == typeof(ushort))
            .ToArray();
        string[] valueNames = valueFields.Select(field => field.Name).ToArray();

        foreach (InstructionsEnum opcode in Enum.GetValues<InstructionsEnum>())
        {
            Assert.Contains(opcode.ToString(), valueNames);
        }
    }

    [Fact]
    public void IsaOpcodeValues_KeepNumericParityWithInstructionEnum()
    {
        foreach (FieldInfo field in typeof(IsaOpcodeValues)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } &&
                            field.FieldType == typeof(ushort)))
        {
            Assert.True(
                Enum.TryParse(field.Name, out InstructionsEnum opcode),
                $"IsaOpcodeValues.{field.Name} has no InstructionsEnum peer.");

            Assert.Equal((ushort)opcode, Assert.IsType<ushort>(field.GetRawConstantValue()));
        }
    }
}

public sealed class OpcodeClassifierParityTests
{
    [Fact]
    public void OpcodeRegistry_PublishedSemantics_AgreeWithInstructionClassifier()
    {
        foreach (OpcodeInfo info in OpcodeRegistry.Opcodes)
        {
            (InstructionClass instructionClass, SerializationClass serializationClass) =
                InstructionClassifier.Classify((ushort)info.OpCode);

            Assert.Equal(info.InstructionClass, instructionClass);
            Assert.Equal(info.SerializationClass, serializationClass);
        }
    }
}

public sealed class OpcodeRegistryCoverageTests
{
    [Fact]
    public void MissingMandatoryInteger64RepairSurface_IsInventoryOnlyUntilOpcodeAllocation()
    {
        foreach (string mnemonic in InstructionSupportStatusCatalog.MandatoryInteger64RepairMnemonics)
        {
            if (string.Equals(mnemonic, "SRA", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mnemonic, "ADDIW", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mnemonic, "ADDW", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mnemonic, "SUBW", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mnemonic, "SLLW", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mnemonic, "SRLW", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mnemonic, "SRAW", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mnemonic, "SLLIW", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mnemonic, "SRLIW", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mnemonic, "SRAIW", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mnemonic, "MULW", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mnemonic, "DIVW", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mnemonic, "DIVUW", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mnemonic, "REMW", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mnemonic, "REMUW", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mnemonic, "SEXT.W", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mnemonic, "ZEXT.W", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);

            Assert.Equal(IsaInstructionStatus.Mandatory, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.None, status.RuntimeEvidence);
            Assert.False(status.HasNumericOpcode);
            Assert.False(status.HasRuntimeOpcodeMetadata);
            Assert.False(status.HasCanonicalDecoderAcceptance);
            Assert.False(status.HasRegistryFactory);
            Assert.False(status.HasExecutionSemantics);
            Assert.False(HasEnumOrRegistryMnemonic(mnemonic));
        }
    }

    [Fact]
    public void ScalarSraSupportStatus_ReportsClosedRuntimeGate()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("SRA");

        Assert.Equal(IsaInstructionStatus.Mandatory, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);
        Assert.Equal((ushort)InstructionsEnum.SRA, IsaOpcodeValues.SRA);
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.SRA));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.SRA));
    }

    [Fact]
    public void ScalarAddiwSupportStatus_ReportsClosedRuntimeGate()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("ADDIW");

        Assert.Equal(IsaInstructionStatus.Mandatory, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);
        Assert.Equal((ushort)InstructionsEnum.ADDIW, IsaOpcodeValues.ADDIW);
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.ADDIW));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.ADDIW));
    }

    [Fact]
    public void ScalarAddwSupportStatus_ReportsClosedRuntimeGate()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("ADDW");

        Assert.Equal(IsaInstructionStatus.Mandatory, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);
        Assert.Equal((ushort)InstructionsEnum.ADDW, IsaOpcodeValues.ADDW);
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.ADDW));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.ADDW));
    }

    [Fact]
    public void ScalarSubwSupportStatus_ReportsClosedRuntimeGate()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("SUBW");

        Assert.Equal(IsaInstructionStatus.Mandatory, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);
        Assert.Equal((ushort)InstructionsEnum.SUBW, IsaOpcodeValues.SUBW);
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.SUBW));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.SUBW));
    }

    [Fact]
    public void ScalarSllwSupportStatus_ReportsClosedRuntimeGate()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("SLLW");

        Assert.Equal(IsaInstructionStatus.Mandatory, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);
        Assert.Equal((ushort)InstructionsEnum.SLLW, IsaOpcodeValues.SLLW);
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.SLLW));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.SLLW));
    }

    [Fact]
    public void ScalarSrlwSupportStatus_ReportsClosedRuntimeGate()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("SRLW");

        Assert.Equal(IsaInstructionStatus.Mandatory, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);
        Assert.Equal((ushort)InstructionsEnum.SRLW, IsaOpcodeValues.SRLW);
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.SRLW));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.SRLW));
    }

    [Fact]
    public void ScalarSrawSupportStatus_ReportsClosedRuntimeGate()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("SRAW");

        Assert.Equal(IsaInstructionStatus.Mandatory, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);
        Assert.Equal((ushort)InstructionsEnum.SRAW, IsaOpcodeValues.SRAW);
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.SRAW));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.SRAW));
    }

    [Fact]
    public void ScalarSlliwSupportStatus_ReportsClosedRuntimeGate()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("SLLIW");

        Assert.Equal(IsaInstructionStatus.Mandatory, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);
        Assert.Equal((ushort)InstructionsEnum.SLLIW, IsaOpcodeValues.SLLIW);
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.SLLIW));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.SLLIW));
    }

    [Fact]
    public void ScalarSrliwSupportStatus_ReportsClosedRuntimeGate()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("SRLIW");

        Assert.Equal(IsaInstructionStatus.Mandatory, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);
        Assert.Equal((ushort)InstructionsEnum.SRLIW, IsaOpcodeValues.SRLIW);
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.SRLIW));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.SRLIW));
    }

    [Fact]
    public void ScalarSraiwSupportStatus_ReportsClosedRuntimeGate()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("SRAIW");

        Assert.Equal(IsaInstructionStatus.Mandatory, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);
        Assert.Equal((ushort)InstructionsEnum.SRAIW, IsaOpcodeValues.SRAIW);
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.SRAIW));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.SRAIW));
    }

    [Fact]
    public void ScalarMulwSupportStatus_ReportsClosedRuntimeGate()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("MULW");

        Assert.Equal(IsaInstructionStatus.Mandatory, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);
        Assert.Equal((ushort)InstructionsEnum.MULW, IsaOpcodeValues.MULW);
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.MULW));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.MULW));
    }

    [Fact]
    public void ScalarDivwSupportStatus_ReportsClosedRuntimeGate()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("DIVW");

        Assert.Equal(IsaInstructionStatus.Mandatory, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);
        Assert.Equal((ushort)InstructionsEnum.DIVW, IsaOpcodeValues.DIVW);
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.DIVW));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.DIVW));
    }

    [Fact]
    public void ScalarDivuwSupportStatus_ReportsClosedRuntimeGate()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("DIVUW");

        Assert.Equal(IsaInstructionStatus.Mandatory, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);
        Assert.Equal((ushort)InstructionsEnum.DIVUW, IsaOpcodeValues.DIVUW);
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.DIVUW));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.DIVUW));
    }

    [Fact]
    public void ScalarRemwSupportStatus_ReportsClosedRuntimeGate()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("REMW");

        Assert.Equal(IsaInstructionStatus.Mandatory, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);
        Assert.Equal((ushort)InstructionsEnum.REMW, IsaOpcodeValues.REMW);
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.REMW));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.REMW));
    }

    [Fact]
    public void ScalarRemuwSupportStatus_ReportsClosedRuntimeGate()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("REMUW");

        Assert.Equal(IsaInstructionStatus.Mandatory, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);
        Assert.Equal((ushort)InstructionsEnum.REMUW, IsaOpcodeValues.REMUW);
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.REMUW));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.REMUW));
    }

    [Fact]
    public void ScalarSextwSupportStatus_ReportsClosedRuntimeGate()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("SEXT.W");

        Assert.Equal(IsaInstructionStatus.Mandatory, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);
        Assert.Equal((ushort)InstructionsEnum.SEXT_W, IsaOpcodeValues.SEXT_W);
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.SEXT_W));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.SEXT_W));
    }

    [Fact]
    public void ScalarZextwSupportStatus_ReportsClosedRuntimeGate()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("ZEXT.W");

        Assert.Equal(IsaInstructionStatus.Mandatory, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);
        Assert.Equal((ushort)InstructionsEnum.ZEXT_W, IsaOpcodeValues.ZEXT_W);
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.ZEXT_W));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.ZEXT_W));
    }

    private static bool HasEnumOrRegistryMnemonic(string mnemonic)
    {
        string enumCandidate = mnemonic.Replace(".", "_", StringComparison.Ordinal);
        bool hasEnum = Enum.GetNames<InstructionsEnum>().Contains(enumCandidate);
        bool hasRegistryMnemonic = OpcodeRegistry.Opcodes.Any(info =>
            string.Equals(info.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase));
        return hasEnum || hasRegistryMnemonic;
    }
}

public sealed class CanonicalDecoderAcceptanceInventoryTests
{
    [Theory]
    [InlineData(InstructionsEnum.MTILE_LOAD, "MTILE_LOAD")]
    [InlineData(InstructionsEnum.MTILE_STORE, "MTILE_STORE")]
    [InlineData(InstructionsEnum.MTILE_MACC, "MTILE_MACC")]
    [InlineData(InstructionsEnum.MTRANSPOSE, "MTRANSPOSE")]
    public void MatrixOpcodes_HaveEnumValuesButCanonicalDecoderFailsClosed(
        InstructionsEnum opcode,
        string mnemonic)
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
        Assert.Equal(IsaInstructionStatus.OptionalDisabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.DeclaredOnly, status.RuntimeEvidence);
        Assert.True(status.HasNumericOpcode);
        Assert.False(status.HasRuntimeOpcodeMetadata);
        Assert.False(status.HasCanonicalDecoderAcceptance);
        Assert.False(status.HasRegistryFactory);
        Assert.False(status.HasExecutionSemantics);
        Assert.False(status.IsExecutableClaim);
        Assert.Null(OpcodeRegistry.GetInfo((uint)opcode));
        Assert.Null(InstructionRegistry.GetDescriptor((uint)opcode));
        Assert.False(InstructionRegistry.IsRegistered((uint)opcode));

        var decoder = new VliwDecoderV4();
        VLIW_Instruction instruction = new()
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            StreamLength = 1
        };

        InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(
            () => decoder.Decode(in instruction, slotIndex: 0));
        Assert.Contains("unsupported optional matrix", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(InstructionsEnum.VGATHER, "VGATHER")]
    [InlineData(InstructionsEnum.VSCATTER, "VSCATTER")]
    public void VectorGatherScatter_DecodeAcceptedButRemainDescriptorOnlyWithoutFactory(
        InstructionsEnum opcode,
        string mnemonic)
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
        Assert.Equal(IsaInstructionStatus.DescriptorOnly, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.DecoderAccepted, status.RuntimeEvidence);

        Assert.NotNull(OpcodeRegistry.GetInfo((uint)opcode));
        Assert.NotNull(InstructionRegistry.GetDescriptor((uint)opcode));
        Assert.False(InstructionRegistry.IsRegistered((uint)opcode));

        var decoder = new VliwDecoderV4();
        VLIW_Instruction instruction = InstructionEncoder.EncodeVector1D(
            (uint)opcode,
            DataTypeEnum.INT32,
            destSrc1Ptr: 0x1000,
            src2Ptr: 0x2000,
            streamLength: 4,
            stride: 4);

        InstructionIR ir = decoder.Decode(in instruction, slotIndex: 0);
        Assert.Equal((ushort)opcode, ir.CanonicalOpcode.Value);
    }
}

public sealed class InstructionRegistryFactoryCoverageTests
{
    [Theory]
    [InlineData("VGATHER", InstructionsEnum.VGATHER, RuntimeInstructionEvidence.DecoderAccepted, false)]
    [InlineData("VSCATTER", InstructionsEnum.VSCATTER, RuntimeInstructionEvidence.DecoderAccepted, false)]
    [InlineData("DmaStreamCompute", InstructionsEnum.DmaStreamCompute, RuntimeInstructionEvidence.DescriptorProjected, true)]
    [InlineData("ACCEL_SUBMIT", InstructionsEnum.ACCEL_SUBMIT, RuntimeInstructionEvidence.DescriptorProjected, true)]
    public void DescriptorOnlySupportStatus_MatchesPublishedRegistryMaterializationBoundary(
        string mnemonic,
        InstructionsEnum opcode,
        RuntimeInstructionEvidence expectedEvidence,
        bool expectedRegistryFactory)
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);

        Assert.Equal(IsaInstructionStatus.DescriptorOnly, status.Status);
        Assert.Equal(expectedEvidence, status.RuntimeEvidence);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.Equal(expectedRegistryFactory, status.HasRegistryFactory);
        Assert.False(status.HasExecutionSemantics);
        Assert.False(status.IsExecutableClaim);

        Assert.NotNull(OpcodeRegistry.GetInfo((uint)opcode));
        Assert.NotNull(InstructionRegistry.GetDescriptor((uint)opcode));
        Assert.Equal(expectedRegistryFactory, InstructionRegistry.IsRegistered((uint)opcode));
    }

    [Fact]
    public void ParserOnlyDsc2_StatusPublishesNoRuntimeExecutionAuthority()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("DSC2");

        Assert.Equal(IsaInstructionStatus.ParserOnly, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.DeclaredOnly, status.RuntimeEvidence);
        Assert.False(status.HasNumericOpcode);
        Assert.False(status.HasRuntimeOpcodeMetadata);
        Assert.False(status.HasCanonicalDecoderAcceptance);
        Assert.False(status.HasRegistryFactory);
        Assert.False(status.HasExecutionSemantics);
        Assert.False(status.IsExecutableClaim);
        Assert.False(Enum.TryParse("DSC2", out InstructionsEnum _));
    }

    [Theory]
    [InlineData("SFENCE.VMA")]
    [InlineData("DCACHE_CLEAN")]
    [InlineData("DCACHE_INVAL")]
    [InlineData("DCACHE_FLUSH")]
    [InlineData("ICACHE_INVAL")]
    public void ReservedCacheTlbContours_HaveNoOpcodeAllocationOrRuntimeAuthority(string mnemonic)
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);

        Assert.Equal(IsaInstructionStatus.Reserved, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.None, status.RuntimeEvidence);
        Assert.False(status.HasNumericOpcode);
        Assert.False(status.HasRuntimeOpcodeMetadata);
        Assert.False(status.HasCanonicalDecoderAcceptance);
        Assert.False(status.HasRegistryFactory);
        Assert.False(status.HasExecutionSemantics);
        Assert.False(status.IsExecutableClaim);
        Assert.False(HasEnumOrRegistryMnemonic(mnemonic));
    }

    [Theory]
    [InlineData(InstructionsEnum.VGATHER)]
    [InlineData(InstructionsEnum.VSCATTER)]
    public void RegistrySupportedDescriptorOnlyVectorMemory_HasDescriptorButNoMicroOpMaterializer(
        InstructionsEnum opcode)
    {
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)opcode));
        Assert.NotNull(InstructionRegistry.GetDescriptor((uint)opcode));
        Assert.False(InstructionRegistry.IsRegistered((uint)opcode));

        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            HasDataType = true,
            DataType = (byte)DataTypeEnum.INT32,
            HasVectorAddressingContour = true,
            HasVectorPayload = true,
            VectorPrimaryPointer = 0x1000,
            VectorSecondaryPointer = 0x2000,
            VectorStreamLength = 4,
            VectorStride = 4
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)opcode, context));
        Assert.Contains("Unsupported instruction opcode", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DmaStreamCompute_RawRegistryFactoryRemainsFailClosedDescriptorOnlyBoundary()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("DmaStreamCompute");
        Assert.Equal(IsaInstructionStatus.DescriptorOnly, status.Status);
        Assert.True(status.HasRegistryFactory);
        Assert.False(status.HasExecutionSemantics);
        Assert.False(status.IsExecutableClaim);
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.DmaStreamCompute));

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.DmaStreamCompute,
            HasDataType = true,
            DataType = (byte)DataTypeEnum.INT32
        };

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.DmaStreamCompute, context));
        Assert.Contains("guard-accepted descriptor sideband", exception.Message, StringComparison.Ordinal);
        Assert.Contains("not the canonical lane6 descriptor path", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AccelSubmit_RawRegistryFactoryMaterializesOnlyFailClosedSystemDeviceCarrier()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("ACCEL_SUBMIT");
        Assert.Equal(IsaInstructionStatus.DescriptorOnly, status.Status);
        Assert.True(status.HasRegistryFactory);
        Assert.False(status.HasExecutionSemantics);
        Assert.False(status.IsExecutableClaim);
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.ACCEL_SUBMIT));

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.ACCEL_SUBMIT,
            HasDataType = true,
            DataType = (byte)DataTypeEnum.INT32
        };

        MicroOp microOp = InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.ACCEL_SUBMIT, context);
        SystemDeviceCommandMicroOp systemDeviceCommand = Assert.IsType<AcceleratorSubmitMicroOp>(microOp);
        Assert.Equal(SystemDeviceCommandKind.Submit, systemDeviceCommand.CommandKind);
        Assert.False(systemDeviceCommand.WritesRegister);

        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => systemDeviceCommand.Execute(ref core));
        Assert.Contains("direct execution is unsupported and must fail closed", exception.Message, StringComparison.Ordinal);
        Assert.Contains("fallback routing are not implemented", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ACCEL_QUERY_CAPS", InstructionsEnum.ACCEL_QUERY_CAPS, RuntimeInstructionEvidence.Materialized, SerializationClass.CsrOrdered, SystemDeviceCommandKind.QueryCaps)]
    [InlineData("ACCEL_POLL", InstructionsEnum.ACCEL_POLL, RuntimeInstructionEvidence.Materialized, SerializationClass.CsrOrdered, SystemDeviceCommandKind.Poll)]
    [InlineData("ACCEL_WAIT", InstructionsEnum.ACCEL_WAIT, RuntimeInstructionEvidence.Materialized, SerializationClass.FullSerial, SystemDeviceCommandKind.Wait)]
    [InlineData("ACCEL_CANCEL", InstructionsEnum.ACCEL_CANCEL, RuntimeInstructionEvidence.Materialized, SerializationClass.FullSerial, SystemDeviceCommandKind.Cancel)]
    [InlineData("ACCEL_FENCE", InstructionsEnum.ACCEL_FENCE, RuntimeInstructionEvidence.Materialized, SerializationClass.FullSerial, SystemDeviceCommandKind.Fence)]
    public void L7SdcCarrierOnlyStatus_MatchesPublishedFailClosedCarrierBoundary(
        string mnemonic,
        InstructionsEnum opcode,
        RuntimeInstructionEvidence expectedEvidence,
        SerializationClass expectedSerialization,
        SystemDeviceCommandKind expectedKind)
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);

        Assert.Equal(IsaInstructionStatus.CarrierOnly, status.Status);
        Assert.Equal(expectedEvidence, status.RuntimeEvidence);
        Assert.Equal("Lane7L7SDC", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.False(status.HasExecutionSemantics);
        Assert.False(status.IsExecutableClaim);

        Assert.Contains(mnemonic, IsaV4Surface.SystemDeviceCommandOpcodes);
        Assert.Contains(mnemonic, IsaV4Surface.CarrierOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.MandatoryCoreOpcodes);
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)opcode));
        Assert.NotNull(InstructionRegistry.GetDescriptor((uint)opcode));
        Assert.True(OpcodeRegistry.IsSystemDeviceCommandOpcode((uint)opcode));
        Assert.True(InstructionRegistry.IsRegistered((uint)opcode));
        Assert.Equal((InstructionClass.System, expectedSerialization), InstructionClassifier.Classify(opcode));

        MicroOp microOp = InstructionRegistry.CreateMicroOp(
            (uint)opcode,
            new DecoderContext { OpCode = (uint)opcode });
        SystemDeviceCommandMicroOp carrier = Assert.IsAssignableFrom<SystemDeviceCommandMicroOp>(microOp);
        Assert.Equal(expectedKind, carrier.CommandKind);
        Assert.Equal(expectedSerialization, carrier.SerializationClass);
    }

    [Theory]
    [InlineData(InstructionsEnum.ACCEL_QUERY_CAPS, SystemDeviceCommandKind.QueryCaps, SerializationClass.CsrOrdered)]
    [InlineData(InstructionsEnum.ACCEL_POLL, SystemDeviceCommandKind.Poll, SerializationClass.CsrOrdered)]
    [InlineData(InstructionsEnum.ACCEL_WAIT, SystemDeviceCommandKind.Wait, SerializationClass.FullSerial)]
    [InlineData(InstructionsEnum.ACCEL_CANCEL, SystemDeviceCommandKind.Cancel, SerializationClass.FullSerial)]
    [InlineData(InstructionsEnum.ACCEL_FENCE, SystemDeviceCommandKind.Fence, SerializationClass.FullSerial)]
    public void L7SdcCarrierOnlyRegistryFactoryMaterializesNoWriteFailClosedCarrier(
        InstructionsEnum opcode,
        SystemDeviceCommandKind expectedKind,
        SerializationClass expectedSerialization)
    {
        MicroOp microOp = InstructionRegistry.CreateMicroOp(
            (uint)opcode,
            new DecoderContext { OpCode = (uint)opcode });
        SystemDeviceCommandMicroOp carrier = Assert.IsAssignableFrom<SystemDeviceCommandMicroOp>(microOp);

        Assert.Equal(expectedKind, carrier.CommandKind);
        Assert.Equal(InstructionClass.System, carrier.InstructionClass);
        Assert.Equal(expectedSerialization, carrier.SerializationClass);
        Assert.Equal(SlotClass.SystemSingleton, carrier.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, carrier.Placement.PinningKind);
        Assert.Equal((byte)7, carrier.Placement.PinnedLaneId);
        Assert.False(carrier.IsMemoryOp);
        Assert.False(carrier.IsControlFlow);
        Assert.True(carrier.HasSideEffects);
        Assert.False(carrier.WritesRegister);
        Assert.Empty(carrier.ReadRegisters);
        Assert.Empty(carrier.WriteRegisters);
        Assert.Empty(carrier.ReadMemoryRanges);
        Assert.Empty(carrier.WriteMemoryRanges);
        Assert.Equal(ResourceBitset.Zero, carrier.ResourceMask);

        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => carrier.Execute(ref core));
        Assert.Contains("direct execution is unsupported and must fail closed", exception.Message, StringComparison.Ordinal);
        Assert.Contains("backend execution", exception.Message, StringComparison.Ordinal);
        Assert.Contains("staged write publication", exception.Message, StringComparison.Ordinal);
        Assert.Contains("architectural rd writeback", exception.Message, StringComparison.Ordinal);
        Assert.Contains("fallback routing are not implemented", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(InstructionsEnum.VSETVL, "VSETVL")]
    [InlineData(InstructionsEnum.VSETVLI, "VSETVLI")]
    [InlineData(InstructionsEnum.VSETIVLI, "VSETIVLI")]
    public void VectorConfigSystemSingletonStatus_MatchesPublishedMainlineMaterializedContour(
        InstructionsEnum opcode,
        string mnemonic)
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);

        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("VectorConfigSystemSingleton", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.MandatoryCoreOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.DescriptorOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.CarrierOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ParserOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ReservedOpcodes);

        Assert.Equal((ushort)opcode, ResolveIsaOpcodeValue(opcode));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
        Assert.NotNull(info);
        Assert.Equal(mnemonic, info!.Value.Mnemonic);
        Assert.True(info.Value.IsVector);
        Assert.Equal(InstructionClass.System, info.Value.InstructionClass);
        Assert.Equal(SerializationClass.FullSerial, info.Value.SerializationClass);
        Assert.Equal((InstructionClass.System, SerializationClass.FullSerial),
            InstructionClassifier.Classify(opcode));

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);
        Assert.NotNull(descriptor);
        Assert.True(InstructionRegistry.IsRegistered((uint)opcode));
        Assert.Equal(info.Value.ExecutionLatency, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.Equal(false, descriptor.IsMemoryOp);
        Assert.Null(descriptor.WritesRegister);

        VConfigMicroOp microOp = MaterializeVectorConfigMicroOp(opcode);
        microOp.OwnerThreadId = 2;
        microOp.VirtualThreadId = 2;
        microOp.OwnerContextId = 2;
        microOp.RefreshAdmissionMetadata();

        Assert.False(microOp.IsMemoryOp);
        Assert.True(microOp.HasSideEffects);
        Assert.True(microOp.AdmissionMetadata.HasSideEffects);
        Assert.True(microOp.WritesRegister);
        Assert.Equal(ExpectedVectorConfigReadRegisters(opcode), microOp.ReadRegisters);
        Assert.Equal(ExpectedVectorConfigWriteRegisters(opcode), microOp.WriteRegisters);
        Assert.Equal(ExpectedVectorConfigWriteRegisters(opcode), microOp.AdmissionMetadata.WriteRegisters);
        Assert.Empty(microOp.ReadMemoryRanges);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.NotEqual(ResourceBitset.Zero, microOp.ResourceMask);
        Assert.True(microOp.SafetyMask.IsNonZero);
        Assert.True(microOp.AdmissionMetadata.StructuralSafetyMask.IsNonZero);
        Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal((byte)7, microOp.AdmissionMetadata.Placement.PinnedLaneId);

        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.VectorConfig.VL = 3;
        core.VectorConfig.VTYPE = 0;
        core.VectorConfig.TailAgnostic = 0;
        core.VectorConfig.MaskAgnostic = 0;
        core.WriteCommittedArch(2, 5, 45UL);
        core.WriteCommittedArch(2, 6, 0xC0UL);
        core.WriteCommittedArch(2, 8, 45UL);

        Assert.True(microOp.Execute(ref core));
        Assert.Equal(3UL, core.VectorConfig.VL);
        Assert.True(microOp.TryGetPrimaryWriteBackResult(out ulong writebackValue));
        Assert.Equal(ExpectedVectorConfigActualVectorLength(opcode), writebackValue);

        VectorConfigRetireEffect retireEffect = microOp.CreateRetireEffect();
        Assert.True(retireEffect.IsValid);
        Assert.Equal(ExpectedVectorConfigOperationKind(opcode), retireEffect.Operation);
        Assert.True(retireEffect.HasRegisterWriteback);
        Assert.Equal(ExpectedVectorConfigDestinationRegister(opcode), retireEffect.DestinationRegister);
        Assert.Equal(ExpectedVectorConfigActualVectorLength(opcode), retireEffect.ActualVectorLength);
        Assert.Equal(ExpectedVectorConfigVType(opcode), retireEffect.VType);

        RetireRecord[] retireRecords = new RetireRecord[2];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);

        Assert.Equal(0, retireRecordCount);

        var dispatcher = new ExecutionDispatcherV4();
        Assert.False(dispatcher.CanRouteToConfiguredExecutionSurface(BuildVectorConfigIr(opcode)));
    }

    [Theory]
    [InlineData(InstructionsEnum.VADD, "VADD", 1)]
    [InlineData(InstructionsEnum.VSUB, "VSUB", 1)]
    [InlineData(InstructionsEnum.VMUL, "VMUL", 3)]
    [InlineData(InstructionsEnum.VDIV, "VDIV", 16)]
    [InlineData(InstructionsEnum.VMOD, "VMOD", 16)]
    [InlineData(InstructionsEnum.VXOR, "VXOR", 1)]
    [InlineData(InstructionsEnum.VOR, "VOR", 1)]
    [InlineData(InstructionsEnum.VAND, "VAND", 1)]
    [InlineData(InstructionsEnum.VSLL, "VSLL", 1)]
    [InlineData(InstructionsEnum.VSRL, "VSRL", 1)]
    [InlineData(InstructionsEnum.VSRA, "VSRA", 1)]
    [InlineData(InstructionsEnum.VMIN, "VMIN", 1)]
    [InlineData(InstructionsEnum.VMAX, "VMAX", 1)]
    [InlineData(InstructionsEnum.VMINU, "VMINU", 1)]
    [InlineData(InstructionsEnum.VMAXU, "VMAXU", 1)]
    public void VectorBinaryComputeCarrierStatus_MatchesPublishedInPlaceMaterializedContour(
        InstructionsEnum opcode,
        string mnemonic,
        byte expectedLatency)
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);

        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("VectorBinaryComputeCarrier", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.MandatoryCoreOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.DescriptorOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.CarrierOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ParserOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ReservedOpcodes);

        Assert.Equal((ushort)opcode, ResolveIsaOpcodeValue(opcode));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
        Assert.NotNull(info);
        Assert.Equal(mnemonic, info!.Value.Mnemonic);
        Assert.True(
            info.Value.Category is OpcodeCategory.Vector or OpcodeCategory.BitManip,
            $"Opcode {mnemonic} should remain in the vector-binary carrier family even when categorized as {info.Value.Category}.");
        Assert.True(info.Value.IsVector);
        Assert.True(OpcodeRegistry.RequiresVectorPayloadProjection((uint)opcode));
        Assert.False(OpcodeRegistry.IsComparisonOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsMaskManipOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsReductionOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsFmaOp((uint)opcode));
        Assert.Equal(InstructionFlags.MemoryRead, info.Value.Flags & InstructionFlags.MemoryRead);
        Assert.Equal(InstructionFlags.MemoryWrite, info.Value.Flags & InstructionFlags.MemoryWrite);
        Assert.Equal(InstructionClass.ScalarAlu, info.Value.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.Value.SerializationClass);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(opcode));

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);
        Assert.NotNull(descriptor);
        Assert.True(InstructionRegistry.IsRegistered((uint)opcode));
        Assert.Equal(expectedLatency, info.Value.ExecutionLatency);
        Assert.Equal(info.Value.ExecutionLatency, descriptor!.Latency);
        Assert.Equal(2, descriptor.MemFootprintClass);
        Assert.Equal(false, descriptor.IsMemoryOp);
        Assert.Equal(false, descriptor.WritesRegister);

        VectorBinaryOpMicroOp microOp = MaterializeVectorBinaryMicroOp(opcode);
        microOp.OwnerThreadId = 2;
        microOp.VirtualThreadId = 2;
        microOp.OwnerContextId = 2;
        microOp.RefreshAdmissionMetadata();

        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.WritesRegister);
        Assert.Empty(microOp.ReadRegisters);
        Assert.Empty(microOp.WriteRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(2, microOp.ReadMemoryRanges.Count);
        Assert.Equal((0x200UL, 8UL), microOp.ReadMemoryRanges[0]);
        Assert.Equal((0x300UL, 8UL), microOp.ReadMemoryRanges[1]);
        Assert.Equal((0x200UL, 8UL), Assert.Single(microOp.WriteMemoryRanges));
        Assert.Equal(microOp.ReadMemoryRanges, microOp.AdmissionMetadata.ReadMemoryRanges);
        Assert.Equal(microOp.WriteMemoryRanges, microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.NotEqual(ResourceBitset.Zero, microOp.ResourceMask);
        Assert.True(microOp.SafetyMask.IsNonZero);
        Assert.True(microOp.AdmissionMetadata.StructuralSafetyMask.IsNonZero);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.TryGetPrimaryWriteBackResult(out _));

        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        RetireRecord[] retireRecords = new RetireRecord[2];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);

        Assert.Equal(0, retireRecordCount);
    }

    [Theory]
    [InlineData(InstructionsEnum.VADD)]
    [InlineData(InstructionsEnum.VSUB)]
    [InlineData(InstructionsEnum.VMUL)]
    [InlineData(InstructionsEnum.VDIV)]
    [InlineData(InstructionsEnum.VMOD)]
    [InlineData(InstructionsEnum.VXOR)]
    [InlineData(InstructionsEnum.VOR)]
    [InlineData(InstructionsEnum.VAND)]
    [InlineData(InstructionsEnum.VSLL)]
    [InlineData(InstructionsEnum.VSRL)]
    [InlineData(InstructionsEnum.VSRA)]
    [InlineData(InstructionsEnum.VMIN)]
    [InlineData(InstructionsEnum.VMAX)]
    [InlineData(InstructionsEnum.VMINU)]
    [InlineData(InstructionsEnum.VMAXU)]
    public void VectorBinaryComputeAddressingSideband_FailsClosedBeforeMaterialization(
        InstructionsEnum opcode)
    {
        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            HasDataType = true,
            DataType = (byte)DataTypeEnum.INT32,
            HasVectorAddressingContour = true,
            IndexedAddressing = true,
            HasVectorPayload = true,
            VectorPrimaryPointer = 0x1000,
            VectorSecondaryPointer = 0x2000,
            VectorStreamLength = 8,
            VectorStride = 4,
            PredicateMask = 0xFF
        };

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)opcode, context));

        Assert.Contains("unsupported indexed vector-binary addressing", exception.Message, StringComparison.Ordinal);
        Assert.Contains("fail closed", exception.Message, StringComparison.Ordinal);
        Assert.Contains("non-representable compat surface", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(InstructionsEnum.VFMADD, "VFMADD")]
    [InlineData(InstructionsEnum.VFMSUB, "VFMSUB")]
    [InlineData(InstructionsEnum.VFNMADD, "VFNMADD")]
    [InlineData(InstructionsEnum.VFNMSUB, "VFNMSUB")]
    public void VectorFmaStatus_MatchesPublishedDescriptorBackedMaterializedContour(
        InstructionsEnum opcode,
        string mnemonic)
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);

        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("VectorFmaDescriptorBacked", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.MandatoryCoreOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.DescriptorOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.CarrierOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ParserOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ReservedOpcodes);

        Assert.Equal((ushort)opcode, ResolveIsaOpcodeValue(opcode));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
        Assert.NotNull(info);
        Assert.Equal(mnemonic, info!.Value.Mnemonic);
        Assert.Equal(OpcodeCategory.Vector, info.Value.Category);
        Assert.True(info.Value.IsVector);
        Assert.True((info.Value.Flags & InstructionFlags.ThreeOperand) != 0);
        Assert.True((info.Value.Flags & InstructionFlags.FloatingPoint) != 0);
        Assert.Equal(InstructionFlags.MemoryRead, info.Value.Flags & InstructionFlags.MemoryRead);
        Assert.Equal(InstructionFlags.MemoryWrite, info.Value.Flags & InstructionFlags.MemoryWrite);
        Assert.False(OpcodeRegistry.IsComparisonOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsMaskManipOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsReductionOp((uint)opcode));
        Assert.True(OpcodeRegistry.IsFmaOp((uint)opcode));
        Assert.True(OpcodeRegistry.RequiresVectorPayloadProjection((uint)opcode));
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(opcode));

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);
        Assert.NotNull(descriptor);
        Assert.True(InstructionRegistry.IsRegistered((uint)opcode));
        Assert.Equal(4, info.Value.ExecutionLatency);
        Assert.Equal(info.Value.ExecutionLatency, descriptor!.Latency);
        Assert.Equal(2, descriptor.MemFootprintClass);
        Assert.Equal(false, descriptor.IsMemoryOp);
        Assert.Equal(false, descriptor.WritesRegister);

        VectorFmaMicroOp microOp = MaterializeVectorFmaMicroOp(opcode);
        microOp.OwnerThreadId = 2;
        microOp.VirtualThreadId = 2;
        microOp.OwnerContextId = 2;
        microOp.RefreshAdmissionMetadata();

        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.WritesRegister);
        Assert.Empty(microOp.ReadRegisters);
        Assert.Empty(microOp.WriteRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(3, microOp.AdmissionMetadata.ReadMemoryRanges.Count);
        Assert.Equal((0x240UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[0]);
        Assert.Equal((0x440UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[1]);
        Assert.Equal((0x540UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[2]);
        Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal((0x240UL, 16UL), microOp.AdmissionMetadata.WriteMemoryRanges[0]);
        Assert.NotEqual(ResourceBitset.Zero, microOp.ResourceMask);
        Assert.True(microOp.SafetyMask.IsNonZero);
        Assert.True(microOp.AdmissionMetadata.StructuralSafetyMask.IsNonZero);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.TryGetPrimaryWriteBackResult(out _));
    }

    [Theory]
    [InlineData(InstructionsEnum.VSQRT, "VSQRT", 8)]
    [InlineData(InstructionsEnum.VNOT, "VNOT", 1)]
    [InlineData(InstructionsEnum.VPOPCNT, "VPOPCNT", 2)]
    [InlineData(InstructionsEnum.VCLZ, "VCLZ", 2)]
    [InlineData(InstructionsEnum.VCTZ, "VCTZ", 2)]
    [InlineData(InstructionsEnum.VBREV8, "VBREV8", 2)]
    [InlineData(InstructionsEnum.VREVERSE, "VREVERSE", 2)]
    public void VectorUnaryComputeCarrierStatus_MatchesPublishedInPlaceMaterializedContour(
        InstructionsEnum opcode,
        string mnemonic,
        byte expectedLatency)
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);

        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("VectorUnaryComputeCarrier", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.MandatoryCoreOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.DescriptorOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.CarrierOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ParserOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ReservedOpcodes);

        Assert.Equal((ushort)opcode, ResolveIsaOpcodeValue(opcode));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
        Assert.NotNull(info);
        Assert.Equal(mnemonic, info!.Value.Mnemonic);
        Assert.True(
            info.Value.Category is OpcodeCategory.Vector or OpcodeCategory.BitManip,
            $"Opcode {mnemonic} should remain in the vector-unary carrier family even when categorized as {info.Value.Category}.");
        Assert.True(info.Value.IsVector);
        Assert.True(OpcodeRegistry.RequiresVectorPayloadProjection((uint)opcode));
        Assert.False(OpcodeRegistry.IsComparisonOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsMaskManipOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsReductionOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsFmaOp((uint)opcode));
        Assert.Equal(InstructionFlags.MemoryRead, info.Value.Flags & InstructionFlags.MemoryRead);
        Assert.Equal(InstructionFlags.MemoryWrite, info.Value.Flags & InstructionFlags.MemoryWrite);
        Assert.Equal(InstructionClass.ScalarAlu, info.Value.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.Value.SerializationClass);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(opcode));

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);
        Assert.NotNull(descriptor);
        Assert.True(InstructionRegistry.IsRegistered((uint)opcode));
        Assert.Equal(expectedLatency, info.Value.ExecutionLatency);
        Assert.Equal(info.Value.ExecutionLatency, descriptor!.Latency);
        Assert.Equal(2, descriptor.MemFootprintClass);
        Assert.Equal(false, descriptor.IsMemoryOp);
        Assert.Equal(false, descriptor.WritesRegister);

        VectorUnaryOpMicroOp microOp = MaterializeVectorUnaryMicroOp(opcode);
        microOp.OwnerThreadId = 2;
        microOp.VirtualThreadId = 2;
        microOp.OwnerContextId = 2;
        microOp.RefreshAdmissionMetadata();

        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.WritesRegister);
        Assert.Empty(microOp.ReadRegisters);
        Assert.Empty(microOp.WriteRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal((0x280UL, 8UL), Assert.Single(microOp.ReadMemoryRanges));
        Assert.Equal((0x280UL, 8UL), Assert.Single(microOp.WriteMemoryRanges));
        Assert.Equal(microOp.ReadMemoryRanges, microOp.AdmissionMetadata.ReadMemoryRanges);
        Assert.Equal(microOp.WriteMemoryRanges, microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.NotEqual(ResourceBitset.Zero, microOp.ResourceMask);
        Assert.True(microOp.SafetyMask.IsNonZero);
        Assert.True(microOp.AdmissionMetadata.StructuralSafetyMask.IsNonZero);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.TryGetPrimaryWriteBackResult(out _));

        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        RetireRecord[] retireRecords = new RetireRecord[2];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);

        Assert.Equal(0, retireRecordCount);
    }

    [Theory]
    [InlineData(InstructionsEnum.VSQRT)]
    [InlineData(InstructionsEnum.VNOT)]
    [InlineData(InstructionsEnum.VPOPCNT)]
    [InlineData(InstructionsEnum.VCLZ)]
    [InlineData(InstructionsEnum.VCTZ)]
    [InlineData(InstructionsEnum.VBREV8)]
    [InlineData(InstructionsEnum.VREVERSE)]
    public void VectorUnaryComputeAddressingSideband_FailsClosedBeforeMaterialization(
        InstructionsEnum opcode)
    {
        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            HasDataType = true,
            DataType = (byte)DataTypeEnum.INT32,
            HasVectorAddressingContour = true,
            IndexedAddressing = true,
            HasVectorPayload = true,
            VectorPrimaryPointer = 0x1000,
            VectorStreamLength = 8,
            VectorStride = 4,
            PredicateMask = 0xFF
        };

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)opcode, context));

        Assert.Contains("unsupported indexed vector-unary addressing", exception.Message, StringComparison.Ordinal);
        Assert.Contains("fail closed", exception.Message, StringComparison.Ordinal);
        Assert.Contains("non-representable compat surface", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(InstructionsEnum.VREDSUM, "VREDSUM")]
    [InlineData(InstructionsEnum.VREDMAX, "VREDMAX")]
    [InlineData(InstructionsEnum.VREDMIN, "VREDMIN")]
    [InlineData(InstructionsEnum.VREDMAXU, "VREDMAXU")]
    [InlineData(InstructionsEnum.VREDMINU, "VREDMINU")]
    [InlineData(InstructionsEnum.VREDAND, "VREDAND")]
    [InlineData(InstructionsEnum.VREDOR, "VREDOR")]
    [InlineData(InstructionsEnum.VREDXOR, "VREDXOR")]
    public void VectorReductionScalarFootprintCarrierStatus_MatchesPublishedMaterializedContour(
        InstructionsEnum opcode,
        string mnemonic)
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);

        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("VectorReductionScalarFootprintCarrier", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.MandatoryCoreOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.DescriptorOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.CarrierOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ParserOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ReservedOpcodes);

        Assert.Equal((ushort)opcode, ResolveIsaOpcodeValue(opcode));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
        Assert.NotNull(info);
        Assert.Equal(mnemonic, info!.Value.Mnemonic);
        Assert.Equal(OpcodeCategory.Vector, info.Value.Category);
        Assert.True(info.Value.IsVector);
        Assert.True(OpcodeRegistry.RequiresVectorPayloadProjection((uint)opcode));
        Assert.False(OpcodeRegistry.IsComparisonOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsMaskManipOp((uint)opcode));
        Assert.True(OpcodeRegistry.IsReductionOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsFmaOp((uint)opcode));
        Assert.Equal(InstructionFlags.MemoryRead, info.Value.Flags & InstructionFlags.MemoryRead);
        Assert.Equal(InstructionFlags.MemoryWrite, info.Value.Flags & InstructionFlags.MemoryWrite);
        Assert.Equal(InstructionFlags.Reduction, info.Value.Flags & InstructionFlags.Reduction);
        Assert.Equal(InstructionClass.ScalarAlu, info.Value.InstructionClass);
        Assert.Equal(SerializationClass.Free, info.Value.SerializationClass);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(opcode));

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);
        Assert.NotNull(descriptor);
        Assert.True(InstructionRegistry.IsRegistered((uint)opcode));
        Assert.Equal(8, info.Value.ExecutionLatency);
        Assert.Equal(info.Value.ExecutionLatency, descriptor!.Latency);
        Assert.Equal(2, descriptor.MemFootprintClass);
        Assert.Equal(false, descriptor.IsMemoryOp);
        Assert.Equal(false, descriptor.WritesRegister);

        VectorReductionMicroOp microOp = MaterializeVectorReductionMicroOp(opcode);
        microOp.OwnerThreadId = 2;
        microOp.VirtualThreadId = 2;
        microOp.OwnerContextId = 2;
        microOp.RefreshAdmissionMetadata();

        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.WritesRegister);
        Assert.Empty(microOp.ReadRegisters);
        Assert.Empty(microOp.WriteRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal((0x2C0UL, 16UL), Assert.Single(microOp.ReadMemoryRanges));
        Assert.Equal((0x2C0UL, 4UL), Assert.Single(microOp.WriteMemoryRanges));
        Assert.Equal(microOp.ReadMemoryRanges, microOp.AdmissionMetadata.ReadMemoryRanges);
        Assert.Equal(microOp.WriteMemoryRanges, microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.NotEqual(ResourceBitset.Zero, microOp.ResourceMask);
        Assert.True(microOp.SafetyMask.IsNonZero);
        Assert.True(microOp.AdmissionMetadata.StructuralSafetyMask.IsNonZero);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.TryGetPrimaryWriteBackResult(out _));

        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        RetireRecord[] retireRecords = new RetireRecord[2];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);

        Assert.Equal(0, retireRecordCount);
    }

    [Theory]
    [InlineData(InstructionsEnum.VREDSUM)]
    [InlineData(InstructionsEnum.VREDMAX)]
    [InlineData(InstructionsEnum.VREDMIN)]
    [InlineData(InstructionsEnum.VREDMAXU)]
    [InlineData(InstructionsEnum.VREDMINU)]
    [InlineData(InstructionsEnum.VREDAND)]
    [InlineData(InstructionsEnum.VREDOR)]
    [InlineData(InstructionsEnum.VREDXOR)]
    public void VectorReductionAddressingSideband_FailsClosedBeforeMaterialization(
        InstructionsEnum opcode)
    {
        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            HasDataType = true,
            DataType = (byte)DataTypeEnum.UINT32,
            HasVectorAddressingContour = true,
            IndexedAddressing = true,
            HasVectorPayload = true,
            VectorPrimaryPointer = 0x1000,
            VectorStreamLength = 8,
            VectorStride = 4,
            PredicateMask = 0xFF
        };

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)opcode, context));

        Assert.Contains("unsupported indexed vector-reduction addressing", exception.Message, StringComparison.Ordinal);
        Assert.Contains("fail closed", exception.Message, StringComparison.Ordinal);
        Assert.Contains("non-representable compat surface", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(InstructionsEnum.VDOT, "VDOT", DataTypeEnum.INT32, 2, 4, 8UL, 4UL)]
    [InlineData(InstructionsEnum.VDOTU, "VDOTU", DataTypeEnum.UINT32, 2, 4, 8UL, 4UL)]
    [InlineData(InstructionsEnum.VDOTF, "VDOTF", DataTypeEnum.FLOAT32, 2, 4, 8UL, 4UL)]
    [InlineData(InstructionsEnum.VDOT_FP8, "VDOT_FP8", DataTypeEnum.FLOAT8_E4M3, 4, 1, 4UL, 4UL)]
    public void VectorDotProductStatus_MatchesPublishedScalarFootprintMaterializedContour(
        InstructionsEnum opcode,
        string mnemonic,
        DataTypeEnum dataType,
        ushort streamLength,
        ushort stride,
        ulong expectedReadBytesPerOperand,
        ulong expectedWriteBytes)
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);

        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("VectorDotProductScalarFootprint", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.MandatoryCoreOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.DescriptorOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.CarrierOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ParserOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ReservedOpcodes);

        Assert.Equal((ushort)opcode, ResolveIsaOpcodeValue(opcode));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
        Assert.NotNull(info);
        Assert.Equal(mnemonic, info!.Value.Mnemonic);
        Assert.Equal(OpcodeCategory.Vector, info.Value.Category);
        Assert.True(info.Value.IsVector);
        Assert.Equal(InstructionFlags.MemoryRead, info.Value.Flags & InstructionFlags.MemoryRead);
        Assert.Equal(InstructionFlags.MemoryWrite, info.Value.Flags & InstructionFlags.MemoryWrite);
        Assert.True(OpcodeRegistry.IsReductionOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsComparisonOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsMaskManipOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsFmaOp((uint)opcode));
        Assert.True(OpcodeRegistry.RequiresVectorPayloadProjection((uint)opcode));
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(opcode));

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);
        Assert.NotNull(descriptor);
        Assert.True(InstructionRegistry.IsRegistered((uint)opcode));
        Assert.Equal(12, info.Value.ExecutionLatency);
        Assert.Equal(info.Value.ExecutionLatency, descriptor!.Latency);
        Assert.Equal(2, descriptor.MemFootprintClass);
        Assert.Equal(false, descriptor.IsMemoryOp);
        Assert.Equal(false, descriptor.WritesRegister);

        VectorDotProductMicroOp microOp =
            MaterializeVectorDotProductMicroOp(opcode, dataType, streamLength, stride);
        microOp.OwnerThreadId = 2;
        microOp.VirtualThreadId = 2;
        microOp.OwnerContextId = 2;
        microOp.RefreshAdmissionMetadata();

        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.WritesRegister);
        Assert.Empty(microOp.ReadRegisters);
        Assert.Empty(microOp.WriteRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(2, microOp.AdmissionMetadata.ReadMemoryRanges.Count);
        Assert.Equal((0x340UL, expectedReadBytesPerOperand), microOp.AdmissionMetadata.ReadMemoryRanges[0]);
        Assert.Equal((0x3C0UL, expectedReadBytesPerOperand), microOp.AdmissionMetadata.ReadMemoryRanges[1]);
        Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal((0x340UL, expectedWriteBytes), microOp.AdmissionMetadata.WriteMemoryRanges[0]);
        Assert.NotEqual(ResourceBitset.Zero, microOp.ResourceMask);
        Assert.True(microOp.SafetyMask.IsNonZero);
        Assert.True(microOp.AdmissionMetadata.StructuralSafetyMask.IsNonZero);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.TryGetPrimaryWriteBackResult(out _));
    }

    [Theory]
    [InlineData(InstructionsEnum.VLOAD, "VLOAD", SerializationClass.Free, 0x300UL, 0x200UL)]
    [InlineData(InstructionsEnum.VSTORE, "VSTORE", SerializationClass.MemoryOrdered, 0x200UL, 0x300UL)]
    public void VectorTransferCarrierStatus_MatchesPublishedMainlineMaterializedContour(
        InstructionsEnum opcode,
        string mnemonic,
        SerializationClass expectedSerialization,
        ulong expectedReadAddress,
        ulong expectedWriteAddress)
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);

        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("VectorTransferCarrier", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.MandatoryCoreOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.DescriptorOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.CarrierOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ParserOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ReservedOpcodes);

        Assert.Equal((ushort)opcode, ResolveIsaOpcodeValue(opcode));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
        Assert.NotNull(info);
        Assert.Equal(mnemonic, info!.Value.Mnemonic);
        Assert.Equal(OpcodeCategory.Vector, info.Value.Category);
        Assert.True(info.Value.IsVector);
        Assert.True(OpcodeRegistry.RequiresVectorPayloadProjection((uint)opcode));
        Assert.Equal(InstructionFlags.MemoryRead, info.Value.Flags & InstructionFlags.MemoryRead);
        Assert.Equal(InstructionFlags.MemoryWrite, info.Value.Flags & InstructionFlags.MemoryWrite);
        Assert.Equal(InstructionClass.Memory, info.Value.InstructionClass);
        Assert.Equal(expectedSerialization, info.Value.SerializationClass);
        Assert.Equal((InstructionClass.Memory, expectedSerialization),
            InstructionClassifier.Classify(opcode));

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);
        Assert.NotNull(descriptor);
        Assert.True(InstructionRegistry.IsRegistered((uint)opcode));
        Assert.Equal(info.Value.ExecutionLatency, descriptor!.Latency);
        Assert.Equal(3, descriptor.MemFootprintClass);
        Assert.Equal(false, descriptor.IsMemoryOp);
        Assert.Equal(false, descriptor.WritesRegister);

        VectorTransferMicroOp microOp = MaterializeVectorTransferMicroOp(opcode);
        microOp.OwnerThreadId = 2;
        microOp.VirtualThreadId = 2;
        microOp.OwnerContextId = 2;
        microOp.RefreshAdmissionMetadata();

        Assert.Equal(InstructionClass.Memory, microOp.InstructionClass);
        Assert.Equal(expectedSerialization, microOp.SerializationClass);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.WritesRegister);
        Assert.Empty(microOp.ReadRegisters);
        Assert.Empty(microOp.WriteRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal((expectedReadAddress, 8UL), Assert.Single(microOp.ReadMemoryRanges));
        Assert.Equal((expectedWriteAddress, 8UL), Assert.Single(microOp.WriteMemoryRanges));
        Assert.Equal((expectedReadAddress, 8UL), Assert.Single(microOp.AdmissionMetadata.ReadMemoryRanges));
        Assert.Equal((expectedWriteAddress, 8UL), Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges));
        Assert.NotEqual(ResourceBitset.Zero, microOp.ResourceMask);
        Assert.True(microOp.SafetyMask.IsNonZero);
        Assert.True(microOp.AdmissionMetadata.StructuralSafetyMask.IsNonZero);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.TryGetPrimaryWriteBackResult(out _));

        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        RetireRecord[] retireRecords = new RetireRecord[2];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);

        Assert.Equal(0, retireRecordCount);
    }

    [Theory]
    [InlineData(InstructionsEnum.VLOAD)]
    [InlineData(InstructionsEnum.VSTORE)]
    public void VectorTransferAddressingSideband_FailsClosedBeforeMaterialization(
        InstructionsEnum opcode)
    {
        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            HasDataType = true,
            DataType = (byte)DataTypeEnum.INT32,
            HasVectorAddressingContour = true,
            IndexedAddressing = true,
            HasVectorPayload = true,
            VectorPrimaryPointer = 0x1000,
            VectorSecondaryPointer = 0x2000,
            VectorStreamLength = 8,
            VectorStride = 4,
            PredicateMask = 0xFF
        };

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)opcode, context));

        Assert.Contains("unsupported indexed vector-transfer addressing", exception.Message, StringComparison.Ordinal);
        Assert.Contains("fail closed", exception.Message, StringComparison.Ordinal);
        Assert.Contains("non-representable compat surface", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(InstructionsEnum.VCMPEQ, "VCMPEQ")]
    [InlineData(InstructionsEnum.VCMPNE, "VCMPNE")]
    [InlineData(InstructionsEnum.VCMPLT, "VCMPLT")]
    [InlineData(InstructionsEnum.VCMPLE, "VCMPLE")]
    [InlineData(InstructionsEnum.VCMPGT, "VCMPGT")]
    [InlineData(InstructionsEnum.VCMPGE, "VCMPGE")]
    public void VectorComparisonPredicatePublicationStatus_MatchesPublishedPredicateMaterializedContour(
        InstructionsEnum opcode,
        string mnemonic)
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);

        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("VectorComparisonPredicatePublication", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.MandatoryCoreOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.DescriptorOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.CarrierOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ParserOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ReservedOpcodes);

        Assert.Equal((ushort)opcode, ResolveIsaOpcodeValue(opcode));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
        Assert.NotNull(info);
        Assert.Equal(mnemonic, info!.Value.Mnemonic);
        Assert.Equal(OpcodeCategory.Comparison, info.Value.Category);
        Assert.True(info.Value.IsVector);
        Assert.True(OpcodeRegistry.IsComparisonOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsMaskManipOp((uint)opcode));
        Assert.Equal(InstructionFlags.MemoryRead, info.Value.Flags & InstructionFlags.MemoryRead);
        Assert.Equal(InstructionFlags.None, info.Value.Flags & InstructionFlags.MemoryWrite);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(opcode));

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);
        Assert.NotNull(descriptor);
        Assert.True(InstructionRegistry.IsRegistered((uint)opcode));
        Assert.Equal(info.Value.ExecutionLatency, descriptor!.Latency);
        Assert.Equal(2, descriptor.MemFootprintClass);
        Assert.Equal(false, descriptor.IsMemoryOp);
        Assert.Equal(false, descriptor.WritesRegister);

        VectorComparisonMicroOp microOp = MaterializeVectorComparisonMicroOp(opcode);
        microOp.OwnerThreadId = 2;
        microOp.VirtualThreadId = 2;
        microOp.OwnerContextId = 2;
        microOp.RefreshAdmissionMetadata();

        Assert.False(microOp.IsMemoryOp);
        Assert.True(microOp.HasSideEffects);
        Assert.True(microOp.AdmissionMetadata.HasSideEffects);
        Assert.False(microOp.WritesRegister);
        Assert.Empty(microOp.ReadRegisters);
        Assert.Empty(microOp.WriteRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(2, microOp.ReadMemoryRanges.Count);
        Assert.Equal(0x200UL, microOp.ReadMemoryRanges[0].Address);
        Assert.Equal(8UL, microOp.ReadMemoryRanges[0].Length);
        Assert.Equal(0x300UL, microOp.ReadMemoryRanges[1].Address);
        Assert.Equal(8UL, microOp.ReadMemoryRanges[1].Length);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.NotEqual(ResourceBitset.Zero, microOp.ResourceMask);
        Assert.True(microOp.SafetyMask.IsNonZero);
        Assert.True(microOp.AdmissionMetadata.StructuralSafetyMask.IsNonZero);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.TryGetPrimaryWriteBackResult(out _));
    }

    [Theory]
    [InlineData(InstructionsEnum.VCMPEQ)]
    [InlineData(InstructionsEnum.VCMPNE)]
    [InlineData(InstructionsEnum.VCMPLT)]
    [InlineData(InstructionsEnum.VCMPLE)]
    [InlineData(InstructionsEnum.VCMPGT)]
    [InlineData(InstructionsEnum.VCMPGE)]
    public void VectorComparisonPredicatePublicationAddressingSideband_FailsClosedBeforeMaterialization(
        InstructionsEnum opcode)
    {
        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            HasDataType = true,
            DataType = (byte)DataTypeEnum.INT32,
            HasVectorAddressingContour = true,
            IndexedAddressing = true,
            HasVectorPayload = true,
            VectorPrimaryPointer = 0x1000,
            VectorSecondaryPointer = 0x2000,
            VectorStreamLength = 8,
            VectorStride = 4,
            PredicateMask = 0xFF
        };

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)opcode, context));

        Assert.Contains("unsupported indexed vector-comparison addressing", exception.Message, StringComparison.Ordinal);
        Assert.Contains("fail closed", exception.Message, StringComparison.Ordinal);
        Assert.Contains("non-representable compat surface", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(InstructionsEnum.VMAND, "VMAND")]
    [InlineData(InstructionsEnum.VMOR, "VMOR")]
    [InlineData(InstructionsEnum.VMXOR, "VMXOR")]
    [InlineData(InstructionsEnum.VMNOT, "VMNOT")]
    public void VectorPredicateMaskPublicationStatus_MatchesPublishedPredicateMaterializedContour(
        InstructionsEnum opcode,
        string mnemonic)
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);

        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("VectorPredicateMaskPublication", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.MandatoryCoreOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.DescriptorOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.CarrierOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ParserOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ReservedOpcodes);

        Assert.Equal((ushort)opcode, ResolveIsaOpcodeValue(opcode));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
        Assert.NotNull(info);
        Assert.Equal(mnemonic, info!.Value.Mnemonic);
        Assert.True(info.Value.IsVector);
        Assert.True(OpcodeRegistry.IsMaskManipOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsReductionOp((uint)opcode));
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(opcode));

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);
        Assert.NotNull(descriptor);
        Assert.True(InstructionRegistry.IsRegistered((uint)opcode));
        Assert.Equal(info.Value.ExecutionLatency, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.Equal(false, descriptor.IsMemoryOp);
        Assert.Equal(false, descriptor.WritesRegister);

        VectorMaskOpMicroOp microOp = MaterializeVectorPredicateMaskMicroOp(opcode);
        microOp.OwnerThreadId = 2;
        microOp.VirtualThreadId = 2;
        microOp.OwnerContextId = 2;
        microOp.RefreshAdmissionMetadata();

        Assert.False(microOp.IsMemoryOp);
        Assert.True(microOp.HasSideEffects);
        Assert.True(microOp.AdmissionMetadata.HasSideEffects);
        Assert.False(microOp.WritesRegister);
        Assert.Empty(microOp.ReadRegisters);
        Assert.Empty(microOp.WriteRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.Empty(microOp.ReadMemoryRanges);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.Equal(ResourceBitset.Zero, microOp.ResourceMask);
        Assert.True(microOp.SafetyMask.IsNonZero);
        Assert.True(microOp.AdmissionMetadata.StructuralSafetyMask.IsNonZero);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);

        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.SetPredicateRegister(1, 0b1010UL);
        core.SetPredicateRegister(2, 0b1100UL);
        core.SetPredicateRegister(3, 0x5AUL);

        Assert.True(microOp.Execute(ref core));
        Assert.Equal(ResolveExpectedPredicateMaskResult(opcode), core.GetPredicateRegister(3));
        Assert.False(microOp.TryGetPrimaryWriteBackResult(out _));

        RetireRecord[] retireRecords = new RetireRecord[2];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);

        Assert.Equal(0, retireRecordCount);
    }

    [Theory]
    [InlineData(InstructionsEnum.VMAND)]
    [InlineData(InstructionsEnum.VMOR)]
    [InlineData(InstructionsEnum.VMXOR)]
    [InlineData(InstructionsEnum.VMNOT)]
    public void VectorPredicateMaskPublicationAddressingSideband_FailsClosedBeforeMaterialization(
        InstructionsEnum opcode)
    {
        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            HasDataType = true,
            DataType = (byte)DataTypeEnum.INT32,
            HasImmediate = true,
            Immediate = (ushort)(1 | (2 << 4) | (3 << 8)),
            HasVectorAddressingContour = true,
            IndexedAddressing = true,
            HasVectorPayload = true,
            VectorPrimaryPointer = 0x1000,
            VectorSecondaryPointer = 0x2000,
            VectorStreamLength = 8,
            VectorStride = 4,
            PredicateMask = 0xFF
        };

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)opcode, context));

        Assert.Contains("unsupported indexed vector-mask addressing", exception.Message, StringComparison.Ordinal);
        Assert.Contains("fail closed", exception.Message, StringComparison.Ordinal);
        Assert.Contains("addressing-tag compat surface", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void VectorMaskPopCountStatus_MatchesPublishedScalarResultMaterializedContour()
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("VPOPC");

        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("VectorPredicateMaskScalarResult", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains("VPOPC", IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain("VPOPC", IsaV4Surface.MandatoryCoreOpcodes);
        Assert.DoesNotContain("VPOPC", IsaV4Surface.DescriptorOnlyOpcodes);
        Assert.DoesNotContain("VPOPC", IsaV4Surface.CarrierOnlyOpcodes);
        Assert.DoesNotContain("VPOPC", IsaV4Surface.OptionalDisabledOpcodes);
        Assert.DoesNotContain("VPOPC", IsaV4Surface.ParserOnlyOpcodes);
        Assert.DoesNotContain("VPOPC", IsaV4Surface.ReservedOpcodes);

        Assert.Equal((ushort)InstructionsEnum.VPOPC, IsaOpcodeValues.VPOPC);

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)InstructionsEnum.VPOPC);
        Assert.NotNull(info);
        Assert.Equal("VPOPC", info!.Value.Mnemonic);
        Assert.True(info.Value.IsVector);
        Assert.True(OpcodeRegistry.IsMaskManipOp((uint)InstructionsEnum.VPOPC));
        Assert.False(OpcodeRegistry.IsReductionOp((uint)InstructionsEnum.VPOPC));
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(InstructionsEnum.VPOPC));

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.VPOPC);
        Assert.NotNull(descriptor);
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.VPOPC));
        Assert.Equal(info.Value.ExecutionLatency, descriptor!.Latency);
        Assert.Equal(0, descriptor.MemFootprintClass);
        Assert.Equal(false, descriptor.IsMemoryOp);
        Assert.Equal(true, descriptor.WritesRegister);

        VectorMaskPopCountMicroOp microOp = MaterializeVectorMaskPopCountMicroOp();
        microOp.OwnerThreadId = 2;
        microOp.VirtualThreadId = 2;
        microOp.OwnerContextId = 2;
        microOp.RefreshAdmissionMetadata();

        Assert.False(microOp.IsMemoryOp);
        Assert.True(microOp.WritesRegister);
        Assert.Equal((ushort)6, microOp.DestRegID);
        Assert.Equal(new[] { 6 }, microOp.WriteRegisters);
        Assert.Equal(new[] { 6 }, microOp.AdmissionMetadata.WriteRegisters);
        Assert.Empty(microOp.ReadRegisters);
        Assert.Empty(microOp.ReadMemoryRanges);
        Assert.Empty(microOp.WriteMemoryRanges);
        Assert.NotEqual(ResourceBitset.Zero, microOp.ResourceMask);
        Assert.True(microOp.SafetyMask.IsNonZero);
        Assert.True(microOp.AdmissionMetadata.StructuralSafetyMask.IsNonZero);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);

        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.SetPredicateRegister(3, 0b101101UL);

        Assert.True(microOp.Execute(ref core));
        Assert.True(microOp.TryGetPrimaryWriteBackResult(out ulong writebackValue));
        Assert.Equal(4UL, writebackValue);

        RetireRecord[] retireRecords = new RetireRecord[2];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);

        Assert.Equal(1, retireRecordCount);
        Assert.True(retireRecords[0].IsRegisterWrite);
        Assert.Equal(2, retireRecords[0].VtId);
        Assert.Equal(6, retireRecords[0].ArchReg);
        Assert.Equal(4UL, retireRecords[0].Value);
    }

    [Fact]
    public void VectorMaskPopCountAddressingSideband_FailsClosedBeforeMaterialization()
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.VPOPC,
            HasDataType = true,
            DataType = (byte)DataTypeEnum.INT32,
            HasImmediate = true,
            Immediate = (ushort)(3 | (6 << 8)),
            HasVectorAddressingContour = true,
            IndexedAddressing = true,
            HasVectorPayload = true,
            VectorPrimaryPointer = 0x1000,
            VectorSecondaryPointer = 0x2000,
            VectorStreamLength = 8,
            VectorStride = 4,
            PredicateMask = 0xFF
        };

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.VPOPC, context));

        Assert.Contains("unsupported indexed vector-mask-popcount addressing", exception.Message, StringComparison.Ordinal);
        Assert.Contains("fail closed", exception.Message, StringComparison.Ordinal);
        Assert.Contains("addressing-tag compat surface", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(InstructionsEnum.VCOMPRESS, "VCOMPRESS")]
    [InlineData(InstructionsEnum.VEXPAND, "VEXPAND")]
    public void VectorPredicativeMovementStatus_MatchesPublishedSingleSurfaceMaterializedContour(
        InstructionsEnum opcode,
        string mnemonic)
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);

        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("VectorPredicativeMovement", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.MandatoryCoreOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.DescriptorOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.CarrierOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ParserOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ReservedOpcodes);

        Assert.Equal((ushort)opcode, ResolveIsaOpcodeValue(opcode));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
        Assert.NotNull(info);
        Assert.Equal(mnemonic, info!.Value.Mnemonic);
        Assert.Equal(OpcodeCategory.Vector, info.Value.Category);
        Assert.True(info.Value.IsVector);
        Assert.False(OpcodeRegistry.IsComparisonOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsMaskManipOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsReductionOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsFmaOp((uint)opcode));
        Assert.True(OpcodeRegistry.RequiresVectorPayloadProjection((uint)opcode));
        Assert.Equal(InstructionFlags.MemoryRead, info.Value.Flags & InstructionFlags.MemoryRead);
        Assert.Equal(InstructionFlags.MemoryWrite, info.Value.Flags & InstructionFlags.MemoryWrite);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(opcode));

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);
        Assert.NotNull(descriptor);
        Assert.True(InstructionRegistry.IsRegistered((uint)opcode));
        Assert.Equal(info.Value.ExecutionLatency, descriptor!.Latency);
        Assert.Equal(2, descriptor.MemFootprintClass);
        Assert.Equal(false, descriptor.IsMemoryOp);
        Assert.Equal(false, descriptor.WritesRegister);

        VectorPredicativeMovementMicroOp microOp = MaterializeVectorPredicativeMovementMicroOp(opcode);
        microOp.OwnerThreadId = 2;
        microOp.VirtualThreadId = 2;
        microOp.OwnerContextId = 2;
        microOp.RefreshAdmissionMetadata();

        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.WritesRegister);
        Assert.Empty(microOp.ReadRegisters);
        Assert.Empty(microOp.WriteRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        var readRange = Assert.Single(microOp.ReadMemoryRanges);
        Assert.Equal(0x240UL, readRange.Address);
        Assert.Equal(8UL, readRange.Length);
        var writeRange = Assert.Single(microOp.WriteMemoryRanges);
        Assert.Equal(0x240UL, writeRange.Address);
        Assert.Equal(8UL, writeRange.Length);
        Assert.NotEqual(ResourceBitset.Zero, microOp.ResourceMask);
        Assert.True(microOp.SafetyMask.IsNonZero);
        Assert.True(microOp.AdmissionMetadata.StructuralSafetyMask.IsNonZero);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.TryGetPrimaryWriteBackResult(out _));

        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        RetireRecord[] retireRecords = new RetireRecord[2];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);

        Assert.Equal(0, retireRecordCount);
    }

    [Theory]
    [InlineData(InstructionsEnum.VCOMPRESS)]
    [InlineData(InstructionsEnum.VEXPAND)]
    public void VectorPredicativeMovementAddressingSideband_FailsClosedBeforeMaterialization(
        InstructionsEnum opcode)
    {
        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            HasDataType = true,
            DataType = (byte)DataTypeEnum.INT32,
            HasVectorAddressingContour = true,
            IndexedAddressing = true,
            HasVectorPayload = true,
            VectorPrimaryPointer = 0x1000,
            VectorSecondaryPointer = 0x2000,
            VectorStreamLength = 8,
            VectorStride = 4,
            PredicateMask = 0xFF
        };

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)opcode, context));

        Assert.Contains("unsupported indexed vector-predicative-movement addressing", exception.Message, StringComparison.Ordinal);
        Assert.Contains("fail closed", exception.Message, StringComparison.Ordinal);
        Assert.Contains("non-representable compat surface", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(InstructionsEnum.VPERMUTE, "VPERMUTE")]
    [InlineData(InstructionsEnum.VRGATHER, "VRGATHER")]
    public void VectorPermutationStatus_MatchesPublishedIndexedMaterializedContour(
        InstructionsEnum opcode,
        string mnemonic)
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);

        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("VectorPermutation", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.MandatoryCoreOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.DescriptorOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.CarrierOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ParserOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ReservedOpcodes);

        Assert.Equal((ushort)opcode, ResolveIsaOpcodeValue(opcode));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
        Assert.NotNull(info);
        Assert.Equal(mnemonic, info!.Value.Mnemonic);
        Assert.Equal(OpcodeCategory.Vector, info.Value.Category);
        Assert.True(info.Value.IsVector);
        Assert.True((info.Value.Flags & InstructionFlags.Indexed) != 0);
        Assert.Equal(InstructionFlags.MemoryRead, info.Value.Flags & InstructionFlags.MemoryRead);
        Assert.Equal(InstructionFlags.MemoryWrite, info.Value.Flags & InstructionFlags.MemoryWrite);
        Assert.False(OpcodeRegistry.IsComparisonOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsMaskManipOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsReductionOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsFmaOp((uint)opcode));
        Assert.True(OpcodeRegistry.RequiresVectorPayloadProjection((uint)opcode));
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(opcode));

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);
        Assert.NotNull(descriptor);
        Assert.True(InstructionRegistry.IsRegistered((uint)opcode));
        Assert.Equal(info.Value.ExecutionLatency, descriptor!.Latency);
        Assert.Equal(2, descriptor.MemFootprintClass);
        Assert.Equal(false, descriptor.IsMemoryOp);
        Assert.Equal(false, descriptor.WritesRegister);

        VectorPermutationMicroOp microOp = MaterializeVectorPermutationMicroOp(opcode);
        microOp.OwnerThreadId = 2;
        microOp.VirtualThreadId = 2;
        microOp.OwnerContextId = 2;
        microOp.RefreshAdmissionMetadata();

        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.WritesRegister);
        Assert.Empty(microOp.ReadRegisters);
        Assert.Empty(microOp.WriteRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(2, microOp.AdmissionMetadata.ReadMemoryRanges.Count);
        Assert.Equal((0x2A0UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[0]);
        Assert.Equal((0x3A0UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[1]);
        Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal((0x2A0UL, 16UL), microOp.AdmissionMetadata.WriteMemoryRanges[0]);
        Assert.NotEqual(ResourceBitset.Zero, microOp.ResourceMask);
        Assert.True(microOp.SafetyMask.IsNonZero);
        Assert.True(microOp.AdmissionMetadata.StructuralSafetyMask.IsNonZero);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.TryGetPrimaryWriteBackResult(out _));
    }

    [Theory]
    [InlineData(InstructionsEnum.VSLIDEUP, "VSLIDEUP")]
    [InlineData(InstructionsEnum.VSLIDEDOWN, "VSLIDEDOWN")]
    public void VectorSlideStatus_MatchesPublishedSingleSurfaceMaterializedContour(
        InstructionsEnum opcode,
        string mnemonic)
    {
        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);

        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("VectorSlide", status.ExtensionName);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);
        Assert.True(status.IsExecutableClaim);

        Assert.Contains(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.MandatoryCoreOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.DescriptorOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.CarrierOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ParserOnlyOpcodes);
        Assert.DoesNotContain(mnemonic, IsaV4Surface.ReservedOpcodes);

        Assert.Equal((ushort)opcode, ResolveIsaOpcodeValue(opcode));

        OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
        Assert.NotNull(info);
        Assert.Equal(mnemonic, info!.Value.Mnemonic);
        Assert.Equal(OpcodeCategory.Vector, info.Value.Category);
        Assert.True(info.Value.IsVector);
        Assert.False(OpcodeRegistry.IsComparisonOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsMaskManipOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsReductionOp((uint)opcode));
        Assert.False(OpcodeRegistry.IsFmaOp((uint)opcode));
        Assert.True(OpcodeRegistry.RequiresVectorPayloadProjection((uint)opcode));
        Assert.Equal(InstructionFlags.MemoryRead, info.Value.Flags & InstructionFlags.MemoryRead);
        Assert.Equal(InstructionFlags.MemoryWrite, info.Value.Flags & InstructionFlags.MemoryWrite);
        Assert.Equal((InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(opcode));

        MicroOpDescriptor? descriptor = InstructionRegistry.GetDescriptor((uint)opcode);
        Assert.NotNull(descriptor);
        Assert.True(InstructionRegistry.IsRegistered((uint)opcode));
        Assert.Equal(info.Value.ExecutionLatency, descriptor!.Latency);
        Assert.Equal(2, descriptor.MemFootprintClass);
        Assert.Equal(false, descriptor.IsMemoryOp);
        Assert.Equal(false, descriptor.WritesRegister);

        VectorSlideMicroOp microOp = MaterializeVectorSlideMicroOp(opcode);
        microOp.OwnerThreadId = 2;
        microOp.VirtualThreadId = 2;
        microOp.OwnerContextId = 2;
        microOp.RefreshAdmissionMetadata();

        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.WritesRegister);
        Assert.Empty(microOp.ReadRegisters);
        Assert.Empty(microOp.WriteRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        var readRange = Assert.Single(microOp.ReadMemoryRanges);
        Assert.Equal(0x2C0UL, readRange.Address);
        Assert.Equal(16UL, readRange.Length);
        var writeRange = Assert.Single(microOp.WriteMemoryRanges);
        Assert.Equal(0x2C0UL, writeRange.Address);
        Assert.Equal(16UL, writeRange.Length);
        Assert.NotEqual(ResourceBitset.Zero, microOp.ResourceMask);
        Assert.True(microOp.SafetyMask.IsNonZero);
        Assert.True(microOp.AdmissionMetadata.StructuralSafetyMask.IsNonZero);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.TryGetPrimaryWriteBackResult(out _));

        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        RetireRecord[] retireRecords = new RetireRecord[2];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);

        Assert.Equal(0, retireRecordCount);
    }

    private static VectorBinaryOpMicroOp MaterializeVectorBinaryMicroOp(
        InstructionsEnum opcode)
    {
        VLIW_Instruction[] rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = 0x200,
            Src2Pointer = 0x300,
            StreamLength = 2,
            Stride = 4,
            VirtualThreadId = 2
        };

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7600, bundleSerial: 148);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        return Assert.IsType<VectorBinaryOpMicroOp>(carrierBundle[0]);
    }

    private static VectorFmaMicroOp MaterializeVectorFmaMicroOp(
        InstructionsEnum opcode)
    {
        InitializeMainMemoryForVectorFmaDescriptor();
        SeedVectorFmaTriOpDescriptor(
            descriptorAddress: 0x340,
            srcAPointer: 0x440,
            srcBPointer: 0x540,
            strideA: 4,
            strideB: 4);

        VLIW_Instruction[] rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.FLOAT32,
            PredicateMask = 0x07,
            DestSrc1Pointer = 0x240,
            Src2Pointer = 0x340,
            StreamLength = 4,
            Stride = 4,
            VirtualThreadId = 2
        };

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7680, bundleSerial: 149);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        return Assert.IsType<VectorFmaMicroOp>(carrierBundle[0]);
    }

    private static void InitializeMainMemoryForVectorFmaDescriptor()
    {
        IOMMU.Initialize();
        IOMMU.RegisterDevice(0);
        IOMMU.Map(
            deviceID: 0,
            ioVirtualAddress: 0,
            physicalAddress: 0,
            size: 0x100000000UL,
            permissions: IOMMUAccessPermissions.ReadWrite);

        YAKSys_Hybrid_CPU.Processor proc = default;
        YAKSys_Hybrid_CPU.Processor.Memory = new MemorySubsystem(ref proc);
    }

    private static void SeedVectorFmaTriOpDescriptor(
        ulong descriptorAddress,
        ulong srcAPointer,
        ulong srcBPointer,
        ushort strideA,
        ushort strideB)
    {
        byte[] descriptor = new byte[20];
        BitConverter.GetBytes(srcAPointer).CopyTo(descriptor, 0);
        BitConverter.GetBytes(srcBPointer).CopyTo(descriptor, 8);
        BitConverter.GetBytes(strideA).CopyTo(descriptor, 16);
        BitConverter.GetBytes(strideB).CopyTo(descriptor, 18);
        YAKSys_Hybrid_CPU.Processor.MainMemory.WriteToPosition(descriptor, descriptorAddress);
    }

    private static VectorUnaryOpMicroOp MaterializeVectorUnaryMicroOp(
        InstructionsEnum opcode)
    {
        VLIW_Instruction[] rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = 0x280,
            StreamLength = 2,
            Stride = 4,
            VirtualThreadId = 2
        };

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7700, bundleSerial: 149);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        return Assert.IsType<VectorUnaryOpMicroOp>(carrierBundle[0]);
    }

    private static VectorReductionMicroOp MaterializeVectorReductionMicroOp(
        InstructionsEnum opcode)
    {
        VLIW_Instruction[] rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.UINT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = 0x2C0,
            StreamLength = 4,
            Stride = 4,
            VirtualThreadId = 2
        };

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7800, bundleSerial: 150);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        return Assert.IsType<VectorReductionMicroOp>(carrierBundle[0]);
    }

    private static VectorDotProductMicroOp MaterializeVectorDotProductMicroOp(
        InstructionsEnum opcode,
        DataTypeEnum dataType,
        ushort streamLength,
        ushort stride)
    {
        VLIW_Instruction[] rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = dataType,
            PredicateMask = 0x0F,
            DestSrc1Pointer = 0x340,
            Src2Pointer = 0x3C0,
            StreamLength = streamLength,
            Stride = stride,
            VirtualThreadId = 2
        };

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7850, bundleSerial: 151);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        return Assert.IsType<VectorDotProductMicroOp>(carrierBundle[0]);
    }

    private static VectorPredicativeMovementMicroOp MaterializeVectorPredicativeMovementMicroOp(
        InstructionsEnum opcode)
    {
        VLIW_Instruction[] rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0b1011,
            DestSrc1Pointer = 0x240,
            StreamLength = 2,
            Stride = 4,
            VirtualThreadId = 2
        };

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7400, bundleSerial: 146);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        return Assert.IsType<VectorPredicativeMovementMicroOp>(carrierBundle[0]);
    }

    private static VectorPermutationMicroOp MaterializeVectorPermutationMicroOp(
        InstructionsEnum opcode)
    {
        VLIW_Instruction[] rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.UINT32,
            PredicateMask = 0x0D,
            DestSrc1Pointer = 0x2A0,
            Src2Pointer = 0x3A0,
            StreamLength = 4,
            Stride = 4,
            VirtualThreadId = 2
        };

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7400, bundleSerial: 150);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        return Assert.IsType<VectorPermutationMicroOp>(carrierBundle[0]);
    }

    private static VectorSlideMicroOp MaterializeVectorSlideMicroOp(
        InstructionsEnum opcode)
    {
        VLIW_Instruction[] rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0b1011,
            DestSrc1Pointer = 0x2C0,
            Immediate = 1,
            StreamLength = 4,
            Stride = 4,
            VirtualThreadId = 2
        };

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7450, bundleSerial: 151);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        return Assert.IsType<VectorSlideMicroOp>(carrierBundle[0]);
    }

    private static VectorTransferMicroOp MaterializeVectorTransferMicroOp(
        InstructionsEnum opcode)
    {
        VLIW_Instruction[] rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = 0x200,
            Src2Pointer = 0x300,
            StreamLength = 2,
            Stride = 4,
            VirtualThreadId = 2
        };

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7500, bundleSerial: 147);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        return Assert.IsType<VectorTransferMicroOp>(carrierBundle[0]);
    }

    private static VectorMaskOpMicroOp MaterializeVectorPredicateMaskMicroOp(
        InstructionsEnum opcode)
    {
        VLIW_Instruction[] rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            Immediate = (ushort)(1 | (2 << 4) | (3 << 8)),
            StreamLength = 8,
            VirtualThreadId = 2
        };

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7100, bundleSerial: 143);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        return Assert.IsType<VectorMaskOpMicroOp>(carrierBundle[0]);
    }

    private static VectorComparisonMicroOp MaterializeVectorComparisonMicroOp(
        InstructionsEnum opcode)
    {
        VLIW_Instruction[] rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = 0x200,
            Src2Pointer = 0x300,
            StreamLength = 2,
            Stride = 4,
            VirtualThreadId = 2
        };

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7300, bundleSerial: 145);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        return Assert.IsType<VectorComparisonMicroOp>(carrierBundle[0]);
    }

    private static VectorMaskPopCountMicroOp MaterializeVectorMaskPopCountMicroOp()
    {
        VLIW_Instruction[] rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.VPOPC,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            Immediate = (ushort)(3 | (6 << 8)),
            StreamLength = 8,
            VirtualThreadId = 2
        };

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7200, bundleSerial: 144);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        return Assert.IsType<VectorMaskPopCountMicroOp>(carrierBundle[0]);
    }

    private static VConfigMicroOp MaterializeVectorConfigMicroOp(
        InstructionsEnum opcode)
    {
        VLIW_Instruction[] rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = CreateVectorConfigInstruction(opcode);

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7000, bundleSerial: 142);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        return Assert.IsType<VConfigMicroOp>(carrierBundle[0]);
    }

    private static VLIW_Instruction CreateVectorConfigInstruction(
        InstructionsEnum opcode)
    {
        return opcode switch
        {
            InstructionsEnum.VSETVL => new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataType = 0,
                Word1 = VLIW_Instruction.PackArchRegs(4, 5, 6),
                PredicateMask = 0xFF,
                VirtualThreadId = 2
            },
            InstructionsEnum.VSETVLI => new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataType = 0x80,
                Word1 = VLIW_Instruction.PackArchRegs(
                    7,
                    8,
                    VLIW_Instruction.NoArchReg),
                StreamLength = 1,
                PredicateMask = 0xFF,
                VirtualThreadId = 2
            },
            InstructionsEnum.VSETIVLI => new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataType = 0x40,
                Immediate = 13,
                Word1 = VLIW_Instruction.PackArchRegs(
                    9,
                    VLIW_Instruction.NoArchReg,
                    VLIW_Instruction.NoArchReg),
                PredicateMask = 0xFF,
                VirtualThreadId = 2
            },
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };
    }

    private static InstructionIR BuildVectorConfigIr(
        InstructionsEnum opcode)
    {
        return new InstructionIR
        {
            CanonicalOpcode = opcode,
            Class = InstructionClass.System,
            SerializationClass = SerializationClass.FullSerial,
            Rd = (byte)ExpectedVectorConfigDestinationRegister(opcode),
            Rs1 = opcode == InstructionsEnum.VSETVL ? (byte)5 :
                opcode == InstructionsEnum.VSETVLI ? (byte)8 : VLIW_Instruction.NoArchReg,
            Rs2 = opcode == InstructionsEnum.VSETVL ? (byte)6 : VLIW_Instruction.NoArchReg,
            Imm = opcode == InstructionsEnum.VSETIVLI ? 13 : 0
        };
    }

    private static int[] ExpectedVectorConfigReadRegisters(
        InstructionsEnum opcode)
    {
        return opcode switch
        {
            InstructionsEnum.VSETVL => new[] { 5, 6 },
            InstructionsEnum.VSETVLI => new[] { 8 },
            InstructionsEnum.VSETIVLI => Array.Empty<int>(),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };
    }

    private static int[] ExpectedVectorConfigWriteRegisters(
        InstructionsEnum opcode) =>
        new[] { ExpectedVectorConfigDestinationRegister(opcode) };

    private static int ExpectedVectorConfigDestinationRegister(
        InstructionsEnum opcode)
    {
        return opcode switch
        {
            InstructionsEnum.VSETVL => 4,
            InstructionsEnum.VSETVLI => 7,
            InstructionsEnum.VSETIVLI => 9,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };
    }

    private static ulong ExpectedVectorConfigActualVectorLength(
        InstructionsEnum opcode)
    {
        return opcode switch
        {
            InstructionsEnum.VSETVL => RVV_Config.VLMAX,
            InstructionsEnum.VSETVLI => RVV_Config.VLMAX,
            InstructionsEnum.VSETIVLI => 13UL,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };
    }

    private static ulong ExpectedVectorConfigVType(
        InstructionsEnum opcode)
    {
        return opcode switch
        {
            InstructionsEnum.VSETVL => 0xC0UL,
            InstructionsEnum.VSETVLI => 0x80UL,
            InstructionsEnum.VSETIVLI => 0x40UL,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };
    }

    private static VectorConfigOperationKind ExpectedVectorConfigOperationKind(
        InstructionsEnum opcode)
    {
        return opcode switch
        {
            InstructionsEnum.VSETVL => VectorConfigOperationKind.Vsetvl,
            InstructionsEnum.VSETVLI => VectorConfigOperationKind.Vsetvli,
            InstructionsEnum.VSETIVLI => VectorConfigOperationKind.Vsetivli,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };
    }

    private static ushort ResolveIsaOpcodeValue(InstructionsEnum opcode)
    {
        return opcode switch
        {
            InstructionsEnum.VSETVL => IsaOpcodeValues.VSETVL,
            InstructionsEnum.VSETVLI => IsaOpcodeValues.VSETVLI,
            InstructionsEnum.VSETIVLI => IsaOpcodeValues.VSETIVLI,
            InstructionsEnum.VADD => IsaOpcodeValues.VADD,
            InstructionsEnum.VSUB => IsaOpcodeValues.VSUB,
            InstructionsEnum.VMUL => IsaOpcodeValues.VMUL,
            InstructionsEnum.VDIV => IsaOpcodeValues.VDIV,
            InstructionsEnum.VSQRT => IsaOpcodeValues.VSQRT,
            InstructionsEnum.VMOD => IsaOpcodeValues.VMOD,
            InstructionsEnum.VLOAD => IsaOpcodeValues.VLOAD,
            InstructionsEnum.VSTORE => IsaOpcodeValues.VSTORE,
            InstructionsEnum.VXOR => IsaOpcodeValues.VXOR,
            InstructionsEnum.VOR => IsaOpcodeValues.VOR,
            InstructionsEnum.VAND => IsaOpcodeValues.VAND,
            InstructionsEnum.VNOT => IsaOpcodeValues.VNOT,
            InstructionsEnum.VSLL => IsaOpcodeValues.VSLL,
            InstructionsEnum.VSRL => IsaOpcodeValues.VSRL,
            InstructionsEnum.VSRA => IsaOpcodeValues.VSRA,
            InstructionsEnum.VFMADD => IsaOpcodeValues.VFMADD,
            InstructionsEnum.VFMSUB => IsaOpcodeValues.VFMSUB,
            InstructionsEnum.VFNMADD => IsaOpcodeValues.VFNMADD,
            InstructionsEnum.VFNMSUB => IsaOpcodeValues.VFNMSUB,
            InstructionsEnum.VMIN => IsaOpcodeValues.VMIN,
            InstructionsEnum.VMAX => IsaOpcodeValues.VMAX,
            InstructionsEnum.VMINU => IsaOpcodeValues.VMINU,
            InstructionsEnum.VMAXU => IsaOpcodeValues.VMAXU,
            InstructionsEnum.VMAND => IsaOpcodeValues.VMAND,
            InstructionsEnum.VMOR => IsaOpcodeValues.VMOR,
            InstructionsEnum.VMXOR => IsaOpcodeValues.VMXOR,
            InstructionsEnum.VMNOT => IsaOpcodeValues.VMNOT,
            InstructionsEnum.VCMPEQ => IsaOpcodeValues.VCMPEQ,
            InstructionsEnum.VCMPNE => IsaOpcodeValues.VCMPNE,
            InstructionsEnum.VCMPLT => IsaOpcodeValues.VCMPLT,
            InstructionsEnum.VCMPLE => IsaOpcodeValues.VCMPLE,
            InstructionsEnum.VCMPGT => IsaOpcodeValues.VCMPGT,
            InstructionsEnum.VCMPGE => IsaOpcodeValues.VCMPGE,
            InstructionsEnum.VPOPC => IsaOpcodeValues.VPOPC,
            InstructionsEnum.VCOMPRESS => IsaOpcodeValues.VCOMPRESS,
            InstructionsEnum.VEXPAND => IsaOpcodeValues.VEXPAND,
            InstructionsEnum.VPERMUTE => IsaOpcodeValues.VPERMUTE,
            InstructionsEnum.VRGATHER => IsaOpcodeValues.VRGATHER,
            InstructionsEnum.VSLIDEUP => IsaOpcodeValues.VSLIDEUP,
            InstructionsEnum.VSLIDEDOWN => IsaOpcodeValues.VSLIDEDOWN,
            InstructionsEnum.VREVERSE => IsaOpcodeValues.VREVERSE,
            InstructionsEnum.VPOPCNT => IsaOpcodeValues.VPOPCNT,
            InstructionsEnum.VCLZ => IsaOpcodeValues.VCLZ,
            InstructionsEnum.VCTZ => IsaOpcodeValues.VCTZ,
            InstructionsEnum.VBREV8 => IsaOpcodeValues.VBREV8,
            InstructionsEnum.VREDSUM => IsaOpcodeValues.VREDSUM,
            InstructionsEnum.VREDMAX => IsaOpcodeValues.VREDMAX,
            InstructionsEnum.VREDMIN => IsaOpcodeValues.VREDMIN,
            InstructionsEnum.VREDMAXU => IsaOpcodeValues.VREDMAXU,
            InstructionsEnum.VREDMINU => IsaOpcodeValues.VREDMINU,
            InstructionsEnum.VREDAND => IsaOpcodeValues.VREDAND,
            InstructionsEnum.VREDOR => IsaOpcodeValues.VREDOR,
            InstructionsEnum.VREDXOR => IsaOpcodeValues.VREDXOR,
            InstructionsEnum.VDOT => IsaOpcodeValues.VDOT,
            InstructionsEnum.VDOTU => IsaOpcodeValues.VDOTU,
            InstructionsEnum.VDOTF => IsaOpcodeValues.VDOTF,
            InstructionsEnum.VDOT_FP8 => IsaOpcodeValues.VDOT_FP8,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };
    }

    private static ulong ResolveExpectedPredicateMaskResult(InstructionsEnum opcode)
    {
        const ulong src1 = 0b1010UL;
        const ulong src2 = 0b1100UL;

        return opcode switch
        {
            InstructionsEnum.VMAND => src1 & src2,
            InstructionsEnum.VMOR => src1 | src2,
            InstructionsEnum.VMXOR => src1 ^ src2,
            InstructionsEnum.VMNOT => ~src1,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };
    }

    private static bool HasEnumOrRegistryMnemonic(string mnemonic)
    {
        string enumCandidate = mnemonic.Replace(".", "_", StringComparison.Ordinal);
        bool hasEnum = Enum.GetNames<InstructionsEnum>().Contains(enumCandidate);
        bool hasRegistryMnemonic = OpcodeRegistry.Opcodes.Any(info =>
            string.Equals(info.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase));
        return hasEnum || hasRegistryMnemonic;
    }
}

public sealed class EncoderDecoderDeclaredRoundtripInventoryTests
{
    [Fact]
    public void GenericVectorEncoder_CanEmitMatrixOpcodeThatCanonicalDecoderRejects()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeVector1D(
            (uint)InstructionsEnum.MTILE_LOAD,
            DataTypeEnum.INT32,
            destSrc1Ptr: 0x1000,
            src2Ptr: 0x2000,
            streamLength: 4);

        var decoder = new VliwDecoderV4();
        InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(
            () => decoder.Decode(in instruction, slotIndex: 0));

        Assert.Contains("unsupported optional matrix memory contour", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Vector2DEncoder_EmitsContourThatRegisteredVectorFactoryRejects()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeVector2D(
            (uint)InstructionsEnum.VADD,
            DataTypeEnum.INT32,
            destSrc1Ptr: 0x1000,
            src2Ptr: 0x2000,
            streamLength: 8,
            colStride: 4,
            rowStride: 32,
            rowLength: 4);

        var decoder = new VliwDecoderV4();
        InstructionIR ir = decoder.Decode(in instruction, slotIndex: 0);
        Assert.Equal((ushort)InstructionsEnum.VADD, ir.CanonicalOpcode.Value);

        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.VADD,
            HasDataType = true,
            DataType = (byte)DataTypeEnum.INT32,
            HasImmediate = true,
            Immediate = instruction.Immediate,
            HasVectorAddressingContour = true,
            Is2DAddressing = true,
            HasVectorPayload = true,
            VectorPrimaryPointer = instruction.DestSrc1Pointer,
            VectorSecondaryPointer = instruction.Src2Pointer,
            VectorStreamLength = instruction.StreamLength,
            VectorStride = instruction.Stride,
            VectorRowStride = instruction.RowStride
        };

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.VADD, context));
        Assert.Contains("unsupported 2D vector-binary addressing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ControlFlowEncoder_EmitsCanonicalImmediateTargetProjection()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeControlFlow(
            (uint)InstructionsEnum.JAL,
            reg1: 1,
            reg2: VLIW_Instruction.NoArchReg,
            reg3: VLIW_Instruction.NoArchReg,
            relativeOffset: 0x1234);

        var decoder = new VliwDecoderV4();
        InstructionIR ir = decoder.Decode(in instruction, slotIndex: 7);

        Assert.Equal((ushort)InstructionsEnum.JAL, ir.CanonicalOpcode.Value);
        Assert.Equal(0x1234, ir.Imm);
        Assert.False(ir.HasAbsoluteAddressing);
        Assert.Equal(0x1234, instruction.Immediate);
        Assert.Equal(0UL, instruction.Src2Pointer);
    }

    [Theory]
    [InlineData(InstructionsEnum.JAL)]
    [InlineData(InstructionsEnum.JALR)]
    [InlineData(InstructionsEnum.BEQ)]
    public void ControlFlowDecoder_RejectsLegacySrc2PointerTargetTransport(
        InstructionsEnum opcode)
    {
        VLIW_Instruction instruction = opcode switch
        {
            InstructionsEnum.JAL => new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                Word1 = VLIW_Instruction.PackArchRegs(
                    1,
                    VLIW_Instruction.NoArchReg,
                    VLIW_Instruction.NoArchReg),
                Immediate = 0x0020,
                Src2Pointer = 0x1234
            },
            InstructionsEnum.JALR => new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                Word1 = VLIW_Instruction.PackArchRegs(
                    1,
                    2,
                    VLIW_Instruction.NoArchReg),
                Immediate = 0x0020,
                Src2Pointer = 0x1234
            },
            _ => new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                Word1 = VLIW_Instruction.PackArchRegs(
                    VLIW_Instruction.NoArchReg,
                    1,
                    2),
                Immediate = 0x0020,
                Src2Pointer = 0x1234
            }
        };

        var decoder = new VliwDecoderV4();
        InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(
            () => decoder.Decode(in instruction, slotIndex: 7));

        Assert.Contains("legacy Src2Pointer control-flow target transport", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Immediate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ControlFlowProjector_DerivesBranchTargetFromCanonicalImmediateNotRawSrc2Pointer()
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.BEQ,
            Word1 = VLIW_Instruction.PackArchRegs(
                VLIW_Instruction.NoArchReg,
                1,
                2),
            Immediate = 0x0040,
            Src2Pointer = 0x9999
        };

        var canonicalBundle = new DecodedInstructionBundle(
            bundleAddress: 0x2000,
            bundleSerial: 41,
            slots: new[]
            {
                DecodedInstruction.CreateOccupied(
                    slotIndex: 0,
                    instruction: new InstructionIR
                    {
                        CanonicalOpcode = InstructionsEnum.BEQ,
                        Class = InstructionClass.ControlFlow,
                        SerializationClass = SerializationClass.Free,
                        Rd = VLIW_Instruction.NoArchReg,
                        Rs1 = 1,
                        Rs2 = 2,
                        Imm = 0x0040
                    })
            });

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        BranchMicroOp microOp = Assert.IsType<BranchMicroOp>(carrierBundle[0]);
        Assert.Equal(0x2040UL, microOp.TargetAddress);
        Assert.True(microOp.HasRelativeTargetDisplacement);
        Assert.Equal((short)0x0040, microOp.RelativeTargetDisplacement);
    }
}

public sealed class CompilerLowererOpcodeEmissionInventoryTests
{
    [Fact]
    public void CompilerBackendContract_RejectsDescriptorOnlyLane6AsProductionLowering()
    {
        InstructionSupportStatus status =
            InstructionSupportStatusCatalog.GetStatus("DmaStreamCompute");
        Assert.Equal(IsaInstructionStatus.DescriptorOnly, status.Status);

        CompilerBackendLoweringDecision decision =
            CompilerBackendLoweringContract.EvaluateProductionDscLowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane6DmaStreamCompute,
                    State = CompilerBackendCapabilityState.DescriptorOnly,
                    UsesDescriptorEvidenceOnly = true
                });

        Assert.False(decision.IsAllowed);
        Assert.NotEqual(CompilerBackendLoweringRequirement.None, decision.MissingRequirements);
    }

    [Fact]
    public void CompilerBackendContract_RejectsDescriptorOnlyLane7AsProductionLowering()
    {
        InstructionSupportStatus status =
            InstructionSupportStatusCatalog.GetStatus("ACCEL_SUBMIT");
        Assert.Equal(IsaInstructionStatus.DescriptorOnly, status.Status);

        CompilerBackendLoweringDecision decision =
            CompilerBackendLoweringContract.EvaluateProductionL7Lowering(
                new CompilerBackendLoweringRequest
                {
                    Surface = CompilerBackendLoweringSurface.Lane7SystemDeviceCommand,
                    State = CompilerBackendCapabilityState.DescriptorOnly,
                    UsesDescriptorEvidenceOnly = true
                });

        Assert.False(decision.IsAllowed);
        Assert.NotEqual(CompilerBackendLoweringRequirement.None, decision.MissingRequirements);
    }
}

public sealed class AtomicOrderingInventoryTests
{
    [Fact]
    public void AtomicAcquireReleaseCarrierBits_AreNotProjectedIntoInstructionIrOrAtomicMicroOp()
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.LR_W,
            DataTypeEnum.INT32,
            reg1: 1,
            reg2: 2,
            reg3: 0);
        instruction.Acquire = true;
        instruction.Release = true;

        Assert.True(instruction.Acquire);
        Assert.True(instruction.Release);

        var decoder = new VliwDecoderV4();
        InstructionIR ir = decoder.Decode(in instruction, slotIndex: 4);

        Assert.Equal((ushort)InstructionsEnum.LR_W, ir.CanonicalOpcode.Value);
        Assert.Null(typeof(InstructionIR).GetProperty("Acquire"));
        Assert.Null(typeof(InstructionIR).GetProperty("Release"));
        Assert.Null(typeof(AtomicMicroOp).GetProperty("Acquire"));
        Assert.Null(typeof(AtomicMicroOp).GetProperty("Release"));
    }
}
