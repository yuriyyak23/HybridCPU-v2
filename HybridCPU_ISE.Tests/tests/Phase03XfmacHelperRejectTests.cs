using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03XfmacHelperRejectTests
{
    private const string MessageFragment = "Optional scalar XFMAC contour is unsupported";
    private const string CarrierFragment = "scalar carrier/materializer follow-through";

    [Fact]
    public void XfmacHelper_CompilerModeRejectsBeforeInstructionPublication()
    {
        _ = new Processor(ProcessorMode.Compiler);
        Processor.Compiler.ResetInstructionBuffer();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Processor.CPU_Cores[0].FMAC(
                ArchRegId.Create(1),
                ArchRegId.Create(2),
                ArchRegId.Create(3)));

        Assert.Contains(MessageFragment, exception.Message);
        Assert.Contains(CarrierFragment, exception.Message);
        Assert.Equal(0, Processor.Compiler.InstructionCount);
        Assert.Equal(0, Processor.Compiler.GetRecordedInstructions().Length);
    }

    [Fact]
    public void XfmacHelper_EmulationModeRejectsBeforeArchitecturalMutation()
    {
        _ = new Processor(ProcessorMode.Emulation);

        const int vtId = 0;
        const int accumulatorRegister = 1;
        const ulong originalAccumulatorValue = 0x1234UL;

        Processor.CPU_Cores[0].WriteCommittedArch(vtId, accumulatorRegister, originalAccumulatorValue);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Processor.CPU_Cores[0].FMAC(
                ArchRegId.Create(accumulatorRegister),
                ArchRegId.Create(2),
                ArchRegId.Create(3)));

        Assert.Contains(MessageFragment, exception.Message);
        Assert.Contains(CarrierFragment, exception.Message);
        Assert.Equal(originalAccumulatorValue, Processor.CPU_Cores[0].ReadArch(vtId, accumulatorRegister));
    }
}
