using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Centralized structural resource capacity model used by Stage 4 legality analysis.
    /// </summary>
    public static class HybridCpuStructuralResourceModel
    {
        private static readonly int LiveLsuLaneCapacity = SlotClassLaneMap.GetClassCapacity(SlotClass.LsuClass);

        private static readonly IrStructuralResource[] KnownResources =
        {
            IrStructuralResource.ReductionUnit,
            IrStructuralResource.VectorPermuteCrossbar,
            IrStructuralResource.AddressGenerationUnit,
            IrStructuralResource.LoadDataPort,
            IrStructuralResource.StoreDataPort,
            IrStructuralResource.BranchResolver,
            IrStructuralResource.ControlSequencer,
            IrStructuralResource.SystemSequencer,
            IrStructuralResource.CsrPort,
            IrStructuralResource.VmStatePort,
            IrStructuralResource.BarrierSequencer
        };

        /// <summary>
        /// Returns structural resources used by an opcode profile.
        /// </summary>
        public static IrStructuralResource ClassifyResources(InstructionsEnum opcode, YAKSys_Hybrid_CPU.Arch.OpcodeInfo? opcodeInfo, IrResourceClass resourceClass)
        {
            IrStructuralResource structuralResources = IrStructuralResource.None;

            if (resourceClass == IrResourceClass.ControlFlow)
            {
                structuralResources |= IrStructuralResource.ControlSequencer | IrStructuralResource.BranchResolver;
            }

            if (resourceClass == IrResourceClass.System)
            {
                structuralResources |= IrStructuralResource.SystemSequencer;
            }

            if (HybridCpuOpcodeSemantics.IsBarrierLike(opcode))
            {
                structuralResources |= IrStructuralResource.BarrierSequencer;
            }

            if (SupportsReduction(opcode, opcodeInfo))
            {
                structuralResources |= IrStructuralResource.ReductionUnit;
            }

            if (UsesPermuteCrossbar(opcode, opcodeInfo))
            {
                structuralResources |= IrStructuralResource.VectorPermuteCrossbar;
            }

            if (resourceClass == IrResourceClass.LoadStore && UsesMemoryReadPort(opcode, opcodeInfo))
            {
                structuralResources |= IrStructuralResource.LoadDataPort;
            }

            if (resourceClass == IrResourceClass.LoadStore && UsesMemoryWritePort(opcode, opcodeInfo))
            {
                structuralResources |= IrStructuralResource.StoreDataPort;
            }

            if (resourceClass == IrResourceClass.LoadStore && UsesAddressGeneration(opcode, opcodeInfo))
            {
                structuralResources |= IrStructuralResource.AddressGenerationUnit;
            }

            if (UsesCsrPort(opcode, opcodeInfo))
            {
                structuralResources |= IrStructuralResource.CsrPort;
            }

            if (UsesVmStatePort(opcode))
            {
                structuralResources |= IrStructuralResource.VmStatePort;
            }

            return structuralResources;
        }

        /// <summary>
        /// Returns the per-cycle capacity for one structural resource.
        /// </summary>
        public static int GetCapacity(IrStructuralResource resource)
        {
            return resource switch
            {
                IrStructuralResource.None => 0,
                IrStructuralResource.ReductionUnit => 1,
                IrStructuralResource.VectorPermuteCrossbar => 1,
                IrStructuralResource.AddressGenerationUnit => LiveLsuLaneCapacity,
                IrStructuralResource.LoadDataPort => LiveLsuLaneCapacity,
                IrStructuralResource.StoreDataPort => LiveLsuLaneCapacity,
                IrStructuralResource.BranchResolver => 1,
                IrStructuralResource.ControlSequencer => 1,
                IrStructuralResource.SystemSequencer => 1,
                IrStructuralResource.CsrPort => 1,
                IrStructuralResource.VmStatePort => 1,
                IrStructuralResource.BarrierSequencer => 1,
                _ => 1
            };
        }

        /// <summary>
        /// Summarizes structural resource pressure for a candidate instruction group.
        /// </summary>
        public static IrStructuralResourceAnalysis AnalyzeResources(IReadOnlyList<IrInstruction> instructions)
        {
            ArgumentNullException.ThrowIfNull(instructions);

            var usages = new List<IrStructuralResourceUsage>();
            var conflicts = new List<IrStructuralResourceUsage>();
            IrStructuralResource combinedResources = IrStructuralResource.None;

            foreach (IrStructuralResource resource in KnownResources)
            {
                int usedUnits = 0;
                foreach (IrInstruction instruction in instructions)
                {
                    if ((instruction.Annotation.StructuralResources & resource) != 0)
                    {
                        usedUnits++;
                    }
                }

                if (usedUnits == 0)
                {
                    continue;
                }

                combinedResources |= resource;
                int capacity = GetCapacity(resource);
                var usage = new IrStructuralResourceUsage(resource, usedUnits, capacity);
                usages.Add(usage);
                if (usage.IsOverSubscribed)
                {
                    conflicts.Add(usage);
                }
            }

            return new IrStructuralResourceAnalysis(combinedResources, usages, conflicts);
        }

        private static bool SupportsReduction(InstructionsEnum opcode, YAKSys_Hybrid_CPU.Arch.OpcodeInfo? opcodeInfo)
        {
            if (opcodeInfo.HasValue)
            {
                OpcodeInfo publishedOpcodeInfo = opcodeInfo.Value;
                if ((publishedOpcodeInfo.Flags & InstructionFlags.MaskManipulation) != 0)
                {
                    return false;
                }

                if (publishedOpcodeInfo.SupportsReduction)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool UsesPermuteCrossbar(InstructionsEnum opcode, OpcodeInfo? opcodeInfo)
        {
            OpcodeInfo? resolvedOpcodeInfo = opcodeInfo ?? HybridCpuOpcodeSemantics.GetOpcodeInfo(opcode);
            if (resolvedOpcodeInfo.HasValue)
            {
                OpcodeInfo info = resolvedOpcodeInfo.Value;
                InstructionFlags flags = info.Flags;

                if (info.InstructionClass != InstructionClass.ScalarAlu ||
                    !info.IsVector ||
                    !info.SupportsMasking ||
                    info.OperandCount != 2 ||
                    (flags & (InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite)) !=
                    (InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite))
                {
                    return false;
                }

                if (info.SupportsIndexed || (flags & InstructionFlags.UsesImmediate) != 0)
                {
                    return true;
                }

                return info.ExecutionLatency == 4 &&
                       (flags & (InstructionFlags.TwoOperand |
                                 InstructionFlags.ThreeOperand |
                                 InstructionFlags.Reduction |
                                 InstructionFlags.MaskManipulation |
                                 InstructionFlags.FloatingPoint)) == 0;
            }

            return false;
        }

        private static bool UsesMemoryReadPort(InstructionsEnum opcode, YAKSys_Hybrid_CPU.Arch.OpcodeInfo? opcodeInfo)
        {
            return HybridCpuOpcodeSemantics.UsesLoadStoreReadPath(opcode, opcodeInfo);
        }

        private static bool UsesMemoryWritePort(InstructionsEnum opcode, YAKSys_Hybrid_CPU.Arch.OpcodeInfo? opcodeInfo)
        {
            return HybridCpuOpcodeSemantics.UsesLoadStoreWritePath(opcode, opcodeInfo);
        }

        private static bool UsesAddressGeneration(InstructionsEnum opcode, YAKSys_Hybrid_CPU.Arch.OpcodeInfo? opcodeInfo)
        {
            return HybridCpuOpcodeSemantics.UsesAddressGeneration(opcode, opcodeInfo);
        }

        private static bool UsesCsrPort(InstructionsEnum opcode, OpcodeInfo? opcodeInfo)
        {
            OpcodeInfo? resolvedOpcodeInfo = opcodeInfo ?? HybridCpuOpcodeSemantics.GetOpcodeInfo(opcode);
            if (resolvedOpcodeInfo.HasValue &&
                resolvedOpcodeInfo.Value.InstructionClass == InstructionClass.Csr)
            {
                return true;
            }

            return InstructionClassifier.GetClass(opcode) == InstructionClass.Csr;
        }

        private static bool UsesVmStatePort(InstructionsEnum opcode)
        {
            return false;
        }

        private static bool IsBarrierLike(InstructionsEnum opcode)
        {
            return HybridCpuOpcodeSemantics.IsBarrierLike(opcode);
        }
    }
}
