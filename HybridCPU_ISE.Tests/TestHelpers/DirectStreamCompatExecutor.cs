using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.TestHelpers;

internal static class DirectStreamCompatExecutor
{
    internal static void ExecuteDirectStreamCompat(
        this ref Processor.CPU_Core core,
        in VLIW_Instruction instruction,
        int ownerThreadId = -1)
    {
        core.TestExecuteDirectStreamCompat(in instruction, ownerThreadId);
    }
}
