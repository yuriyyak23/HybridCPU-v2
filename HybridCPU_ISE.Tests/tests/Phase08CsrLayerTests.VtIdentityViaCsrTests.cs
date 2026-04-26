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
    public sealed class VtIdentityViaCsrTests
    {
        [Theory]
        [InlineData(0UL)]
        [InlineData(1UL)]
        [InlineData(2UL)]
        [InlineData(3UL)]
        public void ReadVtId_ViaCsrrs_ReturnsCorrectVtIndex(ulong vtId)
        {
            var csr = new CsrFile();
            csr.HardwareWrite(CsrAddresses.VtId, vtId);
            var disp = new ExecutionDispatcherV4(csrFile: csr);
            var state = new Csr08FakeCpuState();

            // CSRRS x1, 0x800, x0 → rd = VTID (pseudo: RDVTID x1)
            var instr = CsrIrHelper.MakeCsr(InstructionsEnum.CSRRS, rd: 1, rs1: 0, imm: CsrAddresses.VtId);
            disp.Execute(instr, state);

            Assert.Equal(vtId, state.ReadIntRegister(1));
        }

        [Theory]
        [InlineData(0b0001UL)]
        [InlineData(0b0011UL)]
        [InlineData(0b0111UL)]
        [InlineData(0b1111UL)]
        public void ReadVtMask_ViaCsrrs_ReturnsCorrectActiveMask(ulong mask)
        {
            var csr = new CsrFile();
            csr.HardwareWrite(CsrAddresses.VtMask, mask);
            var disp = new ExecutionDispatcherV4(csrFile: csr);
            var state = new Csr08FakeCpuState();

            // CSRRS x1, 0x801, x0 → rd = VTMASK (pseudo: RDVTMASK x1)
            var instr = CsrIrHelper.MakeCsr(InstructionsEnum.CSRRS, rd: 1, rs1: 0, imm: CsrAddresses.VtMask);
            disp.Execute(instr, state);

            Assert.Equal(mask, state.ReadIntRegister(1));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 15. Counter CSR — Functional Tests
    // ═══════════════════════════════════════════════════════════════════════════

}
