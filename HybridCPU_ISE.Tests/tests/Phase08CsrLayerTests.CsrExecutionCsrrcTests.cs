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
    public sealed class CsrExecutionCsrrcTests
    {
        [Fact]
        public void CSRRC_ClearsBits_WhenRs1IsNonZero()
        {
            var csr = new CsrFile();
            csr.Write(CsrAddresses.Mstatus, 0b1111, PrivilegeLevel.Machine);
            var disp = new ExecutionDispatcherV4(csrFile: csr);
            var state = new Csr08FakeCpuState();
            state.SetReg(1, 0b1010); // bits to clear

            // CSRRC x2, Mstatus, x1: rd=old, Mstatus &= ~x1
            var instr = CsrIrHelper.MakeCsr(InstructionsEnum.CSRRC, rd: 2, rs1: 1, imm: CsrAddresses.Mstatus);
            disp.Execute(instr, state);

            Assert.Equal(0b1111UL, state.ReadIntRegister(2)); // old value
            Assert.Equal(0b0101UL, csr.Read(CsrAddresses.Mstatus, PrivilegeLevel.Machine)); // 0b1111 & ~0b1010
        }

        [Fact]
        public void CSRRC_NoWrite_WhenRs1IsX0()
        {
            var csr = new CsrFile();
            csr.Write(CsrAddresses.Mstatus, 0b1111, PrivilegeLevel.Machine);
            var disp = new ExecutionDispatcherV4(csrFile: csr);
            var state = new Csr08FakeCpuState();

            var instr = CsrIrHelper.MakeCsr(InstructionsEnum.CSRRC, rd: 2, rs1: 0, imm: CsrAddresses.Mstatus);
            disp.Execute(instr, state);

            Assert.Equal(0b1111UL, state.ReadIntRegister(2)); // read value
            Assert.Equal(0b1111UL, csr.Read(CsrAddresses.Mstatus, PrivilegeLevel.Machine)); // unchanged
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 9. ExecutionDispatcherV4 — CSRRWI Semantics
    // ═══════════════════════════════════════════════════════════════════════════

}
