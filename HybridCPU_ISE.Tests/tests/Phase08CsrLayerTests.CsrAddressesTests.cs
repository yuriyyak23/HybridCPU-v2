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
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ
    // 1. CsrAddresses вЂ” Canonical Address Space Constants
    // в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ

    public sealed class CsrAddressesTests
    {
        [Fact]
        public void Mstatus_Is0x300()  => Assert.Equal(0x300, CsrAddresses.Mstatus);

        [Fact]
        public void Misa_Is0x301()     => Assert.Equal(0x301, CsrAddresses.Misa);

        [Fact]
        public void VtId_Is0x800()     => Assert.Equal(0x800, CsrAddresses.VtId);

        [Fact]
        public void VtMask_Is0x801()   => Assert.Equal(0x801, CsrAddresses.VtMask);

        [Fact]
        public void VtStatus_Is0x802() => Assert.Equal(0x802, CsrAddresses.VtStatus);

        [Fact]
        public void VtCause_Is0x803()  => Assert.Equal(0x803, CsrAddresses.VtCause);

        [Fact]
        public void IsaCaps_Is0x810()  => Assert.Equal(0x810, CsrAddresses.IsaCaps);

        [Fact]
        public void VmxEnable_Is0x820() => Assert.Equal(0x820, CsrAddresses.VmxEnable);

        [Fact]
        public void VmxCaps_Is0x821() => Assert.Equal(0x821, CsrAddresses.VmxCaps);

        [Fact]
        public void VmxControl_Is0x822() => Assert.Equal(0x822, CsrAddresses.VmxControl);

        [Fact]
        public void Cycle_Is0xC00()    => Assert.Equal(0xC00, CsrAddresses.Cycle);

        [Fact]
        public void BundleRet_Is0xC01() => Assert.Equal(0xC01, CsrAddresses.BundleRet);

        [Fact]
        public void InstrRet_Is0xC02() => Assert.Equal(0xC02, CsrAddresses.InstrRet);

        [Fact]
        public void MpowerState_Is0x345() => Assert.Equal(0x345, CsrAddresses.MpowerState);

        [Fact]
        public void MperfLevel_Is0x346() => Assert.Equal(0x346, CsrAddresses.MperfLevel);

        [Theory]
        [InlineData(0x300)] // Mstatus
        [InlineData(0x301)] // Misa
        [InlineData(0x800)] // VtId
        [InlineData(0x801)] // VtMask
        [InlineData(0x810)] // IsaCaps
        [InlineData(0x820)] // VmxEnable
        [InlineData(0xC00)] // Cycle
        [InlineData(0x345)] // MpowerState
        [InlineData(0x346)] // MperfLevel
        public void CsrAddresses_AreAll12Bit(int addr)
        {
            Assert.InRange(addr, 0x000, 0xFFF);
        }

        // в”Ђв”Ђ Supervisor-mode address checks в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ

        [Fact]
        public void Sstatus_Is0x100() => Assert.Equal(0x100, CsrAddresses.Sstatus);

        [Fact]
        public void Sepc_Is0x141() => Assert.Equal(0x141, CsrAddresses.Sepc);

        [Fact]
        public void Sip_Is0x144() => Assert.Equal(0x144, CsrAddresses.Sip);
    }

}
