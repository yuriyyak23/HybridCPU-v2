using System;
using System.Collections.Generic;
using System.Linq;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Aggregates pairwise instruction dependences for a basic block without introducing scheduling.
    /// </summary>
    public sealed class HybridCpuBasicBlockDependencyAnalyzer
    {
        private readonly HybridCpuDependencyAnalyzer _dependencyAnalyzer = new();

        /// <summary>
        /// Builds a dependence graph for a single basic block.
        /// </summary>
        public IrBasicBlockDependencyGraph AnalyzeBlock(IrBasicBlock block)
        {
            ArgumentNullException.ThrowIfNull(block);

            var dependencies = new HashSet<IrInstructionDependency>();
            AddPreciseRegisterDependencies(block, dependencies);
            AddLoadAdjacentScalarFollowThroughDependencies(block, dependencies);

            for (int producerIndex = 0; producerIndex < block.Instructions.Count; producerIndex++)
            {
                for (int consumerIndex = producerIndex + 1; consumerIndex < block.Instructions.Count; consumerIndex++)
                {
                    foreach (IrInstructionDependency dependency in _dependencyAnalyzer.AnalyzePair(block.Instructions[producerIndex], block.Instructions[consumerIndex]))
                    {
                        if (dependency.Kind is IrInstructionDependencyKind.Memory or
                            IrInstructionDependencyKind.Control or
                            IrInstructionDependencyKind.Serialization)
                        {
                            dependencies.Add(dependency);
                        }
                    }
                }
            }

            IrInstructionDependency[] orderedDependencies = dependencies
                .OrderBy(d => d.ProducerInstructionIndex)
                .ThenBy(d => d.ConsumerInstructionIndex)
                .ThenBy(d => d.Kind)
                .ThenBy(d => d.RelatedOperandKind)
                .ThenBy(d => d.RelatedOperandValue)
                .ToArray();

            return new IrBasicBlockDependencyGraph(block.Id, block.Instructions, orderedDependencies);
        }

        /// <summary>
        /// Builds dependence graphs for all blocks in a program.
        /// </summary>
        public IReadOnlyList<IrBasicBlockDependencyGraph> AnalyzeProgram(IrProgram program)
        {
            ArgumentNullException.ThrowIfNull(program);

            var blockGraphs = new List<IrBasicBlockDependencyGraph>(program.BasicBlocks.Count);
            foreach (IrBasicBlock block in program.BasicBlocks)
            {
                blockGraphs.Add(AnalyzeBlock(block));
            }

            return blockGraphs;
        }

        /// <summary>
        /// Builds a program-level dependence graph that combines intra-block and inter-block results.
        /// </summary>
        public IrProgramDependencyGraph AnalyzeProgramGraph(IrProgram program)
        {
            ArgumentNullException.ThrowIfNull(program);

            var programAnalyzer = new HybridCpuProgramDependencyAnalyzer();
            return programAnalyzer.AnalyzeProgram(program);
        }

        private static void AddPreciseRegisterDependencies(
            IrBasicBlock block,
            ICollection<IrInstructionDependency> dependencies)
        {
            var lastDefinitions = new Dictionary<HybridCpuRegisterDependencyKey, IrInstruction>();
            var liveUsesSinceLastDefinition = new Dictionary<HybridCpuRegisterDependencyKey, Dictionary<int, IrInstruction>>();

            foreach (IrInstruction instruction in block.Instructions)
            {
                foreach (IrOperand use in instruction.Annotation.Uses)
                {
                    if (!HybridCpuDependencyOperandClassifier.IsRegisterOperand(use))
                    {
                        continue;
                    }

                    foreach (HybridCpuRegisterDependencyKey key in EnumerateRegisterDependencyKeys(instruction, use))
                    {
                        if (lastDefinitions.TryGetValue(key, out IrInstruction? producer))
                        {
                            dependencies.Add(new IrInstructionDependency(
                                Kind: IrInstructionDependencyKind.RegisterRaw,
                                ProducerInstructionIndex: producer.Index,
                                ConsumerInstructionIndex: instruction.Index,
                                MinimumLatencyCycles: HybridCpuDependencyLatencyMatrix.ResolveRegisterRawLatency(producer, instruction),
                                RelatedOperandKind: use.Kind,
                                RelatedOperandValue: use.Value,
                                DominantEffectKind: YAKSys_Hybrid_CPU.Core.HazardEffectKind.RegisterData));
                        }

                        TrackLiveUse(liveUsesSinceLastDefinition, key, instruction);
                    }
                }

                foreach (IrOperand def in instruction.Annotation.Defs)
                {
                    if (!HybridCpuDependencyOperandClassifier.IsRegisterOperand(def))
                    {
                        continue;
                    }

                    foreach (HybridCpuRegisterDependencyKey key in EnumerateRegisterDependencyKeys(instruction, def))
                    {
                        if (liveUsesSinceLastDefinition.TryGetValue(key, out Dictionary<int, IrInstruction>? priorUses))
                        {
                            foreach (IrInstruction priorUse in priorUses.Values)
                            {
                                if (priorUse.Index == instruction.Index)
                                {
                                    continue;
                                }

                                dependencies.Add(new IrInstructionDependency(
                                    Kind: IrInstructionDependencyKind.RegisterWar,
                                    ProducerInstructionIndex: priorUse.Index,
                                    ConsumerInstructionIndex: instruction.Index,
                                    MinimumLatencyCycles: HybridCpuDependencyLatencyMatrix.ResolveRegisterWarLatency(priorUse, instruction),
                                    RelatedOperandKind: def.Kind,
                                    RelatedOperandValue: def.Value,
                                    DominantEffectKind: YAKSys_Hybrid_CPU.Core.HazardEffectKind.RegisterData));
                            }

                            priorUses.Clear();
                        }

                        if (lastDefinitions.TryGetValue(key, out IrInstruction? priorDefinition) &&
                            priorDefinition.Index != instruction.Index)
                        {
                            dependencies.Add(new IrInstructionDependency(
                                Kind: IrInstructionDependencyKind.RegisterWaw,
                                ProducerInstructionIndex: priorDefinition.Index,
                                ConsumerInstructionIndex: instruction.Index,
                                MinimumLatencyCycles: HybridCpuDependencyLatencyMatrix.ResolveRegisterWawLatency(priorDefinition, instruction),
                                RelatedOperandKind: def.Kind,
                                RelatedOperandValue: def.Value,
                                DominantEffectKind: YAKSys_Hybrid_CPU.Core.HazardEffectKind.RegisterData));
                        }

                        lastDefinitions[key] = instruction;
                    }
                }
            }
        }

        private static void TrackLiveUse(
            IDictionary<HybridCpuRegisterDependencyKey, Dictionary<int, IrInstruction>> liveUsesSinceLastDefinition,
            HybridCpuRegisterDependencyKey key,
            IrInstruction instruction)
        {
            if (!liveUsesSinceLastDefinition.TryGetValue(key, out Dictionary<int, IrInstruction>? uses))
            {
                uses = new Dictionary<int, IrInstruction>();
                liveUsesSinceLastDefinition[key] = uses;
            }

            uses[instruction.Index] = instruction;
        }

        private static void AddLoadAdjacentScalarFollowThroughDependencies(
            IrBasicBlock block,
            ICollection<IrInstructionDependency> dependencies)
        {
            for (int instructionIndex = 0; instructionIndex + 1 < block.Instructions.Count; instructionIndex++)
            {
                IrInstruction producer = block.Instructions[instructionIndex];
                IrInstruction consumer = block.Instructions[instructionIndex + 1];
                if (!ShouldSerializeLoadAdjacentScalarFollowThrough(producer, consumer))
                {
                    continue;
                }

                dependencies.Add(new IrInstructionDependency(
                    Kind: IrInstructionDependencyKind.Serialization,
                    ProducerInstructionIndex: producer.Index,
                    ConsumerInstructionIndex: consumer.Index,
                    MinimumLatencyCycles: HybridCpuDependencyLatencyMatrix.ResolveLoadAdjacentScalarFollowThroughLatency(producer, consumer),
                    StructuralResources: producer.Annotation.StructuralResources | consumer.Annotation.StructuralResources,
                    DominantEffectKind: YAKSys_Hybrid_CPU.Core.HazardEffectKind.MemoryBank));
            }
        }

        private static bool ShouldSerializeLoadAdjacentScalarFollowThrough(IrInstruction producer, IrInstruction consumer)
        {
            return producer.VirtualThreadId == consumer.VirtualThreadId &&
                   producer.Annotation.ResourceClass == IrResourceClass.LoadStore &&
                   consumer.Annotation.ResourceClass == IrResourceClass.ScalarAlu;
        }

        private static IEnumerable<HybridCpuRegisterDependencyKey> EnumerateRegisterDependencyKeys(IrInstruction instruction, IrOperand operand)
        {
            yield return HybridCpuRegisterDependencyGuard.GetVirtualThreadLocalKey(instruction, operand);

            if (HybridCpuRegisterDependencyGuard.TryGetCrossVirtualThreadGuardKey(instruction, operand, out HybridCpuRegisterDependencyKey sharedKey))
            {
                yield return sharedKey;
            }
        }
    }
}
