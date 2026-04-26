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
    public sealed class CsrFileVtIdentityTests
    {
        [Theory]
        [InlineData(0UL)]
        [InlineData(1UL)]
        [InlineData(2UL)]
        [InlineData(3UL)]
        public void VtId_ReturnsCorrectThreadId_ForEachVt(ulong vtId)
        {
            var csr = new CsrFile();
            csr.HardwareWrite(CsrAddresses.VtId, vtId);
            Assert.Equal(vtId, csr.Read(CsrAddresses.VtId, PrivilegeLevel.User));
        }

        [Theory]
        [InlineData(0b0001UL)] // VT0 active
        [InlineData(0b0011UL)] // VT0+VT1 active
        [InlineData(0b0111UL)] // VT0+VT1+VT2 active
        [InlineData(0b1111UL)] // All 4 VTs active
        public void VtMask_ReturnsCorrectActiveMask(ulong mask)
        {
            var csr = new CsrFile();
            csr.HardwareWrite(CsrAddresses.VtMask, mask);
            Assert.Equal(mask, csr.Read(CsrAddresses.VtMask, PrivilegeLevel.User));
        }

        [Fact]
        public void VtStatus_ReturnsCorrectStatus()
        {
            var csr = new CsrFile();
            csr.HardwareWrite(CsrAddresses.VtStatus, 0x01UL); // running
            Assert.Equal(0x01UL, csr.Read(CsrAddresses.VtStatus, PrivilegeLevel.User));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 6. ExecutionDispatcherV4 — CSRRW Semantics
    // ═══════════════════════════════════════════════════════════════════════════

}
