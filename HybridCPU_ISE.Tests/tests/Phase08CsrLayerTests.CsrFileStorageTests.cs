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
    // 3. CsrFile вЂ” Register File Storage and Access Control
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class CsrFileStorageTests
    {



        [Fact]
        public void DefaultValue_IsZero()
        {
            var csr = new CsrFile();
            Assert.Equal(0UL, csr.Read(CsrAddresses.Mstatus, PrivilegeLevel.Machine));
        }

        [Fact]
        public void PowerControl_DefaultsMatchBootState()
        {
            var csr = new CsrFile();

            Assert.Equal(
                (ulong)CorePowerState.C0_Active,
                csr.Read(CsrAddresses.MpowerState, PrivilegeLevel.Machine));
            Assert.Equal(
                (ulong)CorePowerState.P0_MaxPerformance,
                csr.Read(CsrAddresses.MperfLevel, PrivilegeLevel.Machine));
        }

        [Fact]
        public void Write_ThenRead_ReturnsSameValue()
        {
            var csr = new CsrFile();
            csr.Write(CsrAddresses.Mstatus, 0xDEAD_BEEF, PrivilegeLevel.Machine);
            Assert.Equal(0xDEAD_BEEF, csr.Read(CsrAddresses.Mstatus, PrivilegeLevel.Machine));
        }

        [Fact]
        public void HardwareWrite_SetsValue()
        {
            var csr = new CsrFile();
            csr.HardwareWrite(CsrAddresses.VtId, 2UL);
            Assert.Equal(2UL, csr.Read(CsrAddresses.VtId, PrivilegeLevel.Machine));
        }

        [Fact]
        public void DirectRead_BypassesPrivilege()
        {
            var csr = new CsrFile();
            csr.HardwareWrite(CsrAddresses.Mstatus, 0x42UL);
            Assert.Equal(0x42UL, csr.DirectRead(CsrAddresses.Mstatus));
        }

        [Fact]
        public void IsRegistered_TrueForKnownAddress()
        {
            var csr = new CsrFile();
            Assert.True(csr.IsRegistered(CsrAddresses.Mstatus));
            Assert.True(csr.IsRegistered(CsrAddresses.VtId));
            Assert.True(csr.IsRegistered(CsrAddresses.Cycle));
        }

        [Fact]
        public void IsRegistered_FalseForUnknownAddress()
        {
            var csr = new CsrFile();
            Assert.False(csr.IsRegistered(0xFFF));
        }

        [Fact]
        public void UnknownAddress_ThrowsCsrUnknownAddressException()
        {
            var csr = new CsrFile();
            Assert.Throws<CsrUnknownAddressException>(() => csr.Read(0xFFF, PrivilegeLevel.Machine));
        }

        [Fact]
        public void UnknownAddress_OnWrite_ThrowsCsrUnknownAddressException()
        {
            var csr = new CsrFile();
            Assert.Throws<CsrUnknownAddressException>(() => csr.Write(0xFFF, 0, PrivilegeLevel.Machine));
        }

        [Fact]
        public void UnknownAddress_OnHardwareWrite_ThrowsCsrUnknownAddressException()
        {
            var csr = new CsrFile();
            Assert.Throws<CsrUnknownAddressException>(() => csr.HardwareWrite(0xFFF, 0));
        }
    }

    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 4. CsrFile вЂ” Access Control / Privilege Checking
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

}
