using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DirectFactoryUnconditionalBranchPublicationTailTests
{
    [Fact]
    public void DirectFactoryJalr_ProjectsCanonicalLinkAndBaseRegistersBeforeManualPublication()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xB580);

        BranchMicroOp microOp = CreateDirectFactoryUnconditionalBranchMicroOp(
            InstructionsEnum.JALR,
            rd: 5,
            rs1: 3,
            immediate: 0x0018);

        core.TestSetDecodedBundle(microOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.Same(microOp, slot.MicroOp);
        Assert.Equal(InstructionClass.ControlFlow, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.True(microOp.WritesRegister);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.True(microOp.AdmissionMetadata.WritesRegister);
        Assert.Equal(SlotClass.BranchControl, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal((byte)7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
        Assert.Equal((ushort)5, microOp.DestRegID);
        Assert.Equal((ushort)3, microOp.Reg1ID);
        Assert.Equal(new[] { 3 }, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(new[] { 5 }, microOp.AdmissionMetadata.WriteRegisters);
        Assert.True(slot.IsControlFlow);
        Assert.False(slot.IsMemoryOp);
        Assert.True(slot.WritesRegister);
        Assert.Equal(new[] { 3 }, slot.ReadRegisters);
        Assert.Equal(new[] { 5 }, slot.WriteRegisters);
        Assert.Equal(SlotClass.BranchControl, slot.Placement.RequiredSlotClass);
    }

    private static BranchMicroOp CreateDirectFactoryUnconditionalBranchMicroOp(
        InstructionsEnum opcode,
        byte rd,
        byte rs1,
        ushort immediate)
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, 0),
            Immediate = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };

        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            Immediate = instruction.Immediate,
            HasImmediate = true,
            PackedRegisterTriplet = instruction.DestSrc1Pointer,
            HasPackedRegisterTriplet = true,
            Reg1ID = instruction.Reg1ID,
            Reg2ID = instruction.Reg2ID,
            Reg3ID = instruction.Reg3ID,
            AuxData = instruction.Src2Pointer,
            PredicateMask = instruction.PredicateMask
        };

        return Assert.IsType<BranchMicroOp>(InstructionRegistry.CreateMicroOp((uint)opcode, context));
    }
}

