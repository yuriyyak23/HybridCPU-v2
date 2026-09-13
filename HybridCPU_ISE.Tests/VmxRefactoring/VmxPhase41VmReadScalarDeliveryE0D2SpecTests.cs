using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase41VmReadScalarDeliveryE0D2SpecTests
{
    [Fact]
    public void E0_DefinesSeparateAttemptBoundScalarDeliveryWithoutAuthority()
    {
        Assert.Equal(Enumerable.Range(1, 14).Select(value => (byte)value),
            Phase41VmReadScalarDeliveryE0Contract.Findings.Select(item => item.Number));
        Assert.True(Phase41VmReadScalarDeliveryE0Contract.ExactFieldIds.SequenceEqual(
            [(ushort)VmcsField.GuestCr0, (ushort)VmcsField.GuestCr4]));
        Assert.False(Phase41VmReadScalarDeliveryE0Contract.RuntimeAuthorityGranted);
        Assert.False(Phase41VmReadScalarDeliveryE0Contract.SourceValueAvailable);
        Assert.False(Phase41VmReadScalarDeliveryE0Contract.ResultReceiptIssued);
        Assert.False(Phase41VmReadScalarDeliveryE0Contract.RegisterWritebackAuthorized);
        Assert.False(Phase41VmReadScalarDeliveryE0Contract.RetireCommitAuthorized);
        Assert.False(Phase41VmReadScalarDeliveryE0Contract.BackendExecutionAuthorized);
        Assert.False(Phase41VmReadScalarDeliveryE0Contract.UnderlyingVirtualizationMutationAuthorized);
        Assert.False(Phase41VmReadScalarDeliveryE0Contract.CompletionPublicationAuthorized);
    }

    [Fact]
    public void Spec_IsExactSeparateScalarDeliveryProfile()
    {
        VirtualizationDecisionSpecV2 spec =
            Phase41VmReadScalarDeliveryDecisionSpecV2.Instance;

        Assert.Equal(VmReadScalarDeliveryDecisionValidatorV2.ExpectedDecisionId, spec.DecisionId);
        Assert.NotEqual(VmReadProjectionDecisionValidatorV2.ExpectedDecisionId, spec.DecisionId);
        Assert.Equal(VmReadScalarDeliveryDecisionValidatorV2.ExpectedOperationNamespace,
            spec.OperationNamespace);
        Assert.Equal(VmReadScalarDeliveryDecisionValidatorV2.ExpectedOperationId, spec.OperationId);
        Assert.Equal(VirtualizationDecisionResultAbiV2.ScalarU64ToDestinationRegister,
            spec.ResultAbi);
        Assert.Equal(VirtualizationDecisionEffectClassV2.ArchitecturalRegisterResultOnly,
            spec.EffectClass);
        Assert.Equal(VirtualizationOperationMigrationPolicyV2.DrainOnly,
            spec.OperationMigrationPolicy);
        Assert.Equal(VirtualizationCancellationPolicyV2.SquashBeforeRetireZeroArchitecturalEffect,
            spec.CancellationPolicy);
        Assert.Equal(VirtualizationReplayPolicyV2.AttemptBoundReceiptNoReplayReuse,
            spec.ReplayPolicy);
        Assert.Equal(VirtualizationCompletionPolicyV2.None, spec.CompletionPolicy);
        Assert.Equal(VirtualizationRetirePolicyV2.CanonicalRetireCoordinatorArchitecturalRegisterCommit,
            spec.RetirePolicy);
        Assert.Equal(VirtualizationCapabilityRequirementV2.None, spec.CapabilityRequirement);
        Assert.Equal(0UL, spec.CapabilityMask);
        Assert.False(spec.RequiresTypedGrant);
        Assert.Equal(VirtualizationDecisionMutationClassV2.UnderlyingVirtualizationStateReadOnly,
            spec.MutationClass);
        Assert.Contains("OpaqueVmReadScalarResultReceipt=SingleUse", spec.DependencyContract);
        Assert.Contains("NoVMCALLE5E6", spec.DependencyContract);
        Assert.True(spec.ExactFieldIds!.Value.SequenceEqual(
            [(ushort)VmcsField.GuestCr0, (ushort)VmcsField.GuestCr4]));
        Assert.All(spec.OwnerMap, entry =>
        {
            Assert.Equal("PrivilegedExecutionStateOwnerPolicy", entry.Owner);
            Assert.Equal("None", entry.CapabilityPolicy);
            Assert.Equal("DrainOnly", entry.MigrationClass);
        });
        Assert.True(VmReadScalarDeliveryDecisionValidatorV2.ValidateSpecShape(spec)
            .IsExactPolicyShape);
        Assert.Equal(spec.SpecDigest,
            VirtualizationDecisionCanonicalEncoderV2.ComputeSpecDigest(spec));
    }

    [Fact]
    public void Spec_DoesNotChangeAcceptedPhase40ProjectionBytes()
    {
        Assert.Equal("52ce040b93f54b36a427c4269f2afff77b2e66f83ceda3ece1b1dc917a58241f",
            Phase40VmReadProjectionDecisionSpecV2.Instance.SpecDigest);
        Assert.Equal(VirtualizationDecisionEffectClassV2.ReadOnlyProjectionNoStateMutation,
            Phase40VmReadProjectionDecisionSpecV2.Instance.EffectClass);
        Assert.Equal(VirtualizationRetirePolicyV2.None,
            Phase40VmReadProjectionDecisionSpecV2.Instance.RetirePolicy);
    }

    [Fact]
    public void Validator_DeniesBroadOrReusableProfiles()
    {
        VirtualizationDecisionSpecV2 valid =
            Phase41VmReadScalarDeliveryDecisionSpecV2.Instance;

        Assert.Equal(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedProfile,
            VmReadScalarDeliveryDecisionValidatorV2.ValidateSpecShape(valid with
            {
                ExactFieldIds = [(ushort)VmcsField.GuestCr0, (ushort)VmcsField.GuestCr3,
                    (ushort)VmcsField.GuestCr4],
            }).Decision);
        Assert.Equal(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedProfile,
            VmReadScalarDeliveryDecisionValidatorV2.ValidateSpecShape(valid with
            {
                ReplayPolicy = VirtualizationReplayPolicyV2.DenyAttemptReplay,
            }).Decision);
        Assert.Equal(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedProfile,
            VmReadScalarDeliveryDecisionValidatorV2.ValidateSpecShape(valid with
            {
                CompletionPolicy = VirtualizationCompletionPolicyV2.AtomicE3ToCompletionRecordAndE5,
            }).Decision);
        Assert.Equal(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedProfile,
            VmReadScalarDeliveryDecisionValidatorV2.ValidateSpecShape(valid with
            {
                RetirePolicy = VirtualizationRetirePolicyV2.PreciseE5BoundNoStateRetire,
            }).Decision);
    }

    [Fact]
    public void GovernancePackage_ContainsNoRuntimeShortcut()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string directory = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Runtime", "Governance", "Virtualization");
        string source = string.Concat(
            File.ReadAllText(Path.Combine(directory,
                "Phase41VmReadScalarDeliveryE0Contract.cs")),
            File.ReadAllText(Path.Combine(directory,
                "Phase41VmReadScalarDeliveryDecisionSpecV2.cs")),
            File.ReadAllText(Path.Combine(directory,
                "VmReadScalarDeliveryDecisionValidatorV2.cs")));

        foreach (string forbidden in new[]
        {
            "PhysicalRegisters.Write(",
            "CommittedRegs[",
            "RetireCoordinator.Retire(",
            "VmxRetireEffect.VmcsRead(",
            "DomainHypercallCompletionPublicationResult",
            "ExactHypercallRetireGrant",
            "BackendExecutionAuthorized => true",
            "RegisterWritebackAuthorized => true",
            "RetireCommitAuthorized => true",
        })
        {
            Assert.DoesNotContain(forbidden, source);
        }
    }
}
