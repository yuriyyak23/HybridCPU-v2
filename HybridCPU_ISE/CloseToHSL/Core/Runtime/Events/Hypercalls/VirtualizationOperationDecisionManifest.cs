using System;
using System.Collections.Generic;
using System.Linq;

namespace YAKSys_Hybrid_CPU.Core;

internal enum VirtualizationOperationDecisionState : byte
{
    Absent = 0,
    Draft = 1,
    Accepted = 2,
    Withdrawn = 3,
}

internal enum VirtualizationOperationDecisionValidationDecision : byte
{
    ValidGovernanceArtifactOnly = 0,
    DeniedSchemaVersion = 1,
    DeniedDecisionNotAccepted = 2,
    DeniedDecisionIdentity = 3,
    DeniedNeutralOwnerAttribution = 4,
    DeniedAcceptedCommitSha = 5,
    DeniedCodeOwnersRule = 6,
    DeniedRequiredReview = 7,
    DeniedReviewerMismatch = 8,
    DeniedCompatibilitySelfApproval = 9,
    DeniedExactLeafCardinality = 10,
    DeniedDuplicateExactLeaf = 11,
    DeniedOwnerMapIncomplete = 12,
}

internal readonly record struct VirtualizationOperationDecisionManifest(
    uint SchemaVersion,
    string DecisionId,
    VirtualizationOperationDecisionState State,
    string OperationName,
    string DecisionOwner,
    NeutralHypercallBackendOwnerSource OwnerSource,
    string AcceptedCommitSha,
    IReadOnlyList<string> RequiredReviewers,
    IReadOnlyList<ulong> ExactNumericLeaves,
    string ValueSource,
    string CapabilityPolicy,
    string EvidenceClass,
    string MigrationClass,
    string DenialReason);

internal readonly record struct VirtualizationDecisionAttributionEvidence(
    string DecisionOwner,
    string AcceptedCommitSha,
    bool CodeOwnersRulePresent,
    bool CodeOwnersRuleMatched,
    bool RequiredReviewersApproved,
    IReadOnlyList<string> ApprovedReviewers,
    bool CompatibilityFrontendSelfApproved);

internal readonly record struct VirtualizationOperationDecisionValidationResult(
    VirtualizationOperationDecisionValidationDecision Decision,
    string Reason)
{
    internal bool IsStructurallyValidGovernanceEvidence =>
        Decision == VirtualizationOperationDecisionValidationDecision.ValidGovernanceArtifactOnly;

    internal bool BackendExecutionAuthorized => false;
    internal bool CompletionPublicationAuthorized => false;
    internal bool RetirePublicationAuthorized => false;
}

/// <summary>
/// Validates the shape and repository attribution of a future D2 decision artifact.
/// A valid result is governance evidence only and never runtime execution authority.
/// </summary>
internal static class VirtualizationOperationDecisionManifestValidator
{
    internal const uint CurrentSchemaVersion = 1;

