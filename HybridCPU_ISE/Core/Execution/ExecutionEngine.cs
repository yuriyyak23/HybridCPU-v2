using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace YAKSys_Hybrid_CPU.Core.Execution
{
    // ─────────────────────────────────────────────────────────────────────────
    // ExecutionEngine — V5 Phase 3
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// ISA-agnostic execution engine.
    ///
    /// <para>
    /// Dispatches <see cref="InternalOp"/> instances to typed
    /// <see cref="IExecutionUnit"/> implementations keyed by
    /// <see cref="InternalOpKind"/>. The engine has no knowledge of
    /// <c>IsaV4Opcode</c>, <c>InstructionsEnum</c>, or any other ISA-specific
    /// concept — all ISA translation happens upstream in
    /// <c>IInternalOpBuilder</c> (Phase 2).
    /// </para>
    ///
    /// <para>
    /// <b>Construction:</b> pass a fully populated registry. If any
    /// <see cref="InternalOpKind"/> value is submitted that lacks a registered
    /// unit the engine throws <see cref="InvalidInternalOpException"/>, which
    /// surfaces pipeline configuration errors at test time.
    /// </para>
    ///
    /// <para>
    /// <b>Thread safety:</b> the registry dictionary is read-only after
    /// construction. The engine itself is thread-safe provided each VT's
    /// <see cref="ICanonicalCpuState"/> is not shared across threads.
    /// </para>
    /// </summary>
    public sealed class ExecutionEngine
    {
        private readonly IReadOnlyDictionary<InternalOpKind, IExecutionUnit> _units;

        /// <summary>
        /// Constructs the engine with the given unit registry.
        /// </summary>
        /// <param name="units">
        /// Mapping from <see cref="InternalOpKind"/> to the execution unit
        /// responsible for that kind. Must not be <see langword="null"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="units"/> is <see langword="null"/>.
        /// </exception>
        public ExecutionEngine(IReadOnlyDictionary<InternalOpKind, IExecutionUnit> units)
            => _units = units ?? throw new ArgumentNullException(nameof(units));

        /// <summary>
        /// Executes <paramref name="op"/> by dispatching to the registered
        /// <see cref="IExecutionUnit"/> for <c>op.Kind</c>.
        /// </summary>
        /// <param name="op">The resolved micro-operation to execute.</param>
        /// <param name="state">VT-scoped CPU state.</param>
        /// <returns>The execution outcome.</returns>
        /// <exception cref="InvalidInternalOpException">
        /// Thrown when <c>op.Kind</c> has no registered execution unit.
        /// </exception>
        public ExecutionResult Execute(InternalOp op, ICanonicalCpuState state)
        {
            if (!_units.TryGetValue(op.Kind, out var unit))
                throw new InvalidInternalOpException(op.Kind);

            return unit.Execute(op, state);
        }
    }
}

