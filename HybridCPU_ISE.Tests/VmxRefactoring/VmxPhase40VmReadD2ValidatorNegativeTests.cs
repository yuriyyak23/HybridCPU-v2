using System.Collections.Immutable;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase40VmReadD2ValidatorNegativeTests
{
    [Fact]
    public void Validator_AcceptsOnlyPolicyMetadataAndCreatesNoRuntimeAuthority()
    {
        Fixture fixture = Fixture.Create();

        VmReadProjectionDecisionValidationResultV2 result = fixture.Validate();

        Assert.True(result.IsAcceptedPolicyObject);
        Assert.Equal(VmReadProjectionDecisionValidationDecisionV2.AcceptedPolicyObject, result.Decision);
        Assert.NotNull(result.AcceptedDecision);
        Assert.Equal(Phase40VmReadProjectionE0Contract.ExactFieldIds,
            result.AcceptedDecision.ExactFieldIds);
        Assert.False(result.RuntimeCapabilityGranted);
        Assert.False(result.ProjectionValueAvailable);
        Assert.False(result.BackendExecutionAuthorized);
        Assert.False(result.MutationAuthorized);
        Assert.False(result.CompletionPublicationAuthorized);
        Assert.False(result.RetirePublicationAuthorized);
    }

    [Theory]
    [InlineData((ushort)VmcsField.GuestCr3)]
    [InlineData((ushort)VmcsField.HostCr3)]
    [InlineData((ushort)VmcsField.PinBasedControls)]
    public void Validator_DeniesEveryAdjacentFieldOutsideExactSemanticGroup(ushort adjacentField)
    {
        Fixture fixture = Fixture.Create();
        VirtualizationDecisionSpecV2 widened = fixture.Spec with
        {
            ExactFieldIds = fixture.Spec.ExactFieldIds!.Value.Add(adjacentField),
        };

        Assert.Equal(VmReadProjectionDecisionValidationDecisionV2.DeniedProfile,
            fixture.WithSpec(widened).Validate().Decision);
    }

    [Fact]
    public void Validator_DeniesSplitOwnerOrMigrationSemanticsInsideOneD2()
    {
        Fixture fixture = Fixture.Create();
        ImmutableArray<VirtualizationDecisionOwnerMapEntryV2> splitOwner =
            fixture.Spec.OwnerMap.SetItem(1, fixture.Spec.OwnerMap[1] with
            {
                Owner = "CompatibilityFrontend",
            });
        ImmutableArray<VirtualizationDecisionOwnerMapEntryV2> splitMigration =
            fixture.Spec.OwnerMap.SetItem(1, fixture.Spec.OwnerMap[1] with
            {
                MigrationClass = "DomainLocal",
            });

        Assert.Equal(VmReadProjectionDecisionValidationDecisionV2.DeniedOwnerMap,
            fixture.WithSpec(fixture.Spec with { OwnerMap = splitOwner }).Validate().Decision);
        Assert.Equal(VmReadProjectionDecisionValidationDecisionV2.DeniedOwnerMap,
            fixture.WithSpec(fixture.Spec with { OwnerMap = splitMigration }).Validate().Decision);
    }

    [Fact]
    public void Validator_DeniesCapabilityProbeReuseAndAllEffectWidening()
    {
        Fixture fixture = Fixture.Create();
        VirtualizationDecisionSpecV2 capability = fixture.Spec with
        {
            CapabilityRequirement = VirtualizationCapabilityRequirementV2.DomainGrantedVmCallProbeNoStateV1,
            CapabilityMask = 1UL << 41,
            RequiresTypedGrant = true,
        };

        Assert.Equal(VmReadProjectionDecisionValidationDecisionV2.DeniedProfile,
            fixture.WithSpec(capability).Validate().Decision);
        Assert.Equal(VmReadProjectionDecisionValidationDecisionV2.DeniedProfile,
            fixture.WithSpec(fixture.Spec with
            {
                MutationClass = VirtualizationDecisionMutationClassV2.Unspecified,
            }).Validate().Decision);
        Assert.Equal(VmReadProjectionDecisionValidationDecisionV2.DeniedProfile,
            fixture.WithSpec(fixture.Spec with
            {
                CompletionPolicy = VirtualizationCompletionPolicyV2.AtomicE3ToCompletionRecordAndE5,
            }).Validate().Decision);
        Assert.Equal(VmReadProjectionDecisionValidationDecisionV2.DeniedProfile,
            fixture.WithSpec(fixture.Spec with
            {
                RetirePolicy = VirtualizationRetirePolicyV2.PreciseE5BoundNoStateRetire,
            }).Validate().Decision);
    }

    [Fact]
    public void Validator_DeniesVmcsAuthorityAndProbeDecisionIdentity()
    {
        Fixture fixture = Fixture.Create();

        Assert.Equal(VmReadProjectionDecisionValidationDecisionV2.DeniedProfile,
            fixture.WithSpec(fixture.Spec with { VmcsMetadataOnly = false }).Validate().Decision);
        Assert.Equal(VmReadProjectionDecisionValidationDecisionV2.DeniedIdentity,
            fixture.WithSpec(fixture.Spec with
            {
                DecisionId = VirtualizationDecisionValidatorV2.ExpectedDecisionId,
                OperationNamespace = VirtualizationDecisionValidatorV2.ExpectedOperationNamespace,
                OperationId = VirtualizationDecisionValidatorV2.ExpectedOperationId,
            }).Validate().Decision);
    }

    [Fact]
    public void Validator_DeniesMissingOrSelfReferentialAcceptanceProvenance()
    {
        Fixture fixture = Fixture.Create();

        Assert.Equal(VmReadProjectionDecisionValidationDecisionV2.DeniedMissingArtifact,
            VmReadProjectionDecisionValidatorV2.Validate(fixture.Spec, null, fixture.Evidence).Decision);
        Assert.Equal(VmReadProjectionDecisionValidationDecisionV2.DeniedProvenance,
            (fixture with
            {
                Evidence = fixture.Evidence with
                {
                    AcceptanceContainingCommitSha = fixture.Acceptance.SpecCommitSha,
                },
            }).Validate().Decision);
    }

    private sealed record Fixture(
        VirtualizationDecisionSpecV2 Spec,
        VirtualizationDecisionAcceptanceRecordV2 Acceptance,
        VirtualizationDecisionValidationEvidenceV2 Evidence)
    {
        private const string SpecSha = "1111111111111111111111111111111111111111";
        private const string ContainingSha = "2222222222222222222222222222222222222222";
        private const string CodeOwnersBlobSha = "3333333333333333333333333333333333333333";
        private const string Principal = "@yaksysdev";

        internal static Fixture Create() => CreateFor(Phase40VmReadProjectionDecisionSpecV2.Instance);

        internal Fixture WithSpec(VirtualizationDecisionSpecV2 candidate)
        {
            candidate = candidate with { SpecDigest = new string('0', 64) };
            candidate = candidate with
            {
                SpecDigest = VirtualizationDecisionCanonicalEncoderV2.ComputeSpecDigest(candidate),
            };
            return CreateFor(candidate);
        }

        internal VmReadProjectionDecisionValidationResultV2 Validate() =>
            VmReadProjectionDecisionValidatorV2.Validate(Spec, Acceptance, Evidence);

        private static Fixture CreateFor(VirtualizationDecisionSpecV2 spec)
        {
            VirtualizationDecisionReviewEvidenceV2 ownerReview = new(
                VirtualizationDecisionReviewRoleV2.OwnerReviewRole,
                VirtualizationDecisionReviewAuthorityPlaneV2.NeutralRuntimeOwner,
                VirtualizationDecisionReviewStateV2.Completed,
                Principal,
                spec.DecisionId,
                spec.SpecDigest,
                SpecSha,
                "PHASE40-OWNER-REVIEW-TEST-EVIDENCE");
            VirtualizationDecisionReviewEvidenceV2 architectureReview = ownerReview with
            {
                Role = VirtualizationDecisionReviewRoleV2.ArchitectureReviewRole,
                AuthorityPlane = VirtualizationDecisionReviewAuthorityPlaneV2.Architecture,
                EvidenceId = "PHASE40-ARCH-REVIEW-TEST-EVIDENCE",
            };
            VirtualizationDecisionAcceptanceRecordV2 acceptance = new(
                VirtualizationDecisionValidatorV2.CurrentSchemaVersion,
                spec.DecisionId,
                spec.SpecDigest,
                SpecSha,
                VirtualizationDecisionAcceptanceStateV2.Accepted,
                Principal,
                1,
                ownerReview,
                architectureReview,
                CodeOwnersBlobSha,
                null,
                null,
                new string('0', 64));
            acceptance = acceptance with
            {
                AcceptanceDigest =
                    VirtualizationDecisionCanonicalEncoderV2.ComputeAcceptanceDigest(acceptance),
            };
            VirtualizationCodeOwnersEvidenceV2 codeOwners = new(
                true,
                CodeOwnersBlobSha,
                [
                    new("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Governance/Virtualization/", Principal),
                    new("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/ExecutionState/", Principal),
                    new("/HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/ExecutionState/", Principal),
                    new("/HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Compatibility/Projection/", Principal),
                    new("/HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/", Principal),
                    new("/HybridCPU_ISE/docs/ref2/VirtualizationActivationPlan/", Principal),
                ]);
            VirtualizationDecisionValidationEvidenceV2 evidence = new(
                VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(spec),
                VirtualizationDecisionCanonicalEncoderV2.EncodeAcceptance(acceptance),
                VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(spec),
                SpecSha,
                ContainingSha,
                codeOwners,
                [],
                [],
                []);
            return new(spec, acceptance, evidence);
        }
    }
}
