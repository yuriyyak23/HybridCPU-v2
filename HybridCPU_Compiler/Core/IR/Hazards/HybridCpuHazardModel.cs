using System;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Centralized hazard metadata model for HybridCPU opcodes at the compiler layer.
    /// </summary>
    public static class HybridCpuHazardModel
    {
        /// <summary>
        /// Returns the compiler-visible execution profile for an opcode.
        /// </summary>
        public static IrOpcodeExecutionProfile GetExecutionProfile(InstructionsEnum opcode)
        {
            OpcodeInfo? opcodeInfo = HybridCpuOpcodeSemantics.GetOpcodeInfo(opcode);
            IrResourceClass resourceClass = ClassifyResource(opcode, opcodeInfo);
            IrSerializationKind serialization = ClassifySerialization(opcode, resourceClass);
            IrLatencyClass latencyClass = ClassifyLatency(resourceClass, serialization);
            byte minimumLatencyCycles = opcodeInfo?.ExecutionLatency ?? GetFallbackLatencyCycles(resourceClass, latencyClass);
            IrIssueSlotMask legalSlots = HybridCpuSlotModel.GetLegalSlots(resourceClass, opcodeInfo);
            IrStructuralResource structuralResources = ClassifyStructuralResources(opcode, opcodeInfo, resourceClass);
            SlotClass derivedSlotClass = IrSlotClassMapping.ToSlotClass(resourceClass);
            IrSlotBindingKind derivedBindingKind = IrSlotClassMapping.DerivePinningKind(resourceClass, serialization);

            return new IrOpcodeExecutionProfile(
                Opcode: opcode,
                ResourceClass: resourceClass,
                LatencyClass: latencyClass,
                MinimumLatencyCycles: minimumLatencyCycles,
                LegalSlots: legalSlots,
                Serialization: serialization,
                StructuralResources: structuralResources,
                DerivedSlotClass: derivedSlotClass,
                DerivedBindingKind: derivedBindingKind);
        }

        private static IrResourceClass ClassifyResource(InstructionsEnum opcode, OpcodeInfo? opcodeInfo)
        {
            if (HybridCpuOpcodeSemantics.IsLoadStoreOpcode(opcode, opcodeInfo))
            {
                return IrResourceClass.LoadStore;
            }

            if (opcodeInfo.HasValue)
            {
                InstructionClass instructionClass = opcodeInfo.Value.InstructionClass;
                if (instructionClass == InstructionClass.ControlFlow)
                {
                    return IrResourceClass.ControlFlow;
                }

                if (instructionClass is InstructionClass.System or
                    InstructionClass.Csr or
                    InstructionClass.SmtVt or
                    InstructionClass.Vmx)
                {
                    return IrResourceClass.System;
                }

                if (opcodeInfo.Value.IsVector)
                {
                    return IrResourceClass.VectorAlu;
                }

                if (instructionClass == InstructionClass.ScalarAlu)
                {
                    return IrResourceClass.ScalarAlu;
                }
            }

            if (IsControlFlowInstruction(opcode, opcodeInfo))
            {
                return IrResourceClass.ControlFlow;
            }

            if (HybridCpuOpcodeSemantics.IsSystemInstruction(opcode, opcodeInfo))
            {
                return IrResourceClass.System;
            }

            if (HybridCpuOpcodeSemantics.IsVectorInstruction(opcode, opcodeInfo))
            {
                return IrResourceClass.VectorAlu;
            }

            return IrResourceClass.ScalarAlu;
        }

        private static IrLatencyClass ClassifyLatency(IrResourceClass resourceClass, IrSerializationKind serialization)
        {
            if (resourceClass == IrResourceClass.LoadStore)
            {
                return IrLatencyClass.LoadUse;
            }

            if (resourceClass == IrResourceClass.ControlFlow)
            {
                return IrLatencyClass.ControlFlow;
            }

            if (resourceClass == IrResourceClass.DmaStream)
            {
                return IrLatencyClass.Serialized;
            }

            if (resourceClass == IrResourceClass.System || serialization != IrSerializationKind.None)
            {
                return IrLatencyClass.Serialized;
            }

            return resourceClass == IrResourceClass.VectorAlu ? IrLatencyClass.Vector : IrLatencyClass.SingleCycle;
        }

        private static IrSerializationKind ClassifySerialization(InstructionsEnum opcode, IrResourceClass resourceClass)
        {
            if (IsBarrierLike(opcode))
            {
                return IrSerializationKind.BarrierBoundary | IrSerializationKind.ExclusiveCycle;
            }

            if (resourceClass == IrResourceClass.ControlFlow)
            {
                return IrSerializationKind.ControlFlowBoundary | IrSerializationKind.ExclusiveCycle;
            }

            if (resourceClass == IrResourceClass.System)
            {
                return IrSerializationKind.SystemBoundary | IrSerializationKind.ExclusiveCycle;
            }

            if (resourceClass == IrResourceClass.DmaStream)
            {
                return IrSerializationKind.SystemBoundary | IrSerializationKind.ExclusiveCycle;
            }

            return IrSerializationKind.None;
        }

        private static IrStructuralResource ClassifyStructuralResources(InstructionsEnum opcode, OpcodeInfo? opcodeInfo, IrResourceClass resourceClass)
        {
            return HybridCpuStructuralResourceModel.ClassifyResources(opcode, opcodeInfo, resourceClass);
        }

        private static byte GetFallbackLatencyCycles(IrResourceClass resourceClass, IrLatencyClass latencyClass)
        {
            return latencyClass switch
            {
                IrLatencyClass.LoadUse => 4,
                IrLatencyClass.Vector => 2,
                IrLatencyClass.ControlFlow => 1,
                IrLatencyClass.Serialized => (byte)(resourceClass == IrResourceClass.DmaStream ? 8 : 1),
                _ => resourceClass == IrResourceClass.ScalarAlu ? (byte)1 : (byte)2
            };
        }

        private static bool IsControlFlowInstruction(InstructionsEnum opcode, OpcodeInfo? opcodeInfo)
        {
            OpcodeInfo? semanticOpcodeInfo = opcodeInfo ?? HybridCpuOpcodeSemantics.GetOpcodeInfo(opcode);
            if (semanticOpcodeInfo.HasValue && semanticOpcodeInfo.Value.IsControlFlow)
            {
                return true;
            }

            return HybridCpuOpcodeSemantics.TryResolveRetainedCompatibilityControlFlowKind(
                opcode,
                out _);
        }

        private static bool IsVectorInstruction(InstructionsEnum opcode, OpcodeInfo? opcodeInfo)
        {
            return HybridCpuOpcodeSemantics.IsVectorInstruction(opcode, opcodeInfo);
        }

        private static bool IsSystemInstruction(InstructionsEnum opcode, OpcodeInfo? opcodeInfo)
        {
            return HybridCpuOpcodeSemantics.IsSystemInstruction(opcode, opcodeInfo);
        }

        private static bool IsBarrierLike(InstructionsEnum opcode)
        {
            return HybridCpuOpcodeSemantics.IsBarrierLike(opcode);
        }
    }
}
