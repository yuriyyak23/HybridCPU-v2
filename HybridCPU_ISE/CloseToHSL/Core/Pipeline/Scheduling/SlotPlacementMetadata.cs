using System;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Placement and admission metadata for a slot in the VLIW bundle.
    ///
    /// Blueprint §8 / Checklist P2: "Ввести SlotPlacementMetadata / AdmissionMetadata."
    /// Carries the fields that were previously scattered across <see cref="MicroOp"/>:
    /// <list type="bullet">
    ///   <item><see cref="RequiredSlotClass"/> — physical lane class constraint.</item>
    ///   <item><see cref="PinningKind"/>       — flexible vs. hard-pinned placement.</item>
    ///   <item><see cref="PinnedLaneId"/>       — lane index when hard-pinned.</item>
    ///   <item><see cref="DomainTag"/>          — Singularity-style isolation tag.</item>
    /// </list>
    /// Moving these out of the MicroOp base keeps MicroOp focused on execution semantics
    /// and allows the FSP scheduler to manipulate placement metadata independently.
    /// </summary>
    public struct SlotPlacementMetadata
    {
        /// <summary>
        /// Physical lane class required by this micro-operation.
        /// Determines which subset of the W=8 bundle lanes can host this op.
        /// </summary>
        public SlotClass RequiredSlotClass;

        /// <summary>
        /// Whether this op is flexible within its <see cref="RequiredSlotClass"/> lane
        /// set or hard-pinned to a specific physical lane (<see cref="PinnedLaneId"/>).
        /// </summary>
        public SlotPinningKind PinningKind;

        /// <summary>
        /// Physical lane index (0–7) when <see cref="PinningKind"/> is
        /// <see cref="SlotPinningKind.HardPinned"/>; ignored otherwise.
        /// </summary>
        public byte PinnedLaneId;

        /// <summary>
        /// Domain tag for Singularity-style software isolation.
        /// 0 = no domain restriction (trusted kernel).
        /// </summary>
        public ulong DomainTag;

        /// <summary>
        /// Default: class-flexible, AluClass, no domain restriction.
        /// </summary>
        public static SlotPlacementMetadata Default => new SlotPlacementMetadata
        {
            RequiredSlotClass = SlotClass.AluClass,
            PinningKind       = SlotPinningKind.ClassFlexible,
            PinnedLaneId      = 0,
            DomainTag         = 0,
        };
    }
}
