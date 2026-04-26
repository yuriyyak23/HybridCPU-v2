using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using HybridCPU_ISE.Tests.TestHelpers;
using System;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Unit tests for MicroOpScheduler TestMode functionality.
    /// These tests verify that the test-only infrastructure works correctly for FSP performance testing.
    ///
    /// Created: 2026-03-02
    /// For: testPerfPlan.md Iteration 2 - Performance test infrastructure
    /// </summary>
    public class MicroOpSchedulerTestModeTests
    {
        #region TestMode Initialization Tests

        [Fact]
        public void InitializeTestMode_ShouldEnableTestMode()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();

            // Act
            scheduler.InitializeTestMode();

            // Assert
            Assert.True(scheduler.TestMode);
            Assert.NotNull(scheduler.TestNominationQueues);
            Assert.Equal(4, scheduler.TestNominationQueues.Length);
        }

        [Fact]
        public void InitializeTestMode_ShouldResetCounters()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.InitializeTestMode();

            // Act
            scheduler.ResetTestCounters();

            // Assert
            Assert.Equal(0, scheduler.TestRejectedDueToResourceConflict);
            Assert.Equal(0, scheduler.TestRejectedDueToMemoryConflict);
            Assert.Equal(0, scheduler.TestRejectedDueToSafetyMask);
            Assert.Equal(0, scheduler.TestRejectedDueToFullBundle);
            Assert.Equal("", scheduler.LastRejectionReason);
        }

        [Fact]
        public void InitializeTestMode_ShouldCreateEmptyNominationQueues()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();

            // Act
            scheduler.InitializeTestMode();

            // Assert
            for (int vt = 0; vt < 4; vt++)
            {
                Assert.NotNull(scheduler.TestNominationQueues[vt]);
                Assert.Equal(0, scheduler.TestNominationQueues[vt].Count);
            }
        }

        #endregion

        #region TestEnqueueMicroOp Tests

        [Fact]
        public void TestEnqueueMicroOp_WithoutTestMode_ShouldThrowException()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var op = MicroOpTestHelper.CreateNop(0);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                scheduler.TestEnqueueMicroOp(0, op));
            Assert.Contains("TestMode", ex.Message);
        }

        [Fact]
        public void TestEnqueueMicroOp_ValidOp_ShouldEnqueueSuccessfully()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.InitializeTestMode();
            var op = MicroOpTestHelper.CreateScalarALU(
                virtualThreadId: 1,
                destReg: 10,
                src1Reg: 11,
                src2Reg: 12);

            // Act
            scheduler.TestEnqueueMicroOp(1, op);

            // Assert
            Assert.Equal(1, scheduler.TestNominationQueues[1].Count);
            Assert.Equal(1, scheduler.TestGetPendingNominationCount());
        }

        [Fact]
        public void TestEnqueueMicroOp_InvalidVirtualThreadId_ShouldThrowException()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.InitializeTestMode();
            var op = MicroOpTestHelper.CreateNop(0);

            // Act & Assert - VT ID -1 is invalid
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                scheduler.TestEnqueueMicroOp(-1, op));

            // VT ID 4 is invalid (valid range 0-3)
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                scheduler.TestEnqueueMicroOp(4, op));
        }

        [Fact]
        public void TestEnqueueMicroOp_NullOp_ShouldThrowException()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.InitializeTestMode();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                scheduler.TestEnqueueMicroOp(0, null));
        }

        [Fact]
        public void TestEnqueueMicroOp_MultipleOps_ShouldEnqueueInOrder()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.InitializeTestMode();
            var op1 = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);
            var op2 = MicroOpTestHelper.CreateScalarALU(0, 4, 5, 6);
            var op3 = MicroOpTestHelper.CreateScalarALU(0, 7, 8, 9);

            // Act
            scheduler.TestEnqueueMicroOp(0, op1);
            scheduler.TestEnqueueMicroOp(0, op2);
            scheduler.TestEnqueueMicroOp(0, op3);

            // Assert
            Assert.Equal(3, scheduler.TestNominationQueues[0].Count);
            Assert.Equal(3, scheduler.TestGetPendingNominationCount());
        }

        [Fact]
        public void TestEnqueueMicroOp_DifferentVirtualThreads_ShouldIsolateQueues()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.InitializeTestMode();
            var opVT0 = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);
            var opVT1 = MicroOpTestHelper.CreateScalarALU(1, 4, 5, 6);
            var opVT2 = MicroOpTestHelper.CreateScalarALU(2, 7, 8, 9);
            var opVT3 = MicroOpTestHelper.CreateScalarALU(3, 10, 11, 12);

            // Act
            scheduler.TestEnqueueMicroOp(0, opVT0);
            scheduler.TestEnqueueMicroOp(1, opVT1);
            scheduler.TestEnqueueMicroOp(2, opVT2);
            scheduler.TestEnqueueMicroOp(3, opVT3);

            // Assert - each VT should have 1 op
            Assert.Equal(1, scheduler.TestNominationQueues[0].Count);
            Assert.Equal(1, scheduler.TestNominationQueues[1].Count);
            Assert.Equal(1, scheduler.TestNominationQueues[2].Count);
            Assert.Equal(1, scheduler.TestNominationQueues[3].Count);
            Assert.Equal(4, scheduler.TestGetPendingNominationCount());
        }

        #endregion

        #region TestGetPendingNominationCount Tests

        [Fact]
        public void TestGetPendingNominationCount_EmptyQueues_ShouldReturnZero()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.InitializeTestMode();

            // Act
            var count = scheduler.TestGetPendingNominationCount();

            // Assert
            Assert.Equal(0, count);
        }

        [Fact]
        public void TestGetPendingNominationCount_WithoutTestMode_ShouldReturnZero()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();

            // Act
            var count = scheduler.TestGetPendingNominationCount();

            // Assert
            Assert.Equal(0, count);
        }

        [Fact]
        public void TestGetPendingNominationCount_WithOps_ShouldReturnCorrectCount()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.InitializeTestMode();
            scheduler.TestEnqueueMicroOp(0, MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3));
            scheduler.TestEnqueueMicroOp(0, MicroOpTestHelper.CreateScalarALU(0, 4, 5, 6));
            scheduler.TestEnqueueMicroOp(1, MicroOpTestHelper.CreateScalarALU(1, 7, 8, 9));

            // Act
            var count = scheduler.TestGetPendingNominationCount();

            // Assert
            Assert.Equal(3, count);
        }

        #endregion

        #region TestClearNominationQueues Tests

        [Fact]
        public void TestClearNominationQueues_ShouldEmptyAllQueues()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.InitializeTestMode();
            scheduler.TestEnqueueMicroOp(0, MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3));
            scheduler.TestEnqueueMicroOp(1, MicroOpTestHelper.CreateScalarALU(1, 4, 5, 6));
            scheduler.TestEnqueueMicroOp(2, MicroOpTestHelper.CreateScalarALU(2, 7, 8, 9));

            // Act
            scheduler.TestClearNominationQueues();

            // Assert
            Assert.Equal(0, scheduler.TestGetPendingNominationCount());
            for (int vt = 0; vt < 4; vt++)
            {
                Assert.Equal(0, scheduler.TestNominationQueues[vt].Count);
            }
        }

        [Fact]
        public void TestClearNominationQueues_WithoutTestMode_ShouldNotThrow()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();

            // Act & Assert - should not throw
            scheduler.TestClearNominationQueues();
        }

        #endregion

        #region Rejection Counter Tests

        [Fact]
        public void ResetTestCounters_ShouldZeroAllCounters()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.InitializeTestMode();

            // Simulate some internal rejection tracking (these would normally be incremented internally)
            // For now just verify reset behavior

            // Act
            scheduler.ResetTestCounters();

            // Assert
            Assert.Equal(0, scheduler.TestRejectedDueToResourceConflict);
            Assert.Equal(0, scheduler.TestRejectedDueToMemoryConflict);
            Assert.Equal(0, scheduler.TestRejectedDueToSafetyMask);
            Assert.Equal(0, scheduler.TestRejectedDueToFullBundle);
            Assert.Equal("", scheduler.LastRejectionReason);
        }

        #endregion

        #region Integration Tests with TestMode

        [Fact]
        public void TestMode_WithOrthogonalOps_ShouldEnqueueSuccessfully()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.InitializeTestMode();
            var orthogonalOps = MicroOpTestHelper.CreateOrthogonalSet(4);

            // Act - Enqueue all orthogonal ops
            for (int i = 0; i < orthogonalOps.Count; i++)
            {
                scheduler.TestEnqueueMicroOp(i, orthogonalOps[i]);
            }

            // Assert
            Assert.Equal(4, scheduler.TestGetPendingNominationCount());
            for (int vt = 0; vt < 4; vt++)
            {
                Assert.Equal(1, scheduler.TestNominationQueues[vt].Count);
            }
        }

        [Fact]
        public void TestMode_WithConflictingOps_ShouldEnqueueButWillBeRejectedLater()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.InitializeTestMode();
            var conflictingOps = MicroOpTestHelper.CreateConflictingSet();

            // Act - Both ops enqueue successfully (rejection happens during injection)
            scheduler.TestEnqueueMicroOp(0, conflictingOps[0]);
            scheduler.TestEnqueueMicroOp(1, conflictingOps[1]);

            // Assert - enqueue is successful regardless of future conflicts
            Assert.Equal(2, scheduler.TestGetPendingNominationCount());
        }

        [Fact]
        public void TestMode_WithRAWDependentOps_ShouldEnqueueSuccessfully()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.InitializeTestMode();
            var rawOps = MicroOpTestHelper.CreateRAWDependentSet();

            // Act
            scheduler.TestEnqueueMicroOp(0, rawOps[0]);
            scheduler.TestEnqueueMicroOp(1, rawOps[1]);

            // Assert
            Assert.Equal(2, scheduler.TestGetPendingNominationCount());
        }

        [Fact]
        public void TestMode_WithMemoryConflictOps_ShouldEnqueueSuccessfully()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.InitializeTestMode();
            var memoryOps = MicroOpTestHelper.CreateMemoryConflictSet();

            // Act
            scheduler.TestEnqueueMicroOp(0, memoryOps[0]);
            scheduler.TestEnqueueMicroOp(1, memoryOps[1]);

            // Assert
            Assert.Equal(2, scheduler.TestGetPendingNominationCount());
        }

        [Fact]
        public void TestMode_WithDiverseOps_ShouldEnqueueSuccessfully()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.InitializeTestMode();
            var diverseOps = MicroOpTestHelper.CreateDiverseSet();

            // Act - Enqueue diverse ops across different VTs
            for (int i = 0; i < diverseOps.Count; i++)
            {
                scheduler.TestEnqueueMicroOp(i % 4, diverseOps[i]);
            }

            // Assert
            Assert.Equal(diverseOps.Count, scheduler.TestGetPendingNominationCount());
        }

        #endregion
    }
}
