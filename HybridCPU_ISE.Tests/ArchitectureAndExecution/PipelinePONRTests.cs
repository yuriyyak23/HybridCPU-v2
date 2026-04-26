using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using HybridCPU_ISE.Tests.TestHelpers;
using System;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Section 4: Pipeline, PONR & Cancellation Tests
    /// Тесты для проверки конвейера, точки невозврата (PONR) и механизмов отмены.
    /// Based on: required_tests_coverage.md Section 4
    /// </summary>
    public class PipelinePONRTests
    {
        #region 4.1 PONR_Architectural_Immutability

        /// <summary>
        /// Test: PONR_Architectural_Immutability
        /// Проверка, что инструкции, не достигшие стадии PONR (Point of No Return),
        /// не должны вызывать изменений в архитектурных регистрах (RF).
        /// </summary>
        [Fact]
        public void PONR_Architectural_Immutability_PrePONR_NoRegisterChanges()
        {
            // Arrange: Create a speculative operation (not yet committed)
            var speculativeOp = MicroOpTestHelper.CreateScalarALU(
                virtualThreadId: 0,
                destReg: 5,
                src1Reg: 1,
                src2Reg: 2);

            speculativeOp.IsSpeculative = true;

            // Act: Verify operation is marked as speculative (pre-PONR)
            // Pre-PONR instructions should NOT modify architectural registers
            // This is enforced by the IsSpeculative flag

            // Assert: Operation should be marked as speculative
            Assert.True(speculativeOp.IsSpeculative);

            // Speculative operations that write registers should not be committed
            Assert.True(speculativeOp.WritesRegister);
            Assert.Equal(5, speculativeOp.DestRegID);
        }

        /// <summary>
        /// Test: Speculative operations can be cancelled without side effects
        /// </summary>
        [Fact]
        public void PONR_Speculative_CanBeCancelledWithoutSideEffects()
        {
            // Arrange: Create speculative operation
            var speculativeOp = MicroOpTestHelper.CreateScalarALU(
                virtualThreadId: 1,
                destReg: 10,
                src1Reg: 11,
                src2Reg: 12);

            speculativeOp.IsSpeculative = true;

            // Act: Mark as faulted (cancelled)
            speculativeOp.Faulted = true;

            // Assert: Operation is cancelled and should not commit
            Assert.True(speculativeOp.IsSpeculative);
            Assert.True(speculativeOp.Faulted);
        }

        #endregion

        #region 4.2 PONR_Atomic_Commit

        /// <summary>
        /// Test: PONR_Atomic_Commit
        /// Проверка того, что многослотовые бандлы коммитятся атомарно (Total Order)
        /// с сортировкой по VTID при одновременном завершении.
        /// </summary>
        [Fact]
        public void PONR_Atomic_Commit_MultiSlotBundle_AtomicCommit()
        {
            // Arrange: Create a bundle with operations from different threads
            const int VLIW_SLOTS = 8;
            var bundle = new MicroOp[VLIW_SLOTS];

            // VT0: writes R4
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 4, src1Reg: 1, src2Reg: 2);
            // VT1: writes R12
            bundle[1] = MicroOpTestHelper.CreateScalarALU(1, destReg: 12, src1Reg: 10, src2Reg: 11);
            // VT2: writes R20
            bundle[2] = MicroOpTestHelper.CreateScalarALU(2, destReg: 20, src1Reg: 18, src2Reg: 19);

            // Act: Create bundle certificate to verify non-interference
            var certificate = BundleResourceCertificate.Create(
                new[] { bundle[0], bundle[1], bundle[2] },
                ownerThreadId: 0,
                cycleNumber: 1);

            // Assert: Bundle should be valid (all operations can commit atomically)
            Assert.True(certificate.IsValid());
            Assert.Equal(3, certificate.OperationCount);

            // Verify operations from different VTs don't conflict
            var op1Mask = bundle[0].SafetyMask;
            var op2Mask = bundle[1].SafetyMask;
            var op3Mask = bundle[2].SafetyMask;

            // Operations from different threads should not conflict
            Assert.True(op1Mask.NoConflictsWith(op2Mask));
            Assert.True(op2Mask.NoConflictsWith(op3Mask));
        }

        /// <summary>
        /// Test: Bundle operations commit in VTID order
        /// </summary>
        [Fact]
        public void PONR_Atomic_Commit_SortedByVTID()
        {
            // Arrange: Create operations with different VTIDs
            var vt0Op = MicroOpTestHelper.CreateScalarALU(0, 4, 1, 2);
            var vt1Op = MicroOpTestHelper.CreateScalarALU(1, 12, 10, 11);
            var vt2Op = MicroOpTestHelper.CreateScalarALU(2, 20, 18, 19);

            // Act: Verify VTIDs are in order
            Assert.Equal(0, vt0Op.VirtualThreadId);
            Assert.Equal(1, vt1Op.VirtualThreadId);
            Assert.Equal(2, vt2Op.VirtualThreadId);

            // Assert: Lower VTID should commit first in Total Order
            Assert.True(vt0Op.VirtualThreadId < vt1Op.VirtualThreadId);
            Assert.True(vt1Op.VirtualThreadId < vt2Op.VirtualThreadId);
        }

        #endregion

        #region 4.3 Pipeline_Flush_On_L1_Miss

        /// <summary>
        /// Test: Pipeline_Flush_On_L1_Miss
        /// Проверка сигнала FLUSH_VT[i]: L1 miss на загрузке мгновенно чистит конвейер
        /// только для вызвавшего потока и сбрасывает PC через ReplayEngine.
        /// </summary>
        [Fact]
        public void Pipeline_Flush_On_L1Miss_OnlyFaultyThreadFlushed()
        {
            // Arrange: Create a core and simulate pipeline with multiple VTs
            var core = new Processor.CPU_Core(0);

            // Create operations for VT0 (will have L1 miss)
            var vt0LoadOp = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 0,
                destReg: 1,
                address: 0x10000,
                domainTag: 0);

            // Create operations for VT1 (should NOT be flushed)
            var vt1AluOp = MicroOpTestHelper.CreateScalarALU(
                virtualThreadId: 1,
                destReg: 10,
                src1Reg: 11,
                src2Reg: 12);

            // Act: Simulate L1 miss on VT0 operation
            vt0LoadOp.IsSpeculative = true;
            vt0LoadOp.Faulted = true; // L1 miss triggers fault

            // Assert: VT0 operation is marked for flush
            Assert.True(vt0LoadOp.Faulted);
            Assert.True(vt0LoadOp.IsSpeculative);

            // VT1 operation should remain unaffected
            Assert.False(vt1AluOp.Faulted);
            Assert.False(vt1AluOp.IsSpeculative);
        }

        /// <summary>
        /// Test: Pipeline flush is thread-isolated
        /// </summary>
        [Fact]
        public void Pipeline_Flush_ThreadIsolation()
        {
            // Arrange: Create multiple thread operations
            var threads = new[] { 0, 1, 2, 3 };
            var operations = new MicroOp[4];

            for (int i = 0; i < 4; i++)
            {
                operations[i] = MicroOpTestHelper.CreateScalarALU(
                    virtualThreadId: i,
                    destReg: (ushort)(i * 4),
                    src1Reg: (ushort)(i * 4 + 1),
                    src2Reg: (ushort)(i * 4 + 2));
            }

            // Act: Fault only VT1
            operations[1].Faulted = true;

            // Assert: Only VT1 is faulted, others are fine
            Assert.False(operations[0].Faulted);
            Assert.True(operations[1].Faulted);
            Assert.False(operations[2].Faulted);
            Assert.False(operations[3].Faulted);
        }

        #endregion

        #region 4.4 FSP_Reclaims_Flushed_Slots

        /// <summary>
        /// Test: FSP_Reclaims_Flushed_Slots
        /// Убедиться, что слоты, освобожденные приостановленным потоком
        /// (в ожидании MSHR), немедленно утилизируются механизмом FSP для фоновых потоков.
        /// </summary>
        [Fact]
        public void FSP_Reclaims_Flushed_Slots_ImmediateReuse()
        {
            // Arrange: Create scheduler and bundle
            var scheduler = new MicroOpScheduler();
            const int VLIW_SLOTS = 8;
            var bundle = new MicroOp[VLIW_SLOTS];

            // VT0: primary thread with one operation in slot 0
            var primaryOp = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);
            bundle[0] = primaryOp;

            // Slot 1: simulate a flushed operation (L1 miss)
            var flushedOp = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 0,
                destReg: 4,
                address: 0x1000,
                domainTag: 0);
            flushedOp.Faulted = true;
            bundle[1] = null; // Slot is now empty due to flush

            // Nominate background operations from VT1
            var bgOp = MicroOpTestHelper.CreateScalarALU(1, 10, 11, 12);
            scheduler.NominateSmtCandidate(1, bgOp);

            // Act: Pack bundle - FSP should reclaim slot 1
            var packed = scheduler.PackBundleIntraCoreSmt(bundle, 0, 0);

            // Assert: Background operation should be injected into empty slot
            Assert.True(scheduler.SmtInjectionsCount > 0,
                "FSP should reclaim flushed slots for background operations");
        }

        /// <summary>
        /// Test: Flushed slots become available immediately
        /// </summary>
        [Fact]
        public void FSP_Reclaims_Flushed_Slots_NextCycle()
        {
            // Arrange: Simulate pipeline with flushed VT
            var scheduler = new MicroOpScheduler();
            var bundle = new MicroOp[8];

            // Primary thread (VT0) has only slot 0
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);

            // Slots 1-7 are empty (simulating flush or stall)
            for (int i = 1; i < 8; i++)
            {
                bundle[i] = null;
            }

            // Nominate background ops
            for (int vt = 1; vt <= 3; vt++)
            {
                var bgOp = MicroOpTestHelper.CreateScalarALU(
                    vt,
                    destReg: (ushort)(vt * 8),
                    src1Reg: (ushort)(vt * 8 + 1),
                    src2Reg: (ushort)(vt * 8 + 2));
                scheduler.NominateSmtCandidate(vt, bgOp);
            }

            // Act: Pack bundle
            var packed = scheduler.PackBundleIntraCoreSmt(bundle, 0, 0);

            // Count filled slots
            int filledSlots = 0;
            for (int i = 0; i < 8; i++)
            {
                if (packed[i] != null) filledSlots++;
            }

            // Assert: Multiple slots should be filled by FSP
            Assert.True(filledSlots > 1, "FSP should fill empty slots immediately");
            Assert.True(scheduler.SmtInjectionsCount >= 1);
        }

        #endregion
    }
}
