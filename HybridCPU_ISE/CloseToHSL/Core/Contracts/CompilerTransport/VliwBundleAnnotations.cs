using System;
using CoreBundleMetadata = YAKSys_Hybrid_CPU.Core.BundleMetadata;
using CoreSlotMetadata = YAKSys_Hybrid_CPU.Core.SlotMetadata;

namespace HybridCPU_ISE.Arch
{
    /// <summary>
    /// Sideband slot annotations that travel with a VT-local instruction stream until lowering.
    /// </summary>
    public sealed class VliwBundleAnnotations
    {
        private readonly InstructionSlotMetadata[] _slotMetadata;

        public static VliwBundleAnnotations Empty { get; } = new(Array.Empty<InstructionSlotMetadata>(), CoreBundleMetadata.Default);

        public CoreBundleMetadata BundleMetadata { get; }

        public VliwBundleAnnotations(InstructionSlotMetadata[] slotMetadata)
            : this(slotMetadata, BuildBundleMetadata(slotMetadata))
        {
        }

        public VliwBundleAnnotations(InstructionSlotMetadata[] slotMetadata, CoreBundleMetadata bundleMetadata)
        {
            ArgumentNullException.ThrowIfNull(slotMetadata);
            ArgumentNullException.ThrowIfNull(bundleMetadata);
            _slotMetadata = slotMetadata;
            BundleMetadata = bundleMetadata;
        }

        public int Count => _slotMetadata.Length;

        public bool TryGetInstructionSlotMetadata(int instructionIndex, out InstructionSlotMetadata metadata)
        {
            if ((uint)instructionIndex < (uint)_slotMetadata.Length)
            {
                metadata = _slotMetadata[instructionIndex];
                return true;
            }

            metadata = default;
            return false;
        }

        private static CoreBundleMetadata BuildBundleMetadata(InstructionSlotMetadata[] slotMetadata)
        {
            if (slotMetadata.Length == 0)
            {
                return CoreBundleMetadata.Default;
            }

            var slots = new CoreSlotMetadata[slotMetadata.Length];
            bool hasExplicitMetadata = false;
            for (int index = 0; index < slotMetadata.Length; index++)
            {
                CoreSlotMetadata metadata = slotMetadata[index].SlotMetadata;
                slots[index] = metadata;
                hasExplicitMetadata |= metadata != CoreSlotMetadata.Default;
            }

            return hasExplicitMetadata
                ? new CoreBundleMetadata { SlotMetadata = slots }
                : CoreBundleMetadata.Default;
        }
    }
}
