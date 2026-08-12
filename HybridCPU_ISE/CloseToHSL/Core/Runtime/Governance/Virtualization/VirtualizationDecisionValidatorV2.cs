using System.Collections.Immutable;
using System.Security.Cryptography;

namespace YAKSys_Hybrid_CPU.Core;

internal enum VirtualizationDecisionValidationDecisionV2 : byte
{
    AcceptedPolicyObject = 0,
    DeniedMissingArtifact = 1,
    DeniedSchemaVersion = 2,
    DeniedMalformedDigest = 3,
    DeniedSpecDigestMismatch = 4,
    DeniedAcceptanceDigestMismatch = 5,
    DeniedNonCanonicalSpecBytes = 6,
    DeniedNonCanonicalAcceptanceBytes = 7,
    DeniedSpecBytesAtCommitMismatch = 8,
    DeniedMalformedCommitSha = 9,
    DeniedSelfReferentialCommitSha = 10,
    DeniedDecisionIdentity = 11,
    DeniedOwner = 12,
    DeniedAbi = 13,
    DeniedPolicyMissing = 14,
    DeniedUnknownPolicy = 15,
    DeniedPolicyProfile = 16,
    DeniedOwnerMapIncomplete = 17,
    DeniedOwnerMapMismatch = 18,
    DeniedZeroLeaf = 19,
    DeniedDuplicateLeaf = 20,
    DeniedAdjacentLeaf = 21,
    DeniedCrossNamespaceLeaf = 22,
    DeniedNamespaceRule = 23,
    DeniedAcceptanceState = 24,
    DeniedCodeOwners = 25,
    DeniedReviewRole = 26,
    DeniedReviewMismatch = 27,
    DeniedCompatibilityReview = 28,
    DeniedInvalidLineage = 29,
    DeniedRevoked = 30,
    DeniedSuperseded = 31,
}

internal sealed record VirtualizationDecisionValidationResultV2(
    VirtualizationDecisionValidationDecisionV2 Decision,
    string Reason,
    AcceptedVirtualizationDecision? AcceptedDecision)
{
    internal bool IsAcceptedPolicyObject =>
        Decision == VirtualizationDecisionValidationDecisionV2.AcceptedPolicyObject &&
        AcceptedDecision is not null;

    internal bool RuntimeCapabilityGranted => false;
    internal bool BackendExecutionAuthorized => false;
    internal bool CompletionPublicationAuthorized => false;
    internal bool RetirePublicationAuthorized => false;
}

