using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09FallbackDecodeFaultTrapBoundaryTests
{
    [Fact]
    public void FallbackDecodeFaultBundle_WithTakenCanonicalBranch_FiltersBranchIssueSurfaceAndDeliversTrapBoundary()
    {
        const ulong faultingPc = 0x8F00;
        const ulong trapHandlerPc = 0x1900;
        const ushort branchImmediate = 0x0040;
        const ulong unexpectedBranchTarget = faultingPc + branchImmediate;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(faultingPc, activeVtId: 0);
        core.WriteCommittedPc(0, faultingPc);
        core.WriteCommittedArch(0, 1, 0x55UL);
        core.WriteCommittedArch(0, 2, 0x55UL);
        core.Csr.Write(CsrAddresses.Mtvec, trapHandlerPc, PrivilegeLevel.Machine);

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateInvalidOpcodeInstruction(),
                CreateControlInstruction(
                    InstructionsEnum.BEQ,
                    rs1: 1,
                    rs2: 2,
                    immediate: branchImmediate));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc: faultingPc);

        Processor.CPU_Core.DecodeStage decodeStage = core.GetDecodeStage();
        RuntimeClusterAdmissionDecisionDraft decisionDraft = core.GetDecodeStageAdmissionDecisionDraft();
        BundleIssuePacket issuePacket = core.GetDecodeStageIssuePacket();
        IssuePacketLane trapIssueLane = GetOccupiedTrapIssueLane(issuePacket);

        Assert.True(decodeStage.Valid);
        Assert.IsType<TrapMicroOp>(decodeStage.MicroOp);
        Assert.Equal(RuntimeClusterAdmissionExecutionMode.AuxiliaryOnlyReference, decisionDraft.ExecutionMode);
        Assert.True(decisionDraft.UsesIssuePacketAsExecutionSource);
        Assert.NotEqual((byte)0, decisionDraft.AuxiliaryReservationMask);
        Assert.Equal((byte)0, decisionDraft.ScalarIssueMask);
        Assert.True(trapIssueLane.IsOccupied);
        Assert.Equal((byte)7, trapIssueLane.PhysicalLaneIndex);
        Assert.Equal((byte)0, trapIssueLane.SlotIndex);
        Assert.IsType<TrapMicroOp>(trapIssueLane.MicroOp);
        Assert.True(issuePacket.Lane7.IsOccupied);
        Assert.IsType<TrapMicroOp>(issuePacket.Lane7.MicroOp);

        core.TestRunExecuteStageFromCurrentDecodeState();

        Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();
        Processor.CPU_Core.ScalarExecuteLaneState trapLane = GetOccupiedTrapExecuteLane(executeStage);
        TrapEntryEvent generatedTrapEvent = Assert.IsType<TrapEntryEvent>(trapLane.GeneratedEvent);

        Assert.True(executeStage.Valid);
        Assert.True(trapLane.IsOccupied);
        Assert.IsType<TrapMicroOp>(trapLane.MicroOp);
        Assert.True(trapLane.ResultReady);
        Assert.True(trapLane.VectorComplete);
        Assert.True(executeStage.Lane7.IsOccupied);
        Assert.IsType<TrapMicroOp>(executeStage.Lane7.MicroOp);
        Assert.Equal(2UL, generatedTrapEvent.CauseCode);
        Assert.Equal(0UL, generatedTrapEvent.FaultAddress);

        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        var control = core.GetPipelineControl();
        Assert.Equal(faultingPc, core.Csr.DirectRead(CsrAddresses.Mepc));
        Assert.Equal(2UL, core.Csr.DirectRead(CsrAddresses.Mcause));
        Assert.Equal(0UL, core.Csr.DirectRead(CsrAddresses.Mtval));
        Assert.Equal(trapHandlerPc, core.ReadCommittedPc(0));
        Assert.Equal(trapHandlerPc, core.ReadActiveLivePc());
        Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(0));
        Assert.Equal(1UL, control.InstructionsRetired);
        Assert.Equal(0UL, control.Lane7ConditionalBranchExecuteCompletionCount);
        Assert.Equal(0UL, control.Lane7ConditionalBranchRedirectCount);
        Assert.NotEqual(unexpectedBranchTarget, core.ReadActiveLivePc());
    }

    private static IssuePacketLane GetOccupiedTrapIssueLane(BundleIssuePacket issuePacket)
    {
        for (byte laneIndex = 0; laneIndex < 8; laneIndex++)
        {
            IssuePacketLane lane = issuePacket.GetPhysicalLane(laneIndex);
            if (lane.IsOccupied && lane.MicroOp is TrapMicroOp)
            {
                return lane;
            }
        }

        throw new Xunit.Sdk.XunitException("Expected a trap issue lane to be occupied.");
    }

    private static Processor.CPU_Core.ScalarExecuteLaneState GetOccupiedTrapExecuteLane(
        Processor.CPU_Core.ExecuteStage executeStage)
    {
        Processor.CPU_Core.ScalarExecuteLaneState[] lanes =
        [
            executeStage.Lane0,
            executeStage.Lane1,
            executeStage.Lane2,
            executeStage.Lane3,
            executeStage.Lane4,
            executeStage.Lane5,
            executeStage.Lane6,
            executeStage.Lane7
        ];

        foreach (Processor.CPU_Core.ScalarExecuteLaneState lane in lanes)
        {
            if (lane.IsOccupied && lane.MicroOp is TrapMicroOp)
            {
                return lane;
            }
        }

        throw new Xunit.Sdk.XunitException("Expected a trap execute lane to be occupied.");
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

