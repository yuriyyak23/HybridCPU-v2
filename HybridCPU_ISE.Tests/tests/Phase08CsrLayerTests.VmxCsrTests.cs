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
    public sealed class VmxCsrTests
    {


        [Fact]
        public void VmxEnable_ReadWrite_FromMachineMode()
        {
            var csr = new CsrFile();
            csr.Write(CsrAddresses.VmxEnable, 1UL, PrivilegeLevel.Machine);
            Assert.Equal(1UL, csr.Read(CsrAddresses.VmxEnable, PrivilegeLevel.Machine));
        }

        [Fact]
        public void VmxControl_IsRegistered()
        {
            var csr = new CsrFile();
            Assert.True(csr.IsRegistered(CsrAddresses.VmxControl));
        }

        [Fact]
        public void VmxExitReason_ReadWrite_FromMachineMode()
        {
            var csr = new CsrFile();
            csr.Write(CsrAddresses.VmxExitReason, 0x42, PrivilegeLevel.Machine);
            Assert.Equal(0x42UL, csr.Read(CsrAddresses.VmxExitReason, PrivilegeLevel.Machine));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 17. RDVTID / RDVTMASK Removal Verification
    // ═══════════════════════════════════════════════════════════════════════════

}
