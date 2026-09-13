using System;
using System.Collections.Generic;
using System.Numerics;
using global::YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Core.Legality
{
    /// <summary>
    /// Canonical Phase 03 legality-only descriptor for one decoded bundle.
    /// Carries bundle identity, slot occupancy, typed slot facts, and the
    /// bundle-local hazard summary, but excludes cluster issue preparation,
    /// narrow-vs-wide admission prep, and scheduler-specific artifacts.
    /// </summary>
    public sealed class BundleLegalityDescriptor
    {
        private const byte AllSlotMask = (byte)((1 << BundleMetadata.BundleSlotCount) - 1);
        private readonly DecodedSlotLegality[] _slotLegalities;

        public BundleLegalityDescriptor(
            ulong bundleAddress,
            ulong bundleSerial,
            byte occupiedSlotMask,
            DecodedBundleTypedSlotFacts typedSlotFacts = default,
            DecodedBundleFlags flags = DecodedBundleFlags.None,
            byte maxVirtualThreadSpan = 0,
            IReadOnlyList<DecodedSlotLegality>? slotLegalities = null,
            DecodedBundleDependencySummary? dependencySummary = null,
            bool hasDecodeFault = false)
        {
            ValidateSlotMask(nameof(occupiedSlotMask), occupiedSlotMask);

            BundleAddress = bundleAddress;
            BundleSerial = bundleSerial;
            OccupiedSlotMask = occupiedSlotMask;
            TypedSlotFacts = typedSlotFacts;
            Flags = flags;
            MaxVirtualThreadSpan = maxVirtualThreadSpan;
            _slotLegalities = NormalizeSlotLegalities(slotLegalities, occupiedSlotMask);
            DependencySummary = dependencySummary;
            HasDecodeFault = hasDecodeFault;
        }

        public ulong BundleAddress { get; }

        public ulong BundleSerial { get; }

        public byte OccupiedSlotMask { get; }

        public byte EmptySlotMask => (byte)(AllSlotMask & ~OccupiedSlotMask);

        public DecodedBundleTypedSlotFacts TypedSlotFacts { get; }

        public DecodedBundleFlags Flags { get; }

        public byte MaxVirtualThreadSpan { get; }

        public IReadOnlyList<DecodedSlotLegality> SlotLegalities => _slotLegalities;

        public DecodedBundleDependencySummary? DependencySummary { get; }

        /// <summary>
        /// True when canonical bundle decode faulted and the runtime continued through the
        /// fallback materializer contour. Legality authority remains absent in this state.
        /// </summary>
        public bool HasDecodeFault { get; }

        public int SlotCount => BundleMetadata.BundleSlotCount;

        public int OccupiedSlotCount => BitOperations.PopCount((uint)OccupiedSlotMask);

        public bool IsEmpty => OccupiedSlotMask == 0;

        public bool HasDependencySummary => DependencySummary.HasValue;

        public bool HasControlFlow => (Flags & DecodedBundleFlags.HasControlFlow) != 0;

        public bool HasMemoryOps => (Flags & DecodedBundleFlags.HasMemoryOps) != 0;

        public bool HasVectorOps => (Flags & DecodedBundleFlags.HasVectorOps) != 0;

        public bool HasCrossThreadSpan => (Flags & DecodedBundleFlags.HasCrossThreadSpan) != 0;

        public DecodedSlotLegality GetSlotLegality(int index)
        {
            ValidateSlotIndex(nameof(index), index);
            return _slotLegalities[index];
        }

        public static BundleLegalityDescriptor CreateEmpty(
            ulong bundleAddress,
            ulong bundleSerial = 0)
        {
            return new BundleLegalityDescriptor(
                bundleAddress,
                bundleSerial,
                occupiedSlotMask: 0);
        }

        public static BundleLegalityDescriptor CreateDecodeFault(
            ulong bundleAddress,
            ulong bundleSerial = 0)
        {
            return new BundleLegalityDescriptor(
                bundleAddress,
                bundleSerial,
                occupiedSlotMask: 0,
                hasDecodeFault: true);
        }

        private static DecodedSlotLegality[] NormalizeSlotLegalities(
            IReadOnlyList<DecodedSlotLegality>? slotLegalities,
            byte occupiedSlotMask)
        {
            var normalized = new DecodedSlotLegality[BundleMetadata.BundleSlotCount];
            var seen = new bool[normalized.Length];
            for (int i = 0; i < normalized.Length; i++)
                normalized[i] = DecodedSlotLegality.CreateEmpty(i);

            if (slotLegalities is not null)
            {
                for (int i = 0; i < slotLegalities.Count; i++)
                {
                    DecodedSlotLegality slotLegality = slotLegalities[i];
                    int slotIndex = slotLegality.SlotIndex;
                    ValidateSlotIndex(nameof(slotLegalities), slotIndex);

                    if (seen[slotIndex])
                        throw new ArgumentException($"Slot legality list contains duplicate slot {slotIndex}.", nameof(slotLegalities));

                    normalized[slotIndex] = slotLegality;
                    seen[slotIndex] = true;
                }
            }

            for (int slotIndex = 0; slotIndex < normalized.Length; slotIndex++)
            {
                bool isOccupied = (occupiedSlotMask & (1 << slotIndex)) != 0;
                if (!seen[slotIndex])
                {
                    normalized[slotIndex] = isOccupied
                        ? DecodedSlotLegality.CreateOccupied(slotIndex, SlotClass.Unclassified)
                        : DecodedSlotLegality.CreateEmpty(slotIndex);
                    continue;
                }

                if (normalized[slotIndex].IsOccupied != isOccupied)
                {
                    throw new ArgumentException(
                        $"Slot legality occupancy for slot {slotIndex} does not match occupied slot mask 0x{occupiedSlotMask:X2}.",
                        nameof(slotLegalities));
                }
            }

            return normalized;
        }


        private static void ValidateSlotIndex(string paramName, int slotIndex)
        {
            if ((uint)slotIndex >= BundleMetadata.BundleSlotCount)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    slotIndex,
                    $"Slot index must be in [0, {BundleMetadata.BundleSlotCount - 1}].");
            }
        }

        private static void ValidateSlotMask(string paramName, byte slotMask)
        {
            if ((slotMask & ~AllSlotMask) != 0)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    slotMask,
                    $"Slot mask must not set bits outside bundle width {BundleMetadata.BundleSlotCount}.");
            }
        }
    }
}
