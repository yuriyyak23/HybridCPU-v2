using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DirectFactoryScalarImmediatePublicationTailTests
{
    [Fact]
    public void DirectFactoryAddiNegativeImmediate_ProjectsCanonicalSignExtendedImmediateBeforeManualPublication()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA500);

        ScalarALUMicroOp microOp = CreateScalarImmediateMicroOp(
            InstructionsEnum.ADDI,
            destinationRegister: 7,
            sourceRegister: 5,
            immediate: 0xFFFF);

        core.TestSetDecodedBundle(microOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.Same(microOp, slot.MicroOp);
        Assert.Equal(unchecked((ulong)-1L), microOp.Immediate);
        Assert.True(microOp.UsesImmediate);
        Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.Equal(SlotClass.AluClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.False(microOp.AdmissionMetadata.HasSideEffects);
        Assert.True(microOp.AdmissionMetadata.WritesRegister);
        Assert.Equal(new[] { 5 }, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(new[] { 7 }, microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(SlotClass.AluClass, slot.Placement.RequiredSlotClass);
        Assert.False(slot.IsMemoryOp);
        Assert.True(slot.WritesRegister);
        Assert.Equal(new[] { 5 }, slot.ReadRegisters);
        Assert.Equal(new[] { 7 }, slot.WriteRegisters);
        Assert.Equal((byte)0b0000_0001, transportFacts.AdmissionPrep.ScalarCandidateMask);
        Assert.Equal((byte)0, transportFacts.AdmissionPrep.AuxiliaryOpMask);
    }

    private static ScalarALUMicroOp CreateScalarImmediateMicroOp(
        InstructionsEnum opcode,
        byte destinationRegister,
        byte sourceRegister,
        ushort immediate)
    {
        var instruction = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(destinationRegister, sourceRegister, 0),
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
            Reg1ID = instruction.Reg1ID,
            Reg2ID = instruction.Reg2ID,
            Reg3ID = instruction.Reg3ID,
            AuxData = instruction.Src2Pointer,
            PredicateMask = instruction.PredicateMask
        };

        return Assert.IsType<ScalarALUMicroOp>(InstructionRegistry.CreateMicroOp((uint)opcode, context));
    }
}

