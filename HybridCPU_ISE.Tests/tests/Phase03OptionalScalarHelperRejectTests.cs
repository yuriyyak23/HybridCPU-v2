using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03OptionalScalarHelperRejectTests
{
    private const string XsqrtMessageFragment = "Optional scalar XSQRT contour is unsupported";
    private const string CarrierFragment = "scalar carrier/materializer follow-through";

    [Fact]
    public void XsqrtHelper_CompilerModeRejectsBeforeInstructionPublication()
    {
        _ = new Processor(ProcessorMode.Compiler);
        Processor.Compiler.ResetInstructionBuffer();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Processor.CPU_Cores[0].SquareRoot(
                ArchRegId.Create(1),
                ArchRegId.Create(2)));

        Assert.Contains(XsqrtMessageFragment, exception.Message);
        Assert.Contains(CarrierFragment, exception.Message);
        Assert.Equal(0, Processor.Compiler.InstructionCount);
        Assert.Equal(0, Processor.Compiler.GetRecordedInstructions().Length);
    }

    [Fact]
    public void XsqrtHelper_EmulationModeRejectsBeforeArchitecturalMutation()
    {
        _ = new Processor(ProcessorMode.Emulation);

        const int vtId = 0;
        const int accumulatorRegister = 1;
        const ulong originalAccumulatorValue = 0x5678UL;

        Processor.CPU_Cores[0].WriteCommittedArch(vtId, accumulatorRegister, originalAccumulatorValue);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => Processor.CPU_Cores[0].SquareRoot(
                ArchRegId.Create(accumulatorRegister),
                ArchRegId.Create(2)));

        Assert.Contains(XsqrtMessageFragment, exception.Message);
        Assert.Contains(CarrierFragment, exception.Message);
        Assert.Equal(originalAccumulatorValue, Processor.CPU_Cores[0].ReadArch(vtId, accumulatorRegister));
    }
}
