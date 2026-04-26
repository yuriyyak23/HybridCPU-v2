using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03ScalarMExtensionPublicationTailTests
{
    [Theory]
    [InlineData(InstructionsEnum.MULH)]
    [InlineData(InstructionsEnum.MULHU)]
    [InlineData(InstructionsEnum.MULHSU)]
    [InlineData(InstructionsEnum.DIVU)]
    [InlineData(InstructionsEnum.REM)]
    [InlineData(InstructionsEnum.REMU)]
    public void DirectFactoryScalarMExtension_PublishesCanonicalScalarFootprintBeforeManualPublication(
        InstructionsEnum opcode)
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA700);

        ScalarALUMicroOp microOp = CreateDirectFactoryScalarMExtensionMicroOp(opcode);
        core.TestSetDecodedBundle(microOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.Same(microOp, slot.MicroOp);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.True(microOp.AdmissionMetadata.WritesRegister);
        Assert.Equal(new[] { 5, 6 }, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(new[] { 7 }, microOp.AdmissionMetadata.WriteRegisters);
        Assert.False(slot.IsMemoryOp);
        Assert.True(slot.WritesRegister);
        Assert.Equal(new[] { 5, 6 }, slot.ReadRegisters);
        Assert.Equal(new[] { 7 }, slot.WriteRegisters);
        Assert.Equal(SlotClass.AluClass, slot.Placement.RequiredSlotClass);
        Assert.Equal((byte)0b0000_0001, transportFacts.AdmissionPrep.ScalarCandidateMask);
        Assert.Equal((byte)0, transportFacts.AdmissionPrep.AuxiliaryOpMask);
    }

    [Theory]
    [InlineData(InstructionsEnum.MULH)]
    [InlineData(InstructionsEnum.MULHU)]
    [InlineData(InstructionsEnum.MULHSU)]
    [InlineData(InstructionsEnum.DIVU)]
    [InlineData(InstructionsEnum.REM)]
    [InlineData(InstructionsEnum.REMU)]
    public void DecodeFullBundle_ScalarMExtension_MaterializesScalarAluCarrierInsteadOfTrap(
        InstructionsEnum opcode)
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x4D00, activeVtId: 3);
        core.ActiveVirtualThreadId = 3;
        core.WriteVirtualThreadPipelineState(3, PipelineState.Task);

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateScalarInstruction(opcode, rd: 7, rs1: 5, rs2: 6));

        core.TestDecodeFetchedBundle(rawSlots, pc: 0x4D00);

        DecodedInstructionBundle canonicalBundle = core.GetCurrentDecodedInstructionBundle();
        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];
        ScalarALUMicroOp microOp = Assert.IsType<ScalarALUMicroOp>(slot.MicroOp);

        Assert.False(canonicalBundle.HasDecodeFault);
        Assert.Equal((uint)opcode, slot.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(new[] { 5, 6 }, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(new[] { 7 }, microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(3, slot.VirtualThreadId);
        Assert.Equal(3, microOp.VirtualThreadId);
        Assert.Equal(3, microOp.OwnerThreadId);
        Assert.Equal(0, microOp.OwnerContextId);
        Assert.Equal((byte)0b0000_0001, transportFacts.AdmissionPrep.ScalarCandidateMask);
        Assert.Equal((byte)0, transportFacts.AdmissionPrep.AuxiliaryOpMask);
    }

    private static ScalarALUMicroOp CreateDirectFactoryScalarMExtensionMicroOp(
        InstructionsEnum opcode)
    {
        VLIW_Instruction instruction =
            CreateScalarInstruction(opcode, rd: 7, rs1: 5, rs2: 6);
        DecoderContext context = CreateDecoderContext(in instruction);
        return Assert.IsType<ScalarALUMicroOp>(InstructionRegistry.CreateMicroOp((uint)opcode, context));
    }

    private static DecoderContext CreateDecoderContext(
        in VLIW_Instruction instruction)
    {
        return new DecoderContext
        {
            OpCode = instruction.OpCode,
            Reg1ID = instruction.Reg1ID,
            Reg2ID = instruction.Reg2ID,
            Reg3ID = instruction.Reg3ID,
            AuxData = instruction.Src2Pointer,
            PredicateMask = instruction.PredicateMask
        };
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
        byte rs2 = 0)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }
}

