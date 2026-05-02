using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute
{
    public enum DmaStreamComputeDsc2ParserStatus : ushort
    {
        ParserOnly = 1,
        ExecutionDisabled = 2,
        ExecutionEligibleAfterFutureGates = 3
    }

    public enum DmaStreamComputeDsc2ExecutionState : byte
    {
        ParserOnlyExecutionDisabled = 0
    }

    public enum DmaStreamComputeDsc2AddressSpaceKind : ushort
    {
        Physical = 0,
        IommuTranslated = 1,
        ReservedFuture = 2
    }

    public enum DmaStreamComputeDsc2AccessKind : ushort
    {
        Read = 1,
        Write = 2,
        ReadWrite = 3
    }

    public enum DmaStreamComputeDsc2CapabilityId : ushort
    {
        None = 0,
        CapabilityProfile = 1,
        AddressSpace = 2,
        StridedRange = 3,
        Tile2D = 4,
        ScatterGather = 5,
        FootprintSummary = 6,
        PartialCompletionPolicy = 7,
        NonSemanticMetadata = 8
    }

    [Flags]
    public enum DmaStreamComputeDsc2CapabilityStage : ushort
    {
        None = 0,
        ParserKnown = 1 << 0,
        Validation = 1 << 1,
        FootprintNormalization = 1 << 2,
        BackendAddressSpace = 1 << 3,
        Execution = 1 << 4,
        CompilerLowering = 1 << 5
    }

    public enum DmaStreamComputeDsc2ExtensionType : ushort
    {
        CapabilityProfile = 1,
        AddressSpace = 2,
        StridedRange = 3,
        Tile2D = 4,
        ScatterGather = 5,
        FootprintSummary = 6,
        PartialCompletionPolicy = 7,
        VendorNonSemanticMetadata = 0x7FFF
    }

    [Flags]
    public enum DmaStreamComputeDsc2ExtensionFlags : ushort
    {
        None = 0,
        Required = 1 << 0,
        Semantic = 1 << 1,
        NonSemantic = 1 << 2
    }

    public enum DmaStreamComputeDsc2FootprintOutcomeKind : byte
    {
        None = 0,
        Exact = 1,
        Conservative = 2,
        Rejected = 3
    }

    public readonly record struct DmaStreamComputeDsc2CapabilityGrant
    {
        public DmaStreamComputeDsc2CapabilityGrant(
            DmaStreamComputeDsc2CapabilityId capabilityId,
            DmaStreamComputeDsc2CapabilityStage stages)
        {
            CapabilityId = capabilityId;
            Stages = stages;
        }

        public DmaStreamComputeDsc2CapabilityId CapabilityId { get; }

        public DmaStreamComputeDsc2CapabilityStage Stages { get; }
    }

    public sealed class DmaStreamComputeDsc2CapabilitySet
    {
        private readonly Dictionary<DmaStreamComputeDsc2CapabilityId, DmaStreamComputeDsc2CapabilityStage> _grants;

        private DmaStreamComputeDsc2CapabilitySet(
            IReadOnlyList<DmaStreamComputeDsc2CapabilityGrant> grants)
        {
            _grants = new Dictionary<DmaStreamComputeDsc2CapabilityId, DmaStreamComputeDsc2CapabilityStage>();
            if (grants is null)
            {
                return;
            }

            for (int index = 0; index < grants.Count; index++)
            {
                DmaStreamComputeDsc2CapabilityGrant grant = grants[index];
                if (grant.CapabilityId == DmaStreamComputeDsc2CapabilityId.None ||
                    grant.Stages == DmaStreamComputeDsc2CapabilityStage.None)
                {
                    continue;
                }

                _grants[grant.CapabilityId] = _grants.TryGetValue(
                    grant.CapabilityId,
                    out DmaStreamComputeDsc2CapabilityStage existing)
                    ? existing | grant.Stages
                    : grant.Stages;
            }
        }

        public static DmaStreamComputeDsc2CapabilitySet Empty { get; } =
            new(Array.Empty<DmaStreamComputeDsc2CapabilityGrant>());

        public static DmaStreamComputeDsc2CapabilitySet ParserOnlyFoundation { get; } =
            Create(
                new DmaStreamComputeDsc2CapabilityGrant(
                    DmaStreamComputeDsc2CapabilityId.CapabilityProfile,
                    DmaStreamComputeDsc2CapabilityStage.ParserKnown |
                    DmaStreamComputeDsc2CapabilityStage.Validation),
                new DmaStreamComputeDsc2CapabilityGrant(
                    DmaStreamComputeDsc2CapabilityId.AddressSpace,
                    DmaStreamComputeDsc2CapabilityStage.ParserKnown |
                    DmaStreamComputeDsc2CapabilityStage.Validation |
                    DmaStreamComputeDsc2CapabilityStage.BackendAddressSpace),
                new DmaStreamComputeDsc2CapabilityGrant(
                    DmaStreamComputeDsc2CapabilityId.StridedRange,
                    DmaStreamComputeDsc2CapabilityStage.ParserKnown |
                    DmaStreamComputeDsc2CapabilityStage.Validation |
                    DmaStreamComputeDsc2CapabilityStage.FootprintNormalization),
                new DmaStreamComputeDsc2CapabilityGrant(
                    DmaStreamComputeDsc2CapabilityId.Tile2D,
                    DmaStreamComputeDsc2CapabilityStage.ParserKnown |
                    DmaStreamComputeDsc2CapabilityStage.Validation |
                    DmaStreamComputeDsc2CapabilityStage.FootprintNormalization),
                new DmaStreamComputeDsc2CapabilityGrant(
                    DmaStreamComputeDsc2CapabilityId.ScatterGather,
                    DmaStreamComputeDsc2CapabilityStage.ParserKnown |
                    DmaStreamComputeDsc2CapabilityStage.Validation |
                    DmaStreamComputeDsc2CapabilityStage.FootprintNormalization),
                new DmaStreamComputeDsc2CapabilityGrant(
                    DmaStreamComputeDsc2CapabilityId.FootprintSummary,
                    DmaStreamComputeDsc2CapabilityStage.ParserKnown |
                    DmaStreamComputeDsc2CapabilityStage.Validation |
                    DmaStreamComputeDsc2CapabilityStage.FootprintNormalization),
                new DmaStreamComputeDsc2CapabilityGrant(
                    DmaStreamComputeDsc2CapabilityId.NonSemanticMetadata,
                    DmaStreamComputeDsc2CapabilityStage.ParserKnown));

        public IReadOnlyList<DmaStreamComputeDsc2CapabilityGrant> Grants
        {
            get
            {
                var grants = new DmaStreamComputeDsc2CapabilityGrant[_grants.Count];
                int cursor = 0;
                foreach (KeyValuePair<DmaStreamComputeDsc2CapabilityId, DmaStreamComputeDsc2CapabilityStage> entry in _grants)
                {
                    grants[cursor++] = new DmaStreamComputeDsc2CapabilityGrant(entry.Key, entry.Value);
                }

                return Array.AsReadOnly(grants);
            }
        }

        public bool GrantsExecution =>
            HasAnyStage(DmaStreamComputeDsc2CapabilityStage.Execution);

        public bool GrantsCompilerLowering =>
            HasAnyStage(DmaStreamComputeDsc2CapabilityStage.CompilerLowering);

        public static DmaStreamComputeDsc2CapabilitySet Create(
            params DmaStreamComputeDsc2CapabilityGrant[] grants) =>
            new(grants);

        public bool Has(
            DmaStreamComputeDsc2CapabilityId capabilityId,
            DmaStreamComputeDsc2CapabilityStage requiredStages) =>
            _grants.TryGetValue(capabilityId, out DmaStreamComputeDsc2CapabilityStage stages) &&
            (stages & requiredStages) == requiredStages;

        private bool HasAnyStage(DmaStreamComputeDsc2CapabilityStage stage)
        {
            foreach (DmaStreamComputeDsc2CapabilityStage stages in _grants.Values)
            {
                if ((stages & stage) != 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed record DmaStreamComputeDsc2ParsedExtension
    {
        public required DmaStreamComputeDsc2ExtensionType ExtensionType { get; init; }

        public required ushort Version { get; init; }

        public required DmaStreamComputeDsc2ExtensionFlags Flags { get; init; }

        public required uint ByteLength { get; init; }

        public required DmaStreamComputeDsc2CapabilityId CapabilityId { get; init; }

        public required DmaStreamComputeDsc2FootprintOutcomeKind FootprintOutcome { get; init; }
    }

    public sealed record DmaStreamComputeDsc2FootprintModel
    {
        public DmaStreamComputeDsc2FootprintModel(
            IReadOnlyList<DmaStreamComputeMemoryRange> readRanges,
            IReadOnlyList<DmaStreamComputeMemoryRange> writeRanges,
            DmaStreamComputeDsc2FootprintOutcomeKind outcomeKind,
            ulong normalizedFootprintHash)
        {
            ReadRanges = FreezeRanges(readRanges);
            WriteRanges = FreezeRanges(writeRanges);
            OutcomeKind = outcomeKind;
            NormalizedFootprintHash = normalizedFootprintHash;
        }

        public IReadOnlyList<DmaStreamComputeMemoryRange> ReadRanges { get; }

        public IReadOnlyList<DmaStreamComputeMemoryRange> WriteRanges { get; }

        public DmaStreamComputeDsc2FootprintOutcomeKind OutcomeKind { get; }

        public ulong NormalizedFootprintHash { get; }

        public bool IsExact => OutcomeKind == DmaStreamComputeDsc2FootprintOutcomeKind.Exact;

        public bool IsConservative => OutcomeKind == DmaStreamComputeDsc2FootprintOutcomeKind.Conservative;

        private static IReadOnlyList<DmaStreamComputeMemoryRange> FreezeRanges(
            IReadOnlyList<DmaStreamComputeMemoryRange>? ranges)
        {
            if (ranges is null || ranges.Count == 0)
            {
                return Array.Empty<DmaStreamComputeMemoryRange>();
            }

            var copy = new DmaStreamComputeMemoryRange[ranges.Count];
            for (int index = 0; index < ranges.Count; index++)
            {
                copy[index] = ranges[index];
            }

            return Array.AsReadOnly(copy);
        }
    }

    public sealed record DmaStreamComputeDsc2Descriptor
    {
        public required DmaStreamComputeDescriptorReference DescriptorReference { get; init; }

        public required ushort MajorVersion { get; init; }

        public required ushort MinorVersion { get; init; }

        public required ushort HeaderSize { get; init; }

        public required uint TotalSize { get; init; }

        public required ulong DescriptorIdentityHash { get; init; }

        public required ulong CapabilitySetHash { get; init; }

        public required DmaStreamComputeOwnerBinding OwnerBinding { get; init; }

        public required DmaStreamComputeOwnerGuardDecision OwnerGuardDecision { get; init; }

        public required DmaStreamComputeDsc2ParserStatus ParserStatus { get; init; }

        public required DmaStreamComputeDsc2ExecutionState ExecutionState { get; init; }

        public required DmaStreamComputeDsc2AddressSpaceKind AddressSpace { get; init; }

        public required ulong MappingEpoch { get; init; }

        public required DmaStreamComputeDsc2CapabilitySet CapabilitySet { get; init; }

        public required IReadOnlyList<DmaStreamComputeDsc2ParsedExtension> Extensions { get; init; }

        public required DmaStreamComputeDsc2FootprintModel NormalizedFootprint { get; init; }

        public bool IsParserOnly => true;

        public bool ExecutionEnabled => false;

        public bool CanIssueToken => false;

        public bool CanPublishMemory => false;

        public bool CanProductionLower => false;
    }

    public sealed record DmaStreamComputeDsc2ValidationResult
    {
        private DmaStreamComputeDsc2ValidationResult(
            DmaStreamComputeValidationFault fault,
            DmaStreamComputeDsc2Descriptor? descriptor,
            string message)
        {
            Fault = fault;
            Descriptor = descriptor;
            Message = string.IsNullOrWhiteSpace(message)
                ? $"DmaStreamCompute DSC2 parser-only validation result: {fault}."
                : message;
        }

        public bool IsParserAccepted =>
            Fault == DmaStreamComputeValidationFault.None && Descriptor is not null;

        public DmaStreamComputeValidationFault Fault { get; }

        public DmaStreamComputeDsc2Descriptor? Descriptor { get; }

        public string Message { get; }

        public bool ExecutionEnabled => false;

        public bool CanIssueToken => false;

        public bool CanPublishMemory => false;

        public bool CanProductionLower => false;

        public static DmaStreamComputeDsc2ValidationResult Accepted(
            DmaStreamComputeDsc2Descriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            return new DmaStreamComputeDsc2ValidationResult(
                DmaStreamComputeValidationFault.None,
                descriptor,
                "DSC2 descriptor accepted for parser-only validation; execution, token issue, memory publication, and compiler lowering remain disabled.");
        }

        public static DmaStreamComputeDsc2ValidationResult Fail(
            DmaStreamComputeValidationFault fault,
            string message)
        {
            if (fault == DmaStreamComputeValidationFault.None)
            {
                throw new ArgumentException(
                    "Use Accepted for successful DSC2 parser-only validation.",
                    nameof(fault));
            }

            return new DmaStreamComputeDsc2ValidationResult(fault, null, message);
        }

        public DmaStreamComputeDsc2Descriptor RequireParserOnlyDescriptor()
        {
            if (IsParserAccepted && Descriptor is not null)
            {
                return Descriptor;
            }

            throw new InvalidOperationException(
                $"DmaStreamCompute DSC2 descriptor is not parser-accepted: {Fault}. {Message}");
        }
    }

    public static partial class DmaStreamComputeDescriptorParser
    {
        public const uint Dsc2Magic = 0x32435344; // "DSC2" as a little-endian scalar.
        public const ushort Dsc2MajorVersion = 2;
        public const ushort Dsc2MinorVersion = 0;
        public const int Dsc2HeaderSize = 128;
        public const int Dsc2ExtensionBlockHeaderSize = 32;

        private const int Dsc2MagicOffset = 0;
        private const int Dsc2MajorVersionOffset = 4;
        private const int Dsc2MinorVersionOffset = 6;
        private const int Dsc2HeaderSizeOffset = 8;
        private const int Dsc2HeaderFlagsOffset = 10;
        private const int Dsc2TotalSizeOffset = 12;
        private const int Dsc2ExtensionTableOffsetOffset = 16;
        private const int Dsc2ExtensionCountOffset = 20;
        private const int Dsc2Reserved0Offset = 22;
        private const int Dsc2ExtensionTableByteSizeOffset = 24;
        private const int Dsc2DescriptorFlagsOffset = 28;
        private const int Dsc2DescriptorIdentityHashOffset = 32;
        private const int Dsc2CapabilitySetHashOffset = 40;
        private const int Dsc2NormalizedFootprintHashOffset = 48;
        private const int Dsc2OwnerVirtualThreadIdOffset = 56;
        private const int Dsc2ParserStatusOffset = 58;
        private const int Dsc2OwnerContextIdOffset = 60;
        private const int Dsc2OwnerCoreIdOffset = 64;
        private const int Dsc2OwnerPodIdOffset = 68;
        private const int Dsc2DeviceIdOffset = 72;
        private const int Dsc2AddressSpaceSummaryOffset = 76;
        private const int Dsc2Reserved1Offset = 78;
        private const int Dsc2OwnerDomainTagOffset = 80;
        private const int Dsc2MappingEpochOffset = 88;
        private const int Dsc2Reserved2Offset = 96;
        private const int Dsc2Reserved3Offset = 104;
        private const int Dsc2Reserved4Offset = 112;
        private const int Dsc2Reserved5Offset = 120;

        private const int Dsc2ExtensionTypeOffset = 0;
        private const int Dsc2ExtensionVersionOffset = 2;
        private const int Dsc2ExtensionFlagsOffset = 4;
        private const int Dsc2ExtensionAlignmentOffset = 6;
        private const int Dsc2ExtensionLengthOffset = 8;
        private const int Dsc2ExtensionCapabilityIdOffset = 12;
        private const int Dsc2ExtensionMinimumParserMajorOffset = 14;
        private const int Dsc2ExtensionPayloadChecksumOffset = 16;
        private const int Dsc2ExtensionReserved0Offset = 20;
        private const int Dsc2ExtensionReserved1Offset = 24;

        private const int StridedRangePayloadSize = 32;
        private const int Tile2DPayloadSize = 40;
        private const int AddressSpacePayloadSize = 32;
        private const int FootprintSummaryPayloadSize = 48;
        private const int ScatterGatherPayloadHeaderSize = 16;
        private const int ScatterGatherSegmentEntrySize = 16;
        private const int MaxExactRangeCount = 64;
        private const int MaxScatterGatherSegmentCount = 128;
        private const ulong MaxConservativeFootprintSpanBytes = 1UL << 32;

        public static DmaStreamComputeStructuralReadResult ReadDsc2StructuralOwnerBinding(
            ReadOnlySpan<byte> descriptorBytes,
            DmaStreamComputeDescriptorReference? descriptorReference = null)
        {
            if (descriptorBytes.Length < Dsc2HeaderSize)
            {
                return DmaStreamComputeStructuralReadResult.Fail(
                    DmaStreamComputeValidationFault.DescriptorDecodeFault,
                    "DSC2 descriptor is shorter than the fixed parser-only header.");
            }

            if (ReadUInt32(descriptorBytes, Dsc2MagicOffset) != Dsc2Magic)
            {
                return DmaStreamComputeStructuralReadResult.Fail(
                    DmaStreamComputeValidationFault.DescriptorDecodeFault,
                    "Descriptor magic does not match DmaStreamCompute DSC2.");
            }

            ushort majorVersion = ReadUInt16(descriptorBytes, Dsc2MajorVersionOffset);
            if (majorVersion != Dsc2MajorVersion)
            {
                return DmaStreamComputeStructuralReadResult.Fail(
                    DmaStreamComputeValidationFault.UnsupportedAbiVersion,
                    $"Unsupported DmaStreamCompute DSC2 major version {majorVersion}.");
            }

            ushort headerSize = ReadUInt16(descriptorBytes, Dsc2HeaderSizeOffset);
            if (headerSize != Dsc2HeaderSize)
            {
                return DmaStreamComputeStructuralReadResult.Fail(
                    DmaStreamComputeValidationFault.DescriptorDecodeFault,
                    $"DSC2 descriptor header size {headerSize} does not match parser-only header size {Dsc2HeaderSize}.");
            }

            uint totalSize = ReadUInt32(descriptorBytes, Dsc2TotalSizeOffset);
            if (totalSize < Dsc2HeaderSize || totalSize > descriptorBytes.Length)
            {
                return DmaStreamComputeStructuralReadResult.Fail(
                    DmaStreamComputeValidationFault.DescriptorDecodeFault,
                    "DSC2 descriptor total size is outside the provided descriptor buffer.");
            }

            ulong descriptorIdentityHash = ReadUInt64(descriptorBytes, Dsc2DescriptorIdentityHashOffset);
            DmaStreamComputeDescriptorReference effectiveReference =
                descriptorReference ??
                new DmaStreamComputeDescriptorReference(
                    descriptorAddress: 0,
                    descriptorSize: totalSize,
                    descriptorIdentityHash);

            if (effectiveReference.DescriptorSize != 0 &&
                effectiveReference.DescriptorSize < totalSize)
            {
                return DmaStreamComputeStructuralReadResult.Fail(
                    DmaStreamComputeValidationFault.DescriptorReferenceLost,
                    "DSC2 descriptor sideband reference does not cover the validated descriptor payload.");
            }

            if (effectiveReference.DescriptorIdentityHash != 0 &&
                effectiveReference.DescriptorIdentityHash != descriptorIdentityHash)
            {
                return DmaStreamComputeStructuralReadResult.Fail(
                    DmaStreamComputeValidationFault.DescriptorReferenceLost,
                    "DSC2 descriptor sideband identity hash does not match the validated payload.");
            }

            var ownerBinding = new DmaStreamComputeOwnerBinding
            {
                OwnerVirtualThreadId = ReadUInt16(descriptorBytes, Dsc2OwnerVirtualThreadIdOffset),
                OwnerContextId = ReadUInt32(descriptorBytes, Dsc2OwnerContextIdOffset),
                OwnerCoreId = ReadUInt32(descriptorBytes, Dsc2OwnerCoreIdOffset),
                OwnerPodId = ReadUInt32(descriptorBytes, Dsc2OwnerPodIdOffset),
                DeviceId = ReadUInt32(descriptorBytes, Dsc2DeviceIdOffset),
                OwnerDomainTag = ReadUInt64(descriptorBytes, Dsc2OwnerDomainTagOffset)
            };

            return DmaStreamComputeStructuralReadResult.Valid(
                effectiveReference,
                descriptorIdentityHash,
                totalSize,
                ownerBinding);
        }

        public static DmaStreamComputeDsc2ValidationResult ParseDsc2ParserOnly(
            ReadOnlySpan<byte> descriptorBytes,
            DmaStreamComputeDsc2CapabilitySet capabilitySet,
            DmaStreamComputeOwnerGuardDecision ownerGuardDecision,
            DmaStreamComputeDescriptorReference? descriptorReference = null)
        {
            ArgumentNullException.ThrowIfNull(capabilitySet);

            DmaStreamComputeStructuralReadResult structuralRead =
                ReadDsc2StructuralOwnerBinding(descriptorBytes, descriptorReference);
            if (!structuralRead.IsValid)
            {
                return DmaStreamComputeDsc2ValidationResult.Fail(
                    structuralRead.Fault,
                    structuralRead.Message);
            }

            DmaStreamComputeOwnerBinding structuralOwnerBinding =
                structuralRead.RequireOwnerBindingForGuard();
            if (!ValidateOwnerGuard(
                    structuralOwnerBinding,
                    ownerGuardDecision,
                    out DmaStreamComputeDsc2ValidationResult? ownerFailure))
            {
                return ownerFailure!;
            }

            if (ReadUInt16(descriptorBytes, Dsc2MinorVersionOffset) != Dsc2MinorVersion)
            {
                return DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.UnsupportedAbiVersion,
                    "DSC2 parser-only support accepts only ABI minor version 0.");
            }

            if (HasAnyDsc2ReservedHeaderBitSet(descriptorBytes))
            {
                return DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.ReservedFieldFault,
                    "DSC2 parser-only header reserved fields and descriptor flags must be zero.");
            }

            ushort parserStatusRaw = ReadUInt16(descriptorBytes, Dsc2ParserStatusOffset);
            var parserStatus = (DmaStreamComputeDsc2ParserStatus)parserStatusRaw;
            if (parserStatus is not (
                DmaStreamComputeDsc2ParserStatus.ParserOnly or
                DmaStreamComputeDsc2ParserStatus.ExecutionDisabled))
            {
                return DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.ExecutionDisabled,
                    "DSC2 Phase07 accepts only parser-only or execution-disabled descriptors.");
            }

            if (!TryDecodeDsc2AddressSpace(
                    ReadUInt16(descriptorBytes, Dsc2AddressSpaceSummaryOffset),
                    out DmaStreamComputeDsc2AddressSpaceKind addressSpace))
            {
                return DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.AddressSpaceFault,
                    "DSC2 address-space summary is unsupported or reserved.");
            }

            uint extensionTableOffset = ReadUInt32(descriptorBytes, Dsc2ExtensionTableOffsetOffset);
            ushort extensionCount = ReadUInt16(descriptorBytes, Dsc2ExtensionCountOffset);
            uint extensionTableByteSize = ReadUInt32(descriptorBytes, Dsc2ExtensionTableByteSizeOffset);
            if (!ValidateExtensionTable(
                    structuralRead.TotalSize,
                    extensionTableOffset,
                    extensionCount,
                    extensionTableByteSize,
                    out string extensionTableFailure))
            {
                return DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.MalformedExtension,
                    extensionTableFailure);
            }

            var parsedExtensions = new List<DmaStreamComputeDsc2ParsedExtension>(extensionCount);
            var footprintBuilder = new Dsc2FootprintBuilder();
            Dsc2FootprintSummary? footprintSummary = null;
            bool hasAddressSpaceExtension = false;
            ulong mappingEpoch = ReadUInt64(descriptorBytes, Dsc2MappingEpochOffset);

            int cursor = checked((int)extensionTableOffset);
            int tableEnd = checked(cursor + (int)extensionTableByteSize);
            for (int extensionIndex = 0; extensionIndex < extensionCount; extensionIndex++)
            {
                if (!TryParseExtensionHeader(
                        descriptorBytes,
                        cursor,
                        tableEnd,
                        out Dsc2ExtensionHeader header,
                        out DmaStreamComputeDsc2ValidationResult? headerFailure))
                {
                    return headerFailure!;
                }

                if (!TryHandleExtension(
                        descriptorBytes.Slice(
                            cursor + Dsc2ExtensionBlockHeaderSize,
                            checked((int)header.ByteLength - Dsc2ExtensionBlockHeaderSize)),
                        header,
                        structuralOwnerBinding,
                        addressSpace,
                        capabilitySet,
                        footprintBuilder,
                        ref footprintSummary,
                        ref hasAddressSpaceExtension,
                        ref mappingEpoch,
                        parsedExtensions,
                        out DmaStreamComputeDsc2ValidationResult? extensionFailure))
                {
                    return extensionFailure!;
                }

                cursor += checked((int)header.ByteLength);
            }

            if (cursor != tableEnd)
            {
                return DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.MalformedExtension,
                    "DSC2 extension table byte size does not match the parsed extension block stream.");
            }

            if (addressSpace == DmaStreamComputeDsc2AddressSpaceKind.IommuTranslated && !hasAddressSpaceExtension)
            {
                return DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.AddressSpaceFault,
                    "DSC2 IOMMU-translated address-space selection requires an explicit AddressSpace extension.");
            }

            if (!footprintBuilder.TryBuild(
                    footprintSummary,
                    out DmaStreamComputeDsc2FootprintModel? footprint,
                    out DmaStreamComputeDsc2ValidationResult? footprintFailure))
            {
                return footprintFailure!;
            }

            ulong headerFootprintHash = ReadUInt64(descriptorBytes, Dsc2NormalizedFootprintHashOffset);
            if (headerFootprintHash != 0 &&
                headerFootprintHash != footprint!.NormalizedFootprintHash)
            {
                return DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.UnderApproximatedFootprintFault,
                    "DSC2 header normalized footprint hash does not match deterministic parser-only normalization.");
            }

            var descriptor = new DmaStreamComputeDsc2Descriptor
            {
                DescriptorReference = structuralRead.DescriptorReference,
                MajorVersion = Dsc2MajorVersion,
                MinorVersion = Dsc2MinorVersion,
                HeaderSize = Dsc2HeaderSize,
                TotalSize = structuralRead.TotalSize,
                DescriptorIdentityHash = structuralRead.DescriptorIdentityHash,
                CapabilitySetHash = ReadUInt64(descriptorBytes, Dsc2CapabilitySetHashOffset),
                OwnerBinding = structuralOwnerBinding,
                OwnerGuardDecision = ownerGuardDecision,
                ParserStatus = parserStatus,
                ExecutionState = DmaStreamComputeDsc2ExecutionState.ParserOnlyExecutionDisabled,
                AddressSpace = addressSpace,
                MappingEpoch = mappingEpoch,
                CapabilitySet = capabilitySet,
                Extensions = parsedExtensions.AsReadOnly(),
                NormalizedFootprint = footprint!
            };

            return DmaStreamComputeDsc2ValidationResult.Accepted(descriptor);
        }

        private static bool ValidateOwnerGuard(
            DmaStreamComputeOwnerBinding structuralOwnerBinding,
            DmaStreamComputeOwnerGuardDecision ownerGuardDecision,
            out DmaStreamComputeDsc2ValidationResult? failure)
        {
            failure = null;
            if (!ownerGuardDecision.IsAllowed)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.OwnerDomainFault,
                    ownerGuardDecision.Message);
                return false;
            }

            if (ownerGuardDecision.LegalityDecision.AuthoritySource != LegalityAuthoritySource.GuardPlane ||
                ownerGuardDecision.LegalityDecision.AttemptedReplayCertificateReuse)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.OwnerDomainFault,
                    "DSC2 owner guard decision must come from the guard plane before replay or certificate reuse.");
                return false;
            }

            if (ownerGuardDecision.DescriptorOwnerBinding is null ||
                !ownerGuardDecision.DescriptorOwnerBinding.Equals(structuralOwnerBinding))
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.OwnerDomainFault,
                    "DSC2 owner guard decision does not match structurally-read descriptor owner fields.");
                return false;
            }

            return true;
        }

        private static bool HasAnyDsc2ReservedHeaderBitSet(ReadOnlySpan<byte> bytes)
        {
            return ReadUInt16(bytes, Dsc2HeaderFlagsOffset) != 0 ||
                   ReadUInt16(bytes, Dsc2Reserved0Offset) != 0 ||
                   ReadUInt32(bytes, Dsc2DescriptorFlagsOffset) != 0 ||
                   ReadUInt16(bytes, Dsc2Reserved1Offset) != 0 ||
                   ReadUInt64(bytes, Dsc2Reserved2Offset) != 0 ||
                   ReadUInt64(bytes, Dsc2Reserved3Offset) != 0 ||
                   ReadUInt64(bytes, Dsc2Reserved4Offset) != 0 ||
                   ReadUInt64(bytes, Dsc2Reserved5Offset) != 0;
        }

        private static bool TryDecodeDsc2AddressSpace(
            ushort raw,
            out DmaStreamComputeDsc2AddressSpaceKind addressSpace)
        {
            addressSpace = (DmaStreamComputeDsc2AddressSpaceKind)raw;
            return addressSpace is
                DmaStreamComputeDsc2AddressSpaceKind.Physical or
                DmaStreamComputeDsc2AddressSpaceKind.IommuTranslated;
        }

        private static bool ValidateExtensionTable(
            uint totalSize,
            uint extensionTableOffset,
            ushort extensionCount,
            uint extensionTableByteSize,
            out string message)
        {
            message = string.Empty;
            if (extensionCount == 0)
            {
                message = "DSC2 parser-only descriptors require at least one extension block.";
                return false;
            }

            if ((extensionTableOffset & 0x7) != 0 ||
                extensionTableOffset < Dsc2HeaderSize)
            {
                message = "DSC2 extension table offset must be 8-byte aligned and outside the fixed header.";
                return false;
            }

            if ((extensionTableByteSize & 0x7) != 0 ||
                extensionTableByteSize < (uint)extensionCount * Dsc2ExtensionBlockHeaderSize)
            {
                message = "DSC2 extension table byte size is not a valid aligned block table.";
                return false;
            }

            ulong tableEnd = (ulong)extensionTableOffset + extensionTableByteSize;
            if (tableEnd < extensionTableOffset || tableEnd > totalSize)
            {
                message = "DSC2 extension table overflows the validated descriptor size.";
                return false;
            }

            return true;
        }

        private static bool TryParseExtensionHeader(
            ReadOnlySpan<byte> bytes,
            int offset,
            int tableEnd,
            out Dsc2ExtensionHeader header,
            out DmaStreamComputeDsc2ValidationResult? failure)
        {
            header = default;
            failure = null;

            if (offset < 0 ||
                offset > tableEnd - Dsc2ExtensionBlockHeaderSize)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.MalformedExtension,
                    "DSC2 extension block header is outside the extension table.");
                return false;
            }

            ushort rawType = ReadUInt16(bytes, offset + Dsc2ExtensionTypeOffset);
            ushort version = ReadUInt16(bytes, offset + Dsc2ExtensionVersionOffset);
            var flags = (DmaStreamComputeDsc2ExtensionFlags)ReadUInt16(bytes, offset + Dsc2ExtensionFlagsOffset);
            ushort alignment = ReadUInt16(bytes, offset + Dsc2ExtensionAlignmentOffset);
            uint byteLength = ReadUInt32(bytes, offset + Dsc2ExtensionLengthOffset);
            var capabilityId = (DmaStreamComputeDsc2CapabilityId)ReadUInt16(bytes, offset + Dsc2ExtensionCapabilityIdOffset);
            ushort minimumParserMajor = ReadUInt16(bytes, offset + Dsc2ExtensionMinimumParserMajorOffset);
            uint payloadChecksum = ReadUInt32(bytes, offset + Dsc2ExtensionPayloadChecksumOffset);
            ulong reserved0 = ReadUInt64(bytes, offset + Dsc2ExtensionReserved0Offset);
            ulong reserved1 = ReadUInt64(bytes, offset + Dsc2ExtensionReserved1Offset);

            if (!IsPowerOfTwoAlignment(alignment) ||
                alignment > 16 ||
                (offset % alignment) != 0)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.MalformedExtension,
                    "DSC2 extension block alignment is malformed or not satisfied.");
                return false;
            }

            if (byteLength < Dsc2ExtensionBlockHeaderSize ||
                (byteLength & 0x7) != 0 ||
                (ulong)offset + byteLength > (ulong)tableEnd)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.MalformedExtension,
                    "DSC2 extension block length is malformed or outside the extension table.");
                return false;
            }

            if (minimumParserMajor > Dsc2MajorVersion)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.UnsupportedCapability,
                    "DSC2 extension requires a newer parser major version.");
                return false;
            }

            if (payloadChecksum != 0 || reserved0 != 0 || reserved1 != 0)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.MalformedExtension,
                    "DSC2 Phase07 extension blocks use deterministic validation rules; checksum and reserved fields must be zero.");
                return false;
            }

            header = new Dsc2ExtensionHeader(
                rawType,
                version,
                flags,
                alignment,
                byteLength,
                capabilityId,
                minimumParserMajor);
            return true;
        }

        private static bool TryHandleExtension(
            ReadOnlySpan<byte> payload,
            Dsc2ExtensionHeader header,
            DmaStreamComputeOwnerBinding ownerBinding,
            DmaStreamComputeDsc2AddressSpaceKind headerAddressSpace,
            DmaStreamComputeDsc2CapabilitySet capabilitySet,
            Dsc2FootprintBuilder footprintBuilder,
            ref Dsc2FootprintSummary? footprintSummary,
            ref bool hasAddressSpaceExtension,
            ref ulong mappingEpoch,
            List<DmaStreamComputeDsc2ParsedExtension> parsedExtensions,
            out DmaStreamComputeDsc2ValidationResult? failure)
        {
            failure = null;
            bool known = Enum.IsDefined(typeof(DmaStreamComputeDsc2ExtensionType), header.ExtensionType);
            if (!known)
            {
                if ((header.Flags & DmaStreamComputeDsc2ExtensionFlags.Required) != 0 ||
                    (header.Flags & DmaStreamComputeDsc2ExtensionFlags.Semantic) != 0 ||
                    (header.Flags & DmaStreamComputeDsc2ExtensionFlags.NonSemantic) == 0)
                {
                    failure = DmaStreamComputeDsc2ValidationResult.Fail(
                        DmaStreamComputeValidationFault.UnsupportedCapability,
                        "Unknown DSC2 required or semantic extension rejects in parser-only mode.");
                    return false;
                }

                parsedExtensions.Add(CreateParsedExtension(header, DmaStreamComputeDsc2FootprintOutcomeKind.None));
                return true;
            }

            if (header.ExtensionType == DmaStreamComputeDsc2ExtensionType.PartialCompletionPolicy)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.UnsupportedCapability,
                    "DSC2 partial-success or partial-completion policy extension is reserved for a future ADR and remains disabled in parser-only mode.");
                return false;
            }

            if (!ValidateKnownExtensionCapability(header, capabilitySet, out failure))
            {
                return false;
            }

            if (header.Version != 1)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.UnsupportedCapability,
                    "DSC2 Phase07 known extensions accept only extension version 1.");
                return false;
            }

            DmaStreamComputeDsc2FootprintOutcomeKind outcome = DmaStreamComputeDsc2FootprintOutcomeKind.None;
            switch (header.ExtensionType)
            {
                case DmaStreamComputeDsc2ExtensionType.CapabilityProfile:
                    if (payload.Length != 0)
                    {
                        failure = DmaStreamComputeDsc2ValidationResult.Fail(
                            DmaStreamComputeValidationFault.MalformedExtension,
                            "DSC2 CapabilityProfile extension is header-only in Phase07.");
                        return false;
                    }

                    break;

                case DmaStreamComputeDsc2ExtensionType.AddressSpace:
                    if (!TryParseAddressSpaceExtension(
                            payload,
                            ownerBinding,
                            headerAddressSpace,
                            out mappingEpoch,
                            out failure))
                    {
                        return false;
                    }

                    hasAddressSpaceExtension = true;
                    break;

                case DmaStreamComputeDsc2ExtensionType.StridedRange:
                    if (!TryParseStridedRange(payload, footprintBuilder, out outcome, out failure))
                    {
                        return false;
                    }

                    break;

                case DmaStreamComputeDsc2ExtensionType.Tile2D:
                    if (!TryParseTile2D(payload, footprintBuilder, out outcome, out failure))
                    {
                        return false;
                    }

                    break;

                case DmaStreamComputeDsc2ExtensionType.ScatterGather:
                    if (!TryParseScatterGather(payload, footprintBuilder, out outcome, out failure))
                    {
                        return false;
                    }

                    break;

                case DmaStreamComputeDsc2ExtensionType.FootprintSummary:
                    if (!TryParseFootprintSummary(payload, out footprintSummary, out failure))
                    {
                        return false;
                    }

                    break;

                case DmaStreamComputeDsc2ExtensionType.VendorNonSemanticMetadata:
                    if ((header.Flags & DmaStreamComputeDsc2ExtensionFlags.NonSemantic) == 0 ||
                        (header.Flags & DmaStreamComputeDsc2ExtensionFlags.Semantic) != 0)
                    {
                        failure = DmaStreamComputeDsc2ValidationResult.Fail(
                            DmaStreamComputeValidationFault.UnsupportedCapability,
                            "DSC2 non-semantic metadata extension must be explicitly non-semantic.");
                        return false;
                    }

                    break;

                default:
                    failure = DmaStreamComputeDsc2ValidationResult.Fail(
                        DmaStreamComputeValidationFault.UnsupportedCapability,
                        "Unhandled DSC2 extension type.");
                    return false;
            }

            parsedExtensions.Add(CreateParsedExtension(header, outcome));
            return true;
        }

        private static bool ValidateKnownExtensionCapability(
            Dsc2ExtensionHeader header,
            DmaStreamComputeDsc2CapabilitySet capabilitySet,
            out DmaStreamComputeDsc2ValidationResult? failure)
        {
            failure = null;
            DmaStreamComputeDsc2CapabilityId expectedCapability = header.ExtensionType switch
            {
                DmaStreamComputeDsc2ExtensionType.CapabilityProfile => DmaStreamComputeDsc2CapabilityId.CapabilityProfile,
                DmaStreamComputeDsc2ExtensionType.AddressSpace => DmaStreamComputeDsc2CapabilityId.AddressSpace,
                DmaStreamComputeDsc2ExtensionType.StridedRange => DmaStreamComputeDsc2CapabilityId.StridedRange,
                DmaStreamComputeDsc2ExtensionType.Tile2D => DmaStreamComputeDsc2CapabilityId.Tile2D,
                DmaStreamComputeDsc2ExtensionType.ScatterGather => DmaStreamComputeDsc2CapabilityId.ScatterGather,
                DmaStreamComputeDsc2ExtensionType.FootprintSummary => DmaStreamComputeDsc2CapabilityId.FootprintSummary,
                DmaStreamComputeDsc2ExtensionType.PartialCompletionPolicy => DmaStreamComputeDsc2CapabilityId.PartialCompletionPolicy,
                DmaStreamComputeDsc2ExtensionType.VendorNonSemanticMetadata => DmaStreamComputeDsc2CapabilityId.NonSemanticMetadata,
                _ => DmaStreamComputeDsc2CapabilityId.None
            };

            if (header.CapabilityId != expectedCapability)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.UnsupportedCapability,
                    "DSC2 extension capability id does not match the extension family.");
                return false;
            }

            DmaStreamComputeDsc2CapabilityStage requiredStages = expectedCapability switch
            {
                DmaStreamComputeDsc2CapabilityId.AddressSpace =>
                    DmaStreamComputeDsc2CapabilityStage.ParserKnown |
                    DmaStreamComputeDsc2CapabilityStage.Validation |
                    DmaStreamComputeDsc2CapabilityStage.BackendAddressSpace,

                DmaStreamComputeDsc2CapabilityId.StridedRange or
                DmaStreamComputeDsc2CapabilityId.Tile2D or
                DmaStreamComputeDsc2CapabilityId.ScatterGather or
                DmaStreamComputeDsc2CapabilityId.FootprintSummary =>
                    DmaStreamComputeDsc2CapabilityStage.ParserKnown |
                    DmaStreamComputeDsc2CapabilityStage.Validation |
                    DmaStreamComputeDsc2CapabilityStage.FootprintNormalization,

                DmaStreamComputeDsc2CapabilityId.NonSemanticMetadata =>
                    DmaStreamComputeDsc2CapabilityStage.ParserKnown,

                _ =>
                    DmaStreamComputeDsc2CapabilityStage.ParserKnown |
                    DmaStreamComputeDsc2CapabilityStage.Validation
            };

            if (!capabilitySet.Has(expectedCapability, requiredStages))
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.UnsupportedCapability,
                    $"DSC2 extension {header.ExtensionType} requires absent capability stage(s): {requiredStages}.");
                return false;
            }

            return true;
        }

        private static bool TryParseAddressSpaceExtension(
            ReadOnlySpan<byte> payload,
            DmaStreamComputeOwnerBinding ownerBinding,
            DmaStreamComputeDsc2AddressSpaceKind headerAddressSpace,
            out ulong mappingEpoch,
            out DmaStreamComputeDsc2ValidationResult? failure)
        {
            mappingEpoch = 0;
            failure = null;
            if (payload.Length != AddressSpacePayloadSize)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.MalformedExtension,
                    "DSC2 AddressSpace extension payload length is malformed.");
                return false;
            }

            if (!TryDecodeDsc2AddressSpace(
                    ReadUInt16(payload, 0),
                    out DmaStreamComputeDsc2AddressSpaceKind extensionAddressSpace) ||
                extensionAddressSpace != headerAddressSpace)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.AddressSpaceFault,
                    "DSC2 AddressSpace extension does not match the descriptor address-space summary.");
                return false;
            }

            if (ReadUInt16(payload, 2) != 0 ||
                ReadUInt32(payload, 24) != 0 ||
                ReadUInt32(payload, 28) != 0)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.MalformedExtension,
                    "DSC2 AddressSpace extension reserved fields must be zero.");
                return false;
            }

            uint deviceId = ReadUInt32(payload, 4);
            ulong domainTag = ReadUInt64(payload, 8);
            mappingEpoch = ReadUInt64(payload, 16);

            if (deviceId != ownerBinding.DeviceId)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.AddressSpaceFault,
                    "DSC2 AddressSpace extension device binding does not match descriptor owner binding.");
                return false;
            }

            if (domainTag != ownerBinding.OwnerDomainTag)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.AddressSpaceFault,
                    "DSC2 AddressSpace extension domain binding does not match descriptor owner binding.");
                return false;
            }

            if (extensionAddressSpace == DmaStreamComputeDsc2AddressSpaceKind.IommuTranslated &&
                mappingEpoch == 0)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.AddressSpaceFault,
                    "DSC2 IOMMU-translated AddressSpace extension requires a non-zero mapping epoch.");
                return false;
            }

            return true;
        }

        private static bool TryParseStridedRange(
            ReadOnlySpan<byte> payload,
            Dsc2FootprintBuilder footprintBuilder,
            out DmaStreamComputeDsc2FootprintOutcomeKind outcome,
            out DmaStreamComputeDsc2ValidationResult? failure)
        {
            outcome = DmaStreamComputeDsc2FootprintOutcomeKind.None;
            failure = null;
            if (payload.Length != StridedRangePayloadSize)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.MalformedExtension,
                    "DSC2 StridedRange extension payload length is malformed.");
                return false;
            }

            if (!TryDecodeAccessKind(ReadUInt16(payload, 0), out DmaStreamComputeDsc2AccessKind accessKind))
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.MalformedExtension,
                    "DSC2 footprint extension access kind is unsupported.");
                return false;
            }

            if (!TryValidateElementSize(ReadUInt16(payload, 2), out ulong elementSize, out failure))
            {
                return false;
            }

            uint elementCount = ReadUInt32(payload, 4);
            ulong baseAddress = ReadUInt64(payload, 8);
            ulong strideBytes = ReadUInt64(payload, 16);
            ulong reserved = ReadUInt64(payload, 24);
            if (reserved != 0)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.MalformedExtension,
                    "DSC2 StridedRange reserved fields must be zero.");
                return false;
            }

            return footprintBuilder.TryAddStridedRange(
                accessKind,
                baseAddress,
                elementCount,
                elementSize,
                strideBytes,
                out outcome,
                out failure);
        }

        private static bool TryParseTile2D(
            ReadOnlySpan<byte> payload,
            Dsc2FootprintBuilder footprintBuilder,
            out DmaStreamComputeDsc2FootprintOutcomeKind outcome,
            out DmaStreamComputeDsc2ValidationResult? failure)
        {
            outcome = DmaStreamComputeDsc2FootprintOutcomeKind.None;
            failure = null;
            if (payload.Length != Tile2DPayloadSize)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.MalformedExtension,
                    "DSC2 Tile2D extension payload length is malformed.");
                return false;
            }

            if (!TryDecodeAccessKind(ReadUInt16(payload, 0), out DmaStreamComputeDsc2AccessKind accessKind))
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.MalformedExtension,
                    "DSC2 footprint extension access kind is unsupported.");
                return false;
            }

            if (!TryValidateElementSize(ReadUInt16(payload, 2), out ulong elementSize, out failure))
            {
                return false;
            }

            uint rows = ReadUInt32(payload, 4);
            uint columns = ReadUInt32(payload, 8);
            if (ReadUInt32(payload, 12) != 0)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.MalformedExtension,
                    "DSC2 Tile2D reserved fields must be zero.");
                return false;
            }

            return footprintBuilder.TryAddTile2D(
                accessKind,
                ReadUInt64(payload, 16),
                rows,
                columns,
                elementSize,
                ReadUInt64(payload, 24),
                ReadUInt64(payload, 32),
                out outcome,
                out failure);
        }

        private static bool TryParseScatterGather(
            ReadOnlySpan<byte> payload,
            Dsc2FootprintBuilder footprintBuilder,
            out DmaStreamComputeDsc2FootprintOutcomeKind outcome,
            out DmaStreamComputeDsc2ValidationResult? failure)
        {
            outcome = DmaStreamComputeDsc2FootprintOutcomeKind.None;
            failure = null;
            if (payload.Length < ScatterGatherPayloadHeaderSize)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.MalformedExtension,
                    "DSC2 ScatterGather extension payload header is malformed.");
                return false;
            }

            if (!TryDecodeAccessKind(ReadUInt16(payload, 0), out DmaStreamComputeDsc2AccessKind accessKind))
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.MalformedExtension,
                    "DSC2 footprint extension access kind is unsupported.");
                return false;
            }

            if (!TryValidateElementSize(ReadUInt16(payload, 2), out ulong elementSize, out failure))
            {
                return false;
            }

            uint segmentCount = ReadUInt32(payload, 4);
            uint segmentTableBytes = ReadUInt32(payload, 8);
            if (ReadUInt32(payload, 12) != 0)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.MalformedExtension,
                    "DSC2 ScatterGather reserved fields must be zero.");
                return false;
            }

            if (segmentCount == 0 ||
                segmentCount > MaxScatterGatherSegmentCount ||
                segmentTableBytes != segmentCount * ScatterGatherSegmentEntrySize ||
                payload.Length != ScatterGatherPayloadHeaderSize + segmentTableBytes)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.FootprintNormalizationFault,
                    "DSC2 ScatterGather segment table count or byte size is outside Phase07 parser-only bounds.");
                return false;
            }

            var segments = new DmaStreamComputeMemoryRange[segmentCount];
            int segmentOffset = ScatterGatherPayloadHeaderSize;
            for (int index = 0; index < segmentCount; index++)
            {
                ulong address = ReadUInt64(payload, segmentOffset);
                ulong length = ReadUInt64(payload, segmentOffset + sizeof(ulong));
                if (!ValidateRange(address, length, elementSize, out failure))
                {
                    return false;
                }

                segments[index] = new DmaStreamComputeMemoryRange(address, length);
                segmentOffset += ScatterGatherSegmentEntrySize;
            }

            return footprintBuilder.TryAddScatterGather(
                accessKind,
                segments,
                out outcome,
                out failure);
        }

        private static bool TryParseFootprintSummary(
            ReadOnlySpan<byte> payload,
            out Dsc2FootprintSummary? summary,
            out DmaStreamComputeDsc2ValidationResult? failure)
        {
            summary = null;
            failure = null;
            if (payload.Length != FootprintSummaryPayloadSize)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.MalformedExtension,
                    "DSC2 FootprintSummary extension payload length is malformed.");
                return false;
            }

            var outcome = (DmaStreamComputeDsc2FootprintOutcomeKind)ReadUInt16(payload, 0);
            if (outcome is not (
                DmaStreamComputeDsc2FootprintOutcomeKind.Exact or
                DmaStreamComputeDsc2FootprintOutcomeKind.Conservative) ||
                ReadUInt16(payload, 2) != 0 ||
                ReadUInt32(payload, 4) != 0 ||
                ReadUInt64(payload, 40) != 0)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.MalformedExtension,
                    "DSC2 FootprintSummary outcome or reserved fields are malformed.");
                return false;
            }

            summary = new Dsc2FootprintSummary(
                outcome,
                new DmaStreamComputeMemoryRange(ReadUInt64(payload, 8), ReadUInt64(payload, 16)),
                new DmaStreamComputeMemoryRange(ReadUInt64(payload, 24), ReadUInt64(payload, 32)));
            return true;
        }

        private static bool TryDecodeAccessKind(
            ushort raw,
            out DmaStreamComputeDsc2AccessKind accessKind)
        {
            accessKind = (DmaStreamComputeDsc2AccessKind)raw;
            return accessKind is
                DmaStreamComputeDsc2AccessKind.Read or
                DmaStreamComputeDsc2AccessKind.Write or
                DmaStreamComputeDsc2AccessKind.ReadWrite;
        }

        private static bool TryValidateElementSize(
            ushort rawElementSize,
            out ulong elementSize,
            out DmaStreamComputeDsc2ValidationResult? failure)
        {
            elementSize = rawElementSize;
            failure = null;
            if (elementSize == 0 || (elementSize & (elementSize - 1)) != 0)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.AlignmentFault,
                    "DSC2 footprint element size must be a non-zero power of two.");
                return false;
            }

            return true;
        }

        private static bool ValidateRange(
            ulong address,
            ulong length,
            ulong elementSize,
            out DmaStreamComputeDsc2ValidationResult? failure)
        {
            failure = null;
            if (length == 0)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.ZeroLengthFault,
                    "DSC2 footprint ranges must have non-zero length.");
                return false;
            }

            if (address > ulong.MaxValue - length)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.RangeOverflow,
                    "DSC2 footprint range address + length overflows UInt64.");
                return false;
            }

            if ((address % elementSize) != 0 || (length % elementSize) != 0)
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.AlignmentFault,
                    "DSC2 footprint range is not aligned to the element size.");
                return false;
            }

            return true;
        }

        private static bool IsPowerOfTwoAlignment(ushort alignment) =>
            alignment != 0 && (alignment & (alignment - 1)) == 0;

        private static DmaStreamComputeDsc2ParsedExtension CreateParsedExtension(
            Dsc2ExtensionHeader header,
            DmaStreamComputeDsc2FootprintOutcomeKind outcome) =>
            new()
            {
                ExtensionType = header.ExtensionType,
                Version = header.Version,
                Flags = header.Flags,
                ByteLength = header.ByteLength,
                CapabilityId = header.CapabilityId,
                FootprintOutcome = outcome
            };

        private readonly record struct Dsc2ExtensionHeader(
            ushort RawType,
            ushort Version,
            DmaStreamComputeDsc2ExtensionFlags Flags,
            ushort Alignment,
            uint ByteLength,
            DmaStreamComputeDsc2CapabilityId CapabilityId,
            ushort MinimumParserMajor)
        {
            public DmaStreamComputeDsc2ExtensionType ExtensionType =>
                (DmaStreamComputeDsc2ExtensionType)RawType;
        }

        private readonly record struct Dsc2FootprintSummary(
            DmaStreamComputeDsc2FootprintOutcomeKind OutcomeKind,
            DmaStreamComputeMemoryRange ReadRange,
            DmaStreamComputeMemoryRange WriteRange);

        private sealed class Dsc2FootprintBuilder
        {
            private readonly List<DmaStreamComputeMemoryRange> _readRanges = new();
            private readonly List<DmaStreamComputeMemoryRange> _writeRanges = new();
            private bool _hasConservativeRange;

            public bool TryAddStridedRange(
                DmaStreamComputeDsc2AccessKind accessKind,
                ulong baseAddress,
                uint elementCount,
                ulong elementSize,
                ulong strideBytes,
                out DmaStreamComputeDsc2FootprintOutcomeKind outcome,
                out DmaStreamComputeDsc2ValidationResult? failure)
            {
                outcome = DmaStreamComputeDsc2FootprintOutcomeKind.None;
                failure = null;
                if (elementCount == 0)
                {
                    failure = DmaStreamComputeDsc2ValidationResult.Fail(
                        DmaStreamComputeValidationFault.ZeroLengthFault,
                        "DSC2 StridedRange element count must be non-zero.");
                    return false;
                }

                if ((baseAddress % elementSize) != 0 ||
                    (strideBytes % elementSize) != 0)
                {
                    failure = DmaStreamComputeDsc2ValidationResult.Fail(
                        DmaStreamComputeValidationFault.AlignmentFault,
                        "DSC2 StridedRange base and stride must be aligned to the element size.");
                    return false;
                }

                ulong lastIndex = elementCount - 1UL;
                if (!TryMultiply(lastIndex, strideBytes, out ulong lastDelta) ||
                    !TryAdd(baseAddress, lastDelta, out ulong lastAddress) ||
                    !TryAdd(lastAddress, elementSize, out ulong lastEnd))
                {
                    failure = DmaStreamComputeDsc2ValidationResult.Fail(
                        DmaStreamComputeValidationFault.RangeOverflow,
                        "DSC2 StridedRange address calculation overflows UInt64.");
                    return false;
                }

                if (strideBytes == elementSize)
                {
                    ulong length = (ulong)elementCount * elementSize;
                    if (!ValidateRange(baseAddress, length, elementSize, out failure))
                    {
                        return false;
                    }

                    AddRange(accessKind, new DmaStreamComputeMemoryRange(baseAddress, length));
                    outcome = DmaStreamComputeDsc2FootprintOutcomeKind.Exact;
                    return true;
                }

                if (elementCount <= MaxExactRangeCount)
                {
                    for (uint index = 0; index < elementCount; index++)
                    {
                        if (!TryMultiply(index, strideBytes, out ulong delta) ||
                            !TryAdd(baseAddress, delta, out ulong address))
                        {
                            failure = DmaStreamComputeDsc2ValidationResult.Fail(
                                DmaStreamComputeValidationFault.RangeOverflow,
                                "DSC2 StridedRange exact normalization overflows UInt64.");
                            return false;
                        }

                        AddRange(accessKind, new DmaStreamComputeMemoryRange(address, elementSize));
                    }

                    outcome = DmaStreamComputeDsc2FootprintOutcomeKind.Exact;
                    return true;
                }

                ulong span = lastEnd - baseAddress;
                if (span > MaxConservativeFootprintSpanBytes)
                {
                    failure = DmaStreamComputeDsc2ValidationResult.Fail(
                        DmaStreamComputeValidationFault.FootprintNormalizationFault,
                        "DSC2 StridedRange conservative footprint exceeds Phase07 bounds.");
                    return false;
                }

                AddRange(accessKind, new DmaStreamComputeMemoryRange(baseAddress, span));
                _hasConservativeRange = true;
                outcome = DmaStreamComputeDsc2FootprintOutcomeKind.Conservative;
                return true;
            }

            public bool TryAddTile2D(
                DmaStreamComputeDsc2AccessKind accessKind,
                ulong baseAddress,
                uint rows,
                uint columns,
                ulong elementSize,
                ulong rowStrideBytes,
                ulong columnStrideBytes,
                out DmaStreamComputeDsc2FootprintOutcomeKind outcome,
                out DmaStreamComputeDsc2ValidationResult? failure)
            {
                outcome = DmaStreamComputeDsc2FootprintOutcomeKind.None;
                failure = null;
                if (rows == 0 || columns == 0)
                {
                    failure = DmaStreamComputeDsc2ValidationResult.Fail(
                        DmaStreamComputeValidationFault.ZeroLengthFault,
                        "DSC2 Tile2D row and column counts must be non-zero.");
                    return false;
                }

                if ((baseAddress % elementSize) != 0 ||
                    (rowStrideBytes % elementSize) != 0 ||
                    (columnStrideBytes % elementSize) != 0)
                {
                    failure = DmaStreamComputeDsc2ValidationResult.Fail(
                        DmaStreamComputeValidationFault.AlignmentFault,
                        "DSC2 Tile2D base, row stride, and column stride must be aligned to the element size.");
                    return false;
                }

                if (!TryMultiply(columns, elementSize, out ulong contiguousRowBytes))
                {
                    failure = DmaStreamComputeDsc2ValidationResult.Fail(
                        DmaStreamComputeValidationFault.RangeOverflow,
                        "DSC2 Tile2D contiguous row byte calculation overflows UInt64.");
                    return false;
                }

                if (columnStrideBytes == elementSize &&
                    rowStrideBytes == contiguousRowBytes)
                {
                    if (!TryMultiply(rows, contiguousRowBytes, out ulong length) ||
                        !ValidateRange(baseAddress, length, elementSize, out failure))
                    {
                        failure ??= DmaStreamComputeDsc2ValidationResult.Fail(
                            DmaStreamComputeValidationFault.RangeOverflow,
                            "DSC2 Tile2D contiguous footprint overflows UInt64.");
                        return false;
                    }

                    AddRange(accessKind, new DmaStreamComputeMemoryRange(baseAddress, length));
                    outcome = DmaStreamComputeDsc2FootprintOutcomeKind.Exact;
                    return true;
                }

                if (!TryMultiply(rows - 1UL, rowStrideBytes, out ulong lastRowDelta) ||
                    !TryMultiply(columns - 1UL, columnStrideBytes, out ulong lastColumnDelta) ||
                    !TryAdd(lastRowDelta, lastColumnDelta, out ulong lastDelta) ||
                    !TryAdd(baseAddress, lastDelta, out ulong lastAddress) ||
                    !TryAdd(lastAddress, elementSize, out ulong maxEnd))
                {
                    failure = DmaStreamComputeDsc2ValidationResult.Fail(
                        DmaStreamComputeValidationFault.RangeOverflow,
                        "DSC2 Tile2D address calculation overflows UInt64.");
                    return false;
                }

                ulong elementTotal = (ulong)rows * columns;
                if (elementTotal <= MaxExactRangeCount)
                {
                    for (uint row = 0; row < rows; row++)
                    {
                        if (!TryMultiply(row, rowStrideBytes, out ulong rowDelta))
                        {
                            failure = DmaStreamComputeDsc2ValidationResult.Fail(
                                DmaStreamComputeValidationFault.RangeOverflow,
                                "DSC2 Tile2D exact row calculation overflows UInt64.");
                            return false;
                        }

                        for (uint column = 0; column < columns; column++)
                        {
                            if (!TryMultiply(column, columnStrideBytes, out ulong columnDelta) ||
                                !TryAdd(baseAddress, rowDelta, out ulong rowAddress) ||
                                !TryAdd(rowAddress, columnDelta, out ulong address))
                            {
                                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                                    DmaStreamComputeValidationFault.RangeOverflow,
                                    "DSC2 Tile2D exact element calculation overflows UInt64.");
                                return false;
                            }

                            AddRange(accessKind, new DmaStreamComputeMemoryRange(address, elementSize));
                        }
                    }

                    outcome = DmaStreamComputeDsc2FootprintOutcomeKind.Exact;
                    return true;
                }

                ulong span = maxEnd - baseAddress;
                if (span > MaxConservativeFootprintSpanBytes)
                {
                    failure = DmaStreamComputeDsc2ValidationResult.Fail(
                        DmaStreamComputeValidationFault.FootprintNormalizationFault,
                        "DSC2 Tile2D conservative footprint exceeds Phase07 bounds.");
                    return false;
                }

                AddRange(accessKind, new DmaStreamComputeMemoryRange(baseAddress, span));
                _hasConservativeRange = true;
                outcome = DmaStreamComputeDsc2FootprintOutcomeKind.Conservative;
                return true;
            }

            public bool TryAddScatterGather(
                DmaStreamComputeDsc2AccessKind accessKind,
                IReadOnlyList<DmaStreamComputeMemoryRange> segments,
                out DmaStreamComputeDsc2FootprintOutcomeKind outcome,
                out DmaStreamComputeDsc2ValidationResult? failure)
            {
                outcome = DmaStreamComputeDsc2FootprintOutcomeKind.None;
                failure = null;
                if (segments.Count <= MaxExactRangeCount)
                {
                    for (int index = 0; index < segments.Count; index++)
                    {
                        AddRange(accessKind, segments[index]);
                    }

                    outcome = DmaStreamComputeDsc2FootprintOutcomeKind.Exact;
                    return true;
                }

                ulong minStart = ulong.MaxValue;
                ulong maxEnd = 0;
                for (int index = 0; index < segments.Count; index++)
                {
                    DmaStreamComputeMemoryRange range = segments[index];
                    ulong end = range.Address + range.Length;
                    if (range.Address < minStart)
                    {
                        minStart = range.Address;
                    }

                    if (end > maxEnd)
                    {
                        maxEnd = end;
                    }
                }

                ulong span = maxEnd - minStart;
                if (span > MaxConservativeFootprintSpanBytes)
                {
                    failure = DmaStreamComputeDsc2ValidationResult.Fail(
                        DmaStreamComputeValidationFault.FootprintNormalizationFault,
                        "DSC2 ScatterGather conservative footprint exceeds Phase07 bounds.");
                    return false;
                }

                AddRange(accessKind, new DmaStreamComputeMemoryRange(minStart, span));
                _hasConservativeRange = true;
                outcome = DmaStreamComputeDsc2FootprintOutcomeKind.Conservative;
                return true;
            }

            public bool TryBuild(
                Dsc2FootprintSummary? summary,
                out DmaStreamComputeDsc2FootprintModel? footprint,
                out DmaStreamComputeDsc2ValidationResult? failure)
            {
                footprint = null;
                failure = null;
                if (_readRanges.Count == 0 && _writeRanges.Count == 0)
                {
                    failure = DmaStreamComputeDsc2ValidationResult.Fail(
                        DmaStreamComputeValidationFault.FootprintNormalizationFault,
                        "DSC2 parser-only descriptor did not produce a normalized read or write footprint.");
                    return false;
                }

                IReadOnlyList<DmaStreamComputeMemoryRange> normalizedReadRanges =
                    NormalizeRanges(_readRanges);
                IReadOnlyList<DmaStreamComputeMemoryRange> normalizedWriteRanges =
                    NormalizeRanges(_writeRanges);
                DmaStreamComputeDsc2FootprintOutcomeKind outcome =
                    _hasConservativeRange
                        ? DmaStreamComputeDsc2FootprintOutcomeKind.Conservative
                        : DmaStreamComputeDsc2FootprintOutcomeKind.Exact;

                if (summary is not null &&
                    !ValidateFootprintSummary(
                        normalizedReadRanges,
                        normalizedWriteRanges,
                        summary.Value,
                        out failure))
                {
                    return false;
                }

                footprint = new DmaStreamComputeDsc2FootprintModel(
                    normalizedReadRanges,
                    normalizedWriteRanges,
                    outcome,
                    ComputeDsc2FootprintHash(normalizedReadRanges, normalizedWriteRanges, outcome));
                return true;
            }

            private void AddRange(
                DmaStreamComputeDsc2AccessKind accessKind,
                DmaStreamComputeMemoryRange range)
            {
                if (accessKind is DmaStreamComputeDsc2AccessKind.Read or DmaStreamComputeDsc2AccessKind.ReadWrite)
                {
                    _readRanges.Add(range);
                }

                if (accessKind is DmaStreamComputeDsc2AccessKind.Write or DmaStreamComputeDsc2AccessKind.ReadWrite)
                {
                    _writeRanges.Add(range);
                }
            }
        }

        private static bool ValidateFootprintSummary(
            IReadOnlyList<DmaStreamComputeMemoryRange> readRanges,
            IReadOnlyList<DmaStreamComputeMemoryRange> writeRanges,
            Dsc2FootprintSummary summary,
            out DmaStreamComputeDsc2ValidationResult? failure)
        {
            failure = null;
            if (!SummaryCovers(readRanges, summary.ReadRange) ||
                !SummaryCovers(writeRanges, summary.WriteRange))
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.UnderApproximatedFootprintFault,
                    "DSC2 FootprintSummary under-approximates parser-normalized memory ranges.");
                return false;
            }

            if (summary.OutcomeKind == DmaStreamComputeDsc2FootprintOutcomeKind.Exact &&
                (!SummaryExactlyMatches(readRanges, summary.ReadRange) ||
                 !SummaryExactlyMatches(writeRanges, summary.WriteRange)))
            {
                failure = DmaStreamComputeDsc2ValidationResult.Fail(
                    DmaStreamComputeValidationFault.UnderApproximatedFootprintFault,
                    "DSC2 exact FootprintSummary must match parser-normalized ranges exactly.");
                return false;
            }

            return true;
        }

        private static bool SummaryCovers(
            IReadOnlyList<DmaStreamComputeMemoryRange> ranges,
            DmaStreamComputeMemoryRange summaryRange)
        {
            if (ranges.Count == 0)
            {
                return summaryRange.Length == 0;
            }

            if (summaryRange.Length == 0 ||
                summaryRange.Address > ulong.MaxValue - summaryRange.Length)
            {
                return false;
            }

            ulong summaryEnd = summaryRange.Address + summaryRange.Length;
            for (int index = 0; index < ranges.Count; index++)
            {
                DmaStreamComputeMemoryRange range = ranges[index];
                ulong rangeEnd = range.Address + range.Length;
                if (range.Address < summaryRange.Address || rangeEnd > summaryEnd)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SummaryExactlyMatches(
            IReadOnlyList<DmaStreamComputeMemoryRange> ranges,
            DmaStreamComputeMemoryRange summaryRange)
        {
            if (ranges.Count == 0)
            {
                return summaryRange.Length == 0;
            }

            return ranges.Count == 1 &&
                   ranges[0].Address == summaryRange.Address &&
                   ranges[0].Length == summaryRange.Length;
        }

        private static IReadOnlyList<DmaStreamComputeMemoryRange> NormalizeRanges(
            IReadOnlyList<DmaStreamComputeMemoryRange> ranges)
        {
            if (ranges.Count == 0)
            {
                return Array.Empty<DmaStreamComputeMemoryRange>();
            }

            var sorted = new DmaStreamComputeMemoryRange[ranges.Count];
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

            var merged = new List<DmaStreamComputeMemoryRange>(sorted.Length);
            ulong currentStart = sorted[0].Address;
            ulong currentEnd = sorted[0].Address + sorted[0].Length;
            for (int index = 1; index < sorted.Length; index++)
            {
                DmaStreamComputeMemoryRange next = sorted[index];
                ulong nextEnd = next.Address + next.Length;
                if (next.Address <= currentEnd)
                {
                    if (nextEnd > currentEnd)
                    {
                        currentEnd = nextEnd;
                    }

                    continue;
                }

                merged.Add(new DmaStreamComputeMemoryRange(currentStart, currentEnd - currentStart));
                currentStart = next.Address;
                currentEnd = nextEnd;
            }

            merged.Add(new DmaStreamComputeMemoryRange(currentStart, currentEnd - currentStart));
            return merged.AsReadOnly();
        }

        private static ulong ComputeDsc2FootprintHash(
            IReadOnlyList<DmaStreamComputeMemoryRange> readRanges,
            IReadOnlyList<DmaStreamComputeMemoryRange> writeRanges,
            DmaStreamComputeDsc2FootprintOutcomeKind outcomeKind)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;

            ulong hash = offsetBasis;
            Add((ushort)outcomeKind);
            AddRanges(readRanges);
            AddRanges(writeRanges);
            return hash == 0 ? offsetBasis : hash;

            void Add(ulong value)
            {
                unchecked
                {
                    hash ^= value;
                    hash *= prime;
                }
            }

            void AddRanges(IReadOnlyList<DmaStreamComputeMemoryRange> ranges)
            {
                Add((ulong)ranges.Count);
                for (int index = 0; index < ranges.Count; index++)
                {
                    Add(ranges[index].Address);
                    Add(ranges[index].Length);
                }
            }
        }

        private static bool TryMultiply(ulong left, ulong right, out ulong product)
        {
            if (left != 0 && right > ulong.MaxValue / left)
            {
                product = 0;
                return false;
            }

            product = left * right;
            return true;
        }

        private static bool TryAdd(ulong left, ulong right, out ulong sum)
        {
            if (left > ulong.MaxValue - right)
            {
                sum = 0;
                return false;
            }

            sum = left + right;
            return true;
        }

    }
}
