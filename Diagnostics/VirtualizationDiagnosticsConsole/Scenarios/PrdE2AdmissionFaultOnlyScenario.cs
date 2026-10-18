using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal sealed class PrdE2AdmissionFaultOnlyScenario : IVirtualizationScenario
{
    public string Id => "prd-e2-admission-fault-only";
    public string Description =>
        "PR-D real SafetyVerifier E2 governance/admission evidence; exact leaf remains fault-only with no executor/publication.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.ModelContract;

    public Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var verifier = new SafetyVerifier();
            VmxMicroOp carrier = CreateCarrier();
            ReplayPhaseContext replay = new(
                true, checked((ulong)iteration + 1), 0x4000, 1, 0, 0, 0,
                ReplayPhaseInvalidationReason.None);
            SmtBundleMetadata4Way bundle = new(0, 42, 7, 7, 7, 1);
            VirtualizationAdmissionIssueResult e1Result =
                verifier.IssueVirtualizationAdmissionAfterStageB(replay, bundle, carrier, 7, 7);
            context.Check(e1Result.IsIssued, "live canonical E1 must issue");
            SafetyVerifier.VirtualizationAdmissionCertificate e1 = e1Result.Certificate!;
            carrier.AttachVirtualizationAdmission(e1);

            VirtualizationOperationOwnerSnapshot o1 =
                Phase38VirtualizationOperationOwnerSnapshotRegistry.ExactSnapshot;
            VirtualizationOperandSnapshot operand =
                new VirtualizationOperandSnapshotMaterializer()
                    .CaptureAfterValidatedE1(carrier, e1, 1, 1, o1).Snapshot!;
            carrier.AttachVirtualizationOperandSnapshot(operand);

            CapabilityGrant grant = CreateGrant();
            var grantOwner = new RuntimeCapabilityGrantOwner();
            RuntimeCapabilityGrantLease lease = grantOwner.Issue(grant);
            DomainRuntimeContext domain = CreateDomain(grant);
            RootAuthorityDescriptor root = new(
                RootAuthorityClass.RuntimeRoot, 1,
                RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
                allowCompatibilityFrontendActivation: false,
                allowAuthoritativeStateMutation: false);
            var restoreOwner = new VirtualizationRestoreGenerationOwner();
            var lifecycleGate = new DomainHypercallLifecycleGate(7);
            context.Check(
                lifecycleGate.TryActivateExact(DomainHypercallExactActivationRequest.Phase38Exact),
                "exact per-domain lifecycle gate must activate for this PR-D diagnostic fixture");
            VirtualizationE2IssueRequest request = new(
                replay, bundle, carrier, 7, 7, e1, o1, operand, domain, root,
                grantOwner, lease, restoreOwner, lifecycleGate);
            VirtualizationE2Result issuance = verifier.IssueVirtualizationE2(request);
            context.Check(issuance.IsIssued, "exact D2-bound SafetyVerifier E2 must issue");
            SafetyVerifier.VirtualizationOperationAdmissionCertificate e2 = issuance.Certificate!;
            context.Check(verifier.ValidateVirtualizationE2(e2, restoreOwner).IsLive, "fresh E2 must validate live");
            context.Check(
                verifier.IssueVirtualizationE2(request).Decision == VirtualizationE2Decision.DuplicateAttempt,
                "duplicate E2 issuance must deny");

            grantOwner.RevokeAll();
            context.Check(
                verifier.ValidateVirtualizationE2(e2, restoreOwner).Decision == VirtualizationE2Decision.CapabilityLeaseNotLive,
                "capability generation advancement must invalidate E2");
            restoreOwner.AdvanceAfterRestore();
            context.Check(
                verifier.ValidateVirtualizationE2(e2, restoreOwner).Decision == VirtualizationE2Decision.RestoreGenerationMismatch,
                "restore generation advancement must invalidate E2");

            var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            context.Check(carrier.Execute(ref core), "VMCALL carrier must still execute as fault-only");
            context.Check(carrier.CreateRetireEffect().IsFaulted, "PR-D must preserve fault-only retire effect");
            context.Check(!e2.BackendExecutionAuthorized, "E2 must not authorize a backend");

            context.Count("e2_issued");
            context.Count("e2_live_validations");
            context.Count("duplicate_issuance_denials");
            context.Count("capability_revocation_denials");
            context.Count("restore_generation_denials");
            context.Count("fault_only_effects");
            context.Count("backend_executions", 0);
            context.Count("completion_publications", 0);
            context.Count("retire_publications", 0);
            context.Trace("prd-e2-admission-fault-only",
                ("evidenceClass", "governance-admission-negative-fault-only"),
                ("decisionId", e2.DecisionId),
                ("leaf", e2.NumericLeaf),
                ("attemptId", e2.AttemptId),
                ("capabilityGeneration", e2.CapabilityGeneration),
                ("restoreGeneration", e2.RestoreGeneration),
                ("backendAuthority", false));
            context.CompleteIteration("Live E2 validated and invalidated fail-closed while backend/completion/retire remained absent.");
        }

        return Task.CompletedTask;
    }

    private static CapabilityGrant CreateGrant() => new(
        RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
        CapabilityGrantScope.DomainGranted,
        true, 7,
        CapabilityDelegationPolicy.NonDelegable,
        CapabilityRevocationPolicy.RuntimeRevocable,
        CapabilityMigrationClass.DomainLocal,
        CapabilityEvidenceVisibility.HostOnly,
        CapabilityFrontendProjectionPolicy.NeverProject);

    private static DomainRuntimeContext CreateDomain(CapabilityGrant grant) => new(
        new ExecutionDomainDescriptor(
            7, new YAKSys_Hybrid_CPU.Core.BundleLegalityDescriptor(), null, null, false),
        null, null,
        new CapabilityDescriptorSet(new CapabilityGrantCollection([grant])),
        null, 7, 0);

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
}
