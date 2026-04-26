using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DirectFactoryNopPublicationTailTests
{
    [Fact]
    public void DirectFactoryExplicitNope_PublishesUnclassifiedEmptyTruthBeforeManualPublication()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA4F0);

        NopMicroOp microOp = CreateExplicitNopeMicroOp();
        core.TestSetDecodedBundle(microOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.Same(microOp, slot.MicroOp);
        Assert.True(slot.IsEmptyOrNop);
        Assert.False(slot.IsMemoryOp);
        Assert.False(slot.IsControlFlow);
        Assert.False(slot.WritesRegister);
        Assert.Empty(slot.ReadRegisters);
        Assert.Empty(slot.WriteRegisters);
        Assert.Equal(SlotClass.Unclassified, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotClass.Unclassified, slot.Placement.RequiredSlotClass);
        Assert.Equal((byte)0, transportFacts.ValidNonEmptyMask);
        Assert.Equal((byte)0, transportFacts.AdmissionPrep.ScalarCandidateMask);
        Assert.Equal((byte)0, transportFacts.AdmissionPrep.AuxiliaryOpMask);
    }

    private static NopMicroOp CreateExplicitNopeMicroOp()
    {
        VLIW_Instruction instruction = CreateExplicitNopeInstruction();
        DecoderContext context = new()
        {
            OpCode = (uint)InstructionsEnum.Nope,
            Reg1ID = instruction.Reg1ID,
            Reg2ID = instruction.Reg2ID,
            Reg3ID = instruction.Reg3ID,
            AuxData = instruction.Src2Pointer,
            PredicateMask = instruction.PredicateMask
        };

        return Assert.IsType<NopMicroOp>(InstructionRegistry.CreateMicroOp((uint)InstructionsEnum.Nope, context));
    }

    private static VLIW_Instruction CreateExplicitNopeInstruction()
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.Nope,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }
}

