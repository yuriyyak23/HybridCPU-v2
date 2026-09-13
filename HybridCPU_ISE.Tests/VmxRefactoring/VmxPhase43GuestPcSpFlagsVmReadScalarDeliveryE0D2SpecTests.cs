using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase43GuestPcSpFlagsVmReadScalarDeliveryE0D2SpecTests
{
    [Fact]
    public void E0_FreezesCanonicalSourceBoundaryAndRecordsEpochGapWithoutAuthority()
    {
        Assert.Equal(Enumerable.Range(1, 15).Select(value => (byte)value),
            Phase43GuestPcSpFlagsVmReadScalarDeliveryE0Contract.Findings.Select(item => item.Number));
        Assert.True(Phase43GuestPcSpFlagsVmReadScalarDeliveryE0Contract.ExactFieldIds.SequenceEqual(
            [(ushort)VmcsField.GuestPc, (ushort)VmcsField.GuestSp, (ushort)VmcsField.GuestFlags]));
        Assert.Contains(Phase43GuestPcSpFlagsVmReadScalarDeliveryE0Contract.Findings,
            finding => finding.Name == "EpochGap" && finding.ExactContract.Contains("DoesNotValidateCurrentEpoch"));
        Assert.False(Phase43GuestPcSpFlagsVmReadScalarDeliveryE0Contract.RuntimeAuthorityGranted);
        Assert.False(Phase43GuestPcSpFlagsVmReadScalarDeliveryE0Contract.SourceValueAvailable);
        Assert.False(Phase43GuestPcSpFlagsVmReadScalarDeliveryE0Contract.ResultReceiptIssued);
        Assert.False(Phase43GuestPcSpFlagsVmReadScalarDeliveryE0Contract.RegisterWritebackAuthorized);
        Assert.False(Phase43GuestPcSpFlagsVmReadScalarDeliveryE0Contract.RetireCommitAuthorized);
        Assert.False(Phase43GuestPcSpFlagsVmReadScalarDeliveryE0Contract.BackendExecutionAuthorized);
        Assert.False(Phase43GuestPcSpFlagsVmReadScalarDeliveryE0Contract.CompletionPublicationAuthorized);
        Assert.False(Phase43GuestPcSpFlagsVmReadScalarDeliveryE0Contract.ProductionCompositionAuthorized);
    }

    [Fact]
    public void Spec_IsExactOwnerSpecificGovernanceOnlyProfile()
    {
        VirtualizationDecisionSpecV2 spec =
            Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionSpecV2.Instance;
        Assert.Equal(GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.ExpectedDecisionId, spec.DecisionId);
        Assert.Equal("DELIVER_GUEST_PC_SP_FLAGS_SCALAR_V1", spec.OperationId);
        Assert.Equal(VirtualizationDecisionAuthorityPlaneV2.ExecutionDomainReadOnlyStateCanonicalRegisterDelivery,
            spec.AuthorityPlane);
        Assert.Equal(VirtualizationDecisionResultAbiV2.ScalarU64ToDestinationRegister, spec.ResultAbi);
        Assert.Equal(VirtualizationDecisionEffectClassV2.ArchitecturalRegisterResultOnly, spec.EffectClass);
        Assert.Equal(VirtualizationCapabilityRequirementV2.None, spec.CapabilityRequirement);
        Assert.Equal(VirtualizationOperationMigrationPolicyV2.DrainOnly, spec.OperationMigrationPolicy);
        Assert.Equal(VirtualizationRetirePolicyV2.CanonicalRetireCoordinatorArchitecturalRegisterCommit,
            spec.RetirePolicy);
        Assert.True(spec.RequiresMemoryDomain);
        Assert.True(spec.RequiresIoDomain);
        Assert.Contains("RuntimeBoundary=ReadCompatibilityProjection+FullDomainRuntimeUnchanged",
            spec.DependencyContract);
        Assert.Contains("Activation=DefaultDisabled", spec.DependencyContract);
        Assert.True(spec.ExactFieldIds!.Value.SequenceEqual(
            [(ushort)VmcsField.GuestPc, (ushort)VmcsField.GuestSp, (ushort)VmcsField.GuestFlags]));
        Assert.All(spec.OwnerMap, entry =>
        {
            Assert.Equal("ExecutionDomainDescriptor", entry.Owner);
            Assert.StartsWith("ExecutionDomainReadOnlyStateView.", entry.ValueSource);
            Assert.Equal("None", entry.CapabilityPolicy);
            Assert.Equal("DrainOnly", entry.MigrationClass);
        });
        Assert.True(GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.ValidateSpecShape(spec).IsExactPolicyShape);
    }

    [Fact]
    public void Validator_DeniesAdjacentSourcesCapabilitiesAndShortcutPolicies()
    {
        VirtualizationDecisionSpecV2 valid =
            Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionSpecV2.Instance;
        Assert.Equal(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedProfile,
            GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.ValidateSpecShape(valid with
            {
                ExactFieldIds = [.. valid.ExactFieldIds!.Value, (ushort)VmcsField.GuestCr3],
            }).Decision);
        Assert.Equal(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedProfile,
            GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.ValidateSpecShape(valid with
            {
                CapabilityRequirement = VirtualizationCapabilityRequirementV2.DomainGrantedVmCallProbeNoStateV1,
                CapabilityMask = 1UL << 41,
            }).Decision);
        Assert.Equal(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedProfile,
            GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.ValidateSpecShape(valid with
            {
                CompletionPolicy = VirtualizationCompletionPolicyV2.AtomicE3ToCompletionRecordAndE5,
            }).Decision);
        Assert.Equal(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedOwnerMap,
            GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.ValidateSpecShape(valid with
            {
                OwnerMap = [valid.OwnerMap[0] with { ValueSource = "VmcsV2Descriptor.BackingStore" },
                    valid.OwnerMap[1], valid.OwnerMap[2]],
            }).Decision);
    }

    [Fact]
    public void EarlierGuestCrDecisionsRemainByteStableAndDistinct()
    {
        Assert.Equal("52ce040b93f54b36a427c4269f2afff77b2e66f83ceda3ece1b1dc917a58241f",
            Phase40VmReadProjectionDecisionSpecV2.Instance.SpecDigest);
        Assert.Equal("ccda8698dbeb3f6eef1b4f13e22a3fb7607e939f493138fb7e3373674e234309",
            Phase41VmReadScalarDeliveryDecisionSpecV2.Instance.SpecDigest);
        Assert.NotEqual(Phase41VmReadScalarDeliveryDecisionSpecV2.Instance.DecisionId,
            Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionSpecV2.Instance.DecisionId);
    }

    [Fact]
    public void GovernancePackage_ContainsNoRuntimeShortcut()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string directory = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Governance", "Virtualization");
        string source = string.Concat(
            File.ReadAllText(Path.Combine(directory, "Phase43GuestPcSpFlagsVmReadScalarDeliveryE0Contract.cs")),
            File.ReadAllText(Path.Combine(directory, "Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionSpecV2.cs")),
            File.ReadAllText(Path.Combine(directory, "GuestPcSpFlagsVmReadScalarDeliveryDecisionValidatorV2.cs")));
        foreach (string forbidden in new[]
        {
            "PhysicalRegisters.Write(", "CommittedRegs[", "RetireCoordinator.Retire(",
            "VmxRetireEffect.VmcsRead(", "DomainHypercallCompletionPublicationResult",
            "BackendExecutionAuthorized => true", "RegisterWritebackAuthorized => true",
            "RetireCommitAuthorized => true", "ProductionCompositionAuthorized => true",
        })
            Assert.DoesNotContain(forbidden, source);
    }
}
