using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DirectFactoryVectorMaskPublicationTailTests
{
    [Theory]
    [InlineData(InstructionsEnum.VMAND)]
    [InlineData(InstructionsEnum.VMOR)]
    [InlineData(InstructionsEnum.VMXOR)]
    [InlineData(InstructionsEnum.VMNOT)]
    public void DirectFactoryVectorMask_PublishesPredicateCarrierBeforeManualPublication(
        InstructionsEnum opcode)
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xB000);

        VectorMaskOpMicroOp microOp = CreateDirectFactoryVectorMaskMicroOp(opcode);

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
        Assert.Empty(microOp.AdmissionMetadata.ReadRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.Empty(microOp.AdmissionMetadata.ReadMemoryRanges);
        Assert.Empty(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(SlotClass.AluClass, slot.Placement.RequiredSlotClass);
        Assert.Equal(microOp.AdmissionMetadata.Placement.PinningKind, slot.Placement.PinningKind);
    }

    private static VectorMaskOpMicroOp CreateDirectFactoryVectorMaskMicroOp(
        InstructionsEnum opcode)
    {
        VLIW_Instruction instruction = CreateVectorInstruction(opcode);
        DecoderContext context = ProjectedVectorDecoderContextBuilder.Create(in instruction);

        return Assert.IsType<VectorMaskOpMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)opcode, context));
    }

    private static VLIW_Instruction CreateVectorInstruction(InstructionsEnum opcode)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            Immediate = (ushort)(1 | (2 << 4) | (3 << 8)),
            StreamLength = 8,
            VirtualThreadId = 0
        };
    }
}

