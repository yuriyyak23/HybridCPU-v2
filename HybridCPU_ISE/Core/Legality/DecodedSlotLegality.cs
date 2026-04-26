using System;
using global::YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Core.Legality
{
    /// <summary>
    /// Canonical Phase 03 legality result for one physical bundle slot.
    /// Carries only slot identity, structural class, and bundle-local peer conflict masks.
    /// Excludes cluster issue intent, scheduler placement metadata, and runtime ownership fields.
    /// </summary>
    public readonly struct DecodedSlotLegality
    {
        private const byte AllSlotMask = (byte)((1 << BundleMetadata.BundleSlotCount) - 1);

        public DecodedSlotLegality(
            int slotIndex,
            bool isOccupied,
            SlotClass slotClass = SlotClass.Unclassified,
            byte hardRejectPeerMask = 0,
            byte needsRuntimeCheckPeerMask = 0,
            HazardEffectKind dominantEffectKind = HazardEffectKind.RegisterData)
        {
            ValidateSlotIndex(slotIndex);
            ValidatePeerMask(nameof(hardRejectPeerMask), slotIndex, hardRejectPeerMask);
            ValidatePeerMask(nameof(needsRuntimeCheckPeerMask), slotIndex, needsRuntimeCheckPeerMask);

            SlotIndex = slotIndex;
            IsOccupied = isOccupied;
            SlotClass = isOccupied ? slotClass : SlotClass.Unclassified;
            HardRejectPeerMask = isOccupied ? hardRejectPeerMask : (byte)0;
            NeedsRuntimeCheckPeerMask = isOccupied ? needsRuntimeCheckPeerMask : (byte)0;
            DominantEffectKind = dominantEffectKind;
        }

        public int SlotIndex { get; }

        public bool IsOccupied { get; }

        public SlotClass SlotClass { get; }

        public byte HardRejectPeerMask { get; }

        public byte NeedsRuntimeCheckPeerMask { get; }

        public HazardEffectKind DominantEffectKind { get; }

        public byte BlockingPeerMask => (byte)(HardRejectPeerMask | NeedsRuntimeCheckPeerMask);

        public bool HasHardRejectPeers => HardRejectPeerMask != 0;

        public bool NeedsRuntimeChecks => NeedsRuntimeCheckPeerMask != 0;

        public bool HasPeerConflicts => BlockingPeerMask != 0;

        public bool IsWideCandidate => IsOccupied && HardRejectPeerMask == 0;

        public static DecodedSlotLegality CreateEmpty(int slotIndex)
            => new(slotIndex, isOccupied: false);

        public static DecodedSlotLegality CreateOccupied(
            int slotIndex,
            SlotClass slotClass,
            byte hardRejectPeerMask = 0,
            byte needsRuntimeCheckPeerMask = 0,
            HazardEffectKind dominantEffectKind = HazardEffectKind.RegisterData)
        {
            return new DecodedSlotLegality(
                slotIndex,
                isOccupied: true,
                slotClass,
                hardRejectPeerMask,
                needsRuntimeCheckPeerMask,
                dominantEffectKind);
        }

        private static void ValidateSlotIndex(int slotIndex)
        {
            if ((uint)slotIndex >= BundleMetadata.BundleSlotCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(slotIndex),
                    slotIndex,
                    $"Slot index must be in [0, {BundleMetadata.BundleSlotCount - 1}].");
            }
        }

        private static void ValidatePeerMask(string paramName, int slotIndex, byte peerMask)
        {
            if ((peerMask & ~AllSlotMask) != 0)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    peerMask,
                    $"Peer mask must not set bits outside bundle width {BundleMetadata.BundleSlotCount}.");
            }

            byte selfBit = (byte)(1 << slotIndex);
            if ((peerMask & selfBit) != 0)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    peerMask,
                    "Peer mask must not include the slot itself.");
            }
        }
    }
}
