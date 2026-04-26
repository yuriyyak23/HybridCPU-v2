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
    // 2. CsrAccessPolicy — Enum Values
    // ═══════════════════════════════════════════════════════════════════════════

    public sealed class CsrAccessPolicyTests
    {
        [Fact]
        public void AllPoliciesAreDefined()
        {
            var values = Enum.GetValues<CsrAccessPolicy>();
            Assert.Equal(4, values.Length);
        }

        [Fact]
        public void PrivilegeLevels_UserIsLowest()
        {
            Assert.True(PrivilegeLevel.User < PrivilegeLevel.Supervisor);
            Assert.True(PrivilegeLevel.Supervisor < PrivilegeLevel.Machine);
        }
    }

}
