// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — Pipeline Metadata
// V5 Phase 2: Decoder Disentangling
// ─────────────────────────────────────────────────────────────────────────────

using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace YAKSys_Hybrid_CPU.Core.Pipeline.Metadata
{
    /// <summary>
    /// Default <see cref="ISlotMetadataBuilder"/> implementation.
    ///
    /// Mapping rules:
    /// <list type="bullet">
    ///   <item>When <c>annotation</c> is <see langword="null"/> — all <see cref="SlotMetadata"/> fields
    ///         take their default values.</item>
    ///   <item>Otherwise — every non-default field in <c>annotation</c> is forwarded
    ///         verbatim into the corresponding <see cref="SlotMetadata"/> field.</item>
    /// </list>
    ///
    /// The <paramref name="instruction"/> parameter is accepted by the interface contract for
    /// future structural defaults but is not currently consulted for any policy decision.
    /// </summary>
    public sealed class SlotMetadataBuilder : ISlotMetadataBuilder
    {
        /// <inheritdoc />
        public SlotMetadata Build(InstructionIR instruction, CompilerAnnotation? annotation)
        {
            if (annotation is null)
                return new SlotMetadata();

            return new SlotMetadata
            {
                BranchHint        = annotation.BranchHint,
                StealabilityPolicy = annotation.StealabilityPolicy,
                DonorVtHint       = annotation.DonorVtHint,
                LocalityHint      = annotation.LocalityHint,
                PreferredVt       = annotation.PreferredVt,
                ThermalHint       = annotation.ThermalHint,
            };
        }
    }
}
