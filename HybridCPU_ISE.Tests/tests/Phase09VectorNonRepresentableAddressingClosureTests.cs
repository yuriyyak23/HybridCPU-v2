using System;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Legality;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09VectorNonRepresentableAddressingClosureTests
{
    [Theory]
    [MemberData(
        nameof(VectorNonRepresentableAddressingTestHelper.RepresentativeContours),
        MemberType = typeof(VectorNonRepresentableAddressingTestHelper))]
    public void ExecuteStage_WhenMaterializedVectorFamilyUsesNonRepresentableIndexedOr2DContour_ThenRejectsCompatSuccessShell(
        VectorNonRepresentableFamily family,
        InstructionsEnum opcode,
        bool is2D,
        string addressingContour)
    {
        var core = new Processor.CPU_Core(0);
        VLIW_Instruction instruction =
            VectorNonRepresentableAddressingTestHelper.CreateInstruction(family, opcode, is2D, virtualThreadId: 2);
        MicroOp microOp =
            VectorNonRepresentableAddressingTestHelper.CreateCarrier(family, opcode, is2D, ownerThreadId: 2);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => core.TestRunExecuteStageWithDecodedInstruction(
                instruction,
                microOp,
                isVectorOp: true,
                isMemoryOp: false));

        Assert.Contains("reference raw execute fallback", ex.Message, StringComparison.Ordinal);
        Assert.NotNull(ex.InnerException);
        Assert.Contains(addressingContour, ex.InnerException!.Message, StringComparison.Ordinal);
        Assert.Contains(
            VectorNonRepresentableAddressingTestHelper.GetExecuteContourLabel(family),
            ex.InnerException.Message,
            StringComparison.Ordinal);
        Assert.Contains("non-representable compat surface", ex.InnerException.Message, StringComparison.Ordinal);

        var executeStage = core.TestReadExecuteStageStatus();
        Assert.False(executeStage.ResultReady);
        Assert.False(executeStage.VectorComplete);
    }

    [Theory]
    [MemberData(
        nameof(VectorNonRepresentableAddressingTestHelper.RepresentativeContours),
        MemberType = typeof(VectorNonRepresentableAddressingTestHelper))]
    public void FetchedFailClosedVectorTrapCarrier_WhenReplayPhaseIsActive_EntersTrapBoundaryForNonRepresentableIndexedOr2DContour(
        VectorNonRepresentableFamily family,
        InstructionsEnum opcode,
        bool is2D,
        string addressingContour)
    {
        const int vtId = 2;
        const ulong trapHandlerPc = 0x1800;
        ulong faultingPc = 0x8C00UL + (((ulong)family) * 0x100UL) + (is2D ? 0x20UL : 0x00UL);

        VLIW_Instruction[] rawSlots =
            VectorNonRepresentableAddressingTestHelper.CreateBundle(
                VectorNonRepresentableAddressingTestHelper.CreateInstruction(
                    family,
                    opcode,
                    is2D,
                    virtualThreadId: (byte)vtId));

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

        Assert.True(decodeStage.Valid);
        Assert.Equal((uint)opcode, decodeStage.OpCode);
        Assert.True(decodeStage.IsVectorOp);
        Assert.Contains(addressingContour, decodedTrap.TrapReason, StringComparison.Ordinal);
        Assert.Contains(
            VectorNonRepresentableAddressingTestHelper.GetFactoryAddressingLabel(family),
            decodedTrap.TrapReason,
            StringComparison.Ordinal);
        Assert.Equal(RuntimeClusterAdmissionExecutionMode.AuxiliaryOnlyReference, decisionDraft.ExecutionMode);
        Assert.True(decisionDraft.UsesIssuePacketAsExecutionSource);
        Assert.Equal((byte)0b0000_0001, decisionDraft.AuxiliaryReservationMask);
        Assert.Equal((byte)0, decisionDraft.ScalarIssueMask);
        Assert.Equal((byte)0b0000_0001, issuePacket.SelectedSlotMask);
        Assert.Equal((byte)0b0000_0001, issuePacket.SelectedNonScalarSlotMask);
        Assert.True(issuePacket.Lane0.IsOccupied);
        Assert.Equal((byte)0, issuePacket.Lane0.SlotIndex);
        Assert.False(issuePacket.Lane0.CountsTowardScalarProjection);
        Assert.IsType<TrapMicroOp>(issuePacket.Lane0.MicroOp);
        Assert.False(issuePacket.Lane4.IsOccupied);
        Assert.False(issuePacket.Lane7.IsOccupied);

        core.TestRunExecuteStageFromCurrentDecodeState();

        Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();
        Processor.CPU_Core.ScalarExecuteLaneState lane0 = executeStage.Lane0;
        TrapEntryEvent generatedTrapEvent = Assert.IsType<TrapEntryEvent>(lane0.GeneratedEvent);

        Assert.True(executeStage.Valid);
        Assert.Equal((byte)0, executeStage.SelectedNonScalarSlotMask);
        Assert.Equal(1, executeStage.MaterializedScalarLaneCount);
        Assert.Equal(1, executeStage.MaterializedPhysicalLaneCount);
        Assert.True(lane0.IsOccupied);
        Assert.IsType<TrapMicroOp>(lane0.MicroOp);
        Assert.True(lane0.ResultReady);
        Assert.True(lane0.VectorComplete);
        Assert.False(executeStage.Lane4.IsOccupied);
        Assert.False(executeStage.Lane7.IsOccupied);
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

        var control = core.GetPipelineControl();
        Assert.Equal(1UL, control.InstructionsRetired);
        Assert.Equal(1UL, control.ScalarLanesRetired);
        Assert.Equal(0UL, control.NonScalarLanesRetired);
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
}

