using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests;

public sealed class PhaseAuditCpuCorePlatformContextTests
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

    [Fact]
    public void ExplicitPlatformContext_WhenPrepareExecutionStartSynchronizesMode_IgnoresGlobalModeFlip()
    {
        ProcessorMode originalMode = Processor.CurrentProcessorMode;
        _ = new Processor(ProcessorMode.Compiler);
        Processor.Compiler.ResetInstructionBuffer();

        var boundMemory = new Processor.MainMemoryArea();
        boundMemory.SetLength(0x2000);

        CpuCorePlatformContext platformContext =
            CpuCorePlatformContext.CreateFixed(boundMemory, ProcessorMode.Compiler);

        try
        {
            Processor.CurrentProcessorMode = ProcessorMode.Emulation;

            var core = new Processor.CPU_Core(0, platformContext);
            core.PrepareExecutionStart(0x1000, activeVtId: 0);
            core.Move_Num(ArchRegId.Create(6), 0x1234UL);

            Assert.Equal(1, Processor.Compiler.InstructionCount);
            Assert.Equal(0UL, core.ReadArch(0, 6));
        }
        finally
        {
            Processor.CurrentProcessorMode = originalMode;
        }
    }

    [Fact]
    public void LegacyConstructor_WhenPrepareExecutionStartSynchronizesMode_StillTracksGlobalMode()
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        ProcessorMode originalMode = Processor.CurrentProcessorMode;
        _ = new Processor(ProcessorMode.Compiler);
        Processor.Compiler.ResetInstructionBuffer();

        var seededMemory = new Processor.MainMemoryArea();
        seededMemory.SetLength(0x2000);

        try
        {
            Processor.MainMemory = seededMemory;
            Processor.CurrentProcessorMode = ProcessorMode.Compiler;

#pragma warning disable CS0618
            var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618

            Processor.CurrentProcessorMode = ProcessorMode.Emulation;
            core.PrepareExecutionStart(0x1000, activeVtId: 0);
            core.Move_Num(ArchRegId.Create(6), 0x1234UL);

            Assert.Equal(0, Processor.Compiler.InstructionCount);
            Assert.Equal(0x1234UL, core.ReadArch(0, 6));
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.CurrentProcessorMode = originalMode;
        }
    }
}
