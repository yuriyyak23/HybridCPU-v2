using System;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// 128-bit resource bitset for Global Resource Lock Bitset (GRLB) - Phase 8 Extended.
    ///
    /// Purpose: Support larger resource spaces as the system scales beyond 64 resources.
    /// - Low 64 bits (0-63): Register groups, memory domains, LSU channels, DMA channels
    /// - High 64 bits (64-127): Stream engines, accelerators, extended resources
    ///
    /// Design goals:
    /// - Hardware-agnostic: suitable for HLS synthesis (maps to two 64-bit registers)
    /// - Efficient: inline operations, minimal overhead, single-cycle logic in hardware
    /// - Scalable: supports up to 128 distinct resource types
    /// - ABA-safe: used with token tracking to prevent resource re-acquisition bugs
    ///
    /// Bit layout (128 bits total):
    /// Low 64 bits (0-63):
    /// - Bits 0-15:  Register read groups (16 groups of 4 registers each)
    /// - Bits 16-31: Register write groups (16 groups of 4 registers each)
    /// - Bits 32-47: Memory domain IDs (16 possible domains)
    /// - Bit 48:     Load operation (LSU read channel)
    /// - Bit 49:     Store operation (LSU write channel)
    /// - Bit 50:     Atomic operation (LSU atomic channel)
    /// - Bits 51-54: DMA channels 0-3 (4 channels)
    /// - Bits 55-58: Stream engines 0-3 (4 engines)
    /// - Bits 59-62: Custom accelerators 0-3 (4 accelerators)
    /// - Bit 63:     Reserved
    ///
    /// High 64 bits (64-127):
    /// - Bits 64-67: DMA channels 4-7 (4 additional channels)
    /// - Bits 68-71: Stream engines 4-7 (4 additional engines)
    /// - Bits 72-75: Custom accelerators 4-7 (4 additional accelerators)
    /// - Bits 76-79: Additional LSU channels
    /// - Bits 80-95: Extended memory domains (16 additional domains)
    /// - Bits 96-127: Reserved for future resource types
    /// </summary>
    public struct ResourceBitset : IEquatable<ResourceBitset>
    {
        /// <summary>
        /// Low 64 bits (resources 0-63)
        /// Registers, LSU channels, basic DMA/Stream/Accel
        /// </summary>
        public ulong Low;

        /// <summary>
        /// High 64 bits (resources 64-127)
        /// Extended DMA, StreamEngine, Accelerators, future resources
        /// </summary>
        public ulong High;

        /// <summary>
        /// Create a 128-bit resource bitset from low and high components
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ResourceBitset(ulong low, ulong high)
        {
            Low = low;
            High = high;
        }

        /// <summary>
        /// Create a 128-bit resource bitset from a 64-bit mask (for backward compatibility)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ResourceBitset(ulong mask)
        {
            Low = mask;
            High = 0;
        }

        /// <summary>
        /// Check if the bitset is zero (no resources)
        /// </summary>
        public readonly bool IsZero => Low == 0 && High == 0;

        /// <summary>
        /// Check if the bitset is non-zero (has resources)
        /// </summary>
        public readonly bool IsNonZero => Low != 0 || High != 0;

        /// <summary>
        /// Bitwise OR operator for combining bitsets
        /// Hardware: Two parallel OR gates (single cycle)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ResourceBitset operator |(ResourceBitset a, ResourceBitset b)
        {
            return new ResourceBitset(a.Low | b.Low, a.High | b.High);
        }

        /// <summary>
        /// Bitwise AND operator for checking conflicts
        /// Hardware: Two parallel AND gates (single cycle)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ResourceBitset operator &(ResourceBitset a, ResourceBitset b)
        {
            return new ResourceBitset(a.Low & b.Low, a.High & b.High);
        }

        /// <summary>
        /// Bitwise NOT operator for clearing resources
        /// Hardware: Two parallel NOT gates (single cycle)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ResourceBitset operator ~(ResourceBitset a)
        {
            return new ResourceBitset(~a.Low, ~a.High);
        }

        /// <summary>
        /// Equality operator
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(ResourceBitset a, ResourceBitset b)
        {
            return a.Low == b.Low && a.High == b.High;
        }

        /// <summary>
        /// Inequality operator
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(ResourceBitset a, ResourceBitset b)
        {
            return !(a == b);
        }

        /// <summary>
        /// Implicit conversion from ulong (backward compatibility)
        /// Allows: ResourceBitset mask = 0x1UL;
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ResourceBitset(ulong mask)
        {
            return new ResourceBitset(mask, 0);
        }

        /// <summary>
        /// Explicit conversion to ulong (backward compatibility for tests)
        /// Converts to Low 64 bits, discarding High bits.
        /// Use only for legacy code that expects 64-bit masks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator ulong(ResourceBitset bitset)
        {
            return bitset.Low;
        }

        /// <summary>
        /// Check if this bitset conflicts with another bitset
        /// Returns true if any structural hazards or data hazards (RAW/WAR/WAW) exist.
        /// Resolves the RAR safety: pure read-read overlaps do not trigger a conflict.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool ConflictsWith(ResourceBitset other)
        {
            // Non-read resources (WAW, domains, LSU, DMA, etc.) overlap directly
            ulong nonReadLowA = Low & ~0xFFFFUL;
            ulong nonReadLowB = other.Low & ~0xFFFFUL;

            if ((nonReadLowA & nonReadLowB) != 0) return true;
            if ((High & other.High) != 0) return true;

            // Cross-dependencies for Registers: Read vs Write (RAW / WAR)
            ulong myReads = Low & 0xFFFFUL;
            ulong theirWrites = (other.Low >> 16) & 0xFFFFUL;
            ulong myWrites = (Low >> 16) & 0xFFFFUL;
            ulong theirReads = other.Low & 0xFFFFUL;

            if ((myReads & theirWrites) != 0) return true;
            if ((myWrites & theirReads) != 0) return true;

            return false;
        }

        /// <summary>
        /// Check if this bitset has no conflicts with another bitset
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool NoConflictsWith(ResourceBitset other)
        {
            return !ConflictsWith(other);
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is ResourceBitset other && Equals(other);
        }

        public readonly bool Equals(ResourceBitset other)
        {
            return Low == other.Low && High == other.High;
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(Low, High);
        }

        public override readonly string ToString()
        {
            return $"ResourceBitset(Low=0x{Low:X16}, High=0x{High:X16})";
        }

        // ===== GRLB Bank Partitioning (Plan 06) =====

        /// <summary>Number of GRLB banks.</summary>
        public const int GRLB_BANK_COUNT = 4;

        /// <summary>
        /// Extract a 32-bit bank slice from this 128-bit bitset.
        /// Bank 0: bits [0..31], Bank 1: bits [32..63],
        /// Bank 2: bits [64..95], Bank 3: bits [96..127].
        /// HLS: pure wire select, zero combinational logic.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly uint GetBank(int bankIndex) => bankIndex switch
        {
            0 => (uint)(Low & 0xFFFFFFFFUL),
            1 => (uint)(Low >> 32),
            2 => (uint)(High & 0xFFFFFFFFUL),
            3 => (uint)(High >> 32),
            _ => 0
        };

        /// <summary>
        /// Return a bitmask of banks touched by this bitset (bits 0–3).
        /// HLS: 4 OR-reduce gates, single-cycle.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly byte ActiveBanks()
        {
            byte banks = 0;
            if ((Low & 0x00000000FFFFFFFFUL) != 0) banks |= 0x01;
            if ((Low & 0xFFFFFFFF00000000UL) != 0) banks |= 0x02;
            if ((High & 0x00000000FFFFFFFFUL) != 0) banks |= 0x04;
            if ((High & 0xFFFFFFFF00000000UL) != 0) banks |= 0x08;
            return banks;
        }

        /// <summary>
        /// Zero bitset (no resources)
        /// </summary>
        public static readonly ResourceBitset Zero = new ResourceBitset(0, 0);

        /// <summary>
        /// All ones bitset (all resources)
        /// </summary>
        public static readonly ResourceBitset All = new ResourceBitset(~0UL, ~0UL);
    }
}
