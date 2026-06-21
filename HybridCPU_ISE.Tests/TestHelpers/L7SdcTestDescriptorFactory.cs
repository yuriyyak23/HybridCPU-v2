using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Telemetry;

namespace HybridCPU_ISE.Tests.TestHelpers;

internal static class L7SdcTestDescriptorFactory
{
    internal const int HeaderSize = 128;
    internal const int RangeEntrySize = 16;
    internal const int MagicOffset = 0;
    internal const int AbiVersionOffset = 4;
    internal const int HeaderSizeOffset = 6;
    internal const int DescriptorSizeOffset = 8;
    internal const int FlagsOffset = 12;
    internal const int AcceleratorClassOffset = 16;
    internal const int AcceleratorIdOffset = 18;
    internal const int OperationOffset = 20;
    internal const int DatatypeOffset = 22;
    internal const int DescriptorIdentityHashOffset = 24;
    internal const int NormalizedFootprintHashOffset = 32;
    internal const int ShapeOffset = 40;
    internal const int ShapeRankOffset = 42;
    internal const int SourceRangeCountOffset = 44;
    internal const int DestinationRangeCountOffset = 46;
    internal const int ScratchRangeCountOffset = 48;
    internal const int PartialCompletionPolicyOffset = 50;
    internal const int AlignmentBytesOffset = 52;
    internal const int Reserved1Offset = 54;
    internal const int ElementCountOffset = 56;
    internal const int CapabilityVersionOffset = 64;
    internal const int OwnerVirtualThreadIdOffset = 68;
    internal const int SourceRangeTableOffset = 96;
    internal const int DestinationRangeTableOffset = 100;
    internal const int ScratchRangeTableOffset = 104;
    internal const int ScratchRequiredBytesOffset = 112;
    internal const int Reserved5Offset = 120;
    internal const int RangeAddressFieldOffset = 0;
    internal const int RangeLengthFieldOffset = 8;

    internal static byte[] BuildDescriptor(
        IReadOnlyList<AcceleratorMemoryRange>? sourceRanges = null,
        IReadOnlyList<AcceleratorMemoryRange>? destinationRanges = null,
        IReadOnlyList<AcceleratorMemoryRange>? scratchRanges = null,
        ulong scratchRequiredBytes = 0,
        ushort alignmentBytes = 16,
        AcceleratorClassId acceleratorClass = AcceleratorClassId.Matrix,
        AcceleratorDeviceId acceleratorId = AcceleratorDeviceId.ReferenceMatMul,
        AcceleratorOperationKind operation = AcceleratorOperationKind.MatMul,
        AcceleratorDatatype datatype = AcceleratorDatatype.Float32,
        AcceleratorShapeKind shape = AcceleratorShapeKind.Matrix2D,
        ushort shapeRank = 2,
        ulong elementCount = 64,
        AcceleratorPartialCompletionPolicy partialCompletionPolicy = AcceleratorPartialCompletionPolicy.AllOrNone,
        ushort ownerVirtualThreadId = OwnerVirtualThreadId,
        uint ownerContextId = OwnerContextId,
        uint ownerCoreId = OwnerCoreId,
        uint ownerPodId = OwnerPodId,
        ulong domainTag = OwnerDomainTag)
    {
        sourceRanges ??= new[]
        {
            new AcceleratorMemoryRange(0x1020, 0x20),
            new AcceleratorMemoryRange(0x1000, 0x20)
        };
        destinationRanges ??= new[]
        {
            new AcceleratorMemoryRange(0x9000, 0x40)
        };
        scratchRanges ??= scratchRequiredBytes == 0
            ? Array.Empty<AcceleratorMemoryRange>()
            : new[] { new AcceleratorMemoryRange(0xA000, scratchRequiredBytes) };

        int sourceTableOffset = HeaderSize;
        int destinationTableOffset = sourceTableOffset + (sourceRanges.Count * RangeEntrySize);
        int scratchTableOffset = destinationTableOffset + (destinationRanges.Count * RangeEntrySize);
        uint descriptorSize = (uint)(HeaderSize + ((sourceRanges.Count + destinationRanges.Count + scratchRanges.Count) * RangeEntrySize));
        byte[] bytes = new byte[descriptorSize];

        WriteUInt32(bytes, MagicOffset, AcceleratorDescriptorParser.Magic);
        WriteUInt16(bytes, AbiVersionOffset, AcceleratorDescriptorParser.CurrentAbiVersion);
        WriteUInt16(bytes, HeaderSizeOffset, HeaderSize);
        WriteUInt32(bytes, DescriptorSizeOffset, descriptorSize);
        WriteUInt16(bytes, AcceleratorClassOffset, (ushort)acceleratorClass);
        WriteUInt16(bytes, AcceleratorIdOffset, (ushort)acceleratorId);
        WriteUInt16(bytes, OperationOffset, (ushort)operation);
        WriteUInt16(bytes, DatatypeOffset, (ushort)datatype);
        WriteUInt16(bytes, ShapeOffset, (ushort)shape);
        WriteUInt16(bytes, ShapeRankOffset, shapeRank);
        WriteUInt16(bytes, SourceRangeCountOffset, (ushort)sourceRanges.Count);
        WriteUInt16(bytes, DestinationRangeCountOffset, (ushort)destinationRanges.Count);
        WriteUInt16(bytes, ScratchRangeCountOffset, (ushort)scratchRanges.Count);
        WriteUInt16(bytes, PartialCompletionPolicyOffset, (ushort)partialCompletionPolicy);
        WriteUInt16(bytes, AlignmentBytesOffset, alignmentBytes);
        WriteUInt64(bytes, ElementCountOffset, elementCount);
        WriteUInt32(bytes, CapabilityVersionOffset, 1);
        WriteUInt16(bytes, OwnerVirtualThreadIdOffset, ownerVirtualThreadId);
        WriteUInt32(bytes, 72, ownerContextId);
        WriteUInt32(bytes, 76, ownerCoreId);
        WriteUInt32(bytes, 80, ownerPodId);
        WriteUInt64(bytes, 88, domainTag);
        WriteUInt32(bytes, SourceRangeTableOffset, (uint)sourceTableOffset);
        WriteUInt32(bytes, DestinationRangeTableOffset, (uint)destinationTableOffset);
        WriteUInt32(bytes, ScratchRangeTableOffset, scratchRanges.Count == 0 ? 0 : (uint)scratchTableOffset);
        WriteUInt64(bytes, ScratchRequiredBytesOffset, scratchRequiredBytes);

        WriteRanges(bytes, sourceTableOffset, sourceRanges);
        WriteRanges(bytes, destinationTableOffset, destinationRanges);
        if (scratchRanges.Count != 0)
        {
            WriteRanges(bytes, scratchTableOffset, scratchRanges);
        }

        RewriteHashes(bytes);
        return bytes;
    }

