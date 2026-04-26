namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Per-slot scheduling and policy metadata for HybridCPU v4.
    /// <para>
    /// Carries ALL hint and policy information evicted from the ISA opcode space.
    /// NOT an architectural observable — invisible to program semantics.
    /// Consumed only by: scheduler, FSP, branch predictor hint injection,
    /// cache prefetch hint injection, and diagnostics.
    /// </para>
    /// <para>Schema version: 4.0</para>
    /// </summary>
    public sealed record SlotMetadata
    {
        /// <summary>Schema version for forward compatibility. Current ISE version: 4.</summary>
        public byte SchemaVersion { get; init; } = MetadataSchemaVersion.Current;

        // ─── Branch Prediction Hints ─────────────────────────────────────────

        /// <summary>
        /// Branch prediction hint for control flow instructions.
        /// Not ISA-visible. Used by branch predictor to bias initial prediction.
        /// Default: <see cref="BranchHint.None"/> — predictor uses history tables.
        /// </summary>
        public BranchHint BranchHint { get; init; } = BranchHint.None;

        // ─── FSP / Stealability Policy ───────────────────────────────────────

        /// <summary>
        /// FSP stealability policy for this slot.
        /// Default: <see cref="StealabilityPolicy.Stealable"/> — FSP may pilfer this slot
        /// when it is free.
        /// Replaces the deprecated <c>MicroOp.CanBeStolen</c> field.
        /// </summary>
        public StealabilityPolicy StealabilityPolicy { get; init; } = StealabilityPolicy.Stealable;

        /// <summary>
        /// Donor VT hint for FSP injection.
        /// When this slot is free, FSP prefers to take an instruction from this VT.
        /// <c>0xFF</c> means no donor preference — FSP selects the lowest available VT.
        /// Default: <c>0xFF</c>.
        /// </summary>
        public byte DonorVtHint { get; init; } = 0xFF;

        // ─── Memory Locality Hints ───────────────────────────────────────────

        /// <summary>
        /// Memory locality hint for load/store instructions.
        /// Default: <see cref="LocalityHint.None"/> — use the normal cache hierarchy.
        /// </summary>
        public LocalityHint LocalityHint { get; init; } = LocalityHint.None;

        // ─── VT Scheduling Hints ─────────────────────────────────────────────

        /// <summary>
        /// Preferred virtual thread for this instruction.
        /// <c>0xFF</c> means no preference — scheduler decides.
        /// This does not guarantee placement; it is a scheduler hint only.
        /// Default: <c>0xFF</c>.
        /// </summary>
        public byte PreferredVt { get; init; } = 0xFF;

        // ─── Thermal Hints ────────────────────────────────────────────────────

        /// <summary>
        /// Per-slot thermal hint: whether this instruction represents a hot or cold code path.
        /// Used for cache and execution unit power management hints.
        /// Default: <see cref="ThermalHint.None"/>.
        /// </summary>
        public ThermalHint ThermalHint { get; init; } = ThermalHint.None;

        /// <summary>
        /// Producer-side admission snapshot for this slot when the frontend or decoder
        /// chooses to materialize one explicitly.
        /// Default remains structural-neutral so existing metadata-only callers do not need
        /// to populate admission facts eagerly.
        /// </summary>
        public MicroOpAdmissionMetadata AdmissionMetadata { get; init; } = MicroOpAdmissionMetadata.Default;

        /// <summary>
        /// Singleton default instance — stealable, no hints, no VT preference.
        /// Use this to express "no metadata override" without allocating.
        /// </summary>
        public static readonly SlotMetadata Default = new();

        /// <summary>
        /// Singleton not-stealable instance — FSP must not pilfer this slot.
        /// Use for privileged operations, control flow, atomics, and MMIO access.
        /// </summary>
        public static readonly SlotMetadata NotStealable = new() { StealabilityPolicy = StealabilityPolicy.NotStealable };
    }
}
