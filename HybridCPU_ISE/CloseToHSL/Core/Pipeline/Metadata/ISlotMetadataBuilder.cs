// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — Pipeline Metadata
// V5 Phase 2: Decoder Disentangling
// ─────────────────────────────────────────────────────────────────────────────

using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace YAKSys_Hybrid_CPU.Core.Pipeline.Metadata
{
    /// <summary>
    /// Builds a <see cref="SlotMetadata"/> record for a single instruction slot.
    /// <para>
    /// ARCHITECTURE RULE: This is a standalone pipeline service.
    /// The decoder must NOT call this builder.
    /// <see cref="CompilerAnnotation"/> is the sole external input; the
    /// <see cref="InstructionIR"/> is provided only for default fallback selection
    /// and must never contribute policy information.
    /// </para>
    /// </summary>
    public interface ISlotMetadataBuilder
    {
        /// <summary>
        /// Builds a <see cref="SlotMetadata"/> from an instruction IR and an optional
        /// compiler annotation.
        /// <para>
        /// When <paramref name="annotation"/> is <see langword="null"/>, the returned
        /// metadata uses all defaults (equivalent to a freshly constructed
        /// <see cref="SlotMetadata"/> with no overrides).
        /// </para>
        /// </summary>
        /// <param name="instruction">The decoded instruction IR (read-only, structural only).</param>
        /// <param name="annotation">
        /// Compiler-emitted hint annotation.  May be <see langword="null"/> when no
        /// annotation is present (e.g. JIT-compiled or dynamically generated code).
        /// </param>
        /// <returns>A fully-initialised <see cref="SlotMetadata"/> record.</returns>
        SlotMetadata Build(InstructionIR instruction, CompilerAnnotation? annotation);
    }
}
