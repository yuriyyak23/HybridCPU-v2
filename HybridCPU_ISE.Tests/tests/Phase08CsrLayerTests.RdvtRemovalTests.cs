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
    public sealed class RdvtRemovalTests
    {
        [Fact]
        public void RDVTID_IsProhibitedInIsaV4Surface()
        {
            Assert.Contains("RDVTID", IsaV4Surface.ProhibitedOpcodes);
        }

        [Fact]
        public void RDVTMASK_IsProhibitedInIsaV4Surface()
        {
            Assert.Contains("RDVTMASK", IsaV4Surface.ProhibitedOpcodes);
        }

        [Fact]
        public void RDVTID_NotInMandatoryCoreOpcodes()
        {
            Assert.DoesNotContain("RDVTID", IsaV4Surface.MandatoryCoreOpcodes);
        }

        [Fact]
        public void RDVTMASK_NotInMandatoryCoreOpcodes()
        {
            Assert.DoesNotContain("RDVTMASK", IsaV4Surface.MandatoryCoreOpcodes);
        }

        [Fact]
        public void VtIdentity_AccessedViaCsrPlane_NotDedicatedOpcode()
        {
            // The canonical way to read VT identity is CSRRS rd, 0x800, x0
            // Verify the CSR addresses exist and are in the correct group
            Assert.Equal(0x800, CsrAddresses.VtId);
            Assert.Equal(0x801, CsrAddresses.VtMask);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 18. CSR_READ / CSR_WRITE Prohibited in IsaV4Surface
    // ═══════════════════════════════════════════════════════════════════════════

}
