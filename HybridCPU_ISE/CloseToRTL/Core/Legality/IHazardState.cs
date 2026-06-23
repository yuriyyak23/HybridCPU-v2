// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — Legality Layer
// V5 Phase 2: Decoder Disentangling
// ─────────────────────────────────────────────────────────────────────────────

namespace YAKSys_Hybrid_CPU.Core.Legality
{
    /// <summary>
    /// Read-only view of the current pipeline hazard state used during legality checking.
    /// <para>
    /// Consumed exclusively by <see cref="ILegalityChecker"/>.
    /// NOT accessible from the decoder or the instruction IR builder.
    /// </para>
    /// </summary>
    public interface IHazardState
    {
        /// <summary>
        /// Returns <see langword="true"/> when there is an unresolved RAW (read-after-write)
        /// dependency on <paramref name="sourceReg"/>: an in-flight instruction is scheduled
        /// to write to <paramref name="sourceReg"/> but its result is not yet available.
        /// The current instruction must not read <paramref name="sourceReg"/> until the hazard clears.
        /// </summary>
        bool HasRawHazard(int sourceReg);

        /// <summary>
        /// Returns <see langword="true"/> when a WAW (write-after-write) hazard exists between
        /// the two destination registers.
        /// </summary>
        bool HasWawHazard(int destReg1, int destReg2);

        /// <summary>
        /// Returns <see langword="true"/> when a WAR (write-after-read) hazard exists,
        /// i.e. a pending read of <paramref name="sourceReg"/> and an incoming write
        /// to <paramref name="destReg"/> targeting the same architectural register.
        /// </summary>
        bool HasWarHazard(int sourceReg, int destReg);
    }
}
