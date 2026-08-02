using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace YAKSys_Hybrid_CPU.Core.Pipeline.Scheduling
{
    /// <summary>
    /// Retained compatibility slot admission policy for metadata-gated bundle densification.
    ///
    /// The public type name is kept for older tests and adapters. Current architecture
    /// vocabulary should describe this as bundle densification / slot admission, not as
    /// the central replay or assist story.
    ///
    /// The policy depends exclusively on:
    /// <list type="bullet">
    ///   <item><see cref="BundleMetadata.FspBoundary"/>: when true, no free slots
    ///     are admitted across this bundle boundary.</item>
    ///   <item><see cref="SlotMetadata.StealabilityPolicy"/>: per-slot opt-out for
    ///     privileged, control-flow, or atomic sequences.</item>
    /// </list>
    ///
    /// This class deliberately has no knowledge of <c>MicroOp.CanBeStolen</c>,
    /// opcode tables, or ISA-specific fields. It is a compatibility adapter over
    /// metadata-owned slot-admission facts.
    ///
    /// Bounded determinism contract: given the same <paramref name="bundle"/> and
    /// <paramref name="bundleMeta"/> inputs, <see cref="Evaluate"/> returns the same
    /// <see cref="SlotAssignment"/>.
    /// </summary>
    public sealed class FspAdmissionPolicy
    {
        /// <summary>
        /// Evaluate which free slots in <paramref name="bundle"/> may be admitted for
        /// bundle densification according to the per-slot policy in
        /// <paramref name="bundleMeta"/>.
        ///
        /// A slot is admitted iff:
        /// <list type="bullet">
        ///   <item>The bundle does not have <see cref="BundleMetadata.FspBoundary"/> set,</item>
        ///   <item>The slot position in <paramref name="bundle"/> is <see langword="null"/>
        ///     and therefore free, and</item>
        ///   <item>The corresponding <see cref="SlotMetadata.StealabilityPolicy"/> is
        ///     <see cref="StealabilityPolicy.Stealable"/>.</item>
        /// </list>
        /// </summary>
        /// <param name="bundle">
        /// Bundle snapshot. <see langword="null"/> elements indicate free slots.
        /// Non-<see langword="null"/> elements are occupied and are never admitted.
        /// </param>
        /// <param name="bundleMeta">
        /// Per-bundle metadata containing the boundary flag and per-slot admission
        /// policies. Must not be <see langword="null"/>.
        /// </param>
        /// <param name="donorVtId">
        /// VT identifier of the thread that may fill admitted free slots.
        /// </param>
        /// <returns>
        /// A <see cref="SlotAssignment"/> listing every admitted free slot.
        /// Returns <see cref="SlotAssignment.Empty"/> when the bundle boundary blocks
        /// admission or when no free admissible slots exist.
        /// </returns>
        public SlotAssignment Evaluate(
            IReadOnlyList<InstructionIR?> bundle,
            BundleMetadata bundleMeta,
            int donorVtId)
        {
            if (bundleMeta is null) throw new ArgumentNullException(nameof(bundleMeta));

            if (bundle is null || bundle.Count == 0)
                return SlotAssignment.Empty;

            if (bundleMeta.FspBoundary)
                return SlotAssignment.Empty;

            var result = new SlotAssignment();

            for (int i = 0; i < bundle.Count; i++)
            {
                if (bundle[i] is not null) continue;

                var slotMeta = bundleMeta.GetSlotMetadata(i);
                if (slotMeta.StealabilityPolicy != StealabilityPolicy.Stealable) continue;

                result.Add(i, donorVtId);
            }

            return result;
        }
    }
}
