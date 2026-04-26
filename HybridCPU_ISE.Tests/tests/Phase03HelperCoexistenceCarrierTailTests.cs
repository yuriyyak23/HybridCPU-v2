using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03HelperCoexistenceCarrierTailTests
{
    [Fact]
    public void HelperCoexistenceMask_UsesAdmissionWriteRegisters_WhenCarrierWriteRegistersAreTampered()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x9000);

        VLIW_Instruction[] rawSlots = CreateBundle(
            CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 4),
            CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 3, immediate: 8));

        core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, pc: 0x9000, bundleSerial: 68);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor slot0 = transportFacts.Slots[0];
        DecodedBundleSlotDescriptor slot1 = transportFacts.Slots[1];

        MicroOp microOp0 = Assert.IsAssignableFrom<MicroOp>(slot0.MicroOp);
        MicroOp microOp1 = Assert.IsAssignableFrom<MicroOp>(slot1.MicroOp);

        Assert.Equal(new[] { 1 }, microOp0.AdmissionMetadata.WriteRegisters);
        Assert.Equal(new[] { 1 }, microOp1.AdmissionMetadata.WriteRegisters);

        var tamperedSlots = (DecodedBundleSlotDescriptor[])transportFacts.Slots.Clone();
        tamperedSlots[0] = WithWriteRegisters(slot0, new[] { 7 });
        tamperedSlots[1] = WithWriteRegisters(slot1, new[] { 8 });

        (int[] scalarWriteRegisters, byte coexistenceMask, int conflictCount) =
            core.TestResolveIssuePacketCoexistenceFromRuntimeAdmission(
                tamperedSlots,
                scalarIssueMask: 0b0000_0001,
                selectedNonScalarSlotMask: 0b0000_0010);

        Assert.Equal(new[] { 1 }, scalarWriteRegisters);
        Assert.Equal((byte)0, coexistenceMask);
        Assert.Equal(1, conflictCount);
    }

    private static DecodedBundleSlotDescriptor WithWriteRegisters(
        in DecodedBundleSlotDescriptor slot,
        IReadOnlyList<int> writeRegisters)
    {
        return new DecodedBundleSlotDescriptor(
            slot.MicroOp,
            slot.SlotIndex,
            slot.VirtualThreadId,
            slot.OwnerThreadId,
            slot.OpCode,
            slot.ReadRegisters,
            writeRegisters,
            slot.WritesRegister,
            slot.IsMemoryOp,
            slot.IsControlFlow,
            slot.Placement,
            slot.MemoryBankIntent,
            slot.IsFspInjected,
            slot.IsEmptyOrNop);
    }

    private static VLIW_Instruction[] CreateBundle(
        params VLIW_Instruction[] populatedSlots)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        for (int i = 0; i < populatedSlots.Length && i < rawSlots.Length; i++)
        {
            rawSlots[i] = populatedSlots[i];
        }

        return rawSlots;
    }

    private static VLIW_Instruction CreateScalarInstruction(
        InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ushort immediate = 0,
        byte virtualThreadId = 0)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
            Src2Pointer = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = virtualThreadId
        };
    }
}

