using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Legality;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03ScalarClusterCarrierTailTests
{
    [Fact]
    public void ScalarClusterPreparation_UsesPublishedExecutionHeader_WhenCarrierHeaderIsTampered()
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(0x8000);

        VLIW_Instruction[] rawSlots = CreateBundle(
            CreateScalarInstruction(InstructionsEnum.ADDI, rd: 1, rs1: 2, immediate: 4, virtualThreadId: 0),
            CreateScalarInstruction(InstructionsEnum.ADDI, rd: 3, rs1: 4, immediate: 8, virtualThreadId: 1));

        core.TestSetCanonicalDecodedBundleTransportFacts(rawSlots, pc: 0x8000, bundleSerial: 67);

        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        Assert.Equal((byte)0b0000_0011, transportFacts.AdmissionPrep.WideReadyScalarMask);

        DecodedBundleSlotDescriptor slot0 = transportFacts.Slots[0];
        DecodedBundleSlotDescriptor slot1 = transportFacts.Slots[1];

        MicroOp microOp0 = Assert.IsAssignableFrom<MicroOp>(slot0.MicroOp);
        MicroOp microOp1 = Assert.IsAssignableFrom<MicroOp>(slot1.MicroOp);

        microOp0.VirtualThreadId = 0;
        microOp0.OwnerThreadId = 0;
        microOp1.VirtualThreadId = 2;
        microOp1.OwnerThreadId = 2;

        var tamperedSlots = (DecodedBundleSlotDescriptor[])transportFacts.Slots.Clone();
        tamperedSlots[0] = WithRuntimeHeader(slot0, virtualThreadId: 0, ownerThreadId: 0, opCode: slot0.OpCode);
        tamperedSlots[1] = WithRuntimeHeader(slot1, virtualThreadId: 0, ownerThreadId: 3, opCode: (uint)InstructionsEnum.ORI);

        ClusterIssuePreparation clusterPreparation = ClusterIssuePreparation.Create(
            transportFacts.PC,
            tamperedSlots,
            transportFacts.AdmissionPrep,
            transportFacts.DependencySummary);

        Assert.Equal(2, clusterPreparation.ScalarClusterGroup.Count);

        VtScalarCandidateSummary vtSummary = clusterPreparation.ScalarClusterGroup.BuildVtSummary();
        Assert.Equal(1, vtSummary.ActiveVtCount);
        Assert.Equal(2, vtSummary.Vt0Count);
        Assert.Equal(0, vtSummary.Vt2Count);

        ScalarClusterIssueEntry[] entries = clusterPreparation.ScalarClusterGroup.Entries;
        Assert.Equal(0, entries[0].VirtualThreadId);
        Assert.Equal(0, entries[0].OwnerThreadId);
        Assert.Equal(slot0.OpCode, entries[0].OpCode);

        Assert.Equal(0, entries[1].VirtualThreadId);
        Assert.Equal(3, entries[1].OwnerThreadId);
        Assert.Equal((uint)InstructionsEnum.ORI, entries[1].OpCode);
        Assert.NotEqual(microOp1.OwnerThreadId, entries[1].OwnerThreadId);
        Assert.NotEqual(microOp1.OpCode, entries[1].OpCode);

        DecodedBundleSlotDescriptor republishedEntrySlot = entries[1].SlotDescriptor;
        Assert.Same(microOp1, republishedEntrySlot.MicroOp);
        Assert.Equal(entries[1].VirtualThreadId, republishedEntrySlot.VirtualThreadId);
        Assert.Equal(entries[1].OwnerThreadId, republishedEntrySlot.OwnerThreadId);
        Assert.Equal(entries[1].OpCode, republishedEntrySlot.OpCode);

        IssuePacketLane issueLane = IssuePacketLane.Create(1, entries[1]);
        Assert.Same(microOp1, issueLane.MicroOp);
        Assert.Equal(entries[1].VirtualThreadId, issueLane.VirtualThreadId);
        Assert.Equal(entries[1].OwnerThreadId, issueLane.OwnerThreadId);
        Assert.Equal(entries[1].OpCode, issueLane.OpCode);

        ClusterIssueIntent intent = ClusterIssueIntent.FromScalarClusterIssueGroup(
            clusterPreparation.ScalarClusterGroup);
        ScalarClusterIssueGroup roundTrippedGroup = intent.ToScalarClusterIssueGroup();
        ScalarClusterIssueEntry roundTrippedEntry = roundTrippedGroup.Entries[1];

        Assert.Same(microOp1, roundTrippedEntry.MicroOp);
        Assert.Equal(entries[1].VirtualThreadId, roundTrippedEntry.VirtualThreadId);
        Assert.Equal(entries[1].OwnerThreadId, roundTrippedEntry.OwnerThreadId);
        Assert.Equal(entries[1].OpCode, roundTrippedEntry.OpCode);
    }

    private static DecodedBundleSlotDescriptor WithRuntimeHeader(
        in DecodedBundleSlotDescriptor slot,
        int virtualThreadId,
        int ownerThreadId,
        uint opCode)
    {
        return new DecodedBundleSlotDescriptor(
            slot.MicroOp,
            slot.SlotIndex,
            virtualThreadId,
            ownerThreadId,
            opCode,
            slot.ReadRegisters,
            slot.WriteRegisters,
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

