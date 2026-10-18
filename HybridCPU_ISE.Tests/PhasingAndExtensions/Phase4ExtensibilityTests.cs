using System;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Tests for Phase 4 nomination-based scheduling behavior.
    /// </summary>
    public class ExtensibilityTests
    {
        #region Nomination-Based Scheduling Tests

        [Fact]
        public void Test_4_8_Nominate_StalledCore_ShouldBeIgnored()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.SetCoreStalled(1, true);
            var op = new NopMicroOp { OwnerThreadId = 1 };

            // Act
            scheduler.Nominate(1, op);

            // Assert - stalled core's nomination should be annulled
            var stolen = scheduler.TryStealSlot(0, 0);
            Assert.Null(stolen);
        }

        [Fact]
        public void Test_4_9_Nominate_ControlFlowOp_ShouldBeIgnored()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var cfOp = new BranchMicroOp { OwnerThreadId = 1 };

            // Act - control flow ops should not be nominated
            scheduler.Nominate(1, cfOp);

            // Assert
            var stolen = scheduler.TryStealSlot(0, 0);
            Assert.Null(stolen);
        }

        [Fact]
        public void Test_4_10_TryStealSlot_PriorityEncoder_ShouldReturnLowestIndex()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var op5 = new NopMicroOp { OwnerThreadId = 5 };
            var op2 = new NopMicroOp { OwnerThreadId = 2 };

            scheduler.Nominate(5, op5);
            scheduler.Nominate(2, op2);

            // Act - priority encoder scans 0→15
            var stolen = scheduler.TryStealSlot(0, 0);

            // Assert - should return core 2's nomination (lower index)
            Assert.NotNull(stolen);
            Assert.Same(op2, stolen);
        }

        [Fact]
        public void Test_4_11_PackBundle_WithNominations_ShouldFillSlots()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var microOp1 = new NopMicroOp { OwnerThreadId = 1 };
            var microOp2 = new NopMicroOp { OwnerThreadId = 2 };
            scheduler.Nominate(1, microOp1);
            scheduler.Nominate(2, microOp2);

            var originalBundle = new MicroOp[8]; // Empty bundle (null slots)

            // Act
            var packedBundle = scheduler.PackBundle(
                originalBundle,
                currentThreadId: 0,
                stealEnabled: true,
                stealMask: 0xFF
            );

            // Assert
            Assert.NotNull(packedBundle);
        }

        [Fact]
        public void Test_4_12_ClearNominationPorts_ShouldResetAllPorts()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.Nominate(0, new NopMicroOp { OwnerThreadId = 0 });
            scheduler.Nominate(5, new NopMicroOp { OwnerThreadId = 5 });
            scheduler.Nominate(15, new NopMicroOp { OwnerThreadId = 15 });

            // Act
            scheduler.ClearNominationPorts();

            // Assert
            var stolen = scheduler.TryStealSlot(0, 0);
            Assert.Null(stolen);
        }

        #endregion
    }
}
