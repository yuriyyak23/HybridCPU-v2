using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;
using HybridCPU_ISE.Tests.TestHelpers;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Comprehensive tests for Refactoring Plan Point 1:
    /// Dynamic GRLB Memory Integration.
    ///
    /// Verifies the end-to-end integration chain:
    ///   MemorySubsystem.GetHardwareOccupancyMask128()
    ///     → ApplyFSPPacking (ResourceBitset conversion)
    ///       → MicroOpScheduler.PackBundle (SafetyMask128 forwarding)
    ///         → SafetyVerifier.VerifyInjectionFast128 (conflict detection)
    ///           → GRLB.GetEffectiveLocks / HasConflictWithHardware
    ///
    /// Also verifies:
    ///   - Stolen memory ops marked as IsSpeculative
    ///   - ProcessFaultedOperations restores OriginalResourceMask
    ///   - SuppressLsu dead-code removal (replaced by mask-based approach)
    /// </summary>
    public class DynamicGRLBMemoryIntegrationTests
    {
        #region GRLB.GetEffectiveLocks Tests

        [Fact]
        public void WhenNoHardwareOccupancyThenEffectiveLocksEqualsGRLB()
        {
            // Arrange
            var core = CreateCore();
            var mask = new ResourceBitset(1UL << 48, 0); // Load bit
            core.AcquireResources(mask);

            // Act
            var effective = core.GetEffectiveLocks(SafetyMask128.Zero);

            // Assert
            Assert.Equal(mask.Low, effective.Low);
            Assert.Equal(mask.High, effective.High);
        }

        [Fact]
        public void WhenHardwareOccupancySetThenEffectiveLocksContainsBothSources()
        {
            // Arrange
            var core = CreateCore();
            var grlbMask = new ResourceBitset(1UL << 48, 0); // Load bit in GRLB
            core.AcquireResources(grlbMask);

            // Hardware occupancy: Store bit (49) is overloaded
            var hwOccupancy = new SafetyMask128(1UL << 49, 0);

            // Act
            var effective = core.GetEffectiveLocks(hwOccupancy);

            // Assert — both Load (from GRLB) and Store (from hardware) must be set
            Assert.True((effective.Low & (1UL << 48)) != 0, "Load bit from GRLB should be set");
            Assert.True((effective.Low & (1UL << 49)) != 0, "Store bit from hardware occupancy should be set");
        }

        [Fact]
        public void WhenGRLBEmptyAndHardwareCongestedThenEffectiveLocksReflectsHardware()
        {
            // Arrange
            var core = CreateCore();
            // No GRLB locks acquired

            // Hardware: Load and Store channels both congested
            var hwOccupancy = ResourceMaskBuilder.ForLoad128() | ResourceMaskBuilder.ForStore128();

            // Act
            var effective = core.GetEffectiveLocks(hwOccupancy);

            // Assert
            Assert.Equal(hwOccupancy.Low, effective.Low);
            Assert.Equal(hwOccupancy.High, effective.High);
        }

        [Fact]
        public void WhenHighBitsOccupiedThenEffectiveLocksPreservesHighBits()
        {
            // Arrange
            var core = CreateCore();
            // Hardware: memory bank 0 congested (High bits)
            var hwOccupancy = ResourceMaskBuilder.ForMemoryBank128(0);

            // Act
            var effective = core.GetEffectiveLocks(hwOccupancy);

            // Assert
            Assert.True(effective.High != 0, "High bits from bank occupancy should be set");
            Assert.Equal(hwOccupancy.High, effective.High);
        }

        #endregion

        #region GRLB.HasConflictWithHardware Tests

        [Fact]
        public void WhenMaskConflictsWithGRLBOnlyThenConflictDetected()
        {
            // Arrange
            var core = CreateCore();
            var grlbMask = ResourceMaskBuilder.ForLoad();
            core.AcquireResources(grlbMask);

            var checkMask = ResourceMaskBuilder.ForLoad(); // Same resource

            // Act
            bool conflict = core.HasConflictWithHardware(checkMask, SafetyMask128.Zero);

            // Assert
            Assert.True(conflict, "Should detect conflict with GRLB-held Load bit");
        }

        [Fact]
        public void WhenMaskConflictsWithHardwareOnlyThenConflictDetected()
        {
            // Arrange
            var core = CreateCore();
            // No GRLB locks — Load channel is free in GRLB
            var hwOccupancy = ResourceMaskBuilder.ForLoad128(); // Hardware reports Load channel congested

            var checkMask = ResourceMaskBuilder.ForLoad();

            // Act
            bool conflict = core.HasConflictWithHardware(checkMask, hwOccupancy);

            // Assert
            Assert.True(conflict, "Should detect conflict with hardware-reported Load congestion");
        }

        [Fact]
        public void WhenNoConflictInEitherSourceThenNoConflict()
        {
            // Arrange
            var core = CreateCore();
            var grlbMask = ResourceMaskBuilder.ForLoad();
            core.AcquireResources(grlbMask);

            var hwOccupancy = ResourceMaskBuilder.ForStore128(); // Hardware: Store congested
            var checkMask = ResourceMaskBuilder.ForAtomic();     // Checking: Atomic (different resource)

            // Act
            bool conflict = core.HasConflictWithHardware(checkMask, hwOccupancy);

            // Assert
            Assert.False(conflict, "Atomic should not conflict with Load (GRLB) or Store (hardware)");
        }

        [Fact]
        public void WhenBankConflictsWithHardwareThenConflictDetected()
        {
            // Arrange
            var core = CreateCore();
            var hwOccupancy = ResourceMaskBuilder.ForMemoryBank128(3); // Bank 3 congested
            var checkMask = ResourceMaskBuilder.ForMemoryBank(3);      // Op targets bank 3

            // Act
            bool conflict = core.HasConflictWithHardware(checkMask, hwOccupancy);

            // Assert
            Assert.True(conflict, "Should detect conflict when op targets congested bank");
        }

        [Fact]
        public void WhenDifferentBankThenNoConflict()
        {
            // Arrange
            var core = CreateCore();
            var hwOccupancy = ResourceMaskBuilder.ForMemoryBank128(3); // Bank 3 congested
            var checkMask = ResourceMaskBuilder.ForMemoryBank(5);      // Op targets bank 5

            // Act
            bool conflict = core.HasConflictWithHardware(checkMask, hwOccupancy);

            // Assert
            Assert.False(conflict, "Bank 5 should not conflict with bank 3 congestion");
        }

        #endregion

        #region SafetyVerifier.VerifyInjectionFast128 with Hardware Mask Tests

        [Fact]
        public void WhenGlobalHardwareMaskConflictsThenInjectionRejected()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = CreateNopBundle(threadId: 0);

            // Candidate: Load op from thread 1 (uses Load bit)
            // InitializeMetadata() sets CanBeStolen = true for LoadMicroOp
            var candidate = MicroOpTestHelper.CreateLoad(virtualThreadId: 1, destReg: 20, address: 0x2000);

            // Global hardware mask: Load channel is overloaded
            var globalMask = ResourceMaskBuilder.ForLoad128();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate, globalMask);

            // Assert
            Assert.False(result, "Load op should be rejected when hardware Load channel is overloaded");
        }

        [Fact]
        public void WhenGlobalHardwareMaskHasNoConflictThenInjectionAllowed()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = CreateNopBundle(threadId: 0);

            // Candidate: ALU op from thread 1 (uses register groups only)
            var candidate = MicroOpTestHelper.CreateScalarALU(virtualThreadId: 1, destReg: 20, src1Reg: 21, src2Reg: 22);

            // Global hardware mask: Load channel overloaded (irrelevant for ALU op)
            var globalMask = ResourceMaskBuilder.ForLoad128();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate, globalMask);

            // Assert
            Assert.True(result, "ALU op should not be affected by Load channel congestion");
        }

        [Fact]
        public void WhenBundleConflictsButNoHardwareMaskThenInjectionRejected()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = new MicroOp[8];

            // Bundle slot 0: Load op from thread 0 (uses Load bit + reg group)
            bundle[0] = MicroOpTestHelper.CreateLoad(virtualThreadId: 0, destReg: 5, address: 0x1000);
            for (int i = 1; i < 8; i++)
                bundle[i] = MicroOpTestHelper.CreateNop(0);

            // Candidate: another Load op (uses same Load bit)
            var candidate = MicroOpTestHelper.CreateLoad(virtualThreadId: 1, destReg: 20, address: 0x2000);

            // No hardware mask
            var globalMask = SafetyMask128.Zero;

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate, globalMask);

            // Assert — both ops claim Load bit → conflict
            Assert.False(result, "Two Load ops in same bundle should conflict on LSU Load channel bit");
        }

        [Fact]
        public void WhenZeroCandidateMaskThenVerificationFails()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = CreateNopBundle(threadId: 0);

            var candidate = new NopMicroOp { SafetyMask = SafetyMask128.Zero };

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate, SafetyMask128.Zero);

            // Assert
            Assert.False(result, "Zero safety mask indicates uninitialized metadata — must fail");
        }

        #endregion

        #region SafetyVerifier.VerifyInjection Full Path with Hardware Mask Tests

        [Fact]
        public void WhenFullVerifyWithHardwareMaskConflictThenRejected()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = CreateNopBundle(threadId: 0);

            var candidate = MicroOpTestHelper.CreateLoad(virtualThreadId: 1, destReg: 20, address: 0x2000);
            candidate.OwnerThreadId = 1;

            var globalMask = ResourceMaskBuilder.ForLoad128();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjection(
                verifier,
                bundle,
                targetSlot: 0,
                candidate,
                bundleOwnerThreadId: 0,
                candidateOwnerThreadId: 1,
                globalHardwareMask: globalMask);

            // Assert
            Assert.False(result, "Full path should reject Load when hardware mask reports Load congestion");
        }

        [Fact]
        public void WhenFullVerifyWithDomainCertAndHardwareMaskConflictThenRejected()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = CreateNopBundle(threadId: 0);

            var candidate = MicroOpTestHelper.CreateLoad(virtualThreadId: 1, destReg: 20, address: 0x2000);
            candidate.OwnerThreadId = 1;
            candidate.Placement = candidate.Placement with { DomainTag = 0x01 }; // Compatible domain

            ulong podDomainCert = 0x01; // Domain certificate allows this tag
            var globalMask = ResourceMaskBuilder.ForLoad128(); // Load channel congested

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjection(
                verifier,
                bundle,
                targetSlot: 0,
                candidate,
                bundleOwnerThreadId: 0,
                candidateOwnerThreadId: 1,
                podDomainCert: podDomainCert,
                globalHardwareMask: globalMask);

            // Assert
            Assert.False(result, "Domain cert passes but hardware mask should reject");
        }

        [Fact]
        public void WhenFullVerifyWithOrthogonalOpsAndNoConflictThenAccepted()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = new MicroOp[8];

            // Slot 0: ALU op using registers 0-2
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 0, src1Reg: 1, src2Reg: 2);
            for (int i = 1; i < 8; i++)
                bundle[i] = MicroOpTestHelper.CreateNop(0);

            // Candidate: ALU op using registers 20-22 (different group, no overlap)
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            candidate.OwnerThreadId = 1;

            var globalMask = SafetyMask128.Zero; // No hardware congestion

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjection(
                verifier,
                bundle, targetSlot: 1, candidate,
                bundleOwnerThreadId: 0, candidateOwnerThreadId: 1,
                globalHardwareMask: globalMask);

            // Assert
            Assert.True(result, "Orthogonal register groups with no hardware congestion should be safe");
        }

        #endregion

        #region MicroOpScheduler.PackBundle Hardware Mask Forwarding Tests

        [Fact]
        public void WhenPackBundleWithHardwareMaskThenLoadOpsRejected()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var bundle = CreateNopBundle(threadId: 0);

            // Nominate a Load op from core 1
            var loadOp = MicroOpTestHelper.CreateLoad(virtualThreadId: 1, destReg: 20, address: 0x2000);
            loadOp.OwnerThreadId = 1;
            scheduler.Nominate(1, loadOp);

            // Hardware mask: Load channel congested
            var hwMask = ResourceMaskBuilder.ForLoad();

            // Act
            var packed = scheduler.PackBundle(
                originalBundle: bundle,
                currentThreadId: 0,
                stealEnabled: true,
                stealMask: 0xFF,
                globalResourceLocks: hwMask);

            // Assert — Load op should have been rejected
            bool foundInjected = false;
            foreach (var op in packed)
            {
                if (op != null && op.OwnerThreadId == 1 && op.IsMemoryOp)
                    foundInjected = true;
            }
            Assert.False(foundInjected, "Load op should be rejected when hardware mask reports Load channel congestion");
            Assert.True(scheduler.RejectedInjectionsCount > 0, "Should have recorded a rejection");
        }

        [Fact]
        public void WhenPackBundleWithNoHardwareMaskThenALUOpsInjected()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var bundle = CreateNopBundle(threadId: 0);

            // Nominate an ALU op from core 2 (orthogonal registers)
            var aluOp = MicroOpTestHelper.CreateScalarALU(virtualThreadId: 2, destReg: 24, src1Reg: 25, src2Reg: 26);
            aluOp.OwnerThreadId = 2;
            scheduler.Nominate(2, aluOp);

            // Act
            var packed = scheduler.PackBundle(
                originalBundle: bundle,
                currentThreadId: 0,
                stealEnabled: true,
                stealMask: 0xFF,
                globalResourceLocks: default);

            // Assert
            bool foundInjected = false;
            foreach (var op in packed)
            {
                if (op != null && op.OwnerThreadId == 2)
                    foundInjected = true;
            }
            Assert.True(foundInjected, "ALU op with no hardware conflicts should be injected");
            Assert.True(scheduler.SuccessfulInjectionsCount > 0, "Should have recorded a successful injection");
        }

        [Fact]
        public void WhenPackBundleWithStoreConflictThenStoreOpsRejected()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var bundle = CreateNopBundle(threadId: 0);

            var storeOp = MicroOpTestHelper.CreateStore(virtualThreadId: 1, srcReg: 20, address: 0x3000);
            storeOp.OwnerThreadId = 1;
            scheduler.Nominate(1, storeOp);

            // Hardware mask: Store channel congested
            var hwMask = ResourceMaskBuilder.ForStore();

            // Act
            var packed = scheduler.PackBundle(
                originalBundle: bundle,
                currentThreadId: 0,
                stealEnabled: true,
                stealMask: 0xFF,
                globalResourceLocks: hwMask);

            // Assert
            bool foundInjected = false;
            foreach (var op in packed)
            {
                if (op != null && op.OwnerThreadId == 1 && op.IsMemoryOp)
                    foundInjected = true;
            }
            Assert.False(foundInjected, "Store op should be rejected when hardware Store channel is congested");
        }

        [Fact]
        public void WhenPackBundleWithBankConflictThenBankOpsRejected()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var bundle = CreateNopBundle(threadId: 0);

            var loadOp = MicroOpTestHelper.CreateLoad(virtualThreadId: 1, destReg: 20, address: 0x2000);
            loadOp.OwnerThreadId = 1;
            // Add memory bank 2 to the safety mask
            loadOp.SafetyMask |= ResourceMaskBuilder.ForMemoryBank128(2);
            loadOp.RefreshAdmissionMetadata();
            scheduler.Nominate(1, loadOp);

            // Hardware mask: bank 2 is congested
            var hwMask = ResourceMaskBuilder.ForMemoryBank(2);

            // Act
            var packed = scheduler.PackBundle(
                originalBundle: bundle,
                currentThreadId: 0,
                stealEnabled: true,
                stealMask: 0xFF,
                globalResourceLocks: hwMask);

            // Assert
            bool foundInjected = false;
            foreach (var op in packed)
            {
                if (op != null && op.OwnerThreadId == 1)
                    foundInjected = true;
            }
            Assert.False(foundInjected, "Load targeting bank 2 should be rejected when bank 2 is congested");
        }

        #endregion

        #region Speculative Marking via PackBundle Tests

        [Fact]
        public void WhenCrossThreadMemoryOpStolenThenMarkedSpeculative()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var bundle = CreateNopBundle(threadId: 0);

            var loadOp = MicroOpTestHelper.CreateLoad(virtualThreadId: 2, destReg: 24, address: 0x5000);
            loadOp.OwnerThreadId = 2;
            scheduler.Nominate(2, loadOp);

            // Act
            var packed = scheduler.PackBundle(
                originalBundle: bundle,
                currentThreadId: 0,
                stealEnabled: true,
                stealMask: 0xFF,
                globalResourceLocks: default);

            // Assert
            MicroOp injected = null;
            foreach (var op in packed)
            {
                if (op != null && op.OwnerThreadId == 2 && op.IsMemoryOp)
                    injected = op;
            }
            Assert.NotNull(injected);
            Assert.True(injected.IsSpeculative, "Cross-thread stolen memory op must be marked speculative");
        }

        [Fact]
        public void WhenCrossThreadALUOpStolenThenNotMarkedSpeculative()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var bundle = CreateNopBundle(threadId: 0);

            var aluOp = MicroOpTestHelper.CreateScalarALU(virtualThreadId: 2, destReg: 24, src1Reg: 25, src2Reg: 26);
            aluOp.OwnerThreadId = 2;
            scheduler.Nominate(2, aluOp);

            // Act
            var packed = scheduler.PackBundle(
                originalBundle: bundle,
                currentThreadId: 0,
                stealEnabled: true,
                stealMask: 0xFF,
                globalResourceLocks: default);

            // Assert
            MicroOp injected = null;
            foreach (var op in packed)
            {
                if (op != null && op.OwnerThreadId == 2)
                    injected = op;
            }
            Assert.NotNull(injected);
            Assert.False(injected.IsSpeculative, "Non-memory ALU op should not be marked speculative");
        }

        [Fact]
        public void WhenSpeculativeStealThenCounterIncremented()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var bundle = CreateNopBundle(threadId: 0);

            var loadOp = MicroOpTestHelper.CreateLoad(virtualThreadId: 3, destReg: 28, address: 0x6000);
            loadOp.OwnerThreadId = 3;
            scheduler.Nominate(3, loadOp);

            // Act
            scheduler.PackBundle(bundle, 0, true, 0xFF, default);

            // Assert
            Assert.Equal(1L, scheduler.SuccessfulSpeculativeSteals);
        }

        #endregion

        #region ProcessFaultedOperations with OriginalResourceMask Tests

        [Fact]
        public void WhenFaultedOpHasOriginalMaskThenResourceMaskRestored()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var loadOp = MicroOpTestHelper.CreateLoad(virtualThreadId: 1, destReg: 5, address: 0x1000);
            var originalMask = new ResourceBitset(0xCAFEBABE, 0);
            var speculativeMask = new ResourceBitset(0xDEADBEEF, 0);

            loadOp.ResourceMask = speculativeMask;
            loadOp.OriginalResourceMask = originalMask;
            loadOp.IsSpeculative = true;
            loadOp.Faulted = true;

            var bundle = new MicroOp[8];
            bundle[0] = loadOp;

            // Act
            scheduler.ProcessFaultedOperations(bundle);

            // Assert
            Assert.Equal(originalMask.Low, loadOp.ResourceMask.Low);
            Assert.False(loadOp.IsSpeculative);
            Assert.False(loadOp.Faulted);
            Assert.Equal(0UL, loadOp.OriginalResourceMask.Low);
        }

        [Fact]
        public void WhenFaultedOpHasZeroOriginalMaskThenResourceMaskUnchanged()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var loadOp = MicroOpTestHelper.CreateLoad(virtualThreadId: 1, destReg: 5, address: 0x1000);
            var currentMask = new ResourceBitset(0x12345678, 0);

            loadOp.ResourceMask = currentMask;
            loadOp.OriginalResourceMask = ResourceBitset.Zero;
            loadOp.IsSpeculative = true;
            loadOp.Faulted = true;

            var bundle = new MicroOp[8];
            bundle[0] = loadOp;

            // Act
            scheduler.ProcessFaultedOperations(bundle);

            // Assert — ResourceMask should remain unchanged when OriginalResourceMask was zero
            Assert.Equal(currentMask.Low, loadOp.ResourceMask.Low);
            Assert.False(loadOp.IsSpeculative);
            Assert.False(loadOp.Faulted);
        }

        [Fact]
        public void WhenFaultedOperationProcessedThenFaultCounterIncremented()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var loadOp = MicroOpTestHelper.CreateLoad(virtualThreadId: 1, destReg: 5, address: 0x1000);
            loadOp.IsSpeculative = true;
            loadOp.Faulted = true;

            var bundle = new MicroOp[8];
            bundle[0] = loadOp;

            long before = scheduler.FaultedSpeculativeSteals;

            // Act
            scheduler.ProcessFaultedOperations(bundle);

            // Assert
            Assert.Equal(before + 1, scheduler.FaultedSpeculativeSteals);
        }

        [Fact]
        public void WhenMultipleFaultedOpsThenAllProcessed()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var bundle = new MicroOp[8];

            for (int i = 0; i < 3; i++)
            {
                var op = MicroOpTestHelper.CreateLoad(virtualThreadId: i + 1, destReg: (ushort)(10 + i), address: (ulong)(0x1000 + i * 0x100));
                op.IsSpeculative = true;
                op.Faulted = true;
                op.OriginalResourceMask = new ResourceBitset((ulong)(0xAA + i), 0);
                bundle[i] = op;
            }

            // Act
            scheduler.ProcessFaultedOperations(bundle);

            // Assert
            Assert.Equal(3L, scheduler.FaultedSpeculativeSteals);
            for (int i = 0; i < 3; i++)
            {
                Assert.False(bundle[i]!.IsSpeculative);
                Assert.False(bundle[i]!.Faulted);
            }
        }

        #endregion

        #region MemorySubsystem.GetHardwareOccupancyMask128 Tests

        [Fact]
        public void WhenNoQueuedRequestsThenHardwareMaskIsZero()
        {
            // Arrange
            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
            Processor proc = default;
            var memory = new MemorySubsystem(ref proc);

            // Act
            var mask = memory.GetHardwareOccupancyMask128();

            // Assert
            Assert.True(mask.IsZero, "No queued requests should produce zero hardware mask");
        }

        #endregion

        #region End-to-End: Hardware Occupancy Blocks FSP Injection

        [Fact]
        public void WhenEndToEndChannelOverloadedThenFSPLoadInjectionBlocked()
        {
            // This test simulates the full chain:
            // 1. Build a hardware occupancy mask with Load channel congested
            // 2. Pass it as globalResourceLocks to PackBundle
            // 3. Verify that a nominated Load op is rejected

            // Arrange
            var scheduler = new MicroOpScheduler();
            var bundle = CreateNopBundle(threadId: 0);

            var loadOp = MicroOpTestHelper.CreateLoad(virtualThreadId: 1, destReg: 20, address: 0x4000);
            loadOp.OwnerThreadId = 1;
            scheduler.Nominate(1, loadOp);

            // Simulate what ApplyFSPPacking does: query hardware mask and convert
            var hwSafetyMask = ResourceMaskBuilder.ForLoad128() | ResourceMaskBuilder.ForStore128();
            var hwResourceLocks = new ResourceBitset(hwSafetyMask.Low, hwSafetyMask.High);

            // Act
            var packed = scheduler.PackBundle(
                originalBundle: bundle,
                currentThreadId: 0,
                stealEnabled: true,
                stealMask: 0xFF,
                globalResourceLocks: hwResourceLocks);

            // Assert
            bool foundLoad = false;
            foreach (var op in packed)
            {
                if (op != null && op.OwnerThreadId == 1 && op.IsMemoryOp)
                    foundLoad = true;
            }
            Assert.False(foundLoad, "End-to-end: Load op must be blocked when memory channels are congested");
        }

        [Fact]
        public void WhenEndToEndChannelOverloadedThenALUStillAllowed()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var bundle = CreateNopBundle(threadId: 0);

            // Nominate ALU op (doesn't use Load/Store channels)
            var aluOp = MicroOpTestHelper.CreateScalarALU(virtualThreadId: 1, destReg: 28, src1Reg: 29, src2Reg: 30);
            aluOp.OwnerThreadId = 1;
            scheduler.Nominate(1, aluOp);

            // Hardware: all memory channels congested
            var hwSafetyMask = ResourceMaskBuilder.ForLoad128() | ResourceMaskBuilder.ForStore128();
            var hwResourceLocks = new ResourceBitset(hwSafetyMask.Low, hwSafetyMask.High);

            // Act
            var packed = scheduler.PackBundle(
                originalBundle: bundle,
                currentThreadId: 0,
                stealEnabled: true,
                stealMask: 0xFF,
                globalResourceLocks: hwResourceLocks);

            // Assert
            bool foundALU = false;
            foreach (var op in packed)
            {
                if (op != null && op.OwnerThreadId == 1)
                    foundALU = true;
            }
            Assert.True(foundALU, "End-to-end: ALU op should still be injectable despite memory channel congestion");
        }

        [Fact]
        public void WhenEndToEndMultipleBanksCongestedThenAllBankOpsBlocked()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var bundle = CreateNopBundle(threadId: 0);

            // Nominate a Load op targeting bank 1
            var loadOp = MicroOpTestHelper.CreateLoad(virtualThreadId: 1, destReg: 20, address: 0x2000);
            loadOp.OwnerThreadId = 1;
            loadOp.SafetyMask |= ResourceMaskBuilder.ForMemoryBank128(1);
            loadOp.RefreshAdmissionMetadata();
            scheduler.Nominate(1, loadOp);

            // Hardware: banks 0, 1, 2 are all congested
            var hwMask = ResourceMaskBuilder.ForMemoryBank(0)
                       | ResourceMaskBuilder.ForMemoryBank(1)
                       | ResourceMaskBuilder.ForMemoryBank(2);

            // Act
            var packed = scheduler.PackBundle(
                originalBundle: bundle,
                currentThreadId: 0,
                stealEnabled: true,
                stealMask: 0xFF,
                globalResourceLocks: hwMask);

            // Assert
            bool foundInjected = false;
            foreach (var op in packed)
            {
                if (op != null && op.OwnerThreadId == 1)
                    foundInjected = true;
            }
            Assert.False(foundInjected, "End-to-end: Load targeting bank 1 should be blocked when banks 0-2 are congested");
        }

        #endregion

        #region SuppressLsu Replacement Verification (mask-based approach)

        [Fact]
        public void WhenPackBundleIntraCoreSmt_SuppressLsuReplacedByMaskApproach()
        {
            // Verify that SuppressLsu still works at the intra-core SMT level
            // (it's a valid signal for PackBundleIntraCoreSmt, now checked BEFORE safety mask)
            var scheduler = new MicroOpScheduler();
            scheduler.SuppressLsu = true;

            // PackBundleIntraCoreSmt injects into null slots (not NOP objects)
            var bundle = new MicroOp[8];

            // Nominate a Load op via SMT port
            var loadOp = MicroOpTestHelper.CreateLoad(virtualThreadId: 1, destReg: 20, address: 0x4000);
            scheduler.NominateSmtCandidate(1, loadOp);

            // Act
            var result = scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            // Assert
            Assert.True(scheduler.MemoryWallSuppressionsCount > 0,
                "SuppressLsu should suppress LSU ops before safety mask check");

            bool foundLoad = false;
            foreach (var op in result)
            {
                if (op != null && op.IsMemoryOp && op.VirtualThreadId == 1)
                    foundLoad = true;
            }
            Assert.False(foundLoad, "Load should be suppressed by SuppressLsu=true");
        }

        [Fact]
        public void WhenPackBundleIntraCoreSmt_SuppressLsuDisabled_ThenALUAllowed()
        {
            // Verify ALU ops are not affected by SuppressLsu
            var scheduler = new MicroOpScheduler();
            scheduler.SuppressLsu = true;

            // PackBundleIntraCoreSmt injects into null slots
            var bundle = new MicroOp[8];

            // Nominate an ALU op via SMT port (not a memory op)
            var aluOp = MicroOpTestHelper.CreateScalarALU(virtualThreadId: 1, destReg: 28, src1Reg: 29, src2Reg: 30);
            scheduler.NominateSmtCandidate(1, aluOp);

            // Act
            var result = scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            // Assert — ALU op should pass even with SuppressLsu=true
            bool foundALU = false;
            foreach (var op in result)
            {
                if (op != null && op.VirtualThreadId == 1)
                    foundALU = true;
            }
            Assert.True(foundALU, "ALU op should not be affected by SuppressLsu");
        }

        #endregion

        #region Bit Layout Consistency Tests

        [Fact]
        public void WhenSafetyMask128AndResourceBitsetCreatedFromSameValuesThenLayoutMatches()
        {
            // Verify the zero-cost conversion assumption: SafetyMask128 and ResourceBitset
            // use identical (Low, High) bit layout
            ulong low = 0x1234567890ABCDEF;
            ulong high = 0xFEDCBA0987654321;

            var safetyMask = new SafetyMask128(low, high);
            var resourceBitset = new ResourceBitset(low, high);

            // Convert SafetyMask128 → ResourceBitset (as done in ApplyFSPPacking)
            var converted = new ResourceBitset(safetyMask.Low, safetyMask.High);

            Assert.Equal(resourceBitset.Low, converted.Low);
            Assert.Equal(resourceBitset.High, converted.High);
        }

        [Fact]
        public void WhenResourceBitsetConvertedToSafetyMask128ThenLayoutMatches()
        {
            // Verify the reverse conversion (as done in MicroOpScheduler.PackBundle)
            var resourceBitset = ResourceMaskBuilder.ForLoad() | ResourceMaskBuilder.ForStore();
            var safetyMask = new SafetyMask128(resourceBitset.Low, resourceBitset.High);

            Assert.Equal(resourceBitset.Low, safetyMask.Low);
            Assert.Equal(resourceBitset.High, safetyMask.High);
        }

        [Fact]
        public void WhenForMemoryBank128ReturnsHighBitsThenHWMaskMatchesGRLB()
        {
            // ForMemoryBank128 uses High bits (EXT_MEM_BANK_BASE = 48 in High ulong)
            // Verify this is consistent between SafetyMask and ResourceBitset
            for (int bankId = 0; bankId < 8; bankId++)
            {
                var safety = ResourceMaskBuilder.ForMemoryBank128(bankId);
                var resource = ResourceMaskBuilder.ForMemoryBank(bankId);

                Assert.Equal(0UL, safety.Low);   // Bank bits are in High
                Assert.Equal(0UL, resource.Low);  // Same for ResourceBitset
                Assert.Equal(resource.High, safety.High); // Identical bit position
                Assert.True(safety.High != 0, $"Bank {bankId} should produce non-zero High bits");
            }
        }

        #endregion

        #region Domain Certificate + Hardware Mask Combined Tests

        [Fact]
        public void WhenDomainCertPasses_ButHardwareMaskBlocks_ThenInjectionRejected()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var bundle = CreateNopBundle(threadId: 0);

            var loadOp = MicroOpTestHelper.CreateLoad(virtualThreadId: 1, destReg: 20, address: 0x2000);
            loadOp.OwnerThreadId = 1;
            loadOp.Placement = loadOp.Placement with { DomainTag = 0x04 }; // Valid domain tag
            scheduler.Nominate(1, loadOp);

            // Hardware: Load channel congested
            var hwMask = ResourceMaskBuilder.ForLoad();

            // Act — use domain certificate that matches the tag
            var packed = scheduler.PackBundle(
                originalBundle: bundle,
                currentThreadId: 0,
                stealEnabled: true,
                stealMask: 0xFF,
                globalResourceLocks: hwMask,
                domainCertificate: 0x0C); // Certificate allows domain 0x04

            // Assert
            bool foundInjected = false;
            foreach (var op in packed)
            {
                if (op != null && op.OwnerThreadId == 1)
                    foundInjected = true;
            }
            Assert.False(foundInjected,
                "Even though domain cert passes, hardware mask should still block Load injection");
        }

        [Fact]
        public void WhenDomainCertBlocks_ThenInjectionRejectedRegardlessOfHardwareMask()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var bundle = CreateNopBundle(threadId: 0);

            var aluOp = MicroOpTestHelper.CreateScalarALU(virtualThreadId: 1, destReg: 20, src1Reg: 21, src2Reg: 22);
            aluOp.OwnerThreadId = 1;
            aluOp.Placement = aluOp.Placement with { DomainTag = 0x08 }; // Domain tag
            scheduler.Nominate(1, aluOp);

            // Act — domain certificate does NOT match (0x01 & 0x08 = 0)
            var packed = scheduler.PackBundle(
                originalBundle: bundle,
                currentThreadId: 0,
                stealEnabled: true,
                stealMask: 0xFF,
                globalResourceLocks: default, // No hardware congestion
                domainCertificate: 0x01);      // Certificate blocks domain 0x08

            // Assert
            bool foundInjected = false;
            foreach (var op in packed)
            {
                if (op != null && op.OwnerThreadId == 1)
                    foundInjected = true;
            }
            Assert.False(foundInjected,
                "Domain certificate mismatch should block injection regardless of hardware mask");
        }

        #endregion

        #region Test Helpers

        /// <summary>
        /// Create a CPU_Core for GRLB testing.
        /// Uses constructor with coreId to properly initialize all arrays.
        /// </summary>
        private static Processor.CPU_Core CreateCore()
        {
            return new Processor.CPU_Core(0);
        }

        /// <summary>
        /// Create a VLIW bundle filled with NOPs for a given thread.
        /// NOPs use a minimal non-zero SafetyMask (reserved bit 63) so that
        /// SafetyVerifier takes the fast path and checks globalHardwareMask.
        /// </summary>
        private static MicroOp[] CreateNopBundle(int threadId)
        {
            // Bit 63 (reserved) is used as a sentinel to ensure non-zero mask
            // without claiming any real resource.
            var nopMask = new SafetyMask128(1UL << 63, 0);
            var bundle = new MicroOp[8];
            for (int i = 0; i < 8; i++)
            {
                bundle[i] = new NopMicroOp
                {
                    VirtualThreadId = threadId,
                    OwnerThreadId = threadId,
                    OpCode = 0,
                    ResourceMask = new ResourceBitset(0, 0),
                    SafetyMask = nopMask
                };
            }
            return bundle;
        }

        #endregion
    }
}
