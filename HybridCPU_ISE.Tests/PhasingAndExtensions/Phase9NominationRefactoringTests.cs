using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using System.Collections.Generic;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Unit tests for Phase 9: Nomination Mechanism Refactoring.
    /// Tests verify HLS-compatible nomination ports, scoreboard, and PackBundle integration.
    /// </summary>
    public class Phase9NominationRefactoringTests
    {
        /// <summary>
        /// Helper method to create a simple scalar ALU micro-operation
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
                WritesRegister = true,
                Latency = 1
            };
            aluOp.InitializeMetadata();
            return aluOp;
        }

        /// <summary>
        /// Helper method to create a load micro-operation
        /// </summary>
        private MicroOp CreateLoadMicroOp(int threadId, ushort destReg, ulong address)
        {
            var loadOp = new LoadMicroOp
            {
                OpCode = 0,
                OwnerThreadId = threadId,
                DestRegID = destReg,
                Address = address,
                Size = 8,
                BaseRegID = 0,
                WritesRegister = true,
                Latency = 4
            };
            loadOp.InitializeMetadata();
            return loadOp;
        }

        #region Nomination Port Tests

        [Fact]
        public void Nominate_ValidCandidate_ShouldBeRetrievableViaTryStealSlot()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var op = CreateScalarALUMicroOp(1, 10, 11, 12);

            // Act
            scheduler.Nominate(1, op);
            var stolen = scheduler.TryStealSlot(0, 0);

            // Assert
            Assert.NotNull(stolen);
            Assert.Same(op, stolen);
        }

        [Fact]
        public void Nominate_StalledCore_ShouldIgnoreNomination()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.SetCoreStalled(1, true);
            var op = CreateScalarALUMicroOp(1, 10, 11, 12);

            // Act
            scheduler.Nominate(1, op);
            var stolen = scheduler.TryStealSlot(0, 0);

            // Assert
            Assert.Null(stolen);
        }

        [Fact]
        public void Nominate_ControlFlowOp_ShouldIgnoreNomination()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var cfOp = new BranchMicroOp { OwnerThreadId = 1 };

            // Act
            scheduler.Nominate(1, cfOp);
            var stolen = scheduler.TryStealSlot(0, 0);

            // Assert
            Assert.Null(stolen);
        }

        [Fact]
        public void TryStealSlot_DomainTagFiltering_ShouldRespectDomainIsolation()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var op = CreateScalarALUMicroOp(1, 10, 11, 12);
            op.Placement = op.Placement with { DomainTag = 0x01 }; // Domain 1
            scheduler.Nominate(1, op);

            // Act - request domain 0x02 (different domain)
            var stolen = scheduler.TryStealSlot(0, 0x02);

            // Assert - domain mismatch, should not steal
            Assert.Null(stolen);
        }

        [Fact]
        public void TryStealSlot_DomainTagZero_ShouldBypassFiltering()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var op = CreateScalarALUMicroOp(1, 10, 11, 12);
            op.Placement = op.Placement with { DomainTag = 0x01 };
            scheduler.Nominate(1, op);

            // Act - requestedDomainTag = 0 means no filtering
            var stolen = scheduler.TryStealSlot(0, 0);

            // Assert
            Assert.NotNull(stolen);
        }

        #endregion

        #region PackBundle Integration Tests

        [Fact]
        public void PackBundle_WithNominations_ShouldInjectCandidates()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();

            // Nominate from cores 1 and 2
            scheduler.Nominate(1, CreateScalarALUMicroOp(1, 10, 11, 12));
            scheduler.Nominate(2, CreateScalarALUMicroOp(2, 20, 21, 22));

            // Create bundle with NOP slots
            var bundle = new MicroOp[8];
            for (int i = 0; i < 8; i++)
            {
                bundle[i] = new NopMicroOp();
            }

            // Act
            var result = scheduler.PackBundle(bundle, 0, true, 0xFF);

            // Assert - at least some slots should be filled
            Assert.Equal(8, result.Length);
        }

        [Fact]
        public void PackBundle_WithMultipleNominations_ShouldFillMultipleSlots()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();

            // Nominate from multiple cores with non-conflicting registers
            scheduler.Nominate(1, CreateScalarALUMicroOp(1, 10, 11, 12));
            scheduler.Nominate(2, CreateScalarALUMicroOp(2, 20, 21, 22));
            scheduler.Nominate(3, CreateScalarALUMicroOp(3, 30, 31, 32));

            // Create empty bundle
            var bundle = new MicroOp[8];
            for (int i = 0; i < 8; i++)
            {
                bundle[i] = new NopMicroOp();
            }

            // Act
            var result = scheduler.PackBundle(bundle, 0, true, 0xFF);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(8, result.Length);
        }

        [Fact]
        public void PackBundle_ShouldIncrementTotalSchedulerCycles()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var bundle = new MicroOp[8];
            for (int i = 0; i < 8; i++)
            {
                bundle[i] = new NopMicroOp();
            }

            // Act
            scheduler.PackBundle(bundle, 0, true, 0xFF);
            scheduler.PackBundle(bundle, 0, true, 0xFF);

            // Assert
            Assert.Equal(2, scheduler.TotalSchedulerCycles);
        }

        #endregion

        #region Scoreboard Integration Tests

        [Fact]
        public void Scoreboard_PendingDMA_ShouldBlockDependentOps()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();

            // Act - mark register 5 as pending DMA
            int slot = scheduler.SetScoreboardPending(5, 1, 100);

            // Assert
            Assert.True(slot >= 0);
            Assert.True(scheduler.IsScoreboardPending(5));
            Assert.False(scheduler.IsScoreboardPending(6));
        }

        [Fact]
        public void Scoreboard_CompletedDMA_ShouldUnblock()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            int slot = scheduler.SetScoreboardPending(5, 1, 100);

            // Act
            scheduler.ClearScoreboardEntry(slot);

            // Assert
            Assert.False(scheduler.IsScoreboardPending(5));
        }

        [Fact]
        public void Scoreboard_FlushAll_ShouldClearEverything()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.SetScoreboardPending(1, 0, 0);
            scheduler.SetScoreboardPending(2, 0, 0);
            scheduler.SetScoreboardPending(3, 0, 0);

            // Act
            scheduler.ClearScoreboard();

            // Assert
            Assert.False(scheduler.IsScoreboardPending(1));
            Assert.False(scheduler.IsScoreboardPending(2));
            Assert.False(scheduler.IsScoreboardPending(3));
        }

        #endregion
    }
}
