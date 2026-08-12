using System.Reflection;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class ResearchVirtualizationCanonicalCompositionTests
{
    [Fact]
    public void CanonicalBoundary_DefaultsOffWhileE1AndFaultOnlyRetireRemainUnchanged()
    {
        Attempt attempt = CreateCanonicalAttempt();

        Assert.True(attempt.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            attempt.Packet,
            attempt.Lane));
        Assert.NotNull(attempt.Carrier.VirtualizationAdmission);
        Assert.Null(attempt.Scheduler.LastResearchVirtualizationCanonicalCompositionResult);

        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        Assert.True(attempt.Carrier.Execute(ref core));
        VmxRetireEffect effect = attempt.Carrier.CreateRetireEffect();
        Assert.True(effect.IsFaulted);
        Assert.Equal(VmExitReason.SecurityPolicyViolation, effect.FailureReason);
        VmxRetireOutcome outcome = core.ApplyRetiredVmxEffectForTesting(effect, 0);
        Assert.True(outcome.Faulted);
        Assert.False(outcome.HasRegisterWriteback);
    }

    [Fact]
    public void CanonicalBoundary_ComposesLiveE1TypedContextAndExactOnceReceipt()
    {
        Attempt attempt = CreateCanonicalAttempt();
        var owner = new ResearchVirtualizationRuntimeOwner();
        ResearchVirtualizationOperationContext context = CreateContext();
        attempt.Scheduler.EnableResearchVirtualizationCanonicalComposition(
            new ResearchVirtualizationCanonicalIssueComposition(owner, context));

        Assert.True(attempt.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            attempt.Packet,
            attempt.Lane));
        ResearchVirtualizationCanonicalCompositionResult result =
            Assert.IsType<ResearchVirtualizationCanonicalCompositionResult>(
                attempt.Scheduler.LastResearchVirtualizationCanonicalCompositionResult);
        Assert.True(result.Succeeded);
        ResearchVirtualizationRuntimeOwner.ExecutionReceipt receipt =
            Assert.IsType<ResearchVirtualizationRuntimeOwner.ExecutionReceipt>(result.Receipt);
        Assert.Equal(attempt.Carrier.VirtualizationAdmission!.AttemptId, receipt.Identity.CarrierAttemptId);
        Assert.Equal(attempt.Phase.EpochId, receipt.Identity.ReplayEpoch);
        Assert.Equal(0, receipt.Identity.VirtualThreadId);
        Assert.Equal(42, receipt.Identity.OwnerContextId);
        Assert.Equal(7UL, receipt.Identity.DomainTag);
        Assert.Equal(9UL, receipt.Identity.AddressSpaceTag);
        Assert.Equal(13UL, receipt.Identity.CapabilityGeneration);
        Assert.Equal(17UL, receipt.Identity.EvidenceGeneration);
        Assert.Equal(19UL, receipt.Identity.RestoreGeneration);
        Assert.Equal(0, receipt.PayloadLength);
        Assert.Equal(0, receipt.StateMutationCount);
        Assert.False(receipt.CompletionPublicationAuthorized);
        Assert.False(receipt.RetirePublicationAuthorized);
    }

    [Fact]
    public void CanonicalBoundary_DeniesDisplacedSourceSlotAndWrongWorkingSlot()
    {
        Attempt displaced = CreateCanonicalAttempt(sourceSlot: 6);
        displaced.Scheduler.EnableResearchVirtualizationCanonicalComposition(
            new ResearchVirtualizationCanonicalIssueComposition(
                new ResearchVirtualizationRuntimeOwner(),
                CreateContext()));

        Assert.False(displaced.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            displaced.Packet,
            displaced.Lane));
        Assert.Null(displaced.Carrier.VirtualizationAdmission);
        Assert.Null(displaced.Scheduler.LastResearchVirtualizationCanonicalCompositionResult);

        Attempt live = MaterializeE1();
        var composition = new ResearchVirtualizationCanonicalIssueComposition(
            new ResearchVirtualizationRuntimeOwner(),
            CreateContext());
        ResearchVirtualizationCanonicalCompositionResult result = composition.Compose(
            CanonicalVerifier(live),
            live.Phase,
            BundleFor(live.Carrier),
            live.Carrier,
            sourceSlotId: 7,
            workingSlotId: 6,
            live.Carrier.VirtualizationAdmission!);

        Assert.Equal(
            ResearchVirtualizationCanonicalCompositionDecision.DeniedPrototypeAdmission,
            result.Decision);
        Assert.Equal(ResearchVirtualizationProbeAdmissionDecision.DeniedE1Carrier, result.AdmissionDecision);
    }

    [Fact]
    public void CanonicalComposition_DeniesReplayMismatchAndSquashedE1()
    {
        Attempt replay = MaterializeE1();
        ReplayPhaseContext changedReplay = CreateReplayPhase(replay.Phase.EpochId + 1);
        ResearchVirtualizationCanonicalCompositionResult replayResult =
            new ResearchVirtualizationCanonicalIssueComposition(
                new ResearchVirtualizationRuntimeOwner(),
                CreateContext())
            .Compose(
                CanonicalVerifier(replay),
                changedReplay,
                BundleFor(replay.Carrier),
                replay.Carrier,
                7,
                7,
                replay.Carrier.VirtualizationAdmission!);
        Assert.Equal(
            ResearchVirtualizationCanonicalCompositionDecision.DeniedPrototypeAdmission,
            replayResult.Decision);
        Assert.Equal(ResearchVirtualizationProbeAdmissionDecision.DeniedE1Carrier, replayResult.AdmissionDecision);

        Attempt squash = MaterializeE1(epoch: 23);
        CanonicalVerifier(squash).InvalidateVirtualizationAdmissions(ReplayPhaseInvalidationReason.Manual);
        ResearchVirtualizationCanonicalCompositionResult squashResult =
            new ResearchVirtualizationCanonicalIssueComposition(
                new ResearchVirtualizationRuntimeOwner(),
                CreateContext())
            .Compose(
                CanonicalVerifier(squash),
                squash.Phase,
                BundleFor(squash.Carrier),
                squash.Carrier,
                7,
                7,
                squash.Carrier.VirtualizationAdmission!);
        Assert.Equal(
            ResearchVirtualizationCanonicalCompositionDecision.DeniedPrototypeAdmission,
            squashResult.Decision);
        Assert.Equal(ResearchVirtualizationProbeAdmissionDecision.DeniedE1Carrier, squashResult.AdmissionDecision);
    }

    [Fact]
    public void CanonicalBoundary_DeniesForeignOwnerStalePolicyAndPostAdmissionRevocation()
    {
        Attempt foreign = CreateCanonicalAttempt();
        foreign.Scheduler.EnableResearchVirtualizationCanonicalComposition(
            new ResearchVirtualizationCanonicalIssueComposition(
                new ResearchVirtualizationRuntimeOwner(),
                CreateContext(),
                executionOwner: new ResearchVirtualizationRuntimeOwner()));
        Assert.True(foreign.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            foreign.Packet,
            foreign.Lane));
        Assert.Equal(
            ResearchVirtualizationProbeExecutionDecision.DeniedForeignOwner,
            foreign.Scheduler.LastResearchVirtualizationCanonicalCompositionResult!.Value.ExecutionDecision);

        Attempt stalePolicy = CreateCanonicalAttempt(epoch: 31);
        var owner = new ResearchVirtualizationRuntimeOwner();
        stalePolicy.Scheduler.EnableResearchVirtualizationCanonicalComposition(
            new ResearchVirtualizationCanonicalIssueComposition(owner, CreateContext()));
        owner.InvalidatePolicy();
        Assert.True(stalePolicy.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            stalePolicy.Packet,
            stalePolicy.Lane));
        Assert.Equal(
            ResearchVirtualizationProbeExecutionDecision.DeniedStalePolicyGeneration,
            stalePolicy.Scheduler.LastResearchVirtualizationCanonicalCompositionResult!.Value.ExecutionDecision);

        Attempt revoked = CreateCanonicalAttempt(epoch: 37);
        revoked.Scheduler.EnableResearchVirtualizationCanonicalComposition(
            new ResearchVirtualizationCanonicalIssueComposition(
                new ResearchVirtualizationRuntimeOwner(),
                CreateContext(),
                afterAdmission: verifier => verifier.InvalidateVirtualizationAdmissions(
                    ReplayPhaseInvalidationReason.Manual)));
        Assert.True(revoked.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            revoked.Packet,
            revoked.Lane));
        Assert.Equal(
            ResearchVirtualizationProbeExecutionDecision.DeniedStaleAdmission,
            revoked.Scheduler.LastResearchVirtualizationCanonicalCompositionResult!.Value.ExecutionDecision);
    }

    [Fact]
    public void CanonicalBoundary_DeniesForeignOrStaleContextAndIdentityMismatch()
    {
        Attempt foreignLease = CreateCanonicalAttempt();
        ResearchVirtualizationOperationContext context = CreateContext();
        ResearchVirtualizationOperationContext foreignContext = CreateContext(addressSpaceTag: 10);
        foreignLease.Scheduler.EnableResearchVirtualizationCanonicalComposition(
            new ResearchVirtualizationCanonicalIssueComposition(
                new ResearchVirtualizationRuntimeOwner(),
                foreignContext,
                contextLease: context.CaptureMaterializationLease()));
        Assert.True(foreignLease.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            foreignLease.Packet,
            foreignLease.Lane));
        Assert.Equal(
            ResearchVirtualizationCanonicalCompositionDecision.DeniedStaleRuntimeContextLease,
            foreignLease.Scheduler.LastResearchVirtualizationCanonicalCompositionResult!.Value.Decision);

        Attempt mismatch = CreateCanonicalAttempt(epoch: 43);
        mismatch.Scheduler.EnableResearchVirtualizationCanonicalComposition(
            new ResearchVirtualizationCanonicalIssueComposition(
                new ResearchVirtualizationRuntimeOwner(),
                CreateContext(virtualThreadId: 1, ownerContextId: 41, domainTag: 8)));
        Assert.True(mismatch.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            mismatch.Packet,
            mismatch.Lane));
        Assert.Equal(
            ResearchVirtualizationProbeAdmissionDecision.DeniedCarrierIdentityMismatch,
            mismatch.Scheduler.LastResearchVirtualizationCanonicalCompositionResult!.Value.AdmissionDecision);

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateContext(addressSpaceTag: 0));
    }

    [Theory]
    [InlineData("capability")]
    [InlineData("evidence")]
    [InlineData("restore")]
    public void CanonicalBoundary_DeniesStaleAuthorityGenerationLease(string generation)
    {
        Attempt attempt = CreateCanonicalAttempt();
        ResearchVirtualizationOperationContext context = CreateContext();
        attempt.Scheduler.EnableResearchVirtualizationCanonicalComposition(
            new ResearchVirtualizationCanonicalIssueComposition(
                new ResearchVirtualizationRuntimeOwner(),
                context));

        switch (generation)
        {
            case "capability": context.AdvanceCapabilityGeneration(); break;
            case "evidence": context.AdvanceEvidenceGeneration(); break;
            case "restore": context.AdvanceRestoreGeneration(); break;
            default: throw new ArgumentOutOfRangeException(nameof(generation));
        }

        Assert.True(attempt.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            attempt.Packet,
            attempt.Lane));
        Assert.Equal(
            ResearchVirtualizationCanonicalCompositionDecision.DeniedStaleRuntimeContextLease,
            attempt.Scheduler.LastResearchVirtualizationCanonicalCompositionResult!.Value.Decision);
    }

    [Fact]
    public async Task CanonicalComposition_ConsumesOneMaterializationExactlyOnceUnderConcurrency()
    {
        Attempt attempt = MaterializeE1(epoch: 53);
        var composition = new ResearchVirtualizationCanonicalIssueComposition(
            new ResearchVirtualizationRuntimeOwner(),
            CreateContext());
        SafetyVerifier verifier = CanonicalVerifier(attempt);
        SmtBundleMetadata4Way bundle = BundleFor(attempt.Carrier);

        Task<ResearchVirtualizationCanonicalCompositionResult>[] tasks = Enumerable
            .Range(0, 16)
            .Select(_ => Task.Run(() => composition.Compose(
                verifier,
                attempt.Phase,
                bundle,
                attempt.Carrier,
                7,
                7,
                attempt.Carrier.VirtualizationAdmission!)))
            .ToArray();
        ResearchVirtualizationCanonicalCompositionResult[] results = await Task.WhenAll(tasks);

        Assert.Single(results, result => result.Decision ==
            ResearchVirtualizationCanonicalCompositionDecision.MaterializedReceipt);
        Assert.Equal(15, results.Count(result => result.Decision ==
            ResearchVirtualizationCanonicalCompositionDecision.DeniedDuplicateMaterialization));
    }

    [Fact]
    public void CompositionTypes_AreTestingOnlyNonPublicAndPublicationIndependent()
    {
        Assert.Empty(typeof(ResearchVirtualizationCanonicalIssueComposition)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public));

        string composition = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Pipeline/Scheduling/Smt/MicroOpScheduler.ResearchVirtualizationCanonicalComposition.cs");
        Assert.StartsWith("#if TESTING", composition);
        Assert.DoesNotContain("CompletionRecord", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("VmxRetireEffect", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("BackendExecutionAuthorized", composition, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionDispatcherV4", composition, StringComparison.Ordinal);

        string materialization = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.Materialization.cs");
        Assert.DoesNotContain("ResearchVirtualization", materialization, StringComparison.Ordinal);
    }

    private static Attempt MaterializeE1(ulong epoch = 11)
    {
        Attempt attempt = CreateCanonicalAttempt(epoch: epoch);
        Assert.True(attempt.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            attempt.Packet,
            attempt.Lane));
        Assert.NotNull(attempt.Carrier.VirtualizationAdmission);
        return attempt;
    }

    private static Attempt CreateCanonicalAttempt(ulong epoch = 11, byte sourceSlot = 7)
    {
        var scheduler = new MicroOpScheduler();
        ReplayPhaseContext phase = CreateReplayPhase(epoch);
        scheduler.SetReplayPhaseContext(phase);
        VmxMicroOp carrier = CreateCarrier();
        IssuePacketLane lane = new(
            physicalLaneIndex: 7,
            isOccupied: true,
            slotIndex: sourceSlot,
            virtualThreadId: 0,
            ownerThreadId: 0,
            opCode: IsaOpcodeValues.VMCALL,
            microOp: carrier,
            requiredSlotClass: SlotClass.SystemSingleton,
            pinningKind: SlotPinningKind.HardPinned,
            countsTowardScalarProjection: false);
        return new(scheduler, phase, CreateIssuePacket(lane), lane, carrier);
    }

    private static ResearchVirtualizationOperationContext CreateContext(
        int virtualThreadId = 0,
        int ownerContextId = 42,
        ulong domainTag = 7,
        ulong addressSpaceTag = 9) =>
        new(virtualThreadId, ownerContextId, domainTag, addressSpaceTag, 13, 17, 19);

    private static SafetyVerifier CanonicalVerifier(Attempt attempt) =>
        attempt.Scheduler.ResearchVirtualizationCanonicalVerifierForTesting
        ?? throw new InvalidOperationException("Canonical test verifier was unavailable.");

    private static ReplayPhaseContext CreateReplayPhase(ulong epoch) =>
        new(true, epoch, 0x4000 + epoch * 8, 1, 0, 0, 0, ReplayPhaseInvalidationReason.None);

    private static SmtBundleMetadata4Way BundleFor(VmxMicroOp carrier) =>
        SmtBundleMetadata4Way.Empty(0).WithOperation(carrier);

    private static VmxMicroOp CreateCarrier()
    {
        var carrier = new VmxMicroOp
        {
            OpCode = IsaOpcodeValues.VMCALL,
            OwnerThreadId = 0,
            VirtualThreadId = 0,
            OwnerContextId = 42,
            Rd = 0,
            Rs1 = 0,
            Rs2 = 0,
            Placement = new SlotPlacementMetadata
            {
                RequiredSlotClass = SlotClass.SystemSingleton,
                PinningKind = SlotPinningKind.HardPinned,
                PinnedLaneId = 7,
                DomainTag = 7,
            },
            Instruction = new InstructionIR
            {
                CanonicalOpcode = new IsaOpcode(IsaOpcodeValues.VMCALL),
                Class = InstructionClass.Vmx,
                SerializationClass = SerializationClass.VmxSerial,
                Rd = 0,
                Rs1 = 0,
                Rs2 = 0,
                Imm = 0,
            },
        };
        carrier.RefreshWriteMetadata();
        return carrier;
    }

    private static BundleIssuePacket CreateIssuePacket(IssuePacketLane lane7) =>
        new(
            pc: 0x7000,
            decodeMode: DecodeMode.ClusterPreparedMode,
            validNonEmptyMask: 0x80,
            scalarCandidateMask: 0,
            scalarIssueMask: 0,
            selectedSlotMask: 0x80,
            unmappedSelectedSlotMask: 0,
            preparedScalarMask: 0,
            refinedPreparedScalarMask: 0,
            advisoryScalarIssueWidth: 0,
            refinedAdvisoryScalarIssueWidth: 0,
            executionMode: RuntimeClusterAdmissionExecutionMode.ClusterPrepared,
            shouldProbeClusterPath: false,
            usesIssuePacketAsExecutionSource: true,
            retainsReferenceSequentialPath: false,
            IssuePacketLane.CreateEmpty(0),
            IssuePacketLane.CreateEmpty(1),
            IssuePacketLane.CreateEmpty(2),
            IssuePacketLane.CreateEmpty(3),
            IssuePacketLane.CreateEmpty(4),
            IssuePacketLane.CreateEmpty(5),
            IssuePacketLane.CreateEmpty(6),
            lane7,
            BundleIssueFallbackInfo.CreateEmpty());

    private sealed record Attempt(
        MicroOpScheduler Scheduler,
        ReplayPhaseContext Phase,
        BundleIssuePacket Packet,
        IssuePacketLane Lane,
        VmxMicroOp Carrier);
}
