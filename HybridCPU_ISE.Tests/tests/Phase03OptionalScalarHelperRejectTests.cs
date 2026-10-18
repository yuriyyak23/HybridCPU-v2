using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests;

public sealed class OptionalScalarHelperRejectTests
{
    private const string XsqrtMessageFragment = "Optional scalar XSQRT contour is unsupported";
    private const string CarrierFragment = "scalar carrier/materializer follow-through";

    [Fact]
    public void XsqrtHelper_CompilerModeRejectsBeforeInstructionPublication()
    {
        ProcessorMode previousMode = Processor.CurrentProcessorMode;
        try
        {
            Processor.CurrentProcessorMode = ProcessorMode.Compiler;
            Processor.Compiler.ResetInstructionBuffer();
            Processor.CPU_Core core = new(0);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => core.SquareRoot(
                    ArchRegId.Create(1),
                    ArchRegId.Create(2)));

            Assert.Contains(XsqrtMessageFragment, exception.Message);
            Assert.Contains(CarrierFragment, exception.Message);
            Assert.Equal(0, Processor.Compiler.InstructionCount);
            Assert.Equal(0, Processor.Compiler.GetRecordedInstructions().Length);
        }
        finally
        {
            Processor.CurrentProcessorMode = previousMode;
            Processor.Compiler.ResetInstructionBuffer();
        }
    }

    [Fact]
    public void XsqrtHelper_EmulationModeRejectsBeforeArchitecturalMutation()
    {
        ProcessorMode previousMode = Processor.CurrentProcessorMode;
        try
        {
            Processor.CurrentProcessorMode = ProcessorMode.Emulation;
            Processor.CPU_Core core = new(0);

            const int vtId = 0;
            const int accumulatorRegister = 1;
            const ulong originalAccumulatorValue = 0x5678UL;

            core.WriteCommittedArch(vtId, accumulatorRegister, originalAccumulatorValue);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => core.SquareRoot(
                    ArchRegId.Create(accumulatorRegister),
                    ArchRegId.Create(2)));

            Assert.Contains(XsqrtMessageFragment, exception.Message);
            Assert.Contains(CarrierFragment, exception.Message);
            Assert.Equal(originalAccumulatorValue, core.ReadArch(vtId, accumulatorRegister));
        }
        finally
        {
            Processor.CurrentProcessorMode = previousMode;
        }
    }
}
