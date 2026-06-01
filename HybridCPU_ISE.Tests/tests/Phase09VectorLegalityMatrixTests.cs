using System.Collections.Generic;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09VectorLegalityMatrixTests
{
    [Fact]
    public void RuntimeVectorLegalityMatrix_CoversEveryPublishedVectorOpcodeAndStatusFamily()
    {
        foreach (OpcodeInfo info in OpcodeRegistry.Opcodes)
        {
            if (!info.IsVector)
            {
                continue;
            }

            var opcode = (InstructionsEnum)info.OpCode;
            VectorLegalityMatrixRow row = VectorLegalityMatrix.GetRow(opcode);
            InstructionSupportStatus supportStatus =
                InstructionSupportStatusCatalog.GetStatus(info.Mnemonic);

            Assert.Contains(opcode, row.Opcodes);
            Assert.Equal(supportStatus.ExtensionName, row.FamilyName);
        }
    }

    [Theory]
    [MemberData(nameof(CurrentVectorMatrixOpcodeRows))]
    public void RuntimeVectorLegalityMatrix_CurrentIndexedAnd2DAddressingContoursMatchPublishedExecutionBoundary(
        InstructionsEnum opcode)
    {
        if (opcode is InstructionsEnum.VGATHER or InstructionsEnum.VSCATTER)
        {
            Assert.Equal(
                VectorContourLegalityStatus.Executable,
                VectorLegalityMatrix.GetAddressingStatus(opcode, indexed: true, is2D: false));
        }
        else
        {
            Assert.NotEqual(
                VectorContourLegalityStatus.Executable,
                VectorLegalityMatrix.GetAddressingStatus(opcode, indexed: true, is2D: false));
        }

        Assert.NotEqual(
            VectorContourLegalityStatus.Executable,
            VectorLegalityMatrix.GetAddressingStatus(opcode, indexed: false, is2D: true));
        Assert.NotEqual(
            VectorContourLegalityStatus.Executable,
            VectorLegalityMatrix.GetAddressingStatus(opcode, indexed: true, is2D: true));
    }

    [Theory]
    [MemberData(nameof(MatrixBackedFactoryRejectCases))]
    public void RegistryFactory_WhenMatrixMarksAddressingNonExecutable_RejectsBeforeMaterialization(
        InstructionsEnum opcode,
        bool is2D,
        DecoderContext context,
        string familyAddressingLabel,
        string addressingContour)
    {
        VectorContourLegalityStatus status = VectorLegalityMatrix.GetAddressingStatus(
            opcode,
            indexed: !is2D,
            is2D: is2D);

        Assert.NotEqual(VectorContourLegalityStatus.Executable, status);

        DecodeProjectionFaultException exception = Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)opcode, context));

        Assert.Contains($"unsupported {addressingContour}", exception.Message, System.StringComparison.Ordinal);
        Assert.Contains(familyAddressingLabel, exception.Message, System.StringComparison.Ordinal);
        Assert.Contains("fail closed", exception.Message, System.StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(InstructionsEnum.VGATHER)]
    [InlineData(InstructionsEnum.VSCATTER)]
    public void GatherScatterPolicy_TracksOpenedGatherAndClosedScatterInRuntimeMatrix(
        InstructionsEnum opcode)
    {
        VectorLegalityMatrixRow row = VectorLegalityMatrix.GetRow(opcode);
        InstructionSupportStatus supportStatus =
            InstructionSupportStatusCatalog.GetStatus(opcode.ToString());

        if (opcode == InstructionsEnum.VGATHER)
        {
            Assert.Equal("VectorIndexedGatherMemory", row.FamilyName);
            Assert.Equal(VectorContourLegalityStatus.FailClosed, row.OneDimensional);
            Assert.Equal(VectorContourLegalityStatus.Executable, row.IndexedAddressing);
            Assert.Equal(VectorContourLegalityStatus.FailClosed, row.TwoDimensionalAddressing);
            Assert.Equal(VectorContourLegalityStatus.Executable, row.DescriptorBacked);
            Assert.True(VectorLegalityMatrix.AllowsAddressingExecution(opcode, indexed: true, is2D: false));

            Assert.Equal(IsaInstructionStatus.OptionalEnabled, supportStatus.Status);
            Assert.True(supportStatus.IsExecutableClaim);
            Assert.True(InstructionRegistry.IsRegistered((uint)opcode));
            return;
        }

        Assert.Equal("VectorIndexedScatterMemory", row.FamilyName);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.OneDimensional);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.IndexedAddressing);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.TwoDimensionalAddressing);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.DescriptorBacked);
        Assert.True(VectorLegalityMatrix.AllowsAddressingExecution(opcode, indexed: true, is2D: false));

        Assert.Equal(IsaInstructionStatus.OptionalEnabled, supportStatus.Status);
        Assert.True(supportStatus.IsExecutableClaim);
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)opcode));
        Assert.NotNull(InstructionRegistry.GetDescriptor((uint)opcode));
        Assert.True(InstructionRegistry.IsRegistered((uint)opcode));
    }

    public static IEnumerable<object[]> CurrentVectorMatrixOpcodeRows()
    {
        foreach (VectorLegalityMatrixRow row in VectorLegalityMatrix.Rows)
        {
            foreach (InstructionsEnum opcode in row.Opcodes)
            {
                yield return new object[] { opcode };
            }
        }
    }

    public static IEnumerable<object[]> MatrixBackedFactoryRejectCases()
    {
        foreach (object[] rawCase in VectorNonRepresentableAddressingTestHelper.RepresentativeContours())
        {
            var family = (VectorNonRepresentableFamily)rawCase[0];
            var opcode = (InstructionsEnum)rawCase[1];
            bool is2D = (bool)rawCase[2];
            string addressingContour = (string)rawCase[3];

            VLIW_Instruction instruction =
                VectorNonRepresentableAddressingTestHelper.CreateInstruction(family, opcode, is2D);
            yield return new object[]
            {
                opcode,
                is2D,
                ProjectedVectorDecoderContextBuilder.Create(in instruction),
                VectorNonRepresentableAddressingTestHelper.GetFactoryAddressingLabel(family),
                addressingContour
            };
        }

        foreach (object[] rawCase in DeferredVectorBatchTestHelper.ExtendedNonRepresentableContours())
        {
            var family = (DeferredVectorAddressingFamily)rawCase[0];
            var opcode = (InstructionsEnum)rawCase[1];
            bool is2D = (bool)rawCase[2];
            string addressingContour = (string)rawCase[3];

            VLIW_Instruction instruction =
                DeferredVectorBatchTestHelper.CreateAddressingInstruction(family, opcode, is2D);
            yield return new object[]
            {
                opcode,
                is2D,
                ProjectedVectorDecoderContextBuilder.Create(in instruction),
                DeferredVectorBatchTestHelper.GetFactoryAddressingLabel(family),
                addressingContour
            };
        }
    }
}
