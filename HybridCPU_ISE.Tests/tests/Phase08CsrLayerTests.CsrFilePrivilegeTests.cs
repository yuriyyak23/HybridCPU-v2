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
    public sealed class CsrFilePrivilegeTests
    {
        [Fact]
        public void MachineCSR_ReadFromUserMode_ThrowsPrivilegeFault()
        {
            var csr = new CsrFile();
            var ex = Assert.Throws<CsrPrivilegeFaultException>(
                () => csr.Read(CsrAddresses.Mstatus, PrivilegeLevel.User));
            Assert.Equal(CsrAddresses.Mstatus, ex.CsrAddress);
            Assert.False(ex.IsWrite);
        }

        [Fact]
        public void MachineCSR_WriteFromUserMode_ThrowsPrivilegeFault()
        {
            var csr = new CsrFile();
            var ex = Assert.Throws<CsrPrivilegeFaultException>(
                () => csr.Write(CsrAddresses.Mstatus, 1, PrivilegeLevel.User));
            Assert.True(ex.IsWrite);
        }

        [Fact]
        public void MachineCSR_ReadFromSupervisorMode_ThrowsPrivilegeFault()
        {
            var csr = new CsrFile();
            Assert.Throws<CsrPrivilegeFaultException>(
                () => csr.Read(CsrAddresses.Mstatus, PrivilegeLevel.Supervisor));
        }

        [Fact]
        public void MachineCSR_ReadWriteFromMachineMode_Succeeds()
        {
            var csr = new CsrFile();
            csr.Write(CsrAddresses.Mstatus, 0x1234, PrivilegeLevel.Machine);
            Assert.Equal(0x1234UL, csr.Read(CsrAddresses.Mstatus, PrivilegeLevel.Machine));
        }

        [Fact]
        public void SupervisorCSR_ReadFromUserMode_Succeeds()
        {
            // S-mode CSRs are readable from all modes
            var csr = new CsrFile();
            csr.Write(CsrAddresses.Sstatus, 0xAB, PrivilegeLevel.Machine);
            Assert.Equal(0xABUL, csr.Read(CsrAddresses.Sstatus, PrivilegeLevel.User));
        }

        [Fact]
        public void SupervisorCSR_WriteFromUserMode_ThrowsPrivilegeFault()
        {
            var csr = new CsrFile();
            Assert.Throws<CsrPrivilegeFaultException>(
                () => csr.Write(CsrAddresses.Sstatus, 0xAB, PrivilegeLevel.User));
        }

        [Fact]
        public void SupervisorCSR_WriteFromSupervisorMode_Succeeds()
        {
            var csr = new CsrFile();
            csr.Write(CsrAddresses.Sstatus, 0xAB, PrivilegeLevel.Supervisor);
            Assert.Equal(0xABUL, csr.Read(CsrAddresses.Sstatus, PrivilegeLevel.Supervisor));
        }

        [Fact]
        public void HardwareUpdatedCSR_SoftwareWrite_ThrowsPrivilegeFault()
        {
            var csr = new CsrFile();
            // VtId is hardware-updated — software writes should be denied even from M-mode
            Assert.Throws<CsrPrivilegeFaultException>(
                () => csr.Write(CsrAddresses.VtId, 1, PrivilegeLevel.Machine));
        }

        [Fact]
        public void HardwareUpdatedCSR_ReadFromAnyMode_Succeeds()
        {
            var csr = new CsrFile();
            csr.HardwareWrite(CsrAddresses.VtId, 3UL);
            Assert.Equal(3UL, csr.Read(CsrAddresses.VtId, PrivilegeLevel.User));
            Assert.Equal(3UL, csr.Read(CsrAddresses.VtId, PrivilegeLevel.Supervisor));
            Assert.Equal(3UL, csr.Read(CsrAddresses.VtId, PrivilegeLevel.Machine));
        }

        [Fact]
        public void ReadOnlyCSR_WriteFromMachine_ThrowsPrivilegeFault()
        {
            var csr = new CsrFile();
            // Misa is read-only
            Assert.Throws<CsrPrivilegeFaultException>(
                () => csr.Write(CsrAddresses.Misa, 0xAB, PrivilegeLevel.Machine));
        }

        [Fact]
        public void ReadOnlyCSR_HardwareWrite_Succeeds()
        {
            var csr = new CsrFile();
            csr.HardwareWrite(CsrAddresses.Misa, 0x1234);
            Assert.Equal(0x1234UL, csr.Read(CsrAddresses.Misa, PrivilegeLevel.Machine));
        }

        [Fact]
        public void CounterCSR_SoftwareWrite_ThrowsPrivilegeFault()
        {
            var csr = new CsrFile();
            Assert.Throws<CsrPrivilegeFaultException>(
                () => csr.Write(CsrAddresses.Cycle, 100, PrivilegeLevel.Machine));
        }

        [Fact]
        public void CounterCSR_HardwareWrite_ThenRead_Succeeds()
        {
            var csr = new CsrFile();
            csr.HardwareWrite(CsrAddresses.Cycle, 1000UL);
            Assert.Equal(1000UL, csr.Read(CsrAddresses.Cycle, PrivilegeLevel.User));
        }

    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 5. CsrFile — VT Identity Registers
    // ═══════════════════════════════════════════════════════════════════════════

}