    internal static AcceleratorCommandDescriptor ParseValidDescriptor()
    {
        byte[] bytes = BuildDescriptor();
        AcceleratorDescriptorValidationResult result = ParseWithGuard(bytes);
        if (!result.IsValid)
        {
            throw new InvalidOperationException(result.Message);
        }

        return result.RequireDescriptor();
    }

    internal static AcceleratorDescriptorValidationResult ParseWithGuard(
        byte[] descriptorBytes,
        AcceleratorGuardEvidence? evidence = null,
        AcceleratorDescriptorReference? reference = null,
        AcceleratorTelemetry? telemetry = null)
    {
        AcceleratorDescriptorStructuralReadResult structural =
            AcceleratorDescriptorParser.ReadStructuralOwnerBinding(
                descriptorBytes,
                reference);
        if (!structural.IsValid)
        {
            return AcceleratorDescriptorParser.Parse(
                descriptorBytes,
                reference,
                telemetry);
        }

        AcceleratorGuardDecision guardDecision =
            AcceleratorOwnerDomainGuard.Default.EnsureBeforeDescriptorAcceptance(
                structural.RequireOwnerBindingForGuard(),
                evidence ?? CreateGuardEvidence(structural.RequireOwnerBindingForGuard()));
        return AcceleratorDescriptorParser.Parse(
            descriptorBytes,
            guardDecision,
            reference ?? CreateReference(descriptorBytes),
            telemetry);
    }

    internal static AcceleratorGuardDecision CreateGuardDecision(
        byte[] descriptorBytes,
        AcceleratorGuardEvidence? evidence = null)
    {
        AcceleratorDescriptorStructuralReadResult structural =
            AcceleratorDescriptorParser.ReadStructuralOwnerBinding(descriptorBytes);
        if (!structural.IsValid)
        {
            throw new InvalidOperationException(structural.Message);
        }

        AcceleratorOwnerBinding ownerBinding = structural.RequireOwnerBindingForGuard();
        return AcceleratorOwnerDomainGuard.Default.EnsureBeforeDescriptorAcceptance(
            ownerBinding,
            evidence ?? CreateGuardEvidence(ownerBinding));
    }

    internal static AcceleratorGuardEvidence CreateGuardEvidence(
        AcceleratorOwnerBinding? ownerBinding = null,
        ulong activeDomainCertificate = OwnerDomainTag,
        AcceleratorMappingEpoch mappingEpoch = default,
        AcceleratorIommuDomainEpoch iommuDomainEpoch = default)
    {
        ownerBinding ??= CreateOwnerBinding();
        return AcceleratorGuardEvidence.FromGuardPlane(
            ownerBinding,
            activeDomainCertificate,
            mappingEpoch,
            iommuDomainEpoch);
    }

    internal static AcceleratorOwnerBinding CreateOwnerBinding(
        ushort ownerVirtualThreadId = OwnerVirtualThreadId,
        uint ownerContextId = OwnerContextId,
        uint ownerCoreId = OwnerCoreId,
        uint ownerPodId = OwnerPodId,
        ulong domainTag = OwnerDomainTag) =>
        new()
        {
            OwnerVirtualThreadId = ownerVirtualThreadId,
            OwnerContextId = ownerContextId,
            OwnerCoreId = ownerCoreId,
            OwnerPodId = ownerPodId,
            DomainTag = domainTag
        };

