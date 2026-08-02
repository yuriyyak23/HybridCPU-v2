// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — Pipeline Metadata
// V5 Phase 2: Decoder Disentangling
// ─────────────────────────────────────────────────────────────────────────────

using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Core.Pipeline.Metadata
{
    /// <summary>
    /// Compiler-generated annotation for a single instruction slot in a VLIW bundle.
    /// <para>
    /// Carries scheduling hints emitted by the compiler front-end.
    /// This record is the sole source of hint information — it must never be
    /// reverse-engineered from the instruction opcode at decode time.
    /// </para>
    /// <para>
    /// ARCHITECTURE RULE: All fields default to "no hint / no policy" so that
    /// a missing or null annotation is always safe (results in default <see cref="SlotMetadata"/>).
    /// </para>
    /// </summary>
    public sealed record CompilerAnnotation
    {
        /// <summary>
        /// Branch prediction hint.
        /// Default: <see cref="BranchHint.None"/> — predictor uses history tables.
        /// </summary>
        public BranchHint BranchHint { get; init; } = BranchHint.None;

        /// <summary>
        /// FSP stealability policy.
        /// Default: <see cref="StealabilityPolicy.Stealable"/> — FSP may pilfer this slot.
        /// </summary>
        public StealabilityPolicy StealabilityPolicy { get; init; } = StealabilityPolicy.Stealable;

        /// <summary>
        /// Donor VT hint for FSP injection.
        /// <c>0xFF</c> = no preference.
        /// </summary>
        public byte DonorVtHint { get; init; } = 0xFF;

        /// <summary>
        /// Memory locality hint.
        /// Default: <see cref="LocalityHint.None"/>.
        /// </summary>
        public LocalityHint LocalityHint { get; init; } = LocalityHint.None;

        /// <summary>
        /// Preferred virtual thread for scheduling.
        /// <c>0xFF</c> = no preference.
        /// </summary>
        public byte PreferredVt { get; init; } = 0xFF;

        /// <summary>
        /// Thermal pressure hint.
        /// Default: <see cref="ThermalHint.None"/>.
        /// </summary>
        public ThermalHint ThermalHint { get; init; } = ThermalHint.None;
    }
}
