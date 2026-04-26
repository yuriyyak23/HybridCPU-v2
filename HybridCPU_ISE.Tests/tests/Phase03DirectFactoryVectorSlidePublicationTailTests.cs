using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DirectFactoryVectorSlidePublicationTailTests
{
    [Theory]
    [InlineData(InstructionsEnum.VSLIDEUP)]
    [InlineData(InstructionsEnum.VSLIDEDOWN)]
    public void DirectFactoryVectorSlide_ProjectsSingleSurfaceMemoryRangesBeforeManualPublication(
        InstructionsEnum opcode)
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA600);

        VectorSlideMicroOp microOp = CreateDirectFactoryVectorSlideMicroOp(opcode);

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
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Single(microOp.AdmissionMetadata.ReadMemoryRanges);
        Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal((0x2C0UL, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[0]);
        Assert.Equal((0x2C0UL, 16UL), microOp.AdmissionMetadata.WriteMemoryRanges[0]);
        Assert.Equal(SlotClass.AluClass, slot.Placement.RequiredSlotClass);
        Assert.Equal(microOp.AdmissionMetadata.Placement.PinningKind, slot.Placement.PinningKind);
    }

    private static VectorSlideMicroOp CreateDirectFactoryVectorSlideMicroOp(InstructionsEnum opcode)
    {
        VLIW_Instruction instruction = CreateVectorInstruction(opcode);
        DecoderContext context = ProjectedVectorDecoderContextBuilder.Create(in instruction);

        return Assert.IsType<VectorSlideMicroOp>(InstructionRegistry.CreateMicroOp((uint)opcode, context));
    }

    private static VLIW_Instruction CreateVectorInstruction(InstructionsEnum opcode)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = 0x2C0,
            Immediate = 1,
            StreamLength = 4,
            Stride = 4,
            VirtualThreadId = 0
        };
    }
}

