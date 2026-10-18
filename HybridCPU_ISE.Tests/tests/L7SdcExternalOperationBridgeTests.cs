using HybridCPU.ExternalRuntime.Contracts;
using HybridCPU_ISE.Tests.TestHelpers;
using Processor = YAKSys_Hybrid_CPU.Processor;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Backends;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcExternalOperationBridgeTests
{
    [Fact]
    public void Bridge_AdmissionReject_FailsClosedWithoutProviderSubmit()
    {
        var provider = new ScriptedProvider(admissionOutcome: ExternalRuntimeOutcome.Denied);
        var runtime = new ExternalAcceleratorRuntime(new Processor.MainMemoryArea(),
            new ProviderNeutralExternalAcceleratorBackend(provider));

        ExternalAcceleratorRuntimeCommandResult result = runtime.Submit(L7SdcTestDescriptorFactory.ParseValidDescriptor());

        Assert.True(result.SubmitAdmission!.IsAccepted);
        Assert.Equal(AcceleratorBackendResultKind.Faulted, result.BackendSubmitResult!.Kind);
        Assert.Equal(0, provider.SubmitCount);
        Assert.Equal(AcceleratorTokenState.Faulted, result.SubmitAdmission.Token!.State);
    }

    [Fact]
    public void Bridge_DelaysDeviceCompleteUntilExactVisibilityReceipt()
    {
        var provider = new ScriptedProvider();
        provider.EnqueuePending();
        provider.EnqueueCompletion();
        provider.EnqueueVisibility();
        var runtime = new ExternalAcceleratorRuntime(new Processor.MainMemoryArea(),
            new ProviderNeutralExternalAcceleratorBackend(provider));

        ExternalAcceleratorRuntimeCommandResult submit = runtime.Submit(L7SdcTestDescriptorFactory.ParseValidDescriptor());
        AcceleratorTokenHandle handle = submit.SubmitAdmission!.Handle;
        Assert.Equal(AcceleratorTokenState.Running, submit.SubmitAdmission.Token!.State);
        Assert.Equal(1, provider.SubmitCount);
        Assert.Equal(AcceleratorBackendResultKind.Pending, submit.BackendTickResult!.Kind);

        ExternalAcceleratorRuntimeCommandResult deviceComplete = runtime.Poll(handle);
        Assert.Equal(AcceleratorBackendResultKind.Pending, deviceComplete.BackendTickResult!.Kind);
        Assert.Equal(AcceleratorTokenState.Running, deviceComplete.TokenLookup!.Token!.State);

        ExternalAcceleratorRuntimeCommandResult visible = runtime.Poll(handle);
        Assert.Equal(AcceleratorBackendResultKind.DeviceCompleted, visible.BackendTickResult!.Kind);
        Assert.Equal(AcceleratorTokenState.DeviceComplete, visible.TokenLookup!.Token!.State);
        Assert.False(visible.TokenLookup.UserVisiblePublicationAllowed);
    }

    [Fact]
    public void Bridge_StaleOrDuplicateProviderReceipt_FaultsAndCannotPublish()
    {
        var provider = new ScriptedProvider();
        provider.EnqueueStale();
        var runtime = new ExternalAcceleratorRuntime(new Processor.MainMemoryArea(),
            new ProviderNeutralExternalAcceleratorBackend(provider));

        ExternalAcceleratorRuntimeCommandResult submit = runtime.Submit(L7SdcTestDescriptorFactory.ParseValidDescriptor());

        Assert.Equal(AcceleratorBackendResultKind.Faulted, submit.BackendTickResult!.Kind);
        Assert.Equal(AcceleratorTokenState.Faulted, submit.SubmitAdmission!.Token!.State);
        Assert.False(submit.SubmitAdmission.Token.UserVisiblePublicationAllowed);
    }

    private sealed class ScriptedProvider : IExternalOperationProvider
    {
        private readonly Queue<Func<ExternalOperationRequest, ExternalOperationProviderPollResult>> _polls = new();
        private readonly ExternalRuntimeOutcome _admissionOutcome;
        private ExternalOperationRequest? _request;

        public ScriptedProvider(ExternalRuntimeOutcome admissionOutcome = ExternalRuntimeOutcome.Succeeded) =>
            _admissionOutcome = admissionOutcome;

        public int SubmitCount { get; private set; }

        public void EnqueuePending() => _polls.Enqueue(request => Pending(request));
        public void EnqueueCompletion() => _polls.Enqueue(request => Receipt(request,
            new ExternalOperationCompletionReceipt(request, ExternalRuntimeOutcome.Succeeded)));
        public void EnqueueVisibility() => _polls.Enqueue(request => Receipt(request,
            new ExternalOperationProgressReceipt(request, ExternalOperationStage.Visible, ExternalRuntimeOutcome.Succeeded)));
        public void EnqueueStale() => _polls.Enqueue(request => new ExternalOperationProviderPollResult(
            ExternalOperationProviderPollStatus.Stale, new ExternalGenerationSet(ExternalOperationContract.Version, [Guid.NewGuid()]),
            new ExternalOperationCompletionReceipt(request, ExternalRuntimeOutcome.Stale)));

        public ExternalOperationProviderPollResult Admit(ExternalOperationSemanticRequest semantic)
        {
            _request = new ExternalOperationRequest(
                new ExternalOperationIdentity(new ExternalOperationHandle(Guid.Parse("A1111111-1111-1111-1111-111111111111")), new ExternalOperationGeneration(1)),
                new ExternalDomainLease(new ExternalDomainLeaseHandle(Guid.Parse("B2222222-2222-2222-2222-222222222222")), new ExternalDomainLeaseEpoch(1)),
                semantic.ContractVersion, new ExternalGenerationSet(semantic.ContractVersion, [Guid.Parse("C3333333-3333-3333-3333-333333333333")]),
                semantic.Correlation, semantic.EffectClass, semantic.VisibilityRequirement, semantic.CancellationMode);
            return Receipt(_request, new ExternalOperationAdmissionReceipt(_request, _admissionOutcome));
        }

        public ExternalOperationProviderPollResult Submit(ExternalOperationRequest request)
        {
            SubmitCount++;
            return Receipt(request, new ExternalOperationProgressReceipt(request, ExternalOperationStage.Submitted, ExternalRuntimeOutcome.Succeeded));
        }

        public ExternalOperationProviderPollResult Poll(ExternalOperationRequest request) =>
            _polls.Count == 0 ? Pending(request) : _polls.Dequeue()(request);

        public ExternalOperationProviderPollResult Cancel(ExternalOperationRequest request) =>
            new(ExternalOperationProviderPollStatus.Unavailable, request.Generations,
                new ExternalOperationCompletionReceipt(request, ExternalRuntimeOutcome.Unknown));

        private static ExternalOperationProviderPollResult Pending(ExternalOperationRequest request) =>
            new(ExternalOperationProviderPollStatus.Pending, request.Generations);
        private static ExternalOperationProviderPollResult Receipt(ExternalOperationRequest request, ExternalOperationReceipt receipt) =>
            new(ExternalOperationProviderPollStatus.Receipt, request.Generations, receipt);
    }
}
