using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using System.Collections.Generic;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Unit tests for MicroOpScheduler - Formally Safe Packing (FSP) slot stealing scheduler.
    /// Tests verify that the scheduler correctly manages nomination ports, identifies stealable slots,
    /// and integrates with SafetyVerifier for non-interference guarantees.
    /// </summary>
    public class MicroOpSchedulerTests
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
                WritesRegister = true
            };
            aluOp.InitializeMetadata();
            return aluOp;
        }

        #region Constructor and Initialization Tests

        [Fact]
        public void Constructor_ShouldInitializeEmptyScheduler()
        {
            // Arrange & Act
            var scheduler = new MicroOpScheduler();

            // Assert
            Assert.NotNull(scheduler);
            Assert.Equal(0, scheduler.TotalSchedulerCycles);
            Assert.Equal(0, scheduler.SuccessfulInjectionsCount);
        }

        [Fact]
        public void PackBundleIntraCoreSmt_WhenReplayPhaseActive_ThenCertificateReuseTelemetryAdvances()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
                isActive: true,
                epochId: 1,
                cachedPc: 0x1000,
                epochLength: 4,
                completedReplays: 0,
                validSlotCount: 1,
                stableDonorMask: 0xFE,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None));

            var bundle = new MicroOp[8];
            bundle[0] = CreateScalarALUMicroOp(0, 1, 2, 3);

            scheduler.NominateSmtCandidate(1, CreateScalarALUMicroOp(1, 8, 9, 10));

            // Act
            var packed = scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            // Assert
            Assert.NotNull(packed[1]);
            Assert.True(scheduler.PhaseCertificateReadyHits > 0);
            Assert.True(scheduler.EstimatedPhaseCertificateChecksSaved > 0);
            Assert.True(scheduler.PhaseCertificateInvalidations > 0);
            Assert.Equal(ReplayPhaseInvalidationReason.CertificateMutation, scheduler.LastPhaseCertificateInvalidationReason);
        }

        #endregion

        #region Nominate Tests

        [Fact]
        public void Nominate_ValidCoreId_ShouldRegisterCandidate()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var op = CreateScalarALUMicroOp(threadId: 5, destReg: 1, src1Reg: 2, src2Reg: 3);

            // Act
            scheduler.Nominate(5, op);

            // Assert - TryStealSlot should return the nominated op
            var stolen = scheduler.TryStealSlot(0, 0);
            Assert.NotNull(stolen);
            Assert.Same(op, stolen);
        }

        [Fact]
        public void Nominate_InvalidCoreId_ShouldBeIgnored()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var op = CreateScalarALUMicroOp(threadId: 0, destReg: 1, src1Reg: 2, src2Reg: 3);

            // Act - Core ID 16 is out of range (0-15)
            scheduler.Nominate(16, op);

            // Assert - No candidate should be available
            var stolen = scheduler.TryStealSlot(0, 0);
            Assert.Null(stolen);
        }

        [Fact]
        public void Nominate_NullOp_ShouldBeIgnored()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();

            // Act
            scheduler.Nominate(0, null);

            // Assert
            var stolen = scheduler.TryStealSlot(1, 0);
            Assert.Null(stolen);
        }

        [Fact]
        public void Nominate_MultipleCores_ShouldIsolateNominations()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var op0 = CreateScalarALUMicroOp(0, 1, 2, 3);
            var op1 = CreateScalarALUMicroOp(1, 4, 5, 6);
            var op2 = CreateScalarALUMicroOp(2, 7, 8, 9);

            // Act
            scheduler.Nominate(0, op0);
            scheduler.Nominate(1, op1);
            scheduler.Nominate(2, op2);

            // Assert - Priority encoder returns lowest index first
            var stolen = scheduler.TryStealSlot(3, 0);
            Assert.Same(op0, stolen);
        }

        #endregion

        #region TryStealSlot Tests

        [Fact]
        public void TryStealSlot_NoNominations_ShouldReturnNull()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();

            // Act
            var stolen = scheduler.TryStealSlot(0, 0);

            // Assert
            Assert.Null(stolen);
        }

        [Fact]
        public void TryStealSlot_ShouldConsumePort()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var op = CreateScalarALUMicroOp(1, 10, 11, 12);
            scheduler.Nominate(1, op);

            // Act - First steal should succeed
            var stolen1 = scheduler.TryStealSlot(0, 0);
            // Second steal should fail (port consumed)
            var stolen2 = scheduler.TryStealSlot(0, 0);

            // Assert
            Assert.NotNull(stolen1);
            Assert.Null(stolen2);
        }

        #endregion

        #region PackBundle Tests - Basic Functionality

        [Fact]
        public void PackBundle_WithStealDisabled_ShouldReturnOriginalBundle()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var originalBundle = new MicroOp[8];
            for (int i = 0; i < 8; i++)
            {
                originalBundle[i] = CreateNopMicroOp(threadId: 0);
            }

            // Act - FSP disabled
            var result = scheduler.PackBundle(originalBundle, currentThreadId: 0,
                stealEnabled: false, stealMask: 0xFF);

            // Assert - should return original bundle unchanged
            Assert.Equal(8, result.Length);
            for (int i = 0; i < 8; i++)
            {
                Assert.Same(originalBundle[i], result[i]);
            }
        }

        [Fact]
        public void PackBundle_WithZeroStealMask_ShouldNotStealAnySlots()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var originalBundle = new MicroOp[8];
            for (int i = 0; i < 8; i++)
            {
                originalBundle[i] = CreateNopMicroOp(threadId: 0);
            }

            // Nominate a candidate from another core
            scheduler.Nominate(1, CreateScalarALUMicroOp(1, 10, 11, 12));

            // Act - FSP enabled but mask is 0x00 (no slots stealable)
            var result = scheduler.PackBundle(originalBundle, currentThreadId: 0,
                stealEnabled: true, stealMask: 0x00);

            // Assert - no changes should be made
            for (int i = 0; i < 8; i++)
            {
                Assert.Same(originalBundle[i], result[i]);
            }
        }

        [Fact]
        public void PackBundle_WithNoNominations_ShouldReturnOriginalBundle()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var originalBundle = new MicroOp[8];
            for (int i = 0; i < 8; i++)
            {
                originalBundle[i] = CreateNopMicroOp(threadId: 0);
            }

            // Act - FSP enabled, all slots stealable, but no nominations
            var result = scheduler.PackBundle(originalBundle, currentThreadId: 0,
                stealEnabled: true, stealMask: 0xFF);

            // Assert
            for (int i = 0; i < 8; i++)
            {
                Assert.Same(originalBundle[i], result[i]);
            }
        }

        [Fact]
        public void PackBundle_WithNominatedOps_ShouldInjectIntoNopSlots()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();

            // Create bundle with NOPs in slots 0, 2, 4
            var originalBundle = new MicroOp[8];
            originalBundle[0] = CreateNopMicroOp(threadId: 0);
            originalBundle[1] = CreateScalarALUMicroOp(0, 1, 2, 3);
            originalBundle[2] = CreateNopMicroOp(threadId: 0);
            originalBundle[3] = CreateScalarALUMicroOp(0, 4, 5, 6);
            originalBundle[4] = CreateNopMicroOp(threadId: 0);
            originalBundle[5] = CreateScalarALUMicroOp(0, 7, 8, 9);
            originalBundle[6] = CreateNopMicroOp(threadId: 0);
            originalBundle[7] = CreateNopMicroOp(threadId: 0);

            // Nominate operations from other cores (no register conflicts)
            scheduler.Nominate(1, CreateScalarALUMicroOp(1, 10, 11, 12));
            scheduler.Nominate(2, CreateScalarALUMicroOp(2, 13, 14, 15));

            // Act - FSP enabled, all slots stealable
            var result = scheduler.PackBundle(originalBundle, currentThreadId: 0,
                stealEnabled: true, stealMask: 0xFF);

            // Assert - some NOPs should be replaced
            Assert.Equal(8, result.Length);
            // Original non-NOP slots should remain
            Assert.Same(originalBundle[1], result[1]);
            Assert.Same(originalBundle[3], result[3]);
            Assert.Same(originalBundle[5], result[5]);
        }

        #endregion

        #region ClearNominationPorts Tests

        [Fact]
        public void ClearNominationPorts_ShouldRemoveAllNominations()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.Nominate(0, CreateScalarALUMicroOp(0, 1, 2, 3));
            scheduler.Nominate(1, CreateScalarALUMicroOp(1, 4, 5, 6));
            scheduler.Nominate(2, CreateScalarALUMicroOp(2, 7, 8, 9));

            // Act
            scheduler.ClearNominationPorts();

            // Assert - no candidates should be available
            var stolen = scheduler.TryStealSlot(3, 0);
            Assert.Null(stolen);
        }

        [Fact]
        public void ClearNominationPorts_ThenNominate_ShouldWorkCorrectly()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.Nominate(0, CreateScalarALUMicroOp(0, 1, 2, 3));
            scheduler.ClearNominationPorts();

            // Act - nominate after clear
            var op = CreateScalarALUMicroOp(0, 4, 5, 6);
            scheduler.Nominate(0, op);

            // Assert
            var stolen = scheduler.TryStealSlot(1, 0);
            Assert.NotNull(stolen);
            Assert.Same(op, stolen);
        }

        #endregion

        #region SetCoreStalled Tests

        [Fact]
        public void SetCoreStalled_StalledCore_ShouldAnnulNomination()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.SetCoreStalled(3, true);

            // Act - Nomination from stalled core should be ignored
            scheduler.Nominate(3, CreateScalarALUMicroOp(3, 1, 2, 3));

            // Assert
            var stolen = scheduler.TryStealSlot(0, 0);
            Assert.Null(stolen);
        }

        [Fact]
        public void SetCoreStalled_UnstalledCore_ShouldAllowNomination()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.SetCoreStalled(3, true);
            scheduler.SetCoreStalled(3, false);

            // Act
            var op = CreateScalarALUMicroOp(3, 1, 2, 3);
            scheduler.Nominate(3, op);

            // Assert
            var stolen = scheduler.TryStealSlot(0, 0);
            Assert.NotNull(stolen);
            Assert.Same(op, stolen);
        }

        #endregion
    }
}
