using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DirectFactoryVectorBinaryPublicationTailTests
{
    [Theory]
    [InlineData(InstructionsEnum.VADD)]
    [InlineData(InstructionsEnum.VMUL)]
    [InlineData(InstructionsEnum.VXOR)]
    [InlineData(InstructionsEnum.VMINU)]
    public void DirectFactoryVectorBinary_ProjectsInPlaceMemoryShapeBeforeManualPublication(
        InstructionsEnum opcode)
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xAC00);

        VectorBinaryOpMicroOp microOp = CreateDirectFactoryVectorBinaryMicroOp(opcode);

        core.TestSetDecodedBundle(microOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.Same(microOp, slot.MicroOp);
        Assert.True(slot.IsVectorOp);
        Assert.False(slot.IsMemoryOp);
        Assert.False(slot.WritesRegister);
        Assert.Empty(slot.WriteRegisters);
        Assert.False(microOp.IsMemoryOp);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(2, microOp.AdmissionMetadata.ReadMemoryRanges.Count);
        Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal((0x220UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[0]);
        Assert.Equal((0x320UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[1]);
        Assert.Equal((0x220UL, 16UL), microOp.AdmissionMetadata.WriteMemoryRanges[0]);
        Assert.Equal(SlotClass.AluClass, slot.Placement.RequiredSlotClass);
        Assert.Equal(microOp.AdmissionMetadata.Placement.PinningKind, slot.Placement.PinningKind);
    }

    private static VectorBinaryOpMicroOp CreateDirectFactoryVectorBinaryMicroOp(
        InstructionsEnum opcode)
    {
        VLIW_Instruction instruction = CreateVectorInstruction(opcode);
        DecoderContext context = ProjectedVectorDecoderContextBuilder.Create(in instruction);

        return Assert.IsType<VectorBinaryOpMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)opcode, context));
    }

    private static VLIW_Instruction CreateVectorInstruction(InstructionsEnum opcode)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0x0D,
            DestSrc1Pointer = 0x220,
            Src2Pointer = 0x320,
            StreamLength = 4,
            Stride = 4,
            VirtualThreadId = 0
        };
    }
}

