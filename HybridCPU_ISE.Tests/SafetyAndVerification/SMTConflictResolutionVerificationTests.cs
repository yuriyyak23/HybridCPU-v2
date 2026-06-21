using Xunit;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Core;
using System;

namespace HybridCPU_ISE.Tests.SafetyAndVerification
{
    /// <summary>
    /// Comprehensive verification tests for SMT/VT conflict resolution correctness.
    ///
    /// Verifies three critical requirements:
    /// 1. Bit Position Verification (SafetyMask128 + ResourceMaskBuilder)
    /// 2. 4-Way SMT Conflict Semantics (BundleResourceCertificate4Way)
    /// 3. Domain Tag Verification (DomainTag + SafetyVerifier)
    ///
    /// These tests ensure the implementation correctly follows the original slot format
    /// and VT/SMT conflict resolution pattern.
    /// </summary>
    public class SMTConflictResolutionVerificationTests
    {
        private static ScalarALUMicroOp CreateScalarRegisterMicroOp(
            int virtualThreadId,
            ushort? readRegister = null,
            ushort? writeRegister = null,
            ulong structuralLowMask = 0,
            ulong domainTag = 0)
        {
            var microOp = new ScalarALUMicroOp
            {
                VirtualThreadId = virtualThreadId,
                Placement = SlotPlacementMetadata.Default with { DomainTag = domainTag },
                Src1RegID = readRegister ?? VLIW_Instruction.NoReg,
                Src2RegID = VLIW_Instruction.NoReg,
                UsesImmediate = true,
                DestRegID = writeRegister ?? VLIW_Instruction.NoReg,
                WritesRegister = writeRegister.HasValue,
                SafetyMask = new SafetyMask128(structuralLowMask, 0)
            };

            microOp.InitializeMetadata();
            return microOp;
        }

        #region 1. Bit Position Verification (SafetyMask128 + ResourceMaskBuilder)

        /// <summary>
        /// Requirement 1.1: Register Read Groups occupy bits 0-15 (Low component)
        /// </summary>
        [Fact]
        public void BitPosition_RegisterReadGroups_Bits0_15()
        {
            // Test first read group (registers 0-3)
            var mask0 = ResourceMaskBuilder.ForRegisterRead(0);
            Assert.Equal(1UL << 0, mask0.Low);
            Assert.Equal(0UL, mask0.High);

            // Test last read group (registers 60-63)
            var mask15 = ResourceMaskBuilder.ForRegisterRead(60);
            Assert.Equal(1UL << 15, mask15.Low);
            Assert.Equal(0UL, mask15.High);

            // Verify middle group (registers 20-23)
            var mask5 = ResourceMaskBuilder.ForRegisterRead(20);
            Assert.Equal(1UL << 5, mask5.Low);
            Assert.Equal(0UL, mask5.High);
        }

        /// <summary>
        /// Requirement 1.2: Register Write Groups occupy bits 16-31 (Low component)
        /// </summary>
        [Fact]
        public void BitPosition_RegisterWriteGroups_Bits16_31()
        {
            // Test first write group (registers 0-3)
            var mask0 = ResourceMaskBuilder.ForRegisterWrite(0);
            Assert.Equal(1UL << 16, mask0.Low);
            Assert.Equal(0UL, mask0.High);

            // Test last write group (registers 60-63)
            var mask15 = ResourceMaskBuilder.ForRegisterWrite(60);
            Assert.Equal(1UL << 31, mask15.Low);
            Assert.Equal(0UL, mask15.High);

            // Verify middle group (registers 20-23)
            var mask5 = ResourceMaskBuilder.ForRegisterWrite(20);
            Assert.Equal(1UL << 21, mask5.Low);
            Assert.Equal(0UL, mask5.High);
        }

        /// <summary>
        /// Requirement 1.3: Memory Domain Locks occupy bits 32-47 (Low component)
        /// Note: These are domain LOCKS, not Singularity Domain ID tags!
        /// </summary>
        [Fact]
        public void BitPosition_MemoryDomainLocks_Bits32_47()
        {
            // Test first domain (domain 0)
            var mask0 = ResourceMaskBuilder.ForMemoryDomain(0);
            Assert.Equal(1UL << 32, mask0.Low);
            Assert.Equal(0UL, mask0.High);

            // Test last domain (domain 15)
            var mask15 = ResourceMaskBuilder.ForMemoryDomain(15);
            Assert.Equal(1UL << 47, mask15.Low);
            Assert.Equal(0UL, mask15.High);

            // Verify middle domain (domain 8)
            var mask8 = ResourceMaskBuilder.ForMemoryDomain(8);
            Assert.Equal(1UL << 40, mask8.Low);
            Assert.Equal(0UL, mask8.High);
        }

