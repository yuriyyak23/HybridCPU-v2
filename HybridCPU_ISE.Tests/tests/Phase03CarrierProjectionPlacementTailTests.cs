using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03CarrierProjectionPlacementTailTests
{
    [Fact]
    public void LegacySlotCarrierMaterializer_StreamWait_UsesCanonicalPlacementAndSerialization()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.STREAM_WAIT));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        StreamControlMicroOp microOp = Assert.IsType<StreamControlMicroOp>(slotDescriptor.MicroOp);

        Assert.Equal(InstructionClass.SmtVt, microOp.InstructionClass);
        Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);
        Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
        Assert.Equal(SlotClass.SystemSingleton, slotDescriptor.Placement.RequiredSlotClass);
    }

    [Theory]
    [InlineData(InstructionsEnum.YIELD, SystemEventKind.Yield)]
    [InlineData(InstructionsEnum.WFE, SystemEventKind.Wfe)]
    [InlineData(InstructionsEnum.SEV, SystemEventKind.Sev)]
    [InlineData(InstructionsEnum.POD_BARRIER, SystemEventKind.PodBarrier)]
    [InlineData(InstructionsEnum.VT_BARRIER, SystemEventKind.VtBarrier)]
    public void LegacySlotCarrierMaterializer_SmtVtEventProjection_UsesCanonicalPlacementAndSerialization(
        InstructionsEnum opcode,
        SystemEventKind expectedKind)
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(opcode));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        SysEventMicroOp microOp = Assert.IsType<SysEventMicroOp>(slotDescriptor.MicroOp);

        Assert.Equal(expectedKind, microOp.EventKind);
        Assert.Equal(InstructionClass.SmtVt, microOp.InstructionClass);
        Assert.Equal(InstructionClassifier.GetSerializationClass(opcode), microOp.SerializationClass);
        Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
        Assert.Equal(SlotClass.SystemSingleton, slotDescriptor.Placement.RequiredSlotClass);
    }

    [Fact]
    public void LegacySlotCarrierMaterializer_AtomicProjection_NoLongerFallsBackToUnclassifiedPlacement()
    {
        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(InstructionsEnum.AMOADD_W, rd: 3, rs1: 4, rs2: 5));

        DecodedBundleSlotDescriptor slotDescriptor = DecodeAndMaterializeSingleSlot(rawSlots);
        AtomicMicroOp microOp = Assert.IsType<AtomicMicroOp>(slotDescriptor.MicroOp);

        Assert.Equal(InstructionClass.Atomic, microOp.InstructionClass);
        Assert.Equal(SerializationClass.AtomicSerial, microOp.SerializationClass);
        Assert.Equal(SlotClass.LsuClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(SlotClass.LsuClass, slotDescriptor.Placement.RequiredSlotClass);
        Assert.True(slotDescriptor.IsMemoryOp);
    }

    private static DecodedBundleSlotDescriptor DecodeAndMaterializeSingleSlot(
        VLIW_Instruction[] rawSlots)
    {
        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x1000, bundleSerial: 19);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        return DecodedBundleSlotDescriptor.Create(0, Assert.IsAssignableFrom<MicroOp>(carrierBundle[0]));
    }

    private static VLIW_Instruction[] CreateBundle(
        VLIW_Instruction slot0)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = slot0;
        return rawSlots;
    }

    private static VLIW_Instruction CreateScalarInstruction(
        InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ushort immediate = 0)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
            Src2Pointer = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }
}


