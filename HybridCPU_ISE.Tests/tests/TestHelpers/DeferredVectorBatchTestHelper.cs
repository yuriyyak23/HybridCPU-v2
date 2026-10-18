using HybridCPU_ISE.Arch;
using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.TestHelpers;

public enum DeferredVectorPublicationFamily
{
    Binary,
    Unary,
    Reduction,
    Comparison,
    Permutation,
    Slide,
    PredicativeMovement,
    DotProduct,
    Transfer,
    Fma
}

public enum DeferredVectorAddressingFamily
{
    Transfer,
    Comparison,
    Mask,
    MaskPopCount,
    DotProduct,
    PredicativeMovement,
    Fma
}

public static class DeferredVectorBatchTestHelper
{
    public static IEnumerable<object[]> InvalidDataTypePublicationCases()
    {
        yield return CreateInvalidDataTypeCase(
            DeferredVectorPublicationFamily.Binary,
            InstructionsEnum.VADD,
            DataTypeEnum.INT32,
            "VectorBinaryOpMicroOp.InitializeMetadata()");
        yield return CreateInvalidDataTypeCase(
            DeferredVectorPublicationFamily.Unary,
            InstructionsEnum.VNOT,
            DataTypeEnum.INT32,
            "VectorUnaryOpMicroOp.InitializeMetadata()");
        yield return CreateInvalidDataTypeCase(
            DeferredVectorPublicationFamily.Reduction,
            InstructionsEnum.VREDSUM,
            DataTypeEnum.UINT32,
            "VectorReductionMicroOp.InitializeMetadata()");
        yield return CreateInvalidDataTypeCase(
            DeferredVectorPublicationFamily.Comparison,
            InstructionsEnum.VCMPEQ,
            DataTypeEnum.INT32,
            "VectorComparisonMicroOp.InitializeMetadata()");
        yield return CreateInvalidDataTypeCase(
            DeferredVectorPublicationFamily.Permutation,
            InstructionsEnum.VPERMUTE,
            DataTypeEnum.UINT32,
            "VectorPermutationMicroOp.InitializeMetadata()");
        yield return CreateInvalidDataTypeCase(
            DeferredVectorPublicationFamily.Slide,
            InstructionsEnum.VSLIDEUP,
            DataTypeEnum.UINT32,
            "VectorSlideMicroOp.InitializeMetadata()");
        yield return CreateInvalidDataTypeCase(
            DeferredVectorPublicationFamily.PredicativeMovement,
            InstructionsEnum.VCOMPRESS,
            DataTypeEnum.INT32,
            "VectorPredicativeMovementMicroOp.InitializeMetadata()");
        yield return CreateInvalidDataTypeCase(
            DeferredVectorPublicationFamily.DotProduct,
            InstructionsEnum.VDOT,
            DataTypeEnum.INT32,
            "VectorDotProductMicroOp.InitializeMetadata()");
        yield return CreateInvalidDataTypeCase(
            DeferredVectorPublicationFamily.DotProduct,
            InstructionsEnum.VDOT_FP8,
            DataTypeEnum.FLOAT8_E4M3,
            "VectorDotProductMicroOp.InitializeMetadata()");
        yield return CreateInvalidDataTypeCase(
            DeferredVectorPublicationFamily.Transfer,
            InstructionsEnum.VLOAD,
            DataTypeEnum.INT32,
            "VectorTransferMicroOp.InitializeMetadata()");
        yield return CreateInvalidDataTypeCase(
            DeferredVectorPublicationFamily.Transfer,
            InstructionsEnum.VSTORE,
            DataTypeEnum.INT32,
            "VectorTransferMicroOp.InitializeMetadata()");
        yield return CreateInvalidDataTypeCase(
            DeferredVectorPublicationFamily.Fma,
            InstructionsEnum.VFMADD,
            DataTypeEnum.FLOAT32,
            "VectorFmaMicroOp.InitializeMetadata()");
    }