        /// <summary>
        /// Requirement 1.4: LSU Ports occupy bits 48 (Load), 49 (Store), 50 (Atomic)
        /// </summary>
        [Fact]
        public void BitPosition_LSUPorts_Bits48_49_50()
        {
            // Bit 48: Load operation
            var loadMask = ResourceMaskBuilder.ForLoad();
            Assert.Equal(1UL << 48, loadMask.Low);
            Assert.Equal(0UL, loadMask.High);

            // Bit 49: Store operation
            var storeMask = ResourceMaskBuilder.ForStore();
            Assert.Equal(1UL << 49, storeMask.Low);
            Assert.Equal(0UL, storeMask.High);

            // Bit 50: Atomic operation
            var atomicMask = ResourceMaskBuilder.ForAtomic();
            Assert.Equal(1UL << 50, atomicMask.Low);
            Assert.Equal(0UL, atomicMask.High);
        }

        /// <summary>
        /// Requirement 1.5: DMA Channels occupy bits 51-54 (4 channels)
        /// </summary>
        [Fact]
        public void BitPosition_DMAChannels_Bits51_54()
        {
            // Channel 0: Bit 51
            var dma0 = ResourceMaskBuilder.ForDMAChannel(0);
            Assert.Equal(1UL << 51, dma0.Low);
            Assert.Equal(0UL, dma0.High);

            // Channel 3: Bit 54
            var dma3 = ResourceMaskBuilder.ForDMAChannel(3);
            Assert.Equal(1UL << 54, dma3.Low);
            Assert.Equal(0UL, dma3.High);

            // Channel 1: Bit 52
            var dma1 = ResourceMaskBuilder.ForDMAChannel(1);
            Assert.Equal(1UL << 52, dma1.Low);
            Assert.Equal(0UL, dma1.High);
        }

        /// <summary>
        /// Requirement 1.6: Stream Engines occupy bits 55-58 (4 engines)
        /// </summary>
        [Fact]
        public void BitPosition_StreamEngines_Bits55_58()
        {
            // Engine 0: Bit 55
            var stream0 = ResourceMaskBuilder.ForStreamEngine(0);
            Assert.Equal(1UL << 55, stream0.Low);
            Assert.Equal(0UL, stream0.High);

            // Engine 3: Bit 58
            var stream3 = ResourceMaskBuilder.ForStreamEngine(3);
            Assert.Equal(1UL << 58, stream3.Low);
            Assert.Equal(0UL, stream3.High);

            // Engine 2: Bit 57
            var stream2 = ResourceMaskBuilder.ForStreamEngine(2);
            Assert.Equal(1UL << 57, stream2.Low);
            Assert.Equal(0UL, stream2.High);
        }

        /// <summary>
        /// Requirement 1.7: Accelerators occupy bits 59-62 (4 accelerators)
        /// </summary>
        [Fact]
        public void BitPosition_Accelerators_Bits59_62()
        {
            // Accelerator 0: Bit 59
            var accel0 = ResourceMaskBuilder.ForAccelerator(0);
            Assert.Equal(1UL << 59, accel0.Low);
            Assert.Equal(0UL, accel0.High);

            // Accelerator 3: Bit 62
            var accel3 = ResourceMaskBuilder.ForAccelerator(3);
            Assert.Equal(1UL << 62, accel3.Low);
            Assert.Equal(0UL, accel3.High);

            // Accelerator 1: Bit 60
            var accel1 = ResourceMaskBuilder.ForAccelerator(1);
            Assert.Equal(1UL << 60, accel1.Low);
            Assert.Equal(0UL, accel1.High);
        }

        #endregion

        #region 2. 4-Way SMT Conflict Semantics (BundleResourceCertificate4Way)

