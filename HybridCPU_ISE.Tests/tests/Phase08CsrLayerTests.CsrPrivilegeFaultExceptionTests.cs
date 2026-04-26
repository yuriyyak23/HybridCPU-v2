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
    public sealed class CsrPrivilegeFaultExceptionTests
    {
        [Fact]
        public void ExceptionCarriesCorrectProperties()
        {
            var ex = new CsrPrivilegeFaultException(
                CsrAddresses.Mstatus, PrivilegeLevel.Machine, PrivilegeLevel.User, isWrite: true);

            Assert.Equal(CsrAddresses.Mstatus, ex.CsrAddress);
            Assert.Equal(PrivilegeLevel.Machine, ex.RequiredLevel);
            Assert.Equal(PrivilegeLevel.User, ex.ActualLevel);
            Assert.True(ex.IsWrite);
            Assert.Contains("0x300", ex.Message);
        }
    }
}
