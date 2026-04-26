using System;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Memory
{
    /// <summary>
    /// L3 Global Distributed Cache — shared SRAM buffer distributed across NoC nodes (req.md §2).
    ///
    /// Purpose: Acts as a large shared buffer between L2 (per-Pod) and HBM4 DRAM.
    /// Data from HBM4 is loaded here via large AXI4-burst DMA transactions,
    /// then distributed to Pods via NoC. Similar to AMD Infinity Cache.
    ///
    /// Architecture:
    /// - Distributed across NoC nodes (each Pod's router holds a slice)
    /// - Home node determined by address hashing (maps address → NoC coordinate)
    /// - HLS: tag+valid arrays per node, fixed-size
    ///
    /// Current implementation: Stub model with basic hit/miss tracking.
    /// Full implementation would distribute slices across NoC_XY_Router instances.
    /// </summary>
    public class GlobalDistributedCache
    {
        /// <summary>Total number of lines across all slices</summary>
        public const int TOTAL_LINES = 4096;

        /// <summary>Cache line size (matches HBM4 burst width)</summary>
        public const int LINE_SIZE_BYTES = 256;

        /// <summary>Number of NoC nodes (64 Pods)</summary>
        private const int NUM_SLICES = 64;

        /// <summary>Lines per slice</summary>
        private const int LINES_PER_SLICE = TOTAL_LINES / NUM_SLICES;

        private const int OFFSET_BITS = 8; // log2(256)

        /// <summary>Per-line metadata</summary>
        private struct L3Line
        {
            public ulong Tag;
            public bool Valid;
            public ulong DomainTag;
            public long LastAccessCycle;
        }

        private readonly L3Line[] _lines;
        private long _currentCycle;

        /// <summary>Total L3 lookups</summary>
        public long TotalLookups { get; private set; }

        /// <summary>L3 cache hits</summary>
        public long Hits { get; private set; }

        /// <summary>L3 cache misses (require HBM4 fetch)</summary>
        public long Misses { get; private set; }

        public GlobalDistributedCache()
        {
            _lines = new L3Line[TOTAL_LINES];
            for (int i = 0; i < TOTAL_LINES; i++)
            {
                _lines[i] = new L3Line
                {
                    Tag = 0,
                    Valid = false,
                    DomainTag = 0,
                    LastAccessCycle = 0
                };
            }
        }

        /// <summary>
        /// Determine which NoC slice (Pod) is the home node for a given address.
        /// Uses address hashing to distribute lines evenly across slices.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetHomeSlice(ulong address)
        {
            return (int)((address >> OFFSET_BITS) % NUM_SLICES);
        }

        /// <summary>
        /// Lookup an address in L3 cache.
        /// </summary>
        /// <param name="address">Physical address</param>
        /// <returns>True on L3 hit, false on L3 miss (needs HBM4 fetch)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Lookup(ulong address)
        {
            TotalLookups++;
            int lineIndex = GetLineIndex(address);
            ulong tag = GetTag(address);

            ref var line = ref _lines[lineIndex];
            if (line.Valid && line.Tag == tag)
            {
                Hits++;
                line.LastAccessCycle = _currentCycle;
                return true;
            }

            Misses++;
            return false;
        }

        /// <summary>
        /// Fill a line after HBM4 burst fetch completes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Fill(ulong address, ulong domainTag)
        {
            int lineIndex = GetLineIndex(address);
            ulong tag = GetTag(address);

            ref var line = ref _lines[lineIndex];
            line.Tag = tag;
            line.Valid = true;
            line.DomainTag = domainTag;
            line.LastAccessCycle = _currentCycle;
        }

        /// <summary>
        /// Invalidate a line.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invalidate(ulong address)
        {
            int lineIndex = GetLineIndex(address);
            ulong tag = GetTag(address);

            ref var line = ref _lines[lineIndex];
            if (line.Valid && line.Tag == tag)
            {
                line.Valid = false;
            }
        }

        /// <summary>
        /// Advance the internal cycle counter.
        /// </summary>
        public void AdvanceCycle()
        {
            _currentCycle++;
        }

        /// <summary>
        /// Reset all cache state.
        /// </summary>
        public void Reset()
        {
            for (int i = 0; i < TOTAL_LINES; i++)
            {
                _lines[i].Valid = false;
                _lines[i].Tag = 0;
                _lines[i].DomainTag = 0;
            }

            TotalLookups = 0;
            Hits = 0;
            Misses = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetLineIndex(ulong address)
        {
            return (int)((address >> OFFSET_BITS) % TOTAL_LINES);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong GetTag(ulong address)
        {
            return address >> (OFFSET_BITS + 12); // 12 = log2(TOTAL_LINES)
        }
    }
}
