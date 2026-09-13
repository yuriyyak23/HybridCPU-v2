using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Retained compatibility slot admission decision: a free slot that may be
    /// filled by an operation from the specified donor virtual thread.
    /// </summary>
    public readonly struct FspPilferDecision
    {
        /// <summary>Slot index in the bundle (0-7).</summary>
        public readonly int SlotIndex;

        /// <summary>Virtual-thread ID of the recommended donor.</summary>
        public readonly int DonorVtId;

        public FspPilferDecision(int slotIndex, int donorVtId)
        {
            SlotIndex = slotIndex;
            DonorVtId = donorVtId;
        }
    }

    /// <summary>
    /// Retained compatibility result from <see cref="FspProcessor.TryPilfer"/>.
    /// The legacy public member names are preserved; their current semantics are
    /// metadata-gated bundle densification and slot admission.
    /// </summary>
    public sealed class FspResult
    {
        private readonly List<FspPilferDecision> _decisions = new();

        /// <summary>Legacy-named ordered list of slot admission decisions.</summary>
        public IReadOnlyList<FspPilferDecision> PilferDecisions => _decisions;

        /// <summary><c>true</c> if at least one slot was admitted.</summary>
        public bool HasPilfers => _decisions.Count > 0;

        /// <summary>Number of eligible slots found.</summary>
        public int PilferCount => _decisions.Count;

        internal void AddPilfer(int slotIndex, int donorVtId)
            => _decisions.Add(new FspPilferDecision(slotIndex, donorVtId));
    }

    /// <summary>
    /// Retained compatibility processor for metadata-gated bundle densification.
    /// Current architecture vocabulary is slot admission over `BundleMetadata` and
    /// `SlotMetadata`, not a central replay or assist explanation.
    ///
    /// Bounded determinism contract: given the same <paramref name="bundle"/> and
    /// <paramref name="bundleMeta"/> inputs, <see cref="TryPilfer"/> always produces
    /// the same <see cref="FspResult"/>. The donor-selection fallback uses a
    /// lowest-VT scan that is fixed for a fixed bundle snapshot.
    /// </summary>
    public sealed class FspProcessor
    {
        /// <summary>
        /// Identify free slots in <paramref name="bundle"/> that are eligible for
        /// slot admission according to the per-slot policy in <paramref name="bundleMeta"/>.
        /// The method does not modify <paramref name="bundle"/>; the caller is
        /// responsible for materializing any donor operation.
        /// </summary>
        /// <param name="bundle">
        /// Current bundle snapshot (array of 8 MicroOp slots; <c>null</c> or
        /// <see cref="NopMicroOp"/> slots are treated as free).
        /// </param>
        /// <param name="bundleMeta">
        /// Metadata for the bundle. Must not be <c>null</c>.
        /// </param>
        /// <returns>
        /// An <see cref="FspResult"/> listing every free, admissible slot and its
        /// recommended donor VT. Empty if none qualify or if
        /// <see cref="BundleMetadata.FspBoundary"/> is set.
        /// </returns>
        public FspResult TryPilfer(MicroOp?[] bundle, BundleMetadata bundleMeta)
        {
            var result = new FspResult();

            if (bundle == null || bundleMeta == null)
                return result;

            if (bundleMeta.FspBoundary)
                return result;

            for (int slotIdx = 0; slotIdx < bundle.Length; slotIdx++)
            {
                MicroOp? existing = bundle[slotIdx];
                if (existing != null && existing is not NopMicroOp)
                    continue;

                SlotMetadata slotMeta = bundleMeta.GetSlotMetadata(slotIdx);
                if (slotMeta.StealabilityPolicy != StealabilityPolicy.Stealable)
                    continue;

                int donorVt = slotMeta.DonorVtHint != 0xFF
                    ? slotMeta.DonorVtHint
                    : SelectLowestAvailableVt(bundle, slotIdx);

                if (donorVt == 0xFF)
                    continue;

                result.AddPilfer(slotIdx, donorVt);
            }

            return result;
        }

        /// <summary>
        /// Scan <paramref name="bundle"/> for the lowest virtual-thread ID that owns
        /// a filled (non-null, non-NOP) slot other than <paramref name="excludeSlot"/>.
        /// Returns <c>0xFF</c> if no filled slot is found.
        /// </summary>
        private static int SelectLowestAvailableVt(MicroOp?[] bundle, int excludeSlot)
        {
            int lowestVt = 0xFF;
            for (int i = 0; i < bundle.Length; i++)
            {
                if (i == excludeSlot) continue;
                MicroOp? op = bundle[i];
                if (op == null || op is NopMicroOp) continue;
                int vtId = op.VirtualThreadId;
                if (vtId < lowestVt)
                    lowestVt = vtId;
            }

            return lowestVt;
        }
    }
}
