using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime.Tests;

public sealed class ExternalOperationAdapterSessionTests
{
    private static readonly Guid Generation = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void FakeProvider_RequiresExactLifecycleAndSingleSubmit()
    {
        ExternalOperationRequest request = Request();
        var provider = new FakeProvider(request);
        provider.PollReceipts.Enqueue(new ExternalOperationCompletionReceipt(request, ExternalRuntimeOutcome.Succeeded));
        provider.PollReceipts.Enqueue(new ExternalOperationProgressReceipt(request, ExternalOperationStage.Visible, ExternalRuntimeOutcome.Succeeded));
        provider.PollReceipts.Enqueue(new ExternalOperationPublicationReceipt(request, ExternalRuntimeOutcome.Succeeded));
        provider.PollReceipts.Enqueue(new ExternalOperationReleaseReceipt(request, ExternalRuntimeOutcome.Closed));
        var adapter = new ExternalOperationAdapterSession(provider);
        ExternalOperationSemanticRequest semantic = Semantic(request);

        Assert.Equal(ExternalOperationAdapterStatus.Accepted, adapter.Admit(semantic));
        Assert.Equal(ExternalOperationAdapterStatus.Accepted, adapter.Submit(request.Correlation));
        Assert.Equal(ExternalOperationAdapterStatus.Rejected, adapter.Submit(request.Correlation));
        Assert.Equal(1, provider.SubmitCount);
        Assert.Equal(ExternalOperationAdapterStatus.Accepted, adapter.Poll(request.Correlation));
        Assert.Equal(ExternalOperationAdapterStatus.Accepted, adapter.Poll(request.Correlation));
        Assert.Equal(ExternalOperationAdapterStatus.Accepted, adapter.Poll(request.Correlation));
        Assert.Equal(ExternalOperationAdapterStatus.Accepted, adapter.Poll(request.Correlation));
        Assert.Equal(ExternalOperationAdapterStatus.Rejected, adapter.Poll(request.Correlation));
    }

    [Fact]
    public void StaleCrossOperationAndTransportFailure_FailClosed()
    {
        ExternalOperationRequest request = Request();
        ExternalOperationRequest cross = Request(correlation: new ExternalRequestCorrelation(Guid.NewGuid()));
        var crossProvider = new FakeProvider(request) { AdmissionReceipt = new ExternalOperationAdmissionReceipt(cross, ExternalRuntimeOutcome.Succeeded) };
        Assert.Equal(ExternalOperationAdapterStatus.Rejected, new ExternalOperationAdapterSession(crossProvider).Admit(Semantic(request)));

        var staleProvider = new FakeProvider(request) { SubmitGenerations = Snapshot(Guid.NewGuid()) };
        var staleAdapter = new ExternalOperationAdapterSession(staleProvider);
        Assert.Equal(ExternalOperationAdapterStatus.Accepted, staleAdapter.Admit(Semantic(request)));
        Assert.Equal(ExternalOperationAdapterStatus.Stale, staleAdapter.Submit(request.Correlation));

        var unavailableProvider = new FakeProvider(request) { ThrowOnSubmit = true };
        var unavailableAdapter = new ExternalOperationAdapterSession(unavailableProvider);
        Assert.Equal(ExternalOperationAdapterStatus.Accepted, unavailableAdapter.Admit(Semantic(request)));
        Assert.Equal(ExternalOperationAdapterStatus.TransportUnavailable, unavailableAdapter.Submit(request.Correlation));
        Assert.Equal(ExternalOperationAdapterStatus.Rejected, unavailableAdapter.Submit(request.Correlation));
    }

