using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using global::YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.Core.Legality
{
    /// <summary>
    /// Canonical Phase 03 legality analyzer over decoded IR bundles.
    /// Classifies slot occupancy, typed-slot facts, and bundle-local dependency
    /// summaries from <see cref="DecodedInstructionBundle"/> without reintroducing
    /// legacy <see cref="MicroOp"/> materialization.
    /// </summary>
    public sealed class BundleLegalityAnalyzer
    {
        private const int BundleWidth = BundleMetadata.BundleSlotCount;

        public BundleLegalityDescriptor Analyze(DecodedInstructionBundle bundle)
        {
            ArgumentNullException.ThrowIfNull(bundle);

            byte occupiedSlotMask = 0;
            var slotDescriptors = new DecodedBundleSlotDescriptor[bundle.SlotCount];
            ulong readRegisterMask = 0;
            ulong writeRegisterMask = 0;
            ResourceBitset aggregateResourceMask = ResourceBitset.Zero;
            byte aluClassMask = 0;
            byte lsuClassMask = 0;
            byte dmaStreamClassMask = 0;
            byte branchControlMask = 0;
            byte systemSingletonMask = 0;
            byte unclassifiedMask = 0;
            byte pinnedSlotMask = 0;
            byte flexibleSlotMask = 0;
            DecodedBundleFlags flags = DecodedBundleFlags.None;
            int minVirtualThreadId = int.MaxValue;
            int maxVirtualThreadId = int.MinValue;
            bool hasNonEmptySlot = false;

            for (int slotIndex = 0; slotIndex < bundle.SlotCount; slotIndex++)
            {
                DecodedInstruction slot = bundle.GetDecodedSlot(slotIndex);
                byte slotBit = (byte)(1 << slotIndex);
                if (!slot.IsOccupied)
                {
                    slotDescriptors[slotIndex] = CreateEmptySlotDescriptor(slotIndex);
                    flags |= DecodedBundleFlags.HasEmptyOrNopSlots;
                    continue;
                }

                occupiedSlotMask |= slotBit;
                flags |= DecodedBundleFlags.HasValidSlots;
                hasNonEmptySlot = true;

                InstructionIR instruction = slot.RequireInstruction();
                SlotClass slotClass = ClassifySlotClass(instruction);
                DecodedBundleSlotDescriptor slotDescriptor =
                    CreateCanonicalSlotDescriptor(slotIndex, slot, instruction, slotClass);
                slotDescriptors[slotIndex] = slotDescriptor;
                AccumulateDependencyInputs(slotDescriptor, ref readRegisterMask, ref writeRegisterMask, ref aggregateResourceMask);
                minVirtualThreadId = Math.Min(minVirtualThreadId, slotDescriptor.VirtualThreadId);
                maxVirtualThreadId = Math.Max(maxVirtualThreadId, slotDescriptor.VirtualThreadId);
                switch (slotClass)
                {
                    case SlotClass.AluClass:
                        aluClassMask |= slotBit;
                        break;
                    case SlotClass.LsuClass:
                        lsuClassMask |= slotBit;
                        break;
                    case SlotClass.DmaStreamClass:
                        dmaStreamClassMask |= slotBit;
                        break;
                    case SlotClass.BranchControl:
                        branchControlMask |= slotBit;
                        break;
                    case SlotClass.SystemSingleton:
                        systemSingletonMask |= slotBit;
                        break;
                    default:
                        unclassifiedMask |= slotBit;
                        break;
                }

                if (IsPinnedSlotClass(slotClass))
                {
                    pinnedSlotMask |= slotBit;
                    flags |= DecodedBundleFlags.HasPinnedOps;
                }
                else
                {
                    flexibleSlotMask |= slotBit;
                }

                if (instruction.Class == InstructionClass.ControlFlow)
                    flags |= DecodedBundleFlags.HasControlFlow;

                if (IsMemoryLikeClass(instruction.Class))
                    flags |= DecodedBundleFlags.HasMemoryOps;

                if (MayWriteArchitecturalRegister(instruction))
                    flags |= DecodedBundleFlags.HasRegisterWrites;
            }

            DecodedBundleTypedSlotFacts typedSlotFacts = new DecodedBundleTypedSlotFacts(
                aluClassMask,
                lsuClassMask,
                dmaStreamClassMask,
                branchControlMask,
                systemSingletonMask,
                unclassifiedMask,
                pinnedSlotMask,
                flexibleSlotMask);

            DecodedBundleDependencySummary? dependencySummary = occupiedSlotMask != 0
                ? BuildDependencySummary(slotDescriptors, readRegisterMask, writeRegisterMask, aggregateResourceMask)
                : null;
            DecodedSlotLegality[] slotLegalities = BuildSlotLegalities(slotDescriptors, dependencySummary);
            byte maxVirtualThreadSpan = 0;
            if (hasNonEmptySlot)
            {
                maxVirtualThreadSpan = (byte)(maxVirtualThreadId - minVirtualThreadId + 1);
                if (maxVirtualThreadSpan > 1)
                    flags |= DecodedBundleFlags.HasCrossThreadSpan;
            }

            return new BundleLegalityDescriptor(
                bundle.BundleAddress,
                bundle.BundleSerial,
                occupiedSlotMask,
                typedSlotFacts,
                flags,
                maxVirtualThreadSpan,
                slotLegalities,
                dependencySummary);
        }

        internal static DecodedBundleDependencySummary BuildDependencySummary(
            IReadOnlyList<DecodedBundleSlotDescriptor> slots,
            ulong readRegisterMask,
            ulong writeRegisterMask,
            ResourceBitset aggregateResourceMask)
        {
            ulong rawDependencyMask = 0;
            ulong wawDependencyMask = 0;
            ulong warDependencyMask = 0;
            ulong controlConflictMask = 0;
            ulong memoryConflictMask = 0;
            ulong systemBarrierConflictMask = 0;
            ulong pinnedLaneConflictMask = 0;
            byte scalarClusterEligibleMask = 0;

            for (int slotIndex = 0; slotIndex < BundleWidth; slotIndex++)
            {
                DecodedBundleSlotDescriptor slot = slots[slotIndex];
                if (IsScalarClusterEligible(slot))
                {
                    scalarClusterEligibleMask |= (byte)(1 << slotIndex);
                }

                if (slot.IsEmptyOrNop)
                    continue;

                for (int dependentSlotIndex = slotIndex + 1; dependentSlotIndex < BundleWidth; dependentSlotIndex++)
                {
                    DecodedBundleSlotDescriptor dependentSlot = slots[dependentSlotIndex];
                    if (dependentSlot.IsEmptyOrNop)
                        continue;

                    if (DecodedBundleSlotDescriptor.HasRegisterIntersection(
                        slot,
                        dependentSlot,
                        slot.WriteRegisters,
                        dependentSlot.ReadRegisters))
                    {
                        rawDependencyMask |= EncodeSlotPair(slotIndex, dependentSlotIndex);
                    }

                    if (DecodedBundleSlotDescriptor.HasRegisterIntersection(
                        slot,
                        dependentSlot,
                        slot.WriteRegisters,
                        dependentSlot.WriteRegisters))
                    {
                        wawDependencyMask |= EncodeSlotPair(slotIndex, dependentSlotIndex);
                    }

                    if (DecodedBundleSlotDescriptor.HasRegisterIntersection(
                        slot,
                        dependentSlot,
                        slot.ReadRegisters,
                        dependentSlot.WriteRegisters))
                    {
                        warDependencyMask |= EncodeSlotPair(slotIndex, dependentSlotIndex);
                    }

                    if (HasControlConflict(slot, dependentSlot))
                    {
                        controlConflictMask |= EncodeSlotPair(slotIndex, dependentSlotIndex);

                        if (HasSystemBarrierConflict(slot, dependentSlot))
                        {
                            systemBarrierConflictMask |= EncodeSlotPair(slotIndex, dependentSlotIndex);
                        }
                    }

                    if (HasCoarseMemoryConflict(slot, dependentSlot))
                    {
                        memoryConflictMask |= EncodeSlotPair(slotIndex, dependentSlotIndex);
                    }

                    if (HasPinnedLaneConflict(slot, dependentSlot))
                    {
                        pinnedLaneConflictMask |= EncodeSlotPair(slotIndex, dependentSlotIndex);
                    }
                }
            }

            return new DecodedBundleDependencySummary(
                readRegisterMask,
                writeRegisterMask,
                aggregateResourceMask,
                rawDependencyMask,
                wawDependencyMask,
                warDependencyMask,
                controlConflictMask,
                memoryConflictMask,
                scalarClusterEligibleMask,
                systemBarrierConflictMask,
                pinnedLaneConflictMask);
        }

        internal static SlotClass ClassifySlotClass(InstructionClass instructionClass)
        {
            return instructionClass switch
            {
                InstructionClass.ScalarAlu => SlotClass.AluClass,
                InstructionClass.Memory => SlotClass.LsuClass,
                InstructionClass.Atomic => SlotClass.LsuClass,
                InstructionClass.ControlFlow => SlotClass.BranchControl,
                InstructionClass.System => SlotClass.SystemSingleton,
                InstructionClass.Csr => SlotClass.SystemSingleton,
                InstructionClass.SmtVt => SlotClass.SystemSingleton,
                InstructionClass.Vmx => SlotClass.SystemSingleton,
                _ => SlotClass.Unclassified,
            };
        }

        internal static SlotPlacementMetadata BuildCanonicalPlacement(
            InstructionClass instructionClass,
            ulong domainTag = 0)
        {
            SlotClass slotClass = ClassifySlotClass(instructionClass);
            SlotPinningKind pinningKind = IsPinnedSlotClass(slotClass)
                ? SlotPinningKind.HardPinned
                : SlotPinningKind.ClassFlexible;

            return new SlotPlacementMetadata
            {
                RequiredSlotClass = slotClass,
                PinningKind = pinningKind,
                PinnedLaneId = ResolvePinnedLaneId(slotClass, pinningKind),
                DomainTag = domainTag
            };
        }

        private static SlotClass ClassifySlotClass(InstructionIR instruction)
        {
            return ClassifySlotClass(instruction.Class);
        }

        private static DecodedBundleSlotDescriptor CreateEmptySlotDescriptor(int slotIndex)
        {
            SlotPlacementMetadata placement = new SlotPlacementMetadata
            {
                RequiredSlotClass = SlotClass.Unclassified,
                PinningKind = SlotPinningKind.HardPinned,
                PinnedLaneId = 0,
                DomainTag = 0
            };

            return new DecodedBundleSlotDescriptor(
                microOp: null!,
                slotIndex: (byte)slotIndex,
                virtualThreadId: 0,
                ownerThreadId: 0,
                opCode: 0,
                readRegisters: Array.Empty<int>(),
                writeRegisters: Array.Empty<int>(),
                writesRegister: false,
                isMemoryOp: false,
                isControlFlow: false,
                placement: placement,
                memoryBankIntent: -1,
                isFspInjected: false,
                isEmptyOrNop: true,
                isVectorOp: false);
        }

        private static DecodedBundleSlotDescriptor CreateCanonicalSlotDescriptor(
            int slotIndex,
            DecodedInstruction slot,
            InstructionIR instruction,
            SlotClass slotClass)
        {
            bool writesRegister = MayWriteArchitecturalRegister(instruction);
            bool isMemoryOp = IsMemoryLikeClass(instruction.Class);
            bool isControlFlow = instruction.Class == InstructionClass.ControlFlow;
            SlotPlacementMetadata placement = BuildCanonicalPlacement(
                instruction.Class,
                domainTag: 0);

            return new DecodedBundleSlotDescriptor(
                microOp: null!,
                slotIndex: (byte)slotIndex,
                virtualThreadId: slot.SlotMetadata.VirtualThreadId,
                ownerThreadId: 0,
                opCode: (uint)instruction.CanonicalOpcode,
                readRegisters: GetCanonicalReadRegisters(instruction),
                writeRegisters: GetCanonicalWriteRegisters(instruction, writesRegister),
                writesRegister: writesRegister,
                isMemoryOp: isMemoryOp,
                isControlFlow: isControlFlow,
                placement: placement,
                memoryBankIntent: isMemoryOp ? -1 : -1,
                isFspInjected: false,
                isEmptyOrNop: false,
                isVectorOp: Arch.OpcodeRegistry.IsVectorOp((uint)instruction.CanonicalOpcode));
        }

        private static void AccumulateDependencyInputs(
            in DecodedBundleSlotDescriptor slot,
            ref ulong readRegisterMask,
            ref ulong writeRegisterMask,
            ref ResourceBitset aggregateResourceMask)
        {
            for (int i = 0; i < slot.ReadRegisters.Count; i++)
            {
                int registerId = slot.ReadRegisters[i];
                if ((uint)registerId < 64)
                    readRegisterMask |= 1UL << registerId;

                aggregateResourceMask |= ResourceMaskBuilder.ForRegisterRead(registerId);
            }

            for (int i = 0; i < slot.WriteRegisters.Count; i++)
            {
                int registerId = slot.WriteRegisters[i];
                if ((uint)registerId < 64)
                    writeRegisterMask |= 1UL << registerId;

                aggregateResourceMask |= ResourceMaskBuilder.ForRegisterWrite(registerId);
            }

            aggregateResourceMask |= BuildStructuralResourceMask(slot);
        }

        private static DecodedSlotLegality[] BuildSlotLegalities(
            IReadOnlyList<DecodedBundleSlotDescriptor> slotDescriptors,
            DecodedBundleDependencySummary? dependencySummary)
        {
            var slotLegalities = new DecodedSlotLegality[BundleWidth];
            byte scalarGroupMask = dependencySummary?.ScalarClusterEligibleMask ?? 0;

            for (int slotIndex = 0; slotIndex < slotLegalities.Length; slotIndex++)
            {
                DecodedBundleSlotDescriptor slot = slotDescriptors[slotIndex];
                if (slot.IsEmptyOrNop)
                {
                    slotLegalities[slotIndex] = DecodedSlotLegality.CreateEmpty(slotIndex);
                    continue;
                }

                if (dependencySummary.HasValue)
                {
                    SlotHazardQueryResult query = dependencySummary.Value.QuerySlotHazards(
                        (byte)slotIndex,
                        scalarGroupMask);

                    slotLegalities[slotIndex] = DecodedSlotLegality.CreateOccupied(
                        slotIndex,
                        slot.Placement.RequiredSlotClass,
                        query.HardRejectPeers,
                        query.NeedsCheckPeers,
                        query.DominantEffectKind);
                }
                else
                {
                    slotLegalities[slotIndex] = DecodedSlotLegality.CreateOccupied(
                        slotIndex,
                        slot.Placement.RequiredSlotClass);
                }
            }

            return slotLegalities;
        }

        private static bool IsPinnedSlotClass(SlotClass slotClass)
        {
            return slotClass == SlotClass.BranchControl
                || slotClass == SlotClass.SystemSingleton;
        }

        internal static bool IsMemoryLikeClass(InstructionClass instructionClass)
        {
            return instructionClass == InstructionClass.Memory
                || instructionClass == InstructionClass.Atomic;
        }

        internal static bool MayWriteArchitecturalRegister(InstructionIR instruction)
        {
            if (IsScalarRegisterFreeVectorAluContour(instruction))
                return false;

            if (instruction.Rd == ArchRegisterTripletEncoding.NoArchReg || instruction.Rd == 0)
                return false;

            if (instruction.Class == InstructionClass.ControlFlow &&
                InstructionRegistry.TryCreatePublishedControlFlowMicroOp(in instruction, out BranchMicroOp? branchMicroOp) &&
                branchMicroOp is not null)
            {
                return branchMicroOp.WritesRegister;
            }

            if (instruction.Class == InstructionClass.Vmx &&
                InstructionRegistry.TryResolvePublishedVmxOperationKind(in instruction, out VmxOperationKind vmxOperationKind))
            {
                return vmxOperationKind == VmxOperationKind.VmRead;
            }

            OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo((uint)instruction.CanonicalOpcode);
            if (opcodeInfo.HasValue)
            {
                return instruction.Class switch
                {
                    InstructionClass.ScalarAlu => true,
                    InstructionClass.Atomic => true,
                    InstructionClass.Csr => true,
                    InstructionClass.System => opcodeInfo.Value.IsVector,
                    InstructionClass.Vmx => false,
                    InstructionClass.Memory => opcodeInfo.Value.SerializationClass != SerializationClass.MemoryOrdered,
                    InstructionClass.ControlFlow => false,
                    _ => false,
                };
            }

            return instruction.Class switch
            {
                InstructionClass.ScalarAlu => true,
                InstructionClass.Atomic => true,
                InstructionClass.Csr => true,
                InstructionClass.System => false,
                InstructionClass.Vmx => false,
                InstructionClass.Memory => instruction.SerializationClass != SerializationClass.MemoryOrdered,
                InstructionClass.ControlFlow => false,
                _ => false,
            };
        }

        internal static IReadOnlyList<int> GetCanonicalReadRegisters(InstructionIR instruction)
        {
            if (IsScalarRegisterFreeVectorAluContour(instruction))
                return Array.Empty<int>();

            if (instruction.Class == InstructionClass.Csr &&
                TryResolvePublishedCsrReadRegisters(in instruction, out IReadOnlyList<int>? csrReadRegisters))
            {
                return csrReadRegisters;
            }

            if (instruction.Class == InstructionClass.System &&
                TryResolvePublishedSystemReadRegisters(in instruction, out IReadOnlyList<int>? readRegisters))
            {
                return readRegisters;
            }

            bool readsRs1 = ReadsRs1(instruction);
            bool readsRs2 = ReadsRs2(instruction);
            return BuildRegisterList(
                readsRs1 ? instruction.Rs1 : (byte)0,
                readsRs2 ? instruction.Rs2 : (byte)0);
        }

        internal static IReadOnlyList<int> GetCanonicalWriteRegisters(InstructionIR instruction, bool writesRegister)
        {
            if (IsScalarRegisterFreeVectorAluContour(instruction))
                return Array.Empty<int>();

            return writesRegister
                ? BuildRegisterList(instruction.Rd)
                : Array.Empty<int>();
        }

        private static IReadOnlyList<int> BuildRegisterList(byte first, byte second = 0)
        {
            bool hasFirst = HasArchitecturalRegister(first);
            bool hasSecond = HasArchitecturalRegister(second);

            if (hasFirst && hasSecond)
                return new[] { (int)first, (int)second };
            if (hasFirst)
                return new[] { (int)first };
            if (hasSecond)
                return new[] { (int)second };

            return Array.Empty<int>();
        }

        private static bool TryResolvePublishedSystemReadRegisters(
            in InstructionIR instruction,
            out IReadOnlyList<int> readRegisters)
        {
            return InstructionRegistry.TryResolvePublishedSystemReadRegisters(
                in instruction,
                out readRegisters);
        }

        private static bool TryResolvePublishedCsrReadRegisters(
            in InstructionIR instruction,
            out IReadOnlyList<int> readRegisters)
        {
            readRegisters = Array.Empty<int>();

            if (instruction.Class != InstructionClass.Csr)
            {
                return false;
            }

            if (!InstructionRegistry.TryCreatePublishedCsrMicroOp(in instruction, out CSRMicroOp? csrMicroOp) ||
                csrMicroOp is null)
            {
                return false;
            }

            readRegisters = csrMicroOp.ReadRegisters;
            return true;
        }

        private static bool HasArchitecturalRegister(byte registerId)
        {
            return registerId != 0 && registerId != ArchRegisterTripletEncoding.NoArchReg;
        }

        private static bool ContainsArchitecturalRegister(IReadOnlyList<int> registers, byte registerId)
        {
            if (!HasArchitecturalRegister(registerId))
                return false;

            for (int i = 0; i < registers.Count; i++)
            {
                if (registers[i] == registerId)
                    return true;
            }

            return false;
        }

        private static bool ReadsRs1(InstructionIR instruction)
        {
            if (IsScalarRegisterFreeVectorAluContour(instruction))
                return false;

            if (instruction.Class == InstructionClass.ControlFlow &&
                InstructionRegistry.TryCreatePublishedControlFlowMicroOp(in instruction, out BranchMicroOp? branchMicroOp) &&
                branchMicroOp is not null)
            {
                return ContainsArchitecturalRegister(branchMicroOp.ReadRegisters, instruction.Rs1);
            }

            if (instruction.Class == InstructionClass.Vmx &&
                InstructionRegistry.TryResolvePublishedVmxOperationKind(in instruction, out VmxOperationKind vmxOperationKind))
            {
                return HasArchitecturalRegister(instruction.Rs1) &&
                       vmxOperationKind is
                           VmxOperationKind.VmRead or
                           VmxOperationKind.VmWrite or
                           VmxOperationKind.VmClear or
                           VmxOperationKind.VmPtrLd;
            }

            OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo((uint)instruction.CanonicalOpcode);
            if (opcodeInfo.HasValue)
            {
                return instruction.Class switch
                {
                    InstructionClass.Memory => true,
                    InstructionClass.Atomic => opcodeInfo.Value.OperandCount != 0,
                    InstructionClass.ControlFlow => HasArchitecturalRegister(instruction.Rs1),
                    InstructionClass.Csr => opcodeInfo.Value.OperandCount == 2,
                    InstructionClass.ScalarAlu => opcodeInfo.Value.OperandCount > 1,
                    _ => HasArchitecturalRegister(instruction.Rs1),
                };
            }

            return instruction.Class switch
            {
                InstructionClass.Memory => HasArchitecturalRegister(instruction.Rs1),
                InstructionClass.Atomic => true,
                InstructionClass.ControlFlow => true,
                InstructionClass.Csr => false,
                InstructionClass.System => false,
                InstructionClass.SmtVt => false,
                InstructionClass.ScalarAlu => HasArchitecturalRegister(instruction.Rs1),
                _ => HasArchitecturalRegister(instruction.Rs1),
            };
        }

        private static bool ReadsRs2(InstructionIR instruction)
        {
            if (IsScalarRegisterFreeVectorAluContour(instruction))
                return false;

            if (instruction.Class == InstructionClass.ControlFlow &&
                InstructionRegistry.TryCreatePublishedControlFlowMicroOp(in instruction, out BranchMicroOp? branchMicroOp) &&
                branchMicroOp is not null)
            {
                return ContainsArchitecturalRegister(branchMicroOp.ReadRegisters, instruction.Rs2);
            }

            if (instruction.Class == InstructionClass.Vmx &&
                InstructionRegistry.TryResolvePublishedVmxOperationKind(in instruction, out VmxOperationKind vmxOperationKind))
            {
                return vmxOperationKind == VmxOperationKind.VmWrite &&
                       HasArchitecturalRegister(instruction.Rs2);
            }

            OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo((uint)instruction.CanonicalOpcode);
            if (opcodeInfo.HasValue)
            {
                return instruction.Class switch
                {
                    InstructionClass.Memory => opcodeInfo.Value.SerializationClass == SerializationClass.MemoryOrdered,
                    InstructionClass.Atomic => opcodeInfo.Value.OperandCount > 1,
                    InstructionClass.ControlFlow => HasArchitecturalRegister(instruction.Rs2),
                    InstructionClass.Csr => false,
                    InstructionClass.System => false,
                    InstructionClass.SmtVt => false,
                    InstructionClass.ScalarAlu => (opcodeInfo.Value.Flags & InstructionFlags.UsesImmediate) == 0,
                    _ => HasArchitecturalRegister(instruction.Rs2),
                };
            }

            return instruction.Class switch
            {
                InstructionClass.Memory => HasArchitecturalRegister(instruction.Rs2),
                InstructionClass.Atomic => true,
                InstructionClass.ControlFlow => true,
                InstructionClass.Csr => false,
                InstructionClass.System => false,
                InstructionClass.SmtVt => false,
                InstructionClass.ScalarAlu => HasArchitecturalRegister(instruction.Rs2),
                _ => HasArchitecturalRegister(instruction.Rs2),
            };
        }

        private static bool IsScalarRegisterFreeVectorAluContour(InstructionIR instruction)
        {
            if (instruction.Class != InstructionClass.ScalarAlu)
                return false;

            OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo((uint)instruction.CanonicalOpcode);
            if (opcodeInfo.HasValue)
            {
                InstructionFlags flags = opcodeInfo.Value.Flags;
                bool isScalarResultMaskPopContour =
                    (flags & InstructionFlags.MaskManipulation) != 0 &&
                    (flags & InstructionFlags.Reduction) != 0;

                return opcodeInfo.Value.IsVector && !isScalarResultMaskPopContour;
            }

            return OpcodeRegistry.IsVectorOp((uint)instruction.CanonicalOpcode);
        }

        private static ResourceBitset BuildStructuralResourceMask(in DecodedBundleSlotDescriptor slot)
        {
            if (!slot.IsMemoryOp)
                return ResourceBitset.Zero;

            OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo(slot.OpCode);
            if (opcodeInfo.HasValue)
            {
                return opcodeInfo.Value.InstructionClass switch
                {
                    InstructionClass.Atomic => ResourceMaskBuilder.ForAtomic(),
                    InstructionClass.Memory when opcodeInfo.Value.SerializationClass == SerializationClass.MemoryOrdered
                        => ResourceMaskBuilder.ForStore(),
                    _ => ResourceMaskBuilder.ForLoad(),
                };
            }

            return slot.OpCode switch
            {
                (uint)InstructionsEnum.Store => ResourceMaskBuilder.ForStore(),
                (uint)InstructionsEnum.MTILE_STORE => ResourceMaskBuilder.ForStore(),
                _ => ResourceMaskBuilder.ForLoad(),
            };
        }

        private static byte ResolvePinnedLaneId(SlotClass slotClass, SlotPinningKind pinningKind)
        {
            if (pinningKind != SlotPinningKind.HardPinned)
                return 0;

            return slotClass switch
            {
                SlotClass.BranchControl => 7,
                SlotClass.SystemSingleton => 7,
                _ => 0,
            };
        }

        private static ulong EncodeSlotPair(int sourceSlotIndex, int targetSlotIndex)
        {
            return 1UL << ((sourceSlotIndex * BundleWidth) + targetSlotIndex);
        }

        private static bool HasControlConflict(DecodedBundleSlotDescriptor left, DecodedBundleSlotDescriptor right)
        {
            return RequiresSingletonLane(left.Placement.RequiredSlotClass) && RequiresSingletonLane(right.Placement.RequiredSlotClass);
        }

        private static bool HasSystemBarrierConflict(DecodedBundleSlotDescriptor left, DecodedBundleSlotDescriptor right)
        {
            return HasControlConflict(left, right)
                && (left.Placement.RequiredSlotClass == SlotClass.SystemSingleton || right.Placement.RequiredSlotClass == SlotClass.SystemSingleton);
        }

        private static bool HasCoarseMemoryConflict(DecodedBundleSlotDescriptor left, DecodedBundleSlotDescriptor right)
        {
            if (!left.IsMemoryOp || !right.IsMemoryOp)
                return false;

            if (left.MemoryBankIntent < 0 || right.MemoryBankIntent < 0)
                return true;

            return left.MemoryBankIntent == right.MemoryBankIntent;
        }

        private static bool HasPinnedLaneConflict(DecodedBundleSlotDescriptor left, DecodedBundleSlotDescriptor right)
        {
            return left.Placement.PinningKind == SlotPinningKind.HardPinned
                && right.Placement.PinningKind == SlotPinningKind.HardPinned
                && left.Placement.PinnedLaneId == right.Placement.PinnedLaneId;
        }

        private static bool IsScalarClusterEligible(DecodedBundleSlotDescriptor slot)
        {
            return !slot.IsEmptyOrNop
                && !slot.IsMemoryOp
                && !slot.IsControlFlow
                && !slot.IsVectorOp
                && slot.Placement.RequiredSlotClass == SlotClass.AluClass;
        }

        private static bool RequiresSingletonLane(SlotClass slotClass)
        {
            return slotClass is SlotClass.BranchControl or SlotClass.SystemSingleton;
        }
    }
}
