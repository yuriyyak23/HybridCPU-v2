using HybridCPU.ExternalRuntime.Contracts;
using HybridCPU_ISE.Tests.TestHelpers;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class DmaStreamComputeExternalOperationBridgeTests
{
    [Fact]
    public void Bridge_RequiresExactProviderAdmissionAndPreventsDuplicateSubmit()
    {
        var provider = new ScriptedDscProvider(ExternalRuntimeOutcome.Denied);
        var token = NewToken();
        var bridge = new DmaStreamComputeExternalOperationBridge(provider);

        DmaStreamComputeExternalOperationResult denied = bridge.Begin(token);

        Assert.Equal(DmaStreamComputeExternalOperationStatus.Rejected, denied.Status);
        Assert.Equal(DmaStreamComputeTokenState.Admitted, token.State);
        Assert.Equal(0, provider.SubmitCount);

        var successProvider = new ScriptedDscProvider();
        var successToken = NewToken();
        var successBridge = new DmaStreamComputeExternalOperationBridge(successProvider);
        Assert.Equal(DmaStreamComputeExternalOperationStatus.Submitted, successBridge.Begin(successToken).Status);
        Assert.Equal(DmaStreamComputeExternalOperationStatus.Rejected, successBridge.Begin(successToken).Status);
        Assert.Equal(1, successProvider.SubmitCount);
    }

    [Fact]
    public void Bridge_DistinguishesPendingDeviceCompleteAndVisible_WithoutRetirePublication()
    {
        var provider = new ScriptedDscProvider();
        provider.EnqueuePending();
        provider.EnqueueCompletion();
        provider.EnqueueVisibility();
        var token = NewToken();
        var bridge = new DmaStreamComputeExternalOperationBridge(provider);

        Assert.Equal(DmaStreamComputeExternalOperationStatus.Submitted, bridge.Begin(token).Status);
        Assert.Equal(DmaStreamComputeExternalOperationStatus.Pending, bridge.Poll(token.TokenId).Status);
        DmaStreamComputeExternalOperationResult completed = bridge.Poll(token.TokenId);
        Assert.Equal(DmaStreamComputeExternalOperationStatus.DeviceComplete, completed.Status);
        Assert.False(completed.MayPublishArchitecturalMemory);
        Assert.Equal(DmaStreamComputeTokenState.Issued, token.State);
        DmaStreamComputeExternalOperationResult visible = bridge.Poll(token.TokenId);
        Assert.Equal(DmaStreamComputeExternalOperationStatus.Visible, visible.Status);
        Assert.False(visible.MayPublishArchitecturalMemory);
        Assert.Equal(DmaStreamComputeTokenState.Issued, token.State);
    }

    [Fact]
    public void Bridge_StaleGenerationFaultsTokenAndCannotBecomeCommitPending()
    {
        var provider = new ScriptedDscProvider();
        provider.EnqueueStale();
        var token = NewToken();
        var bridge = new DmaStreamComputeExternalOperationBridge(provider);

        bridge.Begin(token);
        DmaStreamComputeExternalOperationResult result = bridge.Poll(token.TokenId);

        Assert.Equal(DmaStreamComputeExternalOperationStatus.Stale, result.Status);
        Assert.Equal(DmaStreamComputeTokenState.Faulted, token.State);
        Assert.NotEqual(DmaStreamComputeTokenState.CommitPending, token.State);
    }

    private static DmaStreamComputeToken NewToken() =>
        new(DmaStreamComputeTestDescriptorFactory.CreateDescriptor(), tokenId: 0xD5C);

    private sealed class ScriptedDscProvider : IExternalOperationProvider
    {
        private readonly ExternalRuntimeOutcome _admissionOutcome;
        private readonly Queue<Func<ExternalOperationRequest, ExternalOperationProviderPollResult>> _polls = new();

        public ScriptedDscProvider(ExternalRuntimeOutcome admissionOutcome = ExternalRuntimeOutcome.Succeeded) =>
            _admissionOutcome = admissionOutcome;
        public int SubmitCount { get; private set; }

        public void EnqueuePending() => _polls.Enqueue(request => new(
            ExternalOperationProviderPollStatus.Pending, request.Generations));
        public void EnqueueCompletion() => _polls.Enqueue(request => Receipt(request,
            new ExternalOperationCompletionReceipt(request, ExternalRuntimeOutcome.Succeeded)));
        public void EnqueueVisibility() => _polls.Enqueue(request => Receipt(request,
            new ExternalOperationProgressReceipt(request, ExternalOperationStage.Visible, ExternalRuntimeOutcome.Succeeded)));
        public void EnqueueStale() => _polls.Enqueue(request => new(
            ExternalOperationProviderPollStatus.Stale,
            new ExternalGenerationSet(ExternalOperationContract.Version, [Guid.NewGuid()]),
            new ExternalOperationCompletionReceipt(request, ExternalRuntimeOutcome.Stale)));

        public ExternalOperationProviderPollResult Admit(ExternalOperationSemanticRequest semantic)
        {
            var request = new ExternalOperationRequest(
                new ExternalOperationIdentity(new ExternalOperationHandle(Guid.Parse("D4444444-4444-4444-4444-444444444444")), new ExternalOperationGeneration(1)),
                new ExternalDomainLease(new ExternalDomainLeaseHandle(Guid.Parse("E5555555-5555-5555-5555-555555555555")), new ExternalDomainLeaseEpoch(1)),
                semantic.ContractVersion, new ExternalGenerationSet(semantic.ContractVersion, [Guid.Parse("F6666666-6666-6666-6666-666666666666")]),
                semantic.Correlation, semantic.EffectClass, semantic.VisibilityRequirement, semantic.CancellationMode);
            return Receipt(request, new ExternalOperationAdmissionReceipt(request, _admissionOutcome));
        }

        public ExternalOperationProviderPollResult Submit(ExternalOperationRequest request)
        {
            SubmitCount++;
            return Receipt(request, new ExternalOperationProgressReceipt(request,
                ExternalOperationStage.Submitted, ExternalRuntimeOutcome.Succeeded));
        }

        public ExternalOperationProviderPollResult Poll(ExternalOperationRequest request) =>
            _polls.Count == 0 ? new(ExternalOperationProviderPollStatus.Pending, request.Generations) : _polls.Dequeue()(request);

        public ExternalOperationProviderPollResult Cancel(ExternalOperationRequest request) => new(
            ExternalOperationProviderPollStatus.Unavailable, request.Generations,
            new ExternalOperationCompletionReceipt(request, ExternalRuntimeOutcome.Unknown));

        private static ExternalOperationProviderPollResult Receipt(ExternalOperationRequest request,
            ExternalOperationReceipt receipt) => new(ExternalOperationProviderPollStatus.Receipt, request.Generations, receipt);
    }
}
