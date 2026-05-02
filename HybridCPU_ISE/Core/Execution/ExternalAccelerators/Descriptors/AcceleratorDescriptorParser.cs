using HybridCPU_ISE.Arch;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Telemetry;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

public static class AcceleratorDescriptorParser
{
    public const uint Magic = 0x31434453; // "SDC1" as a little-endian scalar.
    public const ushort CurrentAbiVersion = 1;
    public const int CurrentHeaderSize = 128;

    private const int HeaderSize = CurrentHeaderSize;
    private const int RangeEntrySize = 16;
    private const ushort MaxRangeCount = 16;

    private const int MagicOffset = 0;
    private const int AbiVersionOffset = 4;
    private const int HeaderSizeOffset = 6;
    private const int DescriptorSizeOffset = 8;
    private const int FlagsOffset = 12;
    private const int AcceleratorClassOffset = 16;
    private const int AcceleratorIdOffset = 18;
    private const int OperationOffset = 20;
    private const int DatatypeOffset = 22;
    private const int DescriptorIdentityHashOffset = 24;
    private const int NormalizedFootprintHashOffset = 32;
    private const int ShapeOffset = 40;
    private const int ShapeRankOffset = 42;
    private const int SourceRangeCountOffset = 44;
    private const int DestinationRangeCountOffset = 46;
    private const int ScratchRangeCountOffset = 48;
    private const int PartialCompletionPolicyOffset = 50;
    private const int AlignmentBytesOffset = 52;
    private const int Reserved1Offset = 54;
    private const int ElementCountOffset = 56;
    private const int CapabilityVersionOffset = 64;
    private const int OwnerVirtualThreadIdOffset = 68;
    private const int Reserved2Offset = 70;
    private const int OwnerContextIdOffset = 72;
    private const int OwnerCoreIdOffset = 76;
    private const int OwnerPodIdOffset = 80;
    private const int Reserved3Offset = 84;
    private const int DomainTagOffset = 88;
    private const int SourceRangeTableOffset = 96;
    private const int DestinationRangeTableOffset = 100;
    private const int ScratchRangeTableOffset = 104;
    private const int Reserved4Offset = 108;
    private const int ScratchRequiredBytesOffset = 112;
    private const int Reserved5Offset = 120;

