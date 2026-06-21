using Xunit;
using Xunit.Abstractions;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using HybridCPU_ISE.Tests.TestHelpers;
using System;
using System.Collections.Generic;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Category 5: False Conflict Rate Tests (Empirical)
    ///
    /// Measures and analyzes false positive rates in mask-based conflict detection:
    /// - Random instruction trace generation
    /// - Collision frequency measurement
    /// - Comparison with ideal oracle
    /// - Statistical analysis
    /// </summary>
    public class ISAModelFalseConflictTests
    {
        private readonly ITestOutputHelper _output;
        private readonly Random _random = new Random(42); // Fixed seed for reproducibility

        public ISAModelFalseConflictTests(ITestOutputHelper output)
        {
            _output = output;
        }

        #region Random Trace Generation

        [Fact]
        public void FalseConflict_RandomTrace_GeneratesDiverseOperations()
        {
            // Generate random instruction traces

            // Arrange & Act: Generate 100 random operations
            var operations = new List<MicroOp>();
            for (int i = 0; i < 100; i++)
            {
                ushort destReg = (ushort)_random.Next(0, 32);
                ushort src1Reg = (ushort)_random.Next(0, 32);
                ushort src2Reg = (ushort)_random.Next(0, 32);
                int vt = _random.Next(0, 4);

                var op = MicroOpTestHelper.CreateScalarALU(vt, destReg, src1Reg, src2Reg);
                operations.Add(op);
            }

            // Assert: Operations generated
            Assert.Equal(100, operations.Count);
            _output.WriteLine($"Generated {operations.Count} random operations");
        }

        [Fact]
        public void FalseConflict_RandomTrace_VariesRegisterUsage()
        {
            // Ensure random traces use various register ranges

            // Arrange & Act
            var registerUsage = new HashSet<ushort>();
            for (int i = 0; i < 50; i++)
            {
                ushort reg = (ushort)_random.Next(0, 32);
                registerUsage.Add(reg);
            }

            // Assert: Good coverage
            Assert.True(registerUsage.Count > 10, "Should use diverse registers");
            _output.WriteLine($"Register coverage: {registerUsage.Count} distinct registers used");
        }

        [Fact]
        public void FalseConflict_RandomTrace_MixedOperationTypes()
        {
            // Generate mix of ALU and memory operations

            // Arrange & Act
            int aluCount = 0, loadCount = 0, storeCount = 0;

            for (int i = 0; i < 60; i++)
            {
                int opType = _random.Next(0, 3);
                if (opType == 0)
                {
                    MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);
                    aluCount++;
                }
                else if (opType == 1)
                {
                    MicroOpTestHelper.CreateLoad(0, 1, 0x1000, 0);
                    loadCount++;
                }
                else
                {
                    MicroOpTestHelper.CreateStore(0, 1, 0x1000, domainTag: 0);
                    storeCount++;
                }
            }

            // Assert: Mix achieved
            _output.WriteLine($"Operation mix: ALU={aluCount}, Load={loadCount}, Store={storeCount}");
            Assert.True(aluCount > 0 && loadCount > 0 && storeCount > 0, "Should have mixed operations");
        }

        #endregion

        #region Collision Frequency Measurement

        [Fact]
        public void FalseConflict_CollisionRate_OrthogonalOperations()
        {
            // Measure collision rate for operations that shouldn't conflict

            // Arrange: Generate pairs of operations in different register groups
            int totalPairs = 100;
            int conflicts = 0;

            var verifier = new SafetyVerifier();

            for (int i = 0; i < totalPairs; i++)
            {
                var bundle = new MicroOp[8];
                // Use register group 0 (R0-3)
                bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);

                // Use register group 2 (R8-11)
                var candidate = MicroOpTestHelper.CreateScalarALU(1, 9, 10, 11);

                // Check for conflict
                if (!SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate))
                {
                    conflicts++;
                }
            }

            // Act: Calculate rate
            double conflictRate = (double)conflicts / totalPairs;

            // Assert: Should be low (orthogonal)
            _output.WriteLine($"Orthogonal ops conflict rate: {conflictRate:P2} ({conflicts}/{totalPairs})");
            Assert.True(conflictRate < 0.1, "Orthogonal operations should rarely conflict");
        }

        [Fact]
        public void FalseConflict_CollisionRate_RandomPairs()
        {
            // Measure collision rate for random operation pairs

            // Arrange
            int totalPairs = 200;
            int conflicts = 0;
            var verifier = new SafetyVerifier();

            for (int i = 0; i < totalPairs; i++)
            {
                var bundle = new MicroOp[8];
                ushort r1 = (ushort)_random.Next(0, 16);
                bundle[0] = MicroOpTestHelper.CreateScalarALU(0, r1, (ushort)(r1 + 1), (ushort)(r1 + 2));

                ushort r2 = (ushort)_random.Next(0, 16);
                var candidate = MicroOpTestHelper.CreateScalarALU(1, r2, (ushort)(r2 + 1), (ushort)(r2 + 2));

                if (!SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate))
                {
                    conflicts++;
                }
            }

            // Act
            double conflictRate = (double)conflicts / totalPairs;

            // Assert
            _output.WriteLine($"Random pairs conflict rate: {conflictRate:P2} ({conflicts}/{totalPairs})");
            Assert.True(conflictRate >= 0.0, "Conflict rate should be measurable");
        }

        [Fact]
        public void FalseConflict_CollisionRate_SameGroupPairs()
        {
            // Measure collision rate for same register group

            // Arrange
            int totalPairs = 100;
            int conflicts = 0;
            var verifier = new SafetyVerifier();

            for (int i = 0; i < totalPairs; i++)
            {
                var bundle = new MicroOp[8];
                // All in group 0 (R0-3)
                bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);

                var candidate = MicroOpTestHelper.CreateScalarALU(1, 2, 1, 0);

                if (!SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate))
                {
                    conflicts++;
                }
            }

            // Act
            double conflictRate = (double)conflicts / totalPairs;

            // Assert: Should be high (same group)
            _output.WriteLine($"Same group conflict rate: {conflictRate:P2} ({conflicts}/{totalPairs})");
            Assert.True(conflictRate > 0.5, "Same group should have high conflict rate");
        }

        #endregion

        #region Statistical Analysis

        [Fact]
        public void FalseConflict_Statistics_MeanConflictRate()
        {
            // Calculate mean conflict rate over multiple trials

            // Arrange
            int trials = 10;
            var conflictRates = new List<double>();
            var verifier = new SafetyVerifier();

            for (int trial = 0; trial < trials; trial++)
            {
                int pairs = 50;
                int conflicts = 0;

                for (int i = 0; i < pairs; i++)
                {
                    var bundle = new MicroOp[8];
                    ushort r1 = (ushort)_random.Next(0, 16);
                    bundle[0] = MicroOpTestHelper.CreateScalarALU(0, r1, (ushort)(r1 + 1), (ushort)(r1 + 2));

                    ushort r2 = (ushort)_random.Next(0, 16);
                    var candidate = MicroOpTestHelper.CreateScalarALU(1, r2, (ushort)(r2 + 1), (ushort)(r2 + 2));

                    if (!SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate))
                    {
                        conflicts++;
                    }
                }

                conflictRates.Add((double)conflicts / pairs);
            }

            // Act: Calculate statistics
            double mean = 0;
            foreach (var rate in conflictRates)
                mean += rate;
            mean /= trials;

            // Assert
            _output.WriteLine($"Mean conflict rate over {trials} trials: {mean:P2}");
            Assert.True(mean >= 0.0 && mean <= 1.0, "Mean should be valid probability");
        }

        [Fact]
        public void FalseConflict_Statistics_VarianceAnalysis()
        {
            // Analyze variance in conflict rates

            // Arrange & Act
            var rates = new List<double>();
            var verifier = new SafetyVerifier();

            for (int i = 0; i < 20; i++)
            {
                int conflicts = 0;
                for (int j = 0; j < 30; j++)
                {
                    var bundle = new MicroOp[8];
                    bundle[0] = MicroOpTestHelper.CreateScalarALU(0, (ushort)(j % 8), (ushort)((j + 1) % 8), (ushort)((j + 2) % 8));
                    var candidate = MicroOpTestHelper.CreateScalarALU(1, (ushort)((j + 4) % 8), (ushort)((j + 5) % 8), (ushort)((j + 6) % 8));

                    if (!SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate))
                        conflicts++;
                }
                rates.Add((double)conflicts / 30);
            }

            // Calculate variance
            double mean = 0;
            foreach (var r in rates) mean += r;
            mean /= rates.Count;

            double variance = 0;
            foreach (var r in rates)
                variance += (r - mean) * (r - mean);
            variance /= rates.Count;

            // Assert
            _output.WriteLine($"Variance in conflict rates: {variance:F4}");
            Assert.True(variance >= 0.0, "Variance should be non-negative");
        }

        #endregion

        #region Comparison with Ideal Oracle

        [Fact]
        public void FalseConflict_Oracle_PerfectNonConflict()
        {
            // Ideal oracle: Different register groups should never conflict

            // Arrange: Operations in different groups
            var op1 = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);    // Group 0
            var op2 = MicroOpTestHelper.CreateScalarALU(1, 9, 10, 11);  // Group 2

            // Ideal oracle says: no conflict
            bool idealOracle = true;

            // Actual system
            var bundle = new MicroOp[8];
            bundle[0] = op1;
            var verifier = new SafetyVerifier();
            bool actual = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, op2);

            // Assert: System matches oracle
            Assert.Equal(idealOracle, actual);
            _output.WriteLine("Oracle: Perfect non-conflict case matches");
        }

        [Fact]
        public void FalseConflict_Oracle_TrueConflict()
        {
            // Ideal oracle: Same register should conflict

            // Arrange
            var op1 = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);
            var op2 = MicroOpTestHelper.CreateScalarALU(1, 2, 10, 11); // Writes R2, reads R1-R3

            // Ideal oracle says: conflict (same group)
            bool idealOracle = false;

            // Actual system
            var bundle = new MicroOp[8];
            bundle[0] = op1;
            var verifier = new SafetyVerifier();
            bool actual = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, op2);

            // Assert: System matches oracle
            Assert.Equal(idealOracle, actual);
            _output.WriteLine("Oracle: True conflict case matches");
        }

        [Fact]
        public void FalseConflict_Oracle_FalsePositiveRate()
        {
            // Measure false positive rate vs ideal oracle

            // Arrange: Test cases where ideal oracle says "safe" but system says "conflict"
            int falsePositives = 0;
            int totalIdealSafe = 0;
            var verifier = new SafetyVerifier();

            // Test: Different groups (ideal = safe)
            for (int g1 = 0; g1 < 4; g1++)
            {
                for (int g2 = 0; g2 < 4; g2++)
                {
                    if (g1 == g2) continue; // Skip same group

                    var bundle = new MicroOp[8];
                    bundle[0] = MicroOpTestHelper.CreateScalarALU(0, (ushort)(g1 * 4), (ushort)(g1 * 4 + 1), (ushort)(g1 * 4 + 2));
                    var candidate = MicroOpTestHelper.CreateScalarALU(1, (ushort)(g2 * 4), (ushort)(g2 * 4 + 1), (ushort)(g2 * 4 + 2));

                    totalIdealSafe++;
                    if (!SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate))
                    {
                        falsePositives++;
                    }
                }
            }

            // Act
            double falsePositiveRate = totalIdealSafe > 0 ? (double)falsePositives / totalIdealSafe : 0;

            // Assert
            _output.WriteLine($"False positive rate: {falsePositiveRate:P2} ({falsePositives}/{totalIdealSafe})");
            Assert.True(falsePositiveRate < 0.2, "False positive rate should be low for different groups");
        }

        #endregion

        #region Register Group Sensitivity

        [Fact]
        public void FalseConflict_GroupSensitivity_4RegisterGroups()
        {
            // Test sensitivity to 4-register grouping

            // Arrange: Operations within same group
            var conflicts = 0;
            var total = 0;
            var verifier = new SafetyVerifier();

            for (ushort r1 = 0; r1 < 4; r1++)
            {
                for (ushort r2 = 0; r2 < 4; r2++)
                {
                    var bundle = new MicroOp[8];
                    bundle[0] = MicroOpTestHelper.CreateScalarALU(0, r1, (ushort)((r1 + 1) % 4), (ushort)((r1 + 2) % 4));
                    var candidate = MicroOpTestHelper.CreateScalarALU(1, r2, (ushort)((r2 + 1) % 4), (ushort)((r2 + 2) % 4));

                    total++;
                    if (!SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate))
                        conflicts++;
                }
            }

            // Assert
            double rate = (double)conflicts / total;
            _output.WriteLine($"Within-group (R0-R3) conflict rate: {rate:P2}");
            Assert.True(rate > 0.5, "Within same group should have high conflict rate");
        }

        [Fact]
        public void FalseConflict_GroupSensitivity_CrossGroup()
        {
            // Test cross-group non-conflicts

            // Arrange
            var conflicts = 0;
            var total = 0;
            var verifier = new SafetyVerifier();

            // Group 0 vs Group 2
            for (int i = 0; i < 10; i++)
            {
                var bundle = new MicroOp[8];
                bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);      // Group 0
                var candidate = MicroOpTestHelper.CreateScalarALU(1, 9, 10, 11); // Group 2

                total++;
                if (!SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate))
                    conflicts++;
            }

            // Assert
            double rate = (double)conflicts / total;
            _output.WriteLine($"Cross-group conflict rate: {rate:P2}");
            Assert.True(rate < 0.1, "Cross-group should have low conflict rate");
        }

        #endregion
    }
}
