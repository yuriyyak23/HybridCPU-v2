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
    public void RuntimeVectorLegalityMatrix_CurrentIndexedAnd2DAddressingContoursRemainNonExecutable(
        InstructionsEnum opcode)
    {
        Assert.NotEqual(
            VectorContourLegalityStatus.Executable,
            VectorLegalityMatrix.GetAddressingStatus(opcode, indexed: true, is2D: false));
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
    public void GatherScatterPolicy_RemainsDescriptorOnlyAndCarrierlessInRuntimeMatrix(
        InstructionsEnum opcode)
    {
        VectorLegalityMatrixRow row = VectorLegalityMatrix.GetRow(opcode);
        InstructionSupportStatus supportStatus =
            InstructionSupportStatusCatalog.GetStatus(opcode.ToString());

        Assert.Equal("VectorIndexedMemory", row.FamilyName);
        Assert.Equal(VectorContourLegalityStatus.DescriptorOnly, row.OneDimensional);
        Assert.Equal(VectorContourLegalityStatus.DescriptorOnly, row.IndexedAddressing);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.TwoDimensionalAddressing);
        Assert.Equal(VectorContourLegalityStatus.DescriptorOnly, row.DescriptorBacked);
        Assert.False(VectorLegalityMatrix.AllowsAddressingExecution(opcode, indexed: true, is2D: false));

        Assert.Equal(IsaInstructionStatus.DescriptorOnly, supportStatus.Status);
        Assert.False(supportStatus.IsExecutableClaim);
        Assert.NotNull(OpcodeRegistry.GetInfo((uint)opcode));
        Assert.NotNull(InstructionRegistry.GetDescriptor((uint)opcode));
        Assert.False(InstructionRegistry.IsRegistered((uint)opcode));

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
            VectorStreamLength = 4,
            VectorStride = 4,
            PredicateMask = 0xFF
        };

        System.InvalidOperationException exception = Assert.Throws<System.InvalidOperationException>(
            () => InstructionRegistry.CreateMicroOp((uint)opcode, context));
        Assert.Contains("Unsupported instruction opcode", exception.Message, System.StringComparison.Ordinal);
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
