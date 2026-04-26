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
    public sealed class CounterCsrTests
    {
        [Fact]
        public void CycleCsr_ReadAfterHardwareWrite_ReturnsValue()
        {
            var csr = new CsrFile();
            csr.HardwareWrite(CsrAddresses.Cycle, 5000UL);
            var disp = new ExecutionDispatcherV4(csrFile: csr);
            var state = new Csr08FakeCpuState();

            var instr = CsrIrHelper.MakeCsr(InstructionsEnum.CSRRS, rd: 1, rs1: 0, imm: CsrAddresses.Cycle);
            disp.Execute(instr, state);

            Assert.Equal(5000UL, state.ReadIntRegister(1));
        }

        [Fact]
        public void BundleRetCsr_ReadAfterHardwareWrite_ReturnsValue()
        {
            var csr = new CsrFile();
            csr.HardwareWrite(CsrAddresses.BundleRet, 100UL);
            var disp = new ExecutionDispatcherV4(csrFile: csr);
            var state = new Csr08FakeCpuState();

            var instr = CsrIrHelper.MakeCsr(InstructionsEnum.CSRRS, rd: 1, rs1: 0, imm: CsrAddresses.BundleRet);
            disp.Execute(instr, state);

            Assert.Equal(100UL, state.ReadIntRegister(1));
        }

        [Fact]
        public void InstrRetCsr_ReadAfterHardwareWrite_ReturnsValue()
        {
            var csr = new CsrFile();
            csr.HardwareWrite(CsrAddresses.InstrRet, 42000UL);

            Assert.Equal(42000UL, csr.Read(CsrAddresses.InstrRet, PrivilegeLevel.User));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 16. VMX CSR — Registration and Access
    // ═══════════════════════════════════════════════════════════════════════════

}