    public static IEnumerable<object[]> ExtendedNonRepresentableContours()
    {
        foreach (InstructionsEnum opcode in new[] { InstructionsEnum.VLOAD, InstructionsEnum.VSTORE })
        {
            yield return CreateAddressingCase(DeferredVectorAddressingFamily.Transfer, opcode, is2D: false);
            yield return CreateAddressingCase(DeferredVectorAddressingFamily.Transfer, opcode, is2D: true);
        }

        foreach (InstructionsEnum opcode in new[]
                 {
                     InstructionsEnum.VCMPEQ,
                     InstructionsEnum.VCMPNE,
                     InstructionsEnum.VCMPLT,
                     InstructionsEnum.VCMPLE,
                     InstructionsEnum.VCMPGT,
                     InstructionsEnum.VCMPGE
                 })
        {
            yield return CreateAddressingCase(DeferredVectorAddressingFamily.Comparison, opcode, is2D: false);
            yield return CreateAddressingCase(DeferredVectorAddressingFamily.Comparison, opcode, is2D: true);
        }

        foreach (InstructionsEnum opcode in new[]
                 {
                     InstructionsEnum.VMAND,
                     InstructionsEnum.VMOR,
                     InstructionsEnum.VMXOR,
                     InstructionsEnum.VMNOT
                 })
        {
            yield return CreateAddressingCase(DeferredVectorAddressingFamily.Mask, opcode, is2D: false);
            yield return CreateAddressingCase(DeferredVectorAddressingFamily.Mask, opcode, is2D: true);
        }

        yield return CreateAddressingCase(DeferredVectorAddressingFamily.MaskPopCount, InstructionsEnum.VPOPC, is2D: false);
        yield return CreateAddressingCase(DeferredVectorAddressingFamily.MaskPopCount, InstructionsEnum.VPOPC, is2D: true);

        foreach (InstructionsEnum opcode in new[]
                 {
                     InstructionsEnum.VDOT,
                     InstructionsEnum.VDOTU,
                     InstructionsEnum.VDOTF,
                     InstructionsEnum.VDOT_FP8
                 })
        {
            yield return CreateAddressingCase(DeferredVectorAddressingFamily.DotProduct, opcode, is2D: false);
            yield return CreateAddressingCase(DeferredVectorAddressingFamily.DotProduct, opcode, is2D: true);
        }

        foreach (InstructionsEnum opcode in new[] { InstructionsEnum.VCOMPRESS, InstructionsEnum.VEXPAND })
        {
            yield return CreateAddressingCase(DeferredVectorAddressingFamily.PredicativeMovement, opcode, is2D: false);
            yield return CreateAddressingCase(DeferredVectorAddressingFamily.PredicativeMovement, opcode, is2D: true);
        }

        foreach (InstructionsEnum opcode in new[]
                 {
                     InstructionsEnum.VFMADD,
                     InstructionsEnum.VFMSUB,
                     InstructionsEnum.VFNMADD,
                     InstructionsEnum.VFNMSUB
                 })
        {
            yield return CreateAddressingCase(DeferredVectorAddressingFamily.Fma, opcode, is2D: false);
            yield return CreateAddressingCase(DeferredVectorAddressingFamily.Fma, opcode, is2D: true);
        }
    }

    public static VLIW_Instruction CreateInvalidDataTypeInstruction(
        DeferredVectorPublicationFamily family,
        InstructionsEnum opcode,
        DataTypeEnum nominalDataType,
        byte virtualThreadId = 0)
    {
        VLIW_Instruction instruction =
            CreatePublicationInstruction(family, opcode, nominalDataType, virtualThreadId);
        instruction.DataType = 0xFE;
        return instruction;
    }

    public static VLIW_Instruction CreateAddressingInstruction(
        DeferredVectorAddressingFamily family,
        InstructionsEnum opcode,
        bool is2D,
        byte virtualThreadId = 0)
    {
        VLIW_Instruction instruction = family switch
        {
            DeferredVectorAddressingFamily.Transfer => CreatePublicationInstruction(
                DeferredVectorPublicationFamily.Transfer,
                opcode,
                DataTypeEnum.INT32,
                virtualThreadId),
            DeferredVectorAddressingFamily.Comparison => CreatePublicationInstruction(
                DeferredVectorPublicationFamily.Comparison,
                opcode,
                DataTypeEnum.INT32,
                virtualThreadId),
            DeferredVectorAddressingFamily.Mask => CreateMaskInstruction(opcode, virtualThreadId),
            DeferredVectorAddressingFamily.MaskPopCount => CreateMaskPopCountInstruction(virtualThreadId),
            DeferredVectorAddressingFamily.DotProduct => CreateDotProductInstruction(
                opcode,
                opcode switch
                {
                    InstructionsEnum.VDOTU => DataTypeEnum.UINT32,
                    InstructionsEnum.VDOTF => DataTypeEnum.FLOAT32,
                    InstructionsEnum.VDOT_FP8 => DataTypeEnum.FLOAT8_E4M3,
                    _ => DataTypeEnum.INT32
                },
                virtualThreadId),
            DeferredVectorAddressingFamily.PredicativeMovement => CreatePublicationInstruction(
                DeferredVectorPublicationFamily.PredicativeMovement,
                opcode,
                DataTypeEnum.INT32,
                virtualThreadId),
            DeferredVectorAddressingFamily.Fma => CreatePublicationInstruction(
                DeferredVectorPublicationFamily.Fma,
                opcode,
                DataTypeEnum.FLOAT32,
                virtualThreadId),
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, null)
        };

