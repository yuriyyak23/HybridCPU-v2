using System.Collections.Immutable;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal sealed class PraD2GovernanceNegativeScenario : IVirtualizationScenario
{
    public string Id => "pra-d2-governance-negative";
    public string Description =>
        "PR-A D2 v2 canonical-governance negative evidence; never an accepted instance or runtime authority.";
    public DiagnosticSurfaceKind SurfaceKind => DiagnosticSurfaceKind.ModelContract;

    public Task ExecuteAsync(ScenarioExecutionContext context, CancellationToken cancellationToken)
    {
        for (int iteration = 0; iteration < context.Profile.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Fixture draft = Fixture.Create(VirtualizationDecisionAcceptanceStateV2.Draft);
            VirtualizationDecisionValidationResultV2 draftResult = draft.Validate();
            context.Check(
                draftResult.Decision == VirtualizationDecisionValidationDecisionV2.DeniedAcceptanceState,
                "draft diagnostic record must never become accepted D2");

            VirtualizationDecisionValidationResultV2 wrongDigest = draft.Validate(
                spec: draft.Spec with { SpecDigest = new string('f', 64) });
            context.Check(
                wrongDigest.Decision == VirtualizationDecisionValidationDecisionV2.DeniedSpecDigestMismatch,
                "wrong canonical spec digest must deny");

            VirtualizationDecisionValidationResultV2 noncanonical = draft.Validate(
                evidence: draft.Evidence with
                {
                    SpecCanonicalBytes = draft.Evidence.SpecCanonicalBytes.Add((byte)(iteration & 0xff)),
                });
            context.Check(
                noncanonical.Decision == VirtualizationDecisionValidationDecisionV2.DeniedNonCanonicalSpecBytes,
                "noncanonical spec bytes must deny");

            Fixture zeroOwner = Fixture.Create(
                VirtualizationDecisionAcceptanceStateV2.Draft,
                spec => spec with { OwnerId = 0 });
            VirtualizationDecisionValidationResultV2 zeroOwnerResult = zeroOwner.Validate();
            context.Check(
                zeroOwnerResult.Decision == VirtualizationDecisionValidationDecisionV2.DeniedOwner,
                "zero owner allocation must deny");

            VirtualizationNamespaceAllocationV2 adjacent = new(
                draft.Spec.OperationNamespace,
                draft.Spec.LeafWidth,
                2,
                "DIAGNOSTIC-ADJACENT-LEAF",
                VirtualizationNamespaceClassV2.RuntimeAuthority);
            VirtualizationDecisionValidationResultV2 adjacentResult = draft.Validate(
                evidence: draft.Evidence with
                {
                    ExistingAllocations = draft.Evidence.ExistingAllocations.Add(adjacent),
                });
            context.Check(
                adjacentResult.Decision == VirtualizationDecisionValidationDecisionV2.DeniedAdjacentLeaf,
                "adjacent runtime leaf must remain denied");

            Fixture unattributedAcceptedVocabulary = Fixture.Create(
                VirtualizationDecisionAcceptanceStateV2.Accepted);
            VirtualizationDecisionValidationResultV2 codeOwnersResult =
                unattributedAcceptedVocabulary.Validate();
            context.Check(
                codeOwnersResult.Decision == VirtualizationDecisionValidationDecisionV2.DeniedCodeOwners,
                "accepted vocabulary without a real CODEOWNERS blob and reviews must deny");

            foreach (VirtualizationDecisionValidationResultV2 result in new[]
                     {
                         draftResult,
                         wrongDigest,
                         noncanonical,
                         zeroOwnerResult,
                         adjacentResult,
                         codeOwnersResult,
                     })
            {
                context.Check(!result.IsAcceptedPolicyObject, "diagnostic cases must remain negative");
                context.Check(!result.RuntimeCapabilityGranted, "governance validation cannot grant capability");
                context.Check(!result.BackendExecutionAuthorized, "governance validation cannot authorize backend");
                context.Check(!result.CompletionPublicationAuthorized, "governance validation cannot publish completion");
                context.Check(!result.RetirePublicationAuthorized, "governance validation cannot authorize retire");
            }

            context.Count("draft_acceptance_rejections");
            context.Count("wrong_digest_rejections");
            context.Count("noncanonical_byte_rejections");
            context.Count("zero_owner_rejections");
            context.Count("adjacent_leaf_rejections");
            context.Count("missing_codeowners_rejections");
            context.Count("accepted_policy_objects", 0);
            context.Count("runtime_authority_objects", 0);
            context.Trace("pra-d2-governance-negative",
                ("evidenceClass", "governance-negative-only"),
                ("specDigest", draft.Spec.SpecDigest),
                ("draftDecision", draftResult.Decision),
                ("wrongDigestDecision", wrongDigest.Decision),
                ("noncanonicalDecision", noncanonical.Decision),
                ("zeroOwnerDecision", zeroOwnerResult.Decision),
                ("adjacentLeafDecision", adjacentResult.Decision),
                ("codeOwnersDecision", codeOwnersResult.Decision),
                ("acceptedInstance", false),
                ("runtimeAuthority", false));
            context.CompleteIteration("D2 v2 governance cases remained negative and non-authoritative.");
        }

        return Task.CompletedTask;
    }

    private sealed record Fixture(
        VirtualizationDecisionSpecV2 Spec,
        VirtualizationDecisionAcceptanceRecordV2 Acceptance,
        VirtualizationDecisionValidationEvidenceV2 Evidence)
    {
        private const string SpecCommitSha = "1111111111111111111111111111111111111111";
        private const string ContainingCommitSha = "2222222222222222222222222222222222222222";
        private const string CodeOwnersBlobSha = "3333333333333333333333333333333333333333";

        internal static Fixture Create(
            VirtualizationDecisionAcceptanceStateV2 state,
            Func<VirtualizationDecisionSpecV2, VirtualizationDecisionSpecV2>? mutateSpec = null)
        {
            VirtualizationDecisionSpecV2 spec = new(
                2,
                VirtualizationDecisionValidatorV2.ExpectedDecisionId,
                VirtualizationDecisionValidatorV2.ExpectedOperationNamespace,
                16,
                0,
                1,
                VirtualizationDecisionValidatorV2.ExpectedOperationId,
                VirtualizationDecisionOwnerClassV2.NeutralRuntimeOwner,
                VirtualizationDecisionValidatorV2.ExpectedOwnerId,
                1,
                1,
                1,
                "ArchitecturalRegisterFullNumericLeafValue",
                "X0",
                "X0NoResult",
                VirtualizationDecisionResultAbiV2.NoPayload,
                VirtualizationDecisionEffectClassV2.NoStateNoPayload,
                VirtualizationCapabilityRequirementV2.DomainGrantedVmCallProbeNoStateV1,
                VirtualizationDecisionValidatorV2.ExpectedCapabilityMask,
                true,
                VirtualizationDelegationPolicyV2.NonDelegable,
                VirtualizationRevocationPolicyV2.RuntimeRevocable,
                VirtualizationCapabilityMigrationClassV2.DomainLocal,
                VirtualizationEvidenceVisibilityV2.HostOnly,
                VirtualizationProjectionPolicyV2.NeverProject,
                VirtualizationExecutionEvidenceRequirementV2.None,
                VirtualizationDomainRequirementV2.ExecutionDomainBound,
                true,
                false,
                false,
                VirtualizationAddressSpaceRequirementV2.None,
                VirtualizationSecureDomainPolicyV2.Deny,
                VirtualizationCancellationPolicyV2.DenyBeforeExecution,
                VirtualizationReplayPolicyV2.DenyAttemptReplay,
                VirtualizationOperationMigrationPolicyV2.DrainOnly,
                VirtualizationCompletionEvidenceClassV2.HostOwnedRuntimeEvidence,
                VirtualizationCompletionMigrationClassV2.HostOwnedNonMigratable,
                VirtualizationProjectionPolicyV2.NeverProject,
                VirtualizationCompletionPolicyV2.AtomicE3ToCompletionRecordAndE5,
                VirtualizationRetirePolicyV2.PreciseE5BoundNoStateRetire,
                VirtualizationAdjacentLeafPolicyV2.DenyAllExceptExact,
                VirtualizationCrossNamespacePolicyV2.AllowDistinctFrozenCompatibilityNamespaceOnly,
                OwnerMap(),
                new string('0', 64));
            if (mutateSpec is not null)
                spec = mutateSpec(spec);
            spec = spec with { SpecDigest = VirtualizationDecisionCanonicalEncoderV2.ComputeSpecDigest(spec) };

            VirtualizationDecisionReviewEvidenceV2 missingOwnerReview = new(
                VirtualizationDecisionReviewRoleV2.OwnerReviewRole,
                VirtualizationDecisionReviewAuthorityPlaneV2.NeutralRuntimeOwner,
                VirtualizationDecisionReviewStateV2.Missing,
                string.Empty,
                spec.DecisionId,
                spec.SpecDigest,
                SpecCommitSha,
                string.Empty);
            VirtualizationDecisionReviewEvidenceV2 missingArchitectureReview = missingOwnerReview with
            {
                Role = VirtualizationDecisionReviewRoleV2.ArchitectureReviewRole,
                AuthorityPlane = VirtualizationDecisionReviewAuthorityPlaneV2.Architecture,
            };
            VirtualizationDecisionAcceptanceRecordV2 acceptance = new(
                2,
                spec.DecisionId,
                spec.SpecDigest,
                SpecCommitSha,
                state,
                "@unattributed-diagnostic-fixture",
                1,
                missingOwnerReview,
                missingArchitectureReview,
                CodeOwnersBlobSha,
                null,
                null,
                new string('0', 64));
            acceptance = acceptance with
            {
                AcceptanceDigest = VirtualizationDecisionCanonicalEncoderV2.ComputeAcceptanceDigest(acceptance),
            };

            ImmutableArray<byte> specBytes = VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(spec);
            var evidence = new VirtualizationDecisionValidationEvidenceV2(
                specBytes,
                VirtualizationDecisionCanonicalEncoderV2.EncodeAcceptance(acceptance),
                specBytes,
                SpecCommitSha,
                ContainingCommitSha,
                new(false, CodeOwnersBlobSha, []),
                [
                    new(
                        "HybridCPU.VMFUNC.FrozenAbi.v1",
                        16,
                        1,
                        "FROZEN-VMFUNC-CAPABILITY-QUERY",
                        VirtualizationNamespaceClassV2.FrozenCompatibility),
                ],
                [],
                []);
            return new(spec, acceptance, evidence);
        }

        internal VirtualizationDecisionValidationResultV2 Validate(
            VirtualizationDecisionSpecV2? spec = null,
            VirtualizationDecisionAcceptanceRecordV2? acceptance = null,
            VirtualizationDecisionValidationEvidenceV2? evidence = null) =>
            VirtualizationDecisionValidatorV2.Validate(
                spec ?? Spec,
                acceptance ?? Acceptance,
                evidence ?? Evidence);

        private static ImmutableArray<VirtualizationDecisionOwnerMapEntryV2> OwnerMap()
        {
            (string Field, string Owner)[] entries =
            [
                ("Operation", "DomainHypercallRuntimeOwner"),
                ("OperandAbi", "CanonicalOperandSnapshotOwner"),
                ("CapabilityAdmission", "RuntimeCapabilityOwner"),
                ("DomainAdmission", "RuntimeDomainOwner"),
                ("ExecutionAdmission", "SafetyVerifier"),
                ("CancellationReplay", "DomainHypercallRuntimeOwner"),
                ("CompletionPublication", "NeutralCompletionOwner"),
                ("RetirePublication", "CanonicalRetireOwner"),
                ("MigrationRestore", "CheckpointRestoreOwner"),
                ("AdjacentLeafDenial", "VirtualizationDecisionValidatorV2"),
            ];
            return entries.Select(entry => new VirtualizationDecisionOwnerMapEntryV2(
                entry.Field,
                entry.Owner,
                "Phase38ExactValueSource",
                "ExactTypedGrantOrNone",
                "HostOnlyOrNone",
                "DrainOnlyOrHostOwnedNonMigratable",
                "DenyOnMissingOrMismatch"))
                .ToImmutableArray();
        }
    }
}
