using HybridCPU.ExternalRuntime.Contracts;

namespace YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;

/// <summary>
/// Opt-in provider-neutral bridge for lane-6 DSC. It is deliberately separate
/// from the L7 accelerator backend and carries no descriptor payload, DMA/IOMMU
/// translation state, address, or transport identity across the contract boundary.
/// </summary>
public sealed class DmaStreamComputeExternalOperationBridge
{
    private readonly IExternalOperationProvider _provider;
    private readonly Dictionary<ulong, ExternalOperationState> _operations = new();

    public DmaStreamComputeExternalOperationBridge(IExternalOperationProvider provider) =>
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));

    /// <summary>
    /// Performs exact provider admission and one submission for a DSC token. This
    /// does not perform a local DMA read/write or authorize retire publication.
    /// </summary>
    public DmaStreamComputeExternalOperationResult Begin(
        DmaStreamComputeToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (token.State != DmaStreamComputeTokenState.Admitted || !token.Descriptor.OwnerGuardDecision.IsAllowed)
            return Reject(token, DmaStreamComputeExternalOperationStatus.Rejected,
                "DSC external operation requires an admitted token with a current CPU owner/domain guard.");
        if (_operations.ContainsKey(token.TokenId))
            return Reject(token, DmaStreamComputeExternalOperationStatus.Rejected,
                "DSC external operation token already has a provider lifecycle; duplicate submit is forbidden.");

        ExternalOperationSemanticRequest semantic = CreateSemantic(token.Descriptor);
        ExternalOperationProviderPollResult admission = _provider.Admit(semantic);
        if (admission.Status != ExternalOperationProviderPollStatus.Receipt ||
            admission.Receipt is not ExternalOperationAdmissionReceipt providerAdmission ||
            !Matches(semantic, providerAdmission.Request))
            return Fault(token, DmaStreamComputeExternalOperationStatus.Faulted,
                "DSC provider admission was missing, invalid, or for a different semantic request.");

        ExternalOperationBindingDecision binding = ExternalOperationAdmissionBinding.Evaluate(
            providerAdmission.Request,
            admission.CurrentGenerations,
            new ExternalOperationCpuGuardReceipt(providerAdmission.Request, ExternalCpuGuardStatus.Allowed),
            providerAdmission);
        if (!binding.IsSubmitEligible)
            return Reject(token, binding.Status == ExternalOperationBindingStatus.Stale
                    ? DmaStreamComputeExternalOperationStatus.Stale
                    : DmaStreamComputeExternalOperationStatus.Rejected,
                "DSC provider admission binding rejected or invalidated the exact token operation.");

        ExternalOperationProviderPollResult submitted = _provider.Submit(providerAdmission.Request);
        ExternalOperationStage stage = Accept(ExternalOperationStage.Admitted, providerAdmission.Request, submitted);
        if (stage != ExternalOperationStage.Submitted)
            return Fault(token, stage == ExternalOperationStage.Stale
                    ? DmaStreamComputeExternalOperationStatus.Stale
                    : DmaStreamComputeExternalOperationStatus.Faulted,
                "DSC provider submit acknowledgement was missing, stale, or invalid.");

        token.MarkIssued();
        _operations.Add(token.TokenId, new ExternalOperationState(token, providerAdmission.Request, stage));
        return Result(token, DmaStreamComputeExternalOperationStatus.Submitted, providerAdmission.Request, stage,
            "DSC provider submission acknowledged; completion, visibility, retire publication, and release remain separate.");
    }

    /// <summary>
    /// Consumes one provider lifecycle observation. DeviceComplete intentionally
    /// does not make a DSC token commit-pending; only Visible makes the bridge
    /// eligible for a later staged-output/retire integration.
    /// </summary>
    public DmaStreamComputeExternalOperationResult Poll(ulong tokenId)
    {
        if (!_operations.TryGetValue(tokenId, out ExternalOperationState? state))
            throw new InvalidOperationException("DSC external operation is not known to this bridge.");
        if (state.Token.State is DmaStreamComputeTokenState.Faulted or DmaStreamComputeTokenState.Canceled)
            return Result(state.Token, DmaStreamComputeExternalOperationStatus.Faulted, state.Request, state.Stage,
                "DSC token is terminal; provider lifecycle cannot authorize reuse.");
        if (!state.Token.Descriptor.OwnerGuardDecision.IsAllowed)
            return Fault(state.Token, DmaStreamComputeExternalOperationStatus.Stale,
                "DSC CPU owner/domain guard is no longer current before provider lifecycle reuse.");

        ExternalOperationProviderPollResult observation = _provider.Poll(state.Request);
        if (!state.Request.Generations.Equals(observation.CurrentGenerations) ||
            observation.Status is ExternalOperationProviderPollStatus.Stale or
                ExternalOperationProviderPollStatus.Faulted or
                ExternalOperationProviderPollStatus.Unavailable)
            return Fault(state.Token, DmaStreamComputeExternalOperationStatus.Stale,
                "DSC provider generation changed or provider outcome is unavailable/faulted; no publication is permitted.");
        if (observation.Status == ExternalOperationProviderPollStatus.Pending)
            return Result(state.Token, DmaStreamComputeExternalOperationStatus.Pending, state.Request, state.Stage,
                "DSC provider reports Pending; completion and visibility remain unproven.");

        ExternalOperationStage next = Accept(state.Stage, state.Request, observation);
        if (next is ExternalOperationStage.Failed or ExternalOperationStage.Stale || next == state.Stage)
            return Fault(state.Token, next == ExternalOperationStage.Stale
                    ? DmaStreamComputeExternalOperationStatus.Stale
                    : DmaStreamComputeExternalOperationStatus.Faulted,
                "DSC provider receipt was duplicate, late, cross-operation, or out of sequence.");
        state.Stage = next;
        return next switch
        {
            ExternalOperationStage.DeviceComplete => Result(state.Token, DmaStreamComputeExternalOperationStatus.DeviceComplete,
                state.Request, state.Stage, "DSC device completion observed; output is not yet visible or publishable."),
            ExternalOperationStage.Visible => Result(state.Token, DmaStreamComputeExternalOperationStatus.Visible,
                state.Request, state.Stage, "DSC visibility receipt observed; later staged-output integration must still revalidate before retire."),
            _ => Fault(state.Token, DmaStreamComputeExternalOperationStatus.Faulted,
                "DSC bridge accepts only DeviceComplete then Visible after submission.")
        };
    }

    private static ExternalOperationSemanticRequest CreateSemantic(DmaStreamComputeDescriptor descriptor) =>
        new(ExternalOperationContract.Version,
            new ExternalRequestCorrelation(CorrelationFrom(descriptor.DescriptorIdentityHash, descriptor.NormalizedFootprintHash)),
            ExternalEffectClass.NonIdempotent, ExternalVisibilityRequirement.StagedOutput,
            ExternalCancellationMode.ExactAcknowledgement,
            ExternalReplayEffectClass.StagedReversibleUntilPublish);

    private static Guid CorrelationFrom(ulong descriptorHash, ulong footprintHash)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, descriptorHash);
        BitConverter.TryWriteBytes(bytes[8..], footprintHash);
        return new Guid(bytes);
    }

    private static bool Matches(ExternalOperationSemanticRequest semantic, ExternalOperationRequest request) =>
        request.ContractVersion == semantic.ContractVersion && request.Correlation == semantic.Correlation &&
        request.EffectClass == semantic.EffectClass && request.VisibilityRequirement == semantic.VisibilityRequirement &&
        request.CancellationMode == semantic.CancellationMode;

    private static ExternalOperationStage Accept(ExternalOperationStage current, ExternalOperationRequest request,
        ExternalOperationProviderPollResult observation) =>
        observation.Status == ExternalOperationProviderPollStatus.Receipt
            ? ExternalOperationContract.Accept(current, request, observation.CurrentGenerations, observation.Receipt)
            : ExternalOperationStage.Failed;

    private static DmaStreamComputeExternalOperationResult Reject(DmaStreamComputeToken token,
        DmaStreamComputeExternalOperationStatus status, string message) =>
        Result(token, status, request: null, ExternalOperationStage.Prepared, message);

    private static DmaStreamComputeExternalOperationResult Fault(DmaStreamComputeToken token,
        DmaStreamComputeExternalOperationStatus status, string message)
    {
        if (token.State is not DmaStreamComputeTokenState.Faulted and not DmaStreamComputeTokenState.Canceled)
            token.PublishFault(DmaStreamComputeTokenFaultKind.DmaDeviceFault, message,
                token.Descriptor.DescriptorReference.DescriptorAddress, isWrite: false);
        return Result(token, status, request: null, ExternalOperationStage.Failed, message);
    }

    private static DmaStreamComputeExternalOperationResult Result(DmaStreamComputeToken token,
        DmaStreamComputeExternalOperationStatus status, ExternalOperationRequest? request,
        ExternalOperationStage stage, string message) => new(token, status, request, stage, message);

    private sealed class ExternalOperationState(DmaStreamComputeToken token, ExternalOperationRequest request, ExternalOperationStage stage)
    {
        public DmaStreamComputeToken Token { get; } = token;
        public ExternalOperationRequest Request { get; } = request;
        public ExternalOperationStage Stage { get; set; } = stage;
    }
}

public enum DmaStreamComputeExternalOperationStatus : byte
{
    Rejected = 1,
    Submitted = 2,
    Pending = 3,
    DeviceComplete = 4,
    Visible = 5,
    Stale = 6,
    Faulted = 7
}

/// <summary>Observation only; it never represents DSC retire publication or release.</summary>
public sealed record DmaStreamComputeExternalOperationResult(
    DmaStreamComputeToken Token,
    DmaStreamComputeExternalOperationStatus Status,
    ExternalOperationRequest? Request,
    ExternalOperationStage Stage,
    string Message)
{
    public bool MayPublishArchitecturalMemory => false;
}
