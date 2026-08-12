using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal sealed class ResearchCanonicalCompositionScenario : IVirtualizationScenario
{
    public string Id => "research-canonical-composition";
    public string Description => "Default-off TESTING-only P2 composition at canonical issue/materialization with fail-closed identity and replay diagnostics.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.RuntimeContract;

    public async Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ulong epoch = (ulong)iteration * 20 + 1;

            Attempt defaultOff = CreateAttempt(epoch);
            context.Check(Materialize(defaultOff), "canonical E1 must materialize while P2 remains default-off");
            context.Check(defaultOff.Scheduler.LastResearchVirtualizationCanonicalCompositionResult is null,
                "default-off P2 must not create a research receipt");
            context.Count("default_off_no_receipt");

            Attempt positive = CreateAttempt(epoch + 1);
            var positiveOwner = new ResearchVirtualizationRuntimeOwner();
            positive.Scheduler.EnableResearchVirtualizationCanonicalComposition(
                new ResearchVirtualizationCanonicalIssueComposition(positiveOwner, CreateContext()));
            context.Check(Materialize(positive), "live canonical E1 must reach the armed TESTING-only P2 seam");
            ResearchVirtualizationCanonicalCompositionResult positiveResult = Result(positive);
            context.Check(positiveResult.Succeeded, "P2 must materialize one exact-once receipt");
            ResearchVirtualizationRuntimeOwner.ExecutionReceipt receipt = positiveResult.Receipt
                ?? throw new InvalidOperationException("P2 success omitted its receipt.");
            context.Check(receipt.Identity.VirtualThreadId == 0, "receipt VT identity must match E1");
            context.Check(receipt.Identity.OwnerContextId == 42, "receipt context identity must match E1");
            context.Check(receipt.Identity.DomainTag == 7, "receipt domain identity must match E1");
            context.Check(receipt.Identity.AddressSpaceTag == 9, "receipt must bind the typed address-space identity");
            context.Check(receipt.Identity.CapabilityGeneration == 13, "receipt must bind capability generation");
            context.Check(receipt.Identity.EvidenceGeneration == 17, "receipt must bind evidence generation");
            context.Check(receipt.Identity.RestoreGeneration == 19, "receipt must bind restore generation");
            context.Check(receipt.PayloadLength == 0 && receipt.StateMutationCount == 0,
                "P2 receipt must remain no-state/no-payload");
            context.Check(!receipt.CompletionPublicationAuthorized && !receipt.RetirePublicationAuthorized,
                "P2 receipt must authorize neither completion nor retire");
            context.Count("canonical_receipt_materialized");

            Attempt sourceSlot = CreateAttempt(epoch + 2, sourceSlot: 6);
            sourceSlot.Scheduler.EnableResearchVirtualizationCanonicalComposition(
                new ResearchVirtualizationCanonicalIssueComposition(
                    new ResearchVirtualizationRuntimeOwner(), CreateContext()));
            context.Check(!Materialize(sourceSlot), "displaced source slot must deny E1 before P2");
            context.Check(sourceSlot.Carrier.VirtualizationAdmission is null,
                "source-slot denial must not leave an E1 carrier");
            context.Count("source_slot_denied");

            Attempt workingSlot = MaterializeE1(epoch + 3);
            ResearchVirtualizationCanonicalCompositionResult workingSlotResult =
                ComposeDirect(workingSlot, new ResearchVirtualizationCanonicalIssueComposition(
                    new ResearchVirtualizationRuntimeOwner(), CreateContext()), workingSlotId: 6);
            context.Check(
                workingSlotResult.AdmissionDecision == ResearchVirtualizationProbeAdmissionDecision.DeniedE1Carrier,
                "wrong working slot must deny prototype admission");
            context.Count("working_slot_denied");

            Attempt replay = MaterializeE1(epoch + 4);
            ResearchVirtualizationCanonicalCompositionResult replayResult =
                new ResearchVirtualizationCanonicalIssueComposition(
                    new ResearchVirtualizationRuntimeOwner(), CreateContext())
                .Compose(
                    Verifier(replay),
                    CreateReplayPhase(epoch + 4000),
                    BundleFor(replay.Carrier),
                    replay.Carrier,
                    7,
                    7,
                    replay.Carrier.VirtualizationAdmission!);
            context.Check(replayResult.AdmissionDecision == ResearchVirtualizationProbeAdmissionDecision.DeniedE1Carrier,
                "replay identity change must deny stale E1");
            context.Count("replay_mismatch_denied");

            Attempt squash = MaterializeE1(epoch + 5);
            Verifier(squash).InvalidateVirtualizationAdmissions(ReplayPhaseInvalidationReason.Manual);
            ResearchVirtualizationCanonicalCompositionResult squashResult = ComposeDirect(
                squash,
                new ResearchVirtualizationCanonicalIssueComposition(
                    new ResearchVirtualizationRuntimeOwner(), CreateContext()));
            context.Check(squashResult.AdmissionDecision == ResearchVirtualizationProbeAdmissionDecision.DeniedE1Carrier,
                "squash must revoke E1 before P2 admission");
            context.Count("squashed_e1_denied");

            Attempt identity = CreateAttempt(epoch + 6);
            identity.Scheduler.EnableResearchVirtualizationCanonicalComposition(
                new ResearchVirtualizationCanonicalIssueComposition(
                    new ResearchVirtualizationRuntimeOwner(),
                    CreateContext(virtualThreadId: 1, ownerContextId: 41, domainTag: 8)));
            context.Check(Materialize(identity), "E1 remains independently materializable for identity denial probe");
            context.Check(Result(identity).AdmissionDecision ==
                ResearchVirtualizationProbeAdmissionDecision.DeniedCarrierIdentityMismatch,
                "VT/context/domain mismatch must deny P2 admission");
            context.Count("carrier_identity_denied");

            Attempt foreignContext = CreateAttempt(epoch + 7);
            ResearchVirtualizationOperationContext leaseOwner = CreateContext();
            foreignContext.Scheduler.EnableResearchVirtualizationCanonicalComposition(
                new ResearchVirtualizationCanonicalIssueComposition(
                    new ResearchVirtualizationRuntimeOwner(),
                    CreateContext(addressSpaceTag: 10),
                    contextLease: leaseOwner.CaptureMaterializationLease()));
            context.Check(Materialize(foreignContext), "E1 remains live for foreign-context denial probe");
            context.Check(Result(foreignContext).Decision ==
                ResearchVirtualizationCanonicalCompositionDecision.DeniedStaleRuntimeContextLease,
                "foreign address-space/context lease must deny P2 materialization");
            context.Count("foreign_context_denied");

            CountGenerationDenial(context, CreateGenerationAttempt(epoch + 8, static value => value.AdvanceCapabilityGeneration()),
                "stale_capability_generation_denied");
            CountGenerationDenial(context, CreateGenerationAttempt(epoch + 9, static value => value.AdvanceEvidenceGeneration()),
                "stale_evidence_generation_denied");
            CountGenerationDenial(context, CreateGenerationAttempt(epoch + 10, static value => value.AdvanceRestoreGeneration()),
                "stale_restore_generation_denied");

            Attempt foreignOwner = CreateAttempt(epoch + 11);
            foreignOwner.Scheduler.EnableResearchVirtualizationCanonicalComposition(
                new ResearchVirtualizationCanonicalIssueComposition(
                    new ResearchVirtualizationRuntimeOwner(),
                    CreateContext(),
                    executionOwner: new ResearchVirtualizationRuntimeOwner()));
            context.Check(Materialize(foreignOwner), "E1 remains live for foreign-owner denial probe");
            context.Check(Result(foreignOwner).ExecutionDecision ==
                ResearchVirtualizationProbeExecutionDecision.DeniedForeignOwner,
                "foreign runtime owner must deny P2 execution");
            context.Count("foreign_owner_denied");

            Attempt stalePolicy = CreateAttempt(epoch + 12);
            var policyOwner = new ResearchVirtualizationRuntimeOwner();
            stalePolicy.Scheduler.EnableResearchVirtualizationCanonicalComposition(
                new ResearchVirtualizationCanonicalIssueComposition(policyOwner, CreateContext()));
            policyOwner.InvalidatePolicy();
            context.Check(Materialize(stalePolicy), "E1 remains live for stale-policy denial probe");
            context.Check(Result(stalePolicy).ExecutionDecision ==
                ResearchVirtualizationProbeExecutionDecision.DeniedStalePolicyGeneration,
                "stale owner policy must deny P2 execution");
            context.Count("stale_policy_denied");

            Attempt postAdmissionRevocation = CreateAttempt(epoch + 13);
            postAdmissionRevocation.Scheduler.EnableResearchVirtualizationCanonicalComposition(
                new ResearchVirtualizationCanonicalIssueComposition(
                    new ResearchVirtualizationRuntimeOwner(),
                    CreateContext(),
                    afterAdmission: verifier => verifier.InvalidateVirtualizationAdmissions(
                        ReplayPhaseInvalidationReason.Manual)));
            context.Check(Materialize(postAdmissionRevocation), "E1 must reach post-admission revocation probe");
            context.Check(Result(postAdmissionRevocation).ExecutionDecision ==
                ResearchVirtualizationProbeExecutionDecision.DeniedStaleAdmission,
                "post-admission E1 revocation must deny receipt materialization");
            context.Count("post_admission_revocation_denied");

            Attempt concurrent = MaterializeE1(epoch + 14);
            var concurrentComposition = new ResearchVirtualizationCanonicalIssueComposition(
                new ResearchVirtualizationRuntimeOwner(), CreateContext());
            Task<ResearchVirtualizationCanonicalCompositionResult>[] tasks = Enumerable.Range(0, 8)
                .Select(_ => Task.Run(
                    () => ComposeDirect(concurrent, concurrentComposition), cancellationToken))
                .ToArray();
            ResearchVirtualizationCanonicalCompositionResult[] concurrentResults = await Task.WhenAll(tasks);
            context.Check(concurrentResults.Count(value => value.Succeeded) == 1,
                "concurrent P2 consumption must materialize exactly one receipt");
            context.Check(concurrentResults.Count(value => value.Decision ==
                ResearchVirtualizationCanonicalCompositionDecision.DeniedDuplicateMaterialization) == 7,
                "all losing concurrent P2 consumers must fail closed as duplicates");
            context.Count("concurrent_exact_once_receipt");
            context.Count("duplicate_materialization_denied", 7);

            var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            context.Check(positive.Carrier.Execute(ref core), "production VMX carrier must still produce its fault effect");
            VmxRetireEffect effect = positive.Carrier.CreateRetireEffect();
            VmxRetireOutcome outcome = core.ApplyRetiredVmxEffectForTesting(effect, 0);
            context.Check(effect.IsFaulted && outcome.Faulted && !outcome.HasRegisterWriteback,
                "green P2 diagnostics must not alter production VMX fault/retire behavior");
            context.Count("production_fault_retire_unchanged");

            context.Trace("research-canonical-composition",
                ("carrierAttemptId", receipt.Identity.CarrierAttemptId),
                ("replayEpoch", receipt.Identity.ReplayEpoch),
                ("virtualThreadId", receipt.Identity.VirtualThreadId),
                ("ownerContextId", receipt.Identity.OwnerContextId),
                ("domainTag", receipt.Identity.DomainTag),
                ("addressSpaceTag", receipt.Identity.AddressSpaceTag),
                ("capabilityGeneration", receipt.Identity.CapabilityGeneration),
                ("evidenceGeneration", receipt.Identity.EvidenceGeneration),
                ("restoreGeneration", receipt.Identity.RestoreGeneration),
                ("sourceSlotId", 7),
                ("workingSlotId", 7),
                ("payloadLength", receipt.PayloadLength),
                ("stateMutationCount", receipt.StateMutationCount),
                ("completionAuthorized", receipt.CompletionPublicationAuthorized),
                ("retireAuthorized", receipt.RetirePublicationAuthorized));
            context.CompleteIteration(
                "P2 canonical receipt materialized once; replay/squash/slot/identity/generation/owner/policy/revocation/duplicate paths denied.");
        }
    }

    private static (Attempt Attempt, ResearchVirtualizationOperationContext Context) CreateGenerationAttempt(
        ulong epoch,
        Action<ResearchVirtualizationOperationContext> invalidate)
    {
        Attempt attempt = CreateAttempt(epoch);
        ResearchVirtualizationOperationContext runtimeContext = CreateContext();
        attempt.Scheduler.EnableResearchVirtualizationCanonicalComposition(
            new ResearchVirtualizationCanonicalIssueComposition(
                new ResearchVirtualizationRuntimeOwner(), runtimeContext));
        invalidate(runtimeContext);
        return (attempt, runtimeContext);
    }

    private static void CountGenerationDenial(
        ScenarioExecutionContext context,
        (Attempt Attempt, ResearchVirtualizationOperationContext Context) probe,
        string counter)
    {
        _ = probe.Context;
        context.Check(Materialize(probe.Attempt), "E1 remains live for stale-generation denial probe");
        context.Check(Result(probe.Attempt).Decision ==
            ResearchVirtualizationCanonicalCompositionDecision.DeniedStaleRuntimeContextLease,
            "stale authority generation must deny P2 context materialization");
        context.Count(counter);
    }

    private static ResearchVirtualizationCanonicalCompositionResult ComposeDirect(
        Attempt attempt,
        ResearchVirtualizationCanonicalIssueComposition composition,
        int workingSlotId = 7) =>
        composition.Compose(
            Verifier(attempt),
            attempt.Phase,
            BundleFor(attempt.Carrier),
            attempt.Carrier,
            7,
            workingSlotId,
            attempt.Carrier.VirtualizationAdmission!);

    private static ResearchVirtualizationCanonicalCompositionResult Result(Attempt attempt) =>
        attempt.Scheduler.LastResearchVirtualizationCanonicalCompositionResult
        ?? throw new InvalidOperationException("Canonical P2 seam did not record a result.");

    private static SafetyVerifier Verifier(Attempt attempt) =>
        attempt.Scheduler.ResearchVirtualizationCanonicalVerifierForTesting
        ?? throw new InvalidOperationException("Canonical SafetyVerifier was unavailable.");

    private static Attempt MaterializeE1(ulong epoch)
    {
        Attempt attempt = CreateAttempt(epoch);
        if (!Materialize(attempt))
            throw new InvalidOperationException("Canonical E1 materialization failed.");
        return attempt;
    }

    private static bool Materialize(Attempt attempt) =>
        attempt.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
            attempt.Packet,
            attempt.Lane);

    private static Attempt CreateAttempt(ulong epoch, byte sourceSlot = 7)
    {
        var scheduler = new MicroOpScheduler();
        ReplayPhaseContext phase = CreateReplayPhase(epoch);
        scheduler.SetReplayPhaseContext(phase);
        VmxMicroOp carrier = VirtualizationFixtures.CreateVmCall(domainTag: 7);
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

    private static ReplayPhaseContext CreateReplayPhase(ulong epoch) =>
        new(true, epoch, 0x4000 + epoch * 8, 1, 0, 0, 0, ReplayPhaseInvalidationReason.None);

    private static SmtBundleMetadata4Way BundleFor(VmxMicroOp carrier) =>
        SmtBundleMetadata4Way.Empty(0).WithOperation(carrier);

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
