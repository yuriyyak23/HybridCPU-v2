using Xunit;
using YAKSys_Hybrid_CPU.Execution;
using System;
using System.Linq;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Unit tests for BurstPlanner optimizations.
    /// Tests Plan2D and PlanIndexed methods for efficient burst planning.
    /// </summary>
    public class BurstPlannerTests
    {
        [Fact]
        public void Test_Plan_Basic_ReturnsSegments()
        {
            // Arrange
            ulong baseAddr = 0x1000;
            ulong totalBytes = 100;

            // Act
            var segments = BurstPlanner.Plan(baseAddr, totalBytes).ToList();

            // Assert
            Assert.NotEmpty(segments);
            Assert.Equal(baseAddr, segments[0].Address);

            // Verify total bytes
            ulong totalPlanned = (ulong)segments.Sum(s => s.Length);
            Assert.Equal(totalBytes, totalPlanned);
        }

        [Fact]
        public void Test_Plan_Crosses4KBBoundary_SplitsSegments()
        {
            // Arrange
            ulong baseAddr = 0x0FFE; // 2 bytes before 4KB boundary
            ulong totalBytes = 8;    // Will cross boundary

            // Act
            var segments = BurstPlanner.Plan(baseAddr, totalBytes).ToList();

            // Assert
            Assert.True(segments.Count >= 2, "Should split at 4KB boundary");

            // First segment should be 2 bytes (to boundary)
            Assert.Equal(2, segments[0].Length);
            Assert.Equal(baseAddr, segments[0].Address);

            // Second segment should start at boundary
            Assert.Equal(0x1000UL, segments[1].Address);
            Assert.Equal(6, segments[1].Length);

            // Verify total bytes
            ulong totalPlanned = (ulong)segments.Sum(s => s.Length);
            Assert.Equal(totalBytes, totalPlanned);
        }

        [Fact]
        public void Test_Plan2D_FullyContiguous_UsesOneDimensionalPlanning()
        {
            // Arrange
            ulong baseAddr = 0x2000;
            ulong elementCount = 12; // 3 rows x 4 cols
            int elementSize = 4;
            uint rowLength = 4;
            ushort rowStride = 16; // 4 elements * 4 bytes
            ushort colStride = 4;  // Contiguous

            // Act
            var segments = BurstPlanner.Plan2D(baseAddr, elementCount, elementSize,
                                               rowLength, rowStride, colStride).ToList();

            // Assert
            Assert.NotEmpty(segments);

            // Should be treated as fully contiguous (48 bytes total)
            ulong totalBytes = (ulong)segments.Sum(s => s.Length);
            Assert.Equal(elementCount * (ulong)elementSize, totalBytes);
        }

        [Fact]
        public void Test_Plan2D_ContiguousRows_PlansPerRow()
        {
            // Arrange
            ulong baseAddr = 0x3000;
            ulong elementCount = 12; // 3 rows x 4 cols
            int elementSize = 4;
            uint rowLength = 4;
            ushort rowStride = 32;  // 32 bytes between rows (gap of 16 bytes)
            ushort colStride = 4;   // Contiguous within row

            // Act
            var segments = BurstPlanner.Plan2D(baseAddr, elementCount, elementSize,
                                               rowLength, rowStride, colStride).ToList();

            // Assert
            Assert.NotEmpty(segments);

            // Should plan each row separately
            // Each row is 16 bytes (4 elements * 4 bytes)
            ulong totalBytes = (ulong)segments.Sum(s => s.Length);
            Assert.Equal(elementCount * (ulong)elementSize, totalBytes);
        }

        [Fact]
        public void Test_Plan2D_NonContiguousColumns_ReturnsElementByElement()
        {
            // Arrange
            ulong baseAddr = 0x4000;
            ulong elementCount = 8; // 2 rows x 4 cols
            int elementSize = 4;
            uint rowLength = 4;
            ushort rowStride = 16;
            ushort colStride = 8;  // Non-contiguous (gap between columns)

            // Act
            var segments = BurstPlanner.Plan2D(baseAddr, elementCount, elementSize,
                                               rowLength, rowStride, colStride).ToList();

            // Assert
            Assert.NotEmpty(segments);

            // Should return one segment per element (8 segments)
            // Since columns are not contiguous, each element is accessed individually
            Assert.True(segments.Count >= (int)elementCount || segments.All(s => s.Length == elementSize),
                       "Non-contiguous access should return individual element segments");
        }

        [Fact]
        public void Test_PlanIndexed_SmallCount_ReturnsElementByElement()
        {
            // Arrange
            ulong baseAddr = 0x5000;
            int elementSize = 4;
            ulong[] indices = { 0, 1, 2 }; // Only 3 elements
            bool indexIsByteOffset = false;

            // Act
            var segments = BurstPlanner.PlanIndexed(baseAddr, elementSize, indices, indexIsByteOffset).ToList();

            // Assert
            Assert.Equal(3, segments.Count);

            // Each should be a single element
            foreach (var segment in segments)
            {
                Assert.Equal(elementSize, segment.Length);
            }
        }

        [Fact]
        public void Test_PlanIndexed_ContiguousIndices_GroupsIntoBursts()
        {
            // Arrange
            ulong baseAddr = 0x6000;
            int elementSize = 4;
            ulong[] indices = { 0, 1, 2, 3, 4, 5, 6, 7 }; // Contiguous sequence
            bool indexIsByteOffset = false;

            // Act
            var segments = BurstPlanner.PlanIndexed(baseAddr, elementSize, indices, indexIsByteOffset).ToList();

            // Assert
            Assert.NotEmpty(segments);

            // Should group contiguous accesses into fewer segments than elements
            ulong totalBytes = (ulong)segments.Sum(s => s.Length);
            Assert.Equal((ulong)indices.Length * (ulong)elementSize, totalBytes);

            // With fully contiguous indices, should create one or very few segments
            // (may be split by 4KB boundaries)
            Assert.True(segments.Count < indices.Length,
                       "Contiguous indices should be grouped into bursts");
        }

        [Fact]
        public void Test_PlanIndexed_MixedPattern_OptimizesContiguousRuns()
        {
            // Arrange
            ulong baseAddr = 0x7000;
            int elementSize = 4;
            // Mixed: contiguous run (0-4), gap, contiguous run (10-12)
            ulong[] indices = { 0, 1, 2, 3, 4, 10, 11, 12 };
            bool indexIsByteOffset = false;

            // Act
            var segments = BurstPlanner.PlanIndexed(baseAddr, elementSize, indices, indexIsByteOffset).ToList();

            // Assert
            Assert.NotEmpty(segments);

            // Total bytes should match all elements
            ulong totalBytes = (ulong)segments.Sum(s => s.Length);
            Assert.Equal((ulong)indices.Length * (ulong)elementSize, totalBytes);

            // Should have fewer segments than elements due to grouping
            Assert.True(segments.Count < indices.Length,
                       "Should group contiguous runs into bursts");
        }

        [Fact]
        public void Test_PlanIndexed_ByteOffset_CalculatesCorrectAddresses()
        {
            // Arrange
            ulong baseAddr = 0x8000;
            int elementSize = 8;
            ulong[] indices = { 0, 8, 16, 24 }; // Byte offsets for contiguous elements
            bool indexIsByteOffset = true;

            // Act
            var segments = BurstPlanner.PlanIndexed(baseAddr, elementSize, indices, indexIsByteOffset).ToList();

            // Assert
            Assert.NotEmpty(segments);

            // Should group into burst since byte offsets are contiguous
            ulong totalBytes = (ulong)segments.Sum(s => s.Length);
            Assert.Equal((ulong)indices.Length * (ulong)elementSize, totalBytes);
        }

        [Fact]
        public void Test_PlanIndexed_RandomIndices_HandlesFragmentation()
        {
            // Arrange
            ulong baseAddr = 0x9000;
            int elementSize = 4;
            ulong[] indices = { 0, 5, 2, 9, 1 }; // Random, non-sorted
            bool indexIsByteOffset = false;

            // Act
            var segments = BurstPlanner.PlanIndexed(baseAddr, elementSize, indices, indexIsByteOffset).ToList();

            // Assert
            Assert.NotEmpty(segments);

            // Total bytes should match all elements
            ulong totalBytes = (ulong)segments.Sum(s => s.Length);
            Assert.Equal((ulong)indices.Length * (ulong)elementSize, totalBytes);

            // Note: PlanIndexed sorts indices internally to find contiguous runs
            // Result will have bursts based on sorted order
        }

        [Fact]
        public void Test_PlanIndexed_EmptyArray_ReturnsEmpty()
        {
            // Arrange
            ulong baseAddr = 0xA000;
            int elementSize = 4;
            ulong[] indices = Array.Empty<ulong>();
            bool indexIsByteOffset = false;

            // Act
            var segments = BurstPlanner.PlanIndexed(baseAddr, elementSize, indices, indexIsByteOffset).ToList();

            // Assert
            Assert.Empty(segments);
        }

        [Fact]
        public void Test_Plan2D_ZeroElements_ReturnsEmpty()
        {
            // Arrange
            ulong baseAddr = 0xB000;
            ulong elementCount = 0;
            int elementSize = 4;
            uint rowLength = 4;
            ushort rowStride = 16;
            ushort colStride = 4;

            // Act
            var segments = BurstPlanner.Plan2D(baseAddr, elementCount, elementSize,
                                               rowLength, rowStride, colStride).ToList();

            // Assert
            Assert.Empty(segments);
        }

        [Fact]
        public void Test_Plan_LargeTransfer_MultiplePages_SplitsCorrectly()
        {
            // Arrange
            ulong baseAddr = 0x1000;
            ulong totalBytes = 12288; // 3 pages (3 * 4KB)

            // Act
            var segments = BurstPlanner.Plan(baseAddr, totalBytes).ToList();

            // Assert
            Assert.True(segments.Count >= 3, "Should split across 3 pages");

            // Verify total bytes
            ulong totalPlanned = (ulong)segments.Sum(s => s.Length);
            Assert.Equal(totalBytes, totalPlanned);

            // Verify no segment crosses 4KB boundary
            foreach (var segment in segments)
            {
                ulong startPage = segment.Address >> 12;
                ulong endPage = (segment.Address + (ulong)segment.Length - 1) >> 12;
                Assert.Equal(startPage, endPage);
            }
        }
    }
}