        /// <summary>
        /// Requirement 2.1: Register conflicts (bits 0-31) are checked ONLY within same VT
        /// VT0 and VT1 both writing to register group 5 should NOT conflict
        /// </summary>
        [Fact]
        public void SMTConflict_RegistersIsolatedPerVT_NoInterVTConflict()
        {
            var cert = new BundleResourceCertificate4Way();

            // VT-0 writes to register group 5 (bit 21 = 16 + 5)
            var vt0_op = CreateScalarRegisterMicroOp(
                virtualThreadId: 0,
                writeRegister: 20);

            // VT-1 also writes to register group 5 (same bit 21)
            var vt1_op = CreateScalarRegisterMicroOp(
                virtualThreadId: 1,
                writeRegister: 20);

            // Both operations should be injectable (no conflict across VTs)
            Assert.True(cert.CanInject(vt0_op));
            cert.AddOperation(vt0_op);

            // VT-1 operation should NOT conflict with VT-0's register usage
            Assert.True(cert.CanInject(vt1_op));
            cert.AddOperation(vt1_op);

            // Verify: RegMaskVT0 has bit 21 set, RegMaskVT1 has bit 21 set
            Assert.Equal(1u << 21, cert.RegMaskVT0);
            Assert.Equal(1u << 21, cert.RegMaskVT1);
        }

        /// <summary>
        /// Requirement 2.2: Register conflicts within same VT DO conflict
        /// VT0 writing to register group 5 twice should conflict
        /// </summary>
        [Fact]
        public void SMTConflict_RegistersConflictWithinSameVT()
        {
            var cert = new BundleResourceCertificate4Way();

            // VT-0 first operation: writes to register group 5
            var vt0_op1 = CreateScalarRegisterMicroOp(
                virtualThreadId: 0,
                writeRegister: 20);

            // VT-0 second operation: also writes to register group 5
            var vt0_op2 = CreateScalarRegisterMicroOp(
                virtualThreadId: 0,
                writeRegister: 20);

            // First operation should inject successfully
            Assert.True(cert.CanInject(vt0_op1));
            cert.AddOperation(vt0_op1);

            // Second operation from SAME VT should conflict
            Assert.False(cert.CanInject(vt0_op2));
        }

        /// <summary>
        /// Requirement 2.3: Shared resource collisions (bits 32-127) DO conflict across any threads
        /// VT0 and VT1 requesting DMA channel 1 (bit 52) should conflict
        /// </summary>
        [Fact]
        public void SMTConflict_SharedResourcesConflictAcrossAllVTs_DMAChannel()
        {
            var cert = new BundleResourceCertificate4Way();

            // VT-0 requests DMA channel 1 (bit 52)
            var vt0_op = new ScalarALUMicroOp
            {
                VirtualThreadId = 0,
                SafetyMask = new SafetyMask128(1UL << 52, 0)  // DMA channel 1
            };

            // VT-1 also requests DMA channel 1 (bit 52)
            var vt1_op = new ScalarALUMicroOp
            {
                VirtualThreadId = 1,
                SafetyMask = new SafetyMask128(1UL << 52, 0)  // DMA channel 1
            };

            // VT-0 operation should inject successfully
            Assert.True(cert.CanInject(vt0_op));
            cert.AddOperation(vt0_op);

            // VT-1 operation should CONFLICT (shared resource across VTs)
            Assert.False(cert.CanInject(vt1_op));
        }

        /// <summary>
        /// Requirement 2.4: Shared resources (memory domains, LSU, accelerators) conflict globally
        /// </summary>
        [Fact]
        public void SMTConflict_SharedResourcesConflictAcrossAllVTs_MemoryDomain()
        {
            var cert = new BundleResourceCertificate4Way();

            // VT-2 accesses memory domain 3 (bit 35 = 32 + 3)
            var vt2_op = new ScalarALUMicroOp
            {
                VirtualThreadId = 2,
                SafetyMask = new SafetyMask128(1UL << 35, 0)  // Memory domain 3
            };

            // VT-3 also accesses memory domain 3
            var vt3_op = new ScalarALUMicroOp
            {
                VirtualThreadId = 3,
                SafetyMask = new SafetyMask128(1UL << 35, 0)  // Memory domain 3
            };

            // VT-2 operation should inject successfully
            Assert.True(cert.CanInject(vt2_op));
            cert.AddOperation(vt2_op);

            // VT-3 operation should CONFLICT (shared memory domain)
            Assert.False(cert.CanInject(vt3_op));
        }

