namespace YAKSys_Hybrid_CPU.Core.Memory
{
    /// <summary>
    /// Atomic memory operation interface for the HybridCPU ISA v4 memory subsystem.
    /// <para>
    /// All implementations must guarantee atomicity with respect to:
    /// <list type="bullet">
    ///   <item>Other AMO operations on the same address</item>
    ///   <item>LR/SC reservations on the same address</item>
    ///   <item>DMA transfers to the same region (if DMA is in-flight)</item>
    /// </list>
    /// </para>
    /// <para>
    /// Phase 07: This interface replaces the minimal <c>IAtomicMemoryBus</c>
    /// with a complete typed contract covering the full 64-bit atomic plane:
    /// LR/SC (word and doubleword), AMO*_W (9 word operations), and AMO*_D
    /// (9 doubleword operations).
    /// </para>
    /// </summary>
    public interface IAtomicMemoryUnit
    {
        // ── LR/SC ─────────────────────────────────────────────────────────────

        /// <summary>Load-Reserved Word: rd = sign_extend(mem32[address]); reserve address.</summary>
        int LoadReserved32(ulong address);

        /// <summary>Store-Conditional Word: if reservation holds mem32[address] = value, return 0 (success); else return 1 (fail).</summary>
        int StoreConditional32(ulong address, int value);

        /// <summary>Load-Reserved Doubleword: rd = mem64[address]; reserve address.</summary>
        long LoadReserved64(ulong address);

        /// <summary>Store-Conditional Doubleword: if reservation holds mem64[address] = value, return 0 (success); else return 1 (fail).</summary>
        long StoreConditional64(ulong address, long value);

        // ── AMO Word (signed return, sign-extended to 64 bits by caller) ──────

        /// <summary>Atomic Swap Word: rd = mem32[addr]; mem32[addr] = value. Returns old value.</summary>
        int AtomicSwap32(ulong address, int value);

        /// <summary>Atomic Add Word: rd = mem32[addr]; mem32[addr] += value. Returns old value.</summary>
        int AtomicAdd32(ulong address, int value);

        /// <summary>Atomic XOR Word: rd = mem32[addr]; mem32[addr] ^= value. Returns old value.</summary>
        int AtomicXor32(ulong address, int value);

        /// <summary>Atomic AND Word: rd = mem32[addr]; mem32[addr] &amp;= value. Returns old value.</summary>
        int AtomicAnd32(ulong address, int value);

        /// <summary>Atomic OR Word: rd = mem32[addr]; mem32[addr] |= value. Returns old value.</summary>
        int AtomicOr32(ulong address, int value);

        /// <summary>Atomic MIN Word (signed): rd = mem32[addr]; mem32[addr] = min(mem32[addr], value). Returns old value.</summary>
        int AtomicMinSigned32(ulong address, int value);

        /// <summary>Atomic MAX Word (signed): rd = mem32[addr]; mem32[addr] = max(mem32[addr], value). Returns old value.</summary>
        int AtomicMaxSigned32(ulong address, int value);

        /// <summary>Atomic MIN Word (unsigned): rd = mem32[addr]; mem32[addr] = min(mem32[addr], value). Returns old value.</summary>
        uint AtomicMinUnsigned32(ulong address, uint value);

        /// <summary>Atomic MAX Word (unsigned): rd = mem32[addr]; mem32[addr] = max(mem32[addr], value). Returns old value.</summary>
        uint AtomicMaxUnsigned32(ulong address, uint value);

        // ── AMO Doubleword ────────────────────────────────────────────────────

        /// <summary>Atomic Swap Doubleword: rd = mem64[addr]; mem64[addr] = value. Returns old value.</summary>
        long AtomicSwap64(ulong address, long value);

        /// <summary>Atomic Add Doubleword: rd = mem64[addr]; mem64[addr] += value. Returns old value.</summary>
        long AtomicAdd64(ulong address, long value);

        /// <summary>Atomic XOR Doubleword: rd = mem64[addr]; mem64[addr] ^= value. Returns old value.</summary>
        long AtomicXor64(ulong address, long value);

        /// <summary>Atomic AND Doubleword: rd = mem64[addr]; mem64[addr] &amp;= value. Returns old value.</summary>
        long AtomicAnd64(ulong address, long value);

        /// <summary>Atomic OR Doubleword: rd = mem64[addr]; mem64[addr] |= value. Returns old value.</summary>
        long AtomicOr64(ulong address, long value);

        /// <summary>Atomic MIN Doubleword (signed): rd = mem64[addr]; mem64[addr] = min(mem64[addr], value). Returns old value.</summary>
        long AtomicMin64Signed(ulong address, long value);

        /// <summary>Atomic MAX Doubleword (signed): rd = mem64[addr]; mem64[addr] = max(mem64[addr], value). Returns old value.</summary>
        long AtomicMax64Signed(ulong address, long value);

        /// <summary>Atomic MIN Doubleword (unsigned): rd = mem64[addr]; mem64[addr] = min(mem64[addr], value). Returns old value.</summary>
        ulong AtomicMin64Unsigned(ulong address, ulong value);

        /// <summary>Atomic MAX Doubleword (unsigned): rd = mem64[addr]; mem64[addr] = max(mem64[addr], value). Returns old value.</summary>
        ulong AtomicMax64Unsigned(ulong address, ulong value);
    }
}
