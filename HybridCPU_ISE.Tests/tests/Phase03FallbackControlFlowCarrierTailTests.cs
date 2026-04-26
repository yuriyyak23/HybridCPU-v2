using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Legality;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03FallbackControlFlowCarrierTailTests
{
    [Fact]
    public void DecodeFullBundle_FallbackTrapPath_PreservesCanonicalPcRelativeBranchTargetInCarrierSummary()
    {
        const ulong pc = 0x5C00;
        const ushort immediate = 0x0040;
        const ulong expectedTargetAddress = pc + immediate;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc);

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateInvalidOpcodeInstruction(),
                CreateControlInstruction(
                    InstructionsEnum.BEQ,
                    rs1: 3,
                    rs2: 4,
                    immediate: immediate));

        core.TestDecodeFetchedBundle(rawSlots, pc);

        var canonicalBundle = core.GetCurrentDecodedInstructionBundle();
        BundleLegalityDescriptor legalityDescriptor = core.GetCurrentBundleLegalityDescriptor();
        DecodedBundleTransportFacts transportFacts = core.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor branchSlot = transportFacts.Slots[1];
        BranchMicroOp branchMicroOp = Assert.IsType<BranchMicroOp>(branchSlot.MicroOp);

        Assert.True(canonicalBundle.HasDecodeFault);
        Assert.True(canonicalBundle.IsEmpty);
        Assert.True(legalityDescriptor.HasDecodeFault);
        Assert.True(legalityDescriptor.IsEmpty);
        Assert.Equal((byte)0b0000_0011, transportFacts.ValidNonEmptyMask);
        Assert.Equal((byte)0b0000_0011, transportFacts.AdmissionPrep.AuxiliaryOpMask);
        Assert.True(branchSlot.IsControlFlow);
        Assert.False(branchSlot.IsMemoryOp);
        Assert.Equal(SlotClass.BranchControl, branchSlot.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, branchSlot.Placement.PinningKind);
        Assert.Equal(7, branchSlot.Placement.PinnedLaneId);
        Assert.Equal(expectedTargetAddress, branchMicroOp.TargetAddress);
    }

    private static VLIW_Instruction[] CreateBundle(
        VLIW_Instruction slot0,
        VLIW_Instruction slot1)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = slot0;
        rawSlots[1] = slot1;
        return rawSlots;
    }

    private static VLIW_Instruction CreateInvalidOpcodeInstruction() =>
        new()
        {
            OpCode = 14u,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = 0,
            Src2Pointer = 0,
            Immediate = 0,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };

    private static VLIW_Instruction CreateControlInstruction(
        InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ulong targetPc = 0,
        ushort immediate = 0)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
            Src2Pointer = targetPc,
            Immediate = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }
}

