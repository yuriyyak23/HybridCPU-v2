using System;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Memory
{
    /// <summary>
    /// L2 Shared Pod Cache — shared memory within a Pod of 16 cores (req.md §2).
    ///
    /// Purpose: Model a per-Pod shared cache that sits between L1 (per-core TCDM/StreamRegisterFile)
    /// and L3 (Global Distributed SRAM). When a core gets an L2 miss, its VLIW slots become
    /// available for FSP donation — the scheduler injects ALU ops from sibling cores whose
    /// data is already resident in L2.
    ///
    /// Architecture:
    /// - Fixed number of cache lines (set-associative, tag-based)
    /// - Per-line domain tag for Singularity-style isolation (req.md §4)
    /// - Miss signal exposed to PodController/Scheduler for FSP integration
    /// - HLS-compatible: fixed-size arrays, deterministic lookup
    ///
    /// Simplification: Direct-mapped for HLS, with tag + valid + dirty bits.
    /// </summary>
    public class SharedPodCache
    {
        /// <summary>Number of cache lines (must be power of 2)</summary>
        public const int NUM_LINES = 256;

        /// <summary>Cache line size in bytes (matches AXI burst width)</summary>
        public const int LINE_SIZE_BYTES = 256;

        /// <summary>Tag bits shift: log2(NUM_LINES * LINE_SIZE_BYTES)</summary>
        private const int INDEX_BITS = 8; // log2(256)

        /// <summary>Offset bits: log2(LINE_SIZE_BYTES)</summary>
        private const int OFFSET_BITS = 8; // log2(256)

        /// <summary>Cache line metadata (HLS: register array)</summary>
        private struct CacheLine
        {
            public ulong Tag;
            public bool Valid;
            public bool Dirty;
            public ulong DomainTag;
            public int OwnerCoreId;
            public long LastAccessCycle;
        }

        private readonly CacheLine[] _lines;
        private readonly byte[][] _data;
        private long _currentCycle;

        /// <summary>Total L2 lookups</summary>
        public long TotalLookups { get; private set; }

        /// <summary>L2 cache hits</summary>
        public long Hits { get; private set; }

        /// <summary>L2 cache misses</summary>
        public long Misses { get; private set; }

        // Phase §2: Per-domain access counters for fairness monitoring.
        // Tracks how many L2 accesses each domain makes per measurement window.
        // A significant imbalance between domains may indicate a timing side-channel
        // (e.g., one domain evicting another's lines to probe access patterns).
        // HLS: 16 × 32-bit saturating counters (1 per possible domain slot).
        private const int MAX_DOMAIN_COUNTERS = 16;
        private readonly long[] _domainAccessCounts;
        private readonly long[] _domainHitCounts;
        private readonly long[] _domainMissCounts;

        /// <summary>
        /// Per-core miss-pending flag (16 cores per Pod).
        /// When true, indicates this core is waiting on an L2 miss and its
        /// VLIW slots should be treated as donor-eligible by FSP (req.md §2).
        /// HLS: 16-bit register.
        /// </summary>
        private readonly bool[] _coreMissPending;

        /// <summary>
        /// Bitmask of cores with pending L2 misses.
        /// Consumed by MicroOpScheduler to identify donor slots.
        /// </summary>
        public ushort MissPendingMask
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ushort mask = 0;
                for (int i = 0; i < 16; i++)
                {
                    if (_coreMissPending[i])
                        mask |= (ushort)(1 << i);
                }
                return mask;
            }
        }

        public SharedPodCache()
        {
            _lines = new CacheLine[NUM_LINES];
            _data = new byte[NUM_LINES][];
            _coreMissPending = new bool[16];
            _domainAccessCounts = new long[MAX_DOMAIN_COUNTERS];
            _domainHitCounts = new long[MAX_DOMAIN_COUNTERS];
            _domainMissCounts = new long[MAX_DOMAIN_COUNTERS];

            for (int i = 0; i < NUM_LINES; i++)
            {
                _lines[i] = new CacheLine
                {
                    Tag = 0,
                    Valid = false,
                    Dirty = false,
                    DomainTag = 0,
                    OwnerCoreId = -1,
                    LastAccessCycle = 0
                };
                _data[i] = new byte[LINE_SIZE_BYTES];
            }
        }

        /// <summary>
        /// Lookup an address in L2 cache.
        /// Returns true on hit, false on miss. On miss, sets the requesting core's
        /// miss-pending flag so FSP can donate its empty slots.
        /// </summary>
        /// <param name="address">Physical address to lookup</param>
        /// <param name="localCoreId">Requesting core within the Pod (0–15)</param>
        /// <param name="domainTag">Domain tag for isolation check</param>
        /// <returns>True if L2 hit, false if L2 miss</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Lookup(ulong address, int localCoreId, ulong domainTag)
        {
            TotalLookups++;

            // Phase §2: Track per-domain access for fairness monitoring
            int domainSlot = (int)(domainTag % (ulong)MAX_DOMAIN_COUNTERS);
            _domainAccessCounts[domainSlot]++;

            int index = GetIndex(address);
            ulong tag = GetTag(address);

            ref var line = ref _lines[index];

            if (line.Valid && line.Tag == tag)
            {
                // Domain check: if line belongs to different domain, treat as miss
                if (domainTag != 0 && line.DomainTag != 0 && line.DomainTag != domainTag)
                {
                    Misses++;
                    _domainMissCounts[domainSlot]++;
                    SetCoreMissPending(localCoreId, true);
                    return false;
                }

                Hits++;
                _domainHitCounts[domainSlot]++;
                line.LastAccessCycle = _currentCycle;
                SetCoreMissPending(localCoreId, false);
                return true;
            }

            // L2 miss — signal FSP that this core's slots are available
            Misses++;
            _domainMissCounts[domainSlot]++;
            SetCoreMissPending(localCoreId, true);
            return false;
        }

        /// <summary>
        /// Fill a cache line after an L3/HBM4 fetch completes.
        /// Clears the core's miss-pending flag.
        /// </summary>
        /// <param name="address">Address of the line to fill</param>
        /// <param name="data">Data from L3/HBM4</param>
        /// <param name="localCoreId">Core that requested the fill</param>
        /// <param name="domainTag">Domain tag for the line</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Fill(ulong address, ReadOnlySpan<byte> data, int localCoreId, ulong domainTag)
        {
            int index = GetIndex(address);
            ulong tag = GetTag(address);

            ref var line = ref _lines[index];
            line.Tag = tag;
            line.Valid = true;
            line.Dirty = false;
            line.DomainTag = domainTag;
            line.OwnerCoreId = localCoreId;
            line.LastAccessCycle = _currentCycle;

            int copyLen = Math.Min(data.Length, LINE_SIZE_BYTES);
            data[..copyLen].CopyTo(_data[index]);

            // Clear miss-pending for the core
            SetCoreMissPending(localCoreId, false);
        }

        /// <summary>
        /// Invalidate a cache line (e.g., on coherence action or domain revocation).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invalidate(ulong address)
        {
            int index = GetIndex(address);
            ulong tag = GetTag(address);

            ref var line = ref _lines[index];
            if (line.Valid && line.Tag == tag)
            {
                line.Valid = false;
            }
        }

        /// <summary>
        /// Check if a specific core has a pending L2 miss (for FSP slot donation).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCoreMissPending(int localCoreId)
        {
            if ((uint)localCoreId >= 16) return false;
            return _coreMissPending[localCoreId];
        }

        /// <summary>
        /// Set or clear the miss-pending flag for a core.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetCoreMissPending(int localCoreId, bool pending)
        {
            if ((uint)localCoreId < 16)
                _coreMissPending[localCoreId] = pending;
        }

        /// <summary>
        /// Clear all miss-pending flags (e.g., on Pod reset or barrier).
        /// </summary>
        public void ClearAllMissPending()
        {
            for (int i = 0; i < 16; i++)
                _coreMissPending[i] = false;
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
            for (int i = 0; i < NUM_LINES; i++)
            {
                _lines[i].Valid = false;
                _lines[i].Dirty = false;
                _lines[i].Tag = 0;
                _lines[i].DomainTag = 0;
                _lines[i].OwnerCoreId = -1;
            }

            ClearAllMissPending();
            TotalLookups = 0;
            Hits = 0;
            Misses = 0;

            // Phase §2: Reset per-domain fairness counters
            Array.Clear(_domainAccessCounts);
            Array.Clear(_domainHitCounts);
            Array.Clear(_domainMissCounts);
        }

        /// <summary>
        /// Phase §2: Get per-domain access statistics for fairness monitoring.
        /// Returns (totalAccesses, hits, misses) for a given domain slot.
        /// The scheduler can compare these across domains to detect timing side-channel
        /// patterns (e.g., one domain causing disproportionate evictions of another).
        /// HLS: direct register read, zero latency.
        /// </summary>
        /// <param name="domainSlot">Domain slot index (domainTag mod 16)</param>
        /// <returns>Tuple of (accesses, hits, misses)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (long Accesses, long Hits, long Misses) GetDomainAccessCounts(int domainSlot)
        {
            if ((uint)domainSlot >= MAX_DOMAIN_COUNTERS)
                return (0, 0, 0);
            return (_domainAccessCounts[domainSlot], _domainHitCounts[domainSlot], _domainMissCounts[domainSlot]);
        }

        /// <summary>
        /// Phase §2: Reset per-domain fairness counters (e.g., at the start of each
        /// measurement window). Allows periodic fairness evaluation without
        /// accumulating stale data from earlier execution phases.
        /// </summary>
        public void ResetDomainCounters()
        {
            Array.Clear(_domainAccessCounts);
            Array.Clear(_domainHitCounts);
            Array.Clear(_domainMissCounts);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetIndex(ulong address)
        {
            return (int)((address >> OFFSET_BITS) & (NUM_LINES - 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong GetTag(ulong address)
        {
            return address >> (OFFSET_BITS + INDEX_BITS);
        }
    }
}
