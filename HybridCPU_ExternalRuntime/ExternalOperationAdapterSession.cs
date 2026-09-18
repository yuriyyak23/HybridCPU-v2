using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime;

/// <summary>Runtime-owned classification of a transport-neutral provider call.</summary>
public enum ExternalOperationAdapterStatus : byte
{
    Pending = 1,
    Accepted = 2,
    TransportUnavailable = 3,
    ProviderFault = 4,
    Stale = 5,
    Rejected = 6
}

/// <summary>
/// Narrow runtime adapter over the versioned provider seam. It owns no provider
/// authority: every accepted transition is based on an exact provider receipt.
/// </summary>
public sealed class ExternalOperationAdapterSession
{
    private readonly IExternalOperationProvider provider;
    private readonly object sync = new();
    private sealed class OperationState
    {
        public required ExternalOperationRequest Request { get; init; }
        public ExternalOperationStage Stage { get; set; } = ExternalOperationStage.Admitted;
        public bool SubmitIssued { get; set; }
    }

    private readonly Dictionary<ExternalRequestCorrelation, OperationState> admitted = [];

    public ExternalOperationAdapterSession(IExternalOperationProvider provider) =>
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));

    /// <summary>
    /// Queries only provider-declared semantic capabilities for an exact,
    /// generation-bound host service binding. Missing extension support is
    /// unavailable, never an inferred capability.
    /// </summary>
    public ExternalOperationCapabilityResult QueryCapabilities(ExternalOperationServiceBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (provider is not IExternalOperationCapabilityProvider capabilityProvider)
            return new ExternalOperationCapabilityResult(
                ExternalOperationCapabilityStatus.Unavailable, binding.Generations);
        try
        {
            ExternalOperationCapabilityResult result = capabilityProvider.QueryCapabilities(binding);
            if (!binding.Generations.Equals(result.CurrentGenerations))
                return new ExternalOperationCapabilityResult(
                    ExternalOperationCapabilityStatus.Stale, result.CurrentGenerations);
            return result;
        }
        catch
        {
            return new ExternalOperationCapabilityResult(
                ExternalOperationCapabilityStatus.TransportUnavailable, binding.Generations);
        }
    }

    public ExternalOperationAdapterStatus Admit(ExternalOperationSemanticRequest semantic)
    {
        ArgumentNullException.ThrowIfNull(semantic);
        ExternalOperationProviderPollResult result;
        try { result = provider.Admit(semantic); }
        catch { return ExternalOperationAdapterStatus.TransportUnavailable; }

        if (result.Status == ExternalOperationProviderPollStatus.Unavailable)
            return ExternalOperationAdapterStatus.TransportUnavailable;
        if (result.Status == ExternalOperationProviderPollStatus.Faulted)
            return ExternalOperationAdapterStatus.ProviderFault;
        if (result.Status == ExternalOperationProviderPollStatus.Stale)
            return ExternalOperationAdapterStatus.Stale;
        if (result.Status != ExternalOperationProviderPollStatus.Receipt ||
            result.Receipt is not ExternalOperationAdmissionReceipt receipt ||
            receipt.Outcome != ExternalRuntimeOutcome.Succeeded ||
            !Matches(semantic, receipt.Request) ||
            !receipt.Request.Generations.Equals(result.CurrentGenerations))
            return ExternalOperationAdapterStatus.Rejected;

        lock (sync)
        {
            if (!admitted.TryAdd(semantic.Correlation, new OperationState { Request = receipt.Request }))
                return ExternalOperationAdapterStatus.Rejected;
        }
        return ExternalOperationAdapterStatus.Accepted;
    }

    public ExternalOperationAdapterStatus Submit(ExternalRequestCorrelation correlation)
    {
        OperationState state;
        lock (sync)
        {
            if (!admitted.TryGetValue(correlation, out state!) || state.Stage != ExternalOperationStage.Admitted || state.SubmitIssued)
                return ExternalOperationAdapterStatus.Rejected;
            state.SubmitIssued = true;
        }

        ExternalOperationProviderPollResult result;
        try { result = provider.Submit(state.Request); }
        catch { return ExternalOperationAdapterStatus.TransportUnavailable; }
        return AcceptAndAdvance(state, ExternalOperationStage.Admitted, result);
    }

    public ExternalOperationAdapterStatus Poll(ExternalRequestCorrelation correlation)
    {
        OperationState state;
        lock (sync)
        {
            if (!admitted.TryGetValue(correlation, out state!))
                return ExternalOperationAdapterStatus.Rejected;
            if (state.Stage is ExternalOperationStage.Released or ExternalOperationStage.Failed or ExternalOperationStage.Stale)
                return ExternalOperationAdapterStatus.Rejected;
        }
        try { return AcceptAndAdvance(state, state.Stage, provider.Poll(state.Request)); }
        catch { return ExternalOperationAdapterStatus.TransportUnavailable; }
    }

    /// <summary>
    /// Applies a provider reset/reconfiguration/session-loss observation to one
    /// exact operation. This never proves cancellation, completion, publication,
    /// or release; stale operations require provider-backed resolution.
    /// </summary>
    public ExternalOperationAdapterStatus Invalidate(
        ExternalRequestCorrelation correlation,
        ExternalOperationInvalidationReason reason,
        ExternalGenerationSet currentGenerations)
    {
        if (!Enum.IsDefined(reason)) throw new ArgumentOutOfRangeException(nameof(reason));
        ArgumentNullException.ThrowIfNull(currentGenerations);
        lock (sync)
        {
            if (!admitted.TryGetValue(correlation, out OperationState? state) ||
                state.Stage is ExternalOperationStage.Released or ExternalOperationStage.Failed or ExternalOperationStage.Stale)
                return ExternalOperationAdapterStatus.Rejected;
            state.Stage = ExternalOperationStage.Stale;
            return ExternalOperationAdapterStatus.Stale;
        }
    }

    /// <summary>
    /// Requests cancellation only through an acknowledgement-capable provider.
    /// Unconfirmed cancellation keeps the operation quarantined and never permits
    /// a replacement operation under this correlation.
    /// </summary>
    public ExternalOperationAdapterStatus Cancel(ExternalRequestCorrelation correlation)
    {
        if (provider is not IExternalOperationCancellationProvider cancellationProvider)
            return ExternalOperationAdapterStatus.Rejected;
        OperationState state;
        lock (sync)
        {
            if (!admitted.TryGetValue(correlation, out state!) ||
                state.Stage is ExternalOperationStage.Released or ExternalOperationStage.Failed or ExternalOperationStage.Stale)
                return ExternalOperationAdapterStatus.Rejected;
        }
        ExternalOperationCancellationReceipt receipt;
        try { receipt = cancellationProvider.RequestCancellation(state.Request); }
        catch { return ExternalOperationAdapterStatus.TransportUnavailable; }
        if (receipt.Request != state.Request || !receipt.CurrentGenerations.Equals(state.Request.Generations))
            return ExternalOperationAdapterStatus.Stale;
        if (!receipt.IsConfirmed)
            return receipt.Outcome == ExternalOperationCancellationOutcome.Stale
                ? ExternalOperationAdapterStatus.Stale : ExternalOperationAdapterStatus.Rejected;
        lock (sync)
        {
            if (!ReferenceEquals(admitted.GetValueOrDefault(correlation), state))
                return ExternalOperationAdapterStatus.Rejected;
            admitted.Remove(correlation);
        }
        return ExternalOperationAdapterStatus.Accepted;
    }

    private ExternalOperationAdapterStatus AcceptAndAdvance(
        OperationState state,
        ExternalOperationStage previous,
        ExternalOperationProviderPollResult result)
    {
        if (result.Status == ExternalOperationProviderPollStatus.Pending) return ExternalOperationAdapterStatus.Pending;
        if (result.Status == ExternalOperationProviderPollStatus.Unavailable) return ExternalOperationAdapterStatus.TransportUnavailable;
        if (result.Status == ExternalOperationProviderPollStatus.Faulted) return ExternalOperationAdapterStatus.ProviderFault;
        if (result.Status == ExternalOperationProviderPollStatus.Stale || !state.Request.Generations.Equals(result.CurrentGenerations)) return ExternalOperationAdapterStatus.Stale;
        if (result.Status != ExternalOperationProviderPollStatus.Receipt)
            return ExternalOperationAdapterStatus.Rejected;
        ExternalOperationStage transition = ExternalOperationContract.Accept(
            previous, state.Request, result.CurrentGenerations, result.Receipt);
        if (transition is ExternalOperationStage.Failed or ExternalOperationStage.Stale)
            return ExternalOperationAdapterStatus.Rejected;
        lock (sync)
        {
            if (state.Stage != previous)
                return ExternalOperationAdapterStatus.Rejected;
            state.Stage = transition;
        }
        return ExternalOperationAdapterStatus.Accepted;
    }

    private static bool Matches(ExternalOperationSemanticRequest semantic, ExternalOperationRequest request) =>
        semantic.ContractVersion == request.ContractVersion && semantic.Correlation == request.Correlation &&
        semantic.EffectClass == request.EffectClass && semantic.VisibilityRequirement == request.VisibilityRequirement &&
        semantic.CancellationMode == request.CancellationMode;
}
