using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DirectFactoryVectorComparisonPublicationTailTests
{
    [Theory]
    [InlineData(InstructionsEnum.VCMPEQ)]
    [InlineData(InstructionsEnum.VCMPNE)]
    [InlineData(InstructionsEnum.VCMPLT)]
    [InlineData(InstructionsEnum.VCMPLE)]
    [InlineData(InstructionsEnum.VCMPGT)]
    [InlineData(InstructionsEnum.VCMPGE)]
    public void DirectFactoryVectorComparison_PublishesPredicateCarrierBeforeManualPublication(
        InstructionsEnum opcode)
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xAB00);

        VectorComparisonMicroOp microOp = CreateDirectFactoryVectorComparisonMicroOp(opcode);

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
        Assert.Empty(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal((0x200UL, 8UL), microOp.AdmissionMetadata.ReadMemoryRanges[0]);
        Assert.Equal((0x300UL, 8UL), microOp.AdmissionMetadata.ReadMemoryRanges[1]);
        Assert.Equal(SlotClass.AluClass, slot.Placement.RequiredSlotClass);
        Assert.Equal(microOp.AdmissionMetadata.Placement.PinningKind, slot.Placement.PinningKind);
    }

    private static VectorComparisonMicroOp CreateDirectFactoryVectorComparisonMicroOp(
        InstructionsEnum opcode)
    {
        VLIW_Instruction instruction = CreateVectorInstruction(opcode);
        DecoderContext context = ProjectedVectorDecoderContextBuilder.Create(in instruction);

        return Assert.IsType<VectorComparisonMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)opcode, context));
    }

    private static VLIW_Instruction CreateVectorInstruction(InstructionsEnum opcode)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0x03,
            DestSrc1Pointer = 0x200,
            Src2Pointer = 0x300,
            Immediate = 5,
            StreamLength = 2,
            Stride = 4,
            VirtualThreadId = 0
        };
    }
}

