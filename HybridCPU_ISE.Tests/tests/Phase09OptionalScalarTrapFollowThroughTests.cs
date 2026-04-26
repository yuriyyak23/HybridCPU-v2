using System;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09OptionalScalarTrapFollowThroughTests
{
    [Theory]
    [InlineData(45u, 0x8E00UL)]
    [InlineData(52u, 0x8E20UL)]
    public void FetchedOptionalScalarDecodeFaultTrap_WhenReplayPhaseIsActive_EntersTrapBoundary(
        uint rawOpcode,
        ulong faultingPc)
    {
        const int vtId = 2;
        const ulong trapHandlerPc = 0x1900;

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateRawScalarInstruction(rawOpcode, vtId));

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(faultingPc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, faultingPc);
        core.Csr.Write(CsrAddresses.Mtvec, trapHandlerPc, PrivilegeLevel.Machine);

        MicroOpScheduler scheduler = PrimeReplayScheduler(
            ref core,
            faultingPc,
            out long serializingEpochCountBefore);

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc: faultingPc);

        Processor.CPU_Core.DecodeStage decodeStage = core.GetDecodeStage();
        RuntimeClusterAdmissionDecisionDraft decisionDraft = core.GetDecodeStageAdmissionDecisionDraft();
        BundleIssuePacket issuePacket = core.GetDecodeStageIssuePacket();
        TrapMicroOp decodedTrap = Assert.IsType<TrapMicroOp>(decodeStage.MicroOp);
        IssuePacketLane occupiedIssueLane = GetOccupiedTrapIssueLane(issuePacket);

        Assert.True(decodeStage.Valid);
        Assert.Equal(rawOpcode, decodeStage.OpCode);
        Assert.False(decodeStage.IsVectorOp);
        Assert.Contains($"{rawOpcode}", decodedTrap.TrapReason, StringComparison.Ordinal);
        Assert.Contains("uses unsupported optional", decodedTrap.TrapReason, StringComparison.Ordinal);
        Assert.Equal(RuntimeClusterAdmissionExecutionMode.AuxiliaryOnlyReference, decisionDraft.ExecutionMode);
        Assert.True(decisionDraft.UsesIssuePacketAsExecutionSource);
        Assert.NotEqual((byte)0, decisionDraft.AuxiliaryReservationMask);
        Assert.Equal((byte)0, decisionDraft.ScalarIssueMask);
        Assert.True(occupiedIssueLane.IsOccupied);
        Assert.IsType<TrapMicroOp>(occupiedIssueLane.MicroOp);

        core.TestRunExecuteStageFromCurrentDecodeState();

        Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();
        Processor.CPU_Core.ScalarExecuteLaneState trapLane = GetOccupiedTrapExecuteLane(executeStage);
        TrapEntryEvent generatedTrapEvent = Assert.IsType<TrapEntryEvent>(trapLane.GeneratedEvent);

        Assert.True(executeStage.Valid);
        Assert.True(trapLane.IsOccupied);
        Assert.IsType<TrapMicroOp>(trapLane.MicroOp);
        Assert.True(trapLane.ResultReady);
        Assert.True(trapLane.VectorComplete);
        Assert.Equal(2UL, generatedTrapEvent.CauseCode);
        Assert.Equal(0UL, generatedTrapEvent.FaultAddress);

        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        AssertTrapBoundaryReplayPublication(core, scheduler, serializingEpochCountBefore);
        Assert.Equal(faultingPc, core.Csr.DirectRead(CsrAddresses.Mepc));
        Assert.Equal(2UL, core.Csr.DirectRead(CsrAddresses.Mcause));
        Assert.Equal(0UL, core.Csr.DirectRead(CsrAddresses.Mtval));
        Assert.Equal(trapHandlerPc, core.ReadCommittedPc(vtId));
        Assert.Equal(trapHandlerPc, core.ReadActiveLivePc());
        Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
        Assert.Equal(1UL, core.GetPipelineControl().InstructionsRetired);
    }

    private static MicroOpScheduler PrimeReplayScheduler(
        ref Processor.CPU_Core core,
        ulong retiredPc,
        out long serializingEpochCountBefore)
    {
        core.TestInitializeFSPScheduler();
        core.TestPrimeReplayPhase(
            pc: retiredPc,
            totalIterations: 8,
            MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));

        MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
        var capacityState = new SlotClassCapacityState();
        capacityState.InitializeFromLaneMap();
        scheduler.TestSetClassCapacityTemplate(new ClassCapacityTemplate(capacityState));
        scheduler.TestSetClassTemplateValid(true);
        scheduler.TestSetClassTemplateDomainId(0);
        serializingEpochCountBefore = scheduler.SerializingEpochCount;

        Assert.True(core.GetReplayPhaseContext().IsActive);
        Assert.True(scheduler.TestGetReplayPhaseContext().IsActive);
        return scheduler;
    }

    private static void AssertTrapBoundaryReplayPublication(
        Processor.CPU_Core core,
        MicroOpScheduler scheduler,
        long serializingEpochCountBefore)
    {
        ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
        ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

        Assert.Equal(serializingEpochCountBefore + 1, scheduler.SerializingEpochCount);
        Assert.Contains(
            scheduler.LastPhaseCertificateInvalidationReason,
            new[]
            {
                ReplayPhaseInvalidationReason.SerializingEvent,
                ReplayPhaseInvalidationReason.InactivePhase
            });
        Assert.Equal(AssistInvalidationReason.Trap, scheduler.LastAssistInvalidationReason);
        Assert.False(replayPhase.IsActive);
        Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
        Assert.False(schedulerPhase.IsActive);
        Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
    }

    private static IssuePacketLane GetOccupiedTrapIssueLane(BundleIssuePacket issuePacket)
    {
        if (issuePacket.Lane0.IsOccupied)
        {
            return issuePacket.Lane0;
        }

        if (issuePacket.Lane4.IsOccupied)
        {
            return issuePacket.Lane4;
        }

        if (issuePacket.Lane7.IsOccupied)
        {
            return issuePacket.Lane7;
        }

        throw new Xunit.Sdk.XunitException("Expected a trap issue lane to be occupied.");
    }

    private static Processor.CPU_Core.ScalarExecuteLaneState GetOccupiedTrapExecuteLane(
        in Processor.CPU_Core.ExecuteStage executeStage)
    {
        if (executeStage.Lane0.IsOccupied)
        {
            return executeStage.Lane0;
        }

        if (executeStage.Lane4.IsOccupied)
        {
            return executeStage.Lane4;
        }

        if (executeStage.Lane7.IsOccupied)
        {
            return executeStage.Lane7;
        }

        throw new Xunit.Sdk.XunitException("Expected a trap execute lane to be occupied.");
    }

    private static VLIW_Instruction[] CreateBundle(
        VLIW_Instruction slot0)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = slot0;
        return rawSlots;
    }

    private static VLIW_Instruction CreateRawScalarInstruction(
        uint rawOpcode,
        int vtId)
    {
        return new VLIW_Instruction
        {
            OpCode = rawOpcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(4, 5, 6),
            Src2Pointer = 0x290,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = (byte)vtId
        };
    }
}

