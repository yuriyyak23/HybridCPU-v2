// Phase 08: CSR Layer — CSR Plane Cleanup and VT Identity
// Split into focused test files for surgical maintenance.

using System;
using System.Collections.Generic;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase08
{
    public sealed class CsrExecutionStubFallbackTests
    {
        [Theory]
        [InlineData(InstructionsEnum.CSRRW, CsrAddresses.Mstatus)]
        [InlineData(InstructionsEnum.CSRRS, CsrAddresses.VtId)]
        public void NoCsrFile_NonClearCsrSurface_RejectsStubFallback(
            InstructionsEnum opcode,
            ushort csrAddress)
        {
            var disp = new ExecutionDispatcherV4(); // no CsrFile
            var state = new Csr08FakeCpuState();
            state.SetReg(1, 42);

            var instr = CsrIrHelper.MakeCsr(opcode, rd: 2, rs1: 1, imm: csrAddress);

            Assert.False(disp.CanRouteToConfiguredExecutionSurface(instr));

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => disp.Execute(instr, state));

            Assert.Contains("wired CsrFile", ex.Message, StringComparison.Ordinal);
        Assert.Contains("CaptureRetireWindowPublications", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void NoCsrFile_CsrClear_RemainsSupported()
        {
            var disp = new ExecutionDispatcherV4();
            var state = new Csr08FakeCpuState();

            var instr = CsrIrHelper.MakeCsr(InstructionsEnum.CSR_CLEAR);
            var result = disp.Execute(instr, state);

            Assert.True(disp.CanRouteToConfiguredExecutionSurface(instr));
            Assert.Equal(0UL, result.Value);
        }

        [Fact]
        public void NoCsrFile_InvalidOpcode_ThrowsInvalidOpcodeException()
        {
            var disp = new ExecutionDispatcherV4(csrFile: new CsrFile());
            var state = new Csr08FakeCpuState();

            // Use an opcode that doesn't belong to CSR class
            var instr = new InstructionIR
            {
                CanonicalOpcode = InstructionsEnum.Addition,
                Class = InstructionClass.Csr, // forced to CSR for dispatch
                SerializationClass = SerializationClass.CsrOrdered,
                Rd = 0, Rs1 = 0, Rs2 = 0, Imm = 0,
            };

            Assert.Throws<InvalidOpcodeException>(() => disp.Execute(instr, state));
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 14. VT Identity via CSR вЂ” End-to-End Integration
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

}
