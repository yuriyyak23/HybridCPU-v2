// Phase 08: CSR Layer � CSR Plane Cleanup and VT Identity
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
    public sealed class CsrExecutionLegacyTests
    {

        [Fact]
        public void CSR_READ_ReadsViaCSRFile()
        {
            var csr = new CsrFile();
            csr.Write(CsrAddresses.Mstatus, 0xCAFE, PrivilegeLevel.Machine);
            var disp = new ExecutionDispatcherV4(csrFile: csr);
            var state = new Csr08FakeCpuState();

            var instr = CsrIrHelper.MakeCsr(InstructionsEnum.CSRRS, rd: 3, imm: CsrAddresses.Mstatus);
            disp.Execute(instr, state);

            Assert.Equal(0xCAFEUL, state.ReadIntRegister(3));
        }

        [Fact]
        public void CSR_WRITE_WritesViaCSRFile()
        {
            var csr = new CsrFile();
            var disp = new ExecutionDispatcherV4(csrFile: csr);
            var state = new Csr08FakeCpuState();
            state.SetReg(1, 0xDEAD);

            var instr = CsrIrHelper.MakeCsr(InstructionsEnum.CSRRW, rs1: 1, imm: CsrAddresses.Mstatus);
            disp.Execute(instr, state);

            Assert.Equal(0xDEADUL, csr.Read(CsrAddresses.Mstatus, PrivilegeLevel.Machine));
        }

        [Fact]
        public void CSR_CLEAR_ClearsExceptionCounters()
        {
            var csr = new CsrFile();
            var disp = new ExecutionDispatcherV4(csrFile: csr);
            var state = new Csr08FakeCpuState();

            var instr = CsrIrHelper.MakeCsr(InstructionsEnum.CSR_CLEAR);
            var result = disp.Execute(instr, state);

            Assert.Equal(0UL, result.Value);
            Assert.False(result.TrapRaised);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 13. ExecutionDispatcherV4 — Stub Fallback (no CsrFile wired)
    // ═══════════════════════════════════════════════════════════════════════════

}
