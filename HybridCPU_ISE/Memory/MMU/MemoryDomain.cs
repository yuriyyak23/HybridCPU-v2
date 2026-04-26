using System;

namespace YAKSys_Hybrid_CPU.Memory
{
    /// <summary>
    /// Атрибуты домена памяти (флаги доступа).
    /// Memory domain attributes for Singularity-style SIP isolation.
    /// </summary>
    [Flags]
    public enum MemoryDomainFlags
    {
        None = 0,
        Read = 1,
        Write = 2,
        ReadWrite = Read | Write,  // Convenience combination
        Execute = 4,
        Shared = 8       // Can be accessed by multiple threads (with locks)
    }

    /// <summary>
    /// Описатель домена памяти потока.
    /// Memory domain descriptor for thread isolation (Singularity SIP-style).
    ///
    /// Each hardware thread (0-15) has an isolated memory domain with:
    /// - Dedicated address range (BaseAddress + Size)
    /// - Access permissions (Read/Write/Execute)
    /// - Formal non-overlap guarantees
    ///
    /// Design principles:
    /// - HLS-compatible: simple struct, no complex inheritance
    /// - Verifiable: Contains() and Overlaps() support formal proofs
    /// - Efficient: All operations are O(1) comparisons
    /// </summary>
    public struct MemoryDomain
    {
        public int ThreadId;
        public ulong BaseAddress;
        public ulong Size;
        public MemoryDomainFlags Flags;

        /// <summary>
        /// Check if an address range is fully contained within this domain.
        /// Used for access validation before memory operations.
        /// </summary>
        /// <param name="address">Starting address to check</param>
        /// <param name="length">Length of the range in bytes</param>
        /// <returns>True if [address, address+length) is fully within domain</returns>
        public bool Contains(ulong address, ulong length)
        {
            // Check for overflow in the range calculation
            if (length > 0 && address > (ulong.MaxValue - length))
                return false;

            // Address must be >= base, and end must be <= domain end
            return address >= BaseAddress &&
                   (address + length) <= (BaseAddress + Size);
        }

        /// <summary>
        /// Check if an address range overlaps with this domain.
        /// Used for domain allocation to ensure non-overlapping domains.
        /// </summary>
        /// <param name="address">Starting address of range to check</param>
        /// <param name="length">Length of the range in bytes</param>
        /// <returns>True if ranges overlap, false if disjoint</returns>
        public bool Overlaps(ulong address, ulong length)
        {
            // Check for overflow
            if (length > 0 && address > (ulong.MaxValue - length))
                return true; // Conservative: assume overlap if overflow

            // Two ranges [a1,a2) and [b1,b2) overlap if: a1 < b2 AND b1 < a2
            ulong end1 = BaseAddress + Size;
            ulong end2 = address + length;
            return BaseAddress < end2 && address < end1;
        }
    }
}
