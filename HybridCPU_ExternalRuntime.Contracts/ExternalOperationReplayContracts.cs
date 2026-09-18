namespace HybridCPU.ExternalRuntime.Contracts;

/// <summary>
/// Replay-policy classification supplied by the provider-neutral operation contract.
/// It is a policy input, not a provider permission or a rollback promise.
/// </summary>
public enum ExternalReplayEffectClass : byte
{
    StagedReversibleUntilPublish = 1,
    SnapshotOrIdempotenceRequired = 2,
    IrreversibleBarrier = 3
}

/// <summary>
/// Semantic invalidations that make a cached external replay decision unusable.
/// No value describes provider topology or hardware routing.
/// </summary>
public enum ExternalOperationReplayInvalidationReason : byte
{
    None = 0,
    OwnerOrDomainChanged = 1,
    GenerationChanged = 2,
    DescriptorChanged = 3,
    ProviderCapabilityChanged = 4,
    CancellationOrFault = 5
}

public enum ExternalOperationReplayAction : byte
{
    RequireFreshAdmission = 1,
    AwaitOriginalOperation = 2,
    CancelThenReadmit = 3,
    ContinueOriginalLifecycle = 4,
    ReplayBarrier = 5,
    ReAdmissionRequired = 6
}

/// <summary>
/// A provider-neutral replay cache identity. It has no address, topology, or
/// provider-private component and must be paired with current generation checks.
/// </summary>
public readonly record struct ExternalOperationReplayKey(
    ExternalOperationIdentity Operation,
    ExternalRequestCorrelation Correlation,
    ExternalReplayEffectClass EffectClass,
    ExternalGenerationSet Generations)
{
    public static ExternalOperationReplayKey From(
        ExternalOperationRequest request,
        ExternalReplayEffectClass effectClass)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(effectClass))
            throw new ArgumentOutOfRangeException(nameof(effectClass));

        return new(request.Operation, request.Correlation, effectClass, request.Generations);
    }
}

/// <summary>
/// Immutable replay-relevant state for one external operation. It records the
/// observed lifecycle only; it does not claim rollback of a provider effect.
/// </summary>
public sealed record ExternalOperationReplaySnapshot
{
    public ExternalOperationReplaySnapshot(
        ExternalOperationRequest request,
        ExternalReplayEffectClass effectClass,
        ExternalOperationStage observedStage,
        ExternalOperationReplayInvalidationReason invalidationReason)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(effectClass))
            throw new ArgumentOutOfRangeException(nameof(effectClass));
        if (!Enum.IsDefined(observedStage))
            throw new ArgumentOutOfRangeException(nameof(observedStage));
        if (!Enum.IsDefined(invalidationReason))
            throw new ArgumentOutOfRangeException(nameof(invalidationReason));

        Request = request;
        EffectClass = effectClass;
        ObservedStage = observedStage;
        InvalidationReason = invalidationReason;
        Key = ExternalOperationReplayKey.From(request, effectClass);
    }

    public ExternalOperationRequest Request { get; }

    public ExternalReplayEffectClass EffectClass { get; }

    public ExternalOperationStage ObservedStage { get; }

    public ExternalOperationReplayInvalidationReason InvalidationReason { get; }

    public ExternalOperationReplayKey Key { get; }
}

/// <summary>
/// Result of replay-policy evaluation. No action directly authorizes provider
/// submission; fresh CPU guards and provider admission remain mandatory.
/// </summary>
public sealed record ExternalOperationReplayDecision
{
    internal ExternalOperationReplayDecision(
        ExternalOperationReplayAction action,
        ExternalOperationReplayInvalidationReason invalidationReason)
    {
        Action = action;
        InvalidationReason = invalidationReason;
    }

    public ExternalOperationReplayAction Action { get; }

    public ExternalOperationReplayInvalidationReason InvalidationReason { get; }

    public bool AllowsDirectSubmit => false;
}

