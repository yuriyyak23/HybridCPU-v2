using System;
using System.Collections.Generic;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Tests for Phase 5 extensions: ResizeDomain, Shared Memory, and Configurable Sizes
    /// </summary>
    public class Phase5ExtensionsTests
    {
        public Phase5ExtensionsTests()
        {
            // Reset IOMMU state before each test
            IOMMU.Initialize();
        }

        #region ResizeDomain Tests

        [Fact]
        public void ResizeDomain_ShrinkDomain_ShouldSucceed()
        {
            // Arrange
            IOMMU.Initialize();
            int threadId = 5;
            ulong originalSize = 32 * 1024 * 1024; // 32MB
            ulong newSize = 16 * 1024 * 1024; // 16MB

            IOMMU.AllocateDomain(threadId, originalSize, MemoryDomainFlags.ReadWrite);

            // Act
            bool result = IOMMU.ResizeDomain(threadId, newSize);

            // Assert
            Assert.True(result);
            bool domainExists = IOMMU.GetDomain(threadId, out var domain);
            Assert.True(domainExists);
            Assert.Equal(newSize, domain.Size);
        }

        [Fact]
        public void ResizeDomain_GrowDomain_WithSpace_ShouldSucceed()
        {
            // Arrange
            IOMMU.Initialize();
            int threadId = 3;
            ulong originalSize = 16 * 1024 * 1024; // 16MB
            ulong newSize = 32 * 1024 * 1024; // 32MB

            IOMMU.AllocateDomain(threadId, originalSize, MemoryDomainFlags.ReadWrite);

            // Act
            bool result = IOMMU.ResizeDomain(threadId, newSize);

            // Assert
            Assert.True(result);
            bool domainExists = IOMMU.GetDomain(threadId, out var domain);
            Assert.True(domainExists);
            Assert.Equal(newSize, domain.Size);
        }

        [Fact]
        public void ResizeDomain_GrowDomain_CausesOverlap_ShouldRelocate()
        {
            // Arrange
            IOMMU.Initialize();

            // Allocate two adjacent domains
            int thread1 = 0;
            int thread2 = 1;
            ulong size1 = 16 * 1024 * 1024; // 16MB
            ulong size2 = 16 * 1024 * 1024; // 16MB

            IOMMU.AllocateDomain(thread1, size1, MemoryDomainFlags.ReadWrite);
            IOMMU.AllocateDomain(thread2, size2, MemoryDomainFlags.ReadWrite);

            var originalDomain1 = IOMMU.GetDomain(thread1, out var domain1) ? domain1 : default;

            // Act - Try to grow thread1 which would overlap with thread2
            ulong newSize = 64 * 1024 * 1024; // 64MB - will require relocation
            bool result = IOMMU.ResizeDomain(thread1, newSize);

            // Assert
            Assert.True(result); // Should succeed by relocating
            bool domainExists = IOMMU.GetDomain(thread1, out var resizedDomain);
            Assert.True(domainExists);
            Assert.Equal(newSize, resizedDomain.Size);

            // Verify no overlap with thread2
            bool thread2Exists = IOMMU.GetDomain(thread2, out var domain2After);
            Assert.True(thread2Exists);
            Assert.False(resizedDomain.Overlaps(domain2After.BaseAddress, domain2After.Size));
        }

        [Fact]
        public void ResizeDomain_NonExistentDomain_ShouldFail()
        {
            // Arrange
            IOMMU.Initialize();
            int threadId = 10;

            // Act
            bool result = IOMMU.ResizeDomain(threadId, 16 * 1024 * 1024);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region Shared Memory Tests

        [Fact]
        public void AllocateSharedRegion_WithSharedFlag_ShouldSucceed()
        {
            // Arrange
            IOMMU.Initialize();
            int sharedRegionId = 1;
            ulong size = 4 * 1024 * 1024; // 4MB
            int[] threadIds = { 0, 1, 2 };
            var flags = MemoryDomainFlags.ReadWrite | MemoryDomainFlags.Shared;

            // Act
            ulong baseAddress = IOMMU.AllocateSharedRegion(sharedRegionId, size, threadIds, flags);

            // Assert
            Assert.NotEqual(0UL, baseAddress);
        }

        [Fact]
        public void AllocateSharedRegion_WithoutSharedFlag_ShouldFail()
        {
            // Arrange
            IOMMU.Initialize();
            int sharedRegionId = 2;
            ulong size = 4 * 1024 * 1024; // 4MB
            int[] threadIds = { 0, 1 };
            var flags = MemoryDomainFlags.ReadWrite; // Missing Shared flag

            // Act
            ulong baseAddress = IOMMU.AllocateSharedRegion(sharedRegionId, size, threadIds, flags);

            // Assert
            Assert.Equal(0UL, baseAddress);
        }

        [Fact]
        public void IsSharedMemory_ForSharedRegion_ShouldReturnTrue()
        {
            // Arrange
            IOMMU.Initialize();
            int sharedRegionId = 3;
            ulong size = 4 * 1024 * 1024; // 4MB
            int[] threadIds = { 0, 1 };
            var flags = MemoryDomainFlags.ReadWrite | MemoryDomainFlags.Shared;

            ulong baseAddress = IOMMU.AllocateSharedRegion(sharedRegionId, size, threadIds, flags);

            // Act
            bool isShared = IOMMU.IsSharedMemory(baseAddress + 1024);

            // Assert
            Assert.True(isShared);
        }

        [Fact]
        public void IsSharedMemory_ForPrivateDomain_ShouldReturnFalse()
        {
            // Arrange
            IOMMU.Initialize();
            int threadId = 5;
            ulong size = 16 * 1024 * 1024; // 16MB

            IOMMU.AllocateDomain(threadId, size, MemoryDomainFlags.ReadWrite);
            bool exists = IOMMU.GetDomain(threadId, out var domain);
            Assert.True(exists);

            // Act
            bool isShared = IOMMU.IsSharedMemory(domain.BaseAddress + 1024);

            // Assert
            Assert.False(isShared);
        }

        #endregion

        #region Shared Memory Lock Tests

        [Fact]
        public void SharedMemoryLock_AcquireAndRelease_ShouldWork()
        {
            // Arrange
            SharedMemoryLockManager.Initialize();
            int sharedRegionId = 1;
            ulong baseAddress = 0x20000000;
            ulong size = 4 * 1024 * 1024;

            SharedMemoryLockManager.CreateLock(sharedRegionId, baseAddress, size);

            // Act
            bool acquired = SharedMemoryLockManager.AcquireLock(sharedRegionId, threadId: 0);
            bool isLocked = SharedMemoryLockManager.IsRegionLocked(sharedRegionId);
            int owner = SharedMemoryLockManager.GetLockOwner(sharedRegionId);
            bool released = SharedMemoryLockManager.ReleaseLock(sharedRegionId, threadId: 0);
            bool isLockedAfter = SharedMemoryLockManager.IsRegionLocked(sharedRegionId);

            // Assert
            Assert.True(acquired);
            Assert.True(isLocked);
            Assert.Equal(0, owner);
            Assert.True(released);
            Assert.False(isLockedAfter);
        }

        [Fact]
        public void SharedMemoryLock_AcquireByDifferentThread_WhileLocked_ShouldFail()
        {
            // Arrange
            SharedMemoryLockManager.Initialize();
            int sharedRegionId = 2;
            SharedMemoryLockManager.CreateLock(sharedRegionId, 0x20000000, 4 * 1024 * 1024);

            // Act
            bool thread0Acquired = SharedMemoryLockManager.AcquireLock(sharedRegionId, threadId: 0);
            bool thread1Acquired = SharedMemoryLockManager.AcquireLock(sharedRegionId, threadId: 1);

            // Assert
            Assert.True(thread0Acquired);
            Assert.False(thread1Acquired);
        }

        [Fact]
        public void SharedMemoryLock_ReleaseByNonOwner_ShouldFail()
        {
            // Arrange
            SharedMemoryLockManager.Initialize();
            int sharedRegionId = 3;
            SharedMemoryLockManager.CreateLock(sharedRegionId, 0x20000000, 4 * 1024 * 1024);

            SharedMemoryLockManager.AcquireLock(sharedRegionId, threadId: 0);

            // Act
            bool released = SharedMemoryLockManager.ReleaseLock(sharedRegionId, threadId: 1);

            // Assert
            Assert.False(released);
            Assert.True(SharedMemoryLockManager.IsRegionLocked(sharedRegionId));
        }

        [Fact]
        public void SharedMemoryLock_ForceUnlock_ShouldClearOwner()
        {
            // Arrange
            SharedMemoryLockManager.Initialize();
            int sharedRegionId = 4;
            SharedMemoryLockManager.CreateLock(sharedRegionId, 0x20000000, 4 * 1024 * 1024);

            SharedMemoryLockManager.AcquireLock(sharedRegionId, threadId: 5);
            var lock_ = SharedMemoryLockManager.GetLock(sharedRegionId);

            // Act
            lock_!.ForceUnlock();

            // Assert
            Assert.False(SharedMemoryLockManager.IsRegionLocked(sharedRegionId));
            Assert.Equal(-1, SharedMemoryLockManager.GetLockOwner(sharedRegionId));
        }

        #endregion

        #region Configurable Domain Sizes Tests

        [Fact]
        public void ProcessorConfig_DefaultThreadDomainSize_ShouldBe32MB()
        {
            // Arrange & Act
            var config = ProcessorConfig.Default();

            // Assert
            Assert.Equal(32 * 1024 * 1024UL, config.ThreadDomainSize);
        }

        [Fact]
        public void ProcessorConfig_CustomThreadDomainSize_ShouldBeConfigurable()
        {
            // Arrange
            var config = new ProcessorConfig
            {
                ThreadDomainSize = 64 * 1024 * 1024 // 64MB
            };

            // Act & Assert
            Assert.Equal(64 * 1024 * 1024UL, config.ThreadDomainSize);
        }

        [Fact]
        public void ProcessorConfig_CustomThreadDomainSizes_ShouldSupportPerThreadSizes()
        {
            // Arrange
            var config = new ProcessorConfig
            {
                ThreadDomainSize = 32 * 1024 * 1024, // Default 32MB
                CustomThreadDomainSizes = new Dictionary<int, ulong>
                {
                    { 0, 64 * 1024 * 1024 }, // Thread 0: 64MB
                    { 1, 16 * 1024 * 1024 }, // Thread 1: 16MB
                    { 15, 128 * 1024 * 1024 } // Thread 15: 128MB
                }
            };

            // Act & Assert
            Assert.NotNull(config.CustomThreadDomainSizes);
            Assert.Equal(64 * 1024 * 1024UL, config.CustomThreadDomainSizes[0]);
            Assert.Equal(16 * 1024 * 1024UL, config.CustomThreadDomainSizes[1]);
            Assert.Equal(128 * 1024 * 1024UL, config.CustomThreadDomainSizes[15]);
        }

        [Fact]
        public void Processor_WithCustomDomainSizes_ShouldAllocateCorrectly()
        {
            // Arrange
            var config = new ProcessorConfig
            {
                ThreadDomainSize = 16 * 1024 * 1024, // Default 16MB
                CustomThreadDomainSizes = new Dictionary<int, ulong>
                {
                    { 0, 64 * 1024 * 1024 }, // Thread 0: 64MB
                    { 5, 32 * 1024 * 1024 }  // Thread 5: 32MB
                }
            };

            // Act
            var processor = new Processor(ProcessorMode.Emulation, config);

            // Assert
            bool thread0Exists = IOMMU.GetDomain(0, out var domain0);
            bool thread5Exists = IOMMU.GetDomain(5, out var domain5);
            bool thread10Exists = IOMMU.GetDomain(10, out var domain10);

            Assert.True(thread0Exists);
            Assert.Equal(64 * 1024 * 1024UL, domain0.Size);

            Assert.True(thread5Exists);
            Assert.Equal(32 * 1024 * 1024UL, domain5.Size);

            Assert.True(thread10Exists);
            Assert.Equal(16 * 1024 * 1024UL, domain10.Size); // Uses default
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void Integration_SharedMemoryWithLocks_MultithreadScenario()
        {
            // Arrange
            IOMMU.Initialize();
            SharedMemoryLockManager.Initialize();

            int sharedRegionId = 10;
            ulong size = 4 * 1024 * 1024;
            int[] threads = { 0, 1, 2 };
            var flags = MemoryDomainFlags.ReadWrite | MemoryDomainFlags.Shared;

            ulong baseAddress = IOMMU.AllocateSharedRegion(sharedRegionId, size, threads, flags);
            SharedMemoryLockManager.CreateLock(sharedRegionId, baseAddress, size);

            // Act - Simulate thread 0 accessing shared memory
            bool thread0Lock = SharedMemoryLockManager.AcquireLock(sharedRegionId, 0);
            bool isShared = IOMMU.IsSharedMemory(baseAddress);

            // Thread 1 tries to access while thread 0 holds lock
            bool thread1Lock = SharedMemoryLockManager.AcquireLock(sharedRegionId, 1);

            // Thread 0 releases
            SharedMemoryLockManager.ReleaseLock(sharedRegionId, 0);

            // Thread 1 tries again
            bool thread1LockRetry = SharedMemoryLockManager.AcquireLock(sharedRegionId, 1);

            // Assert
            Assert.True(thread0Lock);
            Assert.True(isShared);
            Assert.False(thread1Lock); // Should fail while thread 0 holds lock
            Assert.True(thread1LockRetry); // Should succeed after thread 0 releases
        }

        #endregion
    }
}
