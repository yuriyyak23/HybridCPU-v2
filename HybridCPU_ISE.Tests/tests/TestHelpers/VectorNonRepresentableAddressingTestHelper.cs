using HybridCPU_ISE.Arch;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.TestHelpers;

public enum VectorNonRepresentableFamily
{
    Binary,
    Unary,
    Reduction,
    Slide,
    Permutation
}

public static class VectorNonRepresentableAddressingTestHelper
{
    public static IEnumerable<object[]> RepresentativeContours()
    {
        yield return CreateCase(VectorNonRepresentableFamily.Binary, InstructionsEnum.VADD, is2D: false);
        yield return CreateCase(VectorNonRepresentableFamily.Binary, InstructionsEnum.VADD, is2D: true);
        yield return CreateCase(VectorNonRepresentableFamily.Unary, InstructionsEnum.VNOT, is2D: false);
        yield return CreateCase(VectorNonRepresentableFamily.Unary, InstructionsEnum.VNOT, is2D: true);
        yield return CreateCase(VectorNonRepresentableFamily.Reduction, InstructionsEnum.VREDSUM, is2D: false);
        yield return CreateCase(VectorNonRepresentableFamily.Reduction, InstructionsEnum.VREDSUM, is2D: true);
        yield return CreateCase(VectorNonRepresentableFamily.Slide, InstructionsEnum.VSLIDEUP, is2D: false);
        yield return CreateCase(VectorNonRepresentableFamily.Slide, InstructionsEnum.VSLIDEUP, is2D: true);
        yield return CreateCase(VectorNonRepresentableFamily.Permutation, InstructionsEnum.VPERMUTE, is2D: false);
        yield return CreateCase(VectorNonRepresentableFamily.Permutation, InstructionsEnum.VPERMUTE, is2D: true);
    }

    public static VLIW_Instruction[] CreateBundle(
        VLIW_Instruction slot0)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = slot0;
        return rawSlots;
    }

    public static DecoderContext CreateDecoderContext(
        in VLIW_Instruction instruction)
    {
        return ProjectedVectorDecoderContextBuilder.Create(in instruction);
    }

    public static VLIW_Instruction CreateInstruction(
        VectorNonRepresentableFamily family,
        InstructionsEnum opcode,
        bool is2D,
        byte virtualThreadId = 0)
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            PredicateMask = 0x0F,
            StreamLength = 4,
            Stride = 4,
            VirtualThreadId = virtualThreadId
        };

        switch (family)
        {
            case VectorNonRepresentableFamily.Binary:
                instruction.DataTypeValue = DataTypeEnum.INT32;
                instruction.DestSrc1Pointer = 0x220;
                instruction.Src2Pointer = 0x320;
                break;

            case VectorNonRepresentableFamily.Unary:
                instruction.DataTypeValue = DataTypeEnum.INT32;
                instruction.DestSrc1Pointer = 0x240;
                break;

            case VectorNonRepresentableFamily.Reduction:
                instruction.DataTypeValue = DataTypeEnum.UINT32;
                instruction.DestSrc1Pointer = 0x2C0;
                break;

            case VectorNonRepresentableFamily.Slide:
                instruction.DataTypeValue = DataTypeEnum.INT32;
                instruction.DestSrc1Pointer = 0x260;
                instruction.Immediate = 1;
                break;

            case VectorNonRepresentableFamily.Permutation:
                instruction.DataTypeValue = DataTypeEnum.UINT32;
                instruction.DestSrc1Pointer = 0x2A0;
                instruction.Src2Pointer = 0x3A0;
                break;
        }

        instruction.Indexed = !is2D;
        instruction.Is2D = is2D;
        if (is2D)
        {
            instruction.RowStride = 16;
            if (family != VectorNonRepresentableFamily.Slide)
            {
                instruction.Immediate = 2;
            }
        }

        return instruction;
    }

    public static VectorMicroOp CreateCarrier(
        VectorNonRepresentableFamily family,
        InstructionsEnum opcode,
        bool is2D,
        int ownerThreadId = 2)
    {
        VLIW_Instruction instruction =
            CreateInstruction(family, opcode, is2D, virtualThreadId: (byte)ownerThreadId);

        VectorMicroOp microOp = family switch
        {
            VectorNonRepresentableFamily.Binary => new VectorBinaryOpMicroOp(),
            VectorNonRepresentableFamily.Unary => new VectorUnaryOpMicroOp(),
            VectorNonRepresentableFamily.Reduction => new VectorReductionMicroOp(),
            VectorNonRepresentableFamily.Slide => new VectorSlideMicroOp(),
            VectorNonRepresentableFamily.Permutation => new VectorPermutationMicroOp(),
            _ => throw new System.ArgumentOutOfRangeException(nameof(family), family, null)
        };

        microOp.OpCode = (uint)opcode;
        microOp.OwnerThreadId = ownerThreadId;
        microOp.VirtualThreadId = ownerThreadId;
        microOp.OwnerContextId = ownerThreadId;
        microOp.Instruction = instruction;
        microOp.PredicateMask = instruction.PredicateMask;
        microOp.InitializeMetadata();
        return microOp;
    }

    public static string GetFactoryAddressingLabel(VectorNonRepresentableFamily family)
    {
        return family switch
        {
            VectorNonRepresentableFamily.Binary => "vector-binary addressing",
            VectorNonRepresentableFamily.Unary => "vector-unary addressing",
            VectorNonRepresentableFamily.Reduction => "vector-reduction addressing",
            VectorNonRepresentableFamily.Slide => "vector-slide addressing",
            VectorNonRepresentableFamily.Permutation => "vector-permutation addressing",
            _ => throw new System.ArgumentOutOfRangeException(nameof(family), family, null)
        };
    }

    public static string GetExecuteContourLabel(VectorNonRepresentableFamily family)
    {
        return family switch
        {
            VectorNonRepresentableFamily.Binary => "authoritative mainline binary contour",
            VectorNonRepresentableFamily.Unary => "authoritative mainline unary contour",
            VectorNonRepresentableFamily.Reduction => "authoritative mainline reduction contour",
            VectorNonRepresentableFamily.Slide => "authoritative mainline slide contour",
            VectorNonRepresentableFamily.Permutation => "authoritative mainline permutation contour",
            _ => throw new System.ArgumentOutOfRangeException(nameof(family), family, null)
        };
    }

    private static object[] CreateCase(
        VectorNonRepresentableFamily family,
        InstructionsEnum opcode,
        bool is2D)
    {
        return
        [
            family,
            opcode,
            is2D,
            is2D ? "2D" : "indexed"
        ];
    }
}

