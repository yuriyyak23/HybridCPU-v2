using HybridCPU_ISE.Arch;

using System;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using YAKSys_Hybrid_CPU.Execution;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            // REF-12 / EA-01: retained system helpers remain on the runtime surface for
            // compat callers, but compiler-mode container recording now routes through
            // CpuCoreSystemInstructionEmitter and Processor.RecordCompilerInstruction(...)
            // so helper expansion does not grow direct bridge append sites.
            public byte Nope()
            {
                if (IsEmulationExecutionMode())
                {
                    return (byte)InstructionsEnum.Nope;
                }
                if (IsCompilerExecutionMode())
                {
                    RecordCompatSystemInstruction(CpuCoreSystemInstructionEmitter.EncodeNope());
                }

                return (byte)InstructionsEnum.Nope;
            }


            private static void RecordCompatSystemInstruction(in VLIW_Instruction instruction)
            {
                Processor.RecordCompilerInstruction(in instruction);
            }
        }
    }
}

