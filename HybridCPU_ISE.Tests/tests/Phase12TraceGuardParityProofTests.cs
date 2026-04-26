using System;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase12
{
    public sealed class TraceGuardParityProofTests
    {
        private static (CapturingSink sink, TelemetryCounters telemetry, CapturingEventQueue queue, ExecutionDispatcherV4 dispatcher) Build()
        {
            var sink = new CapturingSink();
            var telemetry = new TelemetryCounters();
            var queue = new CapturingEventQueue();
            var dispatcher = new ExecutionDispatcherV4(
                traceSink: sink,
                telemetry: telemetry,
                pipelineEventQueue: queue);
            return (sink, telemetry, queue, dispatcher);
        }

        [Theory]
        [InlineData(InstructionsEnum.VSETVL)]
        [InlineData(InstructionsEnum.VSETVLI)]
        [InlineData(InstructionsEnum.VSETIVLI)]
        public void Execute_VectorConfigEagerSurfaceRejected_DoesNotPublishTraceOrTelemetry(
            InstructionsEnum opcode)
        {
            var (sink, telemetry, queue, dispatcher) = Build();
            var state = new P12CpuState();
            state.SetReg(5, 19UL);
            state.SetReg(6, 0x43UL);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(P12Ir.Make(opcode, rd: 4, rs1: 5, rs2: 6, imm: 13), state, 1, vtId: 0));

            Assert.Contains("Vector-config opcode", ex.Message, StringComparison.Ordinal);
            Assert.Empty(queue.Events);
            Assert.Empty(sink.Events);
            Assert.Equal(0UL, telemetry.GetInstrCountForVt(0));
        }

        [Theory]
        [InlineData(InstructionsEnum.VSETVEXCPMASK)]
        [InlineData(InstructionsEnum.VSETVEXCPPRI)]
        public void Execute_VectorExceptionControlCsrSurfaceRejected_DoesNotPublishTraceOrTelemetry(
            InstructionsEnum opcode)
        {
            var (sink, telemetry, queue, dispatcher) = Build();
            var state = new P12CpuState();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(P12Ir.Make(opcode, rd: 0, rs1: 1, imm: 7), state, 1, vtId: 0));

            Assert.Contains("canonical mainline CSR/materializer surface", ex.Message, StringComparison.Ordinal);
            Assert.Empty(queue.Events);
            Assert.Empty(sink.Events);
            Assert.Equal(0UL, telemetry.GetInstrCountForVt(0));
        }

        [Theory]
        [InlineData(InstructionsEnum.Interrupt)]
        [InlineData(InstructionsEnum.InterruptReturn)]
        public void Execute_RetainedInterruptSurfaceRejected_DoesNotPublishTraceOrTelemetry(
            InstructionsEnum opcode)
        {
            var (sink, telemetry, queue, dispatcher) = Build();
            var state = new P12CpuState();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(P12Ir.Make(opcode), state, 1, vtId: 0));

            Assert.Contains("system success/trace", ex.Message, StringComparison.Ordinal);
            Assert.Empty(queue.Events);
            Assert.Empty(sink.Events);
            Assert.Equal(0UL, telemetry.GetInstrCountForVt(0));
        }

        [Theory]
        [InlineData(InstructionsEnum.CSRRW)]
        [InlineData(InstructionsEnum.CSRRS)]
        public void Execute_NonClearCsrWithoutCsrFileRejected_DoesNotPublishTraceOrTelemetry(
            InstructionsEnum opcode)
        {
            var (sink, telemetry, queue, dispatcher) = Build();
            var state = new P12CpuState();

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => dispatcher.Execute(P12Ir.Make(opcode, rs1: 1, imm: CsrAddresses.Mstatus), state, 1, vtId: 0));

            Assert.Contains("without a wired CsrFile", ex.Message, StringComparison.Ordinal);
            Assert.Empty(queue.Events);
            Assert.Empty(sink.Events);
            Assert.Equal(0UL, telemetry.GetInstrCountForVt(0));
        }
    }
}
