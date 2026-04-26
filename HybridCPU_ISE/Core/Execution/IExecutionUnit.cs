using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace YAKSys_Hybrid_CPU.Core.Execution
{
    // ─────────────────────────────────────────────────────────────────────────
    // IExecutionUnit — V5 Phase 3
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Single typed execution unit contract for the ISA-agnostic
    /// <see cref="ExecutionEngine"/>.
    ///
    /// <para>
    /// An execution unit receives a fully resolved <see cref="InternalOp"/>
    /// and the current VT-scoped CPU state, and returns an
    /// <see cref="ExecutionResult"/>. It does NOT know about
    /// <c>IsaV4Opcode</c>, <c>InstructionsEnum</c>, or any other ISA-level
    /// concept. All ISA-specific translation is done upstream by
    /// <c>IInternalOpBuilder</c> (Phase 2).
    /// </para>
    ///
    /// <para>
    /// Registered in the <see cref="ExecutionEngine"/> keyed by
    /// <see cref="InternalOpKind"/>. One unit per kind.
    /// </para>
    /// </summary>
    public interface IExecutionUnit
    {
        /// <summary>
        /// Execute the given <paramref name="op"/> against <paramref name="state"/>
        /// and return the result.
        /// </summary>
        /// <param name="op">The resolved micro-operation to execute.</param>
        /// <param name="state">VT-scoped CPU state (read-only for execution units).</param>
        /// <returns>Execution outcome — value, PC redirect, trap, or pipeline event.</returns>
        ExecutionResult Execute(InternalOp op, ICanonicalCpuState state);
    }
}

