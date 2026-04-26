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
    public sealed class CsrCompletenessTests
    {
        [Fact]
        public void AllPlanAddresses_AreRegistered()
        {
            var csr = new CsrFile();

            // Machine-mode
            Assert.True(csr.IsRegistered(CsrAddresses.Mstatus));
            Assert.True(csr.IsRegistered(CsrAddresses.Misa));
            Assert.True(csr.IsRegistered(CsrAddresses.Mie));
            Assert.True(csr.IsRegistered(CsrAddresses.Mtvec));
            Assert.True(csr.IsRegistered(CsrAddresses.Mscratch));
            Assert.True(csr.IsRegistered(CsrAddresses.Mepc));
            Assert.True(csr.IsRegistered(CsrAddresses.Mcause));
            Assert.True(csr.IsRegistered(CsrAddresses.Mtval));
            Assert.True(csr.IsRegistered(CsrAddresses.Mip));
            Assert.True(csr.IsRegistered(CsrAddresses.MpowerState));
            Assert.True(csr.IsRegistered(CsrAddresses.MperfLevel));

            // Supervisor-mode
            Assert.True(csr.IsRegistered(CsrAddresses.Sstatus));
            Assert.True(csr.IsRegistered(CsrAddresses.Sie));
            Assert.True(csr.IsRegistered(CsrAddresses.Stvec));
            Assert.True(csr.IsRegistered(CsrAddresses.Sscratch));
            Assert.True(csr.IsRegistered(CsrAddresses.Sepc));
            Assert.True(csr.IsRegistered(CsrAddresses.Scause));
            Assert.True(csr.IsRegistered(CsrAddresses.Stval));
            Assert.True(csr.IsRegistered(CsrAddresses.Sip));

            // VT
            Assert.True(csr.IsRegistered(CsrAddresses.VtId));
            Assert.True(csr.IsRegistered(CsrAddresses.VtMask));
            Assert.True(csr.IsRegistered(CsrAddresses.VtStatus));
            Assert.True(csr.IsRegistered(CsrAddresses.VtCause));

            // Capability
            Assert.True(csr.IsRegistered(CsrAddresses.IsaCaps));
            Assert.True(csr.IsRegistered(CsrAddresses.LaneCaps));
            Assert.True(csr.IsRegistered(CsrAddresses.BundleCaps));
            Assert.True(csr.IsRegistered(CsrAddresses.SafetyCaps));
            Assert.True(csr.IsRegistered(CsrAddresses.MemCaps));

            // VMX
            Assert.True(csr.IsRegistered(CsrAddresses.VmxEnable));
            Assert.True(csr.IsRegistered(CsrAddresses.VmxCaps));
            Assert.True(csr.IsRegistered(CsrAddresses.VmxControl));
            Assert.True(csr.IsRegistered(CsrAddresses.VmxExitReason));
            Assert.True(csr.IsRegistered(CsrAddresses.VmxExitQual));

            // Counters
            Assert.True(csr.IsRegistered(CsrAddresses.Cycle));
            Assert.True(csr.IsRegistered(CsrAddresses.BundleRet));
            Assert.True(csr.IsRegistered(CsrAddresses.InstrRet));
            Assert.True(csr.IsRegistered(CsrAddresses.VmExitCnt));
            Assert.True(csr.IsRegistered(CsrAddresses.BarrierCnt));
            Assert.True(csr.IsRegistered(CsrAddresses.StealCnt));
            Assert.True(csr.IsRegistered(CsrAddresses.ReplayCnt));
        }


    }

}
