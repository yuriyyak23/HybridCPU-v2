using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.SecureComputeDiagnostics;

internal sealed class AdmissionBoundaryScenario : ISecureComputeScenario
{
    public string Id => "admission-boundary";
    public string Description => "Exercises ordinary, missing, disabled and generic active-descriptor admission decisions.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.RuntimeContract;
    public string AuthorityCeiling => "Direct runtime service and policy behavior only; no CPU/SafetyVerifier/certificate/issue reachability proof.";

    public Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var service = new RuntimeBoundaryAdmissionService();
        var policy = new SecureDomainAdmissionPolicy();
        SecureComputeDomainDescriptor active = SecureComputeFixtures.CreateDescriptor(41);
        SecureDomainOperationClass[] secureClasses = Enum.GetValues<SecureDomainOperationClass>()
            .Where(value => value != SecureDomainOperationClass.Ordinary)
            .ToArray();

        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RuntimeBoundaryAdmissionResult ordinary = service.Validate(SecureComputeFixtures.CreateRequest(
                SecureComputeFixtures.CreateContext(null), null, SecureDomainOperationClass.Ordinary));
            context.Check(ordinary.IsAllowed, "ordinary operation with no descriptor remains allowed");
            context.Count("ordinary_allowed");

            foreach (SecureDomainOperationClass operation in secureClasses)
            {
                RuntimeBoundaryAdmissionResult missing = service.Validate(SecureComputeFixtures.CreateRequest(
                    SecureComputeFixtures.CreateContext(null), null, operation));
                RuntimeBoundaryAdmissionResult disabled = service.Validate(SecureComputeFixtures.CreateRequest(
                    SecureComputeFixtures.CreateContext(SecureComputeDomainDescriptor.Disabled), null, operation));
                context.Check(!missing.IsAllowed, $"{operation} denies a missing descriptor");
                context.Check(!disabled.IsAllowed, $"{operation} denies a disabled descriptor");
                context.Count("secure_missing_descriptor_denied");
                context.Count("secure_disabled_descriptor_denied");

                SecureDomainAdmissionResult direct = policy.Admit(active, operation, null, null);
                if (direct.Decision == SecureDomainAdmissionDecision.AllowedSecureOperation)
                    context.Count("generic_policy_positive_decisions");
                else
                    context.Count($"policy_decision_{direct.Decision}");
            }

            context.Trace("admission-matrix", ("secureClasses", secureClasses.Length));
            context.CompleteIteration("Admission matrix completed.");
        }

        context.Finding(
            "C0-OPERATION-SPECIFIC-ADMISSION",
            DiagnosticSeverity.Blocker,
            "Generic positive policy decision remains observable",
            "Several non-ordinary operation classes converge on AllowedSecureOperation. This is policy behavior, not an execution certificate or exhaustive operation-specific authority.");
        context.Finding(
            "BOUNDARY-ADMISSION-ONLY",
            DiagnosticSeverity.Information,
            "Direct service invocation is not CPU reachability",
            "The scenario proves fail-closed inputs at the generic runtime boundary only; it does not prove decode-to-retire composition.");
        return Task.CompletedTask;
    }
}
