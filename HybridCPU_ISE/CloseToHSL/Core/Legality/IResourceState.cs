// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — Legality Layer
// V5 Phase 2: Decoder Disentangling
// ─────────────────────────────────────────────────────────────────────────────

using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Core.Legality
{
    /// <summary>
    /// Read-only view of structural resource availability used during legality checking.
    /// <para>
    /// Consumed exclusively by <see cref="ILegalityChecker"/>.
    /// NOT accessible from the decoder or the instruction IR builder.
    /// </para>
    /// </summary>
    public interface IResourceState
    {
        /// <summary>
        /// Returns <see langword="true"/> when at least one execution unit of the given
        /// <paramref name="instructionClass"/> is available to accept work this cycle.
        /// </summary>
        bool IsAvailable(InstructionClass instructionClass);

        /// <summary>
        /// Returns the number of idle execution units of the given <paramref name="instructionClass"/>.
        /// Returns 0 when the class is fully occupied.
        /// </summary>
        int AvailableCount(InstructionClass instructionClass);
    }
}
