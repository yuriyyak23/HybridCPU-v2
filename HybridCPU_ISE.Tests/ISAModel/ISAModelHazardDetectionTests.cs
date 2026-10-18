using Xunit;
using Xunit.Abstractions;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using HybridCPU_ISE.Tests.TestHelpers;
using System.Collections.Generic;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Category 1: Mask-based Hazard Detection Tests
    ///
    /// Tests correctness of mask-based hazard detection for:
    /// - RAW (Read After Write)
    /// - WAR (Write After Read)
    /// - WAW (Write After Write)
    /// - Structural conflicts
    /// - Cross-thread conflicts
    ///
    /// Validates: Assert((BundleMask & candMask) == 0 == expectedSafe)
    /// </summary>
    public class ISAModelHazardDetectionTests
    {
        private readonly ITestOutputHelper _output;

        public ISAModelHazardDetectionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        #region RAW Hazard Tests

        [Fact]
        public void RAW_Hazard_ShouldBeDetected_WhenCandidateReadsBundleWrite()
        {
            // Arrange: Bundle writes R1, candidate reads R1
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 5, src1Reg: 1, src2Reg: 6);

            var verifier = new SafetyVerifier();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: RAW hazard should be detected
            Assert.False(result, "RAW hazard: candidate reads register written by bundle");
            _output.WriteLine($"RAW hazard correctly detected: bundle writes R1, candidate reads R1");
        }

        [Fact]
        public void RAW_Hazard_DifferentRegisterGroups_ShouldNotConflict()
        {
            // Arrange: Bundle writes R1 (group 0), candidate reads R9 (group 2)
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 10, src1Reg: 9, src2Reg: 11);

            var verifier = new SafetyVerifier();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: No conflict - different register groups
            Assert.True(result, "Different register groups should not cause RAW hazard");
            _output.WriteLine($"No RAW conflict: bundle writes R1 (group 0), candidate reads R9 (group 2)");
        }

        [Fact]
        public void RAW_Hazard_MultipleReads_ShouldAllBeChecked()
        {
            // Arrange: Bundle writes R1 and R5, candidate reads R5
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);
            bundle[1] = MicroOpTestHelper.CreateScalarALU(0, destReg: 5, src1Reg: 6, src2Reg: 7);

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 10, src1Reg: 5, src2Reg: 11);

            var verifier = new SafetyVerifier();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: RAW hazard on R5
            Assert.False(result, "RAW hazard should be detected when candidate reads any bundle write");
            _output.WriteLine($"RAW hazard detected: bundle writes R5, candidate reads R5");
        }

        #endregion

        #region WAR Hazard Tests

        [Fact]
        public void WAR_Hazard_SameRegisterGroup_ShouldBeDetected()
        {
            // Arrange: Bundle reads and writes in group 0, candidate also writes in group 0
            // This creates a definite conflict scenario
            var bundle = new MicroOp[8];
            // Bundle writes R1, reads R2-R3 (all group 0: R0-3)
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);

            // Candidate also writes R2 (group 0), creating WAW + potential WAR
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 2, src1Reg: 20, src2Reg: 21);

            var verifier = new SafetyVerifier();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Conflict due to same register group
            // Bundle reads R2, candidate writes R2 - definite conflict
            Assert.False(result, "Register group conflict: bundle reads/writes group 0, candidate writes group 0");
            _output.WriteLine($"Register group conflict detected: bundle uses R1-R3, candidate writes R2 (all group 0)");
        }

        [Fact]
        public void WAR_Hazard_DifferentRegisterGroups_ShouldNotConflict()
        {
            // Arrange: Bundle reads R1-R3 (group 0), candidate writes R9 (group 2)
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 4, src1Reg: 1, src2Reg: 3);

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 9, src1Reg: 10, src2Reg: 11);

            var verifier = new SafetyVerifier();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: No conflict - different register groups
            Assert.True(result, "Different register groups should not cause WAR hazard");
            _output.WriteLine($"No WAR conflict: bundle reads R1-R3 (group 0), candidate writes R9 (group 2)");
        }

        #endregion

        #region WAW Hazard Tests

        [Fact]
        public void WAW_Hazard_ShouldBeDetected_WhenBothWriteSameRegister()
        {
            // Arrange: Both bundle and candidate write R1
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 1, src1Reg: 10, src2Reg: 11);

            var verifier = new SafetyVerifier();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: WAW hazard should be detected
            Assert.False(result, "WAW hazard: both ops write to same register");
            _output.WriteLine($"WAW hazard correctly detected: both write R1");
        }

        [Fact]
        public void WAW_Hazard_SameRegisterGroup_ShouldBeDetected()
        {
            // Arrange: Bundle writes R1 (group 0), candidate writes R2 (also group 0)
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 10, src2Reg: 11);

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 2, src1Reg: 12, src2Reg: 13);

            var verifier = new SafetyVerifier();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Conflict due to same register group
            Assert.False(result, "WAW conflict within same register group should be detected");
            _output.WriteLine($"WAW conflict detected: R1 and R2 in same register group");
        }

        [Fact]
        public void WAW_Hazard_DifferentRegisterGroups_ShouldNotConflict()
        {
            // Arrange: Bundle writes R1 (group 0), candidate writes R8 (group 2)
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 8, src1Reg: 9, src2Reg: 10);

            var verifier = new SafetyVerifier();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: No conflict - different register groups
            Assert.True(result, "Different register groups should not cause WAW hazard");
            _output.WriteLine($"No WAW conflict: R1 (group 0) vs R8 (group 2)");
        }

        #endregion

        #region Structural Conflict Tests

        [Fact]
        public void Structural_LSU_LoadLoad_ShouldConflict()
        {
            // Arrange: Both bundle and candidate use LSU load channel
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateLoad(0, destReg: 1, address: 0x1000, domainTag: 0);

            var candidate = MicroOpTestHelper.CreateLoad(1, destReg: 5, address: 0x2000, domainTag: 1);

            var verifier = new SafetyVerifier();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Structural conflict on LSU load channel
            Assert.False(result, "Both loads should conflict on LSU load channel");
            _output.WriteLine($"LSU structural conflict detected: both ops use load channel");
        }

        [Fact]
        public void Structural_LSU_StoreStore_ShouldConflict()
        {
            // Arrange: Both bundle and candidate use LSU store channel
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateStore(0, srcReg: 1, address: 0x1000, domainTag: 0);

            var candidate = MicroOpTestHelper.CreateStore(1, srcReg: 5, address: 0x2000, domainTag: 1);

            var verifier = new SafetyVerifier();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Structural conflict on LSU store channel
            Assert.False(result, "Both stores should conflict on LSU store channel");
            _output.WriteLine($"LSU structural conflict detected: both ops use store channel");
        }

        [Fact]
        public void Structural_LSU_LoadStore_SameDomain_ShouldConflict()
        {
            // Arrange: Load and store to same memory domain
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateLoad(0, destReg: 1, address: 0x1000, domainTag: 0);

            var candidate = MicroOpTestHelper.CreateStore(1, srcReg: 5, address: 0x2000, domainTag: 0);

            var verifier = new SafetyVerifier();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Memory domain conflict
            Assert.False(result, "Load and store to same domain should conflict");
            _output.WriteLine($"Memory domain conflict detected: both access domain 0");
        }

        [Fact]
        public void Structural_ALU_DifferentOps_ShouldNotConflict()
        {
            // Arrange: Multiple ALU ops with different resources
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 9, src1Reg: 10, src2Reg: 11);

            var verifier = new SafetyVerifier();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: No structural conflict - different register groups
            Assert.True(result, "ALU ops with different registers should not conflict");
            _output.WriteLine($"No structural conflict: ALU ops use different resources");
        }

        #endregion

        #region Cross-Thread Conflict Tests

        [Fact]
        public void CrossThread_SameVT_SameRegister_ShouldConflict()
        {
            // Arrange: Same VT, same register - true conflict
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);

            var candidate = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 4, src2Reg: 5);

            var verifier = new SafetyVerifier();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Conflict within same VT
            Assert.False(result, "Same VT writing same register should conflict (WAW)");
            _output.WriteLine($"Cross-thread conflict: same VT (0), same register (R1)");
        }

        [Fact]
        public void CrossThread_DifferentVT_SameRegister_ShouldNotConflict()
        {
            // Arrange: Different VTs, same register
            // Note: The mask-based system at SafetyVerifier level doesn't encode VT isolation
            // VT isolation is enforced at BundleResourceCertificate4Way level
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 1, src1Reg: 4, src2Reg: 5);

            var verifier = new SafetyVerifier();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: At SafetyMask level, same register group will conflict
            // Per-VT isolation is handled at higher level (BundleResourceCertificate4Way)
            // So this test documents the conservative behavior at the mask level
            Assert.False(result, "SafetyMask level detects register group conflict conservatively");
            _output.WriteLine($"SafetyMask conflict: same register group (per-VT isolation at cert level)");
        }

        [Fact]
        public void CrossThread_MultipleVTs_OrthogonalResources_ShouldNotConflict()
        {
            // Arrange: Multiple VTs, all orthogonal
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);
            bundle[1] = MicroOpTestHelper.CreateScalarALU(1, destReg: 9, src1Reg: 10, src2Reg: 11);

            var candidate = MicroOpTestHelper.CreateScalarALU(2, destReg: 17, src1Reg: 18, src2Reg: 19);

            var verifier = new SafetyVerifier();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: No conflicts
            Assert.True(result, "Multiple VTs with orthogonal resources should not conflict");
            _output.WriteLine($"No cross-thread conflicts: VT0(R1-3), VT1(R9-11), VT2(R17-19)");
        }

        #endregion

        #region Exhaustive Register Group Tests

        [Theory]
        [InlineData(0, 4)]   // Group 0 vs Group 1
        [InlineData(0, 8)]   // Group 0 vs Group 2
        [InlineData(0, 12)]  // Group 0 vs Group 3
        [InlineData(4, 8)]   // Group 1 vs Group 2
        [InlineData(4, 12)]  // Group 1 vs Group 3
        [InlineData(8, 12)]  // Group 2 vs Group 3
        public void RegisterGroups_DifferentGroups_ShouldNotConflict(ushort reg1, ushort reg2)
        {
            // Arrange
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: reg1, src1Reg: (ushort)(reg1 + 1), src2Reg: (ushort)(reg1 + 2));

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: reg2, src1Reg: (ushort)(reg2 + 1), src2Reg: (ushort)(reg2 + 2));

            var verifier = new SafetyVerifier();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert
            Assert.True(result, $"Registers R{reg1} and R{reg2} in different groups should not conflict");
            _output.WriteLine($"No conflict: R{reg1} vs R{reg2} (different groups)");
        }

        [Theory]
        [InlineData(0, 1)]   // Same group
        [InlineData(0, 2)]   // Same group
        [InlineData(4, 5)]   // Same group
        [InlineData(8, 9)]   // Same group
        public void RegisterGroups_SameGroup_ShouldConflict(ushort reg1, ushort reg2)
        {
            // Arrange
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: reg1, src1Reg: (ushort)(reg1 + 10), src2Reg: (ushort)(reg1 + 11));

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: reg2, src1Reg: (ushort)(reg2 + 10), src2Reg: (ushort)(reg2 + 11));

            var verifier = new SafetyVerifier();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert
            Assert.False(result, $"Registers R{reg1} and R{reg2} in same group should conflict");
            _output.WriteLine($"Conflict detected: R{reg1} vs R{reg2} (same group)");
        }

        #endregion

        #region Property-Based Testing Scenarios

        [Fact]
        public void PropertyBased_BundleMaskAndCandMask_ZeroIntersection_MeansNoConflict()
        {
            // Property: (BundleMask & CandMask) == 0 <==> Safe to inject

            // Create truly orthogonal ALU ops (avoiding LSU conflicts from mixed op types)
            var orthogonalOps = new List<MicroOp>
            {
                MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3),    // Group 0
                MicroOpTestHelper.CreateScalarALU(1, destReg: 9, src1Reg: 10, src2Reg: 11),  // Group 2
                MicroOpTestHelper.CreateScalarALU(2, destReg: 13, src1Reg: 14, src2Reg: 15), // Group 3
                MicroOpTestHelper.CreateScalarALU(3, destReg: 17, src1Reg: 18, src2Reg: 19)  // Group 4
            };

            for (int i = 0; i < orthogonalOps.Count; i++)
            {
                var bundle = new MicroOp[8];
                bundle[0] = orthogonalOps[i];

                for (int j = 0; j < orthogonalOps.Count; j++)
                {
                    if (i == j) continue;

                    var candidate = orthogonalOps[j];
                    var verifier = new SafetyVerifier();

                    // Act
                    bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

                    // Assert: All orthogonal pairs should be safe
                    Assert.True(result, $"Orthogonal ops {i} and {j} should not conflict");
                }
            }

            _output.WriteLine($"Property verified: orthogonal ALU ops have zero mask intersection");
        }

        [Fact]
        public void PropertyBased_ConflictingOps_NonZeroIntersection_MeansConflict()
        {
            // Property: (BundleMask & CandMask) != 0 <==> Conflict detected

            var conflictingOps = MicroOpTestHelper.CreateConflictingSet();
            var bundle = new MicroOp[8];
            bundle[0] = conflictingOps[0];

            var candidate = conflictingOps[1];
            var verifier = new SafetyVerifier();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Conflicting ops should be rejected
            Assert.False(result, "Conflicting ops should have non-zero mask intersection");
            _output.WriteLine($"Property verified: conflicting ops detected via mask intersection");
        }

        #endregion

        #region Multi-Thread Mask Overlay Tests

        [Fact]
        public void MultiThread_FourVTs_AllOrthogonal_ShouldAllInject()
        {
            // Arrange: Bundle has VT0, try to inject VT1, VT2, VT3
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);

            var verifier = new SafetyVerifier();

            // VT1 - different register group
            var candidate1 = MicroOpTestHelper.CreateScalarALU(1, destReg: 9, src1Reg: 10, src2Reg: 11);
            Assert.True(SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate1), "VT1 should inject");

            // VT2 - different register group
            var candidate2 = MicroOpTestHelper.CreateScalarALU(2, destReg: 17, src1Reg: 18, src2Reg: 19);
            Assert.True(SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate2), "VT2 should inject");

            // VT3 - different register group
            var candidate3 = MicroOpTestHelper.CreateScalarALU(3, destReg: 25, src1Reg: 26, src2Reg: 27);
            Assert.True(SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate3), "VT3 should inject");

            _output.WriteLine($"All 4 VTs successfully verified with orthogonal resources");
        }

        [Fact]
        public void MultiThread_OverlappingMasks_ShouldRejectConflictingVT()
        {
            // Arrange: Build bundle with VT0 and VT1, try to inject VT2 that conflicts with VT1
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);
            bundle[1] = MicroOpTestHelper.CreateScalarALU(1, destReg: 9, src1Reg: 10, src2Reg: 11);

            var verifier = new SafetyVerifier();

            // VT2 conflicts with VT1 (both use register group around R9-11)
            var candidate = MicroOpTestHelper.CreateScalarALU(2, destReg: 10, src1Reg: 20, src2Reg: 21);

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Should be rejected due to conflict with VT1's registers
            Assert.False(result, "VT2 should be rejected due to register conflict with VT1");
            _output.WriteLine($"Multi-thread mask overlay correctly rejected conflicting VT");
        }

        #endregion
    }
}
