using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03DirectFactoryAtomicPublicationTailTests
{
    [Fact]
    public void DirectFactoryAtomic_AmoAddWord_PublishesCanonicalMemoryHazardsBeforeManualPublication()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA500);

        AtomicMicroOp microOp = CreateDirectFactoryAtomicMicroOp(
            InstructionsEnum.AMOADD_W,
            rd: 3,
            rs1: 4,
            rs2: 5,
            address: 0x180);

        AssertCanonicalAtomicPublication(
            core,
            microOp,
            expectedReadRegisters: new[] { 4, 5 },
            expectedWriteRegisters: new[] { 3 },
            expectedSize: 4);
    }

    [Fact]
    public void DirectFactoryAtomic_LrWord_PublishesBaseOnlyHazardsBeforeManualPublication()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0xA540);

        AtomicMicroOp microOp = CreateDirectFactoryAtomicMicroOp(
            InstructionsEnum.LR_W,
            rd: 6,
            rs1: 2,
            rs2: VLIW_Instruction.NoReg,
            address: 0x1C0);

        AssertCanonicalAtomicPublication(
            core,
            microOp,
            expectedReadRegisters: new[] { 2 },
            expectedWriteRegisters: new[] { 6 },
            expectedSize: 4);
    }

    private static void AssertCanonicalAtomicPublication(
        Processor.CPU_Core core,
        AtomicMicroOp microOp,
        int[] expectedReadRegisters,
        int[] expectedWriteRegisters,
        byte expectedSize)
    {
        core.TestSetDecodedBundle(microOp);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot = transportFacts.Slots[0];

        Assert.Same(microOp, slot.MicroOp);
        Assert.True(slot.IsMemoryOp);
        Assert.False(slot.IsControlFlow);
        Assert.True(slot.WritesRegister);
        Assert.Equal(expectedReadRegisters, slot.ReadRegisters);
        Assert.Equal(expectedWriteRegisters, slot.WriteRegisters);
        Assert.Equal(InstructionClass.Atomic, microOp.InstructionClass);
        Assert.Equal(SerializationClass.AtomicSerial, microOp.SerializationClass);
        Assert.Equal(expectedSize, microOp.Size);
        Assert.True(microOp.AdmissionMetadata.IsMemoryOp);
        Assert.Equal(expectedReadRegisters, microOp.AdmissionMetadata.ReadRegisters);
        Assert.Equal(expectedWriteRegisters, microOp.AdmissionMetadata.WriteRegisters);
        Assert.Equal(SlotClass.LsuClass, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(SlotClass.LsuClass, slot.Placement.RequiredSlotClass);
        Assert.Equal(microOp.AdmissionMetadata.Placement.PinningKind, slot.Placement.PinningKind);
    }

    private static AtomicMicroOp CreateDirectFactoryAtomicMicroOp(
        InstructionsEnum opcode,
        ushort rd,
        ushort rs1,
        ushort rs2,
        ulong address)
    {
        VLIW_Instruction instruction = new()
        {
            OpCode = (uint)opcode,
            PredicateMask = 0xFF,
            VirtualThreadId = 0
        };

        DecoderContext context = new()
        {
            OpCode = (uint)opcode,
            Reg1ID = rd,
            Reg2ID = rs1,
            Reg3ID = rs2,
            AuxData = address,
            PredicateMask = instruction.PredicateMask
        };

        return Assert.IsType<AtomicMicroOp>(InstructionRegistry.CreateMicroOp((uint)opcode, context));
    }
}

