using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core.Pipeline.Scheduling
{
    /// <summary>
    /// Retained compatibility slot admission result for a single bundle cycle.
    ///
    /// Produced by <see cref="FspAdmissionPolicy"/> and consumed by the
    /// instruction-fetch / dispatch stage. Each entry identifies a slot
    /// index that is free and stealable, together with the VT donor that
    /// should fill it.
    ///
    /// Current architecture vocabulary should describe this as metadata-gated
    /// bundle densification and slot admission, not as a central replay or
    /// assist story.
    ///
    /// A <see cref="SlotAssignment"/> is immutable once constructed.
    /// </summary>
    public sealed class SlotAssignment
    {
        private readonly List<SlotAdmission> _entries = new();

        /// <summary>Ordered list of admitted (slotIndex, donorVtId) pairs.</summary>
        public IReadOnlyList<SlotAdmission> Entries => _entries;

        /// <summary><c>true</c> when at least one slot was admitted.</summary>
        public bool HasAssignments => _entries.Count > 0;

        /// <summary>Number of admitted slots in this assignment.</summary>
        public int Count => _entries.Count;

        /// <summary>
        /// Empty assignment returned when <see cref="BundleMetadata.FspBoundary"/>
        /// is set or when no slots are free and stealable.
        /// </summary>
        public static readonly SlotAssignment Empty = new();

        internal void Add(int slotIndex, int donorVtId)
            => _entries.Add(new SlotAdmission(slotIndex, donorVtId));
    }

    /// <summary>
    /// A single retained compatibility slot admission decision: a free stealable
    /// slot and its donor VT.
    /// </summary>
    public readonly struct SlotAdmission
    {
        /// <summary>Zero-based slot index in the VLIW bundle (0-7).</summary>
        public readonly int SlotIndex;

        /// <summary>VT identifier of the recommended donor thread.</summary>
        public readonly int DonorVtId;

        /// <summary>Create an admission entry.</summary>
        public SlotAdmission(int slotIndex, int donorVtId)
        {
            SlotIndex = slotIndex;
            DonorVtId = donorVtId;
        }
    }
}
