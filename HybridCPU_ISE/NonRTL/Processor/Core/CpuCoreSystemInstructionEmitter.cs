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


    }
}
