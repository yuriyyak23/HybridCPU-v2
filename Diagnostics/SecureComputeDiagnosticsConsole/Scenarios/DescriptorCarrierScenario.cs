using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.SecureComputeDiagnostics;

internal sealed class DescriptorCarrierScenario : ISecureComputeScenario
{
    public string Id => "descriptor-carriers";
    public string Description => "Demonstrates context/request descriptor carriers and the current context-first selection rule.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.RuntimeContract;
    public string AuthorityCeiling => "Carrier precedence observation only; caller-supplied descriptors are not lifecycle authority.";

    public Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        var service = new RuntimeBoundaryAdmissionService();
        SecureComputeDomainDescriptor contextDescriptor = SecureComputeFixtures.CreateDescriptor(41);
        SecureComputeDomainDescriptor requestDescriptor = SecureComputeFixtures.CreateDescriptor(43);

        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RuntimeBoundaryAdmissionResult contextOnly = service.Validate(SecureComputeFixtures.CreateRequest(
                SecureComputeFixtures.CreateContext(contextDescriptor), null, SecureDomainOperationClass.EnterSecureDomain));
            RuntimeBoundaryAdmissionResult requestOnly = service.Validate(SecureComputeFixtures.CreateRequest(
                SecureComputeFixtures.CreateContext(null, requestDescriptor.DomainTag), requestDescriptor, SecureDomainOperationClass.EnterSecureDomain));
            RuntimeBoundaryAdmissionResult both = service.Validate(SecureComputeFixtures.CreateRequest(
                SecureComputeFixtures.CreateContext(contextDescriptor), requestDescriptor, SecureDomainOperationClass.EnterSecureDomain));

            context.Check(contextOnly.IsAllowed, "context descriptor is accepted by direct runtime service");
            context.Check(requestOnly.IsAllowed, "request descriptor is accepted by direct runtime service");
            context.Check(both.IsAllowed, "context carrier wins when both descriptors are present");
            context.Count("context_carrier_accepted");
            context.Count("request_carrier_accepted");
            context.Count("dual_carrier_context_precedence");
            context.Trace("descriptor-carriers", ("contextDomain", 41), ("requestDomain", 43));
            context.CompleteIteration("Descriptor carrier matrix completed.");
        }

        context.Finding(
            "C0-DESCRIPTOR-LIFECYCLE-OWNER",
            DiagnosticSeverity.Blocker,
            "Multiple full-descriptor carriers are accepted",
            "DomainRuntimeContext.SecureCompute and RuntimeBoundaryAdmissionRequest.SecureDescriptor can each supply the full descriptor; no registry-backed opaque binding or unique materialize/revoke owner is proven here.");
        return Task.CompletedTask;
    }
}