    [Fact]
    public void CapabilityQuery_RequiresExactHostBindingAndCurrentGeneration()
    {
        ExternalOperationRequest request = Request();
        var provider = new FakeProvider(request)
        {
            CapabilityResult = new ExternalOperationCapabilityResult(
                ExternalOperationCapabilityStatus.Available,
                request.Generations,
                ExternalOperationCapability.ExternalAcceleratorService |
                ExternalOperationCapability.StagedPublication |
                ExternalOperationCapability.Cancellation)
        };
        var adapter = new ExternalOperationAdapterSession(provider);
        var binding = new ExternalOperationServiceBinding(
            ExternalOperationContract.Version,
            new ExternalOperationServiceHandle(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee")),
            request.Scope,
            request.Generations);

        ExternalOperationCapabilityResult result = adapter.QueryCapabilities(binding);
        Assert.Equal(ExternalOperationCapabilityStatus.Available, result.Status);
        Assert.True(result.Capabilities.HasFlag(ExternalOperationCapability.StagedPublication));

        provider.CapabilityResult = new ExternalOperationCapabilityResult(
            ExternalOperationCapabilityStatus.Available, Snapshot(Guid.NewGuid()),
            ExternalOperationCapability.ExternalAcceleratorService);
        Assert.Equal(ExternalOperationCapabilityStatus.Stale, adapter.QueryCapabilities(binding).Status);
        Assert.Throws<ArgumentException>(() => new ExternalOperationServiceBinding(
            ExternalOperationContract.Version, default, request.Scope, request.Generations));
    }

    [Fact]
    public void TransportLossDuringAdmission_RetainsCorrelationTombstone()
    {
        ExternalOperationRequest request = Request();
        var provider = new FakeProvider(request) { ThrowOnAdmit = true };
        var adapter = new ExternalOperationAdapterSession(provider);

        Assert.Equal(ExternalOperationAdapterStatus.TransportUnavailable, adapter.Admit(Semantic(request)));
        Assert.Equal(ExternalOperationAdapterStatus.Rejected, adapter.Submit(request.Correlation));

        provider.ThrowOnAdmit = false;
        Assert.Equal(ExternalOperationAdapterStatus.Rejected, adapter.Admit(Semantic(request)));
        Assert.Equal(1, provider.AdmitCount);
        Assert.Equal(0, provider.SubmitCount);
    }

    [Fact]
    public async Task ConcurrentAdmission_ReservesCorrelationBeforeProviderCallback()
    {
        ExternalOperationRequest request = Request();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var provider = new FakeProvider(request) { AdmissionEffect = () => { entered.Set(); release.Wait(); } };
        var adapter = new ExternalOperationAdapterSession(provider);
        Task<ExternalOperationAdapterStatus> first = Task.Run(() => adapter.Admit(Semantic(request)));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(ExternalOperationAdapterStatus.Rejected, adapter.Admit(Semantic(request)));
        }
        finally { release.Set(); }
        Assert.Equal(ExternalOperationAdapterStatus.Accepted, await first);
        Assert.Equal(1, provider.AdmitCount);
    }

    [Theory]
    [InlineData(ExternalOperationInvalidationReason.DeviceReset)]
    [InlineData(ExternalOperationInvalidationReason.GenerationChanged)]
    [InlineData(ExternalOperationInvalidationReason.DomainOrMappingChanged)]
    [InlineData(ExternalOperationInvalidationReason.ServiceReconfigured)]
    [InlineData(ExternalOperationInvalidationReason.TransportSessionLost)]
    public void InvalidationBeforeOrAfterSubmit_IsStaleAndNeverAllowsReplacement(
        ExternalOperationInvalidationReason reason)
    {
        ExternalOperationRequest request = Request();
        var beforeSubmit = new ExternalOperationAdapterSession(new FakeProvider(request));
        Assert.Equal(ExternalOperationAdapterStatus.Accepted, beforeSubmit.Admit(Semantic(request)));
        Assert.Equal(ExternalOperationAdapterStatus.Stale, beforeSubmit.Invalidate(request.Correlation, reason, Snapshot(Guid.NewGuid())));
        Assert.Equal(ExternalOperationAdapterStatus.Rejected, beforeSubmit.Submit(request.Correlation));

        var afterSubmit = new ExternalOperationAdapterSession(new FakeProvider(request));
        Assert.Equal(ExternalOperationAdapterStatus.Accepted, afterSubmit.Admit(Semantic(request)));
        Assert.Equal(ExternalOperationAdapterStatus.Accepted, afterSubmit.Submit(request.Correlation));
        Assert.Equal(ExternalOperationAdapterStatus.Stale, afterSubmit.Invalidate(request.Correlation, reason, Snapshot(Guid.NewGuid())));
        Assert.Equal(ExternalOperationAdapterStatus.Rejected, afterSubmit.Poll(request.Correlation));
        Assert.Equal(ExternalOperationAdapterStatus.Rejected, afterSubmit.Submit(request.Correlation));
    }

