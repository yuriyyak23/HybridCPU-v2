using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;
using System;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Tests for Phase 5 Singularity-style memory domain isolation.
    /// Validates thread domain allocation, isolation verification, and formal proofs.
    /// </summary>
    public class MemoryDomainTests
    {
        [Fact]
        public void AllocateDomain_ValidThreadId_ShouldSucceed()
        {
            // Arrange
            IOMMU.Initialize();
            int threadId = 5;
            ulong size = 1024 * 1024; // 1MB

            // Act
            bool allocated = IOMMU.AllocateDomain(threadId, size, MemoryDomainFlags.ReadWrite);

            // Assert
            Assert.True(allocated);

            // Verify domain exists
            bool domainExists = IOMMU.GetDomain(threadId, out var domain);
            Assert.True(domainExists);
            Assert.Equal(threadId, domain.ThreadId);
            Assert.Equal(size, domain.Size);
            Assert.True(domain.Flags.HasFlag(MemoryDomainFlags.Read));
            Assert.True(domain.Flags.HasFlag(MemoryDomainFlags.Write));
        }

        [Fact]
        public void AllocateDomain_DuplicateThreadId_ShouldFail()
        {
            // Arrange
            IOMMU.Initialize();
            int threadId = 3;
            ulong size = 1024 * 1024; // 1MB

            // Act
            bool firstAlloc = IOMMU.AllocateDomain(threadId, size, MemoryDomainFlags.ReadWrite);
            bool secondAlloc = IOMMU.AllocateDomain(threadId, size, MemoryDomainFlags.ReadWrite);

            // Assert
            Assert.True(firstAlloc);
            Assert.False(secondAlloc); // Should reject duplicate allocation
        }

        [Fact]
        public void CheckDomainAccess_AddressWithinDomain_ShouldAllow()
        {
            // Arrange
            IOMMU.Initialize();
            int threadId = 7;
            ulong size = 1024 * 1024; // 1MB
            IOMMU.AllocateDomain(threadId, size, MemoryDomainFlags.ReadWrite);

            bool gotDomain = IOMMU.GetDomain(threadId, out var domain);
            Assert.True(gotDomain);

            // Act - read from middle of domain
            var (allowed, reason) = IOMMU.CheckDomainAccess(
                threadId,
                domain.BaseAddress + 512 * 1024, // middle
                1024, // 1KB read
                isWrite: false
            );

            // Assert
            Assert.True(allowed);
            Assert.Empty(reason);
        }

        [Fact]
        public void CheckDomainAccess_AddressOutsideDomain_ShouldDeny()
        {
            // Arrange
            IOMMU.Initialize();
            int threadId = 9;
            ulong size = 1024 * 1024; // 1MB
            IOMMU.AllocateDomain(threadId, size, MemoryDomainFlags.ReadWrite);

            bool gotDomain = IOMMU.GetDomain(threadId, out var domain);
            Assert.True(gotDomain);

            // Act - read beyond domain end
            var (allowed, reason) = IOMMU.CheckDomainAccess(
                threadId,
                domain.BaseAddress + domain.Size, // just past end
                1024,
                isWrite: false
            );

            // Assert
            Assert.False(allowed);
            Assert.NotEmpty(reason);
            Assert.Contains("outside domain", reason);
        }

        [Fact]
        public void CheckDomainAccess_WriteToReadOnlyDomain_ShouldDeny()
        {
            // Arrange
            IOMMU.Initialize();
            int threadId = 11;
            ulong size = 1024 * 1024; // 1MB
            IOMMU.AllocateDomain(threadId, size, MemoryDomainFlags.Read); // Read-only

            bool gotDomain = IOMMU.GetDomain(threadId, out var domain);
            Assert.True(gotDomain);

            // Act - attempt write
            var (allowed, reason) = IOMMU.CheckDomainAccess(
                threadId,
                domain.BaseAddress,
                1024,
                isWrite: true
            );

            // Assert
            Assert.False(allowed);
            Assert.NotEmpty(reason);
            Assert.Contains("Write access denied", reason);
        }

        [Fact]
        public void VerifyDomainIsolation_NonOverlappingDomains_ShouldSucceed()
        {
            // Arrange
            IOMMU.Initialize();

            // Allocate domains for multiple threads
            for (int i = 0; i < 4; i++)
            {
                bool allocated = IOMMU.AllocateDomain(i, 1024 * 1024, MemoryDomainFlags.ReadWrite);
                Assert.True(allocated);
            }

            // Act
            bool isolated = IOMMU.VerifyDomainIsolation();

            // Assert
            Assert.True(isolated);
        }

        [Fact]
        public void GetDomainStatistics_MultipleDomains_ShouldReturnCorrectCounts()
        {
            // Arrange
            IOMMU.Initialize();

            // Allocate 3 domains with different sizes
            IOMMU.AllocateDomain(0, 1 * 1024 * 1024, MemoryDomainFlags.ReadWrite); // 1MB
            IOMMU.AllocateDomain(1, 2 * 1024 * 1024, MemoryDomainFlags.ReadWrite); // 2MB
            IOMMU.AllocateDomain(2, 4 * 1024 * 1024, MemoryDomainFlags.ReadWrite); // 4MB

            // Act
            var (totalDomains, totalAllocated, largestDomain) = IOMMU.GetDomainStatistics();

            // Assert
            Assert.Equal(3, totalDomains);
            Assert.Equal(7UL * 1024 * 1024, totalAllocated); // 1+2+4 MB
            Assert.Equal(4UL * 1024 * 1024, largestDomain); // 4MB
        }

        [Fact]
        public void FreeDomain_ExistingDomain_ShouldSucceed()
        {
            // Arrange
            IOMMU.Initialize();
            int threadId = 13;
            IOMMU.AllocateDomain(threadId, 1024 * 1024, MemoryDomainFlags.ReadWrite);

            // Act
            bool freed = IOMMU.FreeDomain(threadId);

            // Assert
            Assert.True(freed);

            // Verify domain no longer exists
            bool domainExists = IOMMU.GetDomain(threadId, out _);
            Assert.False(domainExists);
        }

        [Fact]
        public void GenerateIsolationProof_MultipleDomains_ShouldGenerateValidSMT()
        {
            // Arrange
            IOMMU.Initialize();

            // Allocate 3 domains
            IOMMU.AllocateDomain(0, 1024 * 1024, MemoryDomainFlags.ReadWrite);
            IOMMU.AllocateDomain(1, 1024 * 1024, MemoryDomainFlags.ReadWrite);
            IOMMU.AllocateDomain(2, 1024 * 1024, MemoryDomainFlags.ReadWrite);

            // Act
            string smtProof = IOMMU.GenerateIsolationProof();

            // Assert
            Assert.NotEmpty(smtProof);
            Assert.Contains("(set-logic QF_BV)", smtProof); // SMT-LIB2 header
            Assert.Contains("domain_0_base", smtProof); // Domain 0 declaration
            Assert.Contains("domain_1_base", smtProof); // Domain 1 declaration
            Assert.Contains("domain_2_base", smtProof); // Domain 2 declaration
            Assert.Contains("(check-sat)", smtProof); // Satisfiability check
            Assert.Contains("bvuge", smtProof); // Non-overlap assertion
        }

        [Fact]
        public void MemoryDomain_Contains_ShouldValidateRangeCorrectly()
        {
            // Arrange
            var domain = new MemoryDomain
            {
                ThreadId = 0,
                BaseAddress = 0x10000000,
                Size = 0x100000, // 1MB
                Flags = MemoryDomainFlags.ReadWrite
            };

            // Act & Assert - address at start
            Assert.True(domain.Contains(0x10000000, 1024));

            // Address in middle
            Assert.True(domain.Contains(0x10080000, 1024));

            // Address at end boundary (should fit exactly)
            Assert.True(domain.Contains(0x100FFC00, 1024)); // Last 1KB

            // Address just past end (should fail)
            Assert.False(domain.Contains(0x10100000, 1024));

            // Address before start (should fail)
            Assert.False(domain.Contains(0x0FFFFFFF, 1024));
        }

        [Fact]
        public void MemoryDomain_Overlaps_ShouldDetectOverlapCorrectly()
        {
            // Arrange
            var domain = new MemoryDomain
            {
                ThreadId = 0,
                BaseAddress = 0x10000000,
                Size = 0x100000, // 1MB
                Flags = MemoryDomainFlags.ReadWrite
            };

            // Act & Assert - range completely before (no overlap)
            Assert.False(domain.Overlaps(0x0FF00000, 0x100000));

            // Range completely after (no overlap)
            Assert.False(domain.Overlaps(0x10100000, 0x100000));

            // Range overlaps start
            Assert.True(domain.Overlaps(0x0FFF0000, 0x20000));

            // Range overlaps end
            Assert.True(domain.Overlaps(0x100F0000, 0x20000));

            // Range completely contained
            Assert.True(domain.Overlaps(0x10080000, 0x1000));

            // Range completely contains domain
            Assert.True(domain.Overlaps(0x0FF00000, 0x300000));
        }
    }
}
