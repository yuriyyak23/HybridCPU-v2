using HybridCPU_ISE.Arch;
using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Builds the first normalized IR layer from the current encoded instruction stream.
    /// </summary>
    public sealed partial class HybridCpuIrBuilder
    {
        private const ulong EncodedInstructionSizeBytes = 32;
        private const ulong PackedArchRegisterTupleMask = 0xFFFFFFFFFFFF0000UL;

        private readonly record struct DecodedRegisterTuple(
            IrOperand? First,
            IrOperand? Second,
            IrOperand? Third)
        {
            public bool HasAny => First is not null || Second is not null || Third is not null;
        }

        /// <summary>
        /// Builds an IR program for a single virtual-thread instruction stream.
        /// </summary>
        public IrProgram BuildProgram(
            byte virtualThreadId,
            ReadOnlySpan<VLIW_Instruction> instructions,
            IReadOnlyList<IrLabelDeclaration>? labelDeclarations = null,
            IReadOnlyList<IrEntryPointDeclaration>? entryPointDeclarations = null,
            IReadOnlyList<IrInstructionSourceBinding>? instructionSourceBindings = null,
            IReadOnlyList<IrSectionDeclaration>? sectionDeclarations = null,
            IReadOnlyList<IrFunctionDeclaration>? functionDeclarations = null,
            VliwBundleAnnotations? bundleAnnotations = null,
            ulong domainTag = 0)
        {
            var irInstructions = new List<IrInstruction>(instructions.Length);

            for (int index = 0; index < instructions.Length; index++)
            {
                IrSlotMetadata slotMetadata = ResolveSlotMetadata(virtualThreadId, index, bundleAnnotations);
                irInstructions.Add(BuildInstruction(index, instructions[index], slotMetadata, domainTag));
            }

            ApplyInstructionSourceBindings(irInstructions, instructionSourceBindings);

            var graph = BuildControlFlowGraph(irInstructions, labelDeclarations, entryPointDeclarations);
            var labels = BuildLabels(virtualThreadId, irInstructions, graph.Blocks, labelDeclarations, entryPointDeclarations);
            var blocks = ApplyPrimaryLabels(graph.Blocks, labels);
            var entryPoints = BuildEntryPoints(virtualThreadId, irInstructions, blocks, entryPointDeclarations);
            var ownedProgram = BuildProgramContainers(virtualThreadId, blocks, labels, entryPoints, sectionDeclarations, functionDeclarations);

            return new IrProgram(
                virtualThreadId,
                irInstructions,
                graph with { Blocks = ownedProgram.Blocks },
                ownedProgram.Labels,
                ownedProgram.EntryPoints,
                ownedProgram.Sections,
                ownedProgram.Functions,
                ownedProgram.Symbols);
        }

        private static void ApplyInstructionSourceBindings(
            IList<IrInstruction> instructions,
            IReadOnlyList<IrInstructionSourceBinding>? instructionSourceBindings)
        {
            if (instructionSourceBindings is null)
            {
                return;
            }

            for (int index = 0; index < instructionSourceBindings.Count; index++)
            {
                var binding = instructionSourceBindings[index];
                ValidateMetadataInstructionIndex(binding.InstructionIndex, instructions.Count, $"instruction[{binding.InstructionIndex}]");
                instructions[binding.InstructionIndex] = instructions[binding.InstructionIndex] with { SourceSpan = binding.SourceSpan };
            }
        }

        private static IrInstruction BuildInstruction(
            int index,
            VLIW_Instruction instruction,
            IrSlotMetadata slotMetadata,
            ulong domainTag)
        {
            var opcode = (InstructionsEnum)instruction.OpCode;
            OpcodeInfo? opcodeInfo = HybridCpuOpcodeSemantics.GetOpcodeInfo(opcode);
            ValidateExplicitAcceleratorIntent(opcode, slotMetadata);
            var operands = BuildOperands(opcode, instruction);
            var defs = BuildDefs(opcode, instruction);
            var uses = BuildUses(opcode, instruction);
            IrOpcodeExecutionProfile executionProfile = HybridCpuHazardModel.GetExecutionProfile(opcode);
            slotMetadata = slotMetadata.WithAdmissionDescriptor(executionProfile);
            IrTypedSlotAdmissionDescriptor admissionDescriptor = slotMetadata.AdmissionDescriptor!.Value;

            var annotation = new IrInstructionAnnotation(
                ResourceClass: admissionDescriptor.ResourceClass,
                LatencyClass: executionProfile.LatencyClass,
                MinimumLatencyCycles: executionProfile.MinimumLatencyCycles,
                LegalSlots: admissionDescriptor.LegalSlots,
                Serialization: executionProfile.Serialization,
                StructuralResources: executionProfile.StructuralResources,
                ControlFlowKind: ClassifyControlFlow(opcode),
                IsBarrierLike: IsBarrierLike(opcode),
                MayTrap: MayTrap(opcode, opcodeInfo),
                EncodedBranchTarget: GetEncodedBranchTarget(opcode, instruction),
                ResolvedBranchTargetInstructionIndex: null,
                MemoryReadRegion: BuildMemoryRegion(opcode, instruction, isWrite: false, opcodeInfo),
                MemoryWriteRegion: BuildMemoryRegion(opcode, instruction, isWrite: true, opcodeInfo),
                Defs: defs,
                Uses: uses,
                RequiredSlotClass: admissionDescriptor.RequiredSlotClass,
                BindingKind: admissionDescriptor.BindingKind,
                DomainTag: domainTag,
                StealabilityHint: slotMetadata.StealabilityHint);

            var (instructionClass, serializationClass) = InstructionClassifier.Classify(opcode);

            return new IrInstruction(
                Index: index,
                VirtualThreadId: slotMetadata.VirtualThreadId,
                EncodedAddress: (ulong)index * EncodedInstructionSizeBytes,
                Opcode: opcode,
                DataType: instruction.DataTypeValue,
                PredicateMask: instruction.PredicateMask,
                Immediate: instruction.Immediate,
                StreamLength: instruction.StreamLength,
                Stride: instruction.Stride,
                RowStride: instruction.RowStride,
                Indexed: instruction.Indexed,
                Is2D: instruction.Is2D,
                Reduction: instruction.Reduction,
                TailAgnostic: instruction.TailAgnostic,
                MaskAgnostic: instruction.MaskAgnostic,
                Operands: operands,
                Annotation: annotation)
            {
                InstructionClass = instructionClass,
                SerializationClass = serializationClass,
                DmaStreamComputeDescriptor = slotMetadata.DmaStreamComputeDescriptor,
                AcceleratorCommandDescriptor = slotMetadata.AcceleratorCommandDescriptor,
            };
        }

        private static IrSlotMetadata ResolveSlotMetadata(
            byte defaultVirtualThreadId,
            int instructionIndex,
            VliwBundleAnnotations? bundleAnnotations)
        {
            if (bundleAnnotations is not null
                && bundleAnnotations.TryGetInstructionSlotMetadata(instructionIndex, out InstructionSlotMetadata metadata))
            {
                return IrSlotMetadata.FromInstructionMetadata(metadata);
            }

            return IrSlotMetadata.DefaultForVirtualThread(defaultVirtualThreadId);
        }

        private static void ValidateExplicitAcceleratorIntent(
            InstructionsEnum opcode,
            IrSlotMetadata slotMetadata)
        {
            bool hasDmaStreamDescriptor = slotMetadata.DmaStreamComputeDescriptor is not null;
            bool hasAcceleratorDescriptor = slotMetadata.AcceleratorCommandDescriptor is not null;
            if (hasDmaStreamDescriptor && hasAcceleratorDescriptor)
            {
                throw new InvalidOperationException(
                    "Compiler typed sideband cannot carry lane6 DmaStreamCompute and lane7 L7-SDC descriptors on the same instruction.");
            }

            if (hasDmaStreamDescriptor &&
                !HybridCpuOpcodeSemantics.IsDmaStreamComputeOpcode(opcode))
            {
                throw new InvalidOperationException(
                    "DmaStreamCompute descriptor sideband may only accompany the native lane6 DmaStreamCompute compiler contour.");
            }

            if (!OpcodeRegistry.IsSystemDeviceCommandOpcode((uint)opcode))
            {
                if (hasAcceleratorDescriptor)
                {
                    throw new InvalidOperationException(
                        "AcceleratorCommandDescriptor sideband may only accompany explicit L7-SDC compiler accelerator intent.");
                }

                return;
            }

            if (opcode != InstructionsEnum.ACCEL_SUBMIT ||
                !hasAcceleratorDescriptor)
            {
                throw new InvalidOperationException(
                    "Compiler L7-SDC emission requires explicit accelerator intent and typed ACCEL_SUBMIT descriptor sideband before native opcode emission.");
            }
        }

        private static IReadOnlyList<IrOperand> BuildOperands(InstructionsEnum opcode, VLIW_Instruction instruction)
        {
            if (HybridCpuOpcodeSemantics.IsDmaStreamComputeOpcode(opcode))
            {
                return Array.Empty<IrOperand>();
            }

            var operands = new List<IrOperand>(8);
            DecodedRegisterTuple word1Registers = DecodeWord1Registers(opcode, instruction);
            if (word1Registers.HasAny)
            {
                AddDecodedOperands(operands, word1Registers);
            }
            else
            {
                AddIfNotNull(operands, CreatePointerOperand("destsrc1", instruction.DestSrc1Pointer));
            }

            DecodedRegisterTuple controlRegisters = DecodeControlRegisters(opcode, instruction);
            if (controlRegisters.HasAny)
            {
                AddDecodedOperands(operands, controlRegisters);
            }
            else
            {
                AddIfNotNull(operands, CreatePointerOperand("src2", instruction.Src2Pointer));
            }

            if (instruction.Immediate != 0)
            {
                operands.Add(new IrOperand(IrOperandKind.Immediate, instruction.Immediate, "imm"));
            }

            if (instruction.PredicateMask != 0)
            {
                operands.Add(new IrOperand(IrOperandKind.PredicateMask, instruction.PredicateMask, "pred"));
            }

            if (instruction.StreamLength != 0)
            {
                operands.Add(new IrOperand(IrOperandKind.StreamLength, instruction.StreamLength, "vl"));
            }

            if (instruction.Stride != 0)
            {
                operands.Add(new IrOperand(IrOperandKind.Stride, instruction.Stride, "stride"));
            }

            if (instruction.RowStride != 0)
            {
                operands.Add(new IrOperand(IrOperandKind.RowStride, instruction.RowStride, "rowStride"));
            }

            return operands;
        }

        private static IReadOnlyList<IrOperand> BuildDefs(InstructionsEnum opcode, VLIW_Instruction instruction)
        {
            if (HybridCpuOpcodeSemantics.IsDmaStreamComputeOpcode(opcode))
            {
                return Array.Empty<IrOperand>();
            }

            var defs = new List<IrOperand>(1);
            DecodedRegisterTuple word1Registers = DecodeWord1Registers(opcode, instruction);
            if (word1Registers.HasAny)
            {
                if (WritesDecodedDestination(opcode))
                {
                    AddIfNotNull(defs, word1Registers.First);
                }

                return defs;
            }

            var destOperand = CreatePointerOperand("destsrc1", instruction.DestSrc1Pointer);

            if (destOperand is not null && WritesPrimaryOperand(opcode))
            {
                defs.Add(destOperand);
            }

            return defs;
        }

        private static IReadOnlyList<IrOperand> BuildUses(InstructionsEnum opcode, VLIW_Instruction instruction)
        {
            if (HybridCpuOpcodeSemantics.IsDmaStreamComputeOpcode(opcode))
            {
                return Array.Empty<IrOperand>();
            }

            var uses = new List<IrOperand>(6);
            DecodedRegisterTuple word1Registers = DecodeWord1Registers(opcode, instruction);
            DecodedRegisterTuple controlRegisters = DecodeControlRegisters(opcode, instruction);

            if (ClassifyControlFlow(opcode) != IrControlFlowKind.None)
            {
                if (controlRegisters.HasAny)
                {
                    AddDecodedOperands(uses, controlRegisters);
                }
                else if (word1Registers.HasAny)
                {
                    AddIfNotNull(uses, word1Registers.Second);
                    AddIfNotNull(uses, word1Registers.Third);
                }
            }
            else if (word1Registers.HasAny)
            {
                AddIfNotNull(uses, word1Registers.Second);
                AddIfNotNull(uses, word1Registers.Third);
            }

            var destOperand = CreatePointerOperand("destsrc1", instruction.DestSrc1Pointer);
            var src2Operand = CreatePointerOperand("src2", instruction.Src2Pointer);

            if (destOperand is not null && ReadsPrimaryOperand(opcode) && !word1Registers.HasAny)
            {
                uses.Add(destOperand);
            }

            if (src2Operand is not null && ReadsSecondaryOperand(opcode) && !controlRegisters.HasAny)
            {
                uses.Add(src2Operand);
            }

            if (instruction.Immediate != 0 && ReadsImmediate(opcode))
            {
                uses.Add(new IrOperand(IrOperandKind.Immediate, instruction.Immediate, "imm"));
            }

            if (instruction.PredicateMask != 0)
            {
                uses.Add(new IrOperand(IrOperandKind.PredicateMask, instruction.PredicateMask, "pred"));
            }

            return uses;
        }

        private static bool HasFallthroughEdge(IrControlFlowKind controlFlowKind)
        {
            return controlFlowKind == IrControlFlowKind.None || controlFlowKind == IrControlFlowKind.ConditionalBranch;
        }

        private static bool IsBlockTerminator(IrInstruction instruction)
        {
            return instruction.Annotation.ControlFlowKind != IrControlFlowKind.None || instruction.Annotation.IsBarrierLike;
        }

        private static IrOperand? CreatePointerOperand(string name, ulong value)
        {
            return value == 0 ? null : new IrOperand(IrOperandKind.Pointer, value, name);
        }

        private static IrOperand? CreateArchitecturalRegisterOperand(string name, byte registerId)
        {
            return registerId == VLIW_Instruction.NoArchReg
                ? null
                : new IrOperand(IrOperandKind.Pointer, registerId, name);
        }

        private static void AddIfNotNull(ICollection<IrOperand> operands, IrOperand? operand)
        {
            if (operand is not null)
            {
                operands.Add(operand);
            }
        }

        private static void AddDecodedOperands(ICollection<IrOperand> operands, DecodedRegisterTuple decodedRegisters)
        {
            AddIfNotNull(operands, decodedRegisters.First);
            AddIfNotNull(operands, decodedRegisters.Second);
            AddIfNotNull(operands, decodedRegisters.Third);
        }

        private static DecodedRegisterTuple DecodeWord1Registers(InstructionsEnum opcode, VLIW_Instruction instruction)
        {
            if (opcode == InstructionsEnum.Move ||
                HybridCpuOpcodeSemantics.TryResolveRetainedCompatibilityScalarMemoryDirection(opcode, out _) ||
                !OpcodeRegistry.UsesPackedArchRegisterWord1(opcode) ||
                !HasPackedArchRegisterTuple(instruction.Word1) ||
                !VLIW_Instruction.TryUnpackArchRegs(instruction.Word1, out byte rd, out byte rs1, out byte rs2))
            {
                return default;
            }

            return new DecodedRegisterTuple(
                CreateArchitecturalRegisterOperand("rd", rd),
                CreateArchitecturalRegisterOperand("rs1", rs1),
                CreateArchitecturalRegisterOperand("rs2", rs2));
        }

        private static DecodedRegisterTuple DecodeControlRegisters(InstructionsEnum opcode, VLIW_Instruction instruction)
        {
            if (ClassifyControlFlow(opcode) == IrControlFlowKind.None ||
                !HasPackedArchRegisterTuple(instruction.Src2Pointer) ||
                !VLIW_Instruction.TryUnpackArchRegs(instruction.Src2Pointer, out byte first, out byte second, out byte third))
            {
                return default;
            }

            return new DecodedRegisterTuple(
                CreateArchitecturalRegisterOperand("ctrl0", first),
                CreateArchitecturalRegisterOperand("ctrl1", second),
                CreateArchitecturalRegisterOperand("ctrl2", third));
        }

        private static bool HasPackedArchRegisterTuple(ulong packedRegisters)
        {
            return (packedRegisters & PackedArchRegisterTupleMask) != 0;
        }

        private static IrResourceClass ClassifyResource(InstructionsEnum opcode)
        {
            if (HybridCpuOpcodeSemantics.IsDmaStreamComputeOpcode(opcode))
            {
                return IrResourceClass.DmaStream;
            }

            if (HybridCpuOpcodeSemantics.IsLoadStoreOpcode(opcode))
            {
                return IrResourceClass.LoadStore;
            }

            if (ClassifyControlFlow(opcode) != IrControlFlowKind.None)
            {
                return IrResourceClass.ControlFlow;
            }

            if (IsSystemInstruction(opcode))
            {
                return IrResourceClass.System;
            }

            return IsVectorInstruction(opcode) ? IrResourceClass.VectorAlu : IrResourceClass.ScalarAlu;
        }

        private static IrLatencyClass ClassifyLatency(InstructionsEnum opcode)
        {
            if (HybridCpuOpcodeSemantics.IsLoadStoreOpcode(opcode))
            {
                return IrLatencyClass.LoadUse;
            }

            if (ClassifyControlFlow(opcode) != IrControlFlowKind.None)
            {
                return IrLatencyClass.ControlFlow;
            }

            if (IsBarrierLike(opcode) || IsSystemInstruction(opcode))
            {
                return IrLatencyClass.Serialized;
            }

            return IsVectorInstruction(opcode) ? IrLatencyClass.Vector : IrLatencyClass.SingleCycle;
        }

        private static IrControlFlowKind ClassifyControlFlow(InstructionsEnum opcode)
        {
            InstructionsEnum semanticOpcode = HybridCpuOpcodeSemantics.NormalizeSemanticOpcode(opcode);
            OpcodeInfo? opcodeInfo = HybridCpuOpcodeSemantics.GetOpcodeInfo(opcode);
            if (opcodeInfo.HasValue)
            {
                if (opcodeInfo.Value.InstructionClass == InstructionClass.ControlFlow)
                {
                    if (TryResolvePublishedControlFlowKind(semanticOpcode, out IrControlFlowKind controlFlowKind))
                    {
                        return controlFlowKind;
                    }

                    return opcodeInfo.Value.OperandCount == 1
                        ? IrControlFlowKind.UnconditionalBranch
                        : IrControlFlowKind.ConditionalBranch;
                }

                if (opcodeInfo.Value.InstructionClass == InstructionClass.System)
                {
                    if (TryResolvePublishedSystemEventKind(opcode, out YAKSys_Hybrid_CPU.Core.SystemEventKind systemEventKind))
                    {
                        return systemEventKind is
                            YAKSys_Hybrid_CPU.Core.SystemEventKind.Wfi or
                            YAKSys_Hybrid_CPU.Core.SystemEventKind.Ebreak
                            ? IrControlFlowKind.Stop
                            : IrControlFlowKind.None;
                    }

                    return IrControlFlowKind.None;
                }
            }

            return HybridCpuOpcodeSemantics.TryResolveRetainedCompatibilityControlFlowKind(
                opcode,
                out IrControlFlowKind retainedControlFlowKind)
                ? retainedControlFlowKind
                : IrControlFlowKind.None;
        }

        private static bool TryResolvePublishedControlFlowKind(
            InstructionsEnum opcode,
            out IrControlFlowKind controlFlowKind)
        {
            controlFlowKind = IrControlFlowKind.None;

            OpcodeInfo? opcodeInfo = HybridCpuOpcodeSemantics.GetOpcodeInfo(opcode);
            if (!opcodeInfo.HasValue || opcodeInfo.Value.InstructionClass != InstructionClass.ControlFlow)
            {
                return false;
            }

            controlFlowKind = opcode switch
            {
                InstructionsEnum.JAL => IrControlFlowKind.UnconditionalBranch,
                InstructionsEnum.JALR => IrControlFlowKind.Return,
                InstructionsEnum.BEQ or
                InstructionsEnum.BNE or
                InstructionsEnum.BLT or
                InstructionsEnum.BGE or
                InstructionsEnum.BLTU or
                InstructionsEnum.BGEU => IrControlFlowKind.ConditionalBranch,
                _ => IrControlFlowKind.None,
            };

            return controlFlowKind != IrControlFlowKind.None;
        }

        private static bool TryResolvePublishedSystemEventKind(
            InstructionsEnum opcode,
            out YAKSys_Hybrid_CPU.Core.SystemEventKind systemEventKind)
        {
            systemEventKind = default;

            OpcodeInfo? opcodeInfo = HybridCpuOpcodeSemantics.GetOpcodeInfo(opcode);
            if (!opcodeInfo.HasValue ||
                opcodeInfo.Value.InstructionClass is not (InstructionClass.System or InstructionClass.SmtVt))
            {
                return false;
            }

            switch (opcode)
            {
                case InstructionsEnum.FENCE:
                    systemEventKind = YAKSys_Hybrid_CPU.Core.SystemEventKind.Fence;
                    return true;
                case InstructionsEnum.FENCE_I:
                    systemEventKind = YAKSys_Hybrid_CPU.Core.SystemEventKind.FenceI;
                    return true;
                case InstructionsEnum.ECALL:
                    systemEventKind = YAKSys_Hybrid_CPU.Core.SystemEventKind.Ecall;
                    return true;
                case InstructionsEnum.EBREAK:
                    systemEventKind = YAKSys_Hybrid_CPU.Core.SystemEventKind.Ebreak;
                    return true;
                case InstructionsEnum.MRET:
                    systemEventKind = YAKSys_Hybrid_CPU.Core.SystemEventKind.Mret;
                    return true;
                case InstructionsEnum.SRET:
                    systemEventKind = YAKSys_Hybrid_CPU.Core.SystemEventKind.Sret;
                    return true;
                case InstructionsEnum.WFI:
                    systemEventKind = YAKSys_Hybrid_CPU.Core.SystemEventKind.Wfi;
                    return true;
                case InstructionsEnum.WFE:
                    systemEventKind = YAKSys_Hybrid_CPU.Core.SystemEventKind.Wfe;
                    return true;
                case InstructionsEnum.SEV:
                    systemEventKind = YAKSys_Hybrid_CPU.Core.SystemEventKind.Sev;
                    return true;
                case InstructionsEnum.YIELD:
                    systemEventKind = YAKSys_Hybrid_CPU.Core.SystemEventKind.Yield;
                    return true;
                case InstructionsEnum.POD_BARRIER:
                    systemEventKind = YAKSys_Hybrid_CPU.Core.SystemEventKind.PodBarrier;
                    return true;
                case InstructionsEnum.VT_BARRIER:
                    systemEventKind = YAKSys_Hybrid_CPU.Core.SystemEventKind.VtBarrier;
                    return true;
                default:
                    return false;
            }
        }

        private static bool ReadsPrimaryOperand(InstructionsEnum opcode)
        {
            if (HybridCpuOpcodeSemantics.TryResolveRetainedCompatibilityVectorTransferDirection(
                    opcode,
                    out bool isVectorWriteContour) &&
                !isVectorWriteContour)
            {
                return false;
            }

            if (HybridCpuOpcodeSemantics.TryResolveRetainedCompatibilityScalarMemoryDirection(
                    opcode,
                    out bool isWriteContour) &&
                !isWriteContour)
            {
                return false;
            }

            return opcode switch
            {
                InstructionsEnum.Move_Num => false,
                _ when ClassifyControlFlow(opcode) != IrControlFlowKind.None => false,
                _ when IsSystemInstruction(opcode) => false,
                _ => true
            };
        }

        private static bool WritesPrimaryOperand(InstructionsEnum opcode)
        {
            if (HybridCpuOpcodeSemantics.TryResolveRetainedCompatibilityVectorTransferDirection(
                    opcode,
                    out bool isVectorWriteContour) &&
                isVectorWriteContour)
            {
                return false;
            }

            if (HybridCpuOpcodeSemantics.TryResolveRetainedCompatibilityScalarMemoryDirection(
                    opcode,
                    out bool isWriteContour) &&
                isWriteContour)
            {
                return false;
            }

            return opcode switch
            {
                _ when ClassifyControlFlow(opcode) != IrControlFlowKind.None => false,
                _ when IsSystemInstruction(opcode) && opcode != InstructionsEnum.CSRRS => false,
                _ => true
            };
        }

        private static bool ReadsSecondaryOperand(InstructionsEnum opcode)
        {
            return opcode switch
            {
                InstructionsEnum.Move_Num => false,
                InstructionsEnum.CSRRS => false,
                _ when opcode == InstructionsEnum.VNOT => false,
                _ when ClassifyControlFlow(opcode) != IrControlFlowKind.None => false,
                _ => true
            };
        }

        private static bool ReadsImmediate(InstructionsEnum opcode)
        {
            return opcode switch
            {
                InstructionsEnum.Move_Num => true,
                _ when HasEncodedBranchTarget(opcode) => true,
                _ => false
            };
        }

        private static bool HasEncodedBranchTarget(InstructionsEnum opcode)
        {
            return ClassifyControlFlow(opcode) is IrControlFlowKind.ConditionalBranch or IrControlFlowKind.UnconditionalBranch;
        }

        private static ulong? GetEncodedBranchTarget(InstructionsEnum opcode, VLIW_Instruction instruction)
        {
            if (!HasEncodedBranchTarget(opcode))
            {
                return null;
            }

            if (instruction.Immediate != 0)
            {
                return instruction.Immediate;
            }

            if (instruction.Src2Pointer != 0 && !HasPackedArchRegisterTuple(instruction.Src2Pointer))
            {
                return instruction.Src2Pointer;
            }

            return null;
        }

        private static bool IsBarrierLike(InstructionsEnum opcode)
        {
            return HybridCpuOpcodeSemantics.IsBarrierLike(opcode);
        }

        private static bool MayTrap(InstructionsEnum opcode, OpcodeInfo? opcodeInfo)
        {
            OpcodeInfo? resolvedOpcodeInfo = opcodeInfo ?? HybridCpuOpcodeSemantics.GetOpcodeInfo(opcode);
            if (HybridCpuOpcodeSemantics.IsLoadStoreOpcode(opcode, resolvedOpcodeInfo))
            {
                return true;
            }

            if (resolvedOpcodeInfo.HasValue &&
                resolvedOpcodeInfo.Value.InstructionClass == InstructionClass.ControlFlow)
            {
                return ClassifyControlFlow(opcode) == IrControlFlowKind.UnconditionalBranch;
            }

            if (resolvedOpcodeInfo.HasValue &&
                resolvedOpcodeInfo.Value.InstructionClass == InstructionClass.ScalarAlu)
            {
                return YAKSys_Hybrid_CPU.Core.Pipeline.InternalOpBuilder.MapToKind(unchecked((ushort)opcode)) ==
                       YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps.InternalOpKind.Div;
            }

            return opcode switch
            {
                InstructionsEnum.Modulus => true,
                _ when IsSystemInstruction(opcode, resolvedOpcodeInfo) => true,
                _ => false
            };
        }

        private static IrMemoryRegion? BuildMemoryRegion(
            InstructionsEnum opcode,
            VLIW_Instruction instruction,
            bool isWrite,
            OpcodeInfo? opcodeInfo)
        {
            bool hasRequiredDirection = isWrite
                ? HybridCpuOpcodeSemantics.UsesLoadStoreWritePath(opcode, opcodeInfo)
                : HybridCpuOpcodeSemantics.UsesLoadStoreReadPath(opcode, opcodeInfo);

            if (!hasRequiredDirection)
            {
                return null;
            }

            ulong? address = instruction.Src2Pointer != 0
                ? instruction.Src2Pointer
                : instruction.DestSrc1Pointer;

            if (address is null)
            {
                return null;
            }

            uint length = EstimateMemoryLength(instruction);
            return new IrMemoryRegion(address.Value, length, isWrite);
        }

        private static uint EstimateMemoryLength(VLIW_Instruction instruction)
        {
            ulong logicalLength = instruction.StreamLength == 0 ? 1UL : instruction.StreamLength;
            ulong bytes = logicalLength * (ulong)DataTypeUtils.SizeOf(instruction.DataTypeValue);
            return bytes > uint.MaxValue ? uint.MaxValue : (uint)bytes;
        }

        private static bool IsVectorInstruction(InstructionsEnum opcode)
        {
            return HybridCpuOpcodeSemantics.IsVectorInstruction(opcode);
        }

        private static bool IsSystemInstruction(InstructionsEnum opcode, OpcodeInfo? opcodeInfo = null)
        {
            return HybridCpuOpcodeSemantics.IsSystemInstruction(opcode, opcodeInfo);
        }

        private static bool WritesDecodedDestination(InstructionsEnum opcode)
        {
            if (ClassifyControlFlow(opcode) != IrControlFlowKind.None)
            {
                return false;
            }

            OpcodeInfo? opcodeInfo = HybridCpuOpcodeSemantics.GetOpcodeInfo(opcode);
            if (opcodeInfo.HasValue)
            {
                if (opcodeInfo.Value.InstructionClass == InstructionClass.Memory &&
                    opcodeInfo.Value.SerializationClass == SerializationClass.MemoryOrdered)
                {
                    return false;
                }

                if (opcodeInfo.Value.InstructionClass == InstructionClass.Csr &&
                    opcodeInfo.Value.OperandCount == 0)
                {
                    return false;
                }
            }

            if (HybridCpuOpcodeSemantics.TryResolveRetainedCompatibilityScalarMemoryDirection(
                    opcode,
                    out bool isRetainedWriteContour))
            {
                return !isRetainedWriteContour;
            }

            if (HybridCpuOpcodeSemantics.TryResolveRetainedCompatibilityVectorTransferDirection(
                    opcode,
                    out bool isRetainedVectorWriteContour))
            {
                return !isRetainedVectorWriteContour;
            }

            return true;
        }
    }
}
