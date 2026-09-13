using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal sealed class PreExactProbeExecutorNoPublicationScenario : IVirtualizationScenario
{
    public string Id => "pre-exact-probe-executor-no-publication";
    public string Description =>
        "PR-E exact default-off executor and one-time E3 receipt evidence; no production composition or publication.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.ModelContract;

    public Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Fixture disabledFixture = CreateFixture(checked((ulong)iteration * 2 + 1));
            var disabled = new DomainHypercallRuntimeExecutor();
            context.Check(
                disabled.ExecuteExactProbe(
                    disabledFixture.Verifier,
                    disabledFixture.E2,
                    disabledFixture.RestoreOwner,
                    disabledFixture.LifecycleGate).Decision == DomainHypercallExecutionDecision.Disabled,
                "default executor must deny without consuming E2");
            context.Check(
                disabledFixture.Verifier.GetVirtualizationE2State(disabledFixture.E2) == VirtualizationE2State.Issued,
                "default-off rollback must preserve live E2 admission");

            Fixture enabledFixture = CreateFixture(checked((ulong)iteration * 2 + 2));
            var enabled = new DomainHypercallRuntimeExecutor(ExactProbeExecutionMode.ExactProbeOnly);
            DomainHypercallExecutionResult execution = enabled.ExecuteExactProbe(
                enabledFixture.Verifier, enabledFixture.E2, enabledFixture.RestoreOwner,
                enabledFixture.LifecycleGate);
            context.Check(execution.IsExecuted, "enabled exact executor must produce one E3");
            DomainHypercallRuntimeExecutor.ExecutionReceipt receipt = execution.Receipt!;
            context.Check(enabled.ValidateReceipt(receipt, enabledFixture.RestoreOwner).IsValid,
                "fresh exact E3 receipt must validate");
            context.Check(
                enabled.ExecuteExactProbe(
                    enabledFixture.Verifier,
                    enabledFixture.E2,
                    enabledFixture.RestoreOwner,
                    enabledFixture.LifecycleGate).Decision == DomainHypercallExecutionDecision.InvalidAdmission,
                "duplicate E2 execution must deny");
            enabledFixture.RestoreOwner.AdvanceAfterRestore();
            context.Check(
                enabled.ValidateReceipt(receipt, enabledFixture.RestoreOwner).Decision ==
                    DomainHypercallReceiptValidationDecision.RestoreGenerationMismatch,
                "restore must invalidate E3");
            context.Check(!receipt.HasStateEffect && !receipt.HasPayload,
                "E3 must carry exact no-state/no-payload semantics");
            context.Check(!receipt.CompletionPublicationAuthorized && !receipt.RetirePublicationAuthorized,
                "E3 must not grant completion or retire");

            context.Count("default_off_denials");
            context.Count("e3_executions");
            context.Count("duplicate_execution_denials");
            context.Count("restore_invalidation_denials");
            context.Count("nonzero_effect_digests");
            context.Count("nonzero_result_digests");
            context.Count("production_compositions", 0);
            context.Count("completion_publications", 0);
            context.Count("retire_publications", 0);
            context.Trace("pre-exact-probe-executor-no-publication",
                ("evidenceClass", "service-execution-no-publication"),
                ("decisionId", receipt.DecisionId),
                ("leaf", receipt.NumericLeaf),
                ("attemptId", receipt.AttemptId),
                ("e2Digest", receipt.E2Digest),
                ("e3Digest", receipt.ReceiptDigest),
                ("completionAuthority", false),
                ("retireAuthority", false));
            context.CompleteIteration("Exact E2 consumed once into E3 while production composition/publication remained absent.");
        }

        return Task.CompletedTask;
    }

    private static Fixture CreateFixture(ulong replayEpoch)
    {
        var verifier = new SafetyVerifier();
        VmxMicroOp carrier = CreateCarrier();
        ReplayPhaseContext replay = new(
            true, replayEpoch, 0x4000, 1, 0, 0, 0, ReplayPhaseInvalidationReason.None);
        SmtBundleMetadata4Way bundle = new(0, 42, 7, 7, 7, 1);
        SafetyVerifier.VirtualizationAdmissionCertificate e1 =
            verifier.IssueVirtualizationAdmissionAfterStageB(replay, bundle, carrier, 7, 7).Certificate!;
        carrier.AttachVirtualizationAdmission(e1);
        VirtualizationOperationOwnerSnapshot o1 =
            Phase38VirtualizationOperationOwnerSnapshotRegistry.ExactSnapshot;
        VirtualizationOperandSnapshot operand =
            new VirtualizationOperandSnapshotMaterializer()
                .CaptureAfterValidatedE1(carrier, e1, 1, 1, o1).Snapshot!;
        carrier.AttachVirtualizationOperandSnapshot(operand);

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
        var restoreOwner = new VirtualizationRestoreGenerationOwner();
        var lifecycleGate = new DomainHypercallLifecycleGate(7);
        if (!lifecycleGate.TryActivateExact(DomainHypercallExactActivationRequest.Phase38Exact))
            throw new InvalidOperationException("Exact diagnostic lifecycle gate activation failed.");
        VirtualizationE2IssueRequest request = new(
            replay, bundle, carrier, 7, 7, e1, o1, operand, domain, root,
            grantOwner, lease, restoreOwner, lifecycleGate);
        SafetyVerifier.VirtualizationOperationAdmissionCertificate e2 =
            verifier.IssueVirtualizationE2(request).Certificate!;
        return new(verifier, e2, restoreOwner, lifecycleGate);
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

    private sealed record Fixture(
        SafetyVerifier Verifier,
        SafetyVerifier.VirtualizationOperationAdmissionCertificate E2,
        VirtualizationRestoreGenerationOwner RestoreOwner,
        DomainHypercallLifecycleGate LifecycleGate);
}
