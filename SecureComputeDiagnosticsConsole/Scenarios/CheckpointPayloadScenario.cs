using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.SecureComputeDiagnostics;

internal sealed class CheckpointPayloadScenario : ISecureComputeScenario
{
    public string Id => "checkpoint-payload";
    public string Description => "Classifies forbidden and unknown checkpoint payload classes.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.PolicyClassifier;
    public string AuthorityCeiling => "Payload classification only; no checkpoint owner, manifest, replay or restore protocol proof.";

    public Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        SecureCheckpointPayloadPolicy policy = SecureCheckpointPayloadPolicy.FailClosed;
        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SecureCheckpointPayloadDecision host = policy.Classify(SecureCheckpointPayloadClass.HostOwnedEvidence);
            SecureCheckpointPayloadDecision pointer = policy.Classify(SecureCheckpointPayloadClass.ActiveHostPointer);
            SecureCheckpointPayloadDecision unknown = policy.Classify((SecureCheckpointPayloadClass)byte.MaxValue);
            context.Check(host != SecureCheckpointPayloadDecision.Allowed, "host-owned evidence is denied");
            context.Check(pointer != SecureCheckpointPayloadDecision.Allowed, "active host pointer is denied");
            context.Count("known_forbidden_payloads_denied", 2);
            context.Count(unknown == SecureCheckpointPayloadDecision.Allowed
                ? "unknown_payload_classes_allowed"
                : "unknown_payload_classes_denied");
            context.Trace("checkpoint-classification", ("unknownDecision", unknown));
            context.CompleteIteration("Checkpoint payload classification completed.");
        }

        if (policy.Classify((SecureCheckpointPayloadClass)byte.MaxValue) == SecureCheckpointPayloadDecision.Allowed)
        {
            context.Finding(
                "C1-MIGRATION-UNKNOWN-DEFAULT",
                DiagnosticSeverity.Blocker,
                "Unknown checkpoint payload defaults to Allowed",
                "The switch fallback accepts an undefined payload class. Migration and restore payload classes require an explicit default-deny decision.");
        }
        return Task.CompletedTask;
    }
}
