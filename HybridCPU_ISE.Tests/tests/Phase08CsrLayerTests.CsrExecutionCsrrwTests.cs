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
    public sealed class CsrExecutionCsrrwTests
    {
        [Fact]
        public void CSRRW_ReadsOldValue_WritesNewValue()
        {
            var csr = new CsrFile();
            csr.Write(CsrAddresses.Mstatus, 0xAAAA, PrivilegeLevel.Machine);
            var disp = new ExecutionDispatcherV4(csrFile: csr);
            var state = new Csr08FakeCpuState();
            state.SetReg(1, 0xBBBB); // rs1 = x1 = 0xBBBB

            // CSRRW x2, Mstatus, x1: rd=x2 gets old value, Mstatus gets x1
            var instr = CsrIrHelper.MakeCsr(InstructionsEnum.CSRRW, rd: 2, rs1: 1, imm: CsrAddresses.Mstatus);
            var result = disp.Execute(instr, state);

            Assert.Equal(0xAAAAUL, result.Value);
            Assert.Equal(0xAAAAUL, state.ReadIntRegister(2)); // rd = old value
            Assert.Equal(0xBBBBUL, csr.Read(CsrAddresses.Mstatus, PrivilegeLevel.Machine)); // CSR = new value
        }

        [Fact]
        public void CSRRW_WithRd0_DoesNotWriteRegister()
        {
            var csr = new CsrFile();
            csr.Write(CsrAddresses.Mstatus, 0x1111, PrivilegeLevel.Machine);
            var disp = new ExecutionDispatcherV4(csrFile: csr);
            var state = new Csr08FakeCpuState();
            state.SetReg(1, 0x2222);

            // CSRRW x0, Mstatus, x1: rd=x0 — don't write (x0 hardwired to 0)
            var instr = CsrIrHelper.MakeCsr(InstructionsEnum.CSRRW, rd: 0, rs1: 1, imm: CsrAddresses.Mstatus);
            disp.Execute(instr, state);

            Assert.Equal(0UL, state.ReadIntRegister(0)); // x0 stays zero
            Assert.Equal(0x2222UL, csr.Read(CsrAddresses.Mstatus, PrivilegeLevel.Machine)); // CSR updated
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 7. ExecutionDispatcherV4 — CSRRS Semantics
    // ═══════════════════════════════════════════════════════════════════════════

}
