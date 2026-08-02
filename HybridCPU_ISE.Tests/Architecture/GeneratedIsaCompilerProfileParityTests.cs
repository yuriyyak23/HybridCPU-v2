using HybridCPU.Compiler.Core.IR;
using YAKSys_Hybrid_CPU.Arch.Generated;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class GeneratedIsaCompilerProfileParityTests
{
    [Fact]
    public void CompilerProfiles_UseGeneratedDeclaredLatencyForEveryDeclaredOpcode()
    {
        foreach (GeneratedIsaDescriptor descriptor in GeneratedIsaCatalog.Descriptors)
        {
            var profile = HybridCpuHazardModel.GetExecutionProfile((InstructionsEnum)descriptor.Opcode);
            Assert.Equal(descriptor.ExecutionLatency, profile.MinimumLatencyCycles);
        }
    }
}
