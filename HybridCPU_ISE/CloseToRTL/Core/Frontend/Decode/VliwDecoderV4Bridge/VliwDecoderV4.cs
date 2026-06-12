using HybridCPU_ISE.Arch;
using System.Globalization;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.Core.Decoder
{
    /// <summary>
    /// HybridCPU ISA v4 native VLIW decoder.
    /// Single decode path — no ISA translation, no compatibility layers, no DBT.
    /// Accepts only canonical ISA v4 opcodes; rejects prohibited opcodes at decode time.
    /// Produces <see cref="InstructionIR"/> records inside the canonical
    /// <see cref="DecodedInstructionBundle"/> frontend contract.
    /// </summary>
    public sealed class VliwDecoderV4 : IDecoderFrontend
    {
        private const ushort XsqrtRawOpcode = 45;
        private const ushort NotRawOpcode = 52;
        private const ushort XfmacRawOpcode = 55;

        // Pre-built set: prohibited opcode names for O(1) lookup.
        // Populated once on class load from IsaV4Surface.ProhibitedOpcodes.
        // ─── Primary decode entry point ───────────────────────────────────────────

        /// <summary>
        /// Decode a single instruction from a VLIW bundle slot.
        /// </summary>
        /// <param name="instruction">Raw VLIW instruction from the bundle slot.</param>
        /// <param name="slotIndex">Physical slot index (0–7) within the bundle.</param>
        /// <returns>Canonical <see cref="InstructionIR"/> record.</returns>
        /// <exception cref="InvalidOpcodeException">
        /// Thrown when the opcode is in <see cref="IsaV4Surface.ProhibitedOpcodes"/>
        /// (pseudo-ops, hint opcodes, or compiler wrappers that must not appear in the
        /// hardware instruction stream).
        /// </exception>
        public InstructionIR Decode(
            in VLIW_Instruction instruction,
            int slotIndex)
        {
            return Decode(in instruction, slotIndex, InstructionSlotMetadata.Default);
        }

        private InstructionIR Decode(
            in VLIW_Instruction instruction,
            int slotIndex,
            InstructionSlotMetadata slotMetadata)
        {
            ushort opcode = Processor.CPU_Core.IsaOpcode.FromRawValue(instruction.OpCode).Value;
            var opcodeName = OpcodeRegistry.GetMnemonicOrHex(opcode);

            // Gate 1: Reject prohibited opcodes immediately.
            if (IsProhibited(opcode, opcodeName))
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) is not part of ISA v4 canonical " +
                    $"surface. Pseudo-ops and hint opcodes must not appear in the hardware " +
                    $"instruction stream. See IsaV4Surface.ProhibitedOpcodes.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: true);
            }

            RejectUnsupportedCustomAcceleratorContour(
                instruction.OpCode,
                slotIndex);
            RejectUnknownOpcode(
                opcode,
                slotIndex);
            RejectRetainedCompatContour(
                opcode,
                instruction.DataType,
                opcodeName,
                slotIndex);
            RejectUnsupportedResidualContour(
                opcode,
                instruction.DataType,
                opcodeName,
                slotIndex);
            RejectReservedWord0(in instruction, slotIndex);
            RejectLegacyPolicyGap(in instruction, slotIndex);
            ValidateOpcodeFlagLegality(
                opcode,
                opcodeName,
                in instruction,
                slotIndex);
            RejectUnsupportedFencePayload(
                in instruction,
                opcode,
                opcodeName,
                slotIndex);
            AcceleratorCommandDescriptor? acceleratorCommandDescriptor =
                ValidateAcceleratorCommandDescriptorNativeCarrier(
                    in instruction,
                    opcode,
                    opcodeName,
                    slotIndex,
                    slotMetadata);
            DmaStreamComputeDescriptor? dmaStreamComputeDescriptor =
                ValidateDmaStreamComputeNativeCarrier(
                    in instruction,
                    opcode,
                    slotIndex,
                    slotMetadata);
            ValidateDmaStreamComputeStatusNativeCarrier(
                in instruction,
                opcode,
                slotIndex,
                slotMetadata);
            ValidateDmaStreamComputeQueryCapsNativeCarrier(
                in instruction,
                opcode,
                slotIndex,
                slotMetadata);

            // Gate 2: Classify via the authoritative ISA v4 classifier.
            var (instrClass, serialClass) = InstructionClassifier.Classify(opcode);
            var (rd, rs1, rs2) = DecodeRegisterOperands(
                in instruction,
                instrClass,
                opcodeName,
                slotIndex);
            RejectScalarImmediateRegisterFormAlias(
                opcode,
                opcodeName,
                rs2,
                slotIndex);
            RejectScalarUnaryRegisterFormAlias(
                opcode,
                opcodeName,
                rs2,
                instruction.Immediate,
                slotIndex);
            RejectScalarAddressGenerationRegisterFormAlias(
                opcode,
                opcodeName,
                instruction.Immediate,
                slotIndex);
            RejectScalarAddressGenerationImmediateRegisterFormAlias(
                opcode,
                opcodeName,
                rs2,
                instruction.Immediate,
                slotIndex);
            RejectScalarCarryLessRegisterFormAlias(
                opcode,
                opcodeName,
                instruction.Immediate,
                slotIndex);
            RejectScalarRotateRegisterFormAlias(
                opcode,
                opcodeName,
                instruction.Immediate,
                slotIndex);
            RejectScalarRotateImmediateRegisterFormAlias(
                opcode,
                opcodeName,
                rs2,
                instruction.Immediate,
                slotIndex);
            RejectScalarBitfieldRegisterFormAlias(
                opcode,
                opcodeName,
                instruction.Immediate,
                slotIndex);
            RejectScalarBitfieldImmediateRegisterFormAlias(
                opcode,
                opcodeName,
                rs2,
                instruction.Immediate,
                slotIndex);
            RejectScalarBooleanInvertRegisterFormAlias(
                opcode,
                opcodeName,
                instruction.Immediate,
                slotIndex);
            RejectScalarMinMaxRegisterFormAlias(
                opcode,
                opcodeName,
                instruction.Immediate,
                slotIndex);
            RejectScalarZeroingSelectRegisterFormAlias(
                opcode,
                opcodeName,
                instruction.Immediate,
                slotIndex);
            RejectCounterReadOperandAlias(
                opcode,
                opcodeName,
                rs1,
                rs2,
                instruction.Immediate,
                slotIndex);
            RejectControlFlowLegacyTargetTransport(
                opcode,
                opcodeName,
                in instruction,
                slotIndex);

            ulong? absoluteMemory = ResolveAbsoluteMemoryAddress(in instruction, opcode);
            bool hasAbsolute = absoluteMemory.HasValue;
            long imm = hasAbsolute
                ? (long)absoluteMemory!.Value
                : (long)(short)instruction.Immediate;
            VectorInstructionPayload? vectorPayload =
                CreateVectorPayloadIfRequired(in instruction, opcode);
            MatrixTileInstructionIrProjection? matrixTileProjection =
                CreateMatrixTileProjectionIfRequired(
                    opcode,
                    vectorPayload,
                    imm);

            // Build canonical IR record.
            return new InstructionIR
            {
                CanonicalOpcode = new Processor.CPU_Core.IsaOpcode(opcode),
                Class = instrClass,
                SerializationClass = serialClass,
                Rd = rd,
                Rs1 = rs1,
                Rs2 = rs2,
                Imm = imm,
                CsrAddress = ResolveCsrAddress(instrClass, opcode, imm),
                HasAbsoluteAddressing = hasAbsolute,
                AcquireOrdering = instruction.Acquire,
                ReleaseOrdering = instruction.Release,
                VectorPayload = vectorPayload,
                MatrixTileProjection = matrixTileProjection,
                DmaStreamComputeDescriptorReference =
                    dmaStreamComputeDescriptor?.DescriptorReference,
                DmaStreamComputeDescriptor = dmaStreamComputeDescriptor,
                AcceleratorCommandDescriptorReference =
                    acceleratorCommandDescriptor?.DescriptorReference,
                AcceleratorCommandDescriptor = acceleratorCommandDescriptor,
            };
        }

        private static MatrixTileInstructionIrProjection? CreateMatrixTileProjectionIfRequired(
            ushort opcode,
            VectorInstructionPayload? vectorPayload,
            long immediate)
        {
            var typedOpcode = (InstructionsEnum)opcode;
            if (!MatrixTileRuntimeOwnedVlmRows.IsMatrixTileOpcode(typedOpcode) ||
                !vectorPayload.HasValue)
            {
                return null;
            }

            return MatrixTileIrProjectionAndMaterializer.ProjectDecodedVectorPayload(
                typedOpcode,
                vectorPayload.Value,
                immediate);
        }

        private static VectorInstructionPayload? CreateVectorPayloadIfRequired(
            in VLIW_Instruction instruction,
            ushort opcode)
        {
            if (!OpcodeRegistry.RequiresVectorPayloadProjection(opcode))
            {
                return null;
            }

            return new VectorInstructionPayload(
                instruction.DestSrc1Pointer,
                instruction.Src2Pointer,
                instruction.StreamLength,
                instruction.Stride,
                instruction.RowStride,
                instruction.Indexed,
                instruction.Is2D,
                instruction.TailAgnostic,
                instruction.MaskAgnostic,
                instruction.Saturating,
                instruction.PredicateMask,
                instruction.DataType);
        }

        /// <summary>
        /// Decode one frontend bundle into the canonical Phase 03 bundle contract.
        /// </summary>
        /// <param name="bundle">Span of raw VLIW instructions (one per slot).</param>
        /// <param name="bundleAddress">PC address of the first byte of the decoded bundle.</param>
        /// <param name="bundleSerial">Bundle serial used by downstream pipeline tracking.</param>
        /// <returns>
        /// Canonical <see cref="DecodedInstructionBundle"/> with one logical slot per
        /// physical bundle slot and semantic content carried only as <see cref="InstructionIR"/>.
        /// </returns>
        public DecodedInstructionBundle DecodeInstructionBundle(
            System.ReadOnlySpan<VLIW_Instruction> bundle,
            VliwBundleAnnotations? bundleAnnotations,
            ulong bundleAddress,
            ulong bundleSerial = 0)
        {
            List<DecodedInstruction> results = new List<DecodedInstruction>(bundle.Length);
            for (int i = 0; i < bundle.Length; i++)
            {
                var slot = bundle[i];
                InstructionSlotMetadata slotMetadata = ResolveSlotMetadata(bundleAnnotations, i);
                // Skip empty slots: opcode 0 is reserved as the hardware NOP sentinel.
                if (slot.OpCode == 0)
                {
                    RejectNonCanonicalEmptySlot(in slot, i);
                    RejectDescriptorSidebandOnEmptySlot(slotMetadata, i);
                    results.Add(DecodedInstruction.CreateEmpty(
                        i,
                        slotMetadata));
                    continue;
                }

                results.Add(DecodedInstruction.CreateOccupied(
                    i,
                    Decode(in slot, i, slotMetadata),
                    slotMetadata));
            }

            return new DecodedInstructionBundle(
                bundleAddress,
                bundleSerial,
                results,
                bundleAnnotations?.BundleMetadata ?? BundleMetadata.Default);
        }

        /// <summary>
        /// Decode one frontend bundle into the canonical Phase 03 bundle contract.
        /// Uses neutral/default slot metadata when no sideband bundle annotations are available.
        /// </summary>
        /// <param name="bundle">Span of raw VLIW instructions (one per slot).</param>
        /// <param name="bundleAddress">PC address of the first byte of the decoded bundle.</param>
        /// <param name="bundleSerial">Bundle serial used by downstream pipeline tracking.</param>
        /// <returns>
        /// Canonical <see cref="DecodedInstructionBundle"/> with one logical slot per
        /// physical bundle slot and semantic content carried only as <see cref="InstructionIR"/>.
        /// </returns>
        public DecodedInstructionBundle DecodeInstructionBundle(
            System.ReadOnlySpan<VLIW_Instruction> bundle,
            ulong bundleAddress,
            ulong bundleSerial = 0)
        {
            return DecodeInstructionBundle(
                bundle,
                null,
                bundleAddress,
                bundleSerial);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Extract the low-order 5-bit register field from a memory pointer field
        /// when the field is being used to carry a register address rather than a
        /// memory pointer (e.g., in compressed register-encoded forms).
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static int ExtractRegField(ulong pointerField)
            => (int)(pointerField & 0x1F);

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static void RejectUnsupportedCustomAcceleratorContour(
            uint rawOpcode,
            int slotIndex)
        {
            if (global::YAKSys_Hybrid_CPU.Core.InstructionRegistry.IsCustomAcceleratorOpcode(rawOpcode))
            {
                string opcodeIdentifier = $"0x{rawOpcode:X}";
                throw global::YAKSys_Hybrid_CPU.Core.InstructionRegistry.CreateUnsupportedCustomAcceleratorException(
                    rawOpcode,
                    opcodeIdentifier,
                    slotIndex);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static void RejectReservedWord0(
            in VLIW_Instruction instruction,
            int slotIndex)
        {
            if (instruction.Reserved == 0)
            {
                return;
            }

            throw new InvalidOpcodeException(
                VLIW_Instruction.GetReservedWord0ViolationMessage(slotIndex),
                opcodeIdentifier: OpcodeRegistry.GetMnemonicOrHex(instruction.OpCode),
                slotIndex: slotIndex,
                isProhibited: false);
        }

        private static void RejectNonCanonicalEmptySlot(
            in VLIW_Instruction instruction,
            int slotIndex)
        {
            if (instruction.Word0 == 0 &&
                instruction.Word1 == 0 &&
                instruction.Word2 == 0 &&
                instruction.Word3 == 0)
            {
                return;
            }

            throw new InvalidOpcodeException(
                $"Slot {slotIndex}: empty/NOP VLIW slots must be canonical all-zero carriers. " +
                "Opcode 0 grants no payload, flag, reserved-bit, address, stream, or sideband authority.",
                opcodeIdentifier: OpcodeRegistry.GetMnemonicOrHex(0),
                slotIndex: slotIndex,
                isProhibited: false);
        }

        private static void ValidateOpcodeFlagLegality(
            ushort opcode,
            string opcodeName,
            in VLIW_Instruction instruction,
            int slotIndex)
        {
            OpcodeInfo? info = OpcodeRegistry.GetInfo(opcode);
            bool isAtomic = info?.InstructionClass == InstructionClass.Atomic ||
                            (info?.Flags & InstructionFlags.Atomic) != 0;
            bool hasVectorPayload = OpcodeRegistry.RequiresVectorPayloadProjection(opcode);

            if ((instruction.Acquire || instruction.Release) && !isAtomic)
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) carries Acquire/Release flag bits, but only atomic opcodes may carry atomic ordering flags.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (instruction.Saturating &&
                !OpcodeRegistry.SupportsSaturatingAddPolicy(opcode))
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) carries the Saturating flag outside the scoped VADD saturating contour.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (instruction.Reduction && !OpcodeRegistry.IsReductionOp(opcode))
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) carries the Reduction flag, but the opcode is not a reduction contour.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (instruction.Indexed && !hasVectorPayload)
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) carries the Indexed flag, but only vector payload opcodes may carry indexed-addressing metadata.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (instruction.Is2D && !hasVectorPayload)
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) carries the Is2D flag, but only vector payload opcodes may carry 2D addressing metadata.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if ((instruction.TailAgnostic || instruction.MaskAgnostic) &&
                !hasVectorPayload)
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) carries vector tail/mask agnostic policy bits outside a vector payload opcode.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static void RejectUnknownOpcode(
            ushort rawOpcode,
            int slotIndex)
        {
            if (!IsKnownDecodeInputOpcode(rawOpcode))
            {
                string opcodeIdentifier = $"0x{rawOpcode:X}";
                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeIdentifier}' (slot {slotIndex}) lies outside the canonical ISA v4 opcode space. " +
                    "Canonical decode must fail closed instead of projecting default ScalarAlu/Free metadata.",
                    opcodeIdentifier,
                    slotIndex,
                    isProhibited: false);
                }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static bool IsKnownDecodeInputOpcode(ushort rawOpcode)
        {
            if (OpcodeRegistry.GetInfo(rawOpcode).HasValue)
            {
                return true;
            }

            return rawOpcode is
                Processor.CPU_Core.IsaOpcodeValues.JumpIfNotEqual or
                Processor.CPU_Core.IsaOpcodeValues.JumpIfBelow or
                Processor.CPU_Core.IsaOpcodeValues.JumpIfBelowOrEqual or
                Processor.CPU_Core.IsaOpcodeValues.JumpIfAbove or
                Processor.CPU_Core.IsaOpcodeValues.JumpIfAboveOrEqual or
                Processor.CPU_Core.IsaOpcodeValues.Interrupt or
                Processor.CPU_Core.IsaOpcodeValues.InterruptReturn or
                Processor.CPU_Core.IsaOpcodeValues.Move or
                Processor.CPU_Core.IsaOpcodeValues.Load or
                Processor.CPU_Core.IsaOpcodeValues.Store or
                Processor.CPU_Core.IsaOpcodeValues.MTILE_LOAD or
                Processor.CPU_Core.IsaOpcodeValues.MTILE_STORE or
                Processor.CPU_Core.IsaOpcodeValues.MTILE_MACC or
                Processor.CPU_Core.IsaOpcodeValues.MTRANSPOSE or
                XsqrtRawOpcode or
                NotRawOpcode or
                XfmacRawOpcode;
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static AcceleratorCommandDescriptor? ValidateAcceleratorCommandDescriptorNativeCarrier(
            in VLIW_Instruction instruction,
            ushort rawOpcode,
            string opcodeName,
            int slotIndex,
            InstructionSlotMetadata slotMetadata)
        {
            AcceleratorCommandDescriptor? descriptor = slotMetadata.AcceleratorCommandDescriptor;
            bool hasDescriptorSideband = descriptor is not null;

            if (!OpcodeRegistry.IsSystemDeviceCommandOpcode(rawOpcode))
            {
                if (hasDescriptorSideband)
                {
                    throw new InvalidOpcodeException(
                        $"Slot {slotIndex}: AcceleratorCommandDescriptor sideband can only accompany the native ACCEL_SUBMIT L7-SDC opcode.",
                        opcodeIdentifier: opcodeName,
                        slotIndex: slotIndex,
                        isProhibited: false);
                }

                return null;
            }

            if (hasDescriptorSideband &&
                rawOpcode != Processor.CPU_Core.IsaOpcodeValues.ACCEL_SUBMIT)
            {
                throw new InvalidOpcodeException(
                    $"Slot {slotIndex}: AcceleratorCommandDescriptor sideband is valid only for ACCEL_SUBMIT in Phase 04, not '{opcodeName}'.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (!AcceleratorDescriptorParser.TryValidateNativeVliwCarrier(
                    in instruction,
                    rawOpcode,
                    slotIndex,
                    hasDescriptorSideband,
                    out AcceleratorCarrierValidationResult? carrierFailure))
            {
                throw new InvalidOpcodeException(
                    carrierFailure!.Message,
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (rawOpcode != Processor.CPU_Core.IsaOpcodeValues.ACCEL_SUBMIT)
            {
                return null;
            }

            if (descriptor is null)
            {
                throw new InvalidOpcodeException(
                    "ACCEL_SUBMIT requires typed AcceleratorCommandDescriptor sideband.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (!AcceleratorOwnerDomainGuard.Default.IsDescriptorGuardBacked(
                    descriptor,
                    out string guardMessage))
            {
                throw new InvalidOpcodeException(
                    $"Slot {slotIndex}: ACCEL_SUBMIT descriptor sideband lacks guard-backed owner/domain acceptance. {guardMessage}",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            AcceleratorGuardDecision submitGuard =
                AcceleratorOwnerDomainGuard.Default.EnsureBeforeSubmit(
                    descriptor,
                    descriptor.OwnerGuardDecision.Evidence);
            if (!submitGuard.IsAllowed)
            {
                throw new InvalidOpcodeException(
                    $"Slot {slotIndex}: ACCEL_SUBMIT admission guard rejected. {submitGuard.Message}",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            ValidateAcceleratorSubmitSidebandPlacement(
                slotMetadata,
                opcodeName,
                slotIndex);

            return descriptor;
        }

        private static void ValidateAcceleratorSubmitSidebandPlacement(
            InstructionSlotMetadata slotMetadata,
            string opcodeName,
            int slotIndex)
        {
            MicroOpAdmissionMetadata admissionMetadata =
                (slotMetadata.SlotMetadata ?? YAKSys_Hybrid_CPU.Core.SlotMetadata.Default).AdmissionMetadata;

            if (admissionMetadata.Equals(MicroOpAdmissionMetadata.Default))
            {
                throw new InvalidOpcodeException(
                    $"Slot {slotIndex}: ACCEL_SUBMIT typed sideband requires explicit slot metadata; default admission metadata is not accepted.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            SlotPlacementMetadata placement = admissionMetadata.Placement;
            if (placement.RequiredSlotClass != SlotClass.SystemSingleton ||
                placement.PinningKind != SlotPinningKind.HardPinned ||
                placement.PinnedLaneId != 7)
            {
                throw new InvalidOpcodeException(
                    $"Slot {slotIndex}: ACCEL_SUBMIT typed sideband requires SystemSingleton hard-pinned lane7 slot metadata.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static void RejectLegacyPolicyGap(
            in VLIW_Instruction instruction,
            int slotIndex)
        {
            if ((instruction.Word3 & VLIW_Instruction.RetiredPolicyGapMask) != 0)
            {
                throw new InvalidOpcodeException(
                    VLIW_Instruction.GetRetiredPolicyGapViolationMessage(slotIndex),
                    opcodeIdentifier: instruction.OpCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    slotIndex: slotIndex,
                    isProhibited: true);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static void RejectRetainedCompatContour(
            ushort rawOpcode,
            byte rawDataType,
            string opcodeName,
            int slotIndex)
        {
            if (rawOpcode == Processor.CPU_Core.IsaOpcodeValues.Move &&
                (rawDataType == 2 || rawDataType == 3))
            {
                string contourDescription = rawDataType == 2
                    ? "absolute-store"
                    : "absolute-load";

                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) uses retained Move DT={rawDataType} {contourDescription} compat contour. " +
                    "Canonical ISA v4 decode must fail closed; route retained wrappers through the quarantined compat decoder/test helper seam instead of mutating canonical decoder truth.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (rawOpcode == Processor.CPU_Core.IsaOpcodeValues.JumpIfEqual ||
                rawOpcode == Processor.CPU_Core.IsaOpcodeValues.JumpIfNotEqual ||
                rawOpcode == Processor.CPU_Core.IsaOpcodeValues.JumpIfBelow ||
                rawOpcode == Processor.CPU_Core.IsaOpcodeValues.JumpIfBelowOrEqual ||
                rawOpcode == Processor.CPU_Core.IsaOpcodeValues.JumpIfAbove ||
                rawOpcode == Processor.CPU_Core.IsaOpcodeValues.JumpIfAboveOrEqual)
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) uses retained conditional-branch compat contour. " +
                    "Canonical ISA v4 decode must fail closed; route retained wrappers through the quarantined compat decoder/test helper seam instead of projecting canonical branch truth inside VliwDecoderV4.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static void RejectUnsupportedResidualContour(
            ushort rawOpcode,
            byte rawDataType,
            string opcodeName,
            int slotIndex)
        {
            if (rawOpcode == Processor.CPU_Core.IsaOpcodeValues.Interrupt ||
                rawOpcode == Processor.CPU_Core.IsaOpcodeValues.InterruptReturn)
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) uses unsupported retained {opcodeName} control-transfer contour. " +
                    "Canonical decode must fail closed instead of publishing system success without a typed mainline retire/boundary carrier.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (rawOpcode == Processor.CPU_Core.IsaOpcodeValues.Move && rawDataType == 4)
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) uses unsupported retained Move DT=4 dual-write contour. " +
                    "Canonical decode must fail closed instead of publishing partial scalar register truth.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (rawOpcode == Processor.CPU_Core.IsaOpcodeValues.Move && rawDataType == 5)
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) uses unsupported retained Move DT=5 triple-destination contour. " +
                    "Canonical decode must fail closed instead of publishing scalar success.",
                    opcodeIdentifier: opcodeName,
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (rawOpcode == XsqrtRawOpcode)
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{XsqrtRawOpcode}' (slot {slotIndex}) uses unsupported optional scalar-math contour (XSQRT). " +
                    "Canonical decode must fail closed instead of publishing scalar ALU register truth without an authoritative scalar carrier/materializer follow-through.",
                    opcodeIdentifier: XsqrtRawOpcode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (rawOpcode == NotRawOpcode)
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{NotRawOpcode}' (slot {slotIndex}) uses unsupported optional scalar bit-manip contour (NOT). " +
                    "Canonical decode must fail closed instead of publishing scalar ALU register truth without an authoritative scalar carrier/materializer follow-through.",
                    opcodeIdentifier: NotRawOpcode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (rawOpcode == XfmacRawOpcode)
            {
                throw new InvalidOpcodeException(
                    $"Opcode '{XfmacRawOpcode}' (slot {slotIndex}) uses unsupported optional scalar-math contour (XFMAC). " +
                    "Canonical decode must fail closed instead of publishing scalar ALU register truth without an authoritative scalar carrier/materializer follow-through.",
                    opcodeIdentifier: XfmacRawOpcode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    slotIndex: slotIndex,
                    isProhibited: false);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static ulong? ResolveAbsoluteMemoryAddress(
            in VLIW_Instruction instruction,
            ushort opcode)
        {
            return opcode switch
            {
                Processor.CPU_Core.IsaOpcodeValues.Load or Processor.CPU_Core.IsaOpcodeValues.Store
                    => instruction.Src2Pointer != 0
                        ? instruction.Src2Pointer
                        : (ulong)(short)instruction.Immediate,
                _ => null,
            };
        }

        private static (byte Rd, byte Rs1, byte Rs2) DecodeRegisterOperands(
            in VLIW_Instruction instruction,
            InstructionClass instructionClass,
            string opcodeName,
            int slotIndex)
        {
            ushort opcode = unchecked((ushort)instruction.OpCode);

            if (opcode == Processor.CPU_Core.IsaOpcodeValues.DmaStreamCompute)
            {
                return (
                    VLIW_Instruction.NoArchReg,
                    VLIW_Instruction.NoArchReg,
                    VLIW_Instruction.NoArchReg);
            }

            if (opcode == Processor.CPU_Core.IsaOpcodeValues.VPOPC)
            {
                // Scalar-result predicate popcount encodes its destination GPR in the upper
                // immediate nibble rather than in the vector address/register fields.
                return (
                    (byte)((instruction.Immediate >> 8) & 0x0F),
                    VLIW_Instruction.NoArchReg,
                    VLIW_Instruction.NoArchReg);
            }

            bool isPublishedVectorOpcode = OpcodeRegistry.IsVectorOp(instruction.OpCode);

            if (isPublishedVectorOpcode &&
                instructionClass is InstructionClass.ScalarAlu or InstructionClass.Memory)
            {
                // Dedicated vector compute and vector-memory carriers do not publish scalar
                // register facts through canonical decode. Their authoritative follow-through is
                // vector/predicate-local rather than the packed Word1 arch-register ABI.
                return (
                    VLIW_Instruction.NoArchReg,
                    VLIW_Instruction.NoArchReg,
                    VLIW_Instruction.NoArchReg);
            }

            if (OpcodeRegistry.UsesPackedArchRegisterWord1(opcode))
            {
                if (!VLIW_Instruction.TryUnpackArchRegs(
                    instruction.Word1,
                    out byte rd,
                    out byte rs1,
                    out byte rs2))
                {
                    throw new InvalidOperationException(
                        $"Opcode '{opcodeName}' (slot {slotIndex}) uses legacy/global register encoding. " +
                        "Scalar/control/system decode accepts only flat architectural register ids.");
                }

                return (rd, rs1, rs2);
            }

            return (
                (byte)(instruction.DestSrc1Pointer != 0
                    ? ExtractRegField(instruction.DestSrc1Pointer)
                    : instruction.Reg1ID),
                (byte)(instruction.Src2Pointer != 0
                    ? ExtractRegField(instruction.Src2Pointer)
                    : instruction.Reg2ID),
                (byte)instruction.Reg3ID);
        }

        private static void RejectScalarImmediateRegisterFormAlias(
            ushort opcode,
            string opcodeName,
            byte rs2,
            int slotIndex)
        {
            if (opcode is not (
                    Processor.CPU_Core.IsaOpcodeValues.SLLIW or
                    Processor.CPU_Core.IsaOpcodeValues.SRLIW or
                    Processor.CPU_Core.IsaOpcodeValues.SRAIW) ||
                rs2 == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Opcode '{opcodeName}' (slot {slotIndex}) requires canonical scalar-immediate Word1=(rd, rs1, x0). " +
                "Register-form aliasing through rs2 is not accepted.");
        }

        private static void RejectScalarUnaryRegisterFormAlias(
            ushort opcode,
            string opcodeName,
            byte rs2,
            ushort immediate,
            int slotIndex)
        {
            if (opcode is not (
                    Processor.CPU_Core.IsaOpcodeValues.SEXT_W or
                    Processor.CPU_Core.IsaOpcodeValues.ZEXT_W or
                    Processor.CPU_Core.IsaOpcodeValues.CLZ or
                    Processor.CPU_Core.IsaOpcodeValues.CTZ or
                    Processor.CPU_Core.IsaOpcodeValues.CPOP or
                    Processor.CPU_Core.IsaOpcodeValues.SEXT_B or
                    Processor.CPU_Core.IsaOpcodeValues.SEXT_H or
                    Processor.CPU_Core.IsaOpcodeValues.ZEXT_H or
                    Processor.CPU_Core.IsaOpcodeValues.REV8 or
                    Processor.CPU_Core.IsaOpcodeValues.BREV8))
            {
                return;
            }

            if (rs2 != 0)
            {
                throw new InvalidOperationException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) requires canonical scalar-unary Word1=(rd, rs1, x0). " +
                    "Register-form aliasing through rs2 is not accepted.");
            }

            if (immediate != 0)
            {
                throw new InvalidOperationException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) requires canonical scalar-unary Immediate=0. " +
                    "Immediate-form aliasing is not accepted.");
            }
        }

        private static void RejectScalarAddressGenerationRegisterFormAlias(
            ushort opcode,
            string opcodeName,
            ushort immediate,
            int slotIndex)
        {
            if (opcode is not (
                    Processor.CPU_Core.IsaOpcodeValues.SH1ADD or
                    Processor.CPU_Core.IsaOpcodeValues.SH2ADD or
                    Processor.CPU_Core.IsaOpcodeValues.SH3ADD or
                    Processor.CPU_Core.IsaOpcodeValues.ADD_UW or
                    Processor.CPU_Core.IsaOpcodeValues.SH1ADD_UW or
                    Processor.CPU_Core.IsaOpcodeValues.SH2ADD_UW or
                    Processor.CPU_Core.IsaOpcodeValues.SH3ADD_UW))
            {
                return;
            }

            if (immediate != 0)
            {
                throw new InvalidOperationException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) requires canonical scalar address-generation Immediate=0. " +
                    "Immediate-form aliasing is not accepted.");
            }
        }

        private static void RejectScalarAddressGenerationImmediateRegisterFormAlias(
            ushort opcode,
            string opcodeName,
            byte rs2,
            ushort immediate,
            int slotIndex)
        {
            if (opcode is not Processor.CPU_Core.IsaOpcodeValues.SLLI_UW)
            {
                return;
            }

            if (rs2 != 0)
            {
                throw new InvalidOperationException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) requires canonical scalar address-generation immediate Word1=(rd, rs1, x0). " +
                    "Register-form aliasing through rs2 is not accepted.");
            }

            if (immediate > 0x3F)
            {
                throw new InvalidOperationException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) requires scalar address-generation imm6 Immediate in [0, 63]. " +
                    "Out-of-range immediate payloads are not accepted.");
            }
        }

        private static void RejectScalarCarryLessRegisterFormAlias(
            ushort opcode,
            string opcodeName,
            ushort immediate,
            int slotIndex)
        {
            if (opcode is not (
                    Processor.CPU_Core.IsaOpcodeValues.CLMUL or
                    Processor.CPU_Core.IsaOpcodeValues.CLMULH or
                    Processor.CPU_Core.IsaOpcodeValues.CLMULR))
            {
                return;
            }

            if (immediate != 0)
            {
                throw new InvalidOperationException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) requires canonical scalar carry-less Immediate=0. " +
                    "Immediate-form aliasing is not accepted.");
            }
        }

        private static void RejectScalarRotateRegisterFormAlias(
            ushort opcode,
            string opcodeName,
            ushort immediate,
            int slotIndex)
        {
            if (opcode is not (
                    Processor.CPU_Core.IsaOpcodeValues.ROL or
                    Processor.CPU_Core.IsaOpcodeValues.ROR))
            {
                return;
            }

            if (immediate != 0)
            {
                throw new InvalidOperationException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) requires canonical scalar rotate Immediate=0. " +
                "Immediate-form aliasing is not accepted.");
            }
        }

        private static void RejectScalarRotateImmediateRegisterFormAlias(
            ushort opcode,
            string opcodeName,
            byte rs2,
            ushort immediate,
            int slotIndex)
        {
            if (opcode is not (
                    Processor.CPU_Core.IsaOpcodeValues.ROLI or
                    Processor.CPU_Core.IsaOpcodeValues.RORI))
            {
                return;
            }

            if (rs2 != 0)
            {
                throw new InvalidOperationException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) requires canonical scalar rotate-immediate Word1=(rd, rs1, x0). " +
                    "Register-form aliasing through rs2 is not accepted.");
            }

            if (immediate > 0x3F)
            {
                throw new InvalidOperationException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) requires scalar rotate-immediate imm6 Immediate in [0, 63]. " +
                    "Out-of-range immediate payloads are not accepted.");
            }
        }

        private static void RejectScalarBooleanInvertRegisterFormAlias(
            ushort opcode,
            string opcodeName,
            ushort immediate,
            int slotIndex)
        {
            if (opcode is not (
                    Processor.CPU_Core.IsaOpcodeValues.ANDN or
                    Processor.CPU_Core.IsaOpcodeValues.ORN or
                    Processor.CPU_Core.IsaOpcodeValues.XNOR))
            {
                return;
            }

            if (immediate != 0)
            {
                throw new InvalidOperationException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) requires canonical scalar boolean-invert Immediate=0. " +
                    "Immediate-form aliasing is not accepted.");
            }
        }

        private static void RejectScalarBitfieldRegisterFormAlias(
            ushort opcode,
            string opcodeName,
            ushort immediate,
            int slotIndex)
        {
            if (opcode is not (
                    Processor.CPU_Core.IsaOpcodeValues.BSET or
                    Processor.CPU_Core.IsaOpcodeValues.BCLR or
                    Processor.CPU_Core.IsaOpcodeValues.BINV or
                    Processor.CPU_Core.IsaOpcodeValues.BEXT))
            {
                return;
            }

            if (immediate != 0)
            {
                throw new InvalidOperationException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) requires canonical scalar bitfield Immediate=0. " +
                    "Immediate-form aliasing is not accepted.");
            }
        }

        private static void RejectScalarBitfieldImmediateRegisterFormAlias(
            ushort opcode,
            string opcodeName,
            byte rs2,
            ushort immediate,
            int slotIndex)
        {
            if (opcode is not (
                    Processor.CPU_Core.IsaOpcodeValues.BSETI or
                    Processor.CPU_Core.IsaOpcodeValues.BCLRI or
                    Processor.CPU_Core.IsaOpcodeValues.BINVI or
                    Processor.CPU_Core.IsaOpcodeValues.BEXTI))
            {
                return;
            }

            if (rs2 != 0)
            {
                throw new InvalidOperationException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) requires canonical scalar bitfield-immediate Word1=(rd, rs1, x0). " +
                    "Register-form aliasing through rs2 is not accepted.");
            }

            if (immediate > 0x3F)
            {
                throw new InvalidOperationException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) requires scalar bitfield-immediate imm6 Immediate in [0, 63]. " +
                    "Out-of-range immediate payloads are not accepted.");
            }
        }

        private static void RejectScalarMinMaxRegisterFormAlias(
            ushort opcode,
            string opcodeName,
            ushort immediate,
            int slotIndex)
        {
            if (opcode is not (
                    Processor.CPU_Core.IsaOpcodeValues.MIN or
                    Processor.CPU_Core.IsaOpcodeValues.MAX or
                    Processor.CPU_Core.IsaOpcodeValues.MINU or
                    Processor.CPU_Core.IsaOpcodeValues.MAXU))
            {
                return;
            }

            if (immediate != 0)
            {
                throw new InvalidOperationException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) requires canonical scalar min/max Immediate=0. " +
                    "Immediate-form aliasing is not accepted.");
            }
        }

        private static void RejectScalarZeroingSelectRegisterFormAlias(
            ushort opcode,
            string opcodeName,
            ushort immediate,
            int slotIndex)
        {
            if (opcode is not Processor.CPU_Core.IsaOpcodeValues.CZERO_NEZ)
            {
                return;
            }

            if (immediate != 0)
            {
                throw new InvalidOperationException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) requires canonical scalar zeroing-select Immediate=0. " +
                    "Immediate-form aliasing is not accepted.");
            }
        }

        private static void RejectCounterReadOperandAlias(
            ushort opcode,
            string opcodeName,
            byte rs1,
            byte rs2,
            ushort immediate,
            int slotIndex)
        {
            if (opcode is not Processor.CPU_Core.IsaOpcodeValues.RDCYCLE)
            {
                return;
            }

            if (rs1 != 0 || rs2 != 0)
            {
                throw new InvalidOperationException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) requires canonical counter-read Word1=(rd, x0, x0). " +
                    "Source-register aliasing is not accepted.");
            }

            if (immediate != 0)
            {
                throw new InvalidOperationException(
                    $"Opcode '{opcodeName}' (slot {slotIndex}) requires canonical counter-read Immediate=0. " +
                    "Immediate-form aliasing is not accepted.");
            }
        }

        private static ushort? ResolveCsrAddress(
            InstructionClass instructionClass,
            ushort opcode,
            long immediate)
        {
            if (opcode == Processor.CPU_Core.IsaOpcodeValues.RDCYCLE)
            {
                return CsrAddresses.Cycle;
            }

            return instructionClass == InstructionClass.Csr
                ? (ushort)(immediate & 0xFFF)
                : null;
        }

        private static void RejectControlFlowLegacyTargetTransport(
            ushort opcode,
            string opcodeName,
            in VLIW_Instruction instruction,
            int slotIndex)
        {
            if (!OpcodeRegistry.IsControlFlowOp(opcode) ||
                instruction.Src2Pointer == 0)
            {
                return;
            }

            throw new InvalidOpcodeException(
                $"Opcode '{opcodeName}' (slot {slotIndex}) uses legacy Src2Pointer control-flow target transport. " +
                "Published branch/control ABI encodes the target displacement in Immediate; JALR uses rs1 plus Immediate.",
                opcodeIdentifier: opcodeName,
                slotIndex: slotIndex,
                isProhibited: false);
        }

        private static void RejectUnsupportedFencePayload(
            in VLIW_Instruction instruction,
            ushort opcode,
            string opcodeName,
            int slotIndex)
        {
            if (opcode is not (
                    Processor.CPU_Core.IsaOpcodeValues.FENCE or
                    Processor.CPU_Core.IsaOpcodeValues.FENCE_I))
            {
                return;
            }

            ulong flags = (instruction.Word0 >> 16) & 0xFFUL;
            ulong word3Payload = instruction.Word3 & ~(0x3UL << 48);
            if (instruction.Immediate == 0 &&
                instruction.PredicateMask == 0 &&
                flags == 0 &&
                instruction.Word1 == 0 &&
                instruction.Word2 == 0 &&
                word3Payload == 0)
            {
                return;
            }

            throw new InvalidOpcodeException(
                $"Opcode '{opcodeName}' (slot {slotIndex}) carries unsupported Phase 10 fence payload. " +
                "Current runtime-owned FENCE/FENCE.I semantics accept only the canonical zero-payload form: " +
                "Immediate=0, PredicateMask=0, flags=0, Word1=0, Word2=0, and no Word3 payload.",
                opcodeIdentifier: opcodeName,
                slotIndex: slotIndex,
                isProhibited: false);
        }

        private static InstructionSlotMetadata ResolveSlotMetadata(
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

        private static void RejectDescriptorSidebandOnEmptySlot(
            InstructionSlotMetadata slotMetadata,
            int slotIndex)
        {
            if (slotMetadata.DmaStreamComputeDescriptor is null &&
                !slotMetadata.DmaStreamComputeDescriptorReference.HasValue &&
                slotMetadata.AcceleratorCommandDescriptor is null &&
                !slotMetadata.AcceleratorCommandDescriptorReference.HasValue)
            {
                return;
            }

            throw new InvalidOpcodeException(
                $"Slot {slotIndex}: descriptor sideband cannot accompany an empty/NOP VLIW slot.",
                opcodeIdentifier: OpcodeRegistry.GetMnemonicOrHex(0),
                slotIndex: slotIndex,
                isProhibited: false);
        }

        // ─── Static prohibition query API ─────────────────────────────────────────

        private static DmaStreamComputeDescriptor? ValidateDmaStreamComputeNativeCarrier(
            in VLIW_Instruction instruction,
            ushort opcode,
            int slotIndex,
            InstructionSlotMetadata slotMetadata)
        {
            if (opcode != Processor.CPU_Core.IsaOpcodeValues.DmaStreamCompute)
            {
                if (slotMetadata.DmaStreamComputeDescriptor is not null ||
                    slotMetadata.DmaStreamComputeDescriptorReference.HasValue)
                {
                    throw new InvalidOpcodeException(
                        $"Slot {slotIndex}: DmaStreamCompute descriptor sideband can only accompany the native DmaStreamCompute opcode.",
                        opcodeIdentifier: OpcodeRegistry.GetMnemonicOrHex(opcode),
                        slotIndex: slotIndex,
                        isProhibited: false);
                }

                return null;
            }

            DmaStreamComputeDescriptor? descriptor = slotMetadata.DmaStreamComputeDescriptor;
            bool hasDescriptorSideband = descriptor is not null;
            if (!DmaStreamComputeDescriptorParser.TryValidateNativeVliwCarrier(
                    in instruction,
                    slotIndex,
                    hasDescriptorSideband,
                    out DmaStreamComputeValidationResult? carrierFailure))
            {
                throw new InvalidOpcodeException(
                    carrierFailure!.Message,
                    opcodeIdentifier: OpcodeRegistry.GetMnemonicOrHex(opcode),
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (slotIndex != 6)
            {
                throw new InvalidOpcodeException(
                    $"Slot {slotIndex}: DmaStreamCompute is a lane6 DMA/stream instruction and cannot decode on any other VLIW slot.",
                    opcodeIdentifier: OpcodeRegistry.GetMnemonicOrHex(opcode),
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (!descriptor!.OwnerGuardDecision.IsAllowed)
            {
                throw new InvalidOpcodeException(
                    $"Slot {slotIndex}: DmaStreamCompute descriptor sideband lacks an accepted owner/domain guard decision.",
                    opcodeIdentifier: OpcodeRegistry.GetMnemonicOrHex(opcode),
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (slotMetadata.DmaStreamComputeDescriptorReference is { } reference &&
                !reference.Equals(descriptor.DescriptorReference))
            {
                throw new InvalidOpcodeException(
                    $"Slot {slotIndex}: DmaStreamCompute descriptor reference sideband does not match the accepted descriptor payload.",
                    opcodeIdentifier: OpcodeRegistry.GetMnemonicOrHex(opcode),
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            return descriptor;
        }

        private static void ValidateDmaStreamComputeStatusNativeCarrier(
            in VLIW_Instruction instruction,
            ushort opcode,
            int slotIndex,
            InstructionSlotMetadata slotMetadata)
        {
            if (opcode != Processor.CPU_Core.IsaOpcodeValues.DSC_STATUS)
            {
                return;
            }

            if (slotIndex != 6)
            {
                throw new InvalidOpcodeException(
                    $"Slot {slotIndex}: DSC_STATUS is a lane6 DMA/stream queue-control instruction and cannot decode on any other VLIW slot.",
                    opcodeIdentifier: OpcodeRegistry.GetMnemonicOrHex(opcode),
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (slotMetadata.DmaStreamComputeDescriptor is not null ||
                slotMetadata.DmaStreamComputeDescriptorReference.HasValue)
            {
                throw new InvalidOpcodeException(
                    $"Slot {slotIndex}: DSC_STATUS uses a token-id register ABI and cannot carry a DmaStreamCompute descriptor sideband.",
                    opcodeIdentifier: OpcodeRegistry.GetMnemonicOrHex(opcode),
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (VLIW_Instruction.TryUnpackArchRegs(
                    instruction.Word1,
                    out _,
                    out _,
                    out byte rs2) &&
                rs2 != 0)
            {
                throw new InvalidOpcodeException(
                    $"Slot {slotIndex}: DSC_STATUS requires rs2 to be x0.",
                    opcodeIdentifier: OpcodeRegistry.GetMnemonicOrHex(opcode),
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (instruction.Reserved != 0 ||
                instruction.VirtualThreadId != 0 ||
                instruction.Immediate != 0 ||
                instruction.PredicateMask != 0 ||
                instruction.Acquire ||
                instruction.Release ||
                instruction.Saturating ||
                instruction.MaskAgnostic ||
                instruction.TailAgnostic ||
                instruction.Indexed ||
                instruction.Is2D ||
                instruction.Reduction)
            {
                throw new InvalidOpcodeException(
                    $"Slot {slotIndex}: DSC_STATUS accepts only the clean packed-register ABI rd, rs1(token-id), x0.",
                    opcodeIdentifier: OpcodeRegistry.GetMnemonicOrHex(opcode),
                    slotIndex: slotIndex,
                    isProhibited: false);
            }
        }

        private static void ValidateDmaStreamComputeQueryCapsNativeCarrier(
            in VLIW_Instruction instruction,
            ushort opcode,
            int slotIndex,
            InstructionSlotMetadata slotMetadata)
        {
            if (opcode != Processor.CPU_Core.IsaOpcodeValues.DSC_QUERY_CAPS)
            {
                return;
            }

            if (slotIndex != 6)
            {
                throw new InvalidOpcodeException(
                    $"Slot {slotIndex}: DSC_QUERY_CAPS is a lane6 DMA/stream capability query and cannot decode on any other VLIW slot.",
                    opcodeIdentifier: OpcodeRegistry.GetMnemonicOrHex(opcode),
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (slotMetadata.DmaStreamComputeDescriptor is not null ||
                slotMetadata.DmaStreamComputeDescriptorReference.HasValue)
            {
                throw new InvalidOpcodeException(
                    $"Slot {slotIndex}: DSC_QUERY_CAPS uses a register writeback ABI and cannot carry a DmaStreamCompute descriptor sideband.",
                    opcodeIdentifier: OpcodeRegistry.GetMnemonicOrHex(opcode),
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (VLIW_Instruction.TryUnpackArchRegs(
                    instruction.Word1,
                    out _,
                    out byte rs1,
                    out byte rs2) &&
                (rs1 != 0 || rs2 != 0))
            {
                throw new InvalidOpcodeException(
                    $"Slot {slotIndex}: DSC_QUERY_CAPS requires source registers to be x0, x0.",
                    opcodeIdentifier: OpcodeRegistry.GetMnemonicOrHex(opcode),
                    slotIndex: slotIndex,
                    isProhibited: false);
            }

            if (instruction.Reserved != 0 ||
                instruction.VirtualThreadId != 0 ||
                instruction.Immediate != 0 ||
                instruction.PredicateMask != 0 ||
                instruction.Acquire ||
                instruction.Release ||
                instruction.Saturating ||
                instruction.MaskAgnostic ||
                instruction.TailAgnostic ||
                instruction.Indexed ||
                instruction.Is2D ||
                instruction.Reduction)
            {
                throw new InvalidOpcodeException(
                    $"Slot {slotIndex}: DSC_QUERY_CAPS accepts only the clean packed-register ABI rd, x0, x0.",
                    opcodeIdentifier: OpcodeRegistry.GetMnemonicOrHex(opcode),
                    slotIndex: slotIndex,
                    isProhibited: false);
            }
        }

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="opcode"/> is listed as
        /// prohibited in <see cref="IsaV4Surface.ProhibitedOpcodes"/>.
        /// </summary>
        public static bool IsProhibited(Processor.CPU_Core.IsaOpcode opcode)
        {
            var name = opcode.ToString();
            return IsProhibited(opcode.Value, name);
        }

        /// <summary>
        /// Returns <see langword="true"/> if the string <paramref name="opcodeName"/> is
        /// listed in <see cref="IsaV4Surface.ProhibitedOpcodes"/>.
        /// </summary>
        public static bool IsProhibited(string opcodeName)
        {
            if (TryParseOpcodeIdentifier(opcodeName, out ushort opcode))
            {
                return IsProhibited(opcode, opcodeName);
            }

            return IsaV4Surface.ProhibitedOpcodes.Contains(opcodeName) ||
                   IsaV4Surface.ProhibitedOpcodes.Contains(opcodeName.ToUpperInvariant());
        }

        private static bool IsProhibited(ushort opcode, string opcodeName)
        {
            if (IsaV4Surface.ProhibitedOpcodes.Contains(opcodeName) ||
                IsaV4Surface.ProhibitedOpcodes.Contains(opcodeName.ToUpperInvariant()))
            {
                return true;
            }

            string decimalIdentifier = opcode.ToString(CultureInfo.InvariantCulture);
            if (IsaV4Surface.ProhibitedOpcodes.Contains(decimalIdentifier))
            {
                return true;
            }

            string hexIdentifier = $"0x{opcode:X}";
            return IsaV4Surface.ProhibitedOpcodes.Contains(hexIdentifier) ||
                   IsaV4Surface.ProhibitedOpcodes.Contains(hexIdentifier.ToUpperInvariant());
        }

        private static bool TryParseOpcodeIdentifier(string opcodeIdentifier, out ushort opcode)
        {
            if (ushort.TryParse(
                opcodeIdentifier,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out opcode))
            {
                return true;
            }

            if (opcodeIdentifier.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                ushort.TryParse(
                    opcodeIdentifier[2..],
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out opcode))
            {
                return true;
            }

            opcode = default;
            return false;
        }
    }
}
