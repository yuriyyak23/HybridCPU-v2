using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DirectFactorySystemEventPublicationTailTests
{
    [Theory]
    [InlineData(InstructionsEnum.FENCE)]
    [InlineData(InstructionsEnum.YIELD)]
    [InlineData(InstructionsEnum.VT_BARRIER)]
    public void DirectFactorySystemEventPublication_UsesPublishedOpcodeSemantics(
        InstructionsEnum opcode)
    {
        Assert.True(
            OpcodeRegistry.TryGetPublishedSemantics(
                opcode,
                out InstructionClass publishedClass,
                out SerializationClass publishedSerialization));

        SysEventMicroOp microOp = CreateDirectFactorySystemEventMicroOp(opcode);

        Assert.Equal(publishedClass, microOp.InstructionClass);
        Assert.Equal(publishedSerialization, microOp.SerializationClass);
    }

    [Fact]
    public void DirectFactoryFence_ProjectsCanonicalSystemClassAndMemoryOrderedSerializationBeforeManualPublication()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA5C0);

        SysEventMicroOp microOp = CreateDirectFactorySystemEventMicroOp(InstructionsEnum.FENCE);

        core.TestSetDecodedBundle(microOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.Same(microOp, slot.MicroOp);
        Assert.Equal(SystemEventKind.Fence, microOp.EventKind);
        Assert.Equal(SystemEventOrderGuarantee.DrainMemory, microOp.OrderGuarantee);
        Assert.Equal(InstructionClass.System, microOp.InstructionClass);
        Assert.Equal(SerializationClass.MemoryOrdered, microOp.SerializationClass);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.False(microOp.AdmissionMetadata.WritesRegister);
        Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal((byte)7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
        Assert.Empty(microOp.AdmissionMetadata.ReadRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.False(slot.IsMemoryOp);
        Assert.False(slot.IsControlFlow);
        Assert.False(slot.WritesRegister);
        Assert.Empty(slot.ReadRegisters);
        Assert.Empty(slot.WriteRegisters);
        Assert.Equal(SlotClass.SystemSingleton, slot.Placement.RequiredSlotClass);
    }

    [Fact]
    public void DirectFactoryYield_ProjectsCanonicalSmtVtClassAndFreeSerializationBeforeManualPublication()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA5D0);

        SysEventMicroOp microOp = CreateDirectFactorySystemEventMicroOp(InstructionsEnum.YIELD);

        core.TestSetDecodedBundle(microOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.Same(microOp, slot.MicroOp);
        Assert.Equal(SystemEventKind.Yield, microOp.EventKind);
        Assert.Equal(SystemEventOrderGuarantee.None, microOp.OrderGuarantee);
        Assert.Equal(InstructionClass.SmtVt, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.False(microOp.AdmissionMetadata.WritesRegister);
        Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal((byte)7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
        Assert.Empty(microOp.AdmissionMetadata.ReadRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.False(slot.IsMemoryOp);
        Assert.False(slot.IsControlFlow);
        Assert.False(slot.WritesRegister);
        Assert.Empty(slot.ReadRegisters);
        Assert.Empty(slot.WriteRegisters);
        Assert.Equal(SlotClass.SystemSingleton, slot.Placement.RequiredSlotClass);
    }

    [Fact]
    public void DirectFactoryVtBarrier_ProjectsCanonicalSmtVtClassAndFullSerialSerializationBeforeManualPublication()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA5E0);

        SysEventMicroOp microOp = CreateDirectFactorySystemEventMicroOp(InstructionsEnum.VT_BARRIER);

        core.TestSetDecodedBundle(microOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.Same(microOp, slot.MicroOp);
        Assert.Equal(SystemEventKind.VtBarrier, microOp.EventKind);
        Assert.Equal(SystemEventOrderGuarantee.None, microOp.OrderGuarantee);
        Assert.Equal(InstructionClass.SmtVt, microOp.InstructionClass);
        Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);
        Assert.False(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.False(microOp.AdmissionMetadata.WritesRegister);
        Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal((byte)7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
        Assert.Empty(microOp.AdmissionMetadata.ReadRegisters);
        Assert.Empty(microOp.AdmissionMetadata.WriteRegisters);
        Assert.False(slot.IsMemoryOp);
        Assert.False(slot.IsControlFlow);
        Assert.False(slot.WritesRegister);
        Assert.Empty(slot.ReadRegisters);
        Assert.Empty(slot.WriteRegisters);
        Assert.Equal(SlotClass.SystemSingleton, slot.Placement.RequiredSlotClass);
    }

    private static SysEventMicroOp CreateDirectFactorySystemEventMicroOp(InstructionsEnum opcode)
    {
        VLIW_Instruction instruction = CreateSystemInstruction(opcode);
        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            Reg1ID = instruction.Reg1ID,
            Reg2ID = instruction.Reg2ID,
            Reg3ID = instruction.Reg3ID,
            AuxData = instruction.Src2Pointer,
            PredicateMask = instruction.PredicateMask
        };

        return Assert.IsType<SysEventMicroOp>(InstructionRegistry.CreateMicroOp((uint)opcode, context));
    }

    private static VLIW_Instruction CreateSystemInstruction(InstructionsEnum opcode)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }
}

