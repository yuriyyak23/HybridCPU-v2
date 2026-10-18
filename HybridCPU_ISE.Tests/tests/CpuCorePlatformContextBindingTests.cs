using System;
using Xunit;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests;

public sealed class CpuCorePlatformContextBindingTests
{
    [Fact]
    public void ExplicitPlatformContext_WhenGlobalMainMemoryChangesAfterConstruction_UsesBoundMemory()
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        ProcessorMode originalMode = Processor.CurrentProcessorMode;

        var boundMemory = new Processor.MainMemoryArea();
        boundMemory.SetLength(0x8000);

        var replacementGlobal = new Processor.MainMemoryArea();
        replacementGlobal.SetLength(0x40);

        CpuCorePlatformContext platformContext =
            CpuCorePlatformContext.CreateFixed(boundMemory, ProcessorMode.Emulation);

        try
        {
            Processor.MainMemory = replacementGlobal;
            Processor.CurrentProcessorMode = ProcessorMode.Compiler;

            var core = new Processor.CPU_Core(0, platformContext);

            Assert.Equal((ulong)0x8000, core.GetBoundMainMemoryLength());

            byte[] payload = BitConverter.GetBytes(0x1122_3344_5566_7788UL);
            core.WriteBoundMainMemoryExact(0x200, payload, "PhaseAuditCpuCorePlatformContextTests");

            byte[] committed = new byte[payload.Length];
            Assert.True(boundMemory.TryReadPhysicalRange(0x200, committed));
            Assert.Equal(0x1122_3344_5566_7788UL, BitConverter.ToUInt64(committed, 0));
            Assert.False(replacementGlobal.TryReadPhysicalRange(0x200, new byte[payload.Length]));
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.CurrentProcessorMode = originalMode;
        }
    }

}
