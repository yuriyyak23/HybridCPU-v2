using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace YAKSys_Hybrid_CPU
{
    internal static class CpuCoreSystemInstructionEmitter
    {
        internal static VLIW_Instruction EncodeNope()
        {
            return new VLIW_Instruction
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Nope
            };
        }

        internal static VLIW_Instruction EncodeMoveImmediate(ArchRegId destinationRegisterId, ulong value)
        {
            return CreateMoveInstruction(
                dataType: 1,
                destinationRegisterId.Value,
                VLIW_Instruction.NoArchReg,
                value);
        }

        internal static VLIW_Instruction EncodeRegisterMove(ArchRegId sourceRegisterId, ArchRegId destinationRegisterId)
        {
            return CreateMoveInstruction(
                dataType: 0,
                sourceRegisterId.Value,
                destinationRegisterId.Value,
                0);
        }

        internal static VLIW_Instruction EncodeRegisterToMemoryMove(ArchRegId sourceRegisterId, ulong memoryAddressDestination)
        {
            return CreateMoveInstruction(
                dataType: 2,
                sourceRegisterId.Value,
                VLIW_Instruction.NoArchReg,
                memoryAddressDestination);
        }

        internal static VLIW_Instruction EncodeMemoryToRegisterMove(ulong memoryAddressSource, ArchRegId destinationRegisterId)
        {
            return CreateMoveInstruction(
                dataType: 3,
                destinationRegisterId.Value,
                VLIW_Instruction.NoArchReg,
                memoryAddressSource);
        }

        private static VLIW_Instruction CreateMoveInstruction(
            byte dataType,
            byte word1Reg0,
            byte word1Reg1,
            ulong src2)
        {
            return new VLIW_Instruction
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Move,
                DataType = dataType,
                PredicateMask = 0,
                Immediate = 0,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                    word1Reg0,
                    word1Reg1,
                    VLIW_Instruction.NoArchReg),
                Src2Pointer = src2,
                StreamLength = 0,
                Stride = 0
            };
        }
    }
}
