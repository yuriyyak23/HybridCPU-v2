using System;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class DmaStreamComputeIsaEncodingTests
{
    [Fact]
    public void CanonicalOpcode_IsPublishedNativeMemoryOrderedAndNotCustomAccelerator()
    {
        Assert.Equal(245, (ushort)InstructionsEnum.DmaStreamCompute);
        Assert.Equal((ushort)InstructionsEnum.DmaStreamCompute, IsaOpcodeValues.DmaStreamCompute);

        OpcodeInfo? maybeInfo = OpcodeRegistry.GetInfo((uint)InstructionsEnum.DmaStreamCompute);
        Assert.True(maybeInfo.HasValue);
        OpcodeInfo info = maybeInfo.Value;
        Assert.Equal("DmaStreamCompute", info.Mnemonic);
        Assert.Equal(InstructionClass.Memory, info.InstructionClass);
        Assert.Equal(SerializationClass.MemoryOrdered, info.SerializationClass);
        Assert.Equal(3, info.MemoryBandwidth);

        Assert.Equal(
            (InstructionClass.Memory, SerializationClass.MemoryOrdered),
            InstructionClassifier.Classify(InstructionsEnum.DmaStreamCompute));
        Assert.Contains("DmaStreamCompute", IsaV4Surface.MandatoryCoreOpcodes);
        Assert.Equal("DMA_STREAM", IsaV4Surface.PipelineClassMap["DmaStreamCompute"]);
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.DmaStreamCompute));
        Assert.False(InstructionRegistry.IsCustomAcceleratorOpcode((uint)InstructionsEnum.DmaStreamCompute));
    }

    [Fact]
    public void NativeDecodeWithDescriptorSideband_ProjectsLane6DmaStreamComputeMicroOp()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[6] = CreateNativeInstruction();
        VliwBundleAnnotations annotations = CreateBundleAnnotations(descriptor);

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle decodedBundle =
            decoder.DecodeInstructionBundle(rawSlots, annotations, bundleAddress: 0x4000, bundleSerial: 9);

        DecodedInstruction decodedSlot = decodedBundle.GetDecodedSlot(6);
        Assert.True(decodedSlot.IsOccupied);
        InstructionIR instruction = decodedSlot.RequireInstruction();
        Assert.Equal(InstructionsEnum.DmaStreamCompute, (InstructionsEnum)instruction.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.Memory, instruction.Class);
        Assert.Equal(SerializationClass.MemoryOrdered, instruction.SerializationClass);
        Assert.Equal(descriptor.DescriptorReference, instruction.DmaStreamComputeDescriptorReference);
        Assert.Same(descriptor, instruction.DmaStreamComputeDescriptor);
        Assert.Equal(VLIW_Instruction.NoArchReg, instruction.Rd);
        Assert.Equal(VLIW_Instruction.NoArchReg, instruction.Rs1);
        Assert.Equal(VLIW_Instruction.NoArchReg, instruction.Rs2);

        MicroOp?[] carriers =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, decodedBundle);
        DmaStreamComputeMicroOp microOp = Assert.IsType<DmaStreamComputeMicroOp>(carriers[6]);
        Assert.Same(descriptor, microOp.Descriptor);
        Assert.Equal(SlotClass.DmaStreamClass, microOp.Placement.RequiredSlotClass);
        Assert.Equal((byte)0b_0100_0000, SlotClassLaneMap.GetLaneMask(SlotClass.DmaStreamClass));
        Assert.False(microOp.WritesRegister);
        Assert.True(microOp.IsMemoryOp);
    }

    [Fact]
    public void NativeDecodeWithoutDescriptorSideband_FailsClosed()
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[6] = CreateNativeInstruction();
        var decoder = new VliwDecoderV4();

        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x4000));

        Assert.Contains("typed decoded sideband", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("reserved")]
    [InlineData("vt-hint")]
    [InlineData("word3-policy-gap")]
    public void NativeDecodeRawReservedBits_AreGuardedBeforeProjection(string mutation)
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[6] = CreateNativeInstruction();

        switch (mutation)
        {
            case "reserved":
                rawSlots[6].Reserved = 1;
                break;
            case "vt-hint":
                rawSlots[6].VirtualThreadId = 1;
                break;
            case "word3-policy-gap":
                rawSlots[6].Word3 |= VLIW_Instruction.RetiredPolicyGapMask;
                break;
        }

        var decoder = new VliwDecoderV4();
        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => decoder.DecodeInstructionBundle(
                rawSlots,
                CreateBundleAnnotations(descriptor),
                bundleAddress: 0x4000));

        Assert.Contains("slot 6", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NativeDecodeOnNonLane6_FailsClosed()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[5] = CreateNativeInstruction();
        var slotMetadata = new InstructionSlotMetadata[BundleMetadata.BundleSlotCount];
        for (int index = 0; index < slotMetadata.Length; index++)
        {
            slotMetadata[index] = InstructionSlotMetadata.Default;
        }

        slotMetadata[5] = new InstructionSlotMetadata(
            VtId.Create(0),
            SlotMetadata.NotStealable)
        {
            DmaStreamComputeDescriptor = descriptor
        };

        var decoder = new VliwDecoderV4();
        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => decoder.DecodeInstructionBundle(
                rawSlots,
                new VliwBundleAnnotations(slotMetadata),
                bundleAddress: 0x4000));

        Assert.Contains("lane6", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static VLIW_Instruction CreateNativeInstruction() =>
        new()
        {
            OpCode = (uint)InstructionsEnum.DmaStreamCompute,
            DataType = 0,
            PredicateMask = 0,
            Immediate = 0,
            DestSrc1Pointer = 0,
            Src2Pointer = 0,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };

    private static VliwBundleAnnotations CreateBundleAnnotations(
        DmaStreamComputeDescriptor descriptor)
    {
        var slotMetadata = new InstructionSlotMetadata[BundleMetadata.BundleSlotCount];
        for (int index = 0; index < slotMetadata.Length; index++)
        {
            slotMetadata[index] = InstructionSlotMetadata.Default;
        }

        slotMetadata[6] = new InstructionSlotMetadata(
            VtId.Create(0),
            SlotMetadata.NotStealable)
        {
            DmaStreamComputeDescriptor = descriptor
        };
        return new VliwBundleAnnotations(slotMetadata);
    }
}
