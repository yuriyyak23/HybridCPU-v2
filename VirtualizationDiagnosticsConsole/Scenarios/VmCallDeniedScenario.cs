using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Vmx;

namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal sealed class VmCallDeniedScenario : IVirtualizationScenario
{
    public string Id => "vmcall-denied";
    public string Description => "VMCALL trap projection with backend execution kept denied.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.RuntimeContract;

    public Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var service = new VmxCompatibilityAdmissionService();
        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VmxCompatibilityTrapAdmissionResult admittedDenied =
                service.AdmitVmCallTrapProjection(VirtualizationFixtures.VmCallRequest());
            context.Check(admittedDenied.Decision == VmxCompatibilityTrapAdmissionDecision.TrapProjectionDeniedBackend,
                "VMCALL must end in admitted-denied trap projection");
            context.Check(admittedDenied.RuntimeAdmissionAllowed, "runtime projection admission must be observable");
            context.Check(admittedDenied.BackendAdmission.DeniesBackendExecution,
                "missing neutral backend descriptor must deny execution");
            context.Check(admittedDenied.NeutralResult.ShouldTrap, "neutral runtime result must request a trap");
            context.Check(admittedDenied.ProjectedDecision.ShouldExit,
                "compatibility mapper may project an exit without authorizing completion");
            context.Check(admittedDenied.IsAdmittedDeniedTrapProjection,
                "result must identify the admitted-denied contour");

            VmxCompatibilityTrapAdmissionResult noEvidence =
                service.AdmitVmCallTrapProjection(VirtualizationFixtures.VmCallRequest(projectionEvidenceValidated: false));
            context.Check(noEvidence.Decision == VmxCompatibilityTrapAdmissionDecision.ProjectionDenied,
                "VMCALL must fail before runtime when projection evidence is absent");
            context.Check(!noEvidence.RuntimeAdmissionAllowed,
                "projection denial must not reach runtime admission");

            VmxCompatibilityTrapAdmissionResult noAlias =
                service.AdmitVmCallTrapProjection(VirtualizationFixtures.VmCallRequest(allowAliases: false));
            context.Check(noAlias.Decision == VmxCompatibilityTrapAdmissionDecision.RuntimeAdmissionDenied,
                "closed compatibility alias policy must deny runtime admission");

            VmxCompatibilityTrapAdmissionResult noTrap =
                service.AdmitVmCallTrapProjection(VirtualizationFixtures.VmCallRequest(enableTrap: false));
            context.Check(noTrap.Decision == VmxCompatibilityTrapAdmissionDecision.TrapPolicyDenied,
                "neutral trap policy must remain authoritative");

            context.Count("admitted_denied_vmcall");
            context.Count("projection_evidence_rejections");
            context.Count("alias_policy_rejections");
            context.Count("trap_policy_rejections");
            context.Trace("vmcall-denied",
                ("decision", admittedDenied.Decision),
                ("backendDecision", admittedDenied.BackendAdmission.Decision),
                ("exitReason", admittedDenied.ProjectedDecision.ExitReason));
            context.CompleteIteration("VMCALL remained admitted-denied with three negative controls.");
        }

        return Task.CompletedTask;
    }
}
