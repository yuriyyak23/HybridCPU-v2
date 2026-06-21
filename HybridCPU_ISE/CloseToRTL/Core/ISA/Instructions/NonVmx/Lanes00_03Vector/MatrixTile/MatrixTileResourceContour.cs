using System;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

public enum MatrixTileRuntimeResourceClass : byte
{
    None = 0,
    MatrixTileMemory = 1,
    MatrixTileCompute = 2,
}

public static class MatrixTileResourceContour
{
    public const byte TileStreamLaneMask = 0b_0100_0000;
    public const byte TileStreamLaneId = 6;
    public const int StreamEngineChannel = 0;

    public static bool IsMatrixTileMemoryOpcode(InstructionsEnum opcode) =>
        opcode is InstructionsEnum.MTILE_LOAD or InstructionsEnum.MTILE_STORE;

    public static bool IsMatrixTileMemoryOpcode(uint opcode) =>
        IsMatrixTileMemoryOpcode((InstructionsEnum)opcode);

    public static bool IsMatrixTileComputeOpcode(InstructionsEnum opcode) =>
        opcode is InstructionsEnum.MTILE_MACC or InstructionsEnum.MTRANSPOSE;

    public static bool IsMatrixTileComputeOpcode(uint opcode) =>
        IsMatrixTileComputeOpcode((InstructionsEnum)opcode);

    public static bool IsMatrixTileOpcode(InstructionsEnum opcode) =>
        IsMatrixTileMemoryOpcode(opcode) || IsMatrixTileComputeOpcode(opcode);

    public static bool IsMatrixTileOpcode(uint opcode) =>
        IsMatrixTileOpcode((InstructionsEnum)opcode);

    public static MatrixTileRuntimeResourceClass Classify(InstructionsEnum opcode)
    {
        if (IsMatrixTileMemoryOpcode(opcode))
        {
            return MatrixTileRuntimeResourceClass.MatrixTileMemory;
        }

        if (IsMatrixTileComputeOpcode(opcode))
        {
            return MatrixTileRuntimeResourceClass.MatrixTileCompute;
        }

        return MatrixTileRuntimeResourceClass.None;
    }

    public static MatrixTileRuntimeResourceClass Classify(uint opcode) =>
        Classify((InstructionsEnum)opcode);

    public static SlotClass ResolveSlotClass(MatrixTileRuntimeResourceClass resourceClass) =>
        resourceClass switch
        {
            MatrixTileRuntimeResourceClass.MatrixTileMemory => SlotClass.MatrixTileStreamClass,
            MatrixTileRuntimeResourceClass.MatrixTileCompute => SlotClass.AluClass,
            _ => throw new ArgumentOutOfRangeException(
                nameof(resourceClass),
                resourceClass,
                "A MatrixTile runtime resource class is required.")
        };
}
