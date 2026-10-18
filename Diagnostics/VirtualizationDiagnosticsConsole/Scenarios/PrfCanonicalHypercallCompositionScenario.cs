using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal sealed class PrfCanonicalHypercallCompositionScenario : IVirtualizationScenario
{
    public string Id => "prf-canonical-hypercall-composition";
    public string Description =>
        "PR-F exclusive canonical E1/operand/E2-to-E3 composition with fault-only retire and rollback evidence.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.ModelContract;

    public Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Fixture active = CreateFixture(configure: true, checked((ulong)iteration * 4 + 1));
            context.Check(Materialize(active, 1), "canonical lane-7 seam must capture E1 and exact operands");
            DomainHypercallCompositionResult prepared = active.Scheduler.LastExactVirtualizationCompositionResult!.Value;
            context.Check(prepared.IsPrepared, "canonical seam must prepare exact E2");
            YAKSys_Hybrid_CPU.Processor.CPU_Core activeCore = active.Core;
            context.Check(active.Carrier.Execute(ref activeCore), "execute-stage carrier must complete deterministically");
            DomainHypercallExecutionResult execution = active.Carrier.ExactHypercallExecutionResult!.Value;
            context.Check(execution.IsExecuted && execution.Receipt is not null,
                "canonical execute must consume E2 into one E3");
            context.Check(active.Carrier.CreateRetireEffect().IsFaulted,
                "PR-F must preserve fault-only VMX retire");
            context.Check(!execution.CompletionPublicationAuthorized && !execution.RetirePublicationAuthorized,
                "E3 must grant no completion or retire authority");

            Fixture fallback = CreateFixture(configure: false, checked((ulong)iteration * 4 + 2));
            context.Check(Materialize(fallback, 1), "default path must preserve canonical E1/operand capture");
            YAKSys_Hybrid_CPU.Processor.CPU_Core fallbackCore = fallback.Core;
            context.Check(fallback.Carrier.Execute(ref fallbackCore), "default path must still execute fault contour");
            context.Check(fallback.Carrier.ExactHypercallExecutionResult is null &&
                          fallback.Carrier.CreateRetireEffect().IsFaulted,
                "no binding must preserve PR-D fault-only rollback");

            Fixture adjacent = CreateFixture(configure: true, checked((ulong)iteration * 4 + 3));
            context.Check(!Materialize(adjacent, 2), "adjacent leaf must never prepare E2");
            context.Check(adjacent.Scheduler.LastExactVirtualizationCompositionResult is null,
                "adjacent leaf must not reach composition");

            Fixture disabled = CreateFixture(configure: true, checked((ulong)iteration * 4 + 4));
            context.Check(Materialize(disabled, 1), "rollback fixture must prepare before revocation");
            disabled.Scheduler.DisableExactVirtualizationComposition();
            YAKSys_Hybrid_CPU.Processor.CPU_Core disabledCore = disabled.Core;
            context.Check(disabled.Carrier.Execute(ref disabledCore), "disabled carrier must remain deterministic");
            context.Check(disabled.Carrier.ExactHypercallExecutionResult?.Decision ==
                          DomainHypercallExecutionDecision.Disabled,
                "disable-before-execute must deny E3");

            DomainHypercallRuntimeExecutor.ExecutionReceipt receipt = execution.Receipt!;
            context.Count("canonical_e4_compositions");
            context.Count("exact_e3_executions");
            context.Count("default_off_faults");
            context.Count("adjacent_leaf_denials");
            context.Count("rollback_revocation_denials");
            context.Count("completion_publications", 0);
            context.Count("retire_publications", 0);
            context.Count("compatibility_direct_invocations", 0);
            context.Trace("prf-canonical-hypercall-composition",
                ("evidenceClass", "canonical-composition-no-publication"),
                ("decisionId", receipt.DecisionId),
                ("leaf", receipt.NumericLeaf),
                ("attemptId", receipt.AttemptId),
                ("e2Digest", receipt.E2Digest),
                ("e3Digest", receipt.ReceiptDigest),
                ("vmxRetireFaultOnly", true),
                ("completionAuthority", false),
                ("retireAuthority", false));
            context.CompleteIteration("Canonical E4 reached one E3 while default/adjacent/rollback paths remained fail-closed.");
        }

        return Task.CompletedTask;
    }

    internal static bool Materialize(Fixture fixture, ulong rs1Value)
    {
        if (!fixture.Scheduler.TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization(
                fixture.Packet, fixture.Lane))
            return false;

        return fixture.Scheduler.TryAttachVirtualizationOperandSnapshotAfterCanonicalValueRead(
            fixture.Packet,
            fixture.Lane,
            rs1Value,
            fixture.RestoreOwner.CurrentGeneration);
    }

    internal static Fixture CreateFixture(
        bool configure,
        ulong replayEpoch,
        bool completion = false,
        bool retirement = false)
    {
        var scheduler = new MicroOpScheduler();
        scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
            true, replayEpoch, 0x4000, 1, 0, 0, 0, ReplayPhaseInvalidationReason.None));
        VmxMicroOp carrier = CreateCarrier();
        IssuePacketLane lane = new(
            7, true, 7, 0, 0, IsaOpcodeValues.VMCALL, carrier,
            SlotClass.SystemSingleton, SlotPinningKind.HardPinned, false);
        BundleIssuePacket packet = CreatePacket(lane);
        var restoreOwner = new VirtualizationRestoreGenerationOwner();
        var lifecycleGate = new DomainHypercallLifecycleGate(7);
        DomainHypercallCompletionOwner? completionOwner = null;
        DomainHypercallCanonicalComposition? composition = null;
        DomainHypercallRuntimeExecutor? executor = null;
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);

        if (configure)
        {
            if (!lifecycleGate.TryActivateExact(DomainHypercallExactActivationRequest.Phase38Exact))
                throw new InvalidOperationException("Exact diagnostic lifecycle gate activation failed.");
            CapabilityGrant grant = new(
                RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
                CapabilityGrantScope.DomainGranted,
                true, 7,
                CapabilityDelegationPolicy.NonDelegable,
                CapabilityRevocationPolicy.RuntimeRevocable,
                CapabilityMigrationClass.DomainLocal,
                CapabilityEvidenceVisibility.HostOnly,
                CapabilityFrontendProjectionPolicy.NeverProject);
            var grantOwner = new RuntimeCapabilityGrantOwner();
            RuntimeCapabilityGrantLease lease = grantOwner.Issue(grant);
            DomainRuntimeContext domain = new(
                new ExecutionDomainDescriptor(
                    7, new YAKSys_Hybrid_CPU.Core.BundleLegalityDescriptor(), null, null, false),
                null, null,
                new CapabilityDescriptorSet(new CapabilityGrantCollection([grant])),
                null, 7, 0);
            RootAuthorityDescriptor root = new(
                RootAuthorityClass.RuntimeRoot, 1,
                RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
                false, false);
            completionOwner = completion ? new DomainHypercallCompletionOwner() : null;
            executor = new DomainHypercallRuntimeExecutor(ExactProbeExecutionMode.ExactProbeOnly);
            composition = new(
                domain,
                root,
                grantOwner,
                lease,
                restoreOwner,
                executor,
                completionOwner,
                retirement ? core.ExactHypercallRetireOwner : null,
                lifecycleGate);
            scheduler.ConfigureExactVirtualizationComposition(composition);
        }

        return new(
            scheduler,
            carrier,
            lane,
            packet,
            restoreOwner,
            composition,
            executor,
            completionOwner,
            lifecycleGate,
            core);
    }

    private static VmxMicroOp CreateCarrier()
    {
        var carrier = new VmxMicroOp
        {
            OpCode = IsaOpcodeValues.VMCALL,
            OwnerThreadId = 0,
            VirtualThreadId = 0,
            OwnerContextId = 42,
            Rs1 = 5,
            Rs2 = 0,
            Rd = 0,
            Instruction = new InstructionIR
            {
                CanonicalOpcode = new IsaOpcode(IsaOpcodeValues.VMCALL),
                Class = InstructionClass.Vmx,
                SerializationClass = SerializationClass.VmxSerial,
                Rs1 = 5,
                Rs2 = 0,
                Rd = 0,
                Imm = 0,
            },
        };
        carrier.Placement = carrier.Placement with { DomainTag = 7 };
        carrier.RefreshWriteMetadata();
        return carrier;
    }

    private static BundleIssuePacket CreatePacket(IssuePacketLane lane7) => new(
        0x7000,
        DecodeMode.ClusterPreparedMode,
        0x80, 0, 0, 0x80, 0, 0, 0, 0, 0,
        RuntimeClusterAdmissionExecutionMode.ClusterPrepared,
        false, true, false,
        IssuePacketLane.CreateEmpty(0),
        IssuePacketLane.CreateEmpty(1),
        IssuePacketLane.CreateEmpty(2),
        IssuePacketLane.CreateEmpty(3),
        IssuePacketLane.CreateEmpty(4),
        IssuePacketLane.CreateEmpty(5),
        IssuePacketLane.CreateEmpty(6),
        lane7,
        BundleIssueFallbackInfo.CreateEmpty());

    internal sealed record Fixture(
        MicroOpScheduler Scheduler,
        VmxMicroOp Carrier,
        IssuePacketLane Lane,
        BundleIssuePacket Packet,
        VirtualizationRestoreGenerationOwner RestoreOwner,
        DomainHypercallCanonicalComposition? Composition,
        DomainHypercallRuntimeExecutor? Executor,
        DomainHypercallCompletionOwner? CompletionOwner,
        DomainHypercallLifecycleGate LifecycleGate,
        YAKSys_Hybrid_CPU.Processor.CPU_Core Core);
}
