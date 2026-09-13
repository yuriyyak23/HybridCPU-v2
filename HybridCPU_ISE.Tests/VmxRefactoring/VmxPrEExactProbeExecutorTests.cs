using System.Reflection;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPrEExactProbeExecutorTests
{
    [Fact]
    public void Executor_DefaultsOff_AndDoesNotConsumeE2()
    {
        Fixture fixture = CreateFixture();
        var executor = new DomainHypercallRuntimeExecutor();

        DomainHypercallExecutionResult result =
            executor.ExecuteExactProbe(fixture.Verifier, fixture.E2, fixture.RestoreOwner, fixture.LifecycleGate);

        Assert.Equal(ExactProbeExecutionMode.Disabled, executor.Mode);
        Assert.Equal(DomainHypercallExecutionDecision.Disabled, result.Decision);
        Assert.Null(result.Receipt);
        Assert.Equal(VirtualizationE2State.Issued, fixture.Verifier.GetVirtualizationE2State(fixture.E2));
        Assert.True(fixture.Verifier.ValidateVirtualizationE2(fixture.E2, fixture.RestoreOwner).IsLive);
    }

    [Fact]
    public void EnabledExecutor_ConsumesExactE2Once_AndReturnsOpaqueNoEffectNoResultE3()
    {
        Fixture fixture = CreateFixture();
        var executor = new DomainHypercallRuntimeExecutor(ExactProbeExecutionMode.ExactProbeOnly);

        DomainHypercallExecutionResult result =
            executor.ExecuteExactProbe(fixture.Verifier, fixture.E2, fixture.RestoreOwner, fixture.LifecycleGate);
        DomainHypercallRuntimeExecutor.ExecutionReceipt receipt =
            Assert.IsType<DomainHypercallRuntimeExecutor.ExecutionReceipt>(result.Receipt);

        Assert.True(result.IsExecuted);
        Assert.Equal(VirtualizationE2State.ConsumedByExecutor, fixture.Verifier.GetVirtualizationE2State(fixture.E2));
        Assert.True(executor.ValidateReceipt(receipt, fixture.RestoreOwner).IsValid);
        Assert.Equal(fixture.E2.CertificateDigest, receipt.E2Digest);
        Assert.Equal(fixture.E2.AttemptId, receipt.AttemptId);
        Assert.Equal(VirtualizationDecisionValidatorV2.ExpectedDecisionId, receipt.DecisionId);
        Assert.Equal(VirtualizationDecisionValidatorV2.ExpectedOwnerId, receipt.OwnerId);
        Assert.Equal("HybridCPU.VMCALL.Runtime.v1", receipt.OperationNamespace);
        Assert.Equal("PROBE_NO_STATE_V1", receipt.OperationId);
        Assert.Equal((ushort)1, receipt.NumericLeaf);
        Assert.Equal(VirtualizationDecisionEffectClassV2.NoStateNoPayload, receipt.EffectClass);
        Assert.Equal(VirtualizationDecisionResultAbiV2.NoPayload, receipt.ResultAbi);
        Assert.Equal(DomainHypercallExecutionReceiptDigest.NoEffectDigest, receipt.EffectDigest);
        Assert.Equal(DomainHypercallExecutionReceiptDigest.NoResultDigest, receipt.ResultDigest);
        Assert.NotEqual(new string('0', 64), receipt.EffectDigest);
        Assert.NotEqual(new string('0', 64), receipt.ResultDigest);
        Assert.False(receipt.HasPayload);
        Assert.False(receipt.HasStateEffect);
        Assert.False(receipt.CompletionPublicationAuthorized);
        Assert.False(receipt.RetirePublicationAuthorized);
        Assert.False(result.CompletionPublicationAuthorized);
        Assert.False(result.RetirePublicationAuthorized);
    }

    [Fact]
    public void Executor_DeniesDuplicateStaleRevokedAndAdjacentAdmission()
    {
        Fixture duplicate = CreateFixture();
        var executor = new DomainHypercallRuntimeExecutor(ExactProbeExecutionMode.ExactProbeOnly);
        Assert.True(executor.ExecuteExactProbe(
            duplicate.Verifier, duplicate.E2, duplicate.RestoreOwner, duplicate.LifecycleGate).IsExecuted);
        Assert.Equal(
            DomainHypercallExecutionDecision.InvalidAdmission,
            executor.ExecuteExactProbe(duplicate.Verifier, duplicate.E2, duplicate.RestoreOwner, duplicate.LifecycleGate).Decision);

        Fixture revoked = CreateFixture();
        revoked.CapabilityOwner.RevokeAll();
        Assert.Equal(
            DomainHypercallExecutionDecision.InvalidAdmission,
            executor.ExecuteExactProbe(revoked.Verifier, revoked.E2, revoked.RestoreOwner, revoked.LifecycleGate).Decision);

        Fixture stale = CreateFixture();
        stale.RestoreOwner.AdvanceAfterRestore();
        Assert.Equal(
            DomainHypercallExecutionDecision.InvalidAdmission,
            executor.ExecuteExactProbe(stale.Verifier, stale.E2, stale.RestoreOwner, stale.LifecycleGate).Decision);

        Fixture adjacent = CreateFixture();
        SafetyVerifier.VirtualizationOperationAdmissionCertificate forgedAdjacent =
            CloneE2(adjacent.E2, numericLeaf: 2);
        Assert.Equal(
            DomainHypercallExecutionDecision.OperationBindingMismatch,
            executor.ExecuteExactProbe(adjacent.Verifier, forgedAdjacent, adjacent.RestoreOwner, adjacent.LifecycleGate).Decision);
        Assert.Equal(VirtualizationE2State.Issued, adjacent.Verifier.GetVirtualizationE2State(adjacent.E2));
    }

    [Fact]
    public async Task ConcurrentExecution_ProducesExactlyOneReceiptAndOneConsumption()
    {
        Fixture fixture = CreateFixture();
        var executor = new DomainHypercallRuntimeExecutor(ExactProbeExecutionMode.ExactProbeOnly);

        DomainHypercallExecutionResult[] results = await Task.WhenAll(
            Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
                executor.ExecuteExactProbe(fixture.Verifier, fixture.E2, fixture.RestoreOwner, fixture.LifecycleGate))));

        Assert.Single(results, result => result.IsExecuted);
        Assert.Equal(15, results.Count(result =>
            result.Decision == DomainHypercallExecutionDecision.InvalidAdmission));
        Assert.Equal(VirtualizationE2State.ConsumedByExecutor, fixture.Verifier.GetVirtualizationE2State(fixture.E2));
    }

    [Fact]
    public void Receipt_IsNonForgeableOwnerBoundAndRestoreInvalidated()
    {
        Fixture fixture = CreateFixture();
        var executor = new DomainHypercallRuntimeExecutor(ExactProbeExecutionMode.ExactProbeOnly);
        DomainHypercallRuntimeExecutor.ExecutionReceipt receipt =
            Assert.IsType<DomainHypercallRuntimeExecutor.ExecutionReceipt>(
                executor.ExecuteExactProbe(fixture.Verifier, fixture.E2, fixture.RestoreOwner, fixture.LifecycleGate).Receipt);

        ConstructorInfo constructor = Assert.Single(
            typeof(DomainHypercallRuntimeExecutor.ExecutionReceipt)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.True(constructor.IsPrivate);
        Assert.Empty(typeof(DomainHypercallRuntimeExecutor.ExecutionReceipt).GetConstructors());
        Assert.DoesNotContain(
            typeof(DomainHypercallRuntimeExecutor.ExecutionReceipt)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            property => property.SetMethod is not null);

        var foreign = new DomainHypercallRuntimeExecutor(ExactProbeExecutionMode.ExactProbeOnly);
        Assert.Equal(
            DomainHypercallReceiptValidationDecision.ForeignIssuer,
            foreign.ValidateReceipt(receipt, fixture.RestoreOwner).Decision);
        fixture.RestoreOwner.AdvanceAfterRestore();
        Assert.Equal(
            DomainHypercallReceiptValidationDecision.RestoreGenerationMismatch,
            executor.ValidateReceipt(receipt, fixture.RestoreOwner).Decision);
    }

    private static Fixture CreateFixture()
    {
        var verifier = new SafetyVerifier();
        VmxMicroOp carrier = CreateCarrier();
        ReplayPhaseContext replay = new(
            true, 17, 0x4000, 1, 0, 0, 0, ReplayPhaseInvalidationReason.None);
        SmtBundleMetadata4Way bundle = new(0, 42, 7, 7, 7, 1);
        SafetyVerifier.VirtualizationAdmissionCertificate e1 =
            Assert.IsType<SafetyVerifier.VirtualizationAdmissionCertificate>(
                verifier.IssueVirtualizationAdmissionAfterStageB(replay, bundle, carrier, 7, 7).Certificate);
        carrier.AttachVirtualizationAdmission(e1);
        VirtualizationOperationOwnerSnapshot o1 =
            Phase38VirtualizationOperationOwnerSnapshotRegistry.ExactSnapshot;
        VirtualizationOperandSnapshot operand = Assert.IsType<VirtualizationOperandSnapshot>(
            new VirtualizationOperandSnapshotMaterializer()
                .CaptureAfterValidatedE1(carrier, e1, 1, 1, o1).Snapshot);
        carrier.AttachVirtualizationOperandSnapshot(operand);

        CapabilityGrant grant = new(
            RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
            CapabilityGrantScope.DomainGranted,
            true, 7,
            CapabilityDelegationPolicy.NonDelegable,
            CapabilityRevocationPolicy.RuntimeRevocable,
            CapabilityMigrationClass.DomainLocal,
            CapabilityEvidenceVisibility.HostOnly,
            CapabilityFrontendProjectionPolicy.NeverProject);
        var capabilityOwner = new RuntimeCapabilityGrantOwner();
        RuntimeCapabilityGrantLease lease = capabilityOwner.Issue(grant);
        DomainRuntimeContext domain = new(
            new ExecutionDomainDescriptor(
                7, new YAKSys_Hybrid_CPU.Core.BundleLegalityDescriptor(), null, null, false),
            null, null,
            new CapabilityDescriptorSet(new CapabilityGrantCollection([grant])),
            null, 7, 0);
        RootAuthorityDescriptor root = new(
            RootAuthorityClass.RuntimeRoot, 1,
            RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
            false, false);
        var restoreOwner = new VirtualizationRestoreGenerationOwner();
        var lifecycleGate = new DomainHypercallLifecycleGate(7);
        Assert.True(lifecycleGate.TryActivateExact(DomainHypercallExactActivationRequest.Phase38Exact));
        VirtualizationE2IssueRequest request = new(
            replay, bundle, carrier, 7, 7, e1, o1, operand, domain, root,
            capabilityOwner, lease, restoreOwner, lifecycleGate);
        SafetyVerifier.VirtualizationOperationAdmissionCertificate e2 =
            Assert.IsType<SafetyVerifier.VirtualizationOperationAdmissionCertificate>(
                verifier.IssueVirtualizationE2(request).Certificate);
        return new(verifier, e2, capabilityOwner, restoreOwner, lifecycleGate);
    }

    private static SafetyVerifier.VirtualizationOperationAdmissionCertificate CloneE2(
        SafetyVerifier.VirtualizationOperationAdmissionCertificate source,
        ushort numericLeaf)
    {
        ConstructorInfo constructor = Assert.Single(
            typeof(SafetyVerifier.VirtualizationOperationAdmissionCertificate)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic));
        return Assert.IsType<SafetyVerifier.VirtualizationOperationAdmissionCertificate>(
            constructor.Invoke(new object[]
            {
                new object(), source.IssuanceSequence, source.AttemptId,
                source.E1IssuerGeneration, source.VirtualThreadId, source.OwnerContextId,
                source.DomainTag, source.BundleIdentity, source.ReplayEpoch, source.DecisionId,
                source.SpecDigest, source.AcceptanceDigest, source.OwnerId,
                source.OwnerPolicyVersion, source.OwnerEpoch, source.OperationNamespace,
                source.OperationId, numericLeaf, source.OwnerPolicyDigest, source.OperandDigest,
                source.CapabilityGrantIdentity, source.CapabilityGeneration,
                source.RootAuthorityEpoch, source.RestoreGeneration, source.CertificateDigest,
            }));
    }

    private static VmxMicroOp CreateCarrier()
    {
        var carrier = new VmxMicroOp
        {
            OpCode = IsaOpcodeValues.VMCALL,
            OwnerThreadId = 0,
            VirtualThreadId = 0,
            OwnerContextId = 42,
            Rs1 = 5,
            Rs2 = 0,
            Rd = 0,
            Instruction = new InstructionIR
            {
                CanonicalOpcode = new IsaOpcode(IsaOpcodeValues.VMCALL),
                Class = InstructionClass.Vmx,
                SerializationClass = SerializationClass.VmxSerial,
                Rs1 = 5,
                Rs2 = 0,
                Rd = 0,
                Imm = 0,
            },
        };
        carrier.Placement = carrier.Placement with { DomainTag = 7 };
        carrier.RefreshWriteMetadata();
        return carrier;
    }

    private sealed record Fixture(
        SafetyVerifier Verifier,
        SafetyVerifier.VirtualizationOperationAdmissionCertificate E2,
        RuntimeCapabilityGrantOwner CapabilityOwner,
        VirtualizationRestoreGenerationOwner RestoreOwner,
        DomainHypercallLifecycleGate LifecycleGate);
}
