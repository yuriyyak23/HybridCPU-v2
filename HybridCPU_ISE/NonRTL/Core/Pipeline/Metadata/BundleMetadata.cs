using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Per-bundle scheduling and policy metadata for a VLIW bundle.
    /// <para>
    /// Carries bundle-level policy that cannot be expressed per-slot,
    /// plus a per-slot metadata array. NOT an architectural observable — invisible
    /// to program semantics. Consumed only by the scheduler, FSP, diagnostics,
    /// and replay subsystem.
    /// </para>
    /// <para>Schema version: 4.0</para>
    /// </summary>
    public sealed record BundleMetadata
    {
        /// <summary>Number of slots in a standard VLIW bundle.</summary>
        public const int BundleSlotCount = 8;

        /// <summary>Schema version for forward compatibility. Current ISE version: 4.</summary>
        public byte SchemaVersion { get; init; } = MetadataSchemaVersion.Current;

        // ─── Per-slot metadata ────────────────────────────────────────────────

        /// <summary>
        /// Per-slot metadata. Length must equal bundle slot count.
        /// If null or shorter than slot count, missing slots use <see cref="SlotMetadata.Default"/>.
        /// </summary>
        public IReadOnlyList<SlotMetadata>? SlotMetadata { get; init; }

        // ─── FSP boundary ─────────────────────────────────────────────────────

        /// <summary>
        /// FSP boundary flag. Replaces the <c>FSP_FENCE</c> ISA opcode.
        /// If <c>true</c>, FSP may not cross this bundle boundary — no slot pilfering
        /// from or into adjacent bundles is allowed.
        /// Default: <c>false</c>.
        /// </summary>
        public bool FspBoundary { get; init; } = false;

        // ─── Bundle-level thermal policy ──────────────────────────────────────

        /// <summary>
        /// Bundle-level thermal hint, consumed by the power management unit.
        /// If set, provides guidance for the entire bundle.
        /// Default: <see cref="ThermalHint.None"/>.
        /// </summary>
        public ThermalHint BundleThermalHint { get; init; } = ThermalHint.None;

        // ─── Replay anchor ────────────────────────────────────────────────────

        /// <summary>
        /// If <c>true</c>, this bundle is a replay anchor point.
        /// The ISE should capture a full state snapshot at this bundle boundary.
        /// Used by Phase 11 (Diagnostics) to optimize replay snapshot placement.
        /// Default: <c>false</c>.
        /// </summary>
        public bool IsReplayAnchor { get; init; } = false;

        // ─── Diagnostics ──────────────────────────────────────────────────────

        /// <summary>
        /// Optional compiler diagnostics tag for this bundle.
        /// Used in telemetry reports only — not consumed at runtime.
        /// </summary>
        public string? DiagnosticsTag { get; init; }

        // ─── Default instance ─────────────────────────────────────────────────

        /// <summary>
        /// Default <see cref="BundleMetadata"/> with all fields at neutral/default values.
        /// Semantically equivalent to a bundle emitted without explicit metadata.
        /// </summary>
        public static readonly BundleMetadata Default = new()
        {
            SlotMetadata = null,
        };

        // ─── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Return the <see cref="SlotMetadata"/> for the given slot index.
        /// Falls back to <see cref="SlotMetadata.Default"/> for out-of-range indices
        /// or when no per-slot metadata is present.
        /// </summary>
        public SlotMetadata GetSlotMetadata(int slotIndex)
        {
            if (SlotMetadata is null || (uint)slotIndex >= (uint)SlotMetadata.Count)
                return Core.SlotMetadata.Default;
            return SlotMetadata[slotIndex];
        }

        /// <summary>
        /// Return the producer-side admission snapshot for the given slot index.
        /// Falls back to <see cref="MicroOpAdmissionMetadata.Default"/> when no explicit
        /// slot metadata is available.
        /// </summary>
        public MicroOpAdmissionMetadata GetAdmissionMetadata(int slotIndex)
        {
            return GetSlotMetadata(slotIndex).AdmissionMetadata;
        }

        /// <summary>
        /// Create a <see cref="BundleMetadata"/> with all slots set to their defaults
        /// (stealable, no hints, no VT preference).
        /// </summary>
        public static BundleMetadata CreateAllStealable()
        {
            var slots = new SlotMetadata[BundleSlotCount];
            for (int i = 0; i < BundleSlotCount; i++)
                slots[i] = Core.SlotMetadata.Default;
            return new BundleMetadata { SlotMetadata = slots };
        }

        /// <summary>
        /// Create a <see cref="BundleMetadata"/> with an FSP boundary and all slots
        /// set to <see cref="StealabilityPolicy.NotStealable"/>.
        /// Used to represent a bundle that acts as an FSP fence.
        /// </summary>
        public static BundleMetadata CreateFspFence()
        {
            var notStealable = new SlotMetadata { StealabilityPolicy = StealabilityPolicy.NotStealable };
            var slots = new SlotMetadata[BundleSlotCount];
            for (int i = 0; i < BundleSlotCount; i++)
                slots[i] = notStealable;
            return new BundleMetadata
            {
                FspBoundary = true,
                SlotMetadata = slots,
            };
        }
    }
}
