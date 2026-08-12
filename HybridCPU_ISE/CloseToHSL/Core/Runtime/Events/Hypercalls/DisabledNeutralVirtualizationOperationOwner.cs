namespace YAKSys_Hybrid_CPU.Core;

internal enum NeutralVirtualizationOperationOwnerDecision : byte
{
    DeniedOwnerInterfaceDisabled = 0,
    DeniedDecisionArtifactMissing = 1,
}

internal readonly record struct NeutralVirtualizationOperationOwnerRequest(
    string OperationName,
    VirtualizationOperationDecisionValidationResult DecisionValidation);

internal readonly record struct NeutralVirtualizationOperationOwnerResult(
    NeutralVirtualizationOperationOwnerDecision Decision,
    string Reason)
{
    internal bool BackendExecutionAuthorized => false;
    internal bool CompletionPublicationAuthorized => false;
    internal bool RetirePublicationAuthorized => false;
}

/// <summary>
/// Governance-facing placeholder only. It deliberately exposes no execution method.
/// </summary>
internal interface INeutralVirtualizationOperationOwner
{
    NeutralVirtualizationOperationOwnerResult Resolve(
        NeutralVirtualizationOperationOwnerRequest request);
}

internal sealed class DisabledNeutralVirtualizationOperationOwner :
    INeutralVirtualizationOperationOwner
{
    internal static DisabledNeutralVirtualizationOperationOwner Instance { get; } = new();

    private DisabledNeutralVirtualizationOperationOwner()
    {
    }

    public NeutralVirtualizationOperationOwnerResult Resolve(
        NeutralVirtualizationOperationOwnerRequest request) =>
        new(
            request.DecisionValidation.IsStructurallyValidGovernanceEvidence
                ? NeutralVirtualizationOperationOwnerDecision.DeniedOwnerInterfaceDisabled
                : NeutralVirtualizationOperationOwnerDecision.DeniedDecisionArtifactMissing,
            "The neutral operation-owner interface is disabled; D2 evidence cannot execute an operation.");
}
