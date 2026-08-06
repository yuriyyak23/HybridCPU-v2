using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Arch.Generated;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Contracts;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class FspPipelineStageBoundaryRegressionTests
{
    [Fact]
    public void Sched2_DoesNotIssueReplacementFromTheSameVirtualThreadPort()
    {
        var scheduler = new MicroOpScheduler
        {
            PipelinedFspEnabled = true
        };
        MicroOp nominatedAtSched1 = CreateAlu(1, 10);
        MicroOp replacement = CreateAlu(1, 20);

        scheduler.NominateSmtCandidate(1, nominatedAtSched1);
        MicroOp[] primingResult = scheduler.PackBundleIntraCoreSmt(
            new MicroOp[8], ownerVirtualThreadId: 0, localCoreId: 0);

        scheduler.NominateSmtCandidate(1, replacement);
        MicroOp[] mismatchResult = scheduler.PackBundleIntraCoreSmt(
            new MicroOp[8], ownerVirtualThreadId: 0, localCoreId: 0);

        Assert.DoesNotContain(primingResult, op => ReferenceEquals(op, nominatedAtSched1));
        Assert.DoesNotContain(mismatchResult, op => ReferenceEquals(op, replacement));
        Assert.DoesNotContain(mismatchResult, op => ReferenceEquals(op, nominatedAtSched1));
        Assert.Equal(0, scheduler.SmtInjectionsCount);

        MicroOp[] replacementOwnStageResult = scheduler.PackBundleIntraCoreSmt(
            new MicroOp[8], ownerVirtualThreadId: 0, localCoreId: 0);

        Assert.Contains(replacementOwnStageResult, op => ReferenceEquals(op, replacement));
        Assert.DoesNotContain(replacementOwnStageResult, op => ReferenceEquals(op, nominatedAtSched1));
        Assert.Equal(1, scheduler.SmtInjectionsCount);
    }

    [Fact]
    public void TypedSched2_MaterializesIdentityFromTheMatchingSched1Candidate()
    {
        var scheduler = new MicroOpScheduler
        {
            PipelinedFspEnabled = true,
            TypedSlotEnabled = true
        };
        MicroOp candidate = CreateAlu(1, 30);
        candidate.PostStageBIdentityTemplate = CreateIdentityTemplate(
            virtualThreadId: 1,
            sourceSlotIndex: 2,
            workingSlotIndex: 2);

        scheduler.NominateSmtCandidate(1, candidate);
        scheduler.PackBundleIntraCoreSmt(
            new MicroOp[8], ownerVirtualThreadId: 0, localCoreId: 0);
        Assert.Null(candidate.PostStageBIssuedAttempt);

        MicroOp[] issued = scheduler.PackBundleIntraCoreSmt(
            new MicroOp[8], ownerVirtualThreadId: 0, localCoreId: 0);

        int physicalLane = Array.FindIndex(issued, op => ReferenceEquals(op, candidate));
        Assert.InRange(physicalLane, 0, 7);
        PostStageBIssuedAttempt attempt = Assert.IsType<PostStageBIssuedAttempt>(
            candidate.PostStageBIssuedAttempt);
        Assert.Equal(2, attempt.ScheduledOperation.Admission.SourceProvenance.SourceSlotIndex);
        Assert.Equal(2, attempt.ScheduledOperation.OperationId.WorkingSlotIndex);
        Assert.Equal(physicalLane, attempt.ScheduledOperation.PhysicalLane);
        Assert.Same(attempt.ScheduledOperation, attempt.ExecutionRecord.ScheduledOperation);
        Assert.Null(candidate.PostStageBIdentityTemplate);
    }

    [Fact]
    public void Nomination_DoesNotReuseOrEraseAnAlreadyIssuedAttempt()
    {
        var scheduler = new MicroOpScheduler
        {
            TypedSlotEnabled = true
        };
        MicroOp candidate = CreateAlu(1, 40);
        PostStageBIdentityTemplate template = CreateIdentityTemplate(
            virtualThreadId: 1,
            sourceSlotIndex: 4,
            workingSlotIndex: 4);
        PostStageBIssuedAttempt issuedAttempt =
            PostStageBIssuedAttempt.CreateAfterSuccessfulStageB(template, LaneId.Create(2));
        candidate.PostStageBIdentityTemplate = template;
        candidate.PostStageBIssuedAttempt = issuedAttempt;

        scheduler.NominateSmtCandidate(1, candidate);
        MicroOp[] result = scheduler.PackBundleIntraCoreSmt(
            new MicroOp[8], ownerVirtualThreadId: 0, localCoreId: 0);

        Assert.DoesNotContain(result, op => ReferenceEquals(op, candidate));
        Assert.Same(issuedAttempt, candidate.PostStageBIssuedAttempt);
        Assert.Same(template, candidate.PostStageBIdentityTemplate);
        Assert.Equal(0, scheduler.SmtInjectionsCount);
    }

    [Fact]
    public void Sched2_RejectsMutationOfTheSameCandidateReference()
    {
        var scheduler = new MicroOpScheduler
        {
            PipelinedFspEnabled = true,
            TypedSlotEnabled = true
        };
        MicroOp candidate = CreateAlu(1, 50);

        scheduler.NominateSmtCandidate(1, candidate);
        scheduler.PackBundleIntraCoreSmt(
            new MicroOp[8], ownerVirtualThreadId: 0, localCoreId: 0);

        candidate.Placement = candidate.Placement with
        {
            PinningKind = SlotPinningKind.HardPinned,
            PinnedLaneId = 6
        };
        MicroOp[] result = scheduler.PackBundleIntraCoreSmt(
            new MicroOp[8], ownerVirtualThreadId: 0, localCoreId: 0);

        Assert.DoesNotContain(result, op => ReferenceEquals(op, candidate));
        Assert.Equal(0, scheduler.SmtInjectionsCount);
        Assert.Null(candidate.PostStageBIssuedAttempt);
    }

    [Fact]
    public void Sched2_RejectsOwnerOrBundleGenerationChange()
    {
        var ownerChangedScheduler = new MicroOpScheduler
        {
            PipelinedFspEnabled = true,
            TypedSlotEnabled = true
        };
        MicroOp ownerCandidate = CreateAlu(1, 60);
        ownerChangedScheduler.NominateSmtCandidate(1, ownerCandidate);
        ownerChangedScheduler.PackBundleIntraCoreSmt(
            new MicroOp[8], ownerVirtualThreadId: 0, localCoreId: 0);

        MicroOp[] ownerChanged = ownerChangedScheduler.PackBundleIntraCoreSmt(
            new MicroOp[8], ownerVirtualThreadId: 2, localCoreId: 0);
        Assert.DoesNotContain(ownerChanged, op => ReferenceEquals(op, ownerCandidate));
        Assert.Equal(0, ownerChangedScheduler.SmtInjectionsCount);

        var bundleChangedScheduler = new MicroOpScheduler
        {
            PipelinedFspEnabled = true,
            TypedSlotEnabled = true
        };
        MicroOp bundleCandidate = CreateAlu(1, 70);
        bundleChangedScheduler.NominateSmtCandidate(1, bundleCandidate);
        bundleChangedScheduler.PackBundleIntraCoreSmt(
            new MicroOp[8], ownerVirtualThreadId: 0, localCoreId: 0);

        var changedBundle = new MicroOp[8];
        changedBundle[0] = CreateAlu(0, 80);
        MicroOp[] bundleChanged = bundleChangedScheduler.PackBundleIntraCoreSmt(
            changedBundle, ownerVirtualThreadId: 0, localCoreId: 0);
        Assert.DoesNotContain(bundleChanged, op => ReferenceEquals(op, bundleCandidate));
        Assert.Equal(0, bundleChangedScheduler.SmtInjectionsCount);
    }

    [Fact]
    public void Sched2_RejectsReplayEpochChange()
    {
        var scheduler = new MicroOpScheduler
        {
            PipelinedFspEnabled = true,
            TypedSlotEnabled = true
        };
        MicroOp candidate = CreateAlu(1, 90);
        scheduler.SetReplayPhaseContext(CreateReplayPhase(epochId: 10));
        scheduler.NominateSmtCandidate(1, candidate);
        scheduler.PackBundleIntraCoreSmt(
            new MicroOp[8], ownerVirtualThreadId: 0, localCoreId: 0);

        scheduler.SetReplayPhaseContext(CreateReplayPhase(epochId: 11));
        MicroOp[] result = scheduler.PackBundleIntraCoreSmt(
            new MicroOp[8], ownerVirtualThreadId: 0, localCoreId: 0);

        Assert.DoesNotContain(result, op => ReferenceEquals(op, candidate));
        Assert.Equal(0, scheduler.SmtInjectionsCount);
    }

    private static MicroOp CreateAlu(int virtualThreadId, ushort registerBase)
    {
        MicroOp candidate = MicroOpTestHelper.CreateScalarALU(
            virtualThreadId,
            destReg: registerBase,
            src1Reg: (ushort)(registerBase + 1),
            src2Reg: (ushort)(registerBase + 2));
        candidate.Placement = candidate.Placement with
        {
            RequiredSlotClass = SlotClass.AluClass,
            PinningKind = SlotPinningKind.ClassFlexible
        };
        return candidate;
    }

    private static PostStageBIdentityTemplate CreateIdentityTemplate(
        int virtualThreadId,
        int sourceSlotIndex,
        int workingSlotIndex)
    {
        Assert.True(GeneratedIsaCatalog.TryGetDescriptor(
            (uint)IsaOpcodeValues.ADD,
            out GeneratedIsaDescriptor descriptor));
        GeneratedStaticBinding binding = GeneratedStaticBinding.FromDescriptor(in descriptor);
        ExecutionContract contract = ExecutionContract.Create(
            binding,
            new RuntimeExecutionProviderBinding(binding.RuntimeExecutionProviderId, "fsp-pipeline-regression"),
            InstructionClass.ScalarAlu,
            SerializationClass.Free,
            ExecutionPlacement.Create(SlotClass.AluClass, SlotPinningKind.ClassFlexible),
            "RegisterWrite",
            MemoryCapability.None,
            readRegisters: [1, 2],
            writeRegisters: [30],
            isRetireVisible: true,
            isAssist: false);
        AdmissionRecord admission = AdmissionRecord.Create(
            new SourceOperationProvenance(
                SemanticInstructionKey.Create([1, 2, 3], "fsp-pipeline-regression", CanonicalDecodeContext.Unbound),
                virtualThreadId,
                sourceBundleSerial: 100,
                sourceSlotId: SlotId.Create(sourceSlotIndex),
                fetchEpoch: 7),
            contract,
            virtualThreadId,
            ownerContextId: 20,
            domainTag: 31);
        return new PostStageBIdentityTemplate(
            admission,
            workingBundleSequence: 200,
            SlotId.Create(workingSlotIndex),
            new OperationAttemptIssuer());
    }

    private static ReplayPhaseContext CreateReplayPhase(ulong epochId) => new(
        isActive: false,
        epochId,
        cachedPc: 0x1000,
        epochLength: 8,
        completedReplays: 0,
        validSlotCount: 8,
        stableDonorMask: 0,
        ReplayPhaseInvalidationReason.None);
}
