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
    public sealed class LegacyCsrProhibitionTests
    {
        [Fact]
        public void CSR_READ_IsProhibitedInIsaV4Surface()
        {
            Assert.Contains("CSR_READ", IsaV4Surface.ProhibitedOpcodes);
        }

        [Fact]
        public void CSR_WRITE_IsProhibitedInIsaV4Surface()
        {
            Assert.Contains("CSR_WRITE", IsaV4Surface.ProhibitedOpcodes);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 19. Capability CSR — Registration
    // ═══════════════════════════════════════════════════════════════════════════

}
