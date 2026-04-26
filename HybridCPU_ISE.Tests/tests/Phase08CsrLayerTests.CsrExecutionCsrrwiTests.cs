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
    public sealed class CsrExecutionCsrrwiTests
    {
        [Fact]
        public void CSRRWI_WritesZeroExtendedImmediate()
        {
            var csr = new CsrFile();
            csr.Write(CsrAddresses.Mstatus, 0x1234, PrivilegeLevel.Machine);
            var disp = new ExecutionDispatcherV4(csrFile: csr);
            var state = new Csr08FakeCpuState();

            // CSRRWI x2, Mstatus, 0x1F: rd=old, Mstatus = 0x1F (5-bit zimm from Rs1 field)
            var instr = CsrIrHelper.MakeCsr(InstructionsEnum.CSRRWI, rd: 2, rs1: 0x1F, imm: CsrAddresses.Mstatus);
            disp.Execute(instr, state);

            Assert.Equal(0x1234UL, state.ReadIntRegister(2)); // old value
            Assert.Equal(0x1FUL, csr.Read(CsrAddresses.Mstatus, PrivilegeLevel.Machine)); // zimm = 31
        }

        [Fact]
        public void CSRRWI_5BitMasking()
        {
            // Verify only lower 5 bits of Rs1 field are used
            var csr = new CsrFile();
            var disp = new ExecutionDispatcherV4(csrFile: csr);
            var state = new Csr08FakeCpuState();

            // Rs1 = 0xFF — should be masked to 0x1F (lower 5 bits)
            var instr = CsrIrHelper.MakeCsr(InstructionsEnum.CSRRWI, rd: 2, rs1: 0xFF, imm: CsrAddresses.Mstatus);
            disp.Execute(instr, state);

            Assert.Equal(0x1FUL, csr.Read(CsrAddresses.Mstatus, PrivilegeLevel.Machine));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 10. ExecutionDispatcherV4 — CSRRSI Semantics
    // ═══════════════════════════════════════════════════════════════════════════

}
