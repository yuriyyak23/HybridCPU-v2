using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal sealed class E1FaultTransportScenario : IVirtualizationScenario
{
    public string Id => "e1-fault-transport";
    public string Description => "SafetyVerifier E1 and VMX fault-only execution/retire contract.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.RuntimeContract;

    public Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var verifier = new SafetyVerifier();
            var foreignVerifier = new SafetyVerifier();
            ReplayPhaseContext phase = VirtualizationFixtures.ReplayPhase((ulong)iteration + 1);
            SmtBundleMetadata4Way bundle = VirtualizationFixtures.Bundle();
            VmxMicroOp vmx = VirtualizationFixtures.CreateVmCall();

            VirtualizationAdmissionIssueResult issue =
                verifier.IssueVirtualizationAdmissionAfterStageB(phase, bundle, vmx, 7, 7);
            context.Check(issue.IsIssued, "canonical SafetyVerifier must issue E1 for a valid frozen VMX carrier");
            context.Check(issue.Decision == VirtualizationAdmissionIssueDecision.IssuedForFaultOnlyTransport,
                "E1 issuance decision must remain fault-only");
            SafetyVerifier.VirtualizationAdmissionCertificate certificate = issue.Certificate
                ?? throw new InvalidOperationException("E1 issue result omitted its opaque certificate.");
            context.Check(!certificate.BackendExecutionAuthorized, "E1 must not authorize backend execution");
            context.Check(!certificate.CompletionPublicationAuthorized, "E1 must not authorize completion publication");
            context.Check(!certificate.RetirePublicationAuthorized, "E1 must not authorize retire publication");
            context.Check(!certificate.HasAcceptedNumericLeaf, "E1 must not contain an accepted numeric VMCALL leaf");

            VirtualizationAdmissionValidationResult valid =
                verifier.ValidateVirtualizationAdmission(phase, bundle, vmx, 7, 7, certificate);
            VirtualizationAdmissionValidationResult foreign =
                foreignVerifier.ValidateVirtualizationAdmission(phase, bundle, vmx, 7, 7, certificate);
            context.Check(valid.IsValidForFaultOnlyTransport, "issuing verifier must validate its live E1");
            context.Check(foreign.Decision == VirtualizationAdmissionValidationDecision.IssuerMismatch,
                "foreign verifier must reject E1");

            vmx.AttachVirtualizationAdmission(certificate);
            var core = new Processor.CPU_Core(
                (ushort)(iteration & 3),
                CpuCorePlatformContext.CreateFixed(
                    new Processor.MainMemoryArea(),
                    ProcessorMode.Emulation));
            core.InitializePipeline();
            context.Check(vmx.Execute(ref core), "VMX fault-only micro-op must complete execution staging");
            VmxRetireEffect effect = vmx.CreateRetireEffect();
            context.Check(effect.IsFaulted, "VMX compatibility execution must resolve to a fault");
            context.Check(effect.FailureReason == VmExitReason.SecurityPolicyViolation,
                "fault-only VMX reason must remain SecurityPolicyViolation");
            VmxRetireOutcome outcome = core.ApplyRetiredVmxEffectForTesting(effect, 0);
            context.Check(outcome.Faulted, "retire must preserve VMX fault");
            context.Check(!outcome.HasRegisterWriteback, "fault-only VMX retire must not write a register");

            verifier.InvalidateVirtualizationAdmissions(ReplayPhaseInvalidationReason.Manual);
            VirtualizationAdmissionValidationResult stale =
                verifier.ValidateVirtualizationAdmission(phase, bundle, vmx, 7, 7, certificate);
            context.Check(stale.Decision == VirtualizationAdmissionValidationDecision.IssuerGenerationMismatch,
                "invalidated E1 must not be reusable");

            context.Count("issued_e1");
            context.Count("fault_only_effects");
            context.Count("foreign_issuer_rejections");
            context.Count("stale_e1_rejections");
            context.Trace("e1-fault-transport",
                ("attemptId", certificate.AttemptId),
                ("replayEpoch", certificate.ReplayEpoch),
                ("operation", certificate.Operation),
                ("retireFault", outcome.Faulted),
                ("registerWriteback", outcome.HasRegisterWriteback));
            context.CompleteIteration("E1 issued, validated, faulted and invalidated.");
        }

        return Task.CompletedTask;
    }
}
