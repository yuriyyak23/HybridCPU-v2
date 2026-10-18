namespace YAKSys_Hybrid_CPU.Core;

internal enum VirtualizationRepositoryOwnerReviewDecision : byte
{
    DeniedCodeOwnersAttributionAbsent = 0,
    DeniedWorkflowDisabled = 1,
}

internal readonly record struct VirtualizationRepositoryOwnerReviewRequest(
    string DecisionId,
    VirtualizationDecisionAttributionEvidence Attribution);

internal readonly record struct VirtualizationRepositoryOwnerReviewResult(
    VirtualizationRepositoryOwnerReviewDecision Decision,
    string Reason)
{
    internal bool OwnerAppointmentAuthorized => false;
    internal bool DecisionAcceptanceAuthorized => false;
    internal bool BackendExecutionAuthorized => false;
}

/// <summary>
/// Repository-owner review preparation only. This workflow has no approve,
/// accept, appoint or execution operation and always remains disabled.
/// </summary>
internal sealed class DisabledVirtualizationRepositoryOwnerReviewWorkflow
{
    internal static DisabledVirtualizationRepositoryOwnerReviewWorkflow Instance { get; } = new();

    private DisabledVirtualizationRepositoryOwnerReviewWorkflow()
    {
    }

    internal VirtualizationRepositoryOwnerReviewResult Evaluate(
        VirtualizationRepositoryOwnerReviewRequest request)
    {
        if (!request.Attribution.CodeOwnersRulePresent ||
            !request.Attribution.CodeOwnersRuleMatched)
            return new(
                VirtualizationRepositoryOwnerReviewDecision.DeniedCodeOwnersAttributionAbsent,
                "Repository-owner review is unavailable because matching CODEOWNERS attribution is absent.");

        return new(
            VirtualizationRepositoryOwnerReviewDecision.DeniedWorkflowDisabled,
            "Repository-owner review workflow preparation is disabled and cannot appoint an owner or accept D2.");
    }
}