        instruction.Indexed = !is2D;
        instruction.Is2D = is2D;
        if (is2D)
        {
            instruction.RowStride = 16;
        }

        return instruction;
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

    public static VectorMicroOp CreateAddressingCarrier(
        DeferredVectorAddressingFamily family,
        InstructionsEnum opcode,
        bool is2D,
        int ownerThreadId = 2)
    {
        VLIW_Instruction instruction =
            CreateAddressingInstruction(family, opcode, is2D, virtualThreadId: (byte)ownerThreadId);

        VectorMicroOp microOp = family switch
        {
            DeferredVectorAddressingFamily.Transfer => new VectorTransferMicroOp(),
            DeferredVectorAddressingFamily.Comparison => new VectorComparisonMicroOp(),
            DeferredVectorAddressingFamily.Mask => new VectorMaskOpMicroOp(),
            DeferredVectorAddressingFamily.MaskPopCount => new VectorMaskPopCountMicroOp(),
            DeferredVectorAddressingFamily.DotProduct => new VectorDotProductMicroOp(),
            DeferredVectorAddressingFamily.PredicativeMovement => new VectorPredicativeMovementMicroOp(),
            DeferredVectorAddressingFamily.Fma => new VectorFmaMicroOp(),
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, null)
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

    public static bool ExpectsRegisterWrite(DeferredVectorAddressingFamily family)
        => family == DeferredVectorAddressingFamily.MaskPopCount;

    public static int[] GetExpectedWriteRegisters(DeferredVectorAddressingFamily family)
        => family == DeferredVectorAddressingFamily.MaskPopCount ? new[] { 6 } : Array.Empty<int>();

    public static string GetFactoryAddressingLabel(DeferredVectorAddressingFamily family)
    {
        return family switch
        {
            DeferredVectorAddressingFamily.Transfer => "vector-transfer addressing",
            DeferredVectorAddressingFamily.Comparison => "vector-comparison addressing",
            DeferredVectorAddressingFamily.Mask => "vector-mask addressing",
            DeferredVectorAddressingFamily.MaskPopCount => "vector-mask-popcount addressing",
            DeferredVectorAddressingFamily.DotProduct => "vector-dot-product addressing",
            DeferredVectorAddressingFamily.PredicativeMovement => "vector-predicative-movement addressing",
            DeferredVectorAddressingFamily.Fma => "vector-FMA addressing",
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, null)
        };
    }

