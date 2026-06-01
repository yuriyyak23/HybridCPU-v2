using HybridCPU_ISE.Arch;
using System;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace YAKSys_Hybrid_CPU.Core.Decoder
{
    /// <summary>
    /// Canonical decode-to-runtime transport projector.
    /// Builds the published decoded-bundle transport facts directly from canonical
    /// decode outputs so the main runtime path no longer depends on a legacy
    /// slot-carrier materializer ABI.
    /// </summary>
    internal static class DecodedBundleTransportProjector
    {
        internal static DecodedBundleTransportFacts BuildCanonicalTransportFacts(
            ReadOnlySpan<VLIW_Instruction> rawSlots,
            DecodedInstructionBundle canonicalBundle,
            DecodedBundleDependencySummary? dependencySummary)
        {
            ArgumentNullException.ThrowIfNull(canonicalBundle);

            MicroOp?[] carrierBundle = ProjectCanonicalCarrierBundle(rawSlots, canonicalBundle);
            return DecodedBundleSlotCarrierBuilder.BuildTransportFacts(
                canonicalBundle.BundleAddress,
                carrierBundle,
                canonicalBundle,
                dependencySummary);
        }

        internal static MicroOp?[] BuildCanonicalCarrierBundleForTesting(
            ReadOnlySpan<VLIW_Instruction> rawSlots,
            DecodedInstructionBundle canonicalBundle)
        {
            ArgumentNullException.ThrowIfNull(canonicalBundle);
            return ProjectCanonicalCarrierBundle(rawSlots, canonicalBundle);
        }

        internal static DecodedBundleTransportFacts BuildFallbackTransportFacts(
            ReadOnlySpan<VLIW_Instruction> rawSlots,
            VliwDecoderV4 slotDecoder,
            InvalidOpcodeException bundleDecodeException,
            ulong bundlePc,
            VliwBundleAnnotations? bundleAnnotations = null)
        {
            MicroOp?[] carrierBundle = ProjectFallbackCarrierBundle(
                rawSlots,
                slotDecoder,
                bundleDecodeException,
                bundlePc,
                bundleAnnotations);
            return DecodedBundleSlotCarrierBuilder.BuildTransportFacts(
                bundlePc,
                carrierBundle,
                Core.DecodedBundleStateKind.DecodeFault,
                Core.DecodedBundleStateOrigin.DecodeFallbackTrap);
        }

        internal static MicroOp?[] BuildFallbackCarrierBundleForTesting(
            ReadOnlySpan<VLIW_Instruction> rawSlots,
            IDecoderFrontend slotDecoder,
            InvalidOpcodeException bundleDecodeException,
            ulong bundlePc,
            VliwBundleAnnotations? bundleAnnotations = null)
        {
            return ProjectFallbackCarrierBundle(
                rawSlots,
                slotDecoder,
                bundleDecodeException,
                bundlePc,
                bundleAnnotations);
        }

        private static MicroOp?[] ProjectCanonicalCarrierBundle(
            ReadOnlySpan<VLIW_Instruction> rawSlots,
            DecodedInstructionBundle canonicalBundle)
        {
            var carrierBundle = new MicroOp?[Core.BundleMetadata.BundleSlotCount];
            for (int slotIndex = 0; slotIndex < Core.BundleMetadata.BundleSlotCount; slotIndex++)
            {
                DecodedInstruction decodedSlot = canonicalBundle.GetDecodedSlot(slotIndex);
                ref readonly VLIW_Instruction rawInstruction = ref rawSlots[slotIndex];

                carrierBundle[slotIndex] = decodedSlot.IsOccupied
                    ? CreateFromDecodedInstruction(
                        in rawInstruction,
                        decodedSlot,
                        canonicalBundle.BundleAddress)
                    : CreateEmptySlot(in rawInstruction, decodedSlot.SlotMetadata);
            }

            return carrierBundle;
        }

        private static MicroOp?[] ProjectFallbackCarrierBundle(
            ReadOnlySpan<VLIW_Instruction> rawSlots,
            IDecoderFrontend slotDecoder,
            InvalidOpcodeException bundleDecodeException,
            ulong bundlePc,
            VliwBundleAnnotations? bundleAnnotations = null)
        {
            var carrierBundle = new MicroOp?[Core.BundleMetadata.BundleSlotCount];
            for (byte slotIndex = 0; slotIndex < Core.BundleMetadata.BundleSlotCount; slotIndex++)
            {
                ref readonly VLIW_Instruction rawInstruction = ref rawSlots[slotIndex];
                InstructionSlotMetadata slotMetadata =
                    ResolveFallbackSlotMetadata(bundleAnnotations, slotIndex);
                MicroOp slotCarrierMicroOp;
                if (rawInstruction.OpCode == 0)
                {
                    slotCarrierMicroOp = CreateEmptySlot(in rawInstruction, slotMetadata);
                }
                else
                {
                    InstructionIR instruction;
                    try
                    {
                        instruction = slotDecoder.Decode(in rawInstruction, slotIndex);
                    }
                    catch (InvalidOpcodeException slotDecodeException)
                    {
                        Exception effectiveException = bundleDecodeException.SlotIndex == slotIndex
                            ? bundleDecodeException
                            : slotDecodeException;

                        slotCarrierMicroOp = CreateTrapMicroOp(
                            rawInstruction.OpCode,
                            BuildTrapReason(effectiveException),
                            slotMetadata);
                        carrierBundle[slotIndex] = slotCarrierMicroOp;
                        continue;
                    }

                    slotCarrierMicroOp = CreateFromInstruction(
                        in rawInstruction,
                        in instruction,
                        slotMetadata,
                        bundlePc);
                }

                carrierBundle[slotIndex] = slotCarrierMicroOp;
            }

            return carrierBundle;
        }

        private static MicroOp CreateEmptySlot(
            in VLIW_Instruction rawInstruction,
            InstructionSlotMetadata slotMetadata = default)
        {
            var nopMicroOp = new NopMicroOp
            {
                OpCode = rawInstruction.OpCode
            };

            ApplyCanonicalExecutionOwnerProjection(nopMicroOp, slotMetadata);
            ApplyCanonicalCompatibilitySlotMetadataProjection(nopMicroOp, slotMetadata);
            return nopMicroOp;
        }

        private static MicroOp CreateFromDecodedInstruction(
            in VLIW_Instruction rawInstruction,
            DecodedInstruction decodedInstruction,
            ulong bundlePc)
        {
            InstructionIR instruction = decodedInstruction.RequireInstruction();
            return CreateFromInstruction(
                in rawInstruction,
                in instruction,
                decodedInstruction.SlotMetadata,
                bundlePc);
        }

        private static MicroOp CreateFromInstruction(
            in VLIW_Instruction rawInstruction,
            in InstructionIR instruction,
            InstructionSlotMetadata slotMetadata = default,
            ulong? bundlePc = null)
        {
            uint opCode = (uint)instruction.CanonicalOpcode;
            int projectedTrapMemoryBankIntent =
                ResolveProjectedTrapMemoryBankIntent(in rawInstruction, in instruction);
            if (instruction.DmaStreamComputeDescriptor is not null)
            {
                if (opCode != Processor.CPU_Core.IsaOpcodeValues.DmaStreamCompute)
                {
                    return CreateCanonicalTrapMicroOp(
                        opCode,
                        in instruction,
                        "DmaStreamCompute descriptor sideband reached projector on a non-DmaStreamCompute opcode.",
                        slotMetadata,
                        projectedTrapMemoryBankIntent);
                }

                var dmaStreamComputeMicroOp = new DmaStreamComputeMicroOp(
                    instruction.DmaStreamComputeDescriptor);
                ApplyCanonicalCompatibilitySlotMetadataProjection(
                    dmaStreamComputeMicroOp,
                    slotMetadata);
                return dmaStreamComputeMicroOp;
            }

            if (instruction.DmaStreamComputeDescriptorReference.HasValue)
            {
                return CreateCanonicalTrapMicroOp(
                    opCode,
                    in instruction,
                    "DmaStreamCompute descriptor reference reached projector without the guard-accepted descriptor payload.",
                    slotMetadata,
                    projectedTrapMemoryBankIntent);
            }

            if (instruction.AcceleratorCommandDescriptor is not null)
            {
                if (opCode != Processor.CPU_Core.IsaOpcodeValues.ACCEL_SUBMIT)
                {
                    return CreateCanonicalTrapMicroOp(
                        opCode,
                        in instruction,
                        "AcceleratorCommandDescriptor sideband reached projector on a non-ACCEL_SUBMIT opcode.",
                        slotMetadata,
                        projectedTrapMemoryBankIntent);
                }

                if (!AcceleratorOwnerDomainGuard.Default.IsDescriptorGuardBacked(
                        instruction.AcceleratorCommandDescriptor,
                        out string guardMessage))
                {
                    return CreateCanonicalTrapMicroOp(
                        opCode,
                        in instruction,
                        "AcceleratorCommandDescriptor reached projector without guard-backed owner/domain acceptance. " +
                        guardMessage,
                        slotMetadata,
                        projectedTrapMemoryBankIntent);
                }

                AcceleratorGuardDecision submitGuard =
                    AcceleratorOwnerDomainGuard.Default.EnsureBeforeSubmit(
                        instruction.AcceleratorCommandDescriptor,
                        instruction.AcceleratorCommandDescriptor.OwnerGuardDecision.Evidence);
                if (!submitGuard.IsAllowed)
                {
                    return CreateCanonicalTrapMicroOp(
                        opCode,
                        in instruction,
                        "ACCEL_SUBMIT admission guard rejected before carrier materialization. " +
                        submitGuard.Message,
                        slotMetadata,
                        projectedTrapMemoryBankIntent);
                }

                var submitMicroOp = new AcceleratorSubmitMicroOp(
                    ToLegacyDecoderField(instruction.Rd),
                    instruction.AcceleratorCommandDescriptor);
                ApplyCanonicalExecutionOwnerProjection(submitMicroOp, slotMetadata);
                submitMicroOp.InitializeMetadata();
                ApplyCanonicalCompatibilitySlotMetadataProjection(
                    submitMicroOp,
                    slotMetadata);
                return submitMicroOp;
            }

            if (instruction.AcceleratorCommandDescriptorReference.HasValue)
            {
                return CreateCanonicalTrapMicroOp(
                    opCode,
                    in instruction,
                    "AcceleratorCommandDescriptor reference reached projector without the validated descriptor payload.",
                    slotMetadata,
                    projectedTrapMemoryBankIntent);
            }

            if (!InstructionRegistry.IsRegistered(opCode))
            {
                return CreateCanonicalTrapMicroOp(
                    opCode,
                    in instruction,
                    slotMetadata: slotMetadata,
                    projectedMemoryBankIntent: projectedTrapMemoryBankIntent);
            }

            try
            {
                VLIW_Instruction contextInstruction =
                    ProjectCanonicalMaterializationInstruction(
                        in rawInstruction,
                        in instruction,
                        bundlePc);
                VectorInstructionPayload? vectorPayload = instruction.VectorPayload;
                bool requiresVectorPayload = OpcodeRegistry.RequiresVectorPayloadProjection(opCode);
                byte contextDataType = vectorPayload?.DataType ?? contextInstruction.DataType;
                bool indexedAddressing = vectorPayload?.Indexed ?? contextInstruction.Indexed;
                bool is2DAddressing = vectorPayload?.Is2D ?? contextInstruction.Is2D;
                ulong vectorPrimaryPointer = vectorPayload?.PrimaryPointer ?? contextInstruction.DestSrc1Pointer;
                ulong vectorSecondaryPointer = vectorPayload?.SecondaryPointer ?? contextInstruction.Src2Pointer;
                uint vectorStreamLength = vectorPayload?.StreamLength ?? contextInstruction.StreamLength;
                ushort vectorStride = vectorPayload?.Stride ?? contextInstruction.Stride;
                ushort vectorRowStride = vectorPayload?.RowStride ?? contextInstruction.RowStride;
                bool tailAgnostic = vectorPayload?.TailAgnostic ?? contextInstruction.TailAgnostic;
                bool maskAgnostic = vectorPayload?.MaskAgnostic ?? contextInstruction.MaskAgnostic;
                bool saturating = vectorPayload?.Saturating ?? contextInstruction.Saturating;
                byte predicateMask = vectorPayload?.PredicateMask ?? rawInstruction.PredicateMask;
                var context = new DecoderContext
                {
                    OpCode = opCode,
                    Immediate = contextInstruction.Immediate,
                    HasImmediate = true,
                    DataType = contextDataType,
                    HasDataType = true,
                    IndexedAddressing = indexedAddressing,
                    Is2DAddressing = is2DAddressing,
                    HasVectorAddressingContour = true,
                    VectorPrimaryPointer = vectorPrimaryPointer,
                    VectorSecondaryPointer = vectorSecondaryPointer,
                    VectorStreamLength = vectorStreamLength,
                    VectorStride = vectorStride,
                    VectorRowStride = vectorRowStride,
                    TailAgnostic = tailAgnostic,
                    MaskAgnostic = maskAgnostic,
                    Saturating = saturating,
                    HasVectorPayload = requiresVectorPayload,
                    MemoryAddress = instruction.HasAbsoluteAddressing ? (ulong)instruction.Imm : contextInstruction.Src2Pointer,
                    HasMemoryAddress = instruction.HasAbsoluteAddressing,
                    PackedRegisterTriplet = contextInstruction.DestSrc1Pointer,
                    HasPackedRegisterTriplet = true,
                    Reg1ID = ToLegacyDecoderField(instruction.Rd),
                    Reg2ID = ToLegacyDecoderField(instruction.Rs1),
                    Reg3ID = ToLegacyDecoderField(instruction.Rs2),
                    AuxData = ResolveCanonicalAuxData(
                        in instruction,
                        in contextInstruction,
                        bundlePc),
                    PredicateMask = predicateMask,
                    AcquireOrdering = instruction.AcquireOrdering,
                    ReleaseOrdering = instruction.ReleaseOrdering
                };

                MicroOp microOp = InstructionRegistry.CreateMicroOp(opCode, context);
                ApplyCanonicalExecutionOwnerProjection(microOp, slotMetadata);
                RefreshOwnerDependentMaterializedResourceFacts(microOp);
                if (RequiresCanonicalDecodeProjection(microOp, in instruction))
                {
                    bool writesRegister = Core.Legality.BundleLegalityAnalyzer.MayWriteArchitecturalRegister(instruction);
                    microOp.ApplyCanonicalDecodeProjection(
                        instruction.Class,
                        instruction.SerializationClass,
                        Core.Legality.BundleLegalityAnalyzer.BuildCanonicalPlacement(
                            instruction.Class,
                            microOp.Placement.DomainTag),
                        Core.Legality.BundleLegalityAnalyzer.IsMemoryLikeClass(instruction.Class),
                        instruction.Class == InstructionClass.ControlFlow,
                        writesRegister,
                        Core.Legality.BundleLegalityAnalyzer.GetCanonicalReadRegisters(instruction),
                        Core.Legality.BundleLegalityAnalyzer.GetCanonicalWriteRegisters(instruction, writesRegister));
                }
                ApplyCanonicalCompatibilitySlotMetadataProjection(microOp, slotMetadata);

                return microOp;
            }
            catch (DecodeProjectionFaultException projectionException)
            {
                return CreateCanonicalTrapMicroOp(
                    opCode,
                    in instruction,
                    BuildTrapReason(projectionException),
                    slotMetadata,
                    projectedTrapMemoryBankIntent);
            }
        }

        private static TrapMicroOp CreateCanonicalTrapMicroOp(
            uint opCode,
            in InstructionIR instruction,
            string? trapReason = null,
            InstructionSlotMetadata slotMetadata = default,
            int projectedMemoryBankIntent = -1)
        {
            TrapMicroOp trapMicroOp = CreateTrapMicroOp(
                opCode,
                trapReason,
                slotMetadata,
                projectedMemoryBankIntent);
            bool writesRegister = Core.Legality.BundleLegalityAnalyzer.MayWriteArchitecturalRegister(instruction);
            trapMicroOp.ApplyCanonicalDecodeProjection(
                instruction.Class,
                instruction.SerializationClass,
                Core.Legality.BundleLegalityAnalyzer.BuildCanonicalPlacement(
                    instruction.Class,
                    trapMicroOp.Placement.DomainTag),
                Core.Legality.BundleLegalityAnalyzer.IsMemoryLikeClass(instruction.Class),
                instruction.Class == InstructionClass.ControlFlow,
                writesRegister,
                Core.Legality.BundleLegalityAnalyzer.GetCanonicalReadRegisters(instruction),
                Core.Legality.BundleLegalityAnalyzer.GetCanonicalWriteRegisters(instruction, writesRegister));
            return trapMicroOp;
        }

        private static TrapMicroOp CreateTrapMicroOp(
            uint opCode,
            string? trapReason = null,
            InstructionSlotMetadata slotMetadata = default,
            int projectedMemoryBankIntent = -1)
        {
            var trapMicroOp = new TrapMicroOp
            {
                OpCode = opCode,
                UndecodedOpCode = opCode,
                TrapReason = trapReason,
                ProjectedMemoryBankIntent = projectedMemoryBankIntent
            };

            ApplyCanonicalExecutionOwnerProjection(trapMicroOp, slotMetadata);
            ApplyCanonicalCompatibilitySlotMetadataProjection(trapMicroOp, slotMetadata);
            return trapMicroOp;
        }

        internal static int ResolveProjectedTrapMemoryBankIntent(
            in VLIW_Instruction rawInstruction,
            in InstructionIR instruction)
        {
            ulong memoryAddress = instruction.Class switch
            {
                InstructionClass.Memory => instruction.HasAbsoluteAddressing
                    ? (ulong)instruction.Imm
                    : (rawInstruction.Src2Pointer != 0
                        ? rawInstruction.Src2Pointer
                        : (ulong)(short)rawInstruction.Immediate),
                InstructionClass.Atomic => rawInstruction.Src2Pointer,
                _ => ulong.MaxValue
            };

            return memoryAddress == ulong.MaxValue
                ? -1
                : (int)((memoryAddress / 4096UL) % 16UL);
        }

        private static VLIW_Instruction ProjectCanonicalMaterializationInstruction(
            in VLIW_Instruction rawInstruction,
            in InstructionIR instruction,
            ulong? bundlePc)
        {
            uint opCode = (uint)instruction.CanonicalOpcode;
            if (instruction.Class == InstructionClass.ControlFlow)
            {
                VLIW_Instruction projectedControlFlowInstruction = rawInstruction;
                projectedControlFlowInstruction.OpCode = opCode;
                projectedControlFlowInstruction.DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                    instruction.Rd,
                    instruction.Rs1,
                    instruction.Rs2);
                projectedControlFlowInstruction.Src2Pointer = 0;

                return projectedControlFlowInstruction;
            }

            // Absolute-memory truth now flows through DecoderContext.MemoryAddress/AuxData.
            // Non-control-flow materialization no longer needs a second raw-instruction
            // rewrite branch for retained absolute Load/Store compatibility contours.
            return rawInstruction;
        }

        private static ulong ResolveCanonicalAuxData(
            in InstructionIR instruction,
            in VLIW_Instruction contextInstruction,
            ulong? bundlePc)
        {
            if (instruction.Class == InstructionClass.ControlFlow &&
                TryResolveCanonicalStaticBranchTarget(
                    in instruction,
                    bundlePc,
                    out ulong targetAddress))
            {
                return targetAddress;
            }

            return instruction.HasAbsoluteAddressing
                ? (ulong)instruction.Imm
                : contextInstruction.Src2Pointer;
        }

        private static bool TryResolveCanonicalStaticBranchTarget(
            in InstructionIR instruction,
            ulong? bundlePc,
            out ulong targetAddress)
        {
            if (instruction.HasAbsoluteAddressing)
            {
                targetAddress = (ulong)instruction.Imm;
                return true;
            }

            if (!bundlePc.HasValue)
            {
                targetAddress = 0;
                return false;
            }

            if (OpcodeRegistry.HasStaticRelativeControlFlowTarget((uint)instruction.CanonicalOpcode))
            {
                targetAddress = unchecked((ulong)((long)bundlePc.Value + instruction.Imm));
                return true;
            }

            targetAddress = 0;
            return false;
        }

        private static bool RequiresCanonicalDecodeProjection(
            MicroOp microOp,
            in InstructionIR instruction)
        {
            return microOp.CanonicalDecodePublication switch
            {
                CanonicalDecodePublicationMode.ProjectorPublishes => true,
                CanonicalDecodePublicationMode.SelfPublishes => false,
                _ => throw new InvalidOperationException(
                    $"Opcode {instruction.CanonicalOpcode} ({instruction.Class}) materialized {microOp.GetType().Name} " +
                    "without an explicit canonical decode publication policy. " +
                    "Declare whether the MicroOp self-publishes canonical facts or requires projector publication.")
            };
        }

        private static string BuildTrapReason(Exception exception)
        {
            if (exception is InvalidOpcodeException invalidOpcodeException)
            {
                string opcodeKind = invalidOpcodeException.IsProhibited
                    ? "Prohibited opcode"
                    : "Invalid opcode";
                return invalidOpcodeException.SlotIndex >= 0
                    ? $"{opcodeKind} at slot {invalidOpcodeException.SlotIndex}: {invalidOpcodeException.Message}"
                    : $"{opcodeKind}: {invalidOpcodeException.Message}";
            }

            if (exception is DecodeProjectionFaultException projectionException)
            {
                return $"Decode projection fault: {projectionException.Message}";
            }

            return $"Decode exception: {exception.Message}";
        }

        private static InstructionSlotMetadata ResolveFallbackSlotMetadata(
            VliwBundleAnnotations? bundleAnnotations,
            int slotIndex)
        {
            if (bundleAnnotations != null &&
                bundleAnnotations.TryGetInstructionSlotMetadata(slotIndex, out InstructionSlotMetadata metadata))
            {
                return metadata;
            }

            return InstructionSlotMetadata.Default;
        }

        private static void RefreshOwnerDependentMaterializedResourceFacts(MicroOp microOp)
        {
            switch (microOp)
            {
                case LoadMicroOp loadMicroOp:
                    loadMicroOp.InitializeMetadata();
                    break;

                case StoreMicroOp storeMicroOp:
                    storeMicroOp.InitializeMetadata();
                    break;

                case AtomicMicroOp atomicMicroOp:
                    atomicMicroOp.InitializeMetadata();
                    break;

                case DmaStreamComputeStatusMicroOp statusMicroOp:
                    statusMicroOp.RefreshOwnerDependentMetadata();
                    break;

                case DmaStreamComputeQueryCapsMicroOp queryCapsMicroOp:
                    queryCapsMicroOp.RefreshOwnerDependentMetadata();
                    break;

                case SystemDeviceCommandMicroOp systemDeviceCommandMicroOp:
                    systemDeviceCommandMicroOp.InitializeMetadata();
                    break;
            }
        }

        private static void ApplyCanonicalExecutionOwnerProjection(
            MicroOp microOp,
            InstructionSlotMetadata slotMetadata)
        {
            int ownerVirtualThreadId = slotMetadata.VirtualThreadId.Value;
            microOp.VirtualThreadId = ownerVirtualThreadId;
            microOp.OwnerThreadId = ownerVirtualThreadId;
            microOp.OwnerContextId = ResolveProjectedOwnerContextId(slotMetadata, ownerVirtualThreadId);

            ulong projectedDomainTag = ResolveProjectedDomainTag(slotMetadata);
            if (projectedDomainTag != microOp.Placement.DomainTag)
            {
                microOp.Placement = microOp.Placement with { DomainTag = projectedDomainTag };
            }
        }

        private static void ApplyCanonicalCompatibilitySlotMetadataProjection(
            MicroOp microOp,
            InstructionSlotMetadata slotMetadata)
        {
            Core.SlotMetadata canonicalSlotMetadata = slotMetadata.SlotMetadata ?? Core.SlotMetadata.Default;
            microOp.IsStealable &= canonicalSlotMetadata.StealabilityPolicy == Core.StealabilityPolicy.Stealable;
            microOp.MemoryLocalityHint = canonicalSlotMetadata.LocalityHint;
            microOp.RefreshAdmissionMetadata();
        }

        private static int ResolveProjectedOwnerContextId(
            InstructionSlotMetadata slotMetadata,
            int ownerVirtualThreadId)
        {
            const int UnsetOwnerContextSentinel = -1;
            MicroOpAdmissionMetadata canonicalAdmissionMetadata =
                (slotMetadata.SlotMetadata ?? Core.SlotMetadata.Default).AdmissionMetadata;

            return canonicalAdmissionMetadata.OwnerContextId != UnsetOwnerContextSentinel
                ? canonicalAdmissionMetadata.OwnerContextId
                : ownerVirtualThreadId;
        }

        private static ulong ResolveProjectedDomainTag(InstructionSlotMetadata slotMetadata)
        {
            MicroOpAdmissionMetadata canonicalAdmissionMetadata =
                (slotMetadata.SlotMetadata ?? Core.SlotMetadata.Default).AdmissionMetadata;

            return canonicalAdmissionMetadata.DomainTag != 0
                ? canonicalAdmissionMetadata.DomainTag
                : canonicalAdmissionMetadata.Placement.DomainTag;
        }

        private static ushort ToLegacyDecoderField(byte regId)
        {
            return regId == VLIW_Instruction.NoArchReg
                ? VLIW_Instruction.NoReg
                : regId;
        }
    }
}
