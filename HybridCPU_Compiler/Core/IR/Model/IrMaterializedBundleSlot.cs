using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// One physical slot in a materialized bundle.
    /// </summary>
    public sealed record IrMaterializedBundleSlot(
        int SlotIndex,
        IrInstruction? Instruction,
        int? OrderInCycle,
        IrIssueSlotMask InstructionLegalSlots,
        string? EmptyReason = null,
        IrSlotBindingKind? BindingKind = null,
        SlotClass? AssignedClass = null)
    {
        /// <summary>
        /// Gets a value indicating whether the slot was left empty and materialized as an internal NOP.
        /// </summary>
        public bool IsNop => Instruction is null;

        /// <summary>
        /// Gets the physical slot mask represented by this slot index.
        /// </summary>
        public IrIssueSlotMask PhysicalSlotMask => (IrIssueSlotMask)(1 << SlotIndex);

        /// <summary>
        /// Gets a value indicating whether the assigned instruction is legal for this slot.
        /// </summary>
        public bool IsLegalPlacement => Instruction is null || (InstructionLegalSlots & PhysicalSlotMask) != 0;
    }
}
