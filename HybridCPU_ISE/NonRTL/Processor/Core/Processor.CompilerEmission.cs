using HybridCPU_ISE.Arch;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        internal static void RecordCompilerInstruction(in VLIW_Instruction instruction)
        {
            Compiler.RecordInstruction(in instruction);
        }
    }
}
