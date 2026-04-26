using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class DirectFactorySystemEventFollowThroughTests
{
    [Fact]
    public void DirectFactoryFence_OnActiveNonZeroVt_DrainsMemoryWithoutBoundaryPromotion()
    {
        const int vtId = 2;
        const ulong retiredPc = 0x7A00;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(retiredPc, vtId);
        core.WriteCommittedPc(vtId, retiredPc);
        MicroOpScheduler scheduler = PrimeReplayScheduler(ref core, retiredPc, out long serializingEpochCountBefore);
        long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
        ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
        ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
        ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
            scheduler.LastPhaseCertificateInvalidationReason;

        SysEventMicroOp microOp = CreateDirectFactorySystemEventMicroOp(InstructionsEnum.FENCE, vtId);
        Assert.Equal(SystemEventKind.Fence, microOp.EventKind);
        Assert.Equal(SystemEventOrderGuarantee.DrainMemory, microOp.OrderGuarantee);
        Assert.Equal(InstructionClass.System, microOp.InstructionClass);
        Assert.Equal(SerializationClass.MemoryOrdered, microOp.SerializationClass);

        core.TestRetireExplicitLane7SingletonMicroOp(
            microOp,
            pc: retiredPc,
            vtId);

        ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
        ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

        Assert.True(replayPhase.IsActive);
        Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
        Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
        Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
        Assert.True(schedulerPhase.IsActive);
        Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
        Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
        Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
        Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
        Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
        Assert.Equal(AssistInvalidationReason.Fence, scheduler.LastAssistInvalidationReason);
        Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
        Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
        Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
        Assert.Equal(retiredPc, core.ReadActiveLivePc());

        var control = core.GetPipelineControl();
        Assert.Equal(1UL, control.InstructionsRetired);
        Assert.Equal(0UL, control.ScalarLanesRetired);
        Assert.Equal(1UL, control.NonScalarLanesRetired);
    }

    [Fact]
    public void DirectFactoryYield_OnActiveNonZeroVt_PreservesReplayAndSchedulerStateWithoutBoundaryPromotion()
    {
        const int vtId = 1;
        const ulong retiredPc = 0x7A20;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(retiredPc, vtId);
        core.WriteCommittedPc(vtId, retiredPc);
        MicroOpScheduler scheduler = PrimeReplayScheduler(ref core, retiredPc, out long serializingEpochCountBefore);
        long phaseCertificateInvalidationsBefore = scheduler.PhaseCertificateInvalidations;
        ReplayPhaseContext replayPhaseBefore = core.GetReplayPhaseContext();
        ReplayPhaseContext schedulerPhaseBefore = scheduler.TestGetReplayPhaseContext();
        ReplayPhaseInvalidationReason schedulerInvalidationReasonBefore =
            scheduler.LastPhaseCertificateInvalidationReason;
        AssistInvalidationReason assistInvalidationReasonBefore =
            scheduler.LastAssistInvalidationReason;

        SysEventMicroOp microOp = CreateDirectFactorySystemEventMicroOp(InstructionsEnum.YIELD, vtId);
        Assert.Equal(SystemEventKind.Yield, microOp.EventKind);
        Assert.Equal(SystemEventOrderGuarantee.None, microOp.OrderGuarantee);
        Assert.Equal(InstructionClass.SmtVt, microOp.InstructionClass);
        Assert.Equal(SerializationClass.Free, microOp.SerializationClass);

        core.TestRetireExplicitLane7SingletonMicroOp(
            microOp,
            pc: retiredPc,
            vtId);

        ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
        ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

        Assert.True(replayPhase.IsActive);
        Assert.Equal(replayPhaseBefore.EpochId, replayPhase.EpochId);
        Assert.Equal(replayPhaseBefore.CachedPc, replayPhase.CachedPc);
        Assert.Equal(replayPhaseBefore.LastInvalidationReason, replayPhase.LastInvalidationReason);
        Assert.True(schedulerPhase.IsActive);
        Assert.Equal(schedulerPhaseBefore.EpochId, schedulerPhase.EpochId);
        Assert.Equal(schedulerPhaseBefore.CachedPc, schedulerPhase.CachedPc);
        Assert.Equal(schedulerPhaseBefore.LastInvalidationReason, schedulerPhase.LastInvalidationReason);
        Assert.Equal(schedulerInvalidationReasonBefore, scheduler.LastPhaseCertificateInvalidationReason);
        Assert.Equal(phaseCertificateInvalidationsBefore, scheduler.PhaseCertificateInvalidations);
        Assert.Equal(assistInvalidationReasonBefore, scheduler.LastAssistInvalidationReason);
        Assert.Equal(serializingEpochCountBefore, scheduler.SerializingEpochCount);
        Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
        Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
        Assert.Equal(retiredPc, core.ReadActiveLivePc());

        var control = core.GetPipelineControl();
        Assert.Equal(1UL, control.InstructionsRetired);
        Assert.Equal(0UL, control.ScalarLanesRetired);
        Assert.Equal(1UL, control.NonScalarLanesRetired);
    }

    [Fact]
    public void DirectFactoryVtBarrier_OnActiveNonZeroVt_PublishesSerializingBoundary()
    {
        const int vtId = 2;
        const ulong retiredPc = 0x7A40;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(retiredPc, vtId);
        core.WriteCommittedPc(vtId, retiredPc);
        MicroOpScheduler scheduler = PrimeReplayScheduler(ref core, retiredPc, out long serializingEpochCountBefore);

        SysEventMicroOp microOp = CreateDirectFactorySystemEventMicroOp(InstructionsEnum.VT_BARRIER, vtId);
        Assert.Equal(SystemEventKind.VtBarrier, microOp.EventKind);
        Assert.Equal(SystemEventOrderGuarantee.None, microOp.OrderGuarantee);
        Assert.Equal(InstructionClass.SmtVt, microOp.InstructionClass);
        Assert.Equal(SerializationClass.FullSerial, microOp.SerializationClass);

        core.TestRetireExplicitLane7SingletonMicroOp(
            microOp,
            pc: retiredPc,
            vtId);

        ReplayPhaseContext replayPhase = core.GetReplayPhaseContext();
        ReplayPhaseContext schedulerPhase = scheduler.TestGetReplayPhaseContext();

        Assert.False(replayPhase.IsActive);
        Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, replayPhase.LastInvalidationReason);
        Assert.False(schedulerPhase.IsActive);
        Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, schedulerPhase.LastInvalidationReason);
        Assert.Equal(ReplayPhaseInvalidationReason.SerializingEvent, scheduler.LastPhaseCertificateInvalidationReason);
        Assert.Equal(AssistInvalidationReason.SerializingBoundary, scheduler.LastAssistInvalidationReason);
        Assert.Equal(serializingEpochCountBefore + 1, scheduler.SerializingEpochCount);
        Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vtId));
        Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
        Assert.Equal(retiredPc, core.ReadActiveLivePc());

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

    private static SysEventMicroOp CreateDirectFactorySystemEventMicroOp(
        InstructionsEnum opcode,
        int vtId)
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

        SysEventMicroOp microOp =
            Assert.IsType<SysEventMicroOp>(InstructionRegistry.CreateMicroOp((uint)opcode, context));
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();
        return microOp;
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