        /// <summary>
        /// Requirement 2.5: Mixed scenario - registers isolated, shared resources conflict
        /// </summary>
        [Fact]
        public void SMTConflict_MixedScenario_RegistersIsolated_SharedResourcesConflict()
        {
            var cert = new BundleResourceCertificate4Way();

            // VT-0: Write register group 0, use LSU Load (bit 48)
            var vt0_op = CreateScalarRegisterMicroOp(
                virtualThreadId: 0,
                writeRegister: 0,
                structuralLowMask: 1UL << 48);

            // VT-1: Write register group 0 (should NOT conflict), use LSU Load (should conflict)
            var vt1_op = CreateScalarRegisterMicroOp(
                virtualThreadId: 1,
                writeRegister: 0,
                structuralLowMask: 1UL << 48);

            // VT-2: Write register group 0 (should NOT conflict), use LSU Store (should NOT conflict)
            var vt2_op = CreateScalarRegisterMicroOp(
                virtualThreadId: 2,
                writeRegister: 0,
                structuralLowMask: 1UL << 49);

            // VT-0 injects successfully
            Assert.True(cert.CanInject(vt0_op));
            cert.AddOperation(vt0_op);

            // VT-1 should CONFLICT due to LSU Load (bit 48), not due to register
            Assert.False(cert.CanInject(vt1_op));

            // VT-2 should inject successfully (different LSU port, register isolated)
            Assert.True(cert.CanInject(vt2_op));
            cert.AddOperation(vt2_op);

            // Verify register masks are isolated
            Assert.Equal(1u << 16, cert.RegMaskVT0);
            Assert.Equal(0u, cert.RegMaskVT1);  // Never injected
            Assert.Equal(1u << 16, cert.RegMaskVT2);
        }

        #endregion

        #region 3. Domain Tag Verification (DomainTag + SafetyVerifier)

        /// <summary>
        /// Requirement 3.1: DomainTag is a separate 64-bit property on MicroOp
        /// It behaves differently from SafetyMask128
        /// </summary>
        [Fact]
        public void DomainTag_IsSeparate64BitProperty_NotPartOfSafetyMask()
        {
            var microOp = new ScalarALUMicroOp
            {
                SafetyMask = new SafetyMask128(0x123456789ABCDEF0UL, 0xFEDCBA9876543210UL),
                Placement = SlotPlacementMetadata.Default with { DomainTag = 0xDEADBEEFCAFEBABEUL }
            };

            // DomainTag is independent of SafetyMask
            Assert.Equal(0x123456789ABCDEF0UL, microOp.SafetyMask.Low);
            Assert.Equal(0xFEDCBA9876543210UL, microOp.SafetyMask.High);
            Assert.Equal(0xDEADBEEFCAFEBABEUL, microOp.Placement.DomainTag);

            // Changing DomainTag doesn't affect SafetyMask
            microOp.Placement = microOp.Placement with { DomainTag = 0x1234567890ABCDEFUL };
            Assert.Equal(0x123456789ABCDEF0UL, microOp.SafetyMask.Low);
            Assert.Equal(0xFEDCBA9876543210UL, microOp.SafetyMask.High);
            Assert.Equal(0x1234567890ABCDEFUL, microOp.Placement.DomainTag);
        }

        /// <summary>
        /// Requirement 3.2: VerifyDomainCertificate checks (DomainTag & podDomainCert)
        /// This is a credential access check, NOT an exclusive resource lock
        /// </summary>
        [Fact]
        public void DomainTag_VerifyDomainCertificate_CredentialAccessCheck()
        {
            var verifier = new SafetyVerifier();

            // Operation with DomainTag = 0b0101 (bits 0 and 2)
            var microOp = new ScalarALUMicroOp
            {
                Placement = SlotPlacementMetadata.Default with { DomainTag = 0b0101UL }
            };

            // Pod certificate = 0b0111 (bits 0, 1, 2) - allows domains 0, 1, 2
            ulong podCert1 = 0b0111UL;

            // Pod certificate = 0b1010 (bits 1, 3) - allows domains 1, 3
            ulong podCert2 = 0b1010UL;

            // (0b0101 & 0b0111) = 0b0101 != 0 → ALLOWED
            Assert.True(verifier.VerifyDomainCertificate(microOp, podCert1));

            // (0b0101 & 0b1010) = 0b0000 = 0 → REJECTED
            Assert.False(verifier.VerifyDomainCertificate(microOp, podCert2));
        }

