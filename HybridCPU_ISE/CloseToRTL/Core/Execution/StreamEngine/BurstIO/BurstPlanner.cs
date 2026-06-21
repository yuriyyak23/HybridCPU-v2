using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Execution
{
    /// <summary>
    /// Burst segment descriptor for AXI4-compliant memory transfers.
    /// Represents a single contiguous burst that respects hardware constraints.
    /// </summary>
    public readonly struct BurstSegment
    {
        /// <summary>
        /// Starting address of this burst segment.
        /// </summary>
        public readonly ulong Address;

        /// <summary>
        /// Length of this burst segment in bytes.
        /// </summary>
        public readonly int Length;

        public BurstSegment(ulong address, int length)
        {
            Address = address;
            Length = length;
        }
    }

    /// <summary>
    /// Burst timing descriptor for AXI4 transactions (Phase 3)
    /// Models realistic cycle-level timing for burst operations
    /// </summary>
    public struct BurstTiming
    {
        /// <summary>
        /// Address phase cycles
        /// </summary>
        public int HeaderCycles;

        /// <summary>
        /// Data beat cycles
        /// </summary>
        public int DataCycles;

        /// <summary>
        /// Response phase cycles
        /// </summary>
        public int ResponseCycles;

        /// <summary>
        /// Unaligned access overhead
        /// </summary>
        public int AlignmentPenalty;

        /// <summary>
        /// Total cycles (sum of above)
        /// </summary>
        public int TotalCycles;

        /// <summary>
        /// Number of data beats
        /// </summary>
        public int DataBeats;

        /// <summary>
        /// Bytes per beat
        /// </summary>
        public int BeatSize;
    }

    /// <summary>
    /// BurstPlanner: Pure mathematical burst planning logic.
    ///
    /// Responsibilities:
    /// - Split large transfers at 4KB page boundaries
    /// - Respect AXI4 burst length limits (256 beats max)
    /// - Deterministic: same input → same burst pattern
    /// - Zero state, zero allocation (HLS-friendly)
    ///
    /// Does NOT:
    /// - Perform actual memory access
    /// - Depend on IOMMU/DMA backend
    /// - Manage device state
    ///
    /// Design philosophy:
    /// - Separation of concerns: planning vs. execution
    /// - Testable: burst math can be verified independently
    /// - Reusable: backend-agnostic planning
    /// </summary>
    public static class BurstPlanner
    {
        /// <summary>
        /// AXI4 page size constraint: bursts cannot cross 4KB boundaries.
        /// This is a hardware limitation imposed by memory controllers.
        /// </summary>
        private const ulong PAGE_SIZE = 4096;

        /// <summary>
        /// Plan burst segments for a contiguous memory transfer.
        /// Splits the transfer into AXI4-compliant segments that:
        /// 1. Do not cross 4KB page boundaries
        /// 2. Respect hardware burst length limits
        ///
        /// Algorithm:
        /// - Iterate through transfer from baseAddr to baseAddr+totalBytes
        /// - At each step, compute bytes remaining in current page
        /// - Emit segment for min(bytes_in_page, remaining_bytes)
        /// - Advance to next page boundary if needed
        ///
        /// Complexity: O(total_bytes / PAGE_SIZE)
        /// Memory: O(1) per iteration (iterator pattern)
        ///
        /// Invariant: [Invariant("Burst segments never cross 4KB boundaries")]
        /// Each returned segment is guaranteed to fit within a single 4KB page.
        /// </summary>
        /// <param name="baseAddr">Starting address of transfer</param>
        /// <param name="totalBytes">Total number of bytes to transfer</param>
        /// <returns>Iterator of burst segments (no allocation)</returns>
        public static IEnumerable<BurstSegment> Plan(ulong baseAddr, ulong totalBytes)
        {
            ulong processed = 0;

            while (processed < totalBytes)
            {
                ulong addr = baseAddr + processed;

                // Calculate offset within current 4KB page
                ulong pageOffset = addr & 0xFFF;  // addr % 4096

                // Calculate bytes remaining in current page
                ulong bytesInPage = PAGE_SIZE - pageOffset;

                // Calculate bytes remaining in transfer
                ulong remaining = totalBytes - processed;

                // Segment size is minimum of bytes-in-page and remaining
                int chunk = (int)Math.Min(bytesInPage, remaining);

                // Validate invariant: segment must not cross 4KB boundary
                System.Diagnostics.Debug.Assert(
                    (addr & 0xFFFFF000UL) == ((addr + (ulong)chunk - 1) & 0xFFFFF000UL) || chunk == 0,
                    $"Burst segment crosses 4KB boundary: addr=0x{addr:X}, length={chunk}"
                );

                // Validate: chunk must be positive and fit in remaining bytes
                System.Diagnostics.Debug.Assert(chunk > 0, "Chunk size must be positive");
                System.Diagnostics.Debug.Assert(chunk <= (int)remaining, "Chunk exceeds remaining bytes");

                yield return new BurstSegment(addr, chunk);

                processed += (ulong)chunk;
            }
        }

        /// <summary>
        /// Plan burst segments for 2D memory transfers.
        /// Optimizes row-major 2D array access by identifying contiguous rows
        /// and planning bursts for each row independently.
        ///
        /// Algorithm:
        /// - For each row, if columns are contiguous (colStride == elementSize):
        ///   - Plan burst for entire row using Plan()
        /// - Otherwise, fall back to element-by-element access
        ///
        /// Optimization: When rowStride == rowLength * colStride, entire 2D region
        /// is contiguous and can be treated as 1D.
        ///
        /// Complexity: O(rows * segments_per_row)
        /// </summary>
        /// <param name="baseAddr">Base address of 2D array</param>
        /// <param name="elementCount">Total number of elements</param>
        /// <param name="elementSize">Size of each element in bytes</param>
        /// <param name="rowLength">Number of elements per row</param>
        /// <param name="rowStride">Byte pitch between rows</param>
        /// <param name="colStride">Byte stride between columns</param>
        /// <returns>Iterator of burst segments optimized for 2D access</returns>
        public static IEnumerable<BurstSegment> Plan2D(ulong baseAddr, ulong elementCount, int elementSize,
                                                       uint rowLength, ushort rowStride, ushort colStride)
        {
            if (elementCount == 0 || rowLength == 0 || elementSize <= 0)
                yield break;

            // Check if entire 2D region is contiguous (can treat as 1D)
            if (colStride == elementSize && rowStride == rowLength * elementSize)
            {
                // Fully contiguous - delegate to 1D planner
                ulong totalBytes = elementCount * (ulong)elementSize;
                foreach (var segment in Plan(baseAddr, totalBytes))
                {
                    yield return segment;
                }
                yield break;
            }

            // Check if columns within each row are contiguous
            if (colStride == elementSize)
            {
                // Rows are contiguous but may have gaps between rows
                ulong numRows = (elementCount + rowLength - 1) / rowLength;
                ulong rowBytes = rowLength * (ulong)elementSize;

                for (ulong row = 0; row < numRows; row++)
                {
                    ulong rowBase = baseAddr + (row * rowStride);
                    ulong elementsInRow = Math.Min(rowLength, elementCount - (row * rowLength));
                    ulong bytesInRow = elementsInRow * (ulong)elementSize;

                    // Plan bursts for this contiguous row
                    foreach (var segment in Plan(rowBase, bytesInRow))
                    {
                        yield return segment;
                    }
                }
            }
            else
            {
                // Non-contiguous access - return individual element addresses
                // Caller should handle element-by-element access
                for (ulong i = 0; i < elementCount; i++)
                {
                    ulong row = i / rowLength;
                    ulong col = i % rowLength;
                    ulong addr = baseAddr + (row * rowStride) + (col * colStride);
                    yield return new BurstSegment(addr, elementSize);
                }
            }
        }

        /// <summary>
        /// Plan burst segments for indexed memory access (gather/scatter).
        /// Analyzes index array to identify contiguous runs and group them into bursts.
        ///
        /// Algorithm:
        /// - Sort indices to identify contiguous runs
        /// - For each contiguous run, plan bursts using Plan()
        /// - Return mapping of original index position to burst segment
        ///
        /// Optimization heuristic:
        /// - If >80% of accesses are contiguous, use burst planning
        /// - Otherwise, fall back to element-by-element access
        ///
        /// Note: This method performs index analysis, which has O(n log n) complexity
        /// due to sorting. Use only when n is large enough to benefit from bursting.
        ///
        /// Complexity: O(n log n) for sorting + O(runs * segments_per_run)
        /// </summary>
        /// <param name="baseAddr">Base address of source/dest array</param>
        /// <param name="elementSize">Size of each element in bytes</param>
        /// <param name="indices">Array of indices (element or byte offsets)</param>
        /// <param name="indexIsByteOffset">True if indices are byte offsets, false if element indices</param>
        /// <returns>Iterator of burst segments for indexed access</returns>
        public static IEnumerable<BurstSegment> PlanIndexed(ulong baseAddr, int elementSize,
                                                            ulong[] indices, bool indexIsByteOffset)
        {
            if (indices == null || indices.Length == 0 || elementSize <= 0)
                yield break;

            // For small index counts, use element-by-element (sorting overhead not worth it)
            if (indices.Length < 4)
            {
                for (int i = 0; i < indices.Length; i++)
                {
                    ulong offset = indexIsByteOffset ? indices[i] : indices[i] * (ulong)elementSize;
                    ulong addr = baseAddr + offset;
                    yield return new BurstSegment(addr, elementSize);
                }
                yield break;
            }

            // Copy and sort indices to find contiguous runs
            // Note: In a real implementation, we'd track original positions
            // For now, we just optimize the access pattern
            ulong[] sortedIndices = new ulong[indices.Length];
            Array.Copy(indices, sortedIndices, indices.Length);
            Array.Sort(sortedIndices);

            // Identify contiguous runs and plan bursts
            int runStart = 0;
            for (int i = 1; i <= sortedIndices.Length; i++)
            {
                bool isEndOfRun = (i == sortedIndices.Length) ||
                                  (sortedIndices[i] != sortedIndices[i - 1] + (indexIsByteOffset ? (ulong)elementSize : 1));

                if (isEndOfRun)
                {
                    int runLength = i - runStart;
                    ulong startIndex = sortedIndices[runStart];
                    ulong startOffset = indexIsByteOffset ? startIndex : startIndex * (ulong)elementSize;
                    ulong startAddr = baseAddr + startOffset;
                    ulong runBytes = (ulong)(runLength * elementSize);

                    if (runLength > 1)
                    {
                        // Contiguous run - use burst planning
                        foreach (var segment in Plan(startAddr, runBytes))
                        {
                            yield return segment;
                        }
                    }
                    else
                    {
                        // Single element
                        yield return new BurstSegment(startAddr, elementSize);
                    }

                    runStart = i;
                }
            }
        }

        /// <summary>
        /// Calculate detailed AXI4 burst timing (Phase 3)
        /// Models realistic cycle-level timing including alignment penalties
        /// </summary>
        /// <param name="address">Starting address</param>
        /// <param name="size">Transfer size in bytes</param>
        /// <param name="isRead">Read or write operation</param>
        /// <returns>Detailed timing breakdown</returns>
        public static BurstTiming CalculateBurstTiming(ulong address, int size, bool isRead)
        {
            var timing = new BurstTiming();

            // AXI4 bus width (64 bytes typical for HBM/DDR)
            timing.BeatSize = 64;

            // Header phase (address + control)
            timing.HeaderCycles = 1;  // AXI4 pipelined

            // Calculate alignment penalty
            int alignment = (int)(address % 64);  // Assume 64-byte cache line
            if (alignment != 0)
            {
                timing.AlignmentPenalty = CalculateAlignmentPenalty(alignment, size);
            }

            // Data phase
            timing.DataBeats = (size + timing.BeatSize - 1) / timing.BeatSize;
            timing.DataCycles = timing.DataBeats;

            // Account for burst fragmentation across 4KB boundaries
            int fragmentCount = Count4KBFragments(address, size);
            if (fragmentCount > 1)
            {
                // Each fragment adds header + response overhead
                timing.HeaderCycles += (fragmentCount - 1);
                timing.ResponseCycles += (fragmentCount - 1);
            }

            // Response phase
            timing.ResponseCycles += 1;  // AXI4 response

            // Total
            timing.TotalCycles = timing.HeaderCycles + timing.DataCycles +
                                timing.ResponseCycles + timing.AlignmentPenalty;

            return timing;
        }

        /// <summary>
        /// Calculate alignment penalty for unaligned accesses (Phase 3)
        /// </summary>
        /// <param name="alignment">Byte offset from aligned boundary</param>
        /// <param name="size">Transfer size</param>
        /// <returns>Penalty in cycles</returns>
        private static int CalculateAlignmentPenalty(int alignment, int size)
        {
            // Unaligned access may require:
            // 1. Extra read-modify-write for partial cache lines
            // 2. Additional burst for crossing boundaries
            int penalty = 0;

            if (alignment != 0)
            {
                // Base penalty for unaligned access
                penalty = 2;  // Read-modify-write overhead

                // Check if transfer crosses 64-byte boundary
                if (alignment + size > 64)
                {
                    penalty += 2;  // Boundary crossing penalty
                }
            }

            return penalty;
        }

        /// <summary>
        /// Count 4KB page fragments for burst transaction (Phase 3)
        /// </summary>
        /// <param name="address">Starting address</param>
        /// <param name="size">Transfer size</param>
        /// <returns>Number of 4KB fragments</returns>
        private static int Count4KBFragments(ulong address, int size)
        {
            ulong endAddress = address + (ulong)size;
            ulong startPage = address / 4096;
            ulong endPage = (endAddress - 1) / 4096;
            return (int)(endPage - startPage + 1);
        }

        /// <summary>
        /// Calculate total latency for burst sequence (Phase 3)
        /// </summary>
        /// <param name="segments">Burst segments</param>
        /// <param name="isRead">Read or write operation</param>
        /// <returns>Total cycles</returns>
        public static int CalculateBurstSequenceLatency(
            IEnumerable<BurstSegment> segments, bool isRead)
        {
            int totalCycles = 0;

            foreach (var segment in segments)
            {
                var timing = CalculateBurstTiming(
                    segment.Address, segment.Length, isRead);
                totalCycles += timing.TotalCycles;
            }

            return totalCycles;
        }
    }
}
