namespace YAKSys_Hybrid_CPU.Core;

public enum SecureComputePhase22LimitedReleaseDecision : byte
{
    DeniedNoNamedPositiveRuntimePath = 0,
    DeniedPhase21MatrixMissing = 1,
    DeniedPhase21FutureGatedOnly = 2,
    DeniedMissingOwnerSpecificRfcAdr = 3,
    DeniedMissingProductionOwnerCode = 4,
    DeniedMissingOwnerPathReachability = 5,
    DeniedMissingTypedRequestResult = 6,
    DeniedMissingBackendResultOwnerBoundary = 7,
    DeniedMissingCompletionRetirePolicy = 8,
    DeniedMissingMigrationManifestRestoreEvidence = 9,
    DeniedMissingDebugAttestationLimits = 10,
    DeniedMissingVmxZeroAuthorityProjectionLimit = 11,
    DeniedMissingCompilerNoEmissionDecision = 12,
    DeniedPhase18NestedNotExcluded = 13,
    DeniedProductClaimOverreach = 14,
    DeniedMissingRollbackProcedure = 15,
    DeniedBackendExecutionAuthorityNotLocallyProven = 16,
    DeniedPhase22ManualApprovalNotImplemented = 17,
}

public readonly record struct SecureComputePhase22LimitedReleaseEvidenceRequest(
    bool Phase21MatrixEvidenceAccepted,
    bool Phase21FuturePositiveTestsRemainGated,
    bool NamedPositiveRuntimePathProven,
    bool OwnerSpecificRfcAdrAccepted,
    bool ProductionOwnerCodeExists,
    bool OwnerPathReachabilityProven,
    bool TypedRequestResultModelExists,
    bool BackendResultOwnerBoundaryProven,
    bool CompletionRetirePolicyForNamedPathProven,
    bool MigrationManifestRestoreEvidenceComplete,
    bool DebugAttestationVisibilityLimitsProven,
    bool VmxProjectionAfterNeutralResultOnly,
    bool CompilerNoEmissionDecisionRecorded,
    bool Phase18NestedExecutionExcluded,
    bool ProductClaimScopedToNamedPath,
    bool BoundedRollbackProcedureReviewed,
    bool BackendExecutionAuthorityLocallyProven);

public readonly record struct SecureComputePhase22LimitedReleaseGateResult(
    SecureComputePhase22LimitedReleaseDecision Decision,
    bool ReleaseEvidenceAccepted,
    bool RuntimeExecutionAuthorized,
    bool BackendResultAccepted,
    bool CompletionPublicationAuthorized,
    bool RetirePublicationAuthorized,
    bool VmxAuthorityAuthorized,
    bool CompilerEmissionAuthorized,
    bool NestedExecutionAuthorized,
    bool ProductionReleaseApproved,
    string Reason)
{
    public bool CreatesAnyReleaseAuthority =>
        RuntimeExecutionAuthorized ||
        BackendResultAccepted ||
        CompletionPublicationAuthorized ||
        RetirePublicationAuthorized ||
        VmxAuthorityAuthorized ||
        CompilerEmissionAuthorized ||
        NestedExecutionAuthorized ||
        ProductionReleaseApproved;

    public static SecureComputePhase22LimitedReleaseGateResult Denied(
        SecureComputePhase22LimitedReleaseDecision decision,
        string reason) =>
        new(
            decision,
            ReleaseEvidenceAccepted: false,
            RuntimeExecutionAuthorized: false,
            BackendResultAccepted: false,
            CompletionPublicationAuthorized: false,
            RetirePublicationAuthorized: false,
            VmxAuthorityAuthorized: false,
            CompilerEmissionAuthorized: false,
            NestedExecutionAuthorized: false,
            ProductionReleaseApproved: false,
            reason);
}

public sealed class SecureComputePhase22LimitedReleaseGatePolicy
{
    public static SecureComputePhase22LimitedReleaseGatePolicy FailClosed { get; } = new();

