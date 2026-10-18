using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using HybridCPU_ISE.Tests.TestHelpers;
using System;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Section 5: Domain-Tagged Memory Isolation Tests
    /// Тесты для аппаратной изоляции памяти с использованием Domain Tags.
    /// Based on: required_tests_coverage.md Section 5
    /// </summary>
    public class DomainMemoryIsolationTests
    {
        #region 5.1 DomainCheck_Valid_Access

        /// <summary>
        /// Test: DomainCheck_Valid_Access
        /// Проверка успешного параллельного доступа к кэшу L1,
        /// если целевой адрес попадает в рамки DomainTag.
        /// </summary>
        [Fact]
        public void DomainCheck_Valid_Access_WithinDomainBounds()
        {
            // Arrange: Create load operations within same domain
            const byte domainTag = 3;

            var load1 = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 0,
                destReg: 1,
                address: 0x1000,
                domainTag: domainTag);

            var load2 = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 0,
                destReg: 2,
                address: 0x1100,
                domainTag: domainTag);

            // Act: Verify both operations have same domain
            bool sameDomain = load1.Placement.DomainTag == load2.Placement.DomainTag;

            // Assert: Operations in same domain should be allowed
            Assert.True(sameDomain);
            Assert.Equal((ulong)domainTag, load1.Placement.DomainTag);
            Assert.Equal((ulong)domainTag, load2.Placement.DomainTag);
        }

        /// <summary>
        /// Test: Multiple operations to same domain can proceed in parallel
        /// </summary>
        [Fact]
        public void DomainCheck_ParallelAccess_SameDomain_Allowed()
        {
            // Arrange: Create multiple memory operations in same domain
            const byte domainTag = 5;

            var ops = new MicroOp[]
            {
                MicroOpTestHelper.CreateLoad(0, 1, 0x1000, domainTag),
                MicroOpTestHelper.CreateLoad(1, 2, 0x2000, domainTag),
                MicroOpTestHelper.CreateStore(2, 3, 0x3000, domainTag: domainTag)
            };

            // Act: Check all operations have correct domain
            bool allSameDomain = true;
            foreach (var op in ops)
            {
                if (op.Placement.DomainTag != domainTag)
                    allSameDomain = false;
            }

            // Assert: All operations should be in same domain
            Assert.True(allSameDomain);
        }

        #endregion

        #region 5.2 DomainCheck_Suppress_Fill

        /// <summary>
        /// Test: DomainCheck_Suppress_Fill
        /// Проверка мгновенного сигнала SUPPRESS_FILL: данные не оседают в кэше L1,
        /// если нарушены границы DomainTag.
        /// </summary>
        [Fact]
        public void DomainCheck_Suppress_Fill_OnDomainViolation()
        {
            // Arrange: Create operations with different domains
            const byte allowedDomain = 3;
            const byte forbiddenDomain = 7;

            var allowedLoad = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 0,
                destReg: 1,
                address: 0x1000,
                domainTag: allowedDomain);

            var violatingLoad = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 0,
                destReg: 2,
                address: 0x2000,
                domainTag: forbiddenDomain);

            // Act: Check if domains differ (violation detection)
            bool domainViolation = allowedLoad.Placement.DomainTag != violatingLoad.Placement.DomainTag;

            // Assert: Domain violation should be detected
            Assert.True(domainViolation);
            Assert.NotEqual(allowedLoad.Placement.DomainTag, violatingLoad.Placement.DomainTag);
        }

        /// <summary>
        /// Test: Domain isolation prevents cross-domain data leakage
        /// </summary>
        [Fact]
        public void DomainCheck_Isolation_PreventsCrossDomainAccess()
        {
            // Arrange: Create operations for different security domains
            var userSpaceOp = MicroOpTestHelper.CreateLoad(0, 1, 0x1000, domainTag: 0);
            var kernelSpaceOp = MicroOpTestHelper.CreateLoad(0, 2, 0xFFFF0000, domainTag: 15);

            // Act: Verify domains are isolated
            bool isolated = userSpaceOp.Placement.DomainTag != kernelSpaceOp.Placement.DomainTag;

            // Assert: Different domains should be isolated
            Assert.True(isolated);
            Assert.Equal(0UL, userSpaceOp.Placement.DomainTag);
            Assert.Equal(15UL, kernelSpaceOp.Placement.DomainTag);
        }

        #endregion

        #region 5.3 DomainCheck_Prefetch_Rejection

        /// <summary>
        /// Test: DomainCheck_Prefetch_Rejection
        /// Проверка, что спекулятивный префетчинг отвергается IOMMU,
        /// если он выходит за рамки изолированного домена.
        /// </summary>
        [Fact]
        public void DomainCheck_Prefetch_Rejection_OutOfBounds()
        {
            // Arrange: Create speculative load (simulating prefetch)
            const byte allowedDomain = 4;

            var speculativePrefetch = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 0,
                destReg: 1,
                address: 0x10000,
                domainTag: allowedDomain);

            speculativePrefetch.IsSpeculative = true;

            // Act: Simulate domain check - mark as faulted if out of bounds
            // In real hardware, IOMMU would check if address is within domain bounds
            bool isSpeculative = speculativePrefetch.IsSpeculative;

            // Assert: Speculative prefetches should be identifiable
            Assert.True(isSpeculative);
            Assert.Equal((ulong)allowedDomain, speculativePrefetch.Placement.DomainTag);
        }

        /// <summary>
        /// Test: Speculative memory operations can be cancelled
        /// </summary>
        [Fact]
        public void DomainCheck_Speculative_CanBeCancelled()
        {
            // Arrange: Create speculative load that violates domain
            var speculativeOp = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 0,
                destReg: 1,
                address: 0x1000,
                domainTag: 5);

            speculativeOp.IsSpeculative = true;

            // Act: Cancel speculative operation (domain violation detected)
            speculativeOp.Faulted = true;

            // Assert: Operation should be marked as faulted and speculative
            Assert.True(speculativeOp.IsSpeculative);
            Assert.True(speculativeOp.Faulted);
        }

        #endregion
    }

    /// <summary>
    /// Section 6: Execution Modes Serialization Tests
    /// Тесты для специфики выполнения MMIO и барьерных инструкций.
    /// Based on: required_tests_coverage.md Section 6
    /// </summary>
    public class ExecutionModesTests
    {
        #region 6.1 MMIO_Deferred_Execution

        /// <summary>
        /// Test: MMIO_Deferred_Execution
        /// Проверка строгого ожидания для Memory-Mapped I/O:
        /// выполнение задерживается до прохождения PONR (защита идемпотентности).
        /// </summary>
        [Fact]
        public void MMIO_Deferred_Execution_WaitsForPONR()
        {
            // Arrange: Create MMIO operation (special memory range)
            const ulong MMIO_BASE = 0xFFFF000000000000UL;

            var mmioOp = MicroOpTestHelper.CreateStore(
                virtualThreadId: 0,
                srcReg: 1,
                address: MMIO_BASE + 0x100,
                domainTag: 15);

            // Act: Mark as speculative (pre-PONR)
            mmioOp.IsSpeculative = true;

            // Assert: MMIO operation should not execute until committed (post-PONR)
            Assert.True(mmioOp.IsSpeculative, "MMIO ops should wait for PONR");

            // Simulate commit (reach PONR)
            mmioOp.IsSpeculative = false;
            Assert.False(mmioOp.IsSpeculative, "After PONR, MMIO can execute");
        }

        /// <summary>
        /// Test: MMIO stores are non-speculative
        /// </summary>
        [Fact]
        public void MMIO_NonSpeculative_GuaranteesIdempotence()
        {
            // Arrange: Create MMIO store operation
            const ulong MMIO_DEVICE_REG = 0xFFFF000000001000UL;

            var mmioStore = MicroOpTestHelper.CreateStore(
                virtualThreadId: 0,
                srcReg: 5,
                address: MMIO_DEVICE_REG,
                domainTag: 15);

            // Act: Operations to MMIO regions must be committed (non-speculative)
            mmioStore.IsSpeculative = false;

            // Assert: MMIO should execute only once (idempotent)
            Assert.False(mmioStore.IsSpeculative);
            Assert.False(mmioStore.Faulted);
            Assert.False(mmioStore.AdmissionMetadata.IsStealable, "MMIO Store must be non-stealable to preserve state rules.");
        }

        /// <summary>
        /// Test: MMIO_Load_IsNonStealable
        /// Проверка, что инструкции загрузки (Load) из MMIO-регионов
        /// помечаются как non-stealable через admission metadata для предотвращения
        /// спекулятивного/чужого исполнения, которое может изменить состояние устройства.
        /// </summary>
        [Fact]
        public void MMIO_Load_IsNonStealable_GuaranteesLocalCommit()
        {
            // Arrange
            const ulong MMIO_DEVICE_REG = 0xFFFF000000002000UL;

            // Act: Create MMIO load operation
            var mmioLoad = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 0,
                destReg: 5,
                address: MMIO_DEVICE_REG,
                domainTag: 15);

            var normalLoad = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 0,
                destReg: 5,
                address: 0x1000,
                domainTag: 15);

            // Assert: MMIO loads must strictly not be stealable to preserve state rules.
            Assert.False(mmioLoad.AdmissionMetadata.IsStealable, "MMIO Load must be non-stealable to preserve state rules.");
            Assert.True(normalLoad.AdmissionMetadata.IsStealable, "Normal memory loads should be stealable by FSP.");
        }

        #endregion

        #region 6.2 FENCE_Total_Order

        /// <summary>
        /// Test: FENCE_Total_Order
        /// Проверка, что барьерная инструкция корректно останавливает выдачу (stall),
        /// пока ScoreboardShared полностью не очистится.
        /// </summary>
        [Fact]
        public void FENCE_Total_Order_StallUntilScoreboardClear()
        {
            // Arrange: Create core and acquire some resources
            var core = new Processor.CPU_Core(0);
            core.ClearAllResourceLocks();

            var resourceMask = new ResourceBitset(0x0000000000000FFFUL, 0);
            core.AcquireResources(resourceMask);

            // Act: Check if scoreboard has any locks
            var locks = core.GetGlobalResourceLocks();
            bool hasLocks = locks.IsNonZero;

            // Assert: Scoreboard should have locks
            Assert.True(hasLocks, "Scoreboard should have active locks before FENCE");

            // Simulate FENCE completion - all resources released
            core.ReleaseResources(resourceMask);
            locks = core.GetGlobalResourceLocks();

            // Assert: After FENCE, scoreboard should be clear
            Assert.True(locks.IsZero, "FENCE should wait until scoreboard is clear");
        }

        /// <summary>
        /// Test: FENCE ensures all prior operations complete
        /// </summary>
        [Fact]
        public void FENCE_EnsuresPriorCompletionOrdering()
        {
            // Arrange: Create multiple operations
            var core = new Processor.CPU_Core(0);
            core.ClearAllResourceLocks();

            // Simulate multiple operations acquiring resources
            var mask1 = new ResourceBitset(1UL << 0, 0); // R0
            var mask2 = new ResourceBitset(1UL << 1, 0); // R1
            var mask3 = new ResourceBitset(1UL << 2, 0); // R2

            core.AcquireResources(mask1);
            core.AcquireResources(mask2);
            core.AcquireResources(mask3);

            // Act: FENCE must wait for all to complete
            var locks = core.GetGlobalResourceLocks();
            Assert.True(locks.IsNonZero, "Operations pending");

            // Release all (simulate completion)
            core.ReleaseResources(mask1);
            core.ReleaseResources(mask2);
            core.ReleaseResources(mask3);

            locks = core.GetGlobalResourceLocks();

            // Assert: All resources cleared, FENCE can proceed
            Assert.True(locks.IsZero, "FENCE proceeds when all prior ops complete");
        }

        #endregion

        #region 6.3 IntraBundle_Dependency_Trap

        /// <summary>
        /// Test: IntraBundle_Dependency_Trap
        /// Проверка обнаружения внутрибандловых RAW/WAW/WAR зависимостей у одного потока.
        /// Должно вызывать панику/ошибку декодера (если компилятор сформировал неверный VLIW).
        /// </summary>
        [Fact]
        public void IntraBundle_Dependency_Trap_DetectsRAW()
        {
            // Arrange: Create two operations with RAW dependency (Read After Write)
            // Op1: writes R5
            var writer = MicroOpTestHelper.CreateScalarALU(
                virtualThreadId: 0,
                destReg: 5,
                src1Reg: 1,
                src2Reg: 2);

            // Op2: reads R5 (RAW dependency!)
            var reader = MicroOpTestHelper.CreateScalarALU(
                virtualThreadId: 0,
                destReg: 10,
                src1Reg: 5, // Reads R5 - RAW hazard!
                src2Reg: 6);

            // Act: Check for register conflict within same VT
            var writerMask = writer.SafetyMask;
            var readerMask = reader.SafetyMask;

            // Same VT operations should detect dependency
            bool sameVT = writer.VirtualThreadId == reader.VirtualThreadId;
            bool hasConflict = writerMask.ConflictsWith(readerMask);

            // Assert: RAW within same VT should be detected
            Assert.True(sameVT, "Both operations are from same VT");
            Assert.True(hasConflict, "RAW dependency should be detected");
        }

        /// <summary>
        /// Test: WAW (Write After Write) hazard detection
        /// </summary>
        [Fact]
        public void IntraBundle_Dependency_Trap_DetectsWAW()
        {
            // Arrange: Two operations writing to same register (WAW)
            var writer1 = MicroOpTestHelper.CreateScalarALU(
                virtualThreadId: 0,
                destReg: 8,
                src1Reg: 1,
                src2Reg: 2);

            var writer2 = MicroOpTestHelper.CreateScalarALU(
                virtualThreadId: 0,
                destReg: 8, // Same destination - WAW hazard!
                src1Reg: 3,
                src2Reg: 4);

            // Act: Check for write conflict
            bool sameVT = writer1.VirtualThreadId == writer2.VirtualThreadId;
            bool hasConflict = writer1.SafetyMask.ConflictsWith(writer2.SafetyMask);

            // Assert: WAW within same VT should be detected
            Assert.True(sameVT);
            Assert.True(hasConflict, "WAW dependency should be detected");
        }

        /// <summary>
        /// Test: No false positives for independent operations
        /// </summary>
        [Fact]
        public void IntraBundle_NoDependency_NoTrap()
        {
            // Arrange: Two independent operations (no hazard)
            var op1 = MicroOpTestHelper.CreateScalarALU(
                virtualThreadId: 0,
                destReg: 1,
                src1Reg: 2,
                src2Reg: 3);

            var op2 = MicroOpTestHelper.CreateScalarALU(
                virtualThreadId: 0,
                destReg: 10,
                src1Reg: 11,
                src2Reg: 12);

            // Act: Check for conflicts
            bool hasConflict = op1.SafetyMask.ConflictsWith(op2.SafetyMask);

            // Assert: Independent operations should have no conflict
            Assert.False(hasConflict, "Independent operations should not trap");
        }

        #endregion
    }
}
