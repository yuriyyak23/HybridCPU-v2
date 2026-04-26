using System;
using System.Collections.Generic;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Builds a legality-correct local baseline that preserves original block instruction order.
    /// </summary>
    public sealed class HybridCpuProgramOrderLocalScheduler
    {
        private readonly HybridCpuProgramDependencyAnalyzer _programDependencyAnalyzer = new();
        private readonly HybridCpuBasicBlockSchedulingDagBuilder _dagBuilder = new();
        private readonly HybridCpuInstructionLegalityChecker _legalityChecker = new();

        /// <summary>
        /// Schedules all basic blocks in one IR program without reordering instructions inside each block.
        /// </summary>
        public IrProgramSchedule ScheduleProgram(IrProgram program)
        {
            ArgumentNullException.ThrowIfNull(program);
            return ScheduleProgram(program, _programDependencyAnalyzer.AnalyzeProgram(program));
        }

        /// <summary>
        /// Schedules all basic blocks in one IR program without reordering instructions inside each block.
        /// </summary>
        public IrProgramSchedule ScheduleProgram(IrProgram program, IrProgramDependencyGraph dependencyGraph)
        {
            ArgumentNullException.ThrowIfNull(program);
            ArgumentNullException.ThrowIfNull(dependencyGraph);

            var blockSchedules = new List<IrBasicBlockSchedule>(program.BasicBlocks.Count);
            foreach (IrBasicBlock block in program.BasicBlocks)
            {
                blockSchedules.Add(ScheduleBlock(block, dependencyGraph));
            }

            return new IrProgramSchedule(program, dependencyGraph, blockSchedules);
        }

        /// <summary>
        /// Schedules one basic block in original instruction order using the same legality and latency rules as the local scheduler.
        /// </summary>
        public IrBasicBlockSchedule ScheduleBlock(IrBasicBlock block, IrProgramDependencyGraph dependencyGraph)
        {
            ArgumentNullException.ThrowIfNull(block);
            ArgumentNullException.ThrowIfNull(dependencyGraph);

            IrBasicBlockSchedulingDag dag = _dagBuilder.BuildBlockDag(block, dependencyGraph);
            var nodesByInstructionIndex = BuildNodeMap(dag);
            var cycleBuilders = new List<ProgramOrderCycleBuilder>();
            var scheduledInstructions = new List<IrScheduledInstruction>(block.Instructions.Count);
            var scheduledCyclesByInstructionIndex = new Dictionary<int, int>(block.Instructions.Count);
            int lastScheduledCycle = 0;
            bool hasScheduledInstructions = false;

            foreach (IrInstruction instruction in block.Instructions)
            {
                if (!nodesByInstructionIndex.TryGetValue(instruction.Index, out IrSchedulingNode? node) || node is null)
                {
                    throw new InvalidOperationException($"Scheduling DAG is missing instruction {instruction.Index} for block {block.Id}.");
                }

                int readyCycle = 0;
                foreach (IrInstructionDependency dependency in node.IncomingDependencies)
                {
                    if (!scheduledCyclesByInstructionIndex.TryGetValue(dependency.ProducerInstructionIndex, out int producerCycle))
                    {
                        throw new InvalidOperationException($"Program-order baseline encountered unscheduled producer {dependency.ProducerInstructionIndex} before instruction {instruction.Index} in block {block.Id}.");
                    }

                    readyCycle = Math.Max(readyCycle, producerCycle + dependency.MinimumLatencyCycles);
                }

                int targetCycle = hasScheduledInstructions
                    ? Math.Max(readyCycle, lastScheduledCycle)
                    : readyCycle;

                if (!TryAddToCurrentCycle(cycleBuilders, targetCycle, instruction))
                {
                    targetCycle = hasScheduledInstructions
                        ? Math.Max(targetCycle, lastScheduledCycle + 1)
                        : targetCycle;

                    cycleBuilders.Add(new ProgramOrderCycleBuilder(targetCycle));
                    cycleBuilders[^1].Instructions.Add(instruction);
                }

                scheduledCyclesByInstructionIndex[instruction.Index] = targetCycle;
                scheduledInstructions.Add(new IrScheduledInstruction(
                    InstructionIndex: instruction.Index,
                    Instruction: instruction,
                    Cycle: targetCycle,
                    OrderInCycle: cycleBuilders[^1].Instructions.Count - 1,
                    ReadyCycle: readyCycle,
                    CriticalPathLengthCycles: node.CriticalPathLengthCycles));
                lastScheduledCycle = targetCycle;
                hasScheduledInstructions = true;
            }

            var cycleGroups = new List<IrScheduleCycleGroup>(cycleBuilders.Count);
            foreach (ProgramOrderCycleBuilder cycleBuilder in cycleBuilders)
            {
                IrCandidateBundleAnalysis legalityAnalysis = _legalityChecker.AnalyzeCandidateBundle(cycleBuilder.Instructions);
                cycleGroups.Add(new IrScheduleCycleGroup(cycleBuilder.Cycle, cycleBuilder.Instructions, legalityAnalysis));
            }

            return new IrBasicBlockSchedule(block, dag, cycleGroups, scheduledInstructions);
        }

        private static Dictionary<int, IrSchedulingNode> BuildNodeMap(IrBasicBlockSchedulingDag dag)
        {
            var nodesByInstructionIndex = new Dictionary<int, IrSchedulingNode>(dag.Nodes.Count);
            foreach (IrSchedulingNode node in dag.Nodes)
            {
                nodesByInstructionIndex[node.InstructionIndex] = node;
            }

            return nodesByInstructionIndex;
        }

        private bool TryAddToCurrentCycle(
            IReadOnlyList<ProgramOrderCycleBuilder> cycleBuilders,
            int targetCycle,
            IrInstruction instruction)
        {
            if (cycleBuilders.Count == 0 || cycleBuilders[^1].Cycle != targetCycle)
            {
                return false;
            }

            var candidateInstructions = new List<IrInstruction>(cycleBuilders[^1].Instructions.Count + 1);
            for (int index = 0; index < cycleBuilders[^1].Instructions.Count; index++)
            {
                candidateInstructions.Add(cycleBuilders[^1].Instructions[index]);
            }

            candidateInstructions.Add(instruction);
            return _legalityChecker.AnalyzeCandidateBundle(candidateInstructions).IsLegal;
        }

        private sealed class ProgramOrderCycleBuilder
        {
            public ProgramOrderCycleBuilder(int cycle)
            {
                Cycle = cycle;
                Instructions = new List<IrInstruction>();
            }

            public int Cycle { get; }

            public List<IrInstruction> Instructions { get; }
        }
    }
}
