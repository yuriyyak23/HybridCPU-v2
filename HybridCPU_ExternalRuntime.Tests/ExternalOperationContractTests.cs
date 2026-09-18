using System.Reflection;
using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime.Tests;

public sealed class ExternalOperationContractTests
{
    private static readonly Guid Token = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static ExternalGenerationSet Snapshot(params Guid[] tokens) => new(ExternalOperationContract.Version, tokens.Length == 0 ? [Token] : tokens);
    private static ExternalOperationRequest Request(ExternalOperationIdentity? operation = null, ExternalDomainLease? scope = null,
        ExternalGenerationSet? generations = null, ExternalRequestCorrelation? correlation = null,
        ExternalEffectClass effect = ExternalEffectClass.NonIdempotent,
        ExternalVisibilityRequirement visibility = ExternalVisibilityRequirement.StagedOutput,
        ExternalCancellationMode cancellation = ExternalCancellationMode.ExactAcknowledgement) =>
        new(operation ?? new(new(Guid.Parse("22222222-2222-2222-2222-222222222222")), new(2)),
            scope ?? new(new(Guid.Parse("33333333-3333-3333-3333-333333333333")), new(3)),
            ExternalOperationContract.Version, generations ?? Snapshot(),
            correlation ?? new(Guid.Parse("44444444-4444-4444-4444-444444444444")), effect, visibility, cancellation);

    private static ExternalOperationReplaySnapshot Replay(
        ExternalOperationStage stage,
        ExternalReplayEffectClass effect = ExternalReplayEffectClass.StagedReversibleUntilPublish,
        ExternalOperationReplayInvalidationReason invalidation = ExternalOperationReplayInvalidationReason.None,
        ExternalVisibilityRequirement visibility = ExternalVisibilityRequirement.StagedOutput,
        ExternalCancellationMode cancellation = ExternalCancellationMode.ExactAcknowledgement) =>
        new(Request(visibility: visibility, cancellation: cancellation), effect, stage, invalidation);

