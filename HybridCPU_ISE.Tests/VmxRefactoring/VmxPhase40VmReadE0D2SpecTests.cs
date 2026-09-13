using System.Collections.Immutable;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase40VmReadE0D2SpecTests
{
    [Fact]
    public void E0_RecordsAllTwelveExactFindingsWithoutAuthority()
    {
        Assert.Equal(Enumerable.Range(1, 12).Select(value => (byte)value),
            Phase40VmReadProjectionE0Contract.Findings.Select(item => item.Number));
        Assert.True(Phase40VmReadProjectionE0Contract.ExactFieldIds.SequenceEqual(
            [(ushort)VmcsField.GuestCr0, (ushort)VmcsField.GuestCr4]));
        Assert.False(Phase40VmReadProjectionE0Contract.RuntimeAuthorityGranted);
        Assert.False(Phase40VmReadProjectionE0Contract.ProjectionValueAvailable);
        Assert.False(Phase40VmReadProjectionE0Contract.CapabilityGranted);
        Assert.False(Phase40VmReadProjectionE0Contract.BackendExecutionAuthorized);
        Assert.False(Phase40VmReadProjectionE0Contract.MutationAuthorized);
        Assert.False(Phase40VmReadProjectionE0Contract.CompletionPublicationAuthorized);
        Assert.False(Phase40VmReadProjectionE0Contract.RetirePublicationAuthorized);
    }

    [Fact]
    public void Spec_IsOneExactTwoFieldReadOnlyProfileAndNotProbeAuthority()
    {
        VirtualizationDecisionSpecV2 spec = Phase40VmReadProjectionDecisionSpecV2.Instance;

        Assert.Equal(VmReadProjectionDecisionValidatorV2.ExpectedDecisionId, spec.DecisionId);
        Assert.Equal(VmReadProjectionDecisionValidatorV2.ExpectedOperationNamespace, spec.OperationNamespace);
        Assert.Equal(VmReadProjectionDecisionValidatorV2.ExpectedOperationId, spec.OperationId);
        Assert.NotEqual(VirtualizationDecisionValidatorV2.ExpectedDecisionId, spec.DecisionId);
        Assert.NotEqual(VirtualizationDecisionValidatorV2.ExpectedOperationNamespace, spec.OperationNamespace);
        Assert.NotEqual(VirtualizationDecisionValidatorV2.ExpectedOperationId, spec.OperationId);
        Assert.Equal((ushort)0, spec.LeafWidth);
        Assert.Equal((ushort)0, spec.NumericLeaf);
        Assert.Equal(VmReadProjectionDecisionValidatorV2.ExpectedOwnerId, spec.OwnerId);
        Assert.Equal(VirtualizationCapabilityRequirementV2.None, spec.CapabilityRequirement);
        Assert.Equal(0UL, spec.CapabilityMask);
        Assert.False(spec.RequiresTypedGrant);
        Assert.Equal(VirtualizationDecisionMutationClassV2.ReadOnly, spec.MutationClass);
        Assert.Equal(VirtualizationDecisionEffectClassV2.ReadOnlyProjectionNoStateMutation, spec.EffectClass);
        Assert.Equal(VirtualizationOperationMigrationPolicyV2.RevalidatedAfterRestore, spec.OperationMigrationPolicy);
        Assert.Equal(VirtualizationCompletionPolicyV2.None, spec.CompletionPolicy);
        Assert.Equal(VirtualizationRetirePolicyV2.None, spec.RetirePolicy);
        Assert.Equal(VirtualizationAdjacentLeafPolicyV2.DenyAllExceptExactFieldSet, spec.AdjacentLeafPolicy);
        Assert.True(spec.ExactFieldIds is { } exactFieldIds &&
            exactFieldIds.SequenceEqual(Phase40VmReadProjectionE0Contract.ExactFieldIds));
        Assert.Equal(2, spec.OwnerMap.Length);
        Assert.All(spec.OwnerMap, entry =>
        {
            Assert.Equal("PrivilegedExecutionStateOwnerPolicy", entry.Owner);
            Assert.Equal("None", entry.CapabilityPolicy);
            Assert.Equal("RevalidatedAfterRestore", entry.MigrationClass);
        });

        Assert.True(VirtualizationDecisionCanonicalEncoderV2.IsCanonicalSha256(spec.SpecDigest));
        Assert.Equal("52ce040b93f54b36a427c4269f2afff77b2e66f83ceda3ece1b1dc917a58241f",
            spec.SpecDigest);
        Assert.Equal(VirtualizationDecisionCanonicalEncoderV2.ComputeSpecDigest(spec), spec.SpecDigest);
        Assert.True(VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(spec).AsSpan()
            .SequenceEqual(Phase40VmReadProjectionDecisionSpecV2.CanonicalBytes.AsSpan()));
        Assert.False(Phase40VmReadProjectionDecisionSpecV2.RuntimeAuthorityGranted);
        Assert.False(Phase40VmReadProjectionDecisionSpecV2.ProjectionValueAvailable);
        Assert.False(Phase40VmReadProjectionDecisionSpecV2.CapabilityGranted);
        Assert.False(Phase40VmReadProjectionDecisionSpecV2.BackendExecutionAuthorized);
        Assert.False(Phase40VmReadProjectionDecisionSpecV2.MutationAuthorized);
        Assert.False(Phase40VmReadProjectionDecisionSpecV2.CompletionPublicationAuthorized);
        Assert.False(Phase40VmReadProjectionDecisionSpecV2.RetirePublicationAuthorized);
    }

    [Fact]
    public void ProjectionProfileSuffix_DoesNotChangeAcceptedPhase38CanonicalBytes()
    {
        VirtualizationDecisionSpecV2 probe = Phase38VirtualizationDecisionSpecV2.Instance;

        Assert.Equal("33076e430fcbc05cf0774d08baadc6d7840f88029fcfb28a458558af82f93ca8",
            probe.SpecDigest);
        Assert.True(VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(probe).AsSpan()
            .SequenceEqual(Phase38VirtualizationDecisionSpecV2.CanonicalBytes.AsSpan()));
        Assert.Equal(VirtualizationDecisionOperationClassV2.Unspecified, probe.OperationClass);
        Assert.Null(probe.ExactFieldIds);
    }

    [Fact]
    public void OwnerPolicy_JointlyRejectsInvalidAdjacentMemberOfTheSemanticGroup()
    {
        PrivilegedExecutionStateDescriptor descriptor = CreateDescriptor() with
        {
            GuestCr4 = new PrivilegedControlRegisterValue(
                PrivilegedControlRegisterKind.GuestCr4,
                1UL << 63),
        };

        PrivilegedExecutionStateProjectionResult result =
            new PrivilegedExecutionStateProjectionService().Project(
                new(
                    PrivilegedControlRegisterKind.GuestCr0,
                    descriptor,
                    RuntimeDomainTag: 7,
                    RuntimeAddressSpaceTag: 9,
                    CurrentEpoch: new PrivilegedExecutionStateEpoch(11),
                    SecureVisibilityAllowed: true,
                    MigrationClassified: true,
                    ConformanceProven: true));

        Assert.False(result.IsAllowed);
        Assert.Equal(PrivilegedExecutionStateProjectionDecision.DeniedOwnerAdmission, result.Decision);
        Assert.Equal(PrivilegedExecutionStateOwnerDecision.DeniedGuestCr4ReservedBits,
            result.OwnerAdmission.Decision);
        Assert.False(result.ValueAvailable);
        Assert.False(result.BackendSuccessAuthorized);
        Assert.False(result.MutationAuthorized);
        Assert.False(result.CompletionPublicationAuthorized);
        Assert.False(result.RetirePublicationAuthorized);
    }

    [Fact]
    public void GovernanceSources_ExposeNoRuntimeProjectionOrProbeReuseShortcut()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string governance = File.ReadAllText(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Governance",
            "Virtualization", "Phase40VmReadProjectionDecisionSpecV2.cs"));
        string validator = File.ReadAllText(Path.Combine(root,
            "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Governance",
            "Virtualization", "VmReadProjectionDecisionValidatorV2.cs"));

        Assert.DoesNotContain("Phase38VirtualizationDecisionAcceptanceV2", governance);
        Assert.DoesNotContain("Phase38AcceptedVirtualizationDecisionRegistry", governance);
        Assert.DoesNotContain("DomainHypercall", governance);
        Assert.DoesNotContain("Project(", governance);
        Assert.DoesNotContain("Admit(", governance);
        Assert.DoesNotContain("Execute(", governance);
        Assert.DoesNotContain("BackendExecutionAuthorized => true", governance);
        Assert.DoesNotContain("RuntimeCapabilityGranted => true", validator);
        Assert.DoesNotContain("ProjectionValueAvailable => true", validator);
    }

    private static PrivilegedExecutionStateDescriptor CreateDescriptor() =>
        new(
            DomainTag: 7,
            AddressSpaceTag: 9,
            PolicyEpoch: new PrivilegedExecutionStateEpoch(11),
            Materialized: true,
            GuestCr0: new PrivilegedControlRegisterValue(
                PrivilegedControlRegisterKind.GuestCr0,
                0x8000_0011UL),
            GuestCr4: new PrivilegedControlRegisterValue(
                PrivilegedControlRegisterKind.GuestCr4,
                0x620UL),
            LegalityPolicy: new PrivilegedControlRegisterLegalityPolicy(
                GuestCr0AllowedMask: 0x8000_0031UL,
                GuestCr0RequiredMask: 0x11UL,
                GuestCr4AllowedMask: 0x0000_07FFUL,
                GuestCr4RequiredMask: 0x20UL,
                Materialized: true),
            EvidenceClass: PrivilegedExecutionStateEvidenceClass.GuestVisibleReadOnlyProjection,
            MigrationClass: PrivilegedExecutionStateMigrationClass.RevalidatedAfterRestore);
}
