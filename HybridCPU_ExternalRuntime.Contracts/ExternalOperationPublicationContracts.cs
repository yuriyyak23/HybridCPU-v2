namespace HybridCPU.ExternalRuntime.Contracts;

/// <summary>
/// Immutable proof bundle required before a CPU-owned staged publication. It is
/// not a publication receipt, does not authenticate a provider, and cannot
/// authorise direct output. The caller must obtain every receipt from the
/// provider and pass the current opaque generation snapshot unchanged.
/// </summary>
public sealed record ExternalOperationPublicationEvidence
{
    public ExternalOperationPublicationEvidence(
        ExternalOperationRequest request,
        ExternalGenerationSet currentGenerations,
        ExternalOperationCompletionReceipt completion,
        ExternalOperationProgressReceipt visibility)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(currentGenerations);
        ArgumentNullException.ThrowIfNull(completion);
        ArgumentNullException.ThrowIfNull(visibility);
        if (visibility.Stage != ExternalOperationStage.Visible)
            throw new ArgumentException("Visibility receipt required.", nameof(visibility));

        Request = request;
        CurrentGenerations = currentGenerations;
        Completion = completion;
        Visibility = visibility;
    }

    public ExternalOperationRequest Request { get; }
    public ExternalGenerationSet CurrentGenerations { get; }
    public ExternalOperationCompletionReceipt Completion { get; }
    public ExternalOperationProgressReceipt Visibility { get; }
}

public enum ExternalOperationPublicationEligibility : byte
{
    Eligible = 1,
    Rejected = 2,
    Stale = 3
}

/// <summary>Pure fail-closed validation for CPU-side staged publication.</summary>
public static class ExternalOperationPublicationGate
{
    public static ExternalOperationPublicationEligibility Evaluate(
        ExternalOperationPublicationEvidence? evidence)
    {
        if (evidence is null) return ExternalOperationPublicationEligibility.Rejected;
        ExternalOperationRequest request = evidence.Request;
        if (!request.Generations.Equals(evidence.CurrentGenerations))
            return ExternalOperationPublicationEligibility.Stale;
        if (ExternalOperationContract.Accept(ExternalOperationStage.Submitted, request,
                evidence.CurrentGenerations, evidence.Completion) != ExternalOperationStage.DeviceComplete)
            return ExternalOperationPublicationEligibility.Rejected;
        if (ExternalOperationContract.Accept(ExternalOperationStage.DeviceComplete, request,
                evidence.CurrentGenerations, evidence.Visibility) != ExternalOperationStage.Visible)
            return ExternalOperationPublicationEligibility.Rejected;
        return request.VisibilityRequirement == ExternalVisibilityRequirement.StagedOutput &&
               request.EffectClass != ExternalEffectClass.ReadOnly
            ? ExternalOperationPublicationEligibility.Eligible
            : ExternalOperationPublicationEligibility.Rejected;
    }
}
