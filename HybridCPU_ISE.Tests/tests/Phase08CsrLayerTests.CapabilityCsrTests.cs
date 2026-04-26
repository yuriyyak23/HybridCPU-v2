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
    public sealed class CapabilityCsrTests
    {
        [Theory]
        [InlineData(0x810)] // IsaCaps
        [InlineData(0x811)] // LaneCaps
        [InlineData(0x812)] // BundleCaps
        [InlineData(0x813)] // SafetyCaps
        [InlineData(0x814)] // MemCaps
        public void CapabilityCsrs_AreRegistered(int addr)
        {
            var csr = new CsrFile();
            Assert.True(csr.IsRegistered((ushort)addr));
        }

        /*        [Theory]
                [InlineData(0x810)]
                [InlineData(0x811)]
                [InlineData(0x812)]
                [InlineData(0x813)]
                [InlineData(0x814)]
                public void CapabilityCsrs_AreReadOnly(int addr)
                {
                    var csr = new CsrFile();
                    Assert.Equal(CsrAccessPolicy.ReadOnly, csr.GetPolicy((ushort)addr));
                }*/

        [Fact]
        public void CapabilityCsr_HardwareWrite_ThenRead_Succeeds()
        {
            var csr = new CsrFile();
            csr.HardwareWrite(CsrAddresses.IsaCaps, 0x04); // ISA v4 indicator
            Assert.Equal(0x04UL, csr.Read(CsrAddresses.IsaCaps, PrivilegeLevel.User));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 20. All Registered CSRs — Completeness
    // ═══════════════════════════════════════════════════════════════════════════

}