    public static AcceleratorDescriptorValidationResult Parse(
        ReadOnlySpan<byte> descriptorBytes,
        AcceleratorDescriptorReference? descriptorReference = null,
        AcceleratorTelemetry? telemetry = null)
    {
        AcceleratorDescriptorStructuralReadResult structuralRead =
            ReadStructuralOwnerBinding(descriptorBytes, descriptorReference);
        if (!structuralRead.IsValid)
        {
            return RecordParseResult(
                AcceleratorDescriptorValidationResult.Fail(
                    structuralRead.Fault,
                    structuralRead.Message),
                telemetry);
        }

        return RecordParseResult(
            AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.OwnerDomainFault,
                "L7-SDC descriptor cannot be accepted without an explicit owner/domain guard decision."),
            telemetry);
    }

    public static AcceleratorDescriptorValidationResult Parse(
        ReadOnlySpan<byte> descriptorBytes,
        AcceleratorGuardEvidence guardEvidence,
        AcceleratorDescriptorReference? descriptorReference = null,
        AcceleratorTelemetry? telemetry = null)
    {
        AcceleratorDescriptorStructuralReadResult structuralRead =
            ReadStructuralOwnerBinding(descriptorBytes, descriptorReference);
        if (!structuralRead.IsValid)
        {
            return RecordParseResult(
                AcceleratorDescriptorValidationResult.Fail(
                    structuralRead.Fault,
                    structuralRead.Message),
                telemetry);
        }

        AcceleratorGuardDecision ownerGuardDecision =
            AcceleratorOwnerDomainGuard.Default.EnsureBeforeDescriptorAcceptance(
                structuralRead.RequireOwnerBindingForGuard(),
                guardEvidence);
        return Parse(descriptorBytes, ownerGuardDecision, descriptorReference, telemetry);
    }

    public static AcceleratorDescriptorValidationResult Parse(
        ReadOnlySpan<byte> descriptorBytes,
        AcceleratorGuardDecision ownerGuardDecision,
        AcceleratorDescriptorReference? descriptorReference = null,
        AcceleratorTelemetry? telemetry = null)
    {
        AcceleratorDescriptorValidationResult result =
            ParseGuardedCore(descriptorBytes, ownerGuardDecision, descriptorReference);
        if (!ownerGuardDecision.IsAllowed)
        {
            telemetry?.RecordGuardReject(ownerGuardDecision, result.Message);
        }

        return RecordParseResult(result, telemetry);
    }

    private static AcceleratorDescriptorValidationResult ParseGuardedCore(
        ReadOnlySpan<byte> descriptorBytes,
        AcceleratorGuardDecision ownerGuardDecision,
        AcceleratorDescriptorReference? descriptorReference)
    {
        AcceleratorDescriptorStructuralReadResult structuralRead =
            ReadStructuralOwnerBinding(descriptorBytes, descriptorReference);
        if (!structuralRead.IsValid)
        {
            return AcceleratorDescriptorValidationResult.Fail(
                structuralRead.Fault,
                structuralRead.Message);
        }

        AcceleratorOwnerBinding structuralOwnerBinding =
            structuralRead.RequireOwnerBindingForGuard();
        if (!ownerGuardDecision.IsAllowed)
        {
            return AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.OwnerDomainFault,
                string.IsNullOrWhiteSpace(ownerGuardDecision.Message)
                    ? "L7-SDC descriptor owner/domain guard rejected before descriptor acceptance."
                    : ownerGuardDecision.Message);
        }

        if (ownerGuardDecision.LegalityDecision.AuthoritySource != LegalityAuthoritySource.GuardPlane ||
            ownerGuardDecision.LegalityDecision.AttemptedReplayCertificateReuse ||
            ownerGuardDecision.Evidence?.Source != AcceleratorGuardEvidenceSource.GuardPlane)
        {
            return AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.OwnerDomainFault,
                "L7-SDC owner guard decision must come from the guard plane before replay/certificate or evidence-plane reuse.");
        }

        if (ownerGuardDecision.DescriptorOwnerBinding is null ||
            !ownerGuardDecision.DescriptorOwnerBinding.Equals(structuralOwnerBinding))
        {
            return AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.OwnerDomainFault,
                "L7-SDC owner guard decision does not match structurally-read descriptor owner fields.");
        }

        if (!TryReadHeader(
                descriptorBytes,
                descriptorReference,
                out AcceleratorDescriptorHeader header,
                out AcceleratorDescriptorReference effectiveReference,
                out AcceleratorDescriptorValidationResult? headerFailure))
        {
            return headerFailure!;
        }

        if (HasAnyReservedBitSet(descriptorBytes))
        {
            return AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.ReservedFieldFault,
                "L7-SDC v1 descriptor flags and reserved fields must be zero.");
        }

        if (!ValidateCapabilityShape(
                header.AcceleratorClass,
                header.AcceleratorId,
                header.Operation,
                header.Datatype,
                header.Shape,
                header.ShapeRank,
                header.ElementCount,
                out AcceleratorDescriptorValidationResult? capabilityFailure))
        {
            return capabilityFailure!;
        }

        uint sourceRangeTableOffset = ReadUInt32(descriptorBytes, SourceRangeTableOffset);
        uint destinationRangeTableOffset = ReadUInt32(descriptorBytes, DestinationRangeTableOffset);
        uint scratchRangeTableOffset = ReadUInt32(descriptorBytes, ScratchRangeTableOffset);

        if (!ValidateRangeTableLayout(
                header.DescriptorSize,
                sourceRangeTableOffset,
                header.SourceRangeCount,
                destinationRangeTableOffset,
                header.DestinationRangeCount,
                scratchRangeTableOffset,
                header.ScratchRangeCount,
                out AcceleratorDescriptorValidationResult? tableLayoutFailure))
        {
            return tableLayoutFailure!;
        }

        if (!TryReadRanges(
                descriptorBytes,
                header.DescriptorSize,
                sourceRangeTableOffset,
                header.SourceRangeCount,
                header.Alignment.MinimumAlignmentBytes,
                requireNonEmpty: true,
                "source",
                out IReadOnlyList<AcceleratorMemoryRange> sourceRanges,
                out AcceleratorDescriptorValidationResult? sourceFailure))
        {
            return sourceFailure!;
        }

        if (!TryReadRanges(
                descriptorBytes,
                header.DescriptorSize,
                destinationRangeTableOffset,
                header.DestinationRangeCount,
                header.Alignment.MinimumAlignmentBytes,
                requireNonEmpty: true,
                "destination",
                out IReadOnlyList<AcceleratorMemoryRange> destinationRanges,
                out AcceleratorDescriptorValidationResult? destinationFailure))
        {
            return destinationFailure!;
        }

        if (!TryReadRanges(
                descriptorBytes,
                header.DescriptorSize,
                scratchRangeTableOffset,
                header.ScratchRangeCount,
                header.Alignment.MinimumAlignmentBytes,
                requireNonEmpty: header.ScratchRequiredBytes != 0,
                "scratch",
                out IReadOnlyList<AcceleratorMemoryRange> scratchRanges,
                out AcceleratorDescriptorValidationResult? scratchFailure))
        {
            return scratchFailure!;
        }

        if (header.ScratchRequiredBytes != 0 &&
            !ScratchRangesCoverRequirement(scratchRanges, header.ScratchRequiredBytes))
        {
            return AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.DescriptorDecodeFault,
                "L7-SDC scratch ranges do not cover the descriptor scratch requirement.");
        }

        IReadOnlyList<AcceleratorMemoryRange> normalizedSourceRanges =
            NormalizeMemoryRanges(sourceRanges);
        IReadOnlyList<AcceleratorMemoryRange> normalizedDestinationRanges =
            NormalizeMemoryRanges(destinationRanges);
        IReadOnlyList<AcceleratorMemoryRange> normalizedScratchRanges =
            NormalizeMemoryRanges(scratchRanges);

        ulong computedFootprintHash = ComputeNormalizedFootprintHash(
            header.AcceleratorClass,
            header.AcceleratorId,
            header.Operation,
            header.Datatype,
            header.Shape,
            header.ShapeRank,
            header.ElementCount,
            header.PartialCompletionPolicy,
            header.Alignment,
            normalizedSourceRanges,
            normalizedDestinationRanges,
            normalizedScratchRanges);

        if (computedFootprintHash != header.Identity.NormalizedFootprintHash)
        {
            return AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.NormalizedFootprintHashMismatch,
                "L7-SDC normalized footprint hash does not match the validated descriptor footprint.");
        }

        ulong computedIdentityHash = ComputeDescriptorIdentityHash(
            descriptorBytes.Slice(0, checked((int)header.DescriptorSize)));
        if (computedIdentityHash != header.Identity.DescriptorIdentityHash)
        {
            return AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.DescriptorIdentityHashMismatch,
                "L7-SDC descriptor identity hash does not match the typed descriptor payload.");
        }

        var footprint = new AcceleratorNormalizedFootprint
        {
            SourceRanges = normalizedSourceRanges,
            DestinationRanges = normalizedDestinationRanges,
            ScratchRanges = normalizedScratchRanges,
            Hash = computedFootprintHash
        };

        var descriptor = new AcceleratorCommandDescriptor
        {
            DescriptorReference = effectiveReference,
            Header = header,
            AbiVersion = header.AbiVersion,
            HeaderSize = header.HeaderSize,
            DescriptorSize = header.DescriptorSize,
            AcceleratorClass = header.AcceleratorClass,
            AcceleratorId = header.AcceleratorId,
            Operation = header.Operation,
            Datatype = header.Datatype,
            Shape = header.Shape,
            ShapeRank = header.ShapeRank,
            ElementCount = header.ElementCount,
            CapabilityVersion = header.CapabilityVersion,
            Alignment = header.Alignment,
            ScratchRequirement = new AcceleratorScratchRequirement(
                header.ScratchRequiredBytes,
                scratchRanges),
            PartialCompletionPolicy = header.PartialCompletionPolicy,
            OwnerBinding = header.OwnerBinding,
            OwnerGuardDecision = ownerGuardDecision,
            Identity = header.Identity,
            SourceRanges = sourceRanges,
            DestinationRanges = destinationRanges,
            ScratchRanges = scratchRanges,
            NormalizedFootprint = footprint
        };

        return AcceleratorDescriptorValidationResult.Valid(descriptor);
    }

    private static AcceleratorDescriptorValidationResult RecordParseResult(
        AcceleratorDescriptorValidationResult result,
        AcceleratorTelemetry? telemetry)
    {
        telemetry?.RecordDescriptorParse(
            result.IsValid,
            result.Fault,
            result.Message);
        return result;
    }

    public static AcceleratorDescriptorStructuralReadResult ReadStructuralOwnerBinding(
        ReadOnlySpan<byte> descriptorBytes,
        AcceleratorDescriptorReference? descriptorReference = null)
    {
        if (descriptorBytes.Length < HeaderSize)
        {
            return AcceleratorDescriptorStructuralReadResult.Fail(
                AcceleratorDescriptorFault.DescriptorDecodeFault,
                "L7-SDC descriptor is shorter than the v1 fixed header.");
        }

        uint magic = ReadUInt32(descriptorBytes, MagicOffset);
        if (magic != Magic)
        {
            return AcceleratorDescriptorStructuralReadResult.Fail(
                AcceleratorDescriptorFault.DescriptorDecodeFault,
                "L7-SDC descriptor magic does not match v1.");
        }

        ushort abiVersion = ReadUInt16(descriptorBytes, AbiVersionOffset);
        if (abiVersion != CurrentAbiVersion)
        {
            return AcceleratorDescriptorStructuralReadResult.Fail(
                AcceleratorDescriptorFault.UnsupportedAbiVersion,
                $"Unsupported L7-SDC descriptor ABI version {abiVersion}.");
        }

        ushort headerSize = ReadUInt16(descriptorBytes, HeaderSizeOffset);
        if (headerSize != HeaderSize)
        {
            return AcceleratorDescriptorStructuralReadResult.Fail(
                AcceleratorDescriptorFault.DescriptorSizeFault,
                $"L7-SDC descriptor header size {headerSize} does not match v1 header size {HeaderSize}.");
        }

        uint descriptorSize = ReadUInt32(descriptorBytes, DescriptorSizeOffset);
        if (descriptorSize < HeaderSize || descriptorSize > descriptorBytes.Length)
        {
            return AcceleratorDescriptorStructuralReadResult.Fail(
                AcceleratorDescriptorFault.DescriptorSizeFault,
                "L7-SDC descriptor size is outside the provided descriptor buffer.");
        }

        ulong descriptorIdentityHash = ReadUInt64(descriptorBytes, DescriptorIdentityHashOffset);
        AcceleratorDescriptorReference effectiveReference =
            descriptorReference ??
            new AcceleratorDescriptorReference(
                DescriptorAddress: 0,
                DescriptorSize: descriptorSize,
                DescriptorIdentityHash: descriptorIdentityHash);

        if (effectiveReference.DescriptorSize != 0 &&
            effectiveReference.DescriptorSize < descriptorSize)
        {
            return AcceleratorDescriptorStructuralReadResult.Fail(
                AcceleratorDescriptorFault.DescriptorReferenceMismatch,
                "L7-SDC descriptor reference sideband does not cover the typed descriptor payload.");
        }

        if (effectiveReference.DescriptorIdentityHash != 0 &&
            effectiveReference.DescriptorIdentityHash != descriptorIdentityHash)
        {
            return AcceleratorDescriptorStructuralReadResult.Fail(
                AcceleratorDescriptorFault.DescriptorReferenceMismatch,
                "L7-SDC descriptor reference identity hash does not match the typed descriptor payload.");
        }

        var ownerBinding = new AcceleratorOwnerBinding
        {
            OwnerVirtualThreadId = ReadUInt16(descriptorBytes, OwnerVirtualThreadIdOffset),
            OwnerContextId = ReadUInt32(descriptorBytes, OwnerContextIdOffset),
            OwnerCoreId = ReadUInt32(descriptorBytes, OwnerCoreIdOffset),
            OwnerPodId = ReadUInt32(descriptorBytes, OwnerPodIdOffset),
            DomainTag = ReadUInt64(descriptorBytes, DomainTagOffset)
        };

        return AcceleratorDescriptorStructuralReadResult.Valid(
            effectiveReference,
            descriptorIdentityHash,
            descriptorSize,
            ownerBinding);
    }

    public static bool TryReadHeader(
        ReadOnlySpan<byte> descriptorBytes,
        AcceleratorDescriptorReference? descriptorReference,
        out AcceleratorDescriptorHeader header,
        out AcceleratorDescriptorReference effectiveReference,
        out AcceleratorDescriptorValidationResult? failure)
    {
        header = default;
        effectiveReference = default;
        failure = null;

        if (descriptorBytes.Length < HeaderSize)
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.DescriptorDecodeFault,
                "L7-SDC descriptor is shorter than the v1 fixed header.");
            return false;
        }

        uint magic = ReadUInt32(descriptorBytes, MagicOffset);
        if (magic != Magic)
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.DescriptorDecodeFault,
                "L7-SDC descriptor magic does not match v1.");
            return false;
        }

        ushort abiVersion = ReadUInt16(descriptorBytes, AbiVersionOffset);
        if (abiVersion != CurrentAbiVersion)
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.UnsupportedAbiVersion,
                $"Unsupported L7-SDC descriptor ABI version {abiVersion}.");
            return false;
        }

        ushort headerSize = ReadUInt16(descriptorBytes, HeaderSizeOffset);
        if (headerSize != HeaderSize)
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.DescriptorSizeFault,
                $"L7-SDC descriptor header size {headerSize} does not match v1 header size {HeaderSize}.");
            return false;
        }

        uint descriptorSize = ReadUInt32(descriptorBytes, DescriptorSizeOffset);
        if (descriptorSize < HeaderSize || descriptorSize > descriptorBytes.Length)
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.DescriptorSizeFault,
                "L7-SDC descriptor size is outside the provided descriptor buffer.");
            return false;
        }

        ulong descriptorIdentityHash = ReadUInt64(descriptorBytes, DescriptorIdentityHashOffset);
        ulong normalizedFootprintHash = ReadUInt64(descriptorBytes, NormalizedFootprintHashOffset);

        effectiveReference = descriptorReference ??
            new AcceleratorDescriptorReference(
                DescriptorAddress: 0,
                DescriptorSize: descriptorSize,
                DescriptorIdentityHash: descriptorIdentityHash);

        if (effectiveReference.DescriptorSize != 0 &&
            effectiveReference.DescriptorSize < descriptorSize)
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.DescriptorReferenceMismatch,
                "L7-SDC descriptor reference sideband does not cover the typed descriptor payload.");
            return false;
        }

        if (effectiveReference.DescriptorIdentityHash != 0 &&
            effectiveReference.DescriptorIdentityHash != descriptorIdentityHash)
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.DescriptorReferenceMismatch,
                "L7-SDC descriptor reference identity hash does not match the typed descriptor payload.");
            return false;
        }

        var acceleratorClass = (AcceleratorClassId)ReadUInt16(descriptorBytes, AcceleratorClassOffset);
        var acceleratorId = (AcceleratorDeviceId)ReadUInt16(descriptorBytes, AcceleratorIdOffset);
        var operation = (AcceleratorOperationKind)ReadUInt16(descriptorBytes, OperationOffset);
        var datatype = (AcceleratorDatatype)ReadUInt16(descriptorBytes, DatatypeOffset);
        var shape = (AcceleratorShapeKind)ReadUInt16(descriptorBytes, ShapeOffset);
        ushort shapeRank = ReadUInt16(descriptorBytes, ShapeRankOffset);
        ushort sourceRangeCount = ReadUInt16(descriptorBytes, SourceRangeCountOffset);
        ushort destinationRangeCount = ReadUInt16(descriptorBytes, DestinationRangeCountOffset);
        ushort scratchRangeCount = ReadUInt16(descriptorBytes, ScratchRangeCountOffset);
        var partialCompletionPolicy =
            (AcceleratorPartialCompletionPolicy)ReadUInt16(descriptorBytes, PartialCompletionPolicyOffset);
        ushort alignmentBytes = ReadUInt16(descriptorBytes, AlignmentBytesOffset);
        ulong elementCount = ReadUInt64(descriptorBytes, ElementCountOffset);
        uint capabilityVersion = ReadUInt32(descriptorBytes, CapabilityVersionOffset);
        ulong scratchRequiredBytes = ReadUInt64(descriptorBytes, ScratchRequiredBytesOffset);

        if (!ValidateDescriptorSize(
                descriptorSize,
                sourceRangeCount,
                destinationRangeCount,
                scratchRangeCount,
                out failure))
        {
            return false;
        }

        if (!ValidateRangeCount(sourceRangeCount, "source", out failure) ||
            !ValidateRangeCount(destinationRangeCount, "destination", out failure) ||
            !ValidateRangeCount(scratchRangeCount, "scratch", out failure))
        {
            return false;
        }

        if (!ValidateAlignment(alignmentBytes, out failure))
        {
            return false;
        }

        if (partialCompletionPolicy != AcceleratorPartialCompletionPolicy.AllOrNone)
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.UnsupportedPartialCompletionPolicy,
                "L7-SDC v1 accepts only all-or-none partial completion policy.");
            return false;
        }

        var ownerBinding = new AcceleratorOwnerBinding
        {
            OwnerVirtualThreadId = ReadUInt16(descriptorBytes, OwnerVirtualThreadIdOffset),
            OwnerContextId = ReadUInt32(descriptorBytes, OwnerContextIdOffset),
            OwnerCoreId = ReadUInt32(descriptorBytes, OwnerCoreIdOffset),
            OwnerPodId = ReadUInt32(descriptorBytes, OwnerPodIdOffset),
            DomainTag = ReadUInt64(descriptorBytes, DomainTagOffset)
        };

        header = new AcceleratorDescriptorHeader(
            magic,
            abiVersion,
            headerSize,
            descriptorSize,
            acceleratorClass,
            acceleratorId,
            operation,
            datatype,
            shape,
            shapeRank,
            sourceRangeCount,
            destinationRangeCount,
            scratchRangeCount,
            partialCompletionPolicy,
            new AcceleratorAlignmentRequirement(alignmentBytes),
            elementCount,
            capabilityVersion,
            new AcceleratorDescriptorIdentity(
                descriptorIdentityHash,
                normalizedFootprintHash),
            ownerBinding,
            scratchRequiredBytes);
        return true;
    }

    public static bool ValidateReservedZero(
        ReadOnlySpan<byte> descriptorBytes,
        out AcceleratorDescriptorValidationResult? failure)
    {
        failure = null;
        if (descriptorBytes.Length < HeaderSize)
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.DescriptorDecodeFault,
                "L7-SDC descriptor is shorter than the v1 fixed header.");
            return false;
        }

        if (HasAnyReservedBitSet(descriptorBytes))
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.ReservedFieldFault,
                "L7-SDC v1 descriptor flags and reserved fields must be zero.");
            return false;
        }

        return true;
    }

    public static bool TryValidateNativeVliwCarrier(
        in VLIW_Instruction instruction,
        ushort opcode,
        int slotIndex,
        bool hasDescriptorSideband,
        out AcceleratorCarrierValidationResult? failure)
    {
        if (!OpcodeRegistry.IsSystemDeviceCommandOpcode(opcode))
        {
            failure = AcceleratorCarrierValidationResult.Fail(
                AcceleratorDescriptorFault.DescriptorCarrierDecodeFault,
                $"Slot {slotIndex}: opcode '{OpcodeRegistry.GetMnemonicOrHex(opcode)}' is not a canonical native L7-SDC system-device command.");
            return false;
        }

        if (slotIndex != 7)
        {
            failure = AcceleratorCarrierValidationResult.Fail(
                AcceleratorDescriptorFault.DescriptorCarrierDecodeFault,
                $"Slot {slotIndex}: L7-SDC system-device command carriers are hard-pinned to physical lane7 (lane 7).");
            return false;
        }

        if ((instruction.Word3 & VLIW_Instruction.RetiredPolicyGapMask) != 0)
        {
            failure = AcceleratorCarrierValidationResult.Fail(
                AcceleratorDescriptorFault.DescriptorCarrierDecodeFault,
                $"Slot {slotIndex}: word3[50] is a retired policy gap and cannot carry L7-SDC ABI data.");
            return false;
        }

        if (instruction.Reserved != 0)
        {
            failure = AcceleratorCarrierValidationResult.Fail(
                AcceleratorDescriptorFault.DescriptorCarrierDecodeFault,
                $"Slot {slotIndex}: word0[47:40] is reserved and cannot carry L7-SDC ABI data.");
            return false;
        }

        if (instruction.VirtualThreadId != 0)
        {
            failure = AcceleratorCarrierValidationResult.Fail(
                AcceleratorDescriptorFault.DescriptorCarrierDecodeFault,
                $"Slot {slotIndex}: word3[49:48] VirtualThreadId is a transport hint, not L7-SDC owner/domain authority.");
            return false;
        }

        if (!VLIW_Instruction.TryUnpackArchRegs(
                instruction.Word1,
                out _,
                out _,
                out _))
        {
            failure = AcceleratorCarrierValidationResult.Fail(
                AcceleratorDescriptorFault.DescriptorCarrierDecodeFault,
                $"Slot {slotIndex}: word1 must be the packed architectural register tuple, not a raw pointer authority.");
            return false;
        }

        if (instruction.Src2Pointer != 0)
        {
            failure = AcceleratorCarrierValidationResult.Fail(
                AcceleratorDescriptorFault.DescriptorCarrierDecodeFault,
                $"Slot {slotIndex}: word2 raw pointer fields are not accepted as L7-SDC descriptor or owner authority.");
            return false;
        }

        if (opcode == Processor.CPU_Core.IsaOpcodeValues.ACCEL_SUBMIT &&
            !hasDescriptorSideband)
        {
            failure = AcceleratorCarrierValidationResult.Fail(
                AcceleratorDescriptorFault.DescriptorCarrierDecodeFault,
                "ACCEL_SUBMIT requires typed AcceleratorCommandDescriptor sideband.");
            return false;
        }

        failure = null;
        return true;
    }

    public static AcceleratorCarrierValidationResult ValidateNativeCarrier(
        in VLIW_Instruction instruction,
        ushort opcode,
        int slotIndex,
        bool hasDescriptorSideband)
    {
        return TryValidateNativeVliwCarrier(
            in instruction,
            opcode,
            slotIndex,
            hasDescriptorSideband,
            out AcceleratorCarrierValidationResult? failure)
            ? AcceleratorCarrierValidationResult.Valid()
            : failure!;
    }

    public static ulong ComputeDescriptorIdentityHash(ReadOnlySpan<byte> descriptorBytes)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        ulong hash = offsetBasis;
        for (int index = 0; index < descriptorBytes.Length; index++)
        {
            byte value =
                index >= DescriptorIdentityHashOffset &&
                index < DescriptorIdentityHashOffset + sizeof(ulong)
                    ? (byte)0
                    : descriptorBytes[index];
            unchecked
            {
                hash ^= value;
                hash *= prime;
            }
        }

        return hash == 0 ? offsetBasis : hash;
    }

    public static ulong ComputeNormalizedFootprintHash(
        AcceleratorClassId acceleratorClass,
        AcceleratorDeviceId acceleratorId,
        AcceleratorOperationKind operation,
        AcceleratorDatatype datatype,
        AcceleratorShapeKind shape,
        ushort shapeRank,
        ulong elementCount,
        AcceleratorPartialCompletionPolicy partialCompletionPolicy,
        AcceleratorAlignmentRequirement alignment,
        IReadOnlyList<AcceleratorMemoryRange> normalizedSourceRanges,
        IReadOnlyList<AcceleratorMemoryRange> normalizedDestinationRanges,
        IReadOnlyList<AcceleratorMemoryRange> normalizedScratchRanges)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        ulong hash = offsetBasis;
        Add((ushort)acceleratorClass);
        Add((ushort)acceleratorId);
        Add((ushort)operation);
        Add((ushort)datatype);
        Add((ushort)shape);
        Add(shapeRank);
        Add(elementCount);
        Add((ushort)partialCompletionPolicy);
        Add(alignment.MinimumAlignmentBytes);
        AddRanges(normalizedSourceRanges);
        AddRanges(normalizedDestinationRanges);
        AddRanges(normalizedScratchRanges);
        return hash == 0 ? offsetBasis : hash;

        void Add(ulong value)
        {
            unchecked
            {
                hash ^= value;
                hash *= prime;
            }
        }

        void AddRanges(IReadOnlyList<AcceleratorMemoryRange>? ranges)
        {
            Add((ulong)(ranges?.Count ?? 0));
            if (ranges is null)
            {
                return;
            }

            for (int index = 0; index < ranges.Count; index++)
            {
                Add(ranges[index].Address);
                Add(ranges[index].Length);
            }
        }
    }

    public static IReadOnlyList<AcceleratorMemoryRange> NormalizeMemoryRanges(
        IReadOnlyList<AcceleratorMemoryRange> ranges)
    {
        if (ranges is null || ranges.Count == 0)
        {
            return Array.Empty<AcceleratorMemoryRange>();
        }

        var sorted = new AcceleratorMemoryRange[ranges.Count];
        for (int index = 0; index < ranges.Count; index++)
        {
            sorted[index] = ranges[index];
        }

        Array.Sort(
            sorted,
            static (left, right) =>
            {
                int addressCompare = left.Address.CompareTo(right.Address);
                return addressCompare != 0
                    ? addressCompare
                    : left.Length.CompareTo(right.Length);
            });

        var merged = new List<AcceleratorMemoryRange>(sorted.Length);
        ulong currentStart = sorted[0].Address;
        ulong currentEnd = sorted[0].Address + sorted[0].Length;
        for (int index = 1; index < sorted.Length; index++)
        {
            AcceleratorMemoryRange next = sorted[index];
            ulong nextEnd = next.Address + next.Length;
            if (next.Address <= currentEnd)
            {
                if (nextEnd > currentEnd)
                {
                    currentEnd = nextEnd;
                }

                continue;
            }

            merged.Add(new AcceleratorMemoryRange(currentStart, currentEnd - currentStart));
            currentStart = next.Address;
            currentEnd = nextEnd;
        }

        merged.Add(new AcceleratorMemoryRange(currentStart, currentEnd - currentStart));
        return merged.ToArray();
    }

    private static bool ValidateCapabilityShape(
        AcceleratorClassId acceleratorClass,
        AcceleratorDeviceId acceleratorId,
        AcceleratorOperationKind operation,
        AcceleratorDatatype datatype,
        AcceleratorShapeKind shape,
        ushort shapeRank,
        ulong elementCount,
        out AcceleratorDescriptorValidationResult? failure)
    {
        failure = null;

        if (acceleratorClass != AcceleratorClassId.Matrix)
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.UnsupportedAcceleratorClass,
                "L7-SDC descriptor accelerator class is unsupported in v1.");
            return false;
        }

        if (acceleratorId != AcceleratorDeviceId.ReferenceMatMul)
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.UnsupportedAcceleratorId,
                "L7-SDC descriptor accelerator id is unsupported in v1.");
            return false;
        }

        if (operation != AcceleratorOperationKind.MatMul)
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.UnsupportedOperation,
                "L7-SDC descriptor operation is unsupported in v1.");
            return false;
        }

        if (datatype is not
            (AcceleratorDatatype.Float32 or AcceleratorDatatype.Float64 or AcceleratorDatatype.Int32))
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.UnsupportedDatatype,
                "L7-SDC descriptor datatype is unsupported in v1.");
            return false;
        }

        if (shape != AcceleratorShapeKind.Matrix2D ||
            shapeRank != 2 ||
            elementCount == 0)
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.UnsupportedShape,
                "L7-SDC v1 accepts only non-empty rank-2 matrix descriptors.");
            return false;
        }

        return true;
    }

    private static bool ValidateDescriptorSize(
        uint descriptorSize,
        ushort sourceRangeCount,
        ushort destinationRangeCount,
        ushort scratchRangeCount,
        out AcceleratorDescriptorValidationResult? failure)
    {
        failure = null;

        ulong rangeBytes =
            ((ulong)sourceRangeCount + destinationRangeCount + scratchRangeCount) *
            RangeEntrySize;
        ulong minimumSize = HeaderSize + rangeBytes;
        if (minimumSize > descriptorSize)
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.DescriptorSizeFault,
                "L7-SDC descriptor size does not cover the declared inline range tables.");
            return false;
        }

        return true;
    }

    private static bool ValidateRangeCount(
        ushort rangeCount,
        string rangeKind,
        out AcceleratorDescriptorValidationResult? failure)
    {
        failure = null;
        if (rangeCount > MaxRangeCount)
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.DescriptorDecodeFault,
                $"L7-SDC {rangeKind} range count exceeds the v1 parser limit.");
            return false;
        }

        return true;
    }

    private static bool ValidateAlignment(
        ushort alignmentBytes,
        out AcceleratorDescriptorValidationResult? failure)
    {
        failure = null;
        if (alignmentBytes == 0 ||
            (alignmentBytes & (alignmentBytes - 1)) != 0)
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.AlignmentFault,
                "L7-SDC alignment requirement must be a non-zero power of two.");
            return false;
        }

        return true;
    }

    private static bool TryReadRanges(
        ReadOnlySpan<byte> bytes,
        uint descriptorSize,
        uint tableOffset,
        ushort rangeCount,
        ushort alignmentBytes,
        bool requireNonEmpty,
        string rangeKind,
        out IReadOnlyList<AcceleratorMemoryRange> ranges,
        out AcceleratorDescriptorValidationResult? failure)
    {
        ranges = Array.Empty<AcceleratorMemoryRange>();
        failure = null;

        if (rangeCount == 0)
        {
            if (requireNonEmpty)
            {
                failure = AcceleratorDescriptorValidationResult.Fail(
                    AcceleratorDescriptorFault.DescriptorDecodeFault,
                    $"L7-SDC descriptor requires at least one {rangeKind} range.");
                return false;
            }

            if (tableOffset != 0)
            {
                failure = AcceleratorDescriptorValidationResult.Fail(
                    AcceleratorDescriptorFault.ReservedFieldFault,
                    $"L7-SDC {rangeKind} range table offset must be zero when the count is zero.");
                return false;
            }

            return true;
        }

        if ((tableOffset & 0x7) != 0)
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.AlignmentFault,
                $"L7-SDC {rangeKind} range table offset must be 8-byte aligned.");
            return false;
        }

        ulong tableBytes = (ulong)rangeCount * RangeEntrySize;
        ulong tableEnd = (ulong)tableOffset + tableBytes;
        if (tableEnd < tableOffset || tableEnd > descriptorSize)
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.RangeOverflow,
                $"L7-SDC {rangeKind} range table overflows the validated descriptor size.");
            return false;
        }

        var decodedRanges = new AcceleratorMemoryRange[rangeCount];
        for (int index = 0; index < rangeCount; index++)
        {
            int entryOffset = checked((int)tableOffset + (index * RangeEntrySize));
            ulong address = ReadUInt64(bytes, entryOffset);
            ulong length = ReadUInt64(bytes, entryOffset + sizeof(ulong));

            if (length == 0)
            {
                failure = AcceleratorDescriptorValidationResult.Fail(
                    AcceleratorDescriptorFault.ZeroLengthFault,
                    $"L7-SDC {rangeKind} memory ranges must have non-zero length.");
                return false;
            }

            if (address > ulong.MaxValue - length)
            {
                failure = AcceleratorDescriptorValidationResult.Fail(
                    AcceleratorDescriptorFault.RangeOverflow,
                    $"L7-SDC {rangeKind} memory range address + length overflows UInt64.");
                return false;
            }

            if ((address % alignmentBytes) != 0 ||
                (length % alignmentBytes) != 0)
            {
                failure = AcceleratorDescriptorValidationResult.Fail(
                    AcceleratorDescriptorFault.AlignmentFault,
                    $"L7-SDC {rangeKind} memory range does not satisfy the descriptor alignment requirement.");
                return false;
            }

            decodedRanges[index] = new AcceleratorMemoryRange(address, length);
        }

        ranges = decodedRanges;
        return true;
    }

    private static bool ValidateRangeTableLayout(
        uint descriptorSize,
        uint sourceOffset,
        ushort sourceCount,
        uint destinationOffset,
        ushort destinationCount,
        uint scratchOffset,
        ushort scratchCount,
        out AcceleratorDescriptorValidationResult? failure)
    {
        failure = null;

        var declaredTables = new List<RangeTableSpan>(3);
        if (!TryAddRangeTableSpan(
                declaredTables,
                "source",
                sourceOffset,
                sourceCount,
                descriptorSize,
                out failure) ||
            !TryAddRangeTableSpan(
                declaredTables,
                "destination",
                destinationOffset,
                destinationCount,
                descriptorSize,
                out failure) ||
            !TryAddRangeTableSpan(
                declaredTables,
                "scratch",
                scratchOffset,
                scratchCount,
                descriptorSize,
                out failure))
        {
            return false;
        }

        for (int leftIndex = 0; leftIndex < declaredTables.Count; leftIndex++)
        {
            RangeTableSpan left = declaredTables[leftIndex];
            for (int rightIndex = leftIndex + 1; rightIndex < declaredTables.Count; rightIndex++)
            {
                RangeTableSpan right = declaredTables[rightIndex];
                if (left.Start < right.End && right.Start < left.End)
                {
                    failure = AcceleratorDescriptorValidationResult.Fail(
                        AcceleratorDescriptorFault.DescriptorDecodeFault,
                        $"L7-SDC {left.Kind} and {right.Kind} range tables overlap.");
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryAddRangeTableSpan(
        List<RangeTableSpan> declaredTables,
        string rangeKind,
        uint tableOffset,
        ushort rangeCount,
        uint descriptorSize,
        out AcceleratorDescriptorValidationResult? failure)
    {
        failure = null;
        if (rangeCount == 0)
        {
            return true;
        }

        if (tableOffset < HeaderSize)
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.DescriptorDecodeFault,
                $"L7-SDC {rangeKind} range table must start after the fixed descriptor header.");
            return false;
        }

        ulong tableBytes = (ulong)rangeCount * RangeEntrySize;
        ulong tableEnd = (ulong)tableOffset + tableBytes;
        if (tableEnd < tableOffset || tableEnd > descriptorSize)
        {
            failure = AcceleratorDescriptorValidationResult.Fail(
                AcceleratorDescriptorFault.RangeOverflow,
                $"L7-SDC {rangeKind} range table overflows the validated descriptor size.");
            return false;
        }

        declaredTables.Add(new RangeTableSpan(rangeKind, tableOffset, tableEnd));
        return true;
    }

    private static bool ScratchRangesCoverRequirement(
        IReadOnlyList<AcceleratorMemoryRange> scratchRanges,
        ulong scratchRequiredBytes)
    {
        ulong total = 0;
        for (int index = 0; index < scratchRanges.Count; index++)
        {
            ulong next = total + scratchRanges[index].Length;
            if (next < total)
            {
                return false;
            }

            total = next;
        }

        return total >= scratchRequiredBytes;
    }

    private static bool HasAnyReservedBitSet(ReadOnlySpan<byte> bytes)
    {
        return ReadUInt32(bytes, FlagsOffset) != 0 ||
               ReadUInt16(bytes, Reserved1Offset) != 0 ||
               ReadUInt16(bytes, Reserved2Offset) != 0 ||
               ReadUInt32(bytes, Reserved3Offset) != 0 ||
               ReadUInt32(bytes, Reserved4Offset) != 0 ||
               ReadUInt64(bytes, Reserved5Offset) != 0;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, sizeof(ushort)));

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, sizeof(uint)));

    private static ulong ReadUInt64(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(offset, sizeof(ulong)));

    private readonly record struct RangeTableSpan(
        string Kind,
        ulong Start,
        ulong End);
}
