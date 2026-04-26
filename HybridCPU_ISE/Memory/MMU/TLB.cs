using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Memory
{
    /// <summary>
    /// HLS-compatible Translation Lookaside Buffer (fully-associative CAM).
    ///
    /// <para>Caches VPN → PPN translations to avoid costly 2-level page walks
    /// on every memory access. In hardware, all 16 comparisons execute in
    /// parallel (Content-Addressable Memory).</para>
    ///
    /// <para><b>HLS characteristics:</b></para>
    /// <list type="bullet">
    ///   <item>16-entry fully-associative CAM: ~200 LUT (Xilinx UltraScale+).</item>
    ///   <item>Single-cycle hit latency (<see cref="HIT_LATENCY_CYCLES"/> = 1).</item>
    ///   <item>Miss penalty: <see cref="MISS_PENALTY_CYCLES"/> = 8 (2-level page walk).</item>
    ///   <item>LRU replacement: 3-bit age counter per entry, updated on every access.</item>
    ///   <item>Zero heap allocation — <see cref="InlineArray"/>-backed fixed storage.</item>
    ///   <item><see cref="FlushDomain"/>: 16 AND-masks in 1 cycle.</item>
    /// </list>
    /// </summary>
    public struct TLB
    {
        /// <summary>Number of TLB entries (HLS: maps to 16-entry CAM).</summary>
        private const int TLB_ENTRIES = 16;

        /// <summary>Miss penalty in cycles (2-level page walk through memory).</summary>
        public const int MISS_PENALTY_CYCLES = 8;

        /// <summary>Hit latency (single-cycle CAM lookup in hardware).</summary>
        public const int HIT_LATENCY_CYCLES = 1;

        /// <summary>TLB entry: VPN → PPN mapping with permissions and domain tag.</summary>
        public struct TlbEntry
        {
            /// <summary>Virtual Page Number (VA &gt;&gt; 12).</summary>
            public ulong VPN;

            /// <summary>Physical Page Number (PA &gt;&gt; 12).</summary>
            public ulong PPN;

            /// <summary>Permission bits (bit 1 = Read, bit 2 = Write).</summary>
            public byte Permissions;

            /// <summary>Valid bit — entry is active.</summary>
            public bool Valid;

            /// <summary>Domain/ASID tag for per-thread/device isolation.</summary>
            public int DomainId;

            /// <summary>LRU age counter for replacement (higher = older).</summary>
            public byte LruAge;
        }

        [InlineArray(TLB_ENTRIES)]
        private struct TlbEntryArray
        {
            private TlbEntry _element0;
        }

        private TlbEntryArray _entries;

        /// <summary>Number of TLB hits since last flush.</summary>
        public ulong Hits { get; private set; }

        /// <summary>Number of TLB misses since last flush.</summary>
        public ulong Misses { get; private set; }

        /// <summary>
        /// Lookup VPN in TLB. O(1) in hardware (parallel CAM compare).
        /// In software: linear scan of 16 entries (small enough to stay in L1).
        /// </summary>
        /// <param name="virtualAddress">Full virtual address.</param>
        /// <param name="domainId">Thread/ASID/device ID for isolation.</param>
        /// <param name="physicalAddress">Translated physical address on hit.</param>
        /// <param name="permissions">Cached permission bits on hit.</param>
        /// <returns>True on hit, false on miss (caller must do page walk).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryTranslate(ulong virtualAddress, int domainId,
                                 out ulong physicalAddress, out byte permissions)
        {
            ulong vpn = virtualAddress >> 12;
            ulong offset = virtualAddress & 0xFFF;

            for (int i = 0; i < TLB_ENTRIES; i++)
            {
                ref var entry = ref _entries[i];
                if (entry.Valid && entry.VPN == vpn && entry.DomainId == domainId)
                {
                    physicalAddress = (entry.PPN << 12) | offset;
                    permissions = entry.Permissions;
                    Hits++;
                    UpdateLru(i);
                    return true;
                }
            }

            physicalAddress = 0;
            permissions = 0;
            Misses++;
            return false;
        }

        /// <summary>
        /// Insert entry after page walk (replaces LRU victim or first invalid slot).
        /// Called by IOMMU on TLB miss after successful page walk.
        /// </summary>
        /// <param name="virtualAddress">Virtual address that was translated.</param>
        /// <param name="physicalAddress">Resulting physical address from page walk.</param>
        /// <param name="permissions">Permission bits (bit 1 = Read, bit 2 = Write).</param>
        /// <param name="domainId">Domain/ASID for this mapping.</param>
        public void Insert(ulong virtualAddress, ulong physicalAddress,
                           byte permissions, int domainId)
        {
            ulong vpn = virtualAddress >> 12;
            ulong ppn = physicalAddress >> 12;

            int victimIdx = 0;
            byte maxAge = 0;

            for (int i = 0; i < TLB_ENTRIES; i++)
            {
                if (!_entries[i].Valid)
                {
                    victimIdx = i;
                    break;
                }

                if (_entries[i].LruAge > maxAge)
                {
                    maxAge = _entries[i].LruAge;
                    victimIdx = i;
                }
            }

            _entries[victimIdx] = new TlbEntry
            {
                VPN = vpn,
                PPN = ppn,
                Permissions = permissions,
                Valid = true,
                DomainId = domainId,
                LruAge = 0
            };
        }

        /// <summary>
        /// Invalidate all entries for a given domain (context switch / domain free).
        /// HLS: 16 parallel AND-masks, single cycle.
        /// </summary>
        /// <param name="domainId">Domain whose entries to invalidate.</param>
        public void FlushDomain(int domainId)
        {
            for (int i = 0; i < TLB_ENTRIES; i++)
            {
                if (_entries[i].DomainId == domainId)
                    _entries[i].Valid = false;
            }
        }

        /// <summary>
        /// Full TLB flush (global page table change). Resets counters.
        /// </summary>
        public void FlushAll()
        {
            for (int i = 0; i < TLB_ENTRIES; i++)
                _entries[i].Valid = false;

            Hits = 0;
            Misses = 0;
        }

        private void UpdateLru(int hitIndex)
        {
            for (int i = 0; i < TLB_ENTRIES; i++)
            {
                if (_entries[i].Valid && i != hitIndex)
                    _entries[i].LruAge++;
            }

            _entries[hitIndex].LruAge = 0;
        }
    }
}