    [Fact]
    public void InvalidationAfterDeviceComplete_BlocksVisibilityPublicationAndReleaseProgress()
    {
        ExternalOperationRequest request = Request();
        var provider = new FakeProvider(request);
        provider.PollReceipts.Enqueue(new ExternalOperationCompletionReceipt(request, ExternalRuntimeOutcome.Succeeded));
        provider.PollReceipts.Enqueue(new ExternalOperationProgressReceipt(request, ExternalOperationStage.Visible, ExternalRuntimeOutcome.Succeeded));
        var adapter = new ExternalOperationAdapterSession(provider);
        Assert.Equal(ExternalOperationAdapterStatus.Accepted, adapter.Admit(Semantic(request)));
        Assert.Equal(ExternalOperationAdapterStatus.Accepted, adapter.Submit(request.Correlation));
        Assert.Equal(ExternalOperationAdapterStatus.Accepted, adapter.Poll(request.Correlation));
        Assert.Equal(ExternalOperationAdapterStatus.Stale, adapter.Invalidate(
            request.Correlation, ExternalOperationInvalidationReason.GenerationChanged, Snapshot(Guid.NewGuid())));
        Assert.Equal(ExternalOperationAdapterStatus.Rejected, adapter.Poll(request.Correlation));
    }

    [Fact]
    public void CancellationRequiresExactProviderAcknowledgement_AndNeverImpliesRelease()
    {
        ExternalOperationRequest request = Request();
        var provider = new FakeProvider(request)
        {
            CancellationReceipt = new ExternalOperationCancellationReceipt(
                request, ExternalOperationCancellationOutcome.ConfirmedBeforeSubmit, request.Generations)
        };
        var adapter = new ExternalOperationAdapterSession(provider);
        Assert.Equal(ExternalOperationAdapterStatus.Accepted, adapter.Admit(Semantic(request)));
        Assert.Equal(ExternalOperationAdapterStatus.Accepted, adapter.Cancel(request.Correlation));
        Assert.Equal(ExternalOperationAdapterStatus.Rejected, adapter.Submit(request.Correlation));
        Assert.Equal(ExternalOperationAdapterStatus.Rejected, adapter.Admit(Semantic(request)));
        Assert.Equal(ExternalOperationAdapterStatus.Rejected, adapter.Poll(request.Correlation));

        var ambiguousProvider = new FakeProvider(request)
        {
            CancellationReceipt = new ExternalOperationCancellationReceipt(
                request, ExternalOperationCancellationOutcome.Ambiguous, request.Generations)
        };
        var ambiguous = new ExternalOperationAdapterSession(ambiguousProvider);
        Assert.Equal(ExternalOperationAdapterStatus.Accepted, ambiguous.Admit(Semantic(request)));
        Assert.Equal(ExternalOperationAdapterStatus.Rejected, ambiguous.Cancel(request.Correlation));
        Assert.Equal(ExternalOperationAdapterStatus.Rejected, ambiguous.Submit(request.Correlation));
        Assert.Equal(ExternalOperationAdapterStatus.Rejected, ambiguous.Admit(Semantic(request)));
        Assert.Equal(0, ambiguousProvider.SubmitCount);
    }

