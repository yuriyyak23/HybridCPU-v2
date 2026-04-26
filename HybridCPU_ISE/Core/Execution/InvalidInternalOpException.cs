using System;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace YAKSys_Hybrid_CPU.Core.Execution
{
    // ─────────────────────────────────────────────────────────────────────────
    // InvalidInternalOpException — V5 Phase 3
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Thrown by <see cref="ExecutionEngine"/> when an <see cref="InternalOp"/>
    /// with an <see cref="InternalOpKind"/> that has no registered
    /// <see cref="IExecutionUnit"/> reaches the engine.
    ///
    /// <para>
    /// This exception indicates a pipeline configuration error — either a new
    /// <see cref="InternalOpKind"/> was added to the taxonomy without a
    /// corresponding unit, or the engine was constructed with an incomplete
    /// unit registry.
    /// </para>
    /// </summary>
    public sealed class InvalidInternalOpException : Exception
    {
        /// <summary>The unhandled operation kind.</summary>
        public InternalOpKind Kind { get; }
        public ExecutionFaultCategory Category => ExecutionFaultCategory.InvalidInternalOp;

        /// <summary>
        /// Initializes the exception for the given unregistered
        /// <paramref name="kind"/>.
        /// </summary>
        public InvalidInternalOpException(InternalOpKind kind)
            : this(kind, innerException: null)
        {
        }

        public InvalidInternalOpException(
            InternalOpKind kind,
            Exception? innerException)
            : base(
                ExecutionFaultContract.FormatMessage(
                    ExecutionFaultCategory.InvalidInternalOp,
                    $"No execution unit registered for InternalOpKind.{kind}. " +
                    $"Ensure every InternalOpKind value has a corresponding IExecutionUnit " +
                    $"in the ExecutionEngine registry."),
                innerException)
        {
            Kind = kind;
            ExecutionFaultContract.Stamp(this, Category);
        }
    }
}
