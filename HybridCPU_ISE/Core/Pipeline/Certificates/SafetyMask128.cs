using System;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// 128-bit safety mask for extended resource tracking (Phase: Safety Tags & Certificates).
    ///
    /// Purpose: Support larger resource spaces as the system scales beyond 64 resources.
    /// - Low 64 bits: Register groups, memory domains, LSU classes
    /// - High 64 bits: GRLB channels (DMA, Stream, Accelerators), future extensions
    ///
    /// Design goals:
    /// - Hardware-agnostic: suitable for HLS synthesis
    /// - Efficient: inline operations, minimal overhead
    /// - Scalable: supports up to 128 distinct resource types
    /// </summary>
    public struct SafetyMask128 : IEquatable<SafetyMask128>
    {
        /// <summary>
        /// Low 64 bits (0-63):
        /// - Bits 0-15:  Register read groups (16 groups of 4 registers each)
        /// - Bits 16-31: Register write groups (16 groups of 4 registers each)
        /// - Bits 32-47: Memory domain IDs (16 possible domains)
        /// - Bits 48:    Load operation (LSU read channel)
        /// - Bits 49:    Store operation (LSU write channel)
        /// - Bits 50:    Atomic operation (LSU atomic channel)
        /// - Bits 51-54: DMA channels (4 channels)
        /// - Bits 55-58: Stream engines (4 engines)
        /// - Bits 59-62: Custom accelerators (4 accelerators)
        /// - Bit 63:     Reserved
        /// </summary>
        public ulong Low;

        /// <summary>
        /// High 64 bits (64-127):
        /// - Bits 64-95:  Extended GRLB channels (32 channels for future hardware)
        /// - Bits 96-111: Extended memory domains (16 additional domains)
        /// - Bits 112-127: Reserved for future resource types
        /// </summary>
        public ulong High;

        /// <summary>
        /// Create a 128-bit safety mask from low and high components
        /// </summary>
        public SafetyMask128(ulong low, ulong high)
        {
            Low = low;
            High = high;
        }

        /// <summary>
        /// Create a 128-bit safety mask from a 64-bit mask (for backward compatibility)
        /// </summary>
        public SafetyMask128(ulong mask)
        {
            Low = mask;
            High = 0;
        }

        /// <summary>
        /// Check if the mask is zero (uninitialized)
        /// </summary>
        public bool IsZero => Low == 0 && High == 0;

        /// <summary>
        /// Check if the mask is non-zero (properly initialized)
        /// </summary>
        public bool IsNonZero => Low != 0 || High != 0;

        /// <summary>
        /// Bitwise OR operator for combining masks
        /// </summary>
        public static SafetyMask128 operator |(SafetyMask128 a, SafetyMask128 b)
        {
            return new SafetyMask128(a.Low | b.Low, a.High | b.High);
        }

        /// <summary>
        /// Bitwise AND operator for checking conflicts
        /// </summary>
        public static SafetyMask128 operator &(SafetyMask128 a, SafetyMask128 b)
        {
            return new SafetyMask128(a.Low & b.Low, a.High & b.High);
        }

        /// <summary>
        /// Equality operator
        /// </summary>
        public static bool operator ==(SafetyMask128 a, SafetyMask128 b)
        {
            return a.Low == b.Low && a.High == b.High;
        }

        /// <summary>
        /// Inequality operator
        /// </summary>
        public static bool operator !=(SafetyMask128 a, SafetyMask128 b)
        {
            return !(a == b);
        }

        /// <summary>
        /// Implicit conversion from ulong (backward compatibility)
        /// </summary>
        public static implicit operator SafetyMask128(ulong mask)
        {
            return new SafetyMask128(mask, 0);
        }

        /// <summary>
        /// Check if this mask conflicts with another mask
        /// Returns true if any bits overlap (conflict exists), accounting for Read/Write hazards
        /// </summary>
        public bool ConflictsWith(SafetyMask128 other)
        {
            // Non-read resources (WAW, domains, LSU, etc.) overlap directly
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
        /// Check if this mask has no conflicts with another mask
        /// Returns true if no bits overlap (safe to execute in parallel)
        /// </summary>
        public bool NoConflictsWith(SafetyMask128 other)
        {
            return !ConflictsWith(other);
        }

        public override bool Equals(object? obj)
        {
            return obj is SafetyMask128 other && Equals(other);
        }

        public bool Equals(SafetyMask128 other)
        {
            return Low == other.Low && High == other.High;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Low, High);
        }

        public override string ToString()
        {
            return $"SafetyMask128(Low=0x{Low:X16}, High=0x{High:X16})";
        }

        /// <summary>
        /// Zero mask (no resources)
        /// </summary>
        public static readonly SafetyMask128 Zero = new SafetyMask128(0, 0);

        /// <summary>
        /// All ones mask (all resources)
        /// </summary>
        public static readonly SafetyMask128 All = new SafetyMask128(~0UL, ~0UL);
    }
}