    [Fact]
    public async Task CancellationInFlight_BlocksConcurrentSubmitAndCorrelationReuse()
    {
        ExternalOperationRequest request = Request();
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        var provider = new FakeProvider(request)
        {
            CancellationEffect = () => { entered.Set(); Assert.True(resume.Wait(TimeSpan.FromSeconds(10))); },
            CancellationReceipt = new ExternalOperationCancellationReceipt(
                request, ExternalOperationCancellationOutcome.Ambiguous, request.Generations)
        };
        var adapter = new ExternalOperationAdapterSession(provider);
        Assert.Equal(ExternalOperationAdapterStatus.Accepted, adapter.Admit(Semantic(request)));
        Task<ExternalOperationAdapterStatus> cancelling = Task.Run(() => adapter.Cancel(request.Correlation));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(ExternalOperationAdapterStatus.Rejected, adapter.Submit(request.Correlation));
            Assert.Equal(ExternalOperationAdapterStatus.Rejected, adapter.Admit(Semantic(request)));
        }
        finally { resume.Set(); }
        Assert.Equal(ExternalOperationAdapterStatus.Rejected, await cancelling);
        Assert.Equal(ExternalOperationAdapterStatus.Rejected, adapter.Submit(request.Correlation));
        Assert.Equal(0, provider.SubmitCount);
    }

    private static ExternalGenerationSet Snapshot(Guid? token = null) =>
        new(ExternalOperationContract.Version, [token ?? Generation]);

    private static ExternalOperationRequest Request(ExternalRequestCorrelation? correlation = null) => new(
        new(new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")), new(1)),
        new(new(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")), new(1)),
        ExternalOperationContract.Version, Snapshot(), correlation ?? new(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")),
        ExternalEffectClass.NonIdempotent, ExternalVisibilityRequirement.StagedOutput, ExternalCancellationMode.ExactAcknowledgement);

    private static ExternalOperationSemanticRequest Semantic(ExternalOperationRequest request) => new(
        request.ContractVersion, request.Correlation, request.EffectClass, request.VisibilityRequirement,
        request.CancellationMode, ExternalReplayEffectClass.StagedReversibleUntilPublish);

    private sealed class FakeProvider(ExternalOperationRequest request) : IExternalOperationProvider, IExternalOperationCapabilityProvider, IExternalOperationCancellationProvider
    {
        public Queue<ExternalOperationReceipt> PollReceipts { get; } = [];
        public ExternalOperationReceipt? AdmissionReceipt { get; set; }
        public ExternalGenerationSet? SubmitGenerations { get; set; }
        public bool ThrowOnSubmit { get; set; }
        public bool ThrowOnAdmit { get; set; }
        public Action? AdmissionEffect { get; set; }
        public int AdmitCount { get; private set; }
        public ExternalOperationCapabilityResult? CapabilityResult { get; set; }
        public ExternalOperationCancellationReceipt? CancellationReceipt { get; set; }
        public Action? CancellationEffect { get; set; }
        public int SubmitCount { get; private set; }
        public ExternalOperationProviderPollResult Admit(ExternalOperationSemanticRequest semantic)
        {
            AdmitCount++;
            AdmissionEffect?.Invoke();
            if (ThrowOnAdmit) throw new TimeoutException();
            return Receipt(AdmissionReceipt ?? new ExternalOperationAdmissionReceipt(request, ExternalRuntimeOutcome.Succeeded), request.Generations);
        }
        public ExternalOperationProviderPollResult Submit(ExternalOperationRequest submitted)
        {
            SubmitCount++;
            if (ThrowOnSubmit) throw new TimeoutException();
            return Receipt(new ExternalOperationProgressReceipt(request, ExternalOperationStage.Submitted, ExternalRuntimeOutcome.Succeeded), SubmitGenerations ?? request.Generations);
        }
        public ExternalOperationProviderPollResult Poll(ExternalOperationRequest polled) => PollReceipts.Count == 0
            ? new(ExternalOperationProviderPollStatus.Pending, request.Generations)
            : Receipt(PollReceipts.Dequeue(), request.Generations);
        public ExternalOperationProviderPollResult Cancel(ExternalOperationRequest cancelled) =>
            new(ExternalOperationProviderPollStatus.Pending, request.Generations);
        public ExternalOperationCapabilityResult QueryCapabilities(ExternalOperationServiceBinding binding) =>
            CapabilityResult ?? new ExternalOperationCapabilityResult(
                ExternalOperationCapabilityStatus.Unavailable, request.Generations);
        public ExternalOperationCancellationReceipt RequestCancellation(ExternalOperationRequest cancelled)
        {
            CancellationEffect?.Invoke();
            return CancellationReceipt ?? new ExternalOperationCancellationReceipt(
                request, ExternalOperationCancellationOutcome.Unsupported, request.Generations);
        }
        private static ExternalOperationProviderPollResult Receipt(ExternalOperationReceipt receipt, ExternalGenerationSet generations) =>
            new(ExternalOperationProviderPollStatus.Receipt, generations, receipt);
    }
}