        /// <summary>
        /// Requirement 3.3: DomainTag 0 in user-mode (podDomainCert != 0) is deny-all.
        /// DomainTag 0 is only permitted when domain enforcement is not configured (podDomainCert == 0).
        /// This prevents user-mode code from emitting DomainTag=0 to bypass domain isolation.
        /// </summary>
        [Fact]
        public void DomainTag_Zero_DenyAllInUserMode_AllowedOnlyWhenNoDomainEnforcement()
        {
            var verifier = new SafetyVerifier();

            // Kernel operation with DomainTag = 0
            var kernelOp = new ScalarALUMicroOp
            {
                Placement = SlotPlacementMetadata.Default with { DomainTag = 0 }
            };

            // Allowed when no domain enforcement configured (podDomainCert == 0)
            Assert.True(verifier.VerifyDomainCertificate(kernelOp, 0x0000UL));

            // Denied in user-mode (podDomainCert != 0) — deny-all semantics
            Assert.False(verifier.VerifyDomainCertificate(kernelOp, 0xFFFFUL));
            Assert.False(verifier.VerifyDomainCertificate(kernelOp, 0b1010UL));
        }

        /// <summary>
        /// Requirement 3.4: podDomainCert 0 = no domain enforcement
        /// </summary>
        [Fact]
        public void DomainTag_PodCertZero_NoDomainEnforcement_AlwaysAllowed()
        {
            var verifier = new SafetyVerifier();

            // User operation with DomainTag = 0b1111
            var userOp = new ScalarALUMicroOp
            {
                Placement = SlotPlacementMetadata.Default with { DomainTag = 0b1111UL }
            };

            // Pod certificate = 0 (no enforcement)
            Assert.True(verifier.VerifyDomainCertificate(userOp, 0));
        }

        /// <summary>
        /// Requirement 3.5: DomainTag is NOT an exclusive resource lock
        /// Multiple operations with same DomainTag can execute in parallel
        /// (unlike SafetyMask resource conflicts)
        /// </summary>
        [Fact]
        public void DomainTag_NotExclusiveLock_MultipleOpsWithSameDomainAllowed()
        {
            var verifier = new SafetyVerifier();
            var cert = new BundleResourceCertificate4Way();

            // Op1 with DomainTag = 0x01, no resource conflicts
            var op1 = CreateScalarRegisterMicroOp(
                virtualThreadId: 0,
                writeRegister: 0,
                domainTag: 0x01UL);

            // Op2 with SAME DomainTag = 0x01, different resources
            var op2 = CreateScalarRegisterMicroOp(
                virtualThreadId: 1,
                writeRegister: 4,
                domainTag: 0x01UL);

            ulong podCert = 0x01UL;  // Pod allows domain 0x01

            // Both operations pass domain verification
            Assert.True(verifier.VerifyDomainCertificate(op1, podCert));
            Assert.True(verifier.VerifyDomainCertificate(op2, podCert));

            // Both operations can inject into certificate (no conflict)
            Assert.True(cert.CanInject(op1));
            cert.AddOperation(op1);

            Assert.True(cert.CanInject(op2));
            cert.AddOperation(op2);

            // This demonstrates DomainTag is credential-based, not exclusive lock
        }

        /// <summary>
        /// Requirement 3.6: DomainTag vs Memory Domain Lock distinction
        /// DomainTag (MicroOp property) = credential check
        /// Memory Domain Lock (SafetyMask bits 32-47) = exclusive resource lock
        /// </summary>
        [Fact]
        public void DomainTag_VsMemoryDomainLock_DifferentSemantics()
        {
            var verifier = new SafetyVerifier();
            var cert = new BundleResourceCertificate4Way();

            // Op1: DomainTag=0x01 (credential), Memory Domain Lock 3 (bit 35)
            var op1 = new ScalarALUMicroOp
            {
                VirtualThreadId = 0,
                Placement = SlotPlacementMetadata.Default with { DomainTag = 0x01UL },
                SafetyMask = new SafetyMask128(1UL << 35, 0)  // Memory domain 3 lock
            };

            // Op2: Same DomainTag=0x01 (credential OK), Same Memory Domain Lock 3 (conflict!)
            var op2 = new ScalarALUMicroOp
            {
                VirtualThreadId = 1,
                Placement = SlotPlacementMetadata.Default with { DomainTag = 0x01UL },
                SafetyMask = new SafetyMask128(1UL << 35, 0)  // Memory domain 3 lock
            };

            ulong podCert = 0x01UL;

            // Both pass domain credential check
            Assert.True(verifier.VerifyDomainCertificate(op1, podCert));
            Assert.True(verifier.VerifyDomainCertificate(op2, podCert));

            // Op1 injects successfully
            Assert.True(cert.CanInject(op1));
            cert.AddOperation(op1);

            // Op2 FAILS due to Memory Domain Lock conflict (bits 32-47 are exclusive)
            Assert.False(cert.CanInject(op2));

            // Demonstrates: DomainTag (credential) != Memory Domain Lock (exclusive)
        }

        #endregion
    }
}
