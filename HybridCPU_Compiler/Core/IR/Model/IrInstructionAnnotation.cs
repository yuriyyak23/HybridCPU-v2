using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Compiler-facing metadata attached to an IR instruction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Core structural fields (<paramref name="ResourceClass"/> through <paramref name="DomainTag"/>)
    /// describe the instruction's execution profile, legality, and ISE contract binding.
    /// </para>
    /// <para>
    /// <paramref name="StealabilityHint"/> is an <b>advisory runtime hint</b> propagated
    /// from encoded metadata. It is consumed only by bounded scheduling heuristics and the
    /// <see cref="HybridCpuStealabilityAnalyzer"/> diagnostic stream. It does not affect
    /// structural admissibility, typed-slot facts, or lane binding.
    /// </para>
    /// </remarks>
    /// <param name="RequiredSlotClass">
    /// ISE-side <see cref="SlotClass"/> derived from <see cref="ResourceClass"/>
    /// via <see cref="IrSlotClassMapping.ToSlotClass"/>.
    /// </param>
    /// <param name="BindingKind">
    /// Compiler-side lane binding classification derived from resource class and serialization
    /// via <see cref="IrSlotClassMapping.DerivePinningKind"/>.
    /// </param>
    /// <param name="StealabilityHint">
    /// Advisory runtime hint. Indicates whether encoded metadata marked this instruction as
    /// stealable. Consumed by scheduling heuristics as a bounded tie-break signal only.
    /// Default: <c>false</c>.
    /// </param>
    public sealed record IrInstructionAnnotation(
        IrResourceClass ResourceClass,
        IrLatencyClass LatencyClass,
        byte MinimumLatencyCycles,
        IrIssueSlotMask LegalSlots,
        IrSerializationKind Serialization,
        IrStructuralResource StructuralResources,
        IrControlFlowKind ControlFlowKind,
        bool IsBarrierLike,
        bool MayTrap,
        ulong? EncodedBranchTarget,
        int? ResolvedBranchTargetInstructionIndex,
        IrMemoryRegion? MemoryReadRegion,
        IrMemoryRegion? MemoryWriteRegion,
        IReadOnlyList<IrOperand> Defs,
        IReadOnlyList<IrOperand> Uses,
        SlotClass RequiredSlotClass,
        IrSlotBindingKind BindingKind,
        ulong DomainTag,
        bool StealabilityHint = false);
}