    public static string GetRawAddressingLabel(DeferredVectorAddressingFamily family)
    {
        return family switch
        {
            DeferredVectorAddressingFamily.Transfer => "legacy raw VLOAD/VSTORE contour",
            DeferredVectorAddressingFamily.Comparison => "vector-comparison addressing",
            DeferredVectorAddressingFamily.Mask => "predicate-mask addressing",
            DeferredVectorAddressingFamily.MaskPopCount => "predicate-mask addressing",
            DeferredVectorAddressingFamily.DotProduct => "vector-dot-product addressing",
            DeferredVectorAddressingFamily.PredicativeMovement => "vector-predicative-movement addressing",
            DeferredVectorAddressingFamily.Fma => "vector-FMA addressing",
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, null)
        };
    }

    public static string GetExecuteContourLabel(DeferredVectorAddressingFamily family)
    {
        return family switch
        {
            DeferredVectorAddressingFamily.Transfer => "authoritative mainline VLOAD/VSTORE carrier contour",
            DeferredVectorAddressingFamily.Comparison => "authoritative mainline predicate-publication contour",
            DeferredVectorAddressingFamily.Mask => "authoritative mainline predicate-publication contour",
            DeferredVectorAddressingFamily.MaskPopCount => "authoritative mainline scalar-result contour",
            DeferredVectorAddressingFamily.DotProduct => "authoritative mainline dot-product contour",
            DeferredVectorAddressingFamily.PredicativeMovement => "authoritative mainline predicative-movement contour",
            DeferredVectorAddressingFamily.Fma => "authoritative mainline tri-operand FMA contour",
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, null)
        };
    }

    private static object[] CreateInvalidDataTypeCase(
        DeferredVectorPublicationFamily family,
        InstructionsEnum opcode,
        DataTypeEnum nominalDataType,
        string metadataSurface)
    {
        return
        [
            family,
            opcode,
            nominalDataType,
            metadataSurface
        ];
    }

    private static object[] CreateAddressingCase(
        DeferredVectorAddressingFamily family,
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

    private static VLIW_Instruction CreatePublicationInstruction(
        DeferredVectorPublicationFamily family,
        InstructionsEnum opcode,
        DataTypeEnum dataType,
        byte virtualThreadId)
    {
        return family switch
        {
            DeferredVectorPublicationFamily.Binary => new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = dataType,
                PredicateMask = 0xFF,
                DestSrc1Pointer = 0x220,
                Src2Pointer = 0x320,
                StreamLength = 4,
                Stride = 4,
                VirtualThreadId = virtualThreadId
            },
            DeferredVectorPublicationFamily.Unary => new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = dataType,
                PredicateMask = 0xFF,
                DestSrc1Pointer = 0x240,
                StreamLength = 4,
                Stride = 4,
                VirtualThreadId = virtualThreadId
            },
            DeferredVectorPublicationFamily.Reduction => new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = dataType,
                PredicateMask = 0xFF,
                DestSrc1Pointer = 0x2C0,
                StreamLength = 4,
                Stride = 4,
                VirtualThreadId = virtualThreadId
            },
            DeferredVectorPublicationFamily.Comparison => new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = dataType,
                PredicateMask = 0xFF,
                DestSrc1Pointer = 0x200,
                Src2Pointer = 0x300,
                Immediate = 5,
                StreamLength = 2,
                Stride = 4,
                VirtualThreadId = virtualThreadId
            },
            DeferredVectorPublicationFamily.Permutation => new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = dataType,
                PredicateMask = 0xFF,
                DestSrc1Pointer = 0x2A0,
                Src2Pointer = 0x3A0,
                StreamLength = 4,
                Stride = 4,
                VirtualThreadId = virtualThreadId
            },
            DeferredVectorPublicationFamily.Slide => new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = dataType,
                PredicateMask = 0xFF,
                DestSrc1Pointer = 0x2C0,
                Immediate = 1,
                StreamLength = 4,
                Stride = 4,
                VirtualThreadId = virtualThreadId
            },
            DeferredVectorPublicationFamily.PredicativeMovement => new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = dataType,
                PredicateMask = 0x0F,
                DestSrc1Pointer = 0x280,
                Src2Pointer = 0x380,
                StreamLength = 4,
                Stride = 4,
                VirtualThreadId = virtualThreadId
            },
            DeferredVectorPublicationFamily.DotProduct => CreateDotProductInstruction(opcode, dataType, virtualThreadId),
            DeferredVectorPublicationFamily.Transfer => new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = dataType,
                PredicateMask = 0xFF,
                DestSrc1Pointer = 0x280,
                Src2Pointer = 0x380,
                StreamLength = 4,
                Stride = 4,
                VirtualThreadId = virtualThreadId
            },
            DeferredVectorPublicationFamily.Fma => new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = dataType,
                PredicateMask = 0x07,
                DestSrc1Pointer = 0x240,
                Src2Pointer = 0x340,
                StreamLength = 4,
                Stride = 4,
                VirtualThreadId = virtualThreadId
            },
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, null)
        };
    }

    private static VLIW_Instruction CreateMaskInstruction(
        InstructionsEnum opcode,
        byte virtualThreadId)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            Immediate = (ushort)(1 | (2 << 4) | (3 << 8)),
            StreamLength = 8,
            VirtualThreadId = virtualThreadId
        };
    }

    private static VLIW_Instruction CreateMaskPopCountInstruction(
        byte virtualThreadId)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.VPOPC,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            Immediate = (ushort)(1 | (6 << 8)),
            StreamLength = 8,
            VirtualThreadId = virtualThreadId
        };
    }

    private static VLIW_Instruction CreateDotProductInstruction(
        InstructionsEnum opcode,
        DataTypeEnum dataType,
        byte virtualThreadId)
    {
        if (opcode == InstructionsEnum.VDOT_FP8)
        {
            return new VLIW_Instruction
            {
                OpCode = (uint)opcode,
                DataTypeValue = dataType,
                PredicateMask = 0x0F,
                DestSrc1Pointer = 0x320,
                Src2Pointer = 0x3A0,
                StreamLength = 4,
                Stride = 1,
                VirtualThreadId = virtualThreadId
            };
        }

        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = dataType,
            PredicateMask = 0x0F,
            DestSrc1Pointer = 0x2E0,
            Src2Pointer = 0x3E0,
            StreamLength = 2,
            Stride = 4,
            VirtualThreadId = virtualThreadId
        };
    }
}