    public SecureComputePhase22LimitedReleaseGateResult Classify(
        SecureComputePhase22LimitedReleaseEvidenceRequest request)
    {
        if (!request.Phase21MatrixEvidenceAccepted)
        {
            return Deny(
                SecureComputePhase22LimitedReleaseDecision.DeniedPhase21MatrixMissing,
                "Phase 22 requires a consumed Phase 21 conformance matrix.");
        }

        if (!request.NamedPositiveRuntimePathProven)
        {
            return Deny(
                SecureComputePhase22LimitedReleaseDecision.DeniedNoNamedPositiveRuntimePath,
                "Phase 22 cannot approve release without a named positive runtime owner/path/reachability chain.");
        }

        if (request.Phase21FuturePositiveTestsRemainGated)
        {
            return Deny(
                SecureComputePhase22LimitedReleaseDecision.DeniedPhase21FutureGatedOnly,
                "Phase 22 cannot consume a Phase 21 matrix whose positive tests remain future-gated.");
        }

        if (!request.OwnerSpecificRfcAdrAccepted)
        {
            return Deny(
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingOwnerSpecificRfcAdr,
                "Phase 22 requires the owner-specific RFC/ADR for the named path.");
        }

        if (!request.ProductionOwnerCodeExists)
        {
            return Deny(
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingProductionOwnerCode,
                "Phase 22 requires production owner code, not a test fixture or documentation claim.");
        }

        if (!request.OwnerPathReachabilityProven)
        {
            return Deny(
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingOwnerPathReachability,
                "Phase 22 requires scoped owner/path/reachability proof for the named path.");
        }

        if (!request.TypedRequestResultModelExists)
        {
            return Deny(
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingTypedRequestResult,
                "Phase 22 requires a typed request/result model for the named path.");
        }

        if (!request.BackendResultOwnerBoundaryProven)
        {
            return Deny(
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingBackendResultOwnerBoundary,
                "Phase 22 requires a backend-result owner boundary separate from admission.");
        }

        if (!request.CompletionRetirePolicyForNamedPathProven)
        {
            return Deny(
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingCompletionRetirePolicy,
                "Phase 22 requires named-path completion and retire publication policy evidence.");
        }

        if (!request.MigrationManifestRestoreEvidenceComplete)
        {
            return Deny(
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingMigrationManifestRestoreEvidence,
                "Phase 22 requires complete migration/output-manifest and restore evidence.");
        }

        if (!request.DebugAttestationVisibilityLimitsProven)
        {
            return Deny(
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingDebugAttestationLimits,
                "Phase 22 requires debug/attestation visibility limits.");
        }

        if (!request.VmxProjectionAfterNeutralResultOnly)
        {
            return Deny(
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingVmxZeroAuthorityProjectionLimit,
                "Phase 22 requires VMX projection to remain after-neutral-result-only and zero-authority.");
        }

        if (!request.CompilerNoEmissionDecisionRecorded)
        {
            return Deny(
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingCompilerNoEmissionDecision,
                "Phase 22 requires the Phase 19 no-compiler-change decision.");
        }

        if (!request.Phase18NestedExecutionExcluded)
        {
            return Deny(
                SecureComputePhase22LimitedReleaseDecision.DeniedPhase18NestedNotExcluded,
                "Phase 22 requires Phase 18 nested execution to remain excluded.");
        }

        if (!request.ProductClaimScopedToNamedPath)
        {
            return Deny(
                SecureComputePhase22LimitedReleaseDecision.DeniedProductClaimOverreach,
                "Phase 22 requires product wording scoped to the named limited path.");
        }

        if (!request.BoundedRollbackProcedureReviewed)
        {
            return Deny(
                SecureComputePhase22LimitedReleaseDecision.DeniedMissingRollbackProcedure,
                "Phase 22 requires a bounded rollback procedure.");
        }

        if (!request.BackendExecutionAuthorityLocallyProven)
        {
            return Deny(
                SecureComputePhase22LimitedReleaseDecision.DeniedBackendExecutionAuthorityNotLocallyProven,
                "Phase 22 requires local backend execution authority proof before release review.");
        }

        return Deny(
            SecureComputePhase22LimitedReleaseDecision.DeniedPhase22ManualApprovalNotImplemented,
            "The current Phase 22 policy is fail-closed; release approval must be implemented only with named-path evidence.");
    }

    private static SecureComputePhase22LimitedReleaseGateResult Deny(
        SecureComputePhase22LimitedReleaseDecision decision,
        string reason) =>
        SecureComputePhase22LimitedReleaseGateResult.Denied(
            decision,
            reason);
}
