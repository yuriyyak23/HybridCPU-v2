using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal sealed class PrcO1OperandFaultOnlyScenario : IVirtualizationScenario
{
    public string Id => "prc-o1-operand-fault-only";
    public string Description =>
        "PR-C execution-only legality, immutable O1 and one-time canonical operands; fault-only and no E2/backend authority.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.ModelContract;

    public Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var verifier = new SafetyVerifier();
        var materializer = new VirtualizationOperandSnapshotMaterializer();
        var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
        core.InitializePipeline();

        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VirtualizationOperationOwnerSnapshotLoadResult load =
                VirtualizationOperationOwnerSnapshotLoader.LoadExactAcceptedPolicy();
            context.Check(load.IsLoaded, "exact accepted D2 must load one immutable O1 policy");
            VirtualizationOperationOwnerSnapshot o1 = load.Snapshot!;

            DomainRuntimeContext runtimeContext = CreateExecutionOnlyContext();
            DomainRuntimeOperation operation = new(
                DomainRuntimeOperationKind.InvokeCapability,
                DomainRuntimeOperationSource.RuntimeService,
                requiresCapabilityGrant: true,
                DomainRuntimeOperationAuthorityClass.NoStateExecution);
            CapabilityBoundaryRequirement capability = CapabilityBoundaryRequirement.TypedGrant(
                RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
                CapabilityGrantScope.DomainGranted);
            RuntimeBoundaryAdmissionResult admission = new RuntimeBoundaryAdmissionService().Validate(
                new(
                    runtimeContext,
                    CreateRoot(),
                    EvidencePolicy: null,
                    operation,
                    DomainBoundaryDescriptor.ExecutionOnly,
                    capability,
                    EvidenceBoundaryRequirement.None));
            context.Check(admission.IsAllowed, "execution-only common legality must not require Memory/IO or mutation privilege");

            VmxMicroOp vmx = CreateVmCall();
            ReplayPhaseContext replay = new(
                isActive: true,
                epochId: checked((ulong)iteration + 1),
                cachedPc: 0x7000,
                epochLength: 1,
                completedReplays: 0,
                validSlotCount: 1,
                stableDonorMask: 0,
                ReplayPhaseInvalidationReason.None);
            SmtBundleMetadata4Way bundle = new(
                ownerVirtualThreadId: 0,
                ownerContextId: 42,
                ownerDomainTag: 7,
                bundleDomainXor: 7,
                bundleDomainSum: 7,
                operationCount: 1);
            VirtualizationAdmissionIssueResult issue =
                verifier.IssueVirtualizationAdmissionAfterStageB(replay, bundle, vmx, 7, 7);
            context.Check(issue.IsIssued, "canonical SafetyVerifier must issue the existing fault-only E1 carrier");
            SafetyVerifier.VirtualizationAdmissionCertificate e1 = issue.Certificate!;
            vmx.AttachVirtualizationAdmission(e1);

            VirtualizationOperandCaptureResult capture = materializer.CaptureAfterValidatedE1(
                vmx,
                e1,
                rs1Value: 1,
                restoreGeneration: 1,
                o1);
            context.Check(capture.IsCaptured, "exact Rs1 leaf value must materialize once after E1");
            vmx.AttachVirtualizationOperandSnapshot(capture.Snapshot!);
            context.Check(VirtualizationOperandSnapshotMaterializer.ValidateForE2Input(
                    capture.Snapshot,
                    vmx,
                    e1,
                    o1,
                    currentRestoreGeneration: 1).IsValidForE2Input,
                "snapshot must validate only as a possible future E2 input");

            context.Check(!Phase38VirtualizationOperationOwnerSnapshotRegistry.TryResolve(
                    o1.OperationNamespace,
                    2,
                    out _),
                "adjacent O1 leaf must remain denied");
            context.Check(vmx.Execute(ref core), "VMCALL must still execute its fault-only carrier");
            context.Check(vmx.CreateRetireEffect().IsFaulted, "PR-C must preserve the production fault effect");
            context.Check(!capture.Snapshot!.BackendExecutionAuthorized, "operand snapshot must not authorize backend execution");

            context.Count("execution_only_admissions");
            context.Count("o1_policy_loads");
            context.Count("operand_snapshots");
            context.Count("adjacent_leaf_denials");
            context.Count("fault_only_effects");
            context.Count("e2_certificates", 0);
            context.Count("backend_authorizations", 0);
            context.Count("completion_publications", 0);
            context.Count("retire_publications", 0);
            context.Trace("prc-o1-operand-fault-only",
                ("evidenceClass", "runtime-policy-and-operand-fault-only"),
                ("o1Digest", o1.PolicyDigest),
                ("operandDigest", capture.Snapshot.OperandDigest),
                ("attemptId", e1.AttemptId),
                ("runtimeAuthority", false),
                ("backendAuthority", false));
            context.CompleteIteration("O1 and operands materialized while E2/backend/completion/retire remained absent.");
        }

        return Task.CompletedTask;
    }

    private static VmxMicroOp CreateVmCall()
    {
        var vmx = new VmxMicroOp
        {
            OpCode = IsaOpcodeValues.VMCALL,
            OwnerThreadId = 0,
            VirtualThreadId = 0,
            OwnerContextId = 42,
            Rd = 0,
            Rs1 = 5,
            Rs2 = 0,
            Instruction = new InstructionIR
            {
                CanonicalOpcode = new IsaOpcode(IsaOpcodeValues.VMCALL),
                Class = InstructionClass.Vmx,
                SerializationClass = SerializationClass.VmxSerial,
                Rd = 0,
                Rs1 = 5,
                Rs2 = 0,
                Imm = 0,
            },
        };
        vmx.Placement = vmx.Placement with { DomainTag = 7 };
        vmx.RefreshWriteMetadata();
        return vmx;
    }

    private static DomainRuntimeContext CreateExecutionOnlyContext() =>
        new(
            execution: new ExecutionDomainDescriptor(
                domainTag: 7,
                bundleLegality: new YAKSys_Hybrid_CPU.Core.BundleLegalityDescriptor(),
                schedulingBudget: null,
                extension: null,
                compatibilityProjectionEnabled: false),
            memory: null,
            io: null,
            capabilities: new CapabilityDescriptorSet(new CapabilityGrantCollection([
                new CapabilityGrant(
                    RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
                    CapabilityGrantScope.DomainGranted,
                    isGranted: true,
                    ownerDomainId: 7,
                    CapabilityDelegationPolicy.NonDelegable,
                    CapabilityRevocationPolicy.RuntimeRevocable,
                    CapabilityMigrationClass.DomainLocal,
                    CapabilityEvidenceVisibility.HostOnly,
                    CapabilityFrontendProjectionPolicy.NeverProject),
            ])),
            secureCompute: null,
            domainTag: 7,
            addressSpaceTag: 0);

    private static RootAuthorityDescriptor CreateRoot() =>
        new(
            RootAuthorityClass.RuntimeRoot,
            authorityEpoch: 1,
            grantedCapabilityMask: RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
            allowCompatibilityFrontendActivation: false,
            allowAuthoritativeStateMutation: false);
}