    // Deterministic provider test seam. No runtime feature claim or production provider integration.
    private sealed class FakeProvider(ExternalOperationRequest request)
    {
        public ExternalOperationReceipt Issue(ExternalOperationStage stage, ExternalRuntimeOutcome? outcome = null)
        {
            var result = outcome ?? (stage == ExternalOperationStage.Released ? ExternalRuntimeOutcome.Closed : ExternalRuntimeOutcome.Succeeded);
            return stage switch
            {
                ExternalOperationStage.Admitted => new ExternalOperationAdmissionReceipt(request, result),
                ExternalOperationStage.Submitted or ExternalOperationStage.Visible => new ExternalOperationProgressReceipt(request, stage, result),
                ExternalOperationStage.DeviceComplete => new ExternalOperationCompletionReceipt(request, result),
                ExternalOperationStage.Published => new ExternalOperationPublicationReceipt(request, result),
                ExternalOperationStage.Released => new ExternalOperationReleaseReceipt(request, result),
                _ => throw new ArgumentOutOfRangeException(nameof(stage))
            };
        }
    }
    [Fact]
    public void FakeProvider_DrivesEveryDistinctStage_AndRejectsReplayAndSkippedStages()
    {
        var request = Request(); var provider = new FakeProvider(request);
        var current = request.Stage;
        for (var next = ExternalOperationStage.Admitted; next <= ExternalOperationStage.Released; next++)
        {
            var receipt = provider.Issue(next);
            Assert.Equal(next, ExternalOperationContract.Accept(current, request, Snapshot(), receipt));
            Assert.Equal(ExternalOperationStage.Failed, ExternalOperationContract.Accept(next, request, Snapshot(), receipt));
            for (var wrong = ExternalOperationStage.Admitted; wrong <= ExternalOperationStage.Released; wrong++)
                if (wrong != next)
                    Assert.Equal(ExternalOperationStage.Failed, ExternalOperationContract.Accept(current, request, Snapshot(), provider.Issue(wrong)));
            current = next;
        }
        Assert.Equal(ExternalOperationStage.Released, current);
        Assert.Equal(ExternalOperationStage.DeviceComplete, provider.Issue(ExternalOperationStage.DeviceComplete).Stage);
        Assert.Equal(ExternalOperationStage.Published, provider.Issue(ExternalOperationStage.Published).Stage);
    }
    [Fact]
    public void Snapshots_AreImmutableUnorderedValues_WithoutPublicComponents()
    {
        var second = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var source = new List<Guid> { Token, second };
        var snapshot = new ExternalGenerationSet(ExternalOperationContract.Version, source);
        source.Clear();
        var equal = Snapshot(second, Token);
        Assert.Equal(equal, snapshot); Assert.Equal(equal.GetHashCode(), snapshot.GetHashCode());
        Assert.NotEqual(Snapshot(), snapshot);
        Assert.Single(typeof(ExternalGenerationSet).GetProperties());
        Assert.Equal(Request(), Request());
        Assert.Equal(new ExternalOperationCompletionReceipt(Request(), ExternalRuntimeOutcome.Succeeded),
            new ExternalOperationCompletionReceipt(Request(), ExternalRuntimeOutcome.Succeeded));
        Assert.NotEqual<ExternalOperationReceipt>(new ExternalOperationCompletionReceipt(Request(), ExternalRuntimeOutcome.Succeeded),
            new ExternalOperationPublicationReceipt(Request(), ExternalRuntimeOutcome.Succeeded));
        foreach (var type in NewTypes())
            Assert.All(type.GetProperties(), property => Assert.Null(property.SetMethod));
    }
    [Fact]
    public void VersionsGenerationsAndInvalidDescriptors_FailClosed()
    {
        foreach (var version in new HybridCpuExternalContractVersion[] { default, new(1, 3, 0), new(1, 4, 1), new(1, 5, 0), new(2, 0, 0) })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExternalGenerationSet(version, [Token]));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExternalOperationRequest(Request().Operation, Request().Scope,
                version, Snapshot(), Request().Correlation, Request().EffectClass, Request().VisibilityRequirement, Request().CancellationMode));
        }
        Assert.Throws<ArgumentException>(() => new ExternalGenerationSet(ExternalOperationContract.Version, []));
        Assert.Throws<ArgumentException>(() => Snapshot(Guid.Empty));
        Assert.Throws<ArgumentException>(() => Snapshot(Token, Token));
        Assert.Throws<ArgumentNullException>(() => new ExternalGenerationSet(ExternalOperationContract.Version, null!));
        Assert.Throws<ArgumentException>(() => Request(operation: default(ExternalOperationIdentity)));
        Assert.Throws<ArgumentException>(() => Request(scope: default(ExternalDomainLease)));
        Assert.Throws<ArgumentException>(() => Request(correlation: default(ExternalRequestCorrelation)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Request(effect: (ExternalEffectClass)0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Request(visibility: (ExternalVisibilityRequirement)255));
        Assert.Throws<ArgumentOutOfRangeException>(() => Request(cancellation: (ExternalCancellationMode)0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExternalOperationProgressReceipt(Request(), ExternalOperationStage.Published, ExternalRuntimeOutcome.Succeeded));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExternalOperationCompletionReceipt(Request(), (ExternalRuntimeOutcome)255));
    }

    [Fact]
    public void ProviderAdapterSeam_RequiresExplicitPendingOrExactReceipt()
    {
        var request = Request();
        var semantic = new ExternalOperationSemanticRequest(
            ExternalOperationContract.Version, request.Correlation, request.EffectClass,
            request.VisibilityRequirement, request.CancellationMode,
            ExternalReplayEffectClass.StagedReversibleUntilPublish);
        Assert.Equal(request.Correlation, semantic.Correlation);
        Assert.Equal(ExternalVisibilityRequirement.StagedOutput, semantic.VisibilityRequirement);

        var pending = new ExternalOperationProviderPollResult(
            ExternalOperationProviderPollStatus.Pending, Snapshot());
        Assert.Null(pending.Receipt);
        Assert.Throws<ArgumentException>(() => new ExternalOperationProviderPollResult(
            ExternalOperationProviderPollStatus.Receipt, Snapshot()));
        Assert.Throws<ArgumentException>(() => new ExternalOperationProviderPollResult(
            ExternalOperationProviderPollStatus.Pending, Snapshot(),
            new ExternalOperationAdmissionReceipt(request, ExternalRuntimeOutcome.Succeeded)));

        var receipt = new ExternalOperationProviderPollResult(
            ExternalOperationProviderPollStatus.Receipt, Snapshot(),
            new ExternalOperationAdmissionReceipt(request, ExternalRuntimeOutcome.Succeeded));
        Assert.Equal(ExternalOperationStage.Admitted, receipt.Receipt!.Stage);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExternalOperationSemanticRequest(
            new HybridCpuExternalContractVersion(1, 6, 0), request.Correlation, request.EffectClass,
            request.VisibilityRequirement, request.CancellationMode,
            ExternalReplayEffectClass.StagedReversibleUntilPublish));
    }

    [Fact]
    public void PublicationGate_RequiresExactCurrentCompletionThenVisibility()
    {
        var request = Request();
        var provider = new FakeProvider(request);
        var evidence = new ExternalOperationPublicationEvidence(
            request, Snapshot(),
            (ExternalOperationCompletionReceipt)provider.Issue(ExternalOperationStage.DeviceComplete),
            (ExternalOperationProgressReceipt)provider.Issue(ExternalOperationStage.Visible));
        Assert.Equal(ExternalOperationPublicationEligibility.Eligible,
            ExternalOperationPublicationGate.Evaluate(evidence));

        Assert.Equal(ExternalOperationPublicationEligibility.Rejected,
            ExternalOperationPublicationGate.Evaluate(null));
        var staleEvidence = new ExternalOperationPublicationEvidence(
            request,
            Snapshot(Guid.NewGuid()),
            evidence.Completion,
            evidence.Visibility);
        Assert.Equal(ExternalOperationPublicationEligibility.Stale,
            ExternalOperationPublicationGate.Evaluate(staleEvidence));
        var wrong = Request(correlation: new ExternalRequestCorrelation(Guid.NewGuid()));
        var crossOperation = new ExternalOperationPublicationEvidence(request, Snapshot(),
            (ExternalOperationCompletionReceipt)new FakeProvider(wrong).Issue(ExternalOperationStage.DeviceComplete),
            (ExternalOperationProgressReceipt)new FakeProvider(wrong).Issue(ExternalOperationStage.Visible));
        Assert.Equal(ExternalOperationPublicationEligibility.Rejected,
            ExternalOperationPublicationGate.Evaluate(crossOperation));
        var coherent = Request(visibility: ExternalVisibilityRequirement.Coherent);
        var coherentEvidence = new ExternalOperationPublicationEvidence(coherent, Snapshot(),
            (ExternalOperationCompletionReceipt)new FakeProvider(coherent).Issue(ExternalOperationStage.DeviceComplete),
            (ExternalOperationProgressReceipt)new FakeProvider(coherent).Issue(ExternalOperationStage.Visible));
        Assert.Equal(ExternalOperationPublicationEligibility.Rejected,
            ExternalOperationPublicationGate.Evaluate(coherentEvidence));
    }
    [Fact]
    public void ExactCorrelation_RejectsCrossOperationAttemptScopeEpochDescriptorAndSemantics()
    {
        var request = Request();
        ExternalOperationRequest[] wrongRequests =
        [
            Request(operation: request.Operation with { Handle = new(Guid.NewGuid()) }),
            Request(operation: request.Operation with { Generation = new(1) }),
            Request(operation: request.Operation with { Generation = new(3) }),
            Request(scope: request.Scope with { Handle = new(Guid.NewGuid()) }),
            Request(scope: request.Scope with { Epoch = new(2) }),
            Request(correlation: new(Guid.NewGuid())),
            Request(generations: Snapshot(Guid.NewGuid())),
            Request(effect: ExternalEffectClass.ReadOnly), Request(visibility: ExternalVisibilityRequirement.Coherent),
            Request(cancellation: ExternalCancellationMode.BestEffort)
        ];
        foreach (var wrong in wrongRequests)
            foreach (var stage in Enum.GetValues<ExternalOperationStage>().Where(x => x >= ExternalOperationStage.Admitted && x <= ExternalOperationStage.Released))
                Assert.Equal(ExternalOperationStage.Stale, ExternalOperationContract.Accept((ExternalOperationStage)((byte)stage - 1),
                    request, Snapshot(), new FakeProvider(wrong).Issue(stage)));
    }
    [Fact]
    public void MissingChangedOrDowngradedSnapshot_AndAsyncInvalidationAreStickyStale()
    {
        var request = Request(); var completion = new FakeProvider(request).Issue(ExternalOperationStage.DeviceComplete);
        foreach (var snapshot in new ExternalGenerationSet?[] { null, Snapshot(Guid.NewGuid()), Snapshot(Token, Guid.NewGuid()) })
        {
            Assert.Equal(ExternalOperationStage.Stale, ExternalOperationContract.Accept(ExternalOperationStage.Submitted, request, snapshot, completion));
            Assert.Equal(ExternalOperationStage.Stale, ExternalOperationContract.Revalidate(ExternalOperationStage.Visible, request, snapshot));
        }
        // A provider revision has no CPU ordering semantics: both older and newer unequal snapshots are stale.
        Assert.Equal(ExternalOperationStage.Stale, ExternalOperationContract.Accept(ExternalOperationStage.Stale, request, Snapshot(), completion));
        Assert.Equal(ExternalOperationStage.Failed, ExternalOperationContract.Accept(ExternalOperationStage.Failed, request, Snapshot(), completion));
    }
    [Fact]
    public void MissingUnknownFaultTimeoutDisconnectAndCancellationRace_NeverProveSuccessOrRelease()
    {
        var request = Request(); var provider = new FakeProvider(request);
        foreach (var stage in Enum.GetValues<ExternalOperationStage>().Where(x => x >= ExternalOperationStage.Admitted && x <= ExternalOperationStage.Released))
        {
            var previous = (ExternalOperationStage)((byte)stage - 1);
            Assert.Equal(ExternalOperationStage.Failed, ExternalOperationContract.Accept(previous, request, Snapshot(), null));
            foreach (var outcome in Enum.GetValues<ExternalRuntimeOutcome>())
            {
                var expected = outcome == ExternalRuntimeOutcome.Stale ? ExternalOperationStage.Stale :
                    outcome == (stage == ExternalOperationStage.Released ? ExternalRuntimeOutcome.Closed : ExternalRuntimeOutcome.Succeeded) ? stage : ExternalOperationStage.Failed;
                Assert.Equal(expected, ExternalOperationContract.Accept(previous, request, Snapshot(), provider.Issue(stage, outcome)));
            }
        }
        // Transport/cancellation failures are represented by absent or Unknown receipts; a late success cannot unquarantine.
        foreach (var failure in new Exception[] { new TimeoutException(), new IOException("disconnect"), new OperationCanceledException() })
        {
            ExternalOperationReceipt? receipt;
            try { throw failure; } catch { receipt = null; }
            var failed = ExternalOperationContract.Accept(ExternalOperationStage.Published, request, Snapshot(), receipt);
            Assert.Equal(ExternalOperationStage.Failed, failed);
            Assert.Equal(ExternalOperationStage.Failed, ExternalOperationContract.Accept(failed, request, Snapshot(), provider.Issue(ExternalOperationStage.Released)));
        }
        Assert.Equal(ExternalOperationStage.Failed, ExternalOperationContract.Accept((ExternalOperationStage)255, request, Snapshot(), provider.Issue(ExternalOperationStage.Admitted)));
    }
    [Fact]
    public void PublicAbi_PreservesPreviousMemberBaseline_AndExcludesImplementationIdentities()
    {
        var assembly = typeof(ExternalOperationRequest).Assembly;
        var actual = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in assembly.GetExportedTypes())
        {
            actual.Add($"TYPE {type.FullName} : {type.BaseType}".TrimEnd());
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                actual.Add($"{type.FullName} | {member.MemberType} | {member}");
        }
        foreach (var line in File.ReadLines(Path.Combine(Root(), "HybridCPU_ExternalRuntime.Contracts", "PublicApi.1.3.0.txt")))
            Assert.Contains(line, actual);
        string[] forbidden = ["Cxl", "SingNext", "Fabric", "Topology", "Bdf", "Hdm", "Dpa", "PhysicalAddress", "BusAddress", "ProviderPrivate", "Port", "Switch", "Mapping"];
        foreach (var type in NewTypes())
        {
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                Assert.DoesNotContain(forbidden, word => member.Name.Contains(word, StringComparison.OrdinalIgnoreCase));
            Assert.All(type.GetConstructors().SelectMany(x => x.GetParameters()), parameter =>
            {
                Assert.NotEqual(typeof(object), parameter.ParameterType); Assert.NotEqual(typeof(string), parameter.ParameterType);
                Assert.NotEqual(typeof(object[]), parameter.ParameterType);
                Assert.Null(parameter.GetCustomAttribute<System.Runtime.CompilerServices.DynamicAttribute>());
            });
        }
        foreach (var path in new[] { "HybridCPU_ISE/HybridCPU_ISE.csproj", "Compilers/HybridCPU_Compiler/Core/HybridCPU.Compiler.Core.csproj" })
            Assert.DoesNotContain("SingNext", File.ReadAllText(Path.Combine(Root(), path)), StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void Serialization_RoundTripsExactTypedReceipts_AndRejectsMalformedSnapshots()
    {
        var request = Request(); var provider = new FakeProvider(request);
        foreach (var stage in Enum.GetValues<ExternalOperationStage>().Where(x => x >= ExternalOperationStage.Admitted && x <= ExternalOperationStage.Released))
        {
            var receipt = provider.Issue(stage);
            var json = System.Text.Json.JsonSerializer.Serialize(receipt, receipt.GetType());
            var restored = (ExternalOperationReceipt)System.Text.Json.JsonSerializer.Deserialize(json, receipt.GetType())!;
            Assert.Equal(receipt, restored);
            Assert.Equal(stage, ExternalOperationContract.Accept((ExternalOperationStage)((byte)stage - 1), request, Snapshot(), restored));
        }
        foreach (var json in new[] { "{}", "{\"version\":[1,3,0],\"tokens\":[]}",
            "{\"version\":[1,4,0],\"tokens\":[]}", "{\"version\":[1,4,0],\"tokens\":[],\"tokens\":[]}",
            "{\"version\":[1,4,0],\"tokens\":[\"bad\"]}",
            $"{{\"version\":[1,4,0],\"tokens\":[\"{Token}\",\"{Token}\"]}}" })
            Assert.Throws<System.Text.Json.JsonException>(() => System.Text.Json.JsonSerializer.Deserialize<ExternalGenerationSet>(json));
    }

    [Fact]
    public void AdmissionBinding_RequiresIndependentCpuGuardAndProviderAdmission()
    {
        var request = Request();
        var cpuGuard = new ExternalOperationCpuGuardReceipt(request, ExternalCpuGuardStatus.Allowed);

        var denied = ExternalOperationAdmissionBinding.Evaluate(
            request,
            Snapshot(),
            cpuGuard,
            new ExternalOperationAdmissionReceipt(request, ExternalRuntimeOutcome.Denied));

        Assert.False(denied.IsSubmitEligible);
        Assert.Equal(ExternalOperationBindingStatus.Rejected, denied.Status);
        Assert.Equal(ExternalOperationBindingRejectKind.AdmissionRejected, denied.RejectKind);

        var accepted = ExternalOperationAdmissionBinding.Evaluate(
            request,
            Snapshot(),
            cpuGuard,
            new ExternalOperationAdmissionReceipt(request, ExternalRuntimeOutcome.Succeeded));

        Assert.True(accepted.IsSubmitEligible);
        Assert.Equal(ExternalOperationBindingRejectKind.None, accepted.RejectKind);
        string legalitySource = File.ReadAllText(Path.Combine(
            Root(), "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Safety", "SafetyVerifier.Types.cs"));
        Assert.DoesNotContain("CxlFabric", legalitySource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SingNextOs", legalitySource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ExternalProvider", legalitySource, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AdmissionBinding_RejectsOwnerScopeAndOperationReuseDespiteProviderSuccess()
    {
        var request = Request();
        var providerAdmission = new ExternalOperationAdmissionReceipt(request, ExternalRuntimeOutcome.Succeeded);
        ExternalOperationRequest[] changedCpuRequests =
        [
            Request(operation: request.Operation with { Generation = new(request.Operation.Generation.Value + 1) }),
            Request(scope: request.Scope with { Epoch = new(request.Scope.Epoch.Value + 1) }),
            Request(correlation: new(Guid.NewGuid()))
        ];

        foreach (var changed in changedCpuRequests)
        {
            var decision = ExternalOperationAdmissionBinding.Evaluate(
                request,
                Snapshot(),
                new ExternalOperationCpuGuardReceipt(changed, ExternalCpuGuardStatus.Allowed),
                providerAdmission);

            Assert.False(decision.IsSubmitEligible);
            Assert.Equal(ExternalOperationBindingStatus.Stale, decision.Status);
            Assert.Equal(ExternalOperationBindingRejectKind.CpuGuardCorrelationMismatch, decision.RejectKind);
        }

        var rejectedCpuGuard = ExternalOperationAdmissionBinding.Evaluate(
            request,
            Snapshot(),
            new ExternalOperationCpuGuardReceipt(request, ExternalCpuGuardStatus.Rejected),
            providerAdmission);
        Assert.Equal(ExternalOperationBindingRejectKind.CpuGuardRejected, rejectedCpuGuard.RejectKind);
        Assert.False(rejectedCpuGuard.IsSubmitEligible);
    }

    [Fact]
    public void AdmissionBinding_StaleGenerationsAndReceiptsCannotBecomeCpuLegality()
    {
        var request = Request();
        var cpuGuard = new ExternalOperationCpuGuardReceipt(request, ExternalCpuGuardStatus.Allowed);
        var staleGeneration = ExternalOperationAdmissionBinding.Evaluate(
            request,
            Snapshot(Guid.NewGuid()),
            cpuGuard,
            new ExternalOperationAdmissionReceipt(request, ExternalRuntimeOutcome.Succeeded));
        var staleReceipt = ExternalOperationAdmissionBinding.Evaluate(
            request,
            Snapshot(),
            cpuGuard,
            new ExternalOperationAdmissionReceipt(request, ExternalRuntimeOutcome.Stale));

        Assert.Equal(ExternalOperationBindingStatus.Stale, staleGeneration.Status);
        Assert.Equal(ExternalOperationBindingRejectKind.GenerationMismatch, staleGeneration.RejectKind);
        Assert.Equal(ExternalOperationBindingStatus.Stale, staleReceipt.Status);
        Assert.Equal(ExternalOperationBindingRejectKind.AdmissionStale, staleReceipt.RejectKind);
        Assert.False(staleGeneration.IsSubmitEligible);
        Assert.False(staleReceipt.IsSubmitEligible);
        Assert.DoesNotContain(typeof(ExternalOperationAdmissionBinding).Assembly.GetReferencedAssemblies(),
            static assembly => assembly.Name!.Contains("ISE", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(ExternalRuntimeOutcome.Unknown)]
    [InlineData(ExternalRuntimeOutcome.NotFound)]
    [InlineData(ExternalRuntimeOutcome.Revoked)]
    [InlineData(ExternalRuntimeOutcome.Faulted)]
    [InlineData(ExternalRuntimeOutcome.Denied)]
    public void AdmissionBinding_AmbiguousOrRevokedProviderOutcomeNeverEnablesSubmit(ExternalRuntimeOutcome outcome)
    {
        var request = Request();
        var decision = ExternalOperationAdmissionBinding.Evaluate(
            request,
            Snapshot(),
            new ExternalOperationCpuGuardReceipt(request, ExternalCpuGuardStatus.Allowed),
            new ExternalOperationAdmissionReceipt(request, outcome));

        Assert.Equal(ExternalOperationBindingStatus.Rejected, decision.Status);
        Assert.Equal(ExternalOperationBindingRejectKind.AdmissionRejected, decision.RejectKind);
        Assert.False(decision.IsSubmitEligible);
    }

    [Fact]
    public void AdmissionBinding_RejectsInvalidCpuGuardStatusAndMissingIndependentGates()
    {
        var request = Request();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ExternalOperationCpuGuardReceipt(request, (ExternalCpuGuardStatus)255));

        var missingGuard = ExternalOperationAdmissionBinding.Evaluate(
            request,
            Snapshot(),
            cpuGuard: null,
            new ExternalOperationAdmissionReceipt(request, ExternalRuntimeOutcome.Succeeded));
        var missingAdmission = ExternalOperationAdmissionBinding.Evaluate(
            request,
            Snapshot(),
            new ExternalOperationCpuGuardReceipt(request, ExternalCpuGuardStatus.Allowed),
            admission: null);

        Assert.Equal(ExternalOperationBindingRejectKind.CpuGuardMissing, missingGuard.RejectKind);
        Assert.Equal(ExternalOperationBindingRejectKind.AdmissionMissing, missingAdmission.RejectKind);
        Assert.False(missingGuard.IsSubmitEligible);
        Assert.False(missingAdmission.IsSubmitEligible);
    }

    [Fact]
    public void ReplayPolicy_UsesLifecycleToPreventDuplicateSubmit()
    {
        var beforeSubmit = ExternalOperationReplayPolicy.Evaluate(Replay(ExternalOperationStage.Admitted), Snapshot());
        var submittedExactCancellation = ExternalOperationReplayPolicy.Evaluate(Replay(ExternalOperationStage.Submitted), Snapshot());
        var submittedBestEffort = ExternalOperationReplayPolicy.Evaluate(
            Replay(ExternalOperationStage.Submitted, cancellation: ExternalCancellationMode.BestEffort), Snapshot());
        var completed = ExternalOperationReplayPolicy.Evaluate(Replay(ExternalOperationStage.DeviceComplete), Snapshot());
        var visible = ExternalOperationReplayPolicy.Evaluate(Replay(ExternalOperationStage.Visible), Snapshot());
        var published = ExternalOperationReplayPolicy.Evaluate(Replay(ExternalOperationStage.Published), Snapshot());

        Assert.Equal(ExternalOperationReplayAction.RequireFreshAdmission, beforeSubmit.Action);
        Assert.Equal(ExternalOperationReplayAction.CancelThenReadmit, submittedExactCancellation.Action);
        Assert.Equal(ExternalOperationReplayAction.AwaitOriginalOperation, submittedBestEffort.Action);
        Assert.Equal(ExternalOperationReplayAction.ContinueOriginalLifecycle, completed.Action);
        Assert.Equal(ExternalOperationReplayAction.ContinueOriginalLifecycle, visible.Action);
        Assert.Equal(ExternalOperationReplayAction.ReplayBarrier, published.Action);
        foreach (var decision in new[] { beforeSubmit, submittedExactCancellation, submittedBestEffort, completed, visible, published })
            Assert.False(decision.AllowsDirectSubmit);
    }

    [Fact]
    public void ReplayPolicy_DirectCoherentAndIrreversibleEffectsAreBarriersAfterSubmit()
    {
        var coherentSubmitted = ExternalOperationReplayPolicy.Evaluate(
            Replay(ExternalOperationStage.Submitted, visibility: ExternalVisibilityRequirement.Coherent), Snapshot());
        var coherentComplete = ExternalOperationReplayPolicy.Evaluate(
            Replay(ExternalOperationStage.DeviceComplete, visibility: ExternalVisibilityRequirement.Coherent), Snapshot());
        var irreversibleSubmitted = ExternalOperationReplayPolicy.Evaluate(
            Replay(ExternalOperationStage.Submitted, ExternalReplayEffectClass.IrreversibleBarrier), Snapshot());
        var publishedSnapshotEffect = ExternalOperationReplayPolicy.Evaluate(
            Replay(ExternalOperationStage.Published, ExternalReplayEffectClass.SnapshotOrIdempotenceRequired), Snapshot());

        Assert.All(new[] { coherentSubmitted, coherentComplete, irreversibleSubmitted, publishedSnapshotEffect },
            static decision => Assert.Equal(ExternalOperationReplayAction.ReplayBarrier, decision.Action));
        Assert.All(new[] { coherentSubmitted, coherentComplete, irreversibleSubmitted, publishedSnapshotEffect },
            static decision => Assert.False(decision.AllowsDirectSubmit));
    }

    [Fact]
    public void ReplayPolicy_InvalidationAndGenerationDriftRequireFreshAuthorityWithoutResubmit()
    {
        foreach (var reason in Enum.GetValues<ExternalOperationReplayInvalidationReason>().Where(static value => value != ExternalOperationReplayInvalidationReason.None))
        {
            var preSubmit = ExternalOperationReplayPolicy.Evaluate(Replay(ExternalOperationStage.Admitted, invalidation: reason), Snapshot());
            var afterSubmit = ExternalOperationReplayPolicy.Evaluate(Replay(ExternalOperationStage.Submitted, invalidation: reason), Snapshot());
            Assert.Equal(ExternalOperationReplayAction.ReAdmissionRequired, preSubmit.Action);
            Assert.Equal(reason, preSubmit.InvalidationReason);
            Assert.Equal(ExternalOperationReplayAction.ReplayBarrier, afterSubmit.Action);
            Assert.Equal(reason, afterSubmit.InvalidationReason);
            Assert.False(preSubmit.AllowsDirectSubmit);
            Assert.False(afterSubmit.AllowsDirectSubmit);
        }

        var driftBeforeSubmit = ExternalOperationReplayPolicy.Evaluate(Replay(ExternalOperationStage.Admitted), Snapshot(Guid.NewGuid()));
        var driftAfterSubmit = ExternalOperationReplayPolicy.Evaluate(Replay(ExternalOperationStage.Submitted), Snapshot(Guid.NewGuid()));
        Assert.Equal(ExternalOperationReplayAction.ReAdmissionRequired, driftBeforeSubmit.Action);
        Assert.Equal(ExternalOperationReplayAction.ReplayBarrier, driftAfterSubmit.Action);
        Assert.Equal(ExternalOperationReplayInvalidationReason.GenerationChanged, driftBeforeSubmit.InvalidationReason);
        Assert.Equal(ExternalOperationReplayInvalidationReason.GenerationChanged, driftAfterSubmit.InvalidationReason);
        Assert.False(driftBeforeSubmit.AllowsDirectSubmit);
        Assert.False(driftAfterSubmit.AllowsDirectSubmit);
    }

    [Fact]
    public void ReplayPolicy_UsesOpaqueSemanticKeyAndDoesNotConsumeReplayToken()
    {
        var first = Replay(ExternalOperationStage.Admitted, ExternalReplayEffectClass.SnapshotOrIdempotenceRequired);
        var same = Replay(ExternalOperationStage.Admitted, ExternalReplayEffectClass.SnapshotOrIdempotenceRequired);
        var changedEffect = Replay(ExternalOperationStage.Admitted, ExternalReplayEffectClass.IrreversibleBarrier);

        Assert.Equal(first.Key, same.Key);
        Assert.NotEqual(first.Key, changedEffect.Key);
        Assert.Equal(first.Request.Operation, first.Key.Operation);
        Assert.Equal(first.Request.Correlation, first.Key.Correlation);
        Assert.Equal(first.Request.Generations, first.Key.Generations);
        Assert.Throws<ArgumentOutOfRangeException>(() => Replay(
            ExternalOperationStage.Admitted,
            (ExternalReplayEffectClass)255));
        Assert.DoesNotContain(typeof(ExternalOperationReplayPolicy).GetMethods(),
            static method => method.GetParameters().Any(parameter =>
                parameter.ParameterType.Name.Contains("ReplayToken", StringComparison.Ordinal)));
    }

    private static Type[] NewTypes() => [typeof(ExternalOperationRequest), typeof(ExternalGenerationSet), typeof(ExternalOperationReceipt),
        typeof(ExternalOperationAdmissionReceipt), typeof(ExternalOperationProgressReceipt), typeof(ExternalOperationCompletionReceipt),
        typeof(ExternalOperationPublicationReceipt), typeof(ExternalOperationReleaseReceipt),
        typeof(ExternalOperationCpuGuardReceipt), typeof(ExternalOperationBindingDecision),
        typeof(ExternalOperationReplaySnapshot), typeof(ExternalOperationReplayDecision),
        typeof(ExternalOperationSemanticRequest), typeof(ExternalOperationProviderPollResult),
        typeof(ExternalOperationPublicationEvidence), typeof(ExternalOperationServiceHandle),
        typeof(ExternalOperationServiceBinding), typeof(ExternalOperationCapabilityResult),
        typeof(ExternalOperationCancellationReceipt), typeof(ExternalSecureDomainHandle),
        typeof(ExternalSecureDomainGeneration), typeof(ExternalSecureDomainLease),
        typeof(ExternalSecureRegionHandle), typeof(ExternalSecureRegionGeneration),
        typeof(ExternalSecureEvidenceContextHandle), typeof(ExternalSecureDomainProperties),
        typeof(ExternalSecureDomainCreateRequest), typeof(ExternalSecureDomainCreationProof),
        typeof(ExternalSecureDomainCreateReceipt), typeof(ExternalSecureDomainCloseReceipt),
        typeof(ExternalSecureDomainCreateProviderResult), typeof(ExternalSecureDomainCloseProviderResult),
        typeof(ExternalSecureVirtualEventAuthorizationReceipt), typeof(ExternalSecureVirtualEventAuthorizationResult),
        typeof(ExternalSecureVirtualIoBinding), typeof(ExternalSecureIoAdmissionReceipt),
        typeof(ExternalSecureRegionBinding),
        typeof(ExternalSecureRegionCloseReceipt), typeof(ExternalSecureExecutionBinding),
        typeof(ExternalSecureGuestRegionBinding)];
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HybridCPU v2.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}

