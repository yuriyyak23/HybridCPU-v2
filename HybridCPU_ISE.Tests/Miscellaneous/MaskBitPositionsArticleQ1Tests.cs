using Xunit;
using YAKSys_Hybrid_CPU.Core;
using System;

namespace HybridCPU_ISE.Tests
{
    public class MaskBitPositionsArticleQ1Tests
    {
        [Fact]
        public void MaskBitPositions_ShouldMatchCodeArchitecture()
        {
            // Register Read Group (Reg 0 -> Group 0, Bits 0)
            var regReadMask = ResourceMaskBuilder.ForRegisterRead128(0);
            Assert.Equal(1UL << 0, regReadMask.Low);

            // Register Write Group (Reg 0 -> Group 0, Bits 16)
            var regWriteMask = ResourceMaskBuilder.ForRegisterWrite128(0);
            Assert.Equal(1UL << 16, regWriteMask.Low);

            // Memory Domain ID - Check that it acts as a lock on bit 32
            var memDomainMask = ResourceMaskBuilder.ForMemoryDomain128(0);
            Assert.Equal(1UL << 32, memDomainMask.Low);

            // LSU Ports
            var loadMask = ResourceMaskBuilder.ForLoad128();
            Assert.Equal(1UL << 48, loadMask.Low);

            var storeMask = ResourceMaskBuilder.ForStore128();
            Assert.Equal(1UL << 49, storeMask.Low);

            var atomicMask = ResourceMaskBuilder.ForAtomic128();
            Assert.Equal(1UL << 50, atomicMask.Low);

            // DMA Channels
            var dmaMask = ResourceMaskBuilder.ForDMAChannel128(0);
            Assert.Equal(1UL << 51, dmaMask.Low);

            // Stream Engines
            var streamMask = ResourceMaskBuilder.ForStreamEngine128(0);
            Assert.Equal(1UL << 55, streamMask.Low);

            // Accelerators
            var accelMask = ResourceMaskBuilder.ForAccelerator128(0);
            Assert.Equal(1UL << 59, accelMask.Low);
        }

        private class DummyMicroOp : MicroOp { public override bool Execute(ref YAKSys_Hybrid_CPU.Processor.CPU_Core core) { return true; } public override string GetDescription() => string.Empty; }

        [Fact]
        public void Certificate4Way_ShouldPreventCrossThreadSharedConflict_AllowCrossThreadRegConflict()
        {
            // Two ops from different VTs
            var opVT0 = new DummyMicroOp { VirtualThreadId = 0 };
            var opVT1 = new DummyMicroOp { VirtualThreadId = 1 };

            // VT0 wants to write to Reg 5 (Group 1) -> Bit 17
            opVT0.SafetyMask = ResourceMaskBuilder.ForRegisterWrite128(5);
            
            // VT1 ALSO wants to write to Reg 5 (Group 1) -> Bit 17
            opVT1.SafetyMask = ResourceMaskBuilder.ForRegisterWrite128(5);

            var cert = new BundleResourceCertificate4Way();
            // Init bundle with opVT0's claims
            cert.SharedMask = new SafetyMask128(opVT0.SafetyMask.Low & ~0xFFFF_FFFFUL, 0); // No shared bits
            cert.RegMaskVT0 = (uint)(opVT0.SafetyMask.Low & 0xFFFF_FFFFUL); // Reg claims for VT0

            // VT1 should NOT conflict, because VT1 checks against RegMaskVT1, which is 0!
            bool canInjectRegConflict = cert.CanInject(opVT1);
            Assert.True(canInjectRegConflict, "Different VTs with identical register IDs should NOT conflict.");

            // Now let's try a shared resource conflict (e.g., both want DMA Channel 0 -> Bit 51)
            opVT0.SafetyMask = ResourceMaskBuilder.ForDMAChannel128(0);
            opVT1.SafetyMask = ResourceMaskBuilder.ForDMAChannel128(0);

            // Init bundle with opVT0's claims
            cert.SharedMask = new SafetyMask128(opVT0.SafetyMask.Low & ~0xFFFF_FFFFUL, 0);
            
            bool canInjectSharedConflict = cert.CanInject(opVT1);
            Assert.False(canInjectSharedConflict, "Different VTs with identical shared resources SHOULD conflict.");
        }

        [Fact]
        public void VerifyDomainCertificate_ShouldUseSeparate64BitProperty()
        {
            var op = new DummyMicroOp();
            // DomainTag is a property of the class itself, not inside SafetyMask128!
            op.Placement = op.Placement with { DomainTag = 0x0000000000000005 }; // Domain 5
            
            var verifier = new SafetyVerifier();
            
            ulong validPodCert = 0x00000000000000FF; 
            bool isAllowed = verifier.VerifyDomainCertificate(op, validPodCert);
            Assert.True(isAllowed);

            ulong invalidPodCert = 0xFFFFFFFFFFFFFF00;
            bool isDenied = !verifier.VerifyDomainCertificate(op, invalidPodCert);
            Assert.True(isDenied);
        }
    }
}
