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
    public sealed class CsrExecutionCsrrsTests
    {
        [Fact]
        public void CSRRS_ReadOnly_WhenRs1IsX0()
        {
            // CSRRS x2, VtId, x0 — canonical read-only VT identity read
            var csr = new CsrFile();
            csr.HardwareWrite(CsrAddresses.VtId, 2UL);
            var disp = new ExecutionDispatcherV4(csrFile: csr);
            var state = new Csr08FakeCpuState();

            var instr = CsrIrHelper.MakeCsr(InstructionsEnum.CSRRS, rd: 2, rs1: 0, imm: CsrAddresses.VtId);
            var result = disp.Execute(instr, state);

            Assert.Equal(2UL, result.Value);
            Assert.Equal(2UL, state.ReadIntRegister(2));
            // No write side-effect (rs1=0 → read-only)
            Assert.Equal(2UL, csr.Read(CsrAddresses.VtId, PrivilegeLevel.Machine));
        }

        [Fact]
        public void CSRRS_SetsBits_WhenRs1IsNonZero()
        {
            var csr = new CsrFile();
            csr.Write(CsrAddresses.Mstatus, 0b0011, PrivilegeLevel.Machine);
            var disp = new ExecutionDispatcherV4(csrFile: csr);
            var state = new Csr08FakeCpuState();
            state.SetReg(1, 0b1100); // bits to set

            // CSRRS x2, Mstatus, x1: rd=old, Mstatus |= x1
            var instr = CsrIrHelper.MakeCsr(InstructionsEnum.CSRRS, rd: 2, rs1: 1, imm: CsrAddresses.Mstatus);
            disp.Execute(instr, state);

            Assert.Equal(0b0011UL, state.ReadIntRegister(2)); // old value
            Assert.Equal(0b1111UL, csr.Read(CsrAddresses.Mstatus, PrivilegeLevel.Machine)); // 0b0011 | 0b1100
        }

        [Fact]
        public void CSRRS_ReadVtMask_ReturnsActiveMask()
        {
            var csr = new CsrFile();
            csr.HardwareWrite(CsrAddresses.VtMask, 0b1111UL);
            var disp = new ExecutionDispatcherV4(csrFile: csr);
            var state = new Csr08FakeCpuState();

            // CSRRS x3, VtMask, x0 — read VT active mask
            var instr = CsrIrHelper.MakeCsr(InstructionsEnum.CSRRS, rd: 3, rs1: 0, imm: CsrAddresses.VtMask);
            var result = disp.Execute(instr, state);

            Assert.Equal(0b1111UL, result.Value);
            Assert.Equal(0b1111UL, state.ReadIntRegister(3));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 8. ExecutionDispatcherV4 — CSRRC Semantics
    // ═══════════════════════════════════════════════════════════════════════════

}
