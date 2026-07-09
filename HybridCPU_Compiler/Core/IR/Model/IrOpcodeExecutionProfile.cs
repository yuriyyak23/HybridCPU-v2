using System;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Centralized compiler-facing execution metadata derived from ISA opcode information.
    /// </summary>
    public sealed record IrOpcodeExecutionProfile(
        Processor.CPU_Core.InstructionsEnum Opcode,
        IrResourceClass ResourceClass,
        IrLatencyClass LatencyClass,
        byte MinimumLatencyCycles,
        [property: Obsolete("Compiler-side LegalSlots are structurally allowed slots only; use StructurallyAllowedSlots.", false)]
        IrIssueSlotMask LegalSlots,
        IrSerializationKind Serialization,
        IrStructuralResource StructuralResources,
        SlotClass DerivedSlotClass,
        IrSlotBindingKind DerivedBindingKind)
    {
        /// <summary>
        /// Gets a value indicating whether the opcode must execute alone in a cycle group.
        /// </summary>
        public bool RequiresExclusiveCycle => (Serialization & IrSerializationKind.ExclusiveCycle) != 0;

        public IrIssueSlotMask StructurallyAllowedSlots => LegalSlots;
    }
}
