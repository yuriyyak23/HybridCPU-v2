using System;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcDescriptorParserTests
{
    [Fact]
    public void L7SdcDescriptorParser_ValidDescriptor_ParsesNormalizedImmutableAbiObject()
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        AcceleratorDescriptorReference reference =
            L7SdcTestDescriptorFactory.CreateReference(descriptorBytes);

        AcceleratorDescriptorValidationResult result =
            L7SdcTestDescriptorFactory.ParseWithGuard(descriptorBytes, reference: reference);

        Assert.True(result.IsValid, result.Message);
        AcceleratorCommandDescriptor descriptor = result.RequireDescriptor();
        Assert.Equal(AcceleratorDescriptorFault.None, result.Fault);
        Assert.Equal(reference, descriptor.DescriptorReference);
        Assert.Equal(AcceleratorClassId.Matrix, descriptor.AcceleratorClass);
        Assert.Equal(AcceleratorDeviceId.ReferenceMatMul, descriptor.AcceleratorId);
        Assert.Equal(AcceleratorOperationKind.MatMul, descriptor.Operation);
        Assert.Equal(AcceleratorDatatype.Float32, descriptor.Datatype);
        Assert.Equal(AcceleratorShapeKind.Matrix2D, descriptor.Shape);
        Assert.Equal(2, descriptor.ShapeRank);
        Assert.Equal(AcceleratorPartialCompletionPolicy.AllOrNone, descriptor.PartialCompletionPolicy);
        Assert.Equal(16, descriptor.Alignment.MinimumAlignmentBytes);
        Assert.Equal(2, descriptor.SourceRanges.Count);
        Assert.Single(descriptor.DestinationRanges);
        Assert.Empty(descriptor.ScratchRanges);
        Assert.Single(descriptor.NormalizedFootprint.SourceRanges);
        Assert.Equal(new AcceleratorMemoryRange(0x1000, 0x40), descriptor.NormalizedFootprint.SourceRanges[0]);
        Assert.NotEqual(0UL, descriptor.Identity.DescriptorIdentityHash);
        Assert.NotEqual(0UL, descriptor.NormalizedFootprint.Hash);
        Assert.Equal(descriptor.Identity.NormalizedFootprintHash, descriptor.NormalizedFootprint.Hash);
    }

    [Theory]
    [InlineData(L7SdcTestDescriptorFactory.MagicOffset, 0, AcceleratorDescriptorFault.DescriptorDecodeFault)]
    [InlineData(L7SdcTestDescriptorFactory.AbiVersionOffset, 2, AcceleratorDescriptorFault.UnsupportedAbiVersion)]
    [InlineData(L7SdcTestDescriptorFactory.HeaderSizeOffset, 64, AcceleratorDescriptorFault.DescriptorSizeFault)]
    [InlineData(L7SdcTestDescriptorFactory.DescriptorSizeOffset, 16, AcceleratorDescriptorFault.DescriptorSizeFault)]
    public void L7SdcDescriptorParser_InvalidMagicVersionOrSize_Rejects(
        int offset,
        uint value,
        AcceleratorDescriptorFault expectedFault)
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        if (offset == L7SdcTestDescriptorFactory.MagicOffset ||
            offset == L7SdcTestDescriptorFactory.DescriptorSizeOffset)
        {
            L7SdcTestDescriptorFactory.WriteUInt32(descriptorBytes, offset, value);
        }
        else
        {
            L7SdcTestDescriptorFactory.WriteUInt16(descriptorBytes, offset, (ushort)value);
        }

        AcceleratorDescriptorValidationResult result =
            L7SdcTestDescriptorFactory.ParseWithGuard(descriptorBytes);

        Assert.False(result.IsValid);
        Assert.Null(result.Descriptor);
        Assert.Equal(expectedFault, result.Fault);
    }

    [Theory]
    [InlineData(L7SdcTestDescriptorFactory.FlagsOffset)]
    [InlineData(L7SdcTestDescriptorFactory.Reserved1Offset)]
    [InlineData(L7SdcTestDescriptorFactory.Reserved5Offset)]
    public void L7SdcDescriptorParser_DirtyReservedFields_Reject(int offset)
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        descriptorBytes[offset] = 1;

        AcceleratorDescriptorValidationResult result =
            L7SdcTestDescriptorFactory.ParseWithGuard(descriptorBytes);

        Assert.False(result.IsValid);
        Assert.Equal(AcceleratorDescriptorFault.ReservedFieldFault, result.Fault);
    }

    [Fact]
    public void L7SdcDescriptorParser_RangeTableInsideFixedHeader_Rejects()
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        L7SdcTestDescriptorFactory.WriteUInt32(
            descriptorBytes,
            L7SdcTestDescriptorFactory.SourceRangeTableOffset,
            96);

        AcceleratorDescriptorValidationResult result =
            L7SdcTestDescriptorFactory.ParseWithGuard(descriptorBytes);

        Assert.False(result.IsValid);
        Assert.Equal(AcceleratorDescriptorFault.DescriptorDecodeFault, result.Fault);
        Assert.Contains("fixed descriptor header", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void L7SdcDescriptorParser_OverlappingRangeTables_Rejects()
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        L7SdcTestDescriptorFactory.WriteUInt32(
            descriptorBytes,
            L7SdcTestDescriptorFactory.DestinationRangeTableOffset,
            L7SdcTestDescriptorFactory.HeaderSize);

        AcceleratorDescriptorValidationResult result =
            L7SdcTestDescriptorFactory.ParseWithGuard(descriptorBytes);

        Assert.False(result.IsValid);
        Assert.Equal(AcceleratorDescriptorFault.DescriptorDecodeFault, result.Fault);
        Assert.Contains("overlap", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(L7SdcTestDescriptorFactory.AcceleratorClassOffset, 99, AcceleratorDescriptorFault.UnsupportedAcceleratorClass)]
    [InlineData(L7SdcTestDescriptorFactory.AcceleratorIdOffset, 99, AcceleratorDescriptorFault.UnsupportedAcceleratorId)]
    [InlineData(L7SdcTestDescriptorFactory.OperationOffset, 99, AcceleratorDescriptorFault.UnsupportedOperation)]
    [InlineData(L7SdcTestDescriptorFactory.DatatypeOffset, 99, AcceleratorDescriptorFault.UnsupportedDatatype)]
    [InlineData(L7SdcTestDescriptorFactory.ShapeOffset, 99, AcceleratorDescriptorFault.UnsupportedShape)]
    [InlineData(L7SdcTestDescriptorFactory.ShapeRankOffset, 1, AcceleratorDescriptorFault.UnsupportedShape)]
    public void L7SdcDescriptorParser_InvalidClassIdOperationDatatypeOrShape_Reject(
        int offset,
        ushort value,
        AcceleratorDescriptorFault expectedFault)
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        L7SdcTestDescriptorFactory.WriteUInt16(descriptorBytes, offset, value);

        AcceleratorDescriptorValidationResult result =
            L7SdcTestDescriptorFactory.ParseWithGuard(descriptorBytes);

        Assert.False(result.IsValid);
        Assert.Equal(expectedFault, result.Fault);
    }

    [Theory]
    [InlineData(L7SdcTestDescriptorFactory.SourceRangeCountOffset)]
    [InlineData(L7SdcTestDescriptorFactory.DestinationRangeCountOffset)]
    public void L7SdcDescriptorParser_EmptyRequiredRanges_Reject(int countOffset)
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        L7SdcTestDescriptorFactory.WriteUInt16(descriptorBytes, countOffset, 0);
        L7SdcTestDescriptorFactory.RewriteHashes(descriptorBytes);

        AcceleratorDescriptorValidationResult result =
            L7SdcTestDescriptorFactory.ParseWithGuard(descriptorBytes);

        Assert.False(result.IsValid);
        Assert.Equal(AcceleratorDescriptorFault.DescriptorDecodeFault, result.Fault);
    }

    [Fact]
    public void L7SdcDescriptorParser_ZeroLengthRange_Rejects()
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        L7SdcTestDescriptorFactory.WriteUInt64(
            descriptorBytes,
            L7SdcTestDescriptorFactory.HeaderSize + L7SdcTestDescriptorFactory.RangeLengthFieldOffset,
            0);

        AcceleratorDescriptorValidationResult result =
            L7SdcTestDescriptorFactory.ParseWithGuard(descriptorBytes);

        Assert.False(result.IsValid);
        Assert.Equal(AcceleratorDescriptorFault.ZeroLengthFault, result.Fault);
    }

    [Fact]
    public void L7SdcDescriptorParser_MisalignedRange_Rejects()
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        L7SdcTestDescriptorFactory.WriteUInt64(
            descriptorBytes,
            L7SdcTestDescriptorFactory.HeaderSize + L7SdcTestDescriptorFactory.RangeAddressFieldOffset,
            0x1008);

        AcceleratorDescriptorValidationResult result =
            L7SdcTestDescriptorFactory.ParseWithGuard(descriptorBytes);

        Assert.False(result.IsValid);
        Assert.Equal(AcceleratorDescriptorFault.AlignmentFault, result.Fault);
    }

    [Fact]
    public void L7SdcDescriptorParser_ScratchRequirementWithoutRanges_Rejects()
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor(scratchRequiredBytes: 0);
        L7SdcTestDescriptorFactory.WriteUInt64(
            descriptorBytes,
            L7SdcTestDescriptorFactory.ScratchRequiredBytesOffset,
            0x40);
        L7SdcTestDescriptorFactory.RewriteHashes(descriptorBytes);

        AcceleratorDescriptorValidationResult result =
            L7SdcTestDescriptorFactory.ParseWithGuard(descriptorBytes);

        Assert.False(result.IsValid);
        Assert.Equal(AcceleratorDescriptorFault.DescriptorDecodeFault, result.Fault);
    }

    [Fact]
    public void L7SdcDescriptorParser_AlignmentAndFootprintNormalization_AreDeterministic()
    {
        byte[] left = L7SdcTestDescriptorFactory.BuildDescriptor(
            sourceRanges: new[]
            {
                new AcceleratorMemoryRange(0x1020, 0x20),
                new AcceleratorMemoryRange(0x1000, 0x20)
            });
        byte[] right = L7SdcTestDescriptorFactory.BuildDescriptor(
            sourceRanges: new[]
            {
                new AcceleratorMemoryRange(0x1000, 0x20),
                new AcceleratorMemoryRange(0x1020, 0x20)
            });

        AcceleratorCommandDescriptor leftDescriptor =
            L7SdcTestDescriptorFactory.ParseWithGuard(left).RequireDescriptor();
        AcceleratorCommandDescriptor rightDescriptor =
            L7SdcTestDescriptorFactory.ParseWithGuard(right).RequireDescriptor();

        Assert.Equal(leftDescriptor.NormalizedFootprint.Hash, rightDescriptor.NormalizedFootprint.Hash);
        Assert.Equal(leftDescriptor.NormalizedFootprint.SourceRanges, rightDescriptor.NormalizedFootprint.SourceRanges);
        Assert.NotEqual(leftDescriptor.Identity.DescriptorIdentityHash, rightDescriptor.Identity.DescriptorIdentityHash);
    }

    [Fact]
    public void L7SdcDescriptorParser_DescriptorIdentityHashMismatch_Rejects()
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        L7SdcTestDescriptorFactory.WriteUInt64(
            descriptorBytes,
            L7SdcTestDescriptorFactory.DescriptorIdentityHashOffset,
            0xBAD);

        AcceleratorDescriptorValidationResult result =
            L7SdcTestDescriptorFactory.ParseWithGuard(descriptorBytes);

        Assert.False(result.IsValid);
        Assert.Equal(AcceleratorDescriptorFault.DescriptorIdentityHashMismatch, result.Fault);
    }

    [Fact]
    public void L7SdcDescriptorParser_NormalizedFootprintHashMismatch_Rejects()
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        L7SdcTestDescriptorFactory.WriteUInt64(
            descriptorBytes,
            L7SdcTestDescriptorFactory.NormalizedFootprintHashOffset,
            0xBAD);
        L7SdcTestDescriptorFactory.WriteUInt64(
            descriptorBytes,
            L7SdcTestDescriptorFactory.DescriptorIdentityHashOffset,
            AcceleratorDescriptorParser.ComputeDescriptorIdentityHash(descriptorBytes));

        AcceleratorDescriptorValidationResult result =
            L7SdcTestDescriptorFactory.ParseWithGuard(descriptorBytes);

        Assert.False(result.IsValid);
        Assert.Equal(AcceleratorDescriptorFault.NormalizedFootprintHashMismatch, result.Fault);
    }

    [Fact]
    public void L7SdcDescriptorParser_UnsupportedPartialCompletionPolicy_Rejects()
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        L7SdcTestDescriptorFactory.WriteUInt16(
            descriptorBytes,
            L7SdcTestDescriptorFactory.PartialCompletionPolicyOffset,
            2);

        AcceleratorDescriptorValidationResult result =
            L7SdcTestDescriptorFactory.ParseWithGuard(descriptorBytes);

        Assert.False(result.IsValid);
        Assert.Equal(AcceleratorDescriptorFault.UnsupportedPartialCompletionPolicy, result.Fault);
    }

    [Fact]
    public void L7SdcDescriptorParser_DescriptorReferenceMismatch_Rejects()
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        var reference = new AcceleratorDescriptorReference(
            DescriptorAddress: 0x8800,
            DescriptorSize: (uint)descriptorBytes.Length,
            DescriptorIdentityHash: 0x1234);

        AcceleratorDescriptorValidationResult result =
            L7SdcTestDescriptorFactory.ParseWithGuard(descriptorBytes, reference: reference);

        Assert.False(result.IsValid);
        Assert.Equal(AcceleratorDescriptorFault.DescriptorReferenceMismatch, result.Fault);
    }
}
