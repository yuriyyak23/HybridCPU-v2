using System.Collections.Generic;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Top-level IR container for one virtual-thread instruction stream.
    /// </summary>
    public sealed record IrProgram(
        byte VirtualThreadId,
        IReadOnlyList<IrInstruction> Instructions,
        ControlFlowGraph ControlFlowGraph,
        IReadOnlyList<IrProgramLabel> Labels,
        IReadOnlyList<IrEntryPointMetadata> EntryPoints,
        IReadOnlyList<IrSection> Sections,
        IReadOnlyList<IrFunction> Functions,
        IrProgramSymbols Symbols)
    {
        /// <summary>
        /// Basic blocks projected from the program control-flow graph.
        /// </summary>
        public IReadOnlyList<IrBasicBlock> BasicBlocks => ControlFlowGraph.Blocks;
    }
}