    internal static VirtualizationOperationDecisionValidationResult Validate(
        VirtualizationOperationDecisionManifest manifest,
        VirtualizationDecisionAttributionEvidence attribution)
    {
        if (manifest.SchemaVersion != CurrentSchemaVersion)
            return Deny(VirtualizationOperationDecisionValidationDecision.DeniedSchemaVersion,
                "The D2 schema version is missing or unsupported.");

        if (manifest.State != VirtualizationOperationDecisionState.Accepted)
            return Deny(VirtualizationOperationDecisionValidationDecision.DeniedDecisionNotAccepted,
                "Only an attributable accepted decision artifact can satisfy D2.");

        if (string.IsNullOrWhiteSpace(manifest.DecisionId) ||
            string.IsNullOrWhiteSpace(manifest.OperationName))
            return Deny(VirtualizationOperationDecisionValidationDecision.DeniedDecisionIdentity,
                "The decision and operation identities must be explicit.");

        if (manifest.OwnerSource != NeutralHypercallBackendOwnerSource.NeutralRuntimeOwner ||
            string.IsNullOrWhiteSpace(manifest.DecisionOwner) ||
            !string.Equals(manifest.DecisionOwner, attribution.DecisionOwner, StringComparison.Ordinal))
            return Deny(VirtualizationOperationDecisionValidationDecision.DeniedNeutralOwnerAttribution,
                "D2 requires an attributable neutral runtime owner independent of compatibility planes.");

        if (!IsCommitSha(manifest.AcceptedCommitSha) ||
            !string.Equals(manifest.AcceptedCommitSha, attribution.AcceptedCommitSha, StringComparison.OrdinalIgnoreCase))
            return Deny(VirtualizationOperationDecisionValidationDecision.DeniedAcceptedCommitSha,
                "The accepted decision must be bound to one matching repository commit SHA.");

        if (!attribution.CodeOwnersRulePresent || !attribution.CodeOwnersRuleMatched)
            return Deny(VirtualizationOperationDecisionValidationDecision.DeniedCodeOwnersRule,
                "No matching repository CODEOWNERS attribution was proven.");

        if (manifest.RequiredReviewers is null ||
            manifest.RequiredReviewers.Count == 0 ||
            !attribution.RequiredReviewersApproved)
            return Deny(VirtualizationOperationDecisionValidationDecision.DeniedRequiredReview,
                "Required owner review is absent or unproven.");

        if (attribution.ApprovedReviewers is null ||
            !manifest.RequiredReviewers.All(requiredReviewer =>
                attribution.ApprovedReviewers.Contains(requiredReviewer, StringComparer.Ordinal)))
            return Deny(VirtualizationOperationDecisionValidationDecision.DeniedReviewerMismatch,
                "The attributed approvals do not cover every required reviewer.");

        if (attribution.CompatibilityFrontendSelfApproved)
            return Deny(VirtualizationOperationDecisionValidationDecision.DeniedCompatibilitySelfApproval,
                "The VMX compatibility plane cannot approve its own runtime authority.");

        if (new[]
            {
                manifest.ValueSource,
                manifest.CapabilityPolicy,
                manifest.EvidenceClass,
                manifest.MigrationClass,
                manifest.DenialReason,
            }.Any(string.IsNullOrWhiteSpace))
            return Deny(VirtualizationOperationDecisionValidationDecision.DeniedOwnerMapIncomplete,
                "The complete owner/value/capability/evidence/migration/denial map is required.");

        if (manifest.ExactNumericLeaves is null || manifest.ExactNumericLeaves.Count == 0)
            return Deny(VirtualizationOperationDecisionValidationDecision.DeniedExactLeafCardinality,
                "D2 requires exactly one owner-accepted numeric leaf; none is supplied by this substrate.");

        if (manifest.ExactNumericLeaves.Count !=
            manifest.ExactNumericLeaves.Distinct().Count())
            return Deny(VirtualizationOperationDecisionValidationDecision.DeniedDuplicateExactLeaf,
                "D2 rejects duplicate numeric leaves; this substrate allocates no leaf.");

        if (manifest.ExactNumericLeaves.Count != 1)
            return Deny(VirtualizationOperationDecisionValidationDecision.DeniedExactLeafCardinality,
                "D2 requires exactly one owner-accepted numeric leaf; a range or set is not accepted.");

        return new(
            VirtualizationOperationDecisionValidationDecision.ValidGovernanceArtifactOnly,
            "The D2 artifact shape and attribution are valid governance evidence only; runtime authority is not granted.");
    }

    private static bool IsCommitSha(string? value) =>
        value is { Length: 40 } && value.All(Uri.IsHexDigit);

    private static VirtualizationOperationDecisionValidationResult Deny(
        VirtualizationOperationDecisionValidationDecision decision,
        string reason) => new(decision, reason);
}
