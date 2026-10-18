using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests;

internal static class ProjectedVectorDecoderContextBuilder
{
    internal static DecoderContext Create(
        in VLIW_Instruction instruction)
    {
        return new DecoderContext
        {
            OpCode = instruction.OpCode,
            Immediate = instruction.Immediate,
            HasImmediate = true,
            DataType = instruction.DataType,
            HasDataType = true,
            IndexedAddressing = instruction.Indexed,
            Is2DAddressing = instruction.Is2D,
            HasVectorAddressingContour = true,
            VectorPrimaryPointer = instruction.DestSrc1Pointer,
            VectorSecondaryPointer = instruction.Src2Pointer,
            VectorStreamLength = instruction.StreamLength,
            VectorStride = instruction.Stride,
            VectorRowStride = instruction.RowStride,
            TailAgnostic = instruction.TailAgnostic,
            MaskAgnostic = instruction.MaskAgnostic,
            HasVectorPayload = true,
            Reg1ID = instruction.Reg1ID,
            Reg2ID = instruction.Reg2ID,
            Reg3ID = instruction.Reg3ID,
            AuxData = instruction.Src2Pointer,
            PredicateMask = instruction.PredicateMask
        };
    }
}

