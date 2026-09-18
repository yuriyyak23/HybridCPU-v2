using HybridCPU.ExternalRuntime.Contracts;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Queues;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Backends;

/// <summary>
/// Opt-in L7 adapter backend. It only translates an already validated descriptor
/// into semantic contract data; the external provider remains the authority for
/// admission and lifecycle receipts. It does not expose or interpret transport
/// identity and it never enables direct coherent architectural output.
/// </summary>
public sealed class ProviderNeutralExternalAcceleratorBackend : IExternalAcceleratorBackend, IExternalAcceleratorBackendPollsExternalOperation, IExternalAcceleratorPublicationEvidenceProvider
{
    private readonly IExternalOperationProvider _provider;
    private readonly IAcceleratorBackendClock _clock;
    private readonly Dictionary<ulong, OperationState> _operations = new();

    public ProviderNeutralExternalAcceleratorBackend(
        IExternalOperationProvider provider,
        IAcceleratorBackendClock? clock = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _clock = clock ?? new ManualAcceleratorBackendClock();
    }

    public AcceleratorBackendResult TrySubmit(
        AcceleratorQueueAdmissionRequest request,
        IAcceleratorCommandQueue queue,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(queue);
        ulong tick = _clock.Advance();
        AcceleratorToken? token = request.TokenAdmission.Token;
        if (!request.TokenAdmission.IsAccepted || token is null || currentGuardEvidence is null)
            return AcceleratorBackendResult.Rejected(AcceleratorTokenFaultCode.SubmitAdmissionRejected,
                "External-operation bridge requires an accepted L7 token and fresh CPU guard evidence.", tick, token);

        ExternalOperationSemanticRequest semantic = CreateSemanticRequest(request.Descriptor);
        ExternalOperationProviderPollResult admission = _provider.Admit(semantic);
        if (admission.Status != ExternalOperationProviderPollStatus.Receipt ||
            admission.Receipt is not ExternalOperationAdmissionReceipt providerAdmission ||
            !MatchesSemantic(providerAdmission.Request, semantic))
            return RejectAndFault(token, currentGuardEvidence, tick,
                "External-operation provider did not return an exact successful admission receipt.");

        ExternalOperationBindingDecision binding = ExternalOperationAdmissionBinding.Evaluate(
            providerAdmission.Request,
            admission.CurrentGenerations,
            new ExternalOperationCpuGuardReceipt(providerAdmission.Request, ExternalCpuGuardStatus.Allowed),
            providerAdmission);
        if (!binding.IsSubmitEligible)
            return RejectAndFault(token, currentGuardEvidence, tick,
                "External-operation admission binding rejected or became stale.");

        AcceleratorQueueAdmissionResult queued = queue.TryEnqueue(request, currentGuardEvidence);
        if (!queued.IsAccepted || queued.Command is null)
            return AcceleratorBackendResult.Rejected(
                queued.FaultCode == AcceleratorTokenFaultCode.None ? AcceleratorTokenFaultCode.QueueAdmissionRejected : queued.FaultCode,
                queued.Message, tick, token, queued);
        if (!queue.TryDequeueReady(currentGuardEvidence, out AcceleratorQueuedCommand? command, out AcceleratorQueueAdmissionResult dequeue) ||
            command is null)
            return AcceleratorBackendResult.Rejected(dequeue.FaultCode, dequeue.Message, tick, token, queued);

        ExternalOperationProviderPollResult submitted = _provider.Submit(providerAdmission.Request);
        ExternalOperationStage stage = Accept(ExternalOperationStage.Admitted, providerAdmission.Request, submitted);
        if (stage != ExternalOperationStage.Submitted)
            return RejectAndFault(token, currentGuardEvidence, tick,
                "External-operation provider submit acknowledgement was missing, stale, or invalid.");

        AcceleratorTokenTransition running = token.MarkRunning(currentGuardEvidence);
        if (running.Rejected)
            return AcceleratorBackendResult.Rejected(running.FaultCode, running.Message, tick, token, queued);

        _operations.Add(token.Handle.Value, new OperationState(token, request.Descriptor, providerAdmission.Request, ExternalOperationStage.Submitted));
        return AcceleratorBackendResult.Submitted(queued, tick);
    }

