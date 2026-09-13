using System;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcInstructionTransportSidebandTests
{
    [Fact]
    public void L7SdcInstructionTransportSideband_SlotMetadataCarriesDescriptorWithoutRawAbi()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        InstructionSlotMetadata metadata = new InstructionSlotMetadata(
            VtId.Create(0),
            L7SdcNativeCarrierValidationTests.CreateSystemSingletonSlotMetadata())
            .WithAcceleratorDescriptor(descriptor);

        Assert.Same(descriptor, metadata.AcceleratorCommandDescriptor);
        Assert.Equal(descriptor.DescriptorReference, metadata.AcceleratorCommandDescriptorReference);
        Assert.Null(metadata.DmaStreamComputeDescriptor);
        Assert.Null(metadata.DmaStreamComputeDescriptorReference);
    }

    [Fact]
    public void L7SdcInstructionTransportSideband_DecodeAndProjectorMaterializeSubmitCarrierWithDescriptor()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[7] = L7SdcPhase03TestCases.CreateNativeInstruction(InstructionsEnum.ACCEL_SUBMIT);

        VliwBundleAnnotations annotations =
            L7SdcNativeCarrierValidationTests.CreateAnnotations(
                7,
                descriptor,
                L7SdcNativeCarrierValidationTests.CreateSystemSingletonSlotMetadata());

        DecodedInstructionBundle decoded = new VliwDecoderV4().DecodeInstructionBundle(
            rawSlots,
            annotations,
            bundleAddress: 0xC000,
            bundleSerial: 4);
        InstructionIR instruction = decoded.GetDecodedSlot(7).RequireInstruction();

        Assert.Same(descriptor, instruction.AcceleratorCommandDescriptor);
        Assert.Equal(descriptor.DescriptorReference, instruction.AcceleratorCommandDescriptorReference);
        Assert.Null(instruction.DmaStreamComputeDescriptor);

        MicroOp?[] carriers =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, decoded);
        AcceleratorSubmitMicroOp submit = Assert.IsType<AcceleratorSubmitMicroOp>(carriers[7]);
        Assert.IsNotType<TrapMicroOp>(submit);
        Assert.IsNotType<GenericMicroOp>(submit);
        Assert.Same(descriptor, submit.CommandDescriptor);
        Assert.Equal(descriptor.DescriptorReference, submit.CommandDescriptorReference);
        Assert.Equal(SlotClass.SystemSingleton, submit.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, submit.Placement.PinningKind);
        Assert.Equal((byte)7, submit.Placement.PinnedLaneId);
    }

    [Fact]
    public void L7SdcInstructionTransportSideband_ReferenceWithoutPayload_TrapsAtProjector()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        var instruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.ACCEL_SUBMIT,
            Class = InstructionClass.System,
            SerializationClass = SerializationClass.MemoryOrdered,
            Rd = VLIW_Instruction.NoArchReg,
            Rs1 = VLIW_Instruction.NoArchReg,
            Rs2 = VLIW_Instruction.NoArchReg,
            Imm = 0,
            AcceleratorCommandDescriptorReference = descriptor.DescriptorReference
        };
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[7] = L7SdcPhase03TestCases.CreateNativeInstruction(InstructionsEnum.ACCEL_SUBMIT);
        var bundle = new DecodedInstructionBundle(
            bundleAddress: 0xC100,
            bundleSerial: 5,
            slots: new[] { DecodedInstruction.CreateOccupied(7, instruction) });

        MicroOp?[] carriers =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, bundle);

        TrapMicroOp trap = Assert.IsType<TrapMicroOp>(carriers[7]);
        Assert.Contains(nameof(AcceleratorCommandDescriptor), trap.TrapReason, StringComparison.Ordinal);
        Assert.Contains("without the validated descriptor payload", trap.TrapReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void L7SdcInstructionTransportSideband_DescriptorOnNonSubmitIr_TrapsAtProjector()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        var instruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.ACCEL_POLL,
            Class = InstructionClass.System,
            SerializationClass = SerializationClass.CsrOrdered,
            Rd = VLIW_Instruction.NoArchReg,
            Rs1 = VLIW_Instruction.NoArchReg,
            Rs2 = VLIW_Instruction.NoArchReg,
            Imm = 0,
            AcceleratorCommandDescriptor = descriptor,
            AcceleratorCommandDescriptorReference = descriptor.DescriptorReference
        };
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[7] = L7SdcPhase03TestCases.CreateNativeInstruction(InstructionsEnum.ACCEL_POLL);
        var bundle = new DecodedInstructionBundle(
            bundleAddress: 0xC200,
            bundleSerial: 6,
            slots: new[] { DecodedInstruction.CreateOccupied(7, instruction) });

        MicroOp?[] carriers =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, bundle);

        TrapMicroOp trap = Assert.IsType<TrapMicroOp>(carriers[7]);
        Assert.Contains("non-ACCEL_SUBMIT", trap.TrapReason, StringComparison.Ordinal);
    }

    [Fact]
    public void L7SdcInstructionTransportSideband_RawReservedBitsCannotBeRescuedByValidSideband()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[7] = L7SdcPhase03TestCases.CreateNativeInstruction(InstructionsEnum.ACCEL_SUBMIT);
        rawSlots[7].Reserved = 0x5A;

        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => new VliwDecoderV4().DecodeInstructionBundle(
                rawSlots,
                L7SdcNativeCarrierValidationTests.CreateAnnotations(
                    7,
                    descriptor,
                    L7SdcNativeCarrierValidationTests.CreateSystemSingletonSlotMetadata()),
                bundleAddress: 0xC300));

        Assert.Contains("word0[47:40]", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(DmaStreamComputeMicroOp), ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(GenericMicroOp), ex.Message, StringComparison.Ordinal);
    }
}