/// <summary>
/// Fail-closed external replay policy. Replay evidence is intentionally not an
/// input: it cannot authorize a duplicate external effect.
/// </summary>
public static class ExternalOperationReplayPolicy
{
    public static ExternalOperationReplayDecision Evaluate(
        ExternalOperationReplaySnapshot snapshot,
        ExternalGenerationSet? currentGenerations)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.Request.Generations.Equals(currentGenerations))
            return DecisionForInvalidation(
                snapshot,
                ExternalOperationReplayInvalidationReason.GenerationChanged);

        if (snapshot.InvalidationReason != ExternalOperationReplayInvalidationReason.None)
            return DecisionForInvalidation(snapshot, snapshot.InvalidationReason);

        return snapshot.ObservedStage switch
        {
            ExternalOperationStage.Prepared or ExternalOperationStage.Admitted =>
                Decision(ExternalOperationReplayAction.RequireFreshAdmission, ExternalOperationReplayInvalidationReason.None),
            ExternalOperationStage.Submitted => DecisionForSubmitted(snapshot),
            ExternalOperationStage.DeviceComplete or ExternalOperationStage.Visible => DecisionForCompletion(snapshot),
            ExternalOperationStage.Published or ExternalOperationStage.Released =>
                Decision(ExternalOperationReplayAction.ReplayBarrier, ExternalOperationReplayInvalidationReason.None),
            ExternalOperationStage.Failed or ExternalOperationStage.Stale =>
                Decision(ExternalOperationReplayAction.ReAdmissionRequired, ExternalOperationReplayInvalidationReason.None),
            _ => Decision(ExternalOperationReplayAction.ReplayBarrier, ExternalOperationReplayInvalidationReason.None)
        };
    }

    private static ExternalOperationReplayDecision DecisionForSubmitted(ExternalOperationReplaySnapshot snapshot)
    {
        if (IsDirectCoherentWrite(snapshot) || snapshot.EffectClass == ExternalReplayEffectClass.IrreversibleBarrier)
            return Decision(ExternalOperationReplayAction.ReplayBarrier, ExternalOperationReplayInvalidationReason.None);

        return snapshot.Request.CancellationMode == ExternalCancellationMode.ExactAcknowledgement
            ? Decision(ExternalOperationReplayAction.CancelThenReadmit, ExternalOperationReplayInvalidationReason.None)
            : Decision(ExternalOperationReplayAction.AwaitOriginalOperation, ExternalOperationReplayInvalidationReason.None);
    }

    private static ExternalOperationReplayDecision DecisionForCompletion(ExternalOperationReplaySnapshot snapshot) =>
        IsDirectCoherentWrite(snapshot) || snapshot.EffectClass == ExternalReplayEffectClass.IrreversibleBarrier
            ? Decision(ExternalOperationReplayAction.ReplayBarrier, ExternalOperationReplayInvalidationReason.None)
            : Decision(ExternalOperationReplayAction.ContinueOriginalLifecycle, ExternalOperationReplayInvalidationReason.None);

    private static ExternalOperationReplayDecision DecisionForInvalidation(
        ExternalOperationReplaySnapshot snapshot,
        ExternalOperationReplayInvalidationReason invalidationReason)
    {
        if (snapshot.ObservedStage is ExternalOperationStage.Prepared or ExternalOperationStage.Admitted or
            ExternalOperationStage.Failed or ExternalOperationStage.Stale)
        {
            return Decision(ExternalOperationReplayAction.ReAdmissionRequired, invalidationReason);
        }

        return Decision(ExternalOperationReplayAction.ReplayBarrier, invalidationReason);
    }

    private static bool IsDirectCoherentWrite(ExternalOperationReplaySnapshot snapshot) =>
        snapshot.Request.VisibilityRequirement == ExternalVisibilityRequirement.Coherent;

    private static ExternalOperationReplayDecision Decision(
        ExternalOperationReplayAction action,
        ExternalOperationReplayInvalidationReason invalidationReason) =>
        new(action, invalidationReason);
}