    public AcceleratorBackendResult Tick(
        IAcceleratorCommandQueue queue,
        IAcceleratorMemoryPortal memoryPortal,
        IAcceleratorStagingBuffer stagingBuffer,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(memoryPortal);
        ArgumentNullException.ThrowIfNull(stagingBuffer);
        ulong tick = _clock.Advance();
        OperationState? state = _operations.Values.FirstOrDefault(static item => !item.Token.IsTerminal && !item.Visible);
        if (state is null)
            return AcceleratorBackendResult.Rejected(AcceleratorTokenFaultCode.BackendRejected,
                "External-operation bridge has no pending L7 operation to poll.", tick);
        if (currentGuardEvidence is null)
            return RejectAndFault(state.Token, null, tick, "External-operation bridge lost CPU guard evidence before provider poll.");

        ExternalOperationProviderPollResult poll = _provider.Poll(state.Request);
        if (!state.Request.Generations.Equals(poll.CurrentGenerations) ||
            poll.Status is ExternalOperationProviderPollStatus.Stale or ExternalOperationProviderPollStatus.Faulted or ExternalOperationProviderPollStatus.Unavailable)
            return RejectAndFault(state.Token, currentGuardEvidence, tick,
                "External-operation provider lifecycle became stale, faulted, or unavailable.");
        if (poll.Status == ExternalOperationProviderPollStatus.Pending)
            return AcceleratorBackendResult.Pending(state.Token, tick,
                "External-operation provider reports pending; DeviceComplete and visibility remain unproven.");

        ExternalOperationStage next = Accept(state.Stage, state.Request, poll);
        if (next is ExternalOperationStage.Failed or ExternalOperationStage.Stale || next == state.Stage)
            return RejectAndFault(state.Token, currentGuardEvidence, tick,
                "External-operation provider receipt was duplicate, late, cross-operation, or out of sequence.");
        state.Stage = next;
        if (poll.Receipt is ExternalOperationCompletionReceipt completion)
            state.Completion = completion;
        if (poll.Receipt is ExternalOperationProgressReceipt visibility && visibility.Stage == ExternalOperationStage.Visible)
            state.Visibility = visibility;
        if (next == ExternalOperationStage.DeviceComplete)
            return AcceleratorBackendResult.Pending(state.Token, tick,
                "External-operation DeviceComplete was observed; visibility and architectural publication remain unproven.");
        if (next != ExternalOperationStage.Visible)
            return RejectAndFault(state.Token, currentGuardEvidence, tick,
                "External-operation bridge accepts only completion then visibility before L7 commit.");

        AcceleratorTokenTransition complete = state.Token.MarkDeviceComplete(currentGuardEvidence);
        if (complete.Rejected)
            return AcceleratorBackendResult.Rejected(complete.FaultCode, complete.Message, tick, state.Token);
        state.Visible = true;
        return AcceleratorBackendResult.DeviceCompleted(state.Token, 0, 0, 0, tick,
            "External-operation visibility receipt was validated; L7 token is DeviceComplete and remains unpublished.");
    }

    public AcceleratorBackendResult TryCancel(AcceleratorTokenStore tokenStore, AcceleratorTokenHandle handle, AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(tokenStore);
        ulong tick = _clock.Advance();
        if (!_operations.TryGetValue(handle.Value, out OperationState? state) || currentGuardEvidence is null)
            return AcceleratorBackendResult.Rejected(AcceleratorTokenFaultCode.CancelRejected,
                "External-operation bridge cannot prove a current cancellable provider operation.", tick);
        ExternalOperationProviderPollResult cancel = _provider.Cancel(state.Request);
        if (cancel.Status != ExternalOperationProviderPollStatus.Receipt || !state.Request.Generations.Equals(cancel.CurrentGenerations))
            return RejectAndFault(state.Token, currentGuardEvidence, tick,
                "External-operation cancellation is ambiguous or stale and cannot prove release.");
        AcceleratorTokenLookupResult lookup = tokenStore.Cancel(handle, currentGuardEvidence);
        return lookup.IsAllowed ? AcceleratorBackendResult.Canceled(lookup, tick) :
            AcceleratorBackendResult.Rejected(lookup.FaultCode, lookup.Message, tick, state.Token, tokenLookupResult: lookup);
    }

