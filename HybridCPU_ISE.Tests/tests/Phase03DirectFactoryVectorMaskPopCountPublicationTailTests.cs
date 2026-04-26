using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DirectFactoryVectorMaskPopCountPublicationTailTests
{
    [Fact]
    public void DirectFactoryVectorMaskPopCount_PublishesScalarWritebackTruthBeforeManualPublication()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA700);

        VectorMaskPopCountMicroOp microOp = CreateDirectFactoryVectorMaskPopCountMicroOp();

        core.TestSetDecodedBundle(microOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.Same(microOp, slot.MicroOp);
        Assert.True(slot.IsVectorOp);
        Assert.False(slot.IsMemoryOp);
        Assert.True(slot.WritesRegister);
        Assert.Equal(new[] { 6 }, slot.WriteRegisters);
        Assert.Equal(slot.WriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.True(microOp.WritesRegister);
        Assert.Equal((ushort)6, microOp.DestRegID);
        Assert.Equal(new[] { 6 }, microOp.AdmissionMetadata.WriteRegisters);
        Assert.Empty(microOp.AdmissionMetadata.ReadMemoryRanges);
        Assert.Empty(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(microOp.AdmissionMetadata.Placement.RequiredSlotClass, slot.Placement.RequiredSlotClass);
        Assert.Equal(microOp.AdmissionMetadata.Placement.PinningKind, slot.Placement.PinningKind);
    }

    private static VectorMaskPopCountMicroOp CreateDirectFactoryVectorMaskPopCountMicroOp()
    {
        VLIW_Instruction instruction = CreateVectorInstruction();
        DecoderContext context = ProjectedVectorDecoderContextBuilder.Create(in instruction);

        return Assert.IsType<VectorMaskPopCountMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.VPOPC, context));
    }

    private static VLIW_Instruction CreateVectorInstruction()
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.VPOPC,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            Immediate = (ushort)(3 | (6 << 8)),
            StreamLength = 8,
            VirtualThreadId = 0
        };
    }
}

