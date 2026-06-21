using System;
using System.Collections.Generic;
using global::YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Core.Legality
{
    /// <summary>
    /// Legality-layer projection of one prepared scalar-cluster entry.
    /// Mirrors the current compatibility surface without carrying runtime policy.
    /// </summary>
    public readonly struct ClusterIssueIntentEntry
    {
        public ClusterIssueIntentEntry(DecodedBundleSlotDescriptor slotDescriptor)
            : this(
                slotDescriptor.SlotIndex,
                slotDescriptor.VirtualThreadId,
                slotDescriptor.OwnerThreadId,
                slotDescriptor.OpCode,
                slotDescriptor.MicroOp)
        {
        }

        internal ClusterIssueIntentEntry(
            byte slotIndex,
            int virtualThreadId,
            int ownerThreadId,
            uint opCode,
            MicroOp microOp)
        {
            SlotIndex = slotIndex;
            VirtualThreadId = virtualThreadId;
            OwnerThreadId = ownerThreadId;
            OpCode = opCode;
            MicroOp = microOp;
        }

        public MicroOp MicroOp { get; }
        public DecodedBundleSlotDescriptor SlotDescriptor
        {
            get
            {
                DecodedBundleSlotDescriptor slotDescriptor =
                    DecodedBundleSlotDescriptor.Create(SlotIndex, MicroOp);
                return new DecodedBundleSlotDescriptor(
                    slotDescriptor.MicroOp,
                    slotDescriptor.SlotIndex,
                    VirtualThreadId,
                    OwnerThreadId,
                    OpCode,
                    slotDescriptor.ReadRegisters,
                    slotDescriptor.WriteRegisters,
                    slotDescriptor.WritesRegister,
                    slotDescriptor.IsMemoryOp,
                    slotDescriptor.IsControlFlow,
                    slotDescriptor.Placement,
                    slotDescriptor.MemoryBankIntent,
                    slotDescriptor.IsFspInjected,
                    slotDescriptor.IsEmptyOrNop,
                    slotDescriptor.IsVectorOp);
            }
        }

        public byte SlotIndex { get; }

        public int VirtualThreadId { get; }

        public int OwnerThreadId { get; }

        public uint OpCode { get; }

        public static ClusterIssueIntentEntry FromScalarClusterIssueEntry(ScalarClusterIssueEntry entry)
            => new(entry.SlotIndex, entry.VirtualThreadId, entry.OwnerThreadId, entry.OpCode, entry.MicroOp);

        public ScalarClusterIssueEntry ToScalarClusterIssueEntry()
            => new(SlotIndex, VirtualThreadId, OwnerThreadId, OpCode, MicroOp);
    }

    /// <summary>
    /// Canonical legality-layer contract for decode-derived scalar cluster issue intent.
    /// Keeps only bundle-local scalar targeting facts and exposes compatibility
    /// projections to the existing runtime-facing group shape.
    /// </summary>
    public sealed class ClusterIssueIntent
    {
        private const byte AllSlotMask = (byte)((1 << BundleMetadata.BundleSlotCount) - 1);
        private readonly ClusterIssueIntentEntry[] _entries;

        public ClusterIssueIntent(
            byte candidateMask,
            byte preparedMask,
            IReadOnlyList<ClusterIssueIntentEntry>? entries = null)
        {
            ValidateSlotMask(nameof(candidateMask), candidateMask);
            ValidateSlotMask(nameof(preparedMask), preparedMask);

            if ((preparedMask & ~candidateMask) != 0)
            {
                throw new ArgumentException(
                    "Prepared mask must be a subset of candidate mask.",
                    nameof(preparedMask));
            }

            CandidateMask = candidateMask;
            PreparedMask = preparedMask;
            _entries = NormalizeEntries(entries, preparedMask);
        }

        public byte CandidateMask { get; }

        public byte PreparedMask { get; }

        /// <summary>
        /// Prepared entries in their producer-defined order.
        /// Order is preserved so compatibility projections cannot drift from the current runtime surface.
        /// </summary>
        public IReadOnlyList<ClusterIssueIntentEntry> PreparedEntries => _entries;

        /// <summary>
        /// Compatibility alias that mirrors <see cref="ScalarClusterIssueGroup.Entries"/>.
        /// </summary>
        public IReadOnlyList<ClusterIssueIntentEntry> Entries => PreparedEntries;

        public int Count => _entries.Length;

        public VtScalarCandidateSummary BuildVtSummary()
        {
            int vt0 = 0;
            int vt1 = 0;
            int vt2 = 0;
            int vt3 = 0;

            for (int i = 0; i < _entries.Length; i++)
            {
                switch (_entries[i].VirtualThreadId)
                {
                    case 0:
                        vt0++;
                        break;
                    case 1:
                        vt1++;
                        break;
                    case 2:
                        vt2++;
                        break;
                    case 3:
                        vt3++;
                        break;
                }
            }

            return new VtScalarCandidateSummary(vt0, vt1, vt2, vt3);
        }

        public static ClusterIssueIntent CreateEmpty()
            => new(candidateMask: 0, preparedMask: 0);

        public static ClusterIssueIntent FromScalarClusterIssueGroup(ScalarClusterIssueGroup scalarClusterGroup)
        {
            ArgumentNullException.ThrowIfNull(scalarClusterGroup);

            if (scalarClusterGroup.Count == 0)
                return new ClusterIssueIntent(scalarClusterGroup.CandidateMask, scalarClusterGroup.PreparedMask);

            var entries = new ClusterIssueIntentEntry[scalarClusterGroup.Entries.Length];
            for (int i = 0; i < scalarClusterGroup.Entries.Length; i++)
                entries[i] = ClusterIssueIntentEntry.FromScalarClusterIssueEntry(scalarClusterGroup.Entries[i]);

            return new ClusterIssueIntent(
                scalarClusterGroup.CandidateMask,
                scalarClusterGroup.PreparedMask,
                entries);
        }

        public ScalarClusterIssueGroup ToScalarClusterIssueGroup()
        {
            if (_entries.Length == 0)
                return new ScalarClusterIssueGroup(CandidateMask, PreparedMask, Array.Empty<ScalarClusterIssueEntry>());

            var entries = new ScalarClusterIssueEntry[_entries.Length];
            for (int i = 0; i < _entries.Length; i++)
                entries[i] = _entries[i].ToScalarClusterIssueEntry();

            return new ScalarClusterIssueGroup(CandidateMask, PreparedMask, entries);
        }

        private static ClusterIssueIntentEntry[] NormalizeEntries(
            IReadOnlyList<ClusterIssueIntentEntry>? entries,
            byte preparedMask)
        {
            if (entries is null || entries.Count == 0)
            {
                if (preparedMask != 0)
                {
                    throw new ArgumentException(
                        "Prepared mask requires matching prepared entries.",
                        nameof(entries));
                }

                return Array.Empty<ClusterIssueIntentEntry>();
            }

            var normalized = new ClusterIssueIntentEntry[entries.Count];
            byte entryMask = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                ClusterIssueIntentEntry entry = entries[i];
                ValidateSlotIndex(entry.SlotIndex);

                byte slotBit = (byte)(1 << entry.SlotIndex);
                if ((preparedMask & slotBit) == 0)
                {
                    throw new ArgumentException(
                        $"Prepared entries contain slot {entry.SlotIndex}, but it is not set in the prepared mask.",
                        nameof(entries));
                }

                if ((entryMask & slotBit) != 0)
                {
                    throw new ArgumentException(
                        $"Prepared entries contain duplicate slot {entry.SlotIndex}.",
                        nameof(entries));
                }

                normalized[i] = entry;
                entryMask |= slotBit;
            }

            if (entryMask != preparedMask)
            {
                throw new ArgumentException(
                    "Prepared entries must exactly match the prepared mask.",
                    nameof(entries));
            }

            return normalized;
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

    /// <summary>
    /// Temporary builder that moves scalar issue-group selection out of the decoder-owned
    /// descriptor while preserving the current compatibility ordering and VT-spread heuristic.
    /// </summary>
    internal static class ClusterIssueIntentBuilder
    {
        private const int ScalarIssueWidthCeiling = 4;

        public static ClusterIssueIntent Build(
            IReadOnlyList<DecodedBundleSlotDescriptor> slots,
            byte candidateMask)
        {
            ArgumentNullException.ThrowIfNull(slots);

            if (slots.Count < BundleMetadata.BundleSlotCount)
            {
                throw new ArgumentException(
                    $"Slot list must contain at least {BundleMetadata.BundleSlotCount} entries.",
                    nameof(slots));
            }

            var entries = new ClusterIssueIntentEntry[ScalarIssueWidthCeiling];
            byte preparedMask = 0;
            int count = 0;
            byte selectedVirtualThreadMask = 0;

            for (int slotIndex = 0; slotIndex < BundleMetadata.BundleSlotCount && count < entries.Length; slotIndex++)
            {
                byte slotBit = (byte)(1 << slotIndex);
                if ((candidateMask & slotBit) == 0)
                    continue;

                if (!TryGetVirtualThreadBit(
                        slots[slotIndex].VirtualThreadId,
                        out byte virtualThreadBit)
                    || (selectedVirtualThreadMask & virtualThreadBit) != 0)
                {
                    continue;
                }

                entries[count] = new ClusterIssueIntentEntry(slots[slotIndex]);
                preparedMask |= slotBit;
                selectedVirtualThreadMask |= virtualThreadBit;
                count++;
            }

            for (int slotIndex = 0; slotIndex < BundleMetadata.BundleSlotCount && count < entries.Length; slotIndex++)
            {
                byte slotBit = (byte)(1 << slotIndex);
                if ((candidateMask & slotBit) == 0 || (preparedMask & slotBit) != 0)
                    continue;

                entries[count] = new ClusterIssueIntentEntry(slots[slotIndex]);
                preparedMask |= slotBit;
                count++;
            }

            if (count == 0)
                return new ClusterIssueIntent(candidateMask, preparedMask);

            var preparedEntries = new ClusterIssueIntentEntry[count];
            Array.Copy(entries, preparedEntries, count);
            return new ClusterIssueIntent(candidateMask, preparedMask, preparedEntries);
        }

        private static bool TryGetVirtualThreadBit(int virtualThreadId, out byte virtualThreadBit)
        {
            switch (virtualThreadId)
            {
                case 0:
                    virtualThreadBit = 1 << 0;
                    return true;
                case 1:
                    virtualThreadBit = 1 << 1;
                    return true;
                case 2:
                    virtualThreadBit = 1 << 2;
                    return true;
                case 3:
                    virtualThreadBit = 1 << 3;
                    return true;
                default:
                    virtualThreadBit = 0;
                    return false;
            }
        }
    }
}
