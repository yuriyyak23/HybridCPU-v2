using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class DmaStreamComputeDescriptorAbiTests
{
    [Fact]
    public void ValidDescriptor_Parse_PublishesNormalizedDescriptorWithoutEnablingExecution()
    {
        byte[] descriptorBytes = BuildDescriptor();
        var carrier = new DmaStreamComputeDescriptorReference(
            descriptorAddress: 0x8000,
            descriptorSize: (uint)descriptorBytes.Length,
            descriptorIdentityHash: IdentityHash);

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                CreateGuardDecision(descriptorBytes, carrier),
                carrier);

        Assert.True(result.IsValid, result.Message);
        Assert.NotNull(result.Descriptor);
        Assert.Equal(DmaStreamComputeValidationFault.None, result.Fault);
        Assert.Equal(DmaStreamComputeOperationKind.Add, result.Descriptor.Operation);
        Assert.Equal(DmaStreamComputeElementType.UInt32, result.Descriptor.ElementType);
        Assert.Equal(DmaStreamComputeShapeKind.Contiguous1D, result.Descriptor.Shape);
        Assert.Equal(carrier, result.Descriptor.DescriptorReference);
        Assert.Equal(2, result.Descriptor.ReadMemoryRanges.Count);
        Assert.Single(result.Descriptor.WriteMemoryRanges);
        Assert.NotEqual(0UL, result.Descriptor.NormalizedFootprintHash);
        Assert.False(DmaStreamComputeDescriptorParser.ExecutionEnabled);
    }

    [Fact]
    public void DescriptorParseWithoutGuardDecision_RemainsOwnerDomainFault()
    {
        byte[] descriptorBytes = BuildDescriptor();

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(descriptorBytes);

        Assert.False(result.IsValid);
        Assert.Null(result.Descriptor);
        Assert.Equal(DmaStreamComputeValidationFault.OwnerDomainFault, result.Fault);
        Assert.Contains("explicit owner/domain guard decision", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(HeaderFlagsOffset)]
    [InlineData(HeaderReserved0Offset)]
    [InlineData(HeaderReserved1Offset)]
    [InlineData(HeaderReserved2Offset)]
    [InlineData(HeaderReserved3Offset)]
    public void ReservedOrNonZeroV1Fields_FailClosed(int offset)
    {
        byte[] descriptorBytes = BuildDescriptor();
        descriptorBytes[offset] = 1;

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                CreateGuardDecision(descriptorBytes));

        Assert.False(result.IsValid);
        Assert.Null(result.Descriptor);
        Assert.Equal(DmaStreamComputeValidationFault.ReservedFieldFault, result.Fault);
    }

    [Theory]
    [InlineData(HeaderAbiVersionOffset, 2, DmaStreamComputeValidationFault.UnsupportedAbiVersion)]
    [InlineData(HeaderOperationOffset, 99, DmaStreamComputeValidationFault.UnsupportedOperation)]
    [InlineData(HeaderElementTypeOffset, 99, DmaStreamComputeValidationFault.UnsupportedElementType)]
    [InlineData(HeaderShapeOffset, 99, DmaStreamComputeValidationFault.UnsupportedShape)]
    public void UnsupportedAbiOperationTypeOrShape_FailClosedWithoutFallback(
        int offset,
        ushort value,
        DmaStreamComputeValidationFault expectedFault)
    {
        byte[] descriptorBytes = BuildDescriptor();
        BinaryPrimitives.WriteUInt16LittleEndian(descriptorBytes.AsSpan(offset), value);

        DmaStreamComputeValidationResult result =
            offset == HeaderAbiVersionOffset
                ? DmaStreamComputeDescriptorParser.Parse(descriptorBytes)
                : DmaStreamComputeDescriptorParser.Parse(
                    descriptorBytes,
                    CreateGuardDecision(descriptorBytes));

        Assert.False(result.IsValid);
        Assert.Null(result.Descriptor);
        Assert.Equal(expectedFault, result.Fault);
        Assert.Throws<InvalidOperationException>(() => result.RequireDescriptorForAdmission());
    }

    [Fact]
    public void UnsupportedRangeEncoding_FailsClosedWithoutNormalization()
    {
        byte[] descriptorBytes = BuildDescriptor();
        BinaryPrimitives.WriteUInt16LittleEndian(
            descriptorBytes.AsSpan(HeaderRangeEncodingOffset),
            2);

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                CreateGuardDecision(descriptorBytes));

        Assert.False(result.IsValid);
        Assert.Null(result.Descriptor);
        Assert.Equal(DmaStreamComputeValidationFault.UnsupportedShape, result.Fault);
        Assert.Contains("inline contiguous", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnsupportedPartialCompletionPolicy_FailsClosedAsReservedV1Field()
    {
        byte[] descriptorBytes = BuildDescriptor();
        BinaryPrimitives.WriteUInt16LittleEndian(
            descriptorBytes.AsSpan(HeaderPartialCompletionPolicyOffset),
            2);

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                CreateGuardDecision(descriptorBytes));

        Assert.False(result.IsValid);
        Assert.Null(result.Descriptor);
        Assert.Equal(DmaStreamComputeValidationFault.ReservedFieldFault, result.Fault);
        Assert.Contains("all-or-none", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RangeWithZeroLength_FailsClosed()
    {
        byte[] descriptorBytes = BuildDescriptor();
        BinaryPrimitives.WriteUInt64LittleEndian(
            descriptorBytes.AsSpan(HeaderSize + RangeLengthFieldOffset),
            0);

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                CreateGuardDecision(descriptorBytes));

        Assert.False(result.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.ZeroLengthFault, result.Fault);
    }

    [Fact]
    public void RangeWithElementMisalignment_FailsClosed()
    {
        byte[] descriptorBytes = BuildDescriptor();
        BinaryPrimitives.WriteUInt64LittleEndian(
            descriptorBytes.AsSpan(HeaderSize + RangeAddressFieldOffset),
            0x1002);

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                CreateGuardDecision(descriptorBytes));

        Assert.False(result.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.AlignmentFault, result.Fault);
    }

    [Fact]
    public void RangeWithAddressOverflow_FailsClosed()
    {
        byte[] descriptorBytes = BuildDescriptor();
        BinaryPrimitives.WriteUInt64LittleEndian(
            descriptorBytes.AsSpan(HeaderSize + RangeAddressFieldOffset),
            ulong.MaxValue - 1);
        BinaryPrimitives.WriteUInt64LittleEndian(
            descriptorBytes.AsSpan(HeaderSize + RangeLengthFieldOffset),
            16);

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                CreateGuardDecision(descriptorBytes));

        Assert.False(result.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.RangeOverflow, result.Fault);
    }

    [Fact]
    public void RawVliwLayout_RemainsFixedAndCannotCarryDescriptorThroughReservedBits()
    {
        Assert.Equal(32, Marshal.SizeOf<VLIW_Instruction>());
        Assert.Equal(256, Marshal.SizeOf<VLIW_Bundle>());

        var instruction = new VLIW_Instruction
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.STREAM_SETUP,
            Reserved = 0x5A,
            DestSrc1Pointer = 0x8000,
            Src2Pointer = 0x100
        };

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.TryDecodeRawVliwCarrier(in instruction, slotIndex: 6);

        Assert.False(result.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.DescriptorCarrierDecodeFault, result.Fault);
        Assert.Contains("word0[47:40]", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RawVliwRetiredPolicyGapBit_CannotBecomeDescriptorCarrier()
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.STREAM_SETUP,
            DestSrc1Pointer = 0x8000,
            Src2Pointer = 0x100
        };
        instruction.Word3 |= VLIW_Instruction.RetiredPolicyGapMask;

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.TryDecodeRawVliwCarrier(in instruction, slotIndex: 6);

        Assert.False(result.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.DescriptorCarrierDecodeFault, result.Fault);
        Assert.Contains("word3[50]", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RawVirtualThreadHint_CannotSatisfyDescriptorOwnerBinding()
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.STREAM_SETUP,
            VirtualThreadId = 3,
            DestSrc1Pointer = 0x8000,
            Src2Pointer = 0x100
        };

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.TryDecodeRawVliwCarrier(in instruction, slotIndex: 6);

        Assert.False(result.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.DescriptorCarrierDecodeFault, result.Fault);
        Assert.Contains("word3[49:48]", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DescriptorReferenceSidebandWithoutPayload_SurvivesIrAndProjectorFailsClosed()
    {
        var reference = new DmaStreamComputeDescriptorReference(
            descriptorAddress: 0x8000,
            descriptorSize: 192,
            descriptorIdentityHash: IdentityHash);
        var instruction = new InstructionIR
        {
            CanonicalOpcode = Processor.CPU_Core.InstructionsEnum.STREAM_SETUP,
            Class = InstructionClass.System,
            SerializationClass = SerializationClass.FullSerial,
            Rd = VLIW_Instruction.NoArchReg,
            Rs1 = VLIW_Instruction.NoArchReg,
            Rs2 = VLIW_Instruction.NoArchReg,
            Imm = 0,
            DmaStreamComputeDescriptorReference = reference
        };
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[6] = new VLIW_Instruction
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.STREAM_SETUP
        };
        var bundle = new DecodedInstructionBundle(
            bundleAddress: 0x4000,
            bundleSerial: 7,
            slots: new[] { DecodedInstruction.CreateOccupied(6, instruction) });

        Assert.Equal(reference, bundle.GetDecodedSlot(6).Instruction!.DmaStreamComputeDescriptorReference);

        MicroOp?[] carriers =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, bundle);
        TrapMicroOp trap = Assert.IsType<TrapMicroOp>(carriers[6]);

        Assert.Contains("DmaStreamCompute descriptor reference", trap.TrapReason, StringComparison.Ordinal);
        Assert.Contains("without the guard-accepted descriptor payload", trap.TrapReason, StringComparison.OrdinalIgnoreCase);
    }

    private const ulong IdentityHash = 0xA11CE5EEDUL;
    private const int HeaderSize = 128;
    private const int RangeEntrySize = 16;
    private const int RangeAddressFieldOffset = 0;
    private const int RangeLengthFieldOffset = 8;
    private const int HeaderAbiVersionOffset = 4;
    private const int HeaderFlagsOffset = 12;
    private const int HeaderReserved0Offset = 16;
    private const int HeaderOperationOffset = 40;
    private const int HeaderElementTypeOffset = 42;
    private const int HeaderShapeOffset = 44;
    private const int HeaderRangeEncodingOffset = 46;
    private const int HeaderPartialCompletionPolicyOffset = 56;
    private const int HeaderReserved1Offset = 62;
    private const int HeaderReserved2Offset = 88;
    private const int HeaderReserved3Offset = 120;

    private static byte[] BuildDescriptor(
        DmaStreamComputeOperationKind operation = DmaStreamComputeOperationKind.Add,
        DmaStreamComputeElementType elementType = DmaStreamComputeElementType.UInt32,
        DmaStreamComputeShapeKind shape = DmaStreamComputeShapeKind.Contiguous1D)
    {
        ushort sourceRangeCount = operation switch
        {
            DmaStreamComputeOperationKind.Copy => 1,
            DmaStreamComputeOperationKind.Fma => 3,
            _ => 2
        };
        ushort destinationRangeCount = 1;
        int sourceRangeTableOffset = HeaderSize;
        int destinationRangeTableOffset = HeaderSize + (sourceRangeCount * RangeEntrySize);
        uint totalSize = (uint)(HeaderSize + ((sourceRangeCount + destinationRangeCount) * RangeEntrySize));
        byte[] bytes = new byte[totalSize];

        WriteUInt32(bytes, 0, DmaStreamComputeDescriptorParser.Magic);
        WriteUInt16(bytes, HeaderAbiVersionOffset, DmaStreamComputeDescriptorParser.CurrentAbiVersion);
        WriteUInt16(bytes, 6, HeaderSize);
        WriteUInt32(bytes, 8, totalSize);
        WriteUInt64(bytes, 24, IdentityHash);
        WriteUInt64(bytes, 32, 0xC011EC7EUL);
        WriteUInt16(bytes, HeaderOperationOffset, (ushort)operation);
        WriteUInt16(bytes, HeaderElementTypeOffset, (ushort)elementType);
        WriteUInt16(bytes, HeaderShapeOffset, (ushort)shape);
        WriteUInt16(bytes, 46, (ushort)DmaStreamComputeRangeEncoding.InlineContiguous);
        WriteUInt16(bytes, 48, sourceRangeCount);
        WriteUInt16(bytes, 50, destinationRangeCount);
        WriteUInt16(bytes, 56, (ushort)DmaStreamComputePartialCompletionPolicy.AllOrNone);
        WriteUInt32(bytes, 64, 77);
        WriteUInt32(bytes, 68, 1);
        WriteUInt32(bytes, 72, 2);
        WriteUInt32(bytes, 76, DmaStreamComputeDescriptor.CanonicalLane6DeviceId);
        WriteUInt64(bytes, 80, 0xD0A11);
        WriteUInt32(bytes, 96, (uint)sourceRangeTableOffset);
        WriteUInt32(bytes, 100, (uint)destinationRangeTableOffset);

        for (int i = 0; i < sourceRangeCount; i++)
        {
            WriteRange(bytes, sourceRangeTableOffset + (i * RangeEntrySize), 0x1000UL + ((ulong)i * 0x1000UL), 16);
        }

        WriteRange(bytes, destinationRangeTableOffset, 0x9000, 16);

        return bytes;
    }

    private static DmaStreamComputeOwnerGuardDecision CreateGuardDecision(
        byte[] descriptorBytes,
        DmaStreamComputeDescriptorReference? descriptorReference = null)
    {
        DmaStreamComputeStructuralReadResult structuralRead =
            DmaStreamComputeDescriptorParser.ReadStructuralOwnerBinding(
                descriptorBytes,
                descriptorReference);
        Assert.True(structuralRead.IsValid, structuralRead.Message);
        DmaStreamComputeOwnerBinding ownerBinding =
            structuralRead.RequireOwnerBindingForGuard();
        var context = new DmaStreamComputeOwnerGuardContext(
            ownerBinding.OwnerVirtualThreadId,
            ownerBinding.OwnerContextId,
            ownerBinding.OwnerCoreId,
            ownerBinding.OwnerPodId,
            ownerBinding.OwnerDomainTag,
            activeDomainCertificate: ownerBinding.OwnerDomainTag,
            deviceId: ownerBinding.DeviceId);
        return new SafetyVerifier().EvaluateDmaStreamComputeOwnerGuard(
            ownerBinding,
            context);
    }

    private static void WriteRange(byte[] bytes, int offset, ulong address, ulong length)
    {
        WriteUInt64(bytes, offset + RangeAddressFieldOffset, address);
        WriteUInt64(bytes, offset + RangeLengthFieldOffset, length);
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteUInt64(byte[] bytes, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset), value);
}
