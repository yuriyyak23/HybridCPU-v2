using System;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcHardPinnedPlacementTests
{
    [Theory]
    [MemberData(nameof(L7SdcPhase03TestCases.AllOpcodes), MemberType = typeof(L7SdcPhase03TestCases))]
    public void L7SdcHardPinnedPlacement_CarriersUseSystemSingletonLane7Only(
        InstructionsEnum opcode,
        ushort _,
        string mnemonic,
        SerializationClass expectedSerialization,
        Type expectedCarrierType)
    {
        Assert.Equal(mnemonic, OpcodeRegistry.GetMnemonicOrHex((uint)opcode));
        MicroOp carrier = InstructionRegistry.CreateMicroOp(
            (uint)opcode,
            new DecoderContext { OpCode = (uint)opcode });

        Assert.Equal(expectedCarrierType, carrier.GetType());
        Assert.Equal(expectedSerialization, carrier.SerializationClass);
        Assert.Equal(SlotClass.SystemSingleton, carrier.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, carrier.Placement.PinningKind);
        Assert.Equal((byte)7, carrier.Placement.PinnedLaneId);
        Assert.Equal(SlotClass.SystemSingleton, carrier.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, carrier.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal((byte)7, carrier.AdmissionMetadata.Placement.PinnedLaneId);

        Assert.NotEqual(SlotClass.BranchControl, carrier.Placement.RequiredSlotClass);
        Assert.NotEqual(SlotClass.DmaStreamClass, carrier.Placement.RequiredSlotClass);
        Assert.Equal((byte)0b_1000_0000, SlotClassLaneMap.GetLaneMask(SlotClass.SystemSingleton));
    }

    [Fact]
    public void L7SdcHardPinnedPlacement_AccelSubmitCannotDecodeOrOccupyLane6()
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[6] = L7SdcPhase03TestCases.CreateNativeInstruction(InstructionsEnum.ACCEL_SUBMIT);

        var decoder = new VliwDecoderV4();
        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7000, bundleSerial: 1));

        Assert.Contains("L7-SDC", ex.Message, StringComparison.Ordinal);
        Assert.Contains("lane7", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("slot 6", ex.Message, StringComparison.OrdinalIgnoreCase);

        MicroOp submit = InstructionRegistry.CreateMicroOp(
            (uint)InstructionsEnum.ACCEL_SUBMIT,
            new DecoderContext { OpCode = (uint)InstructionsEnum.ACCEL_SUBMIT });
        Assert.Equal(SlotClass.SystemSingleton, submit.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, submit.Placement.PinningKind);
        Assert.Equal((byte)7, submit.Placement.PinnedLaneId);
        Assert.NotEqual(SlotClass.DmaStreamClass, submit.Placement.RequiredSlotClass);
    }

    [Fact]
    public void L7SdcHardPinnedPlacement_Lane7DecodeProjectsAcceleratorSubmit_NotTrapOrGenericFallback()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[7] = L7SdcPhase03TestCases.CreateNativeInstruction(InstructionsEnum.ACCEL_SUBMIT);

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle decodedBundle =
            decoder.DecodeInstructionBundle(
                rawSlots,
                L7SdcNativeCarrierValidationTests.CreateAnnotations(
                    7,
                    descriptor,
                    L7SdcNativeCarrierValidationTests.CreateSystemSingletonSlotMetadata()),
                bundleAddress: 0x7100,
                bundleSerial: 2);

        DecodedInstruction decodedSlot = decodedBundle.GetDecodedSlot(7);
        Assert.True(decodedSlot.IsOccupied);
        InstructionIR instruction = decodedSlot.RequireInstruction();
        Assert.Equal(InstructionsEnum.ACCEL_SUBMIT, (InstructionsEnum)instruction.CanonicalOpcode.Value);
        Assert.Equal(InstructionClass.System, instruction.Class);
        Assert.Equal(SerializationClass.MemoryOrdered, instruction.SerializationClass);
        Assert.Same(descriptor, instruction.AcceleratorCommandDescriptor);

        MicroOp?[] carriers =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, decodedBundle);
        AcceleratorSubmitMicroOp submit = Assert.IsType<AcceleratorSubmitMicroOp>(carriers[7]);
        Assert.IsNotType<TrapMicroOp>(submit);
        Assert.IsNotType<GenericMicroOp>(submit);
        Assert.Equal(SlotClass.SystemSingleton, submit.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, submit.Placement.PinningKind);
        Assert.Equal((byte)7, submit.Placement.PinnedLaneId);
        Assert.Same(descriptor, submit.CommandDescriptor);
    }

    [Fact]
    public void L7SdcHardPinnedPlacement_DmaStreamComputeRemainsLane6DmaStreamClass()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var dma = new DmaStreamComputeMicroOp(descriptor);

        Assert.Equal(InstructionClass.Memory, dma.InstructionClass);
        Assert.Equal(SerializationClass.MemoryOrdered, dma.SerializationClass);
        Assert.Equal(SlotClass.DmaStreamClass, dma.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, dma.Placement.PinningKind);
        Assert.Equal((byte)0b_0100_0000, SlotClassLaneMap.GetLaneMask(SlotClass.DmaStreamClass));
        Assert.Equal(1, SlotClassLaneMap.GetClassCapacity(SlotClass.DmaStreamClass));
        Assert.NotEqual(SlotClass.SystemSingleton, dma.Placement.RequiredSlotClass);
    }
}
