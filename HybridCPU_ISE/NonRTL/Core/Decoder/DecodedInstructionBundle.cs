using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Pipeline;

namespace YAKSys_Hybrid_CPU.Core.Decoder
{
    /// <summary>
    /// Canonical phase 03 decoder contract for one decoded frontend bundle.
    /// Carries only bundle identity plus per-slot <see cref="DecodedInstruction"/> records.
    /// </summary>
    public sealed class DecodedInstructionBundle : IAbstractBundle
    {
        private readonly DecodedInstruction[] _slots;

        public DecodedInstructionBundle(
            ulong bundleAddress,
            ulong bundleSerial,
            IReadOnlyList<DecodedInstruction>? slots = null,
            BundleMetadata? bundleMetadata = null,
            bool hasDecodeFault = false)
        {
            BundleAddress = bundleAddress;
            BundleSerial = bundleSerial;
            _slots = NormalizeSlots(slots);
            BundleMetadata = bundleMetadata ?? BundleMetadata.Default;
            HasDecodeFault = hasDecodeFault;
        }

        public ulong BundleAddress { get; }

        public ulong BundleSerial { get; }

        public BundleMetadata BundleMetadata { get; }

        /// <summary>
        /// True when the canonical frontend failed to decode this fetched bundle and the
        /// runtime had to continue through the fallback materializer contour.
        /// This does not grant semantic authority to the fallback path.
        /// </summary>
        public bool HasDecodeFault { get; }

        public int SlotCount => _slots.Length;

        public IReadOnlyList<DecodedInstruction> Slots => _slots;

        public bool IsEmpty
        {
            get
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i].IsOccupied)
                        return false;
                }

                return true;
            }
        }

        public int OccupiedSlotCount
        {
            get
            {
                int occupied = 0;
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i].IsOccupied)
                        occupied++;
                }

                return occupied;
            }
        }

        public DecodedInstruction GetDecodedSlot(int index)
        {
            if ((uint)index >= (uint)_slots.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Slot index must be in [0, {_slots.Length - 1}].");

            return _slots[index];
        }

        public IAbstractBundleSlot GetSlot(int index) => GetDecodedSlot(index);

        public static DecodedInstructionBundle CreateEmpty(ulong bundleAddress, ulong bundleSerial = 0)
            => new(bundleAddress, bundleSerial, bundleMetadata: BundleMetadata.Default);

        public static DecodedInstructionBundle CreateDecodeFault(ulong bundleAddress, ulong bundleSerial = 0)
            => new(
                bundleAddress,
                bundleSerial,
                bundleMetadata: BundleMetadata.Default,
                hasDecodeFault: true);

        private static DecodedInstruction[] NormalizeSlots(IReadOnlyList<DecodedInstruction>? slots)
        {
            var normalized = new DecodedInstruction[BundleMetadata.BundleSlotCount];
            var seen = new bool[normalized.Length];
            for (int i = 0; i < normalized.Length; i++)
                normalized[i] = DecodedInstruction.CreateEmpty(i);

            if (slots is null)
                return normalized;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i] ?? throw new ArgumentNullException(nameof(slots), "Decoded bundle slots must not contain null entries.");
                int slotIndex = slot.SlotIndex;

                if ((uint)slotIndex >= (uint)normalized.Length)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(slots),
                        $"Decoded slot index {slotIndex} is outside bundle width {normalized.Length}.");
                }

                if (seen[slotIndex])
                    throw new ArgumentException($"Decoded bundle contains duplicate slot {slotIndex}.", nameof(slots));

                normalized[slotIndex] = slot;
                seen[slotIndex] = true;
            }

            return normalized;
        }
    }
}
