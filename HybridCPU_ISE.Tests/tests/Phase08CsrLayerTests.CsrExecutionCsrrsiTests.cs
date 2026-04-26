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
    public sealed class CsrExecutionCsrrsiTests
    {
        [Fact]
        public void CSRRSI_SetsBits_WhenZimmNonZero()
        {
            var csr = new CsrFile();
            csr.Write(CsrAddresses.Mstatus, 0b0001, PrivilegeLevel.Machine);
            var disp = new ExecutionDispatcherV4(csrFile: csr);
            var state = new Csr08FakeCpuState();

            // CSRRSI x2, Mstatus, 0b0110
            var instr = CsrIrHelper.MakeCsr(InstructionsEnum.CSRRSI, rd: 2, rs1: 0b0110, imm: CsrAddresses.Mstatus);
            disp.Execute(instr, state);

            Assert.Equal(0b0001UL, state.ReadIntRegister(2)); // old value
            Assert.Equal(0b0111UL, csr.Read(CsrAddresses.Mstatus, PrivilegeLevel.Machine)); // 0b0001 | 0b0110
        }

        [Fact]
        public void CSRRSI_NoWrite_WhenZimmIsZero()
        {
            var csr = new CsrFile();
            csr.Write(CsrAddresses.Mstatus, 0b1111, PrivilegeLevel.Machine);
            var disp = new ExecutionDispatcherV4(csrFile: csr);
            var state = new Csr08FakeCpuState();

            var instr = CsrIrHelper.MakeCsr(InstructionsEnum.CSRRSI, rd: 2, rs1: 0, imm: CsrAddresses.Mstatus);
            disp.Execute(instr, state);

            Assert.Equal(0b1111UL, csr.Read(CsrAddresses.Mstatus, PrivilegeLevel.Machine)); // unchanged
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 11. ExecutionDispatcherV4 — CSRRCI Semantics
    // ═══════════════════════════════════════════════════════════════════════════

}
