// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — Legality Layer
// V5 Phase 2: Decoder Disentangling
// ─────────────────────────────────────────────────────────────────────────────

using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace YAKSys_Hybrid_CPU.Core.Legality
{
    /// <summary>
    /// Result of a legality check for a single <see cref="InstructionIR"/>.
    /// </summary>
    public enum LegalityResult : byte
    {
        /// <summary>Instruction is legal and can proceed to issue.</summary>
        Legal = 0,

        /// <summary>
        /// Instruction cannot issue this cycle due to a data or structural hazard.
        /// The instruction should be re-presented in a future cycle.
        /// </summary>
        Stall = 1,

        /// <summary>
        /// Instruction caused a pipeline flush (e.g. branch misprediction).
        /// The instruction window must be discarded and the PC corrected.
        /// </summary>
        Flush = 2,

        /// <summary>
        /// Instruction requires a higher privilege level than the current mode provides.
        /// A privilege-mode exception (illegal-instruction trap) must be raised.
        /// </summary>
        PrivilegeFault = 3,
    }

    /// <summary>
    /// Standalone legality checker — a pipeline stage service, not part of the decoder.
    /// <para>
    /// ARCHITECTURE RULE: This interface is consumed by the issue / scheduling stage only.
    /// The decoder must NEVER call this checker. All legality decisions are deferred to
    /// the issue stage so that the decoder output (<see cref="InstructionIR"/>) remains
    /// a pure structural representation with no policy information.
    /// </para>
    /// </summary>
    public interface ILegalityChecker
    {
        /// <summary>
        /// Evaluates whether <paramref name="instruction"/> may issue given the current
        /// hazard state, resource availability, and privilege level.
        /// </summary>
        /// <param name="instruction">Instruction IR to evaluate.</param>
        /// <param name="hazards">Current pipeline hazard state.</param>
        /// <param name="resources">Current structural resource availability.</param>
        /// <param name="currentPrivilege">Privilege level of the executing thread.</param>
        /// <returns>
        /// <see cref="LegalityResult.Legal"/> when the instruction may issue;
        /// <see cref="LegalityResult.Stall"/> when a hazard blocks this cycle;
        /// <see cref="LegalityResult.PrivilegeFault"/> when the instruction requires
        /// a higher privilege level than <paramref name="currentPrivilege"/>.
        /// </returns>
        LegalityResult Check(
            InstructionIR instruction,
            IHazardState hazards,
            IResourceState resources,
            PrivilegeLevel currentPrivilege);
    }
}