/// <summary>
/// Fail-closed validator for the one exact Phase 38 architecture/policy profile.
/// It validates governance evidence only and has no owner registry, capability,
/// admission, execution, completion or retire operation.
/// </summary>
internal static class VirtualizationDecisionValidatorV2
{
    internal const uint CurrentSchemaVersion = 2;
    internal const string ExpectedDecisionId = "D2-HV-VMCALL-RUNTIME-V1-PROBE-0001";
    internal const string ExpectedOperationNamespace = "HybridCPU.VMCALL.Runtime.v1";
    internal const string ExpectedOperationId = "PROBE_NO_STATE_V1";
    internal const ulong ExpectedOwnerId = 0x4843_4F57_4E52UL;
    internal const ulong ExpectedCapabilityMask = 1UL << 41;
    private static readonly ImmutableDictionary<string, string> RequiredOwnerMap =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Operation"] = "DomainHypercallRuntimeOwner",
            ["OperandAbi"] = "CanonicalOperandSnapshotOwner",
            ["CapabilityAdmission"] = "RuntimeCapabilityOwner",
            ["DomainAdmission"] = "RuntimeDomainOwner",
            ["ExecutionAdmission"] = "SafetyVerifier",
            ["CancellationReplay"] = "DomainHypercallRuntimeOwner",
            ["CompletionPublication"] = "NeutralCompletionOwner",
            ["RetirePublication"] = "CanonicalRetireOwner",
            ["MigrationRestore"] = "CheckpointRestoreOwner",
            ["AdjacentLeafDenial"] = "VirtualizationDecisionValidatorV2",
        }.ToImmutableDictionary(StringComparer.Ordinal);

    private static readonly ImmutableArray<string> RequiredCodeOwnersScopes =
    [
        "/HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/Hypercalls/",
        "/HybridCPU_ISE/CloseToHSL/Core/Runtime/Capabilities/",
        "/HybridCPU_ISE/CloseToHSL/Core/Pipeline/Safety/",
        "/HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/",
        "/HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/",
        "/HybridCPU_ISE/docs/ref2/VirtualizationActivationPlan/",
    ];

    internal static VirtualizationDecisionValidationResultV2 Validate(
        VirtualizationDecisionSpecV2? spec,
        VirtualizationDecisionAcceptanceRecordV2? acceptance,
        VirtualizationDecisionValidationEvidenceV2? evidence)
    {
        if (spec is null || acceptance is null || evidence is null || evidence.CodeOwners is null)
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedMissingArtifact,
                "SpecV2, AcceptanceRecordV2 and repository evidence are all required.");

        if (spec.SchemaVersion != CurrentSchemaVersion ||
            acceptance.SchemaVersion != CurrentSchemaVersion)
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedSchemaVersion,
                "Only D2 v2 governance artifacts are supported.");

        if (!VirtualizationDecisionCanonicalEncoderV2.IsCanonicalCommitSha(acceptance.SpecCommitSha) ||
            !VirtualizationDecisionCanonicalEncoderV2.IsCanonicalCommitSha(acceptance.CodeOwnersBlobSha) ||
            !VirtualizationDecisionCanonicalEncoderV2.IsCanonicalCommitSha(evidence.ResolvedSpecCommitSha) ||
            !VirtualizationDecisionCanonicalEncoderV2.IsCanonicalCommitSha(evidence.AcceptanceContainingCommitSha))
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedMalformedCommitSha,
                "Spec, CODEOWNERS and containing commit identities must be canonical 40-hex SHAs.");

        if (string.Equals(
                acceptance.SpecCommitSha,
                evidence.AcceptanceContainingCommitSha,
                StringComparison.Ordinal))
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedSelfReferentialCommitSha,
                "SpecCommitSha must name the earlier commit containing the immutable spec bytes, not the acceptance record's containing commit.");

        if (!string.Equals(
                acceptance.SpecCommitSha,
                evidence.ResolvedSpecCommitSha,
                StringComparison.Ordinal))
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedSpecBytesAtCommitMismatch,
                "The repository resolver did not resolve exact spec bytes at the recorded SpecCommitSha.");

        VirtualizationDecisionValidationResultV2? canonicalFailure =
            ValidateCanonicalArtifacts(spec, acceptance, evidence);
        if (canonicalFailure is not null)
            return canonicalFailure;

        if (!string.Equals(spec.DecisionId, ExpectedDecisionId, StringComparison.Ordinal) ||
            !string.Equals(acceptance.DecisionId, spec.DecisionId, StringComparison.Ordinal))
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedDecisionIdentity,
                "DecisionId does not identify the exact Phase 38 first slice.");

        VirtualizationDecisionValidationResultV2? ownerFailure = ValidateOwnerAndAbi(spec);
        if (ownerFailure is not null)
            return ownerFailure;

        VirtualizationDecisionValidationResultV2? policyFailure = ValidatePolicies(spec);
        if (policyFailure is not null)
            return policyFailure;

        VirtualizationDecisionValidationResultV2? ownerMapFailure = ValidateOwnerMap(spec.OwnerMap);
        if (ownerMapFailure is not null)
            return ownerMapFailure;

        VirtualizationDecisionValidationResultV2? namespaceFailure =
            ValidateNamespaceAndLeaf(spec, evidence.ExistingAllocations);
        if (namespaceFailure is not null)
            return namespaceFailure;

        VirtualizationDecisionValidationResultV2? acceptanceFailure =
            ValidateAcceptance(spec, acceptance, evidence.CodeOwners);
        if (acceptanceFailure is not null)
            return acceptanceFailure;

        VirtualizationDecisionValidationResultV2? lineageFailure;
        try
        {
            lineageFailure = ValidateLineage(
                acceptance,
                evidence.Revocations,
                evidence.Supersessions);
        }
        catch (Exception exception) when (
            exception is FormatException or OverflowException or ArgumentException or NullReferenceException)
        {
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedInvalidLineage,
                "Lineage contains malformed fields that cannot be canonically validated.");
        }
        if (lineageFailure is not null)
            return lineageFailure;

        var acceptedPolicy = new AcceptedVirtualizationDecision(
            spec.DecisionId,
            spec.SpecDigest,
            acceptance.AcceptanceDigest,
            acceptance.SpecCommitSha,
            spec.OperationNamespace,
            spec.NumericLeaf,
            spec.OwnerId,
            spec.OwnerPolicyVersion,
            spec.OwnerEpoch,
            spec.EffectClass,
            new(spec.AdjacentLeafPolicy, spec.CrossNamespacePolicy));

        return new(
            VirtualizationDecisionValidationDecisionV2.AcceptedPolicyObject,
            "The exact D2 v2 structure is valid as immutable governance policy only; no runtime authority is granted.",
            acceptedPolicy);
    }

    private static VirtualizationDecisionValidationResultV2? ValidateCanonicalArtifacts(
        VirtualizationDecisionSpecV2 spec,
        VirtualizationDecisionAcceptanceRecordV2 acceptance,
        VirtualizationDecisionValidationEvidenceV2 evidence)
    {
        if (!VirtualizationDecisionCanonicalEncoderV2.IsCanonicalSha256(spec.SpecDigest) ||
            !VirtualizationDecisionCanonicalEncoderV2.IsCanonicalSha256(acceptance.SpecDigest) ||
            !VirtualizationDecisionCanonicalEncoderV2.IsCanonicalSha256(acceptance.AcceptanceDigest))
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedMalformedDigest,
                "All D2 digests must be canonical lowercase SHA-256 values.");

        try
        {
            string computedSpecDigest = VirtualizationDecisionCanonicalEncoderV2.ComputeSpecDigest(spec);
            if (!FixedEquals(spec.SpecDigest, computedSpecDigest) ||
                !FixedEquals(acceptance.SpecDigest, spec.SpecDigest))
                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedSpecDigestMismatch,
                    "SpecDigest does not reproduce the canonical spec payload or its acceptance reference.");

            string computedAcceptanceDigest =
                VirtualizationDecisionCanonicalEncoderV2.ComputeAcceptanceDigest(acceptance);
            if (!FixedEquals(acceptance.AcceptanceDigest, computedAcceptanceDigest))
                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedAcceptanceDigestMismatch,
                    "AcceptanceDigest does not reproduce the canonical acceptance payload.");

            ImmutableArray<byte> canonicalSpec =
                VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(spec);
            if (!FixedEquals(canonicalSpec, evidence.SpecCanonicalBytes))
                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedNonCanonicalSpecBytes,
                    "Provided spec bytes are not the v2 canonical binary form.");

            ImmutableArray<byte> canonicalAcceptance =
                VirtualizationDecisionCanonicalEncoderV2.EncodeAcceptance(acceptance);
            if (!FixedEquals(canonicalAcceptance, evidence.AcceptanceCanonicalBytes))
                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedNonCanonicalAcceptanceBytes,
                    "Provided acceptance bytes are not the v2 canonical binary form.");

            if (!FixedEquals(canonicalSpec, evidence.SpecBytesAtCommit))
                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedSpecBytesAtCommitMismatch,
                    "The bytes resolved at SpecCommitSha differ from the accepted canonical spec bytes.");
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
        {
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedMalformedDigest,
                "A digest, commit identity or canonical field cannot be encoded.");
        }

        return null;
    }

    private static VirtualizationDecisionValidationResultV2? ValidateOwnerAndAbi(
        VirtualizationDecisionSpecV2 spec)
    {
        if (spec.OwnerId == 0 ||
            spec.OwnerClass != VirtualizationDecisionOwnerClassV2.NeutralRuntimeOwner ||
            spec.OwnerId != ExpectedOwnerId ||
            spec.OwnerPolicyVersion != 1 ||
            spec.OwnerEpoch != 1)
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedOwner,
                "The neutral HCOWNR allocation and owner policy/epoch must match Phase 38 exactly.");

        if (spec.OperandAbiVersion != 1 ||
            !string.Equals(spec.Rs1Contract, "ArchitecturalRegisterFullNumericLeafValue", StringComparison.Ordinal) ||
            !string.Equals(spec.Rs2Contract, "X0", StringComparison.Ordinal) ||
            !string.Equals(spec.RdContract, "X0NoResult", StringComparison.Ordinal) ||
            spec.ResultAbi != VirtualizationDecisionResultAbiV2.NoPayload ||
            spec.EffectClass != VirtualizationDecisionEffectClassV2.NoStateNoPayload)
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedAbi,
                "Operand, result and no-state/no-payload ABI must match the exact Phase 38 profile.");

        return null;
    }

    private static VirtualizationDecisionValidationResultV2? ValidatePolicies(
        VirtualizationDecisionSpecV2 spec)
    {
        Array policyValues = new object[]
        {
            spec.CapabilityRequirement,
            spec.DelegationPolicy,
            spec.RevocationPolicy,
            spec.CapabilityMigrationClass,
            spec.EvidenceVisibility,
            spec.FrontendProjectionPolicy,
            spec.ExecutionEvidenceRequirement,
            spec.DomainRequirement,
            spec.AddressSpaceRequirement,
            spec.SecureDomainPolicy,
            spec.CancellationPolicy,
            spec.ReplayPolicy,
            spec.OperationMigrationPolicy,
            spec.CompletionEvidenceClass,
            spec.CompletionMigrationClass,
            spec.CompletionProjectionPolicy,
            spec.CompletionPolicy,
            spec.RetirePolicy,
            spec.AdjacentLeafPolicy,
            spec.CrossNamespacePolicy,
        };

        foreach (object value in policyValues)
        {
            Type enumType = value.GetType();
            if (!Enum.IsDefined(enumType, value))
                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedUnknownPolicy,
                    $"Unknown policy value for {enumType.Name} is denied.");
            if (Convert.ToUInt32(value) == 0)
                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedPolicyMissing,
                    $"Missing policy value for {enumType.Name} is denied.");
        }

        if (spec.CapabilityRequirement != VirtualizationCapabilityRequirementV2.DomainGrantedVmCallProbeNoStateV1 ||
            spec.CapabilityMask != ExpectedCapabilityMask ||
            !spec.RequiresTypedGrant ||
            spec.DelegationPolicy != VirtualizationDelegationPolicyV2.NonDelegable ||
            spec.RevocationPolicy != VirtualizationRevocationPolicyV2.RuntimeRevocable ||
            spec.CapabilityMigrationClass != VirtualizationCapabilityMigrationClassV2.DomainLocal ||
            spec.EvidenceVisibility != VirtualizationEvidenceVisibilityV2.HostOnly ||
            spec.FrontendProjectionPolicy != VirtualizationProjectionPolicyV2.NeverProject ||
            spec.ExecutionEvidenceRequirement != VirtualizationExecutionEvidenceRequirementV2.None ||
            spec.DomainRequirement != VirtualizationDomainRequirementV2.ExecutionDomainBound ||
            !spec.RequireNonZeroDomainTag ||
            spec.RequiresMemoryDomain ||
            spec.RequiresIoDomain ||
            spec.AddressSpaceRequirement != VirtualizationAddressSpaceRequirementV2.None ||
            spec.SecureDomainPolicy != VirtualizationSecureDomainPolicyV2.Deny ||
            spec.CancellationPolicy != VirtualizationCancellationPolicyV2.DenyBeforeExecution ||
            spec.ReplayPolicy != VirtualizationReplayPolicyV2.DenyAttemptReplay ||
            spec.OperationMigrationPolicy != VirtualizationOperationMigrationPolicyV2.DrainOnly ||
            spec.CompletionEvidenceClass != VirtualizationCompletionEvidenceClassV2.HostOwnedRuntimeEvidence ||
            spec.CompletionMigrationClass != VirtualizationCompletionMigrationClassV2.HostOwnedNonMigratable ||
            spec.CompletionProjectionPolicy != VirtualizationProjectionPolicyV2.NeverProject ||
            spec.CompletionPolicy != VirtualizationCompletionPolicyV2.AtomicE3ToCompletionRecordAndE5 ||
            spec.RetirePolicy != VirtualizationRetirePolicyV2.PreciseE5BoundNoStateRetire ||
            spec.AdjacentLeafPolicy != VirtualizationAdjacentLeafPolicyV2.DenyAllExceptExact ||
            spec.CrossNamespacePolicy != VirtualizationCrossNamespacePolicyV2.AllowDistinctFrozenCompatibilityNamespaceOnly)
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedPolicyProfile,
                "The complete capability/domain/evidence/cancellation/replay/completion/retire policy profile must match Phase 38 exactly.");

        return null;
    }

    private static VirtualizationDecisionValidationResultV2? ValidateOwnerMap(
        ImmutableArray<VirtualizationDecisionOwnerMapEntryV2> ownerMap)
    {
        if (ownerMap.IsDefaultOrEmpty || ownerMap.Length != RequiredOwnerMap.Count)
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedOwnerMapIncomplete,
                "The complete Phase 38 owner map is required.");

        if (ownerMap.Any(entry =>
                entry is null ||
                string.IsNullOrWhiteSpace(entry.FieldOrOperation) ||
                string.IsNullOrWhiteSpace(entry.Owner) ||
                string.IsNullOrWhiteSpace(entry.ValueSource) ||
                string.IsNullOrWhiteSpace(entry.CapabilityPolicy) ||
                string.IsNullOrWhiteSpace(entry.EvidenceClass) ||
                string.IsNullOrWhiteSpace(entry.MigrationClass) ||
                string.IsNullOrWhiteSpace(entry.DenialReason)))
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedOwnerMapIncomplete,
                "Every owner-map entry requires owner, source, capability, evidence, migration and denial fields.");

        if (ownerMap.Select(entry => entry.FieldOrOperation).Distinct(StringComparer.Ordinal).Count() != ownerMap.Length)
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedOwnerMapMismatch,
                "Owner-map fields must be unique.");

        foreach ((string field, string expectedOwner) in RequiredOwnerMap)
        {
            VirtualizationDecisionOwnerMapEntryV2? entry = ownerMap.FirstOrDefault(candidate =>
                string.Equals(candidate.FieldOrOperation, field, StringComparison.Ordinal));
            if (entry is null || !string.Equals(entry.Owner, expectedOwner, StringComparison.Ordinal))
                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedOwnerMapMismatch,
                    $"Owner-map field {field} is missing or assigned to the wrong neutral owner.");
        }

        if (ownerMap.Any(entry =>
                entry.Owner.Contains("CompatibilityFrontend", StringComparison.OrdinalIgnoreCase) ||
                entry.Owner.Contains("VMX", StringComparison.OrdinalIgnoreCase) ||
                entry.Owner.Contains("VMCS", StringComparison.OrdinalIgnoreCase)))
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedOwnerMapMismatch,
                "Compatibility vocabulary cannot own a D2 policy field.");

        return null;
    }

    private static VirtualizationDecisionValidationResultV2? ValidateNamespaceAndLeaf(
        VirtualizationDecisionSpecV2 spec,
        ImmutableArray<VirtualizationNamespaceAllocationV2> allocations)
    {
        if (spec.NumericLeaf == 0 || spec.InvalidLeaf != 0)
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedZeroLeaf,
                "Leaf zero is invalid and cannot be allocated.");

        if (!string.Equals(spec.OperationNamespace, ExpectedOperationNamespace, StringComparison.Ordinal) ||
            !string.Equals(spec.OperationId, ExpectedOperationId, StringComparison.Ordinal) ||
            spec.LeafWidth != 16 ||
            spec.NumericLeaf != 1)
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedNamespaceRule,
                "Namespace, width, operation and numeric leaf must match the exact Phase 38 ABI.");

        if (allocations.IsDefault)
            allocations = [];

        foreach (VirtualizationNamespaceAllocationV2 allocation in allocations)
        {
            if (allocation is null ||
                allocation.NumericLeaf == 0 ||
                string.IsNullOrWhiteSpace(allocation.OperationNamespace))
                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedNamespaceRule,
                    "Malformed existing allocation evidence is denied.");

            bool sameNamespace = string.Equals(
                allocation.OperationNamespace,
                spec.OperationNamespace,
                StringComparison.Ordinal);

            if (sameNamespace && allocation.LeafWidth != spec.LeafWidth)
                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedNamespaceRule,
                    "One operation namespace cannot carry conflicting leaf widths.");

            if (sameNamespace && allocation.NumericLeaf == spec.NumericLeaf)
                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedDuplicateLeaf,
                    "The exact runtime namespace/leaf is already allocated.");

            if (sameNamespace && Math.Abs((int)allocation.NumericLeaf - spec.NumericLeaf) == 1)
                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedAdjacentLeaf,
                    "Adjacent leaves remain denied by the exact first-slice policy.");

            if (!sameNamespace &&
                allocation.NumericLeaf == spec.NumericLeaf &&
                allocation.NamespaceClass != VirtualizationNamespaceClassV2.FrozenCompatibility)
                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedCrossNamespaceLeaf,
                    "Numeric reuse is allowed only for a distinct frozen compatibility namespace.");

            if (string.Equals(allocation.DecisionId, spec.DecisionId, StringComparison.Ordinal))
                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedDuplicateLeaf,
                    "DecisionId is already bound to another allocation.");
        }

        return null;
    }

    private static VirtualizationDecisionValidationResultV2? ValidateAcceptance(
        VirtualizationDecisionSpecV2 spec,
        VirtualizationDecisionAcceptanceRecordV2 acceptance,
        VirtualizationCodeOwnersEvidenceV2 codeOwners)
    {
        if (acceptance.AcceptanceState != VirtualizationDecisionAcceptanceStateV2.Accepted)
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedAcceptanceState,
                "Draft, revoked or superseded acceptance-record states are not active acceptance.");

        if (acceptance.AcceptancePolicyVersion != 1 ||
            !IsRepositoryPrincipal(acceptance.AcceptedBy))
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedAcceptanceState,
                "Acceptance policy version and attributable accepting principal are required.");

        if (!codeOwners.FilePresent ||
            !string.Equals(codeOwners.BlobSha, acceptance.CodeOwnersBlobSha, StringComparison.Ordinal) ||
            codeOwners.Rules.IsDefaultOrEmpty)
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedCodeOwners,
                "A matching repository CODEOWNERS blob and rules are required.");

        foreach (string scope in RequiredCodeOwnersScopes)
        {
            if (!codeOwners.Rules.Any(rule =>
                    rule is not null &&
                    string.Equals(rule.Scope, scope, StringComparison.Ordinal) &&
                    string.Equals(rule.Principal, acceptance.AcceptedBy, StringComparison.Ordinal)))
                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedCodeOwners,
                    $"Required CODEOWNERS scope {scope} is absent or does not match the accepting repository principal.");
        }

        VirtualizationDecisionValidationResultV2? ownerReviewFailure = ValidateReview(
            acceptance.OwnerReviewEvidence,
            VirtualizationDecisionReviewRoleV2.OwnerReviewRole,
            VirtualizationDecisionReviewAuthorityPlaneV2.NeutralRuntimeOwner,
            spec,
            acceptance.SpecCommitSha);
        if (ownerReviewFailure is not null)
            return ownerReviewFailure;

        VirtualizationDecisionValidationResultV2? architectureReviewFailure = ValidateReview(
            acceptance.ArchitectureReviewEvidence,
            VirtualizationDecisionReviewRoleV2.ArchitectureReviewRole,
            VirtualizationDecisionReviewAuthorityPlaneV2.Architecture,
            spec,
            acceptance.SpecCommitSha);
        if (architectureReviewFailure is not null)
            return architectureReviewFailure;

        if (!string.Equals(acceptance.OwnerReviewEvidence.Principal, acceptance.AcceptedBy, StringComparison.Ordinal) ||
            !string.Equals(acceptance.ArchitectureReviewEvidence.Principal, acceptance.AcceptedBy, StringComparison.Ordinal))
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedReviewMismatch,
                "Owner review, architecture review and acceptance must name the same CODEOWNERS-attributed repository principal.");

        return null;
    }

    private static bool IsRepositoryPrincipal(string? principal)
    {
        if (principal is not { Length: > 1 } || principal[0] != '@')
            return false;

        for (int index = 1; index < principal.Length; index++)
        {
            char value = principal[index];
            if (!(char.IsAsciiLetterOrDigit(value) || value is '-' or '_' or '/'))
                return false;
        }

        return !string.Equals(principal, "@CompatibilityFrontend", StringComparison.OrdinalIgnoreCase);
    }

    private static VirtualizationDecisionValidationResultV2? ValidateReview(
        VirtualizationDecisionReviewEvidenceV2 review,
        VirtualizationDecisionReviewRoleV2 requiredRole,
        VirtualizationDecisionReviewAuthorityPlaneV2 requiredPlane,
        VirtualizationDecisionSpecV2 spec,
        string specCommitSha)
    {
        if (review is null || review.Role != requiredRole)
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedReviewRole,
                $"The {requiredRole} evidence is missing or role-mismatched.");

        if (review.AuthorityPlane == VirtualizationDecisionReviewAuthorityPlaneV2.CompatibilityFrontend)
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedCompatibilityReview,
                "CompatibilityFrontend cannot satisfy owner or architecture review.");

        if (review.AuthorityPlane != requiredPlane ||
            review.State != VirtualizationDecisionReviewStateV2.Completed ||
            string.IsNullOrWhiteSpace(review.Principal) ||
            string.IsNullOrWhiteSpace(review.EvidenceId) ||
            !string.Equals(review.ReviewedDecisionId, spec.DecisionId, StringComparison.Ordinal) ||
            !FixedEquals(review.ReviewedSpecDigest, spec.SpecDigest) ||
            !string.Equals(review.ReviewedSpecCommitSha, specCommitSha, StringComparison.Ordinal))
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedReviewMismatch,
                $"The {requiredRole} evidence does not bind the exact spec identity, digest and commit.");

        return null;
    }

    private static VirtualizationDecisionValidationResultV2? ValidateLineage(
        VirtualizationDecisionAcceptanceRecordV2 acceptance,
        ImmutableArray<VirtualizationDecisionRevocationEvidenceV2> revocations,
        ImmutableArray<VirtualizationDecisionSupersessionEvidenceV2> supersessions)
    {
        if (revocations.IsDefault)
            revocations = [];
        if (supersessions.IsDefault)
            supersessions = [];

        foreach (VirtualizationDecisionRevocationEvidenceV2 evidence in revocations)
        {
            VirtualizationDecisionRevocationRecordV2 record = evidence.Record;
            if (record is null || record.SchemaVersion != CurrentSchemaVersion ||
                !VirtualizationDecisionCanonicalEncoderV2.IsCanonicalSha256(record.RevocationDigest) ||
                !FixedEquals(record.RevocationDigest,
                    VirtualizationDecisionCanonicalEncoderV2.ComputeRevocationDigest(record)) ||
                !FixedEquals(evidence.CanonicalBytes,
                    VirtualizationDecisionCanonicalEncoderV2.EncodeRevocation(record)))
                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedInvalidLineage,
                    "Revocation lineage is malformed, noncanonical or has the wrong digest.");

            if (!string.Equals(record.DecisionId, acceptance.DecisionId, StringComparison.Ordinal))
                continue;

            if (!FixedEquals(record.AcceptanceDigest, acceptance.AcceptanceDigest) ||
                record.Sequence == 0 ||
                string.IsNullOrWhiteSpace(record.RevocationId) ||
                string.IsNullOrWhiteSpace(record.RevokedBy) ||
                string.IsNullOrWhiteSpace(record.Reason))
                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedInvalidLineage,
                    "Revocation lineage does not bind the exact accepted record.");

            if (record.State == VirtualizationDecisionRevocationStateV2.Effective)
                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedRevoked,
                    "The accepted record has an effective immutable revocation.");

            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedInvalidLineage,
                "A draft revocation cannot be treated as active lineage evidence.");
        }

        foreach (VirtualizationDecisionSupersessionEvidenceV2 evidence in supersessions)
        {
            VirtualizationDecisionSupersessionRecordV2 record = evidence.Record;
            if (record is null || record.SchemaVersion != CurrentSchemaVersion ||
                !VirtualizationDecisionCanonicalEncoderV2.IsCanonicalSha256(record.SupersessionDigest) ||
                !FixedEquals(record.SupersessionDigest,
                    VirtualizationDecisionCanonicalEncoderV2.ComputeSupersessionDigest(record)) ||
                !FixedEquals(evidence.CanonicalBytes,
                    VirtualizationDecisionCanonicalEncoderV2.EncodeSupersession(record)) ||
                record.Sequence == 0 ||
                string.IsNullOrWhiteSpace(record.SupersessionId) ||
                string.IsNullOrWhiteSpace(record.SupersededBy) ||
                string.Equals(record.SupersededDecisionId, record.SupersedingDecisionId, StringComparison.Ordinal) ||
                FixedEquals(record.SupersededAcceptanceDigest, record.SupersedingAcceptanceDigest))
                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedInvalidLineage,
                    "Supersession lineage is malformed, self-referential, noncanonical or has the wrong digest.");

            if (string.Equals(record.SupersededDecisionId, acceptance.DecisionId, StringComparison.Ordinal))
            {
                if (!FixedEquals(record.SupersededAcceptanceDigest, acceptance.AcceptanceDigest))
                    return Deny(VirtualizationDecisionValidationDecisionV2.DeniedInvalidLineage,
                        "Supersession lineage does not bind the exact superseded acceptance record.");

                if (record.State == VirtualizationDecisionSupersessionStateV2.Effective)
                    return Deny(VirtualizationDecisionValidationDecisionV2.DeniedSuperseded,
                        "The accepted record has an effective immutable supersession.");

                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedInvalidLineage,
                    "A draft supersession cannot be treated as active lineage evidence.");
            }
        }

        bool hasSupersedesDecision = !string.IsNullOrWhiteSpace(acceptance.SupersedesDecisionId);
        bool hasSupersedesDigest = !string.IsNullOrWhiteSpace(acceptance.SupersedesAcceptanceDigest);
        if (hasSupersedesDecision != hasSupersedesDigest)
            return Deny(VirtualizationDecisionValidationDecisionV2.DeniedInvalidLineage,
                "Superseding acceptance lineage must provide both prior DecisionId and AcceptanceDigest.");

        if (hasSupersedesDecision)
        {
            if (string.Equals(acceptance.SupersedesDecisionId, acceptance.DecisionId, StringComparison.Ordinal) ||
                !VirtualizationDecisionCanonicalEncoderV2.IsCanonicalSha256(acceptance.SupersedesAcceptanceDigest))
                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedInvalidLineage,
                    "Acceptance supersession lineage is self-referential or malformed.");

            bool matchingLineage = supersessions.Any(item =>
                item.Record.State == VirtualizationDecisionSupersessionStateV2.Effective &&
                string.Equals(item.Record.SupersededDecisionId, acceptance.SupersedesDecisionId, StringComparison.Ordinal) &&
                FixedEquals(item.Record.SupersededAcceptanceDigest, acceptance.SupersedesAcceptanceDigest!) &&
                string.Equals(item.Record.SupersedingDecisionId, acceptance.DecisionId, StringComparison.Ordinal) &&
                FixedEquals(item.Record.SupersedingAcceptanceDigest, acceptance.AcceptanceDigest));
            if (!matchingLineage)
                return Deny(VirtualizationDecisionValidationDecisionV2.DeniedInvalidLineage,
                    "The superseding acceptance record lacks one matching effective immutable lineage record.");
        }

        return null;
    }

    private static bool FixedEquals(string? left, string? right) =>
        left is not null &&
        right is not null &&
        CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(left),
            System.Text.Encoding.ASCII.GetBytes(right));

    private static bool FixedEquals(ImmutableArray<byte> left, ImmutableArray<byte> right) =>
        !left.IsDefault &&
        !right.IsDefault &&
        CryptographicOperations.FixedTimeEquals(left.AsSpan(), right.AsSpan());

    private static VirtualizationDecisionValidationResultV2 Deny(
        VirtualizationDecisionValidationDecisionV2 decision,
        string reason) => new(decision, reason, null);
}