    ExternalOperationPublicationEvidence? IExternalAcceleratorPublicationEvidenceProvider.GetPublicationEvidence(
        AcceleratorToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (!_operations.TryGetValue(token.Handle.Value, out OperationState? state) ||
            !ReferenceEquals(state.Token, token) || !state.Visible ||
            state.Completion is null || state.Visibility is null)
            return null;

        // This is deliberately a provider call outside any token-store lock.
        // A pending response is the provider's explicit current-state answer;
        // any new receipt, failure, unavailable result, or generation drift is
        // ambiguous at publication time and therefore cannot be used.
        ExternalOperationProviderPollResult revalidation = _provider.Poll(state.Request);
        if (revalidation.Status != ExternalOperationProviderPollStatus.Pending ||
            !state.Request.Generations.Equals(revalidation.CurrentGenerations))
            return null;

        // The CPU still applies the pure gate before its staged publication.
        return new ExternalOperationPublicationEvidence(
            state.Request,
            state.Request.Generations,
            state.Completion,
            state.Visibility);
    }

    private static ExternalOperationSemanticRequest CreateSemanticRequest(AcceleratorCommandDescriptor descriptor)
    {
        Guid correlation = CorrelationFrom(descriptor.Identity);
        return new ExternalOperationSemanticRequest(ExternalOperationContract.Version,
            new ExternalRequestCorrelation(correlation), ExternalEffectClass.NonIdempotent,
            ExternalVisibilityRequirement.StagedOutput, ExternalCancellationMode.ExactAcknowledgement,
            ExternalReplayEffectClass.StagedReversibleUntilPublish);
    }

    private static Guid CorrelationFrom(AcceleratorDescriptorIdentity identity)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, identity.DescriptorIdentityHash);
        BitConverter.TryWriteBytes(bytes[8..], identity.NormalizedFootprintHash);
        return new Guid(bytes);
    }

    private static bool MatchesSemantic(ExternalOperationRequest request, ExternalOperationSemanticRequest semantic) =>
        request.ContractVersion == semantic.ContractVersion && request.Correlation == semantic.Correlation &&
        request.EffectClass == semantic.EffectClass && request.VisibilityRequirement == semantic.VisibilityRequirement &&
        request.CancellationMode == semantic.CancellationMode;

    private static ExternalOperationStage Accept(ExternalOperationStage current, ExternalOperationRequest request, ExternalOperationProviderPollResult result) =>
        result.Status == ExternalOperationProviderPollStatus.Receipt
            ? ExternalOperationContract.Accept(current, request, result.CurrentGenerations, result.Receipt)
            : ExternalOperationStage.Failed;

    private static AcceleratorBackendResult RejectAndFault(AcceleratorToken token, AcceleratorGuardEvidence? evidence, ulong tick, string message)
    {
        if (evidence is not null)
            token.MarkFaulted(AcceleratorTokenFaultCode.BackendRejected, evidence);
        return AcceleratorBackendResult.Faulted(token, AcceleratorTokenFaultCode.BackendRejected, message, tick);
    }

    private sealed class OperationState(AcceleratorToken token, AcceleratorCommandDescriptor descriptor, ExternalOperationRequest request, ExternalOperationStage stage)
    {
        public AcceleratorToken Token { get; } = token;
        public AcceleratorCommandDescriptor Descriptor { get; } = descriptor;
        public ExternalOperationRequest Request { get; } = request;
        public ExternalOperationStage Stage { get; set; } = stage;
        public bool Visible { get; set; }
        public ExternalOperationCompletionReceipt? Completion { get; set; }
        public ExternalOperationProgressReceipt? Visibility { get; set; }
    }
}
