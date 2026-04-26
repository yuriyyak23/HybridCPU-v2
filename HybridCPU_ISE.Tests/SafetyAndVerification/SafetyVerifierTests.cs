using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.Metadata;
using System.Collections.Generic;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Unit tests for SafetyVerifier - Formally Safe Packing (FSP) non-interference verification.
    /// Tests verify that the verifier correctly enforces the FSP non-interference contract:
    /// 1. State isolation (registers per thread)
    /// 2. Memory domain separation
    /// 3. Per-thread in-order commit
    /// 4. No cross-thread hazards (RAW/WAW/WAR)
    /// 5. Control/system ops non-stealable
    /// </summary>
    public class SafetyVerifierTests
    {
        /// <summary>
        /// Helper method to create a simple NOP micro-operation
        /// </summary>
        private MicroOp CreateNopMicroOp(int threadId = 0)
        {
            return new NopMicroOp
            {
                OpCode = 0,
                OwnerThreadId = threadId
            };
        }

        /// <summary>
        /// Helper method to create a scalar ALU micro-operation
        /// </summary>
        private MicroOp CreateScalarALUMicroOp(int threadId, ushort destReg, ushort src1Reg, ushort src2Reg)
        {
            var aluOp = new ScalarALUMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Addition,
                OwnerThreadId = threadId,
                DestRegID = destReg,
                Src1RegID = src1Reg,
                Src2RegID = src2Reg,
                WritesRegister = true
            };
            aluOp.InitializeMetadata();
            return aluOp;
        }

        [Fact]
        public void AdmissionMetadata_TracksProducerSideRegisterAndPlacementFacts()
        {
            var op = CreateScalarALUMicroOp(threadId: 1, destReg: 8, src1Reg: 4, src2Reg: 5);

            MicroOpAdmissionMetadata admission = op.AdmissionMetadata;

            Assert.True(admission.IsStealable);
            Assert.False(admission.IsControlFlow);
            Assert.True(admission.WritesRegister);
            Assert.Equal(SlotClass.AluClass, admission.Placement.RequiredSlotClass);
            Assert.Equal(new[] { 4, 5 }, admission.ReadRegisters);
            Assert.Equal(new[] { 8 }, admission.WriteRegisters);
            Assert.NotEqual(0U, admission.RegisterHazardMask);
        }

        [Fact]
        public void EvaluateInterCoreLegality_WhenAdmissionStructuralFastPathAllows_UsesAdmissionMetadataStructuralAuthority()
        {
            var verifier = new SafetyVerifier();
            var bundle = new MicroOp[8];
            bundle[0] = CreateLoadMicroOp(0, destReg: 1, address: 0x1000);
            bundle[0].SafetyMask = new SafetyMask128(1UL << 48, 0);
            bundle[0].RefreshAdmissionMetadata();

            var candidate = CreateStoreMicroOp(1, srcReg: 2, address: 0x2000);
            candidate.SafetyMask = new SafetyMask128(1UL << 49, 0);
            candidate.RefreshAdmissionMetadata();

            LegalityDecision decision = SafetyVerifierCompatibilityTestModel.EvaluateInterCoreLegalityDecision(
                verifier,
                bundle,
                candidate,
                bundleOwnerThreadId: 0,
                candidateOwnerThreadId: 1);

            Assert.True(decision.IsAllowed);
            Assert.Equal(LegalityAuthoritySource.AdmissionMetadataStructuralCheck, decision.AuthoritySource);
            Assert.Equal(RejectKind.None, decision.RejectKind);
        }

        [Fact]
        public void EvaluateInterCoreLegality_WhenAdmissionStructuralFastPathRejects_UsesAdmissionMetadataStructuralAuthority()
        {
            var verifier = new SafetyVerifier();
            var bundle = new MicroOp[8];
            bundle[0] = CreateLoadMicroOp(0, destReg: 1, address: 0x1000);
            bundle[0].SafetyMask = new SafetyMask128(1UL << 48, 0);
            bundle[0].RefreshAdmissionMetadata();

            var candidate = CreateLoadMicroOp(1, destReg: 2, address: 0x2000);
            candidate.SafetyMask = new SafetyMask128(1UL << 48, 0);
            candidate.RefreshAdmissionMetadata();

            LegalityDecision decision = SafetyVerifierCompatibilityTestModel.EvaluateInterCoreLegalityDecision(
                verifier,
                bundle,
                candidate,
                bundleOwnerThreadId: 0,
                candidateOwnerThreadId: 1);

            Assert.False(decision.IsAllowed);
            Assert.Equal(LegalityAuthoritySource.AdmissionMetadataStructuralCheck, decision.AuthoritySource);
            Assert.Equal(RejectKind.CrossLaneConflict, decision.RejectKind);
            Assert.Equal(CertificateRejectDetail.SharedResourceConflict, decision.CertificateDetail);
        }

        [Fact]
        public void EvaluateInterCoreLegality_WhenStructuralAdmissionMasksAreMissing_FallsBackToDetailedCompatibilityAuthority()
        {
            var verifier = new SafetyVerifier();
            var bundle = new MicroOp[8];
            bundle[0] = CreateScalarALUMicroOp(0, destReg: 1, src1Reg: 2, src2Reg: 3);
            bundle[0].SafetyMask = SafetyMask128.Zero;
            bundle[0].RefreshAdmissionMetadata();

            var candidate = CreateScalarALUMicroOp(1, destReg: 1, src1Reg: 4, src2Reg: 5);
            candidate.SafetyMask = SafetyMask128.Zero;
            candidate.RefreshAdmissionMetadata();

            LegalityDecision decision = SafetyVerifierCompatibilityTestModel.EvaluateInterCoreLegalityDecision(
                verifier,
                bundle,
                candidate,
                bundleOwnerThreadId: 0,
                candidateOwnerThreadId: 1);

            Assert.False(decision.IsAllowed);
            Assert.Equal(LegalityAuthoritySource.DetailedCompatibilityCheck, decision.AuthoritySource);
            Assert.Equal(RejectKind.CrossLaneConflict, decision.RejectKind);
            Assert.Equal(CertificateRejectDetail.RegisterGroupConflict, decision.CertificateDetail);
        }

        /// <summary>
        /// Helper method to create a load micro-operation
        /// </summary>
        private MicroOp CreateLoadMicroOp(int threadId, ushort destReg, ulong address)
        {
            var loadOp = new LoadMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Load,
                OwnerThreadId = threadId,
                DestRegID = destReg,
                Address = address,
                Size = 4
            };
            loadOp.InitializeMetadata();
            return loadOp;
        }

        /// <summary>
        /// Helper method to create a store micro-operation
        /// </summary>
        private MicroOp CreateStoreMicroOp(int threadId, ushort srcReg, ulong address)
        {
            var storeOp = new StoreMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Store,
                OwnerThreadId = threadId,
                SrcRegID = srcReg,
                Address = address,
                Value = 0,
                Size = 4
            };
            storeOp.InitializeMetadata();
            return storeOp;
        }

        /// <summary>
        /// Helper method to create an empty bundle
        /// </summary>
        private MicroOp[] CreateEmptyBundle(int threadId)
        {
            var bundle = new MicroOp[8];
            for (int i = 0; i < 8; i++)
            {
                bundle[i] = CreateNopMicroOp(threadId);
            }
            return bundle;
        }

        #region Constructor and Initialization Tests

        [Fact]
        public void Constructor_ShouldInitializeVerifier()
        {
            // Arrange & Act
            var verifier = new SafetyVerifier();

            // Assert
            Assert.NotNull(verifier);
        }

        #endregion

        #region VerifyInjection - Basic Validation Tests

        [Fact]
        public void VerifyInjection_WithNullBundle_ShouldReturnFalse()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var candidate = CreateScalarALUMicroOp(1, 1, 2, 3);

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjection(verifier, null!, 0, candidate, 0, 1);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VerifyInjection_WithInvalidBundleSize_ShouldReturnFalse()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var invalidBundle = new MicroOp[4]; // Wrong size
            var candidate = CreateScalarALUMicroOp(1, 1, 2, 3);

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjection(verifier, invalidBundle, 0, candidate, 0, 1);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VerifyInjection_WithInvalidTargetSlot_ShouldReturnFalse()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = CreateEmptyBundle(0);
            var candidate = CreateScalarALUMicroOp(1, 1, 2, 3);

            // Act & Assert
            Assert.False(SafetyVerifierCompatibilityTestModel.VerifyInjection(verifier, bundle, -1, candidate, 0, 1));
            Assert.False(SafetyVerifierCompatibilityTestModel.VerifyInjection(verifier, bundle, 8, candidate, 0, 1));
            Assert.False(SafetyVerifierCompatibilityTestModel.VerifyInjection(verifier, bundle, 100, candidate, 0, 1));
        }

        [Fact]
        public void VerifyInjection_WithNullCandidate_ShouldReturnFalse()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = CreateEmptyBundle(0);

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjection(verifier, bundle, 0, null!, 0, 1);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region VerifyInjection - Same Thread Tests

        [Fact]
        public void VerifyInjection_SameThread_ShouldAlwaysReturnTrue()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = CreateEmptyBundle(0);
            var candidate = CreateScalarALUMicroOp(0, 1, 2, 3);

            // Act - same thread (0 -> 0), no cross-thread interference
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjection(verifier, bundle, 0, candidate, 0, 0);

            // Assert - should be safe (same thread)
            Assert.True(result);
        }

        #endregion

        #region VerifyInjection - Register Dependency Tests (RAW/WAW/WAR Hazards)

        [Fact]
        public void VerifyInjection_NoRegisterConflict_ShouldReturnTrue()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = CreateEmptyBundle(0);

            // Bundle op uses registers 1, 2, 3
            bundle[0] = CreateScalarALUMicroOp(0, destReg: 1, src1Reg: 2, src2Reg: 3);

            // Candidate uses completely different registers (10, 11, 12)
            var candidate = CreateScalarALUMicroOp(1, destReg: 10, src1Reg: 11, src2Reg: 12);

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjection(verifier, bundle, 1, candidate, 0, 1);

            // Assert - no register conflict, should be safe
            Assert.True(result);
        }

        [Fact]
        public void VerifyInjection_WAWHazard_ShouldReturnFalse()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = CreateEmptyBundle(0);

            // Bundle op writes to register 1
            bundle[0] = CreateScalarALUMicroOp(0, destReg: 1, src1Reg: 2, src2Reg: 3);

            // Candidate also writes to register 1 (Write-After-Write hazard)
            var candidate = CreateScalarALUMicroOp(1, destReg: 1, src1Reg: 10, src2Reg: 11);

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjection(verifier, bundle, 1, candidate, 0, 1);

            // Assert - WAW hazard detected, should reject
            Assert.False(result);
        }

        [Fact]
        public void VerifyInjection_WhenLiveSafetyMasksAreCleared_StillRejectsRegisterHazard()
        {
            var verifier = new SafetyVerifier();
            var bundle = CreateEmptyBundle(0);

            bundle[0] = CreateScalarALUMicroOp(0, destReg: 1, src1Reg: 2, src2Reg: 3);
            bundle[0].SafetyMask = SafetyMask128.Zero;
            bundle[0].RefreshAdmissionMetadata();

            var candidate = CreateScalarALUMicroOp(1, destReg: 1, src1Reg: 10, src2Reg: 11);
            candidate.SafetyMask = SafetyMask128.Zero;
            candidate.RefreshAdmissionMetadata();

            Assert.False(SafetyVerifierCompatibilityTestModel.VerifyInjection(verifier, bundle, 1, candidate, 0, 1));
        }

        [Fact]
        public void VerifyInjection_RAWHazard_ShouldReturnFalse()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = CreateEmptyBundle(0);

            // Bundle op reads from register 10
            bundle[0] = CreateScalarALUMicroOp(0, destReg: 1, src1Reg: 10, src2Reg: 3);

            // Candidate writes to register 10 (Read-After-Write hazard)
            var candidate = CreateScalarALUMicroOp(1, destReg: 10, src1Reg: 20, src2Reg: 21);

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjection(verifier, bundle, 1, candidate, 0, 1);

            // Assert - RAW hazard detected, should reject
            Assert.False(result);
        }

        [Fact]
        public void VerifyInjection_WARHazard_ShouldReturnFalse()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = CreateEmptyBundle(0);

            // Bundle op writes to register 10
            bundle[0] = CreateScalarALUMicroOp(0, destReg: 10, src1Reg: 2, src2Reg: 3);

            // Candidate reads from register 10 (Write-After-Read hazard)
            var candidate = CreateScalarALUMicroOp(1, destReg: 1, src1Reg: 10, src2Reg: 11);

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjection(verifier, bundle, 1, candidate, 0, 1);

            // Assert - WAR hazard detected, should reject
            Assert.False(result);
        }

        [Fact]
        public void VerifyInjection_MultipleOpsNoConflict_ShouldReturnTrue()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = CreateEmptyBundle(0);

            // Multiple operations in bundle, all with different registers
            bundle[0] = CreateScalarALUMicroOp(0, destReg: 1, src1Reg: 2, src2Reg: 3);
            bundle[1] = CreateScalarALUMicroOp(0, destReg: 4, src1Reg: 5, src2Reg: 6);
            bundle[2] = CreateScalarALUMicroOp(0, destReg: 7, src1Reg: 8, src2Reg: 9);

            // Candidate uses completely different register set
            var candidate = CreateScalarALUMicroOp(1, destReg: 20, src1Reg: 21, src2Reg: 22);

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjection(verifier, bundle, 3, candidate, 0, 1);

            // Assert - no conflicts with any operation in bundle
            Assert.True(result);
        }

        #endregion

        #region VerifyInjection - Memory Dependency Tests

        [Fact]
        public void VerifyInjection_NoMemoryConflict_ShouldReturnTrue()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = CreateEmptyBundle(0);

            // Bundle has load from address 0x1000
            bundle[0] = CreateLoadMicroOp(0, destReg: 1, address: 0x1000);

            // Candidate loads from completely different address (0x5000)
            var candidate = CreateLoadMicroOp(1, destReg: 10, address: 0x5000);

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjection(verifier, bundle, 1, candidate, 0, 1);

            // Assert - different memory regions, should be safe
            Assert.True(result);
        }

        [Fact]
        public void VerifyInjection_MemoryConflict_LoadLoad_ShouldReturnTrue()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = CreateEmptyBundle(0);

            // Bundle has load from address 0x1000
            bundle[0] = CreateLoadMicroOp(0, destReg: 1, address: 0x1000);

            // Candidate also loads from same address (Load-Load is safe)
            var candidate = CreateLoadMicroOp(1, destReg: 10, address: 0x1000);

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjection(verifier, bundle, 1, candidate, 0, 1);

            // Assert - Load-Load is safe even with same address
            Assert.True(result);
        }

        [Fact]
        public void VerifyInjection_MemoryConflict_StoreStore_ShouldReturnFalse()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = CreateEmptyBundle(0);

            // Bundle has store to address 0x1000
            bundle[0] = CreateStoreMicroOp(0, srcReg: 1, address: 0x1000);

            // Candidate also stores to same address (Store-Store conflict)
            var candidate = CreateStoreMicroOp(1, srcReg: 10, address: 0x1000);

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjection(verifier, bundle, 1, candidate, 0, 1);

            // Assert - Store-Store to same address is unsafe
            Assert.False(result);
        }

        [Fact]
        public void VerifyInjection_MemoryNoOps_ShouldReturnTrue()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = CreateEmptyBundle(0);

            // Bundle has compute operation (no memory access)
            bundle[0] = CreateScalarALUMicroOp(0, destReg: 1, src1Reg: 2, src2Reg: 3);

            // Candidate also compute operation (no memory access)
            var candidate = CreateScalarALUMicroOp(1, destReg: 10, src1Reg: 11, src2Reg: 12);

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjection(verifier, bundle, 1, candidate, 0, 1);

            // Assert - no memory operations, no memory conflict
            Assert.True(result);
        }

        #endregion

        #region VerifyBundle Tests

        [Fact]
        public void VerifyBundle_WithNullBundle_ShouldReturnFalse()
        {
            // Arrange
            var verifier = new SafetyVerifier();

            // Act
            bool result = verifier.VerifyBundle(null!);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VerifyBundle_WithInvalidBundleSize_ShouldReturnFalse()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var invalidBundle = new MicroOp[4];

            // Act
            bool result = verifier.VerifyBundle(invalidBundle);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VerifyBundle_WithNoConflicts_ShouldReturnTrue()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = CreateEmptyBundle(0);

            // All operations use different registers
            bundle[0] = CreateScalarALUMicroOp(0, destReg: 1, src1Reg: 2, src2Reg: 3);
            bundle[1] = CreateScalarALUMicroOp(1, destReg: 10, src1Reg: 11, src2Reg: 12);
            bundle[2] = CreateScalarALUMicroOp(2, destReg: 20, src1Reg: 21, src2Reg: 22);

            // Act
            bool result = verifier.VerifyBundle(bundle);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void VerifyBundle_WithRegisterConflicts_ShouldReturnFalse()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = CreateEmptyBundle(0);

            // Operations from different threads with register conflict
            bundle[0] = CreateScalarALUMicroOp(0, destReg: 1, src1Reg: 2, src2Reg: 3);
            bundle[1] = CreateScalarALUMicroOp(1, destReg: 1, src1Reg: 11, src2Reg: 12); // WAW hazard

            // Act
            bool result = verifier.VerifyBundle(bundle);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VerifyBundle_AllNops_ShouldReturnTrue()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = CreateEmptyBundle(0);

            // Act
            bool result = verifier.VerifyBundle(bundle);

            // Assert - all NOPs, no conflicts
            Assert.True(result);
        }

        [Fact]
        public void VerifyBundle_MixedThreadsSafeOps_ShouldReturnTrue()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = CreateEmptyBundle(0);

            // Different threads, no conflicts
            bundle[0] = CreateScalarALUMicroOp(0, destReg: 1, src1Reg: 2, src2Reg: 3);
            bundle[1] = CreateScalarALUMicroOp(1, destReg: 10, src1Reg: 11, src2Reg: 12);
            bundle[2] = CreateScalarALUMicroOp(2, destReg: 20, src1Reg: 21, src2Reg: 22);
            bundle[3] = CreateScalarALUMicroOp(3, destReg: 30, src1Reg: 31, src2Reg: 32);

            // Act
            bool result = verifier.VerifyBundle(bundle);

            // Assert - multiple threads but safe operations
            Assert.True(result);
        }

        #endregion
    }
}
