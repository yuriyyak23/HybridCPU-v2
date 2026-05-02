using System;
using System.IO;
using System.Reflection;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class DmaStreamComputeTokenStorePhase03Tests
{
    [Fact]
    public void TokenStore_DecodeCarrierDoesNotAllocateUntilExplicitIssueAdmission()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var microOp = new DmaStreamComputeMicroOp(descriptor);
        var store = new DmaStreamComputeTokenStore();

        Assert.Equal(0, store.ActiveTokenCount);
        Assert.Equal(
            DmaStreamComputeTokenEvidenceKind.NotCreated,
            microOp.ReplayEvidence.TokenLifecycleEvidence.EvidenceKind);
        Assert.False(microOp.ReplayEvidence.TokenLifecycleEvidence.HasCommitted);

        DmaStreamComputeIssueAdmissionResult admission =
            store.TryAllocateAtIssueAdmission(
                DmaStreamComputeIssueAdmissionRequest.ForLane6(
                    microOp,
                    DmaStreamComputeIssueAdmissionMetadata.Lane6(
                        issuingPc: 0x400,
                        bundleId: 0x44,
                        issueCycle: 12,
                        replayEpoch: 3)));

        Assert.True(admission.IsAccepted, admission.Message);
        Assert.True(admission.HasAllocatedToken);
        Assert.Equal(1, store.ActiveTokenCount);
        Assert.Equal(DmaStreamComputeTokenState.Admitted, admission.Token!.State);
        Assert.True(admission.Handle.MatchesOwner(descriptor.OwnerBinding));
        Assert.Equal(0x400UL, admission.Entry!.Metadata.IssuingPc);
        Assert.Equal(0x44UL, admission.Entry.Metadata.BundleId);
        Assert.Equal(6, admission.Entry.Metadata.SlotIndex);
        Assert.Equal(6, admission.Entry.Metadata.LaneIndex);

        Assert.False(DmaStreamComputeDescriptorParser.ExecutionEnabled);
        var core = new Processor.CPU_Core(0);
        Assert.Throws<InvalidOperationException>(() => microOp.Execute(ref core));
    }

    [Fact]
    public void TokenStore_DescriptorIdentityAloneCannotAliasStoreOwnedHandles()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var microOp = new DmaStreamComputeMicroOp(descriptor);
        var store = new DmaStreamComputeTokenStore();

        DmaStreamComputeIssueAdmissionResult first =
            store.TryAllocateAtIssueAdmission(
                DmaStreamComputeIssueAdmissionRequest.ForLane6(
                    microOp,
                    DmaStreamComputeIssueAdmissionMetadata.Lane6(bundleId: 1)));
        DmaStreamComputeIssueAdmissionResult second =
            store.TryAllocateAtIssueAdmission(
                DmaStreamComputeIssueAdmissionRequest.ForLane6(
                    microOp,
                    DmaStreamComputeIssueAdmissionMetadata.Lane6(bundleId: 2)));

        Assert.True(first.IsAccepted, first.Message);
        Assert.True(second.IsAccepted, second.Message);
        Assert.Equal(2, store.ActiveTokenCount);
        Assert.Equal(first.Entry!.DescriptorIdentityHash, second.Entry!.DescriptorIdentityHash);
        Assert.NotEqual(first.Handle, second.Handle);
        Assert.NotEqual(first.Handle.TokenId, second.Handle.TokenId);

        Assert.True(store.TryGet(first.Handle, descriptor.OwnerBinding, out DmaStreamComputeActiveTokenEntry? entry));
        Assert.Same(first.Token, entry!.Token);

        DmaStreamComputeOwnerBinding wrongOwner = descriptor.OwnerBinding with
        {
            OwnerDomainTag = descriptor.OwnerBinding.OwnerDomainTag ^ 0x10UL
        };
        Assert.False(store.TryGet(first.Handle, wrongOwner, out _));
    }

    [Fact]
    public void TokenStore_OwnerDomainMismatchRejectsBeforeTokenCreation()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var microOp = new DmaStreamComputeMicroOp(descriptor);
        var store = new DmaStreamComputeTokenStore();
        DmaStreamComputeOwnerGuardContext staleContext =
            descriptor.OwnerGuardDecision.RuntimeOwnerContext with
            {
                OwnerDomainTag = 0x4000,
                ActiveDomainCertificate = 0x4000
            };
        DmaStreamComputeOwnerGuardDecision staleGuard =
            new SafetyVerifier().EvaluateDmaStreamComputeOwnerGuard(
                descriptor.OwnerBinding,
                staleContext);
        DmaStreamComputePressurePolicy policy = DmaStreamComputePressurePolicy.Default;

        DmaStreamComputeIssueAdmissionResult admission =
            store.TryAllocateAtIssueAdmission(
                new DmaStreamComputeIssueAdmissionRequest(
                    microOp,
                    staleGuard,
                    DmaStreamComputeIssueAdmissionMetadata.Lane6(),
                    policy,
                    DmaStreamComputePressureSnapshot.Permissive(policy)));

        Assert.False(admission.IsAccepted);
        Assert.False(admission.HasAllocatedToken);
        Assert.Equal(0, store.ActiveTokenCount);
        Assert.Equal(DmaStreamComputeIssueAdmissionStatus.ArchitecturalFault, admission.Status);
        Assert.Equal(DmaStreamComputeIssueAdmissionRejectKind.OwnerDomainMismatch, admission.RejectKind);
        Assert.Equal(DmaStreamComputeValidationFault.OwnerDomainFault, admission.ValidationFault);
        Assert.True(admission.RequiresRetireExceptionPublication);
    }

    [Fact]
    public void TokenStore_ReplayAuthorityCannotAllocateIssueAdmissionToken()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var microOp = new DmaStreamComputeMicroOp(descriptor);
        var store = new DmaStreamComputeTokenStore();
        DmaStreamComputeOwnerGuardDecision replayLikeGuard = CreateIssueGuardDecisionForTest(
            descriptor,
            LegalityDecision.Allow(
                LegalityAuthoritySource.ReplayPhaseCertificate,
                attemptedReplayCertificateReuse: true),
            "replay certificate is evidence, not issue/admission authority");
        DmaStreamComputePressurePolicy policy = DmaStreamComputePressurePolicy.Default;

        DmaStreamComputeIssueAdmissionResult admission =
            store.TryAllocateAtIssueAdmission(
                new DmaStreamComputeIssueAdmissionRequest(
                    microOp,
                    replayLikeGuard,
                    DmaStreamComputeIssueAdmissionMetadata.Lane6(),
                    policy,
                    DmaStreamComputePressureSnapshot.Permissive(policy)));

        Assert.False(admission.IsAccepted);
        Assert.False(admission.HasAllocatedToken);
        Assert.Equal(0, store.ActiveTokenCount);
        Assert.Equal(DmaStreamComputeIssueAdmissionStatus.ArchitecturalFault, admission.Status);
        Assert.Equal(DmaStreamComputeIssueAdmissionRejectKind.OwnerDomainMismatch, admission.RejectKind);
        Assert.Equal(DmaStreamComputeValidationFault.OwnerDomainFault, admission.ValidationFault);
    }

    [Fact]
    public void TokenStore_TerminalTokensRejectStoreOwnedCancellation()
    {
        DmaStreamComputeTelemetryTests.InitializeMainMemory(0x10000);
        DmaStreamComputeTelemetryTests.WriteMemory(0x9000, DmaStreamComputeTelemetryTests.Fill(0x11, 16));
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTelemetryTests.ParseValid(
                DmaStreamComputeTelemetryTests.BuildDescriptor(DmaStreamComputeOperationKind.Copy));
        var microOp = new DmaStreamComputeMicroOp(descriptor);
        var store = new DmaStreamComputeTokenStore();

        DmaStreamComputeIssueAdmissionResult committedAdmission =
            AllocateStoreToken(store, microOp);
        DmaStreamComputeToken committedToken = committedAdmission.Token!;
        committedToken.MarkIssued();
        committedToken.MarkReadsComplete();
        committedToken.StageDestinationWrite(0x9000, DmaStreamComputeTelemetryTests.Fill(0x44, 16));
        Assert.False(committedToken.MarkComputeComplete().RequiresRetireExceptionPublication);
        Assert.True(committedToken.Commit(Processor.MainMemory, descriptor.OwnerGuardDecision).Succeeded);

        Assert.False(store.TryCancel(
            committedAdmission.Handle,
            descriptor.OwnerBinding,
            DmaStreamComputeTokenCancelReason.Flush));
        Assert.Equal(DmaStreamComputeTokenState.Committed, committedToken.State);

        DmaStreamComputeIssueAdmissionResult faultedAdmission =
            AllocateStoreToken(store, microOp);
        DmaStreamComputeToken faultedToken = faultedAdmission.Token!;
        faultedToken.MarkIssued();
        faultedToken.PublishFault(
            DmaStreamComputeTokenFaultKind.DescriptorDecodeFault,
            "test fault",
            descriptor.DescriptorReference.DescriptorAddress,
            isWrite: false);

        Assert.False(store.TryCancel(
            faultedAdmission.Handle,
            descriptor.OwnerBinding,
            DmaStreamComputeTokenCancelReason.Flush));
        Assert.Equal(DmaStreamComputeTokenState.Faulted, faultedToken.State);
    }

    [Fact]
    public void TokenStore_CapacityQuotaAndPressureRejectBeforeTokenCreation()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        var microOp = new DmaStreamComputeMicroOp(descriptor);

        var capacityStore = new DmaStreamComputeTokenStore(
            new DmaStreamComputeTokenStoreOptions(
                activeTokenCapacity: 1,
                perDomainTokenQuota: 1));
        Assert.True(capacityStore.TryAllocateAtIssueAdmission(
            DmaStreamComputeIssueAdmissionRequest.ForLane6(
                microOp,
                DmaStreamComputeIssueAdmissionMetadata.Lane6(bundleId: 1))).IsAccepted);

        DmaStreamComputeIssueAdmissionResult capacityReject =
            capacityStore.TryAllocateAtIssueAdmission(
                DmaStreamComputeIssueAdmissionRequest.ForLane6(
                    microOp,
                    DmaStreamComputeIssueAdmissionMetadata.Lane6(bundleId: 2)));

        Assert.False(capacityReject.IsAccepted);
        Assert.False(capacityReject.HasAllocatedToken);
        Assert.Equal(1, capacityStore.ActiveTokenCount);
        Assert.Equal(DmaStreamComputeIssueAdmissionRejectKind.StoreCapacity, capacityReject.RejectKind);
        Assert.Equal(DmaStreamComputeValidationFault.TokenCapAdmissionReject, capacityReject.ValidationFault);

        var quotaStore = new DmaStreamComputeTokenStore(
            new DmaStreamComputeTokenStoreOptions(
                activeTokenCapacity: 2,
                perDomainTokenQuota: 1));
        Assert.True(quotaStore.TryAllocateAtIssueAdmission(
            DmaStreamComputeIssueAdmissionRequest.ForLane6(
                microOp,
                DmaStreamComputeIssueAdmissionMetadata.Lane6(bundleId: 1))).IsAccepted);

        DmaStreamComputeIssueAdmissionResult quotaReject =
            quotaStore.TryAllocateAtIssueAdmission(
                DmaStreamComputeIssueAdmissionRequest.ForLane6(
                    microOp,
                    DmaStreamComputeIssueAdmissionMetadata.Lane6(bundleId: 2)));

        Assert.False(quotaReject.IsAccepted);
        Assert.False(quotaReject.HasAllocatedToken);
        Assert.Equal(1, quotaStore.ActiveTokenCount);
        Assert.Equal(DmaStreamComputeIssueAdmissionRejectKind.DomainQuota, quotaReject.RejectKind);
        Assert.Equal(DmaStreamComputeValidationFault.QuotaAdmissionReject, quotaReject.ValidationFault);

        DmaStreamComputePressurePolicy pressurePolicy = DmaStreamComputePressurePolicy.Default;
        DmaStreamComputeIssueAdmissionResult pressureReject =
            new DmaStreamComputeTokenStore().TryAllocateAtIssueAdmission(
                new DmaStreamComputeIssueAdmissionRequest(
                    microOp,
                    descriptor.OwnerGuardDecision,
                    DmaStreamComputeIssueAdmissionMetadata.Lane6(),
                    pressurePolicy,
                    new DmaStreamComputePressureSnapshot(
                        lane6Available: false,
                        dmaCreditsAvailable: 1,
                        srfCreditsAvailable: 1,
                        memorySubsystemCreditsAvailable: 1,
                        outstandingTokens: 0)));

        Assert.False(pressureReject.IsAccepted);
        Assert.False(pressureReject.HasAllocatedToken);
        Assert.Equal(DmaStreamComputeIssueAdmissionRejectKind.Pressure, pressureReject.RejectKind);
        Assert.Equal(DmaStreamComputePressureRejectKind.Lane6Unavailable, pressureReject.PressureRejectKind);
    }

    [Fact]
    public void TokenStore_RuntimeHelperTokensRemainModelOnlyAndOutsideStore()
    {
        DmaStreamComputeTelemetryTests.InitializeMainMemory(0x10000);
        DmaStreamComputeTelemetryTests.WriteUInt32Array(0x1000, 1, 2, 3, 4);
        DmaStreamComputeTelemetryTests.WriteMemory(0x9000, DmaStreamComputeTelemetryTests.Fill(0x11, 16));
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTelemetryTests.ParseValid(
                DmaStreamComputeTelemetryTests.BuildDescriptor(DmaStreamComputeOperationKind.Copy));
        var store = new DmaStreamComputeTokenStore();

        DmaStreamComputeExecutionResult execution =
            DmaStreamComputeRuntime.ExecuteToCommitPending(descriptor);

        Assert.True(execution.IsCommitPending);
        Assert.Equal(DmaStreamComputeTokenState.CommitPending, execution.Token.State);
        Assert.Equal(0, store.ActiveTokenCount);

        var helperLikeHandle = new DmaStreamComputeTokenHandle(
            execution.Token.TokenId,
            descriptor.OwnerBinding.OwnerVirtualThreadId,
            descriptor.OwnerBinding.OwnerContextId,
            descriptor.OwnerBinding.OwnerCoreId,
            descriptor.OwnerBinding.OwnerPodId,
            descriptor.OwnerBinding.OwnerDomainTag,
            descriptor.OwnerBinding.DeviceId,
            store.Generation);

        Assert.False(store.TryGet(helperLikeHandle, descriptor.OwnerBinding, out _));
    }

    [Fact]
    public void TokenStore_CancelBeforeCommitPreventsMemoryMutation()
    {
        DmaStreamComputeTelemetryTests.InitializeMainMemory(0x10000);
        byte[] original = DmaStreamComputeTelemetryTests.Fill(0x22, 16);
        byte[] staged = DmaStreamComputeTelemetryTests.Fill(0xA5, 16);
        DmaStreamComputeTelemetryTests.WriteMemory(0x9000, original);
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTelemetryTests.ParseValid(
                DmaStreamComputeTelemetryTests.BuildDescriptor(DmaStreamComputeOperationKind.Copy));
        var microOp = new DmaStreamComputeMicroOp(descriptor);
        var store = new DmaStreamComputeTokenStore();

        DmaStreamComputeIssueAdmissionResult admission =
            store.TryAllocateAtIssueAdmission(
                DmaStreamComputeIssueAdmissionRequest.ForLane6(
                    microOp,
                    DmaStreamComputeIssueAdmissionMetadata.Lane6()));
        DmaStreamComputeToken token = admission.Token!;
        token.MarkIssued();
        token.MarkReadsComplete();
        token.StageDestinationWrite(0x9000, staged);
        Assert.False(token.MarkComputeComplete().RequiresRetireExceptionPublication);

        Assert.True(store.TryCancel(
            admission.Handle,
            descriptor.OwnerBinding,
            DmaStreamComputeTokenCancelReason.ReplayDiscard));
        DmaStreamComputeCommitResult commit =
            token.Commit(Processor.MainMemory, descriptor.OwnerGuardDecision);

        Assert.False(commit.Succeeded);
        Assert.True(commit.IsCanceled);
        Assert.Equal(DmaStreamComputeTokenState.Canceled, token.State);
        Assert.Equal(original, DmaStreamComputeTelemetryTests.ReadMemory(0x9000, 16));
    }

    [Fact]
    public void TokenStore_DoesNotWireDecodeExecuteRuntimeOrParserGate()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string microOpText = File.ReadAllText(Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "MicroOps",
            "DmaStreamComputeMicroOp.cs"));
        string runtimeText = File.ReadAllText(Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Execution",
            "DmaStreamCompute",
            "DmaStreamComputeRuntime.cs"));

        Assert.DoesNotContain("DmaStreamComputeRuntime.ExecuteToCommitPending", microOpText, StringComparison.Ordinal);
        Assert.DoesNotContain("DmaStreamComputeTokenStore", runtimeText, StringComparison.Ordinal);
        Assert.False(DmaStreamComputeDescriptorParser.ExecutionEnabled);

        string[] unexpectedStoreReferences = CompatFreezeScanner.ScanProductionFilesForPatterns(
            repoRoot,
            new[] { "DmaStreamComputeTokenStore" },
            new[]
            {
                Path.Combine(
                    "HybridCPU_ISE",
                    "Core",
                    "Execution",
                    "DmaStreamCompute",
                    "DmaStreamComputeTokenStore.cs")
            },
            new[] { "HybridCPU_ISE" });

        Assert.Empty(unexpectedStoreReferences);
    }

    private static DmaStreamComputeIssueAdmissionResult AllocateStoreToken(
        DmaStreamComputeTokenStore store,
        DmaStreamComputeMicroOp microOp)
    {
        DmaStreamComputeIssueAdmissionResult admission =
            store.TryAllocateAtIssueAdmission(
                DmaStreamComputeIssueAdmissionRequest.ForLane6(
                    microOp,
                    DmaStreamComputeIssueAdmissionMetadata.Lane6()));
        Assert.True(admission.IsAccepted, admission.Message);
        return admission;
    }

    private static DmaStreamComputeOwnerGuardDecision CreateIssueGuardDecisionForTest(
        DmaStreamComputeDescriptor descriptor,
        LegalityDecision legalityDecision,
        string message)
    {
        ConstructorInfo? constructor =
            typeof(DmaStreamComputeOwnerGuardDecision).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                new[]
                {
                    typeof(DmaStreamComputeOwnerBinding),
                    typeof(DmaStreamComputeOwnerGuardContext),
                    typeof(LegalityDecision),
                    typeof(string)
                },
                modifiers: null);

        Assert.NotNull(constructor);
        return (DmaStreamComputeOwnerGuardDecision)constructor!.Invoke(
            new object[]
            {
                descriptor.OwnerBinding,
                descriptor.OwnerGuardDecision.RuntimeOwnerContext,
                legalityDecision,
                message
            });
    }
}
