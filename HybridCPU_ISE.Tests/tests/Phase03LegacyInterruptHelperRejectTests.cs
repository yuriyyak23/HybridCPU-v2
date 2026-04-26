using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03LegacyInterruptHelperRejectTests
{
    private const string MessageFragment = "typed mainline retire/boundary carrier exists";
    /*
        [Fact]
        public void InterruptArchReg_HelperRejectsInCompilerMode()
        {
            _ = new Processor(ProcessorMode.Compiler);
            Processor.EntryPoint entryPoint = default;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Processor.CPU_Cores[0].Interrupt(
                    ref entryPoint,
                    ArchRegId.Create(1),
                    memoryAddress: 0x180));

            Assert.Contains("Interrupt contour is unsupported", exception.Message);
            Assert.Contains(MessageFragment, exception.Message);
        }

        [Fact]
        public void InterruptReturnArchReg_HelperRejectsInEmulationMode()
        {
            _ = new Processor(ProcessorMode.Emulation);
            Processor.EntryPoint entryPoint = default;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Processor.CPU_Cores[0].InterruptReturn(
                    ref entryPoint,
                    ArchRegId.Create(2)));

            Assert.Contains("InterruptReturn contour is unsupported", exception.Message);
            Assert.Contains(MessageFragment, exception.Message);
        }

        [Fact]
        public void InterruptIntRegister_HelperRejectsInCompilerMode()
        {
            _ = new Processor(ProcessorMode.Compiler);
            Processor.EntryPoint entryPoint = default;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Processor.CPU_Cores[0].Interrupt(
                    ref entryPoint,
                    new Processor.CPU_Core.IntRegister(1, 0),
                    MemoryAddress: 0x1C0));

            Assert.Contains("Interrupt contour is unsupported", exception.Message);
            Assert.Contains(MessageFragment, exception.Message);
        }

        [Fact]
        public void InterruptReturnIntRegister_HelperRejectsInEmulationMode()
        {
            _ = new Processor(ProcessorMode.Emulation);
            Processor.EntryPoint entryPoint = default;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Processor.CPU_Cores[0].InterruptReturn(
                    ref entryPoint,
                    new Processor.CPU_Core.IntRegister(3, 0)));

            Assert.Contains("InterruptReturn contour is unsupported", exception.Message);
            Assert.Contains(MessageFragment, exception.Message);
        }*/
}
