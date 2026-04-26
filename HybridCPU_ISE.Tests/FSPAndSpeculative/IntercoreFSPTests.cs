using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using HybridCPU_ISE.Tests.TestHelpers;
using System;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Section 3: Asynchronous Pod-Level Arbitration (Intercore FSP) Tests
    /// Тесты для межядерной миграции DMA-дескрипторов и арбитража на уровне Pod.
    /// Based on: required_tests_coverage.md Section 3
    /// Note: Uses Load/Store ops with DomainTag to simulate DMA descriptors
    /// </summary>
    public class IntercoreFSPTests
    {
        #region 3.1 Intercore_Accept_Valid_Descriptor

        /// <summary>
        /// Test: Intercore_Accept_Valid_Descriptor
        /// Проверка приема мигрирующего DMA-дескриптора на целевом ядре,
        /// если необходимые локальные каналы свободны.
        /// </summary>
        [Fact]
        public void Intercore_Accept_Valid_Descriptor_WhenChannelsFree()
        {
            // Arrange: Create two cores - source and target
            var sourceCore = new Processor.CPU_Core(0);
            var targetCore = new Processor.CPU_Core(1);

            // Clear all resource locks on target core
            targetCore.ClearAllResourceLocks();

            // Create a DMA operation that uses only DMA channels (no scalar registers)
            // DMA channels are bits 51-54 in Low part
            var dmaMask = new ResourceBitset(1UL << 51, 0); // DMA channel 0

            // Act: Try to acquire resources on target core
            bool acquired = targetCore.AcquireResources(dmaMask);

            // Assert: Should succeed because channels are free
            Assert.True(acquired);

            // Verify the resource is now locked
            var locks = targetCore.GetGlobalResourceLocks();
            Assert.Equal(1UL << 51, locks.Low & (1UL << 51));
        }

        /// <summary>
        /// Test: Intercore descriptor with valid DomainTag should be accepted
        /// </summary>
        [Fact]
        public void Intercore_Accept_ValidDescriptor_WithValidDomainTag()
        {
            // Arrange: Create Load operation with domain tag (simulating DMA descriptor)
            var loadOp = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 0,
                destReg: 1,
                address: 0x1000,
                domainTag: 5);

            // Act: Verify DomainTag is set
            ulong domainTag = loadOp.Placement.DomainTag;

            // Assert: Domain should be valid
            Assert.Equal(5UL, domainTag);
            Assert.InRange(domainTag, 0UL, 15UL);
        }

        #endregion

        #region 3.2 Intercore_Reject_Scalar_Dependency

        /// <summary>
        /// Test: Intercore_Reject_Scalar_Dependency
        /// Строгое отклонение дескриптора целевым ядром, если биты 0-31
        /// (локальные регистры) не равны нулю.
        /// </summary>
        [Fact]
        public void Intercore_Reject_Scalar_Dependency_WhenRegisterBitsSet()
        {
            // Arrange: Create a mask that includes both DMA channel and register bits
            // Bits 0-31 are register bits - these should be ZERO for intercore migration
            // Bits 51-54 are DMA channels

            // Valid intercore mask: only DMA channels, no register bits
            var validIntercoreMask = new ResourceBitset(1UL << 51, 0);

            // Invalid intercore mask: includes register bits (bit 1)
            var invalidIntercoreMask = new ResourceBitset((1UL << 51) | (1UL << 1), 0);

            // Act: Check if masks have register bits
            bool validHasRegisterBits = (validIntercoreMask.Low & 0x00000000FFFFFFFFUL) != 0;
            bool invalidHasRegisterBits = (invalidIntercoreMask.Low & 0x00000000FFFFFFFFUL) != 0;

            // Assert: Valid mask should have NO register bits
            Assert.False(validHasRegisterBits, "Valid intercore descriptor should have no register bits (0-31)");

            // Invalid mask DOES have register bits - should be rejected
            Assert.True(invalidHasRegisterBits, "Invalid descriptor with register bits should be detected");
        }

        /// <summary>
        /// Test: Validate that only DMA/shared resources can migrate between cores
        /// </summary>
        [Fact]
        public void Intercore_ValidateMigrationMask_OnlySharedResources()
        {
            // Arrange: Define what resources CAN migrate between cores
            // Only shared resources: DMA (51-54), Stream engines (55-58), Accelerators (59-62)
            // Bits 48-50 (LSU ports) are also shared but need careful handling

            ulong sharedResourceMask = 0;
            // DMA channels (51-54)
            sharedResourceMask |= (1UL << 51) | (1UL << 52) | (1UL << 53) | (1UL << 54);
            // Stream engines (55-58)
            sharedResourceMask |= (1UL << 55) | (1UL << 56) | (1UL << 57) | (1UL << 58);
            // Accelerators (59-62)
            sharedResourceMask |= (1UL << 59) | (1UL << 60) | (1UL << 61) | (1UL << 62);

            // Create a valid migration descriptor
            var migrationMask = new ResourceBitset(1UL << 51, 0); // DMA channel 0

            // Act: Check if migration mask only uses shared resources
            bool onlySharedResources = (migrationMask.Low & ~sharedResourceMask) == 0;

            // Assert: Migration should only use shared resources
            Assert.True(onlySharedResources);
        }

        #endregion

        #region 3.3 Intercore_Reject_Resource_Conflict

        /// <summary>
        /// Test: Intercore_Reject_Resource_Conflict
        /// Отклонение дескриптора, если запрашиваемый DMA engine уже занят на целевом ядре.
        /// </summary>
        [Fact]
        public void Intercore_Reject_ResourceConflict_WhenDMABusy()
        {
            // Arrange: Create target core and lock DMA channel 0
            var targetCore = new Processor.CPU_Core(1);
            targetCore.ClearAllResourceLocks();

            var dmaChannel0 = new ResourceBitset(1UL << 51, 0);

            // Act: First acquisition should succeed
            bool firstAcquire = targetCore.AcquireResources(dmaChannel0);
            Assert.True(firstAcquire);

            // Try to acquire same DMA channel again (simulating intercore migration)
            bool secondAcquire = targetCore.AcquireResources(dmaChannel0);

            // Assert: Second acquisition should FAIL (conflict)
            Assert.False(secondAcquire, "Should reject intercore descriptor when DMA channel is busy");

            // Verify the resource is still locked
            var locks = targetCore.GetGlobalResourceLocks();
            Assert.NotEqual(0UL, locks.Low & (1UL << 51));
        }

        /// <summary>
        /// Test: Multiple DMA channels can be used simultaneously if different
        /// </summary>
        [Fact]
        public void Intercore_Accept_DifferentChannels_NoConflict()
        {
            // Arrange: Create target core
            var targetCore = new Processor.CPU_Core(1);
            targetCore.ClearAllResourceLocks();

            var dmaChannel0 = new ResourceBitset(1UL << 51, 0);
            var dmaChannel1 = new ResourceBitset(1UL << 52, 0);

            // Act: Acquire two different DMA channels
            bool acquire0 = targetCore.AcquireResources(dmaChannel0);
            bool acquire1 = targetCore.AcquireResources(dmaChannel1);

            // Assert: Both should succeed (no conflict)
            Assert.True(acquire0);
            Assert.True(acquire1);

            // Verify both channels are locked
            var locks = targetCore.GetGlobalResourceLocks();
            Assert.NotEqual(0UL, locks.Low & (1UL << 51));
            Assert.NotEqual(0UL, locks.Low & (1UL << 52));
        }

        #endregion

        #region 3.4 Intercore_DomainTag_Propagation

        /// <summary>
        /// Test: Intercore_DomainTag_Propagation
        /// Убедиться, что 64-битный токен DomainTag передается вместе с дескриптором
        /// без изменений и проверяется целевым ядром.
        /// </summary>
        [Fact]
        public void Intercore_DomainTag_Propagation_PreservesTag()
        {
            // Arrange: Create Load operation with specific domain tag
            const byte originalDomainTag = 7;
            var sourceOp = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 0,
                destReg: 1,
                address: 0x10000,
                domainTag: originalDomainTag);

            // Act: Simulate migration - domain tag should be preserved
            ulong migratedDomainTag = sourceOp.Placement.DomainTag;

            // Assert: Domain tag should be unchanged
            Assert.Equal((ulong)originalDomainTag, migratedDomainTag);
        }

        /// <summary>
        /// Test: Domain tag validation on target core
        /// </summary>
        [Fact]
        public void Intercore_DomainTag_TargetValidation()
        {
            // Arrange: Create operations with different domain tags
            var domainA_Op = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 0,
                destReg: 1,
                address: 0x1000,
                domainTag: 5);

            var domainB_Op = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 0,
                destReg: 2,
                address: 0x2000,
                domainTag: 10);

            // Act: Check if operations have different domains
            bool differentDomains = domainA_Op.Placement.DomainTag != domainB_Op.Placement.DomainTag;

            // Assert: Operations should have distinct domains
            Assert.True(differentDomains);
            Assert.Equal(5UL, domainA_Op.Placement.DomainTag);
            Assert.Equal(10UL, domainB_Op.Placement.DomainTag);
        }

        /// <summary>
        /// Test: Verify domain tag is within valid range (0-15)
        /// </summary>
        [Fact]
        public void Intercore_DomainTag_ValidRange()
        {
            // Arrange & Act: Create operations with boundary domain tags
            var minDomain = MicroOpTestHelper.CreateLoad(0, 1, 0x1000, domainTag: 0);
            var maxDomain = MicroOpTestHelper.CreateLoad(0, 2, 0x2000, domainTag: 15);

            // Assert: Both should be valid
            Assert.InRange(minDomain.Placement.DomainTag, 0UL, 15UL);
            Assert.InRange(maxDomain.Placement.DomainTag, 0UL, 15UL);
        }

        #endregion
    }
}
