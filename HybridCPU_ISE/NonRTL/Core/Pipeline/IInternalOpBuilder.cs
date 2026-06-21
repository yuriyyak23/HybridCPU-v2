// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — Pipeline
// V5 Phase 2: Decoder Disentangling
// ─────────────────────────────────────────────────────────────────────────────

using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace YAKSys_Hybrid_CPU.Core.Pipeline
{
    /// <summary>
    /// Translates a decoded <see cref="InstructionIR"/> into an execution-unit-ready
    /// <see cref="InternalOp"/>.
    /// <para>
    /// ARCHITECTURE RULE: This is a standalone pipeline service, not part of the decoder.
    /// The decoder emits <see cref="InstructionIR"/>; the issue stage calls this builder
    /// to produce the <see cref="InternalOp"/> that is handed off to an execution unit.
    /// The mapping is a pure function of the <see cref="InstructionIR"/> — no pipeline
    /// state, hazard state, or policy information enters here.
    /// </para>
    /// </summary>
    public interface IInternalOpBuilder
    {
        /// <summary>
        /// Builds an <see cref="InternalOp"/> from the given instruction IR.
        /// The mapping is deterministic and stateless: the same IR always produces
        /// the same <see cref="InternalOp"/>.
        /// </summary>
        /// <param name="instruction">Decoded instruction IR (must not be <see langword="null"/>).</param>
        /// <returns>The corresponding <see cref="InternalOp"/> ready for execution.</returns>
        InternalOp Build(InstructionIR instruction);
    }
}
