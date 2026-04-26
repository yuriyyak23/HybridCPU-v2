using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Legality;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class TrapCarrierFollowThroughTests
{
    [Theory]
    [InlineData(InstructionsEnum.VGATHER)]
    [InlineData(InstructionsEnum.VSCATTER)]
    public void FetchedCanonicalTrapCarrier_WhenReplayPhaseIsActive_EntersIllegalInstructionTrapAndPublishesTrapBoundary(
        InstructionsEnum opcode)
    {
        const int vtId = 2;
        const ulong faultingPc = 0x7C00;
        const ulong trapHandlerPc = 0x1800;

        VLIW_Instruction[] rawSlots =
            CreateBundle(CreateUnsupportedCanonicalTrapInstruction(vtId, opcode));

        var publicationCore = new Processor.CPU_Core(0);
        publicationCore.PrepareExecutionStart(faultingPc, activeVtId: vtId);
        publicationCore.TestDecodeFetchedBundle(rawSlots, pc: faultingPc);

        var publishedCanonicalBundle = publicationCore.GetCurrentDecodedInstructionBundle();
        BundleLegalityDescriptor publishedLegalityDescriptor = publicationCore.GetCurrentBundleLegalityDescriptor();
        DecodedBundleTransportFacts publishedTransportFacts = publicationCore.TestReadCurrentDecodedBundleTransportFacts();
        DecodedBundleSlotDescriptor publishedSlot = publishedTransportFacts.Slots[0];

        Assert.False(publishedCanonicalBundle.HasDecodeFault);
        Assert.False(publishedCanonicalBundle.IsEmpty);
        Assert.False(publishedLegalityDescriptor.HasDecodeFault);
        Assert.False(publishedLegalityDescriptor.IsEmpty);
        Assert.Equal((byte)0b0000_0001, publishedTransportFacts.ValidNonEmptyMask);
        Assert.Equal((byte)0, publishedTransportFacts.AdmissionPrep.ScalarCandidateMask);
        Assert.Equal((byte)0b0000_0001, publishedTransportFacts.AdmissionPrep.AuxiliaryOpMask);
        Assert.IsType<TrapMicroOp>(publishedSlot.MicroOp);
        Assert.True(publishedSlot.IsMemoryOp);
        Assert.False(publishedSlot.IsControlFlow);
        Assert.Equal(SlotClass.LsuClass, publishedSlot.Placement.RequiredSlotClass);

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

        Assert.True(decodeStage.Valid);
        Assert.Equal((uint)opcode, decodeStage.OpCode);
        Assert.True(decodeStage.IsVectorOp);
        Assert.IsType<TrapMicroOp>(decodeStage.MicroOp);
        Assert.Equal(RuntimeClusterAdmissionExecutionMode.AuxiliaryOnlyReference, decisionDraft.ExecutionMode);
        Assert.True(decisionDraft.UsesIssuePacketAsExecutionSource);
        Assert.Equal((byte)0b0000_0001, decisionDraft.AuxiliaryReservationMask);
        Assert.Equal((byte)0, decisionDraft.ScalarIssueMask);
        Assert.IsType<TrapMicroOp>(issuePacket.Lane4.MicroOp);
        Assert.False(issuePacket.Lane7.IsOccupied);

        core.TestRunExecuteStageFromCurrentDecodeState();

        Processor.CPU_Core.ExecuteStage executeStage = core.GetExecuteStage();
        Processor.CPU_Core.ScalarExecuteLaneState lane4 = executeStage.Lane4;
        TrapEntryEvent generatedTrapEvent = Assert.IsType<TrapEntryEvent>(lane4.GeneratedEvent);

        Assert.True(executeStage.Valid);
        Assert.True(lane4.IsOccupied);
        Assert.IsType<TrapMicroOp>(lane4.MicroOp);
        Assert.True(lane4.ResultReady);
        Assert.True(lane4.VectorComplete);
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
        Assert.Equal(0UL, control.ScalarLanesRetired);
        Assert.Equal(1UL, control.NonScalarLanesRetired);
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

    private static VLIW_Instruction[] CreateBundle(
        VLIW_Instruction slot0)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = slot0;
        return rawSlots;
    }

    private static VLIW_Instruction CreateUnsupportedCanonicalTrapInstruction(
        int vtId,
        InstructionsEnum opcode)
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeVector1D(
            (uint)opcode,
            DataTypeEnum.INT32,
            destSrc1Ptr: 0x1000,
            src2Ptr: 0x2000,
            streamLength: 4);
        instruction.VirtualThreadId = (byte)vtId;
        return instruction;
    }
}

