using Xunit;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests;

public sealed class CoreExecutionModeEntryPointTests
{
    [Fact]
    public void EntryPoint_WhenCoreWasSeededInCompilerMode_RemainsCompilerAfterGlobalModeFlip()
    {
        ProcessorMode originalMode = Processor.CurrentProcessorMode;
        _ = new Processor(ProcessorMode.Compiler);
        long originalPosition = Processor.MainMemory.Position;

        try
        {
            Processor.CurrentProcessorMode = ProcessorMode.Emulation;
            Processor.MainMemory.Position = 0x180;
            Processor.EntryPoint entryPoint = default;

            Processor.CPU_Cores[0].ENTRY_POINT(ref entryPoint);

            Assert.True(entryPoint.EntryPointAddressAlreadyDefined);
            Assert.Equal(0x180UL, entryPoint.EntryPoint_Address);
            Assert.Equal(
                Processor.EntryPoint.EntryPointType.EntryPoint,
                entryPoint.Type);
        }
        finally
        {
            Processor.MainMemory.Position = originalPosition;
            Processor.CurrentProcessorMode = originalMode;
        }
    }
}
