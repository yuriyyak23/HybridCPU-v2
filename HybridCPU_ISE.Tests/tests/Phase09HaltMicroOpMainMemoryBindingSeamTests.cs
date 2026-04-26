using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09HaltMicroOpMainMemoryBindingSeamTests
{
    [Fact]
    public void HaltMicroOp_WhenGlobalMainMemoryIsReplacedAfterCoreConstruction_UsesBoundCoreMemoryLength()
    {
        const ulong seededBankSize = 0x1000UL;
        const ulong replacementBankSize = 0x10UL;
        const ulong startingPc = 0x80UL;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        var seededMemory = new Processor.MultiBankMemoryArea(4, seededBankSize);
        var replacementMemory = new Processor.MultiBankMemoryArea(4, replacementBankSize);

        try
        {
            Processor.MainMemory = seededMemory;

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(startingPc, activeVtId: 0);

            Processor.MainMemory = replacementMemory;

            var halt = new HaltMicroOp();

            Assert.True(halt.Execute(ref core));
            Assert.Equal((ulong)seededMemory.Length, core.ReadActiveLivePc());
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
        }
    }
}