    internal static AcceleratorOwnerBinding ReadOwnerBinding(byte[] descriptorBytes)
    {
        AcceleratorDescriptorStructuralReadResult structural =
            AcceleratorDescriptorParser.ReadStructuralOwnerBinding(descriptorBytes);
        if (!structural.IsValid)
        {
            throw new InvalidOperationException(structural.Message);
        }

        return structural.RequireOwnerBindingForGuard();
    }

    internal static AcceleratorDescriptorReference CreateReference(
        byte[] descriptorBytes,
        ulong descriptorAddress = 0x8800)
    {
        return new AcceleratorDescriptorReference(
            descriptorAddress,
            (uint)descriptorBytes.Length,
            ReadUInt64(descriptorBytes, DescriptorIdentityHashOffset));
    }

    internal static void RewriteHashes(byte[] bytes)
    {
        Array.Clear(bytes, NormalizedFootprintHashOffset, sizeof(ulong));
        Array.Clear(bytes, DescriptorIdentityHashOffset, sizeof(ulong));

        ushort sourceCount = ReadUInt16(bytes, SourceRangeCountOffset);
        ushort destinationCount = ReadUInt16(bytes, DestinationRangeCountOffset);
        ushort scratchCount = ReadUInt16(bytes, ScratchRangeCountOffset);

        IReadOnlyList<AcceleratorMemoryRange> sourceRanges = ReadRanges(
            bytes,
            ReadUInt32(bytes, SourceRangeTableOffset),
            sourceCount);
        IReadOnlyList<AcceleratorMemoryRange> destinationRanges = ReadRanges(
            bytes,
            ReadUInt32(bytes, DestinationRangeTableOffset),
            destinationCount);
        IReadOnlyList<AcceleratorMemoryRange> scratchRanges = scratchCount == 0
            ? Array.Empty<AcceleratorMemoryRange>()
            : ReadRanges(bytes, ReadUInt32(bytes, ScratchRangeTableOffset), scratchCount);

        ulong footprintHash = AcceleratorDescriptorParser.ComputeNormalizedFootprintHash(
            (AcceleratorClassId)ReadUInt16(bytes, AcceleratorClassOffset),
            (AcceleratorDeviceId)ReadUInt16(bytes, AcceleratorIdOffset),
            (AcceleratorOperationKind)ReadUInt16(bytes, OperationOffset),
            (AcceleratorDatatype)ReadUInt16(bytes, DatatypeOffset),
            (AcceleratorShapeKind)ReadUInt16(bytes, ShapeOffset),
            ReadUInt16(bytes, ShapeRankOffset),
            ReadUInt64(bytes, ElementCountOffset),
            (AcceleratorPartialCompletionPolicy)ReadUInt16(bytes, PartialCompletionPolicyOffset),
            new AcceleratorAlignmentRequirement(ReadUInt16(bytes, AlignmentBytesOffset)),
            AcceleratorDescriptorParser.NormalizeMemoryRanges(sourceRanges),
            AcceleratorDescriptorParser.NormalizeMemoryRanges(destinationRanges),
            AcceleratorDescriptorParser.NormalizeMemoryRanges(scratchRanges));
        WriteUInt64(bytes, NormalizedFootprintHashOffset, footprintHash);

        ulong identityHash = AcceleratorDescriptorParser.ComputeDescriptorIdentityHash(bytes);
        WriteUInt64(bytes, DescriptorIdentityHashOffset, identityHash);
    }

    internal static void WriteUInt16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);

    internal static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);

    internal static void WriteUInt64(byte[] bytes, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset), value);

    internal static ushort ReadUInt16(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)));

    internal static uint ReadUInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)));

    internal static ulong ReadUInt64(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset, sizeof(ulong)));

    internal const ushort OwnerVirtualThreadId = 0;
    internal const uint OwnerContextId = 77;
    internal const uint OwnerCoreId = 1;
    internal const uint OwnerPodId = 2;
    internal const ulong OwnerDomainTag = 0xD0A11;

    private static void WriteRanges(
        byte[] bytes,
        int tableOffset,
        IReadOnlyList<AcceleratorMemoryRange> ranges)
    {
        for (int index = 0; index < ranges.Count; index++)
        {
            int offset = tableOffset + (index * RangeEntrySize);
            WriteUInt64(bytes, offset + RangeAddressFieldOffset, ranges[index].Address);
            WriteUInt64(bytes, offset + RangeLengthFieldOffset, ranges[index].Length);
        }
    }

    private static IReadOnlyList<AcceleratorMemoryRange> ReadRanges(
        byte[] bytes,
        uint tableOffset,
        ushort count)
    {
        var ranges = new AcceleratorMemoryRange[count];
        for (int index = 0; index < count; index++)
        {
            int offset = checked((int)tableOffset + (index * RangeEntrySize));
            ranges[index] = new AcceleratorMemoryRange(
                ReadUInt64(bytes, offset + RangeAddressFieldOffset),
                ReadUInt64(bytes, offset + RangeLengthFieldOffset));
        }

        return ranges;
    }
}
