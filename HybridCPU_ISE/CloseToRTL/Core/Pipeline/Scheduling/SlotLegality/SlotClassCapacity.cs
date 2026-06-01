using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Per-cycle class-capacity snapshot for all slot classes.
    /// Tracks how many lanes of each class are occupied vs total available
    /// in the current VLIW bundle.
    /// <para>
    /// This is a derived summary layered on top of the physical slot view —
    /// it does not replace slot-level occupancy but enables O(1) class-level
    /// admission queries for FSP and scheduling logic.
    /// </para>
    /// <para>
    /// HLS design note: 6 × (3-bit occupied + 3-bit total) = 36 flip-flops.
    /// Fits in a single CLB. Written once per cycle, read by class-admission MUX.
    /// </para>
    /// </summary>
    public struct SlotClassCapacityState
    {
        // Per-class counters: [occupied, total].
        // Total is derived from SlotClassLaneMap at init; occupied is updated per-cycle.

        /// <summary>Number of ALU lanes currently occupied.</summary>
        public byte AluOccupied;

        /// <summary>Total ALU lanes available (from lane map).</summary>
        public byte AluTotal;

        /// <summary>Number of LSU lanes currently occupied.</summary>
        public byte LsuOccupied;

        /// <summary>Total LSU lanes available (from lane map).</summary>
        public byte LsuTotal;

        /// <summary>Number of DMA/Stream lanes currently occupied.</summary>
        public byte DmaStreamOccupied;

        /// <summary>Total DMA/Stream lanes available (from lane map).</summary>
        public byte DmaStreamTotal;

        /// <summary>Number of BranchControl lanes currently occupied.</summary>
        public byte BranchControlOccupied;

        /// <summary>Total BranchControl lanes available (from lane map).</summary>
        public byte BranchControlTotal;

        /// <summary>Number of SystemSingleton lanes currently occupied.</summary>
        public byte SystemSingletonOccupied;

        /// <summary>Total SystemSingleton lanes available (from lane map).</summary>
        public byte SystemSingletonTotal;

        /// <summary>
        /// Check if there is free capacity in the given slot class.
        /// Alias-aware: if the class shares a physical lane with another class
        /// (e.g., BranchControl and SystemSingleton both map to lane 7),
        /// capacity is considered free only if no aliased class has occupied
        /// the shared lane(s).
        /// <para>HLS: 3-bit comparator + optional aliased-occupancy check.</para>
        /// </summary>
        /// <param name="slotClass">The slot class to query.</param>
        /// <returns><see langword="true"/> if at least one lane is free for this class.</returns>
        public readonly bool HasFreeCapacity(SlotClass slotClass)
        {
            int free = GetFreeCapacity(slotClass);
            if (free <= 0) return false;

            // For aliased classes, subtract the occupancy of co-aliased classes
            // that compete for the same physical lane(s).
            var aliased = SlotClassLaneMap.GetAliasedClasses(slotClass);
            int aliasedOccupied = 0;
            foreach (var alias in aliased)
                aliasedOccupied += GetOccupied(alias);

            return free - aliasedOccupied > 0;
        }

        /// <summary>
        /// Get remaining free capacity for a slot class (no alias adjustment).
        /// <para>HLS: 3-bit subtractor.</para>
        /// </summary>
        /// <param name="slotClass">The slot class to query.</param>
        /// <returns>Number of free lanes (total − occupied). May be zero or negative if over-counted.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int GetFreeCapacity(SlotClass slotClass) => slotClass switch
        {
            SlotClass.AluClass        => AluTotal - AluOccupied,
            SlotClass.LsuClass        => LsuTotal - LsuOccupied,
            SlotClass.DmaStreamClass  => DmaStreamTotal - DmaStreamOccupied,
            SlotClass.BranchControl   => BranchControlTotal - BranchControlOccupied,
            SlotClass.SystemSingleton => SystemSingletonTotal - SystemSingletonOccupied,
            _                         => 0,
        };

        /// <summary>
        /// Get raw occupied count for a slot class (no alias adjustment).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly int GetOccupied(SlotClass slotClass) => slotClass switch
        {
            SlotClass.AluClass        => AluOccupied,
            SlotClass.LsuClass        => LsuOccupied,
            SlotClass.DmaStreamClass  => DmaStreamOccupied,
            SlotClass.BranchControl   => BranchControlOccupied,
            SlotClass.SystemSingleton => SystemSingletonOccupied,
            _                         => 0,
        };

        /// <summary>
        /// Increment the occupied counter for the given slot class.
        /// Called after a successful placement (inject/steal).
        /// <para>HLS: 3-bit adder, negligible gate cost.</para>
        /// </summary>
        /// <param name="slotClass">The slot class whose counter to increment.</param>
        public void IncrementOccupancy(SlotClass slotClass)
        {
            switch (slotClass)
            {
                case SlotClass.AluClass:        AluOccupied++;        break;
                case SlotClass.LsuClass:        LsuOccupied++;        break;
                case SlotClass.DmaStreamClass:  DmaStreamOccupied++;  break;
                case SlotClass.BranchControl:   BranchControlOccupied++;   break;
                case SlotClass.SystemSingleton: SystemSingletonOccupied++; break;
                // Unclassified ops do not consume typed capacity
            }
        }

        /// <summary>
        /// Initialize total capacities from <see cref="SlotClassLaneMap"/>.
        /// Called once at scheduler reset or configuration change.
        /// </summary>
        public void InitializeFromLaneMap()
        {
            AluTotal             = (byte)SlotClassLaneMap.GetClassCapacity(SlotClass.AluClass);
            LsuTotal             = (byte)SlotClassLaneMap.GetClassCapacity(SlotClass.LsuClass);
            DmaStreamTotal       = (byte)SlotClassLaneMap.GetClassCapacity(SlotClass.DmaStreamClass);
            BranchControlTotal   = (byte)SlotClassLaneMap.GetClassCapacity(SlotClass.BranchControl);
            SystemSingletonTotal = (byte)SlotClassLaneMap.GetClassCapacity(SlotClass.SystemSingleton);

            ResetOccupancy();
        }

        /// <summary>
        /// Reset all occupied counters to zero (start of new bundle).
        /// Totals are preserved.
        /// <para>HLS: 5 × wire-reset to 0.</para>
        /// </summary>
        public void ResetOccupancy()
        {
            AluOccupied = 0;
            LsuOccupied = 0;
            DmaStreamOccupied = 0;
            BranchControlOccupied = 0;
            SystemSingletonOccupied = 0;
        }
    }

    /// <summary>
    /// Static helpers for computing <see cref="SlotClassCapacityState"/> from a VLIW bundle.
    /// </summary>
    public static class SlotClassCapacity
    {
        /// <summary>
        /// Scan a VLIW bundle and compute per-class occupancy.
        /// <para>HLS: 8-iteration combinational scan (1 comparator per slot).</para>
        /// </summary>
        /// <param name="bundle">The VLIW bundle (up to <paramref name="bundleWidth"/> slots).</param>
        /// <param name="bundleWidth">Number of slots in the bundle (default 8).</param>
        /// <returns>Fully initialized capacity state with occupancy counts.</returns>
        public static SlotClassCapacityState ComputeFromBundle(MicroOp?[] bundle, int bundleWidth = 8)
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();

            int limit = bundle.Length < bundleWidth ? bundle.Length : bundleWidth;
            for (int i = 0; i < limit; i++)
            {
                if (bundle[i] is not null)
                {
                    state.IncrementOccupancy(bundle[i]!.Placement.RequiredSlotClass);
                }
            }

            return state;
        }
    }
}
