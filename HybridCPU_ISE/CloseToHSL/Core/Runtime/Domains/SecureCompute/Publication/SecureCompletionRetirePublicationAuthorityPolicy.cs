namespace YAKSys_Hybrid_CPU.Core;

public enum SecurePublicationPathKind : byte
{
    None = 0,
    ProofOnlyBackendOwnerAdmission = 1,
    AdmittedDeniedSecureHypercallAdmission = 2,
    RegistryBackedHypercallContractAdmission = 3,
    SecureIoPolicyAdmissionOnly = 4,
    GenericTrapRouteFlags = 5,
    VmxCompatibilityProjectionOnly = 6,
    NeutralRuntimeBackendResult = 7,
}

public enum SecurePublicationBackendResultState : byte
{
    Missing = 0,
    InternalNeutralResult = 1,
}

public enum SecurePublicationLadderStep : byte
{
    NoBackendResult = 0,
    InternalBackendResult = 1,
    InternalCompletionRecord = 2,
    CompletionPublication = 3,
    RetirePublication = 4,
}

public enum SecureCompletionRetirePublicationDecision : byte
{
    DeniedNoBackendResult = 0,
    DeniedProofOnlyAdmission = 1,
    DeniedAdmittedDeniedAdmission = 2,
    DeniedRegistryBackedProofOnlyAdmission = 3,
    DeniedPolicyAdmissionOnly = 4,
    DeniedGenericTrapRouteNotSecureAuthority = 5,
    DeniedVmxProjectionOnly = 6,
    DeniedBackendResultOwner = 7,
    DeniedCompletionRecordMissing = 8,
    DeniedCompletionOwnerMissing = 9,
    DeniedCompletionOwnerSource = 10,
    DeniedCompletionOwnerEpoch = 11,
    DeniedCompletionOwnerProof = 12,
    DeniedCompletionFence = 13,
    CompletionPublishedRetirePending = 14,
    DeniedCompletionPublicationRequired = 15,
    DeniedRetireOwnerMissing = 16,
    DeniedRetireOwnerSource = 17,
    DeniedRetireOwnerEpoch = 18,
    DeniedRetireOwnerProof = 19,
    DeniedRetireFence = 20,
    DeniedRetireEvidence = 21,
    DeniedRetireMigrationClass = 22,
    RetirePublished = 23,
}

public readonly record struct SecureCompletionPublicationAuthorityRequest(
    SecurePublicationPathKind PathKind,
    SecurePublicationBackendResultState BackendResultState,
    SecureBackendOwnerDescriptor? BackendResultOwner,
    SecureBackendOwnerDescriptor? CompletionOwner,
    SecureCompletionPublicationFence? PublicationFence,
    SecureRevocationEpoch CurrentEpoch,
    bool CompletionRecordMaterialized);

public readonly record struct SecureRetirePublicationAuthorityRequest(
    SecureCompletionRetirePublicationResult CompletionPublication,
    SecureBackendOwnerDescriptor? RetireOwner,
    SecureCompletionPublicationFence? PublicationFence,
    SecureRevocationEpoch CurrentEpoch,
    EvidenceVisibilityClass EvidenceClass,
    TrapCompletionMigrationClass MigrationClass);

public readonly record struct SecureCompletionRetirePublicationResult(
    SecureCompletionRetirePublicationDecision Decision,
    SecurePublicationLadderStep LadderStep,
    bool CompletionPublished,
    bool RetirePublished,
    string Reason)
{
    public bool IsDenied => !CompletionPublished && !RetirePublished;

    public bool IsCompletionOnly =>
        CompletionPublished &&
        !RetirePublished;

    public static SecureCompletionRetirePublicationResult Denied(
        SecureCompletionRetirePublicationDecision decision,
        SecurePublicationLadderStep ladderStep,
        string reason) =>
        new(
            decision,
            ladderStep,
            CompletionPublished: false,
            RetirePublished: false,
            reason);

    public static SecureCompletionRetirePublicationResult PublishedCompletion(
        string reason) =>
        new(
            SecureCompletionRetirePublicationDecision.CompletionPublishedRetirePending,
            SecurePublicationLadderStep.CompletionPublication,
            CompletionPublished: true,
            RetirePublished: false,
            reason);

    public static SecureCompletionRetirePublicationResult PublishedRetire() =>
        new(
            SecureCompletionRetirePublicationDecision.RetirePublished,
            SecurePublicationLadderStep.RetirePublication,
            CompletionPublished: true,
            RetirePublished: true,
            string.Empty);
}

public sealed class SecureCompletionRetirePublicationAuthorityPolicy
{
    public static SecureCompletionRetirePublicationAuthorityPolicy Default { get; } = new();

    public SecureCompletionRetirePublicationResult AdmitCompletion(
        SecureCompletionPublicationAuthorityRequest request)
    {
        SecureCompletionRetirePublicationResult pathResult =
            DenyCurrentNonPublishingPath(request.PathKind);
        if (!pathResult.IsDenied ||
            pathResult.Decision != SecureCompletionRetirePublicationDecision.DeniedNoBackendResult)
        {
            return pathResult;
        }

        if (request.BackendResultState != SecurePublicationBackendResultState.InternalNeutralResult)
        {
            return Deny(
                SecureCompletionRetirePublicationDecision.DeniedNoBackendResult,
                SecurePublicationLadderStep.NoBackendResult,
                "Secure completion publication requires a separately produced neutral backend result.");
        }

        if (!IsCurrentNeutralOwner(request.BackendResultOwner, request.CurrentEpoch))
        {
            return Deny(
                SecureCompletionRetirePublicationDecision.DeniedBackendResultOwner,
                SecurePublicationLadderStep.InternalBackendResult,
                "Secure completion publication requires a current neutral backend-result owner.");
        }

        if (!request.CompletionRecordMaterialized)
        {
            return Deny(
                SecureCompletionRetirePublicationDecision.DeniedCompletionRecordMissing,
                SecurePublicationLadderStep.InternalBackendResult,
                "Secure completion publication requires an internal completion record after the backend result.");
        }

        SecureCompletionRetirePublicationResult? owner =
            ValidateCompletionOwner(request.CompletionOwner, request.CurrentEpoch);
        if (owner.HasValue)
        {
            return owner.Value;
        }

        if (request.PublicationFence?.CanPublishCompletion != true)
        {
            return Deny(
                SecureCompletionRetirePublicationDecision.DeniedCompletionFence,
                SecurePublicationLadderStep.InternalCompletionRecord,
                "Secure completion publication requires a completion owner and completion fence.");
        }

        return SecureCompletionRetirePublicationResult.PublishedCompletion(
            "Secure completion publication reached the completion step only; retire publication remains separate.");
    }

    public SecureCompletionRetirePublicationResult AdmitRetire(
        SecureRetirePublicationAuthorityRequest request)
    {
        if (!request.CompletionPublication.CompletionPublished)
        {
            return Deny(
                SecureCompletionRetirePublicationDecision.DeniedCompletionPublicationRequired,
                SecurePublicationLadderStep.InternalCompletionRecord,
                "Secure retire publication requires a previously published secure completion.");
        }

        SecureCompletionRetirePublicationResult? owner =
            ValidateRetireOwner(request.RetireOwner, request.CurrentEpoch);
        if (owner.HasValue)
        {
            return owner.Value;
        }

        if (request.PublicationFence?.CanPublishRetire != true)
        {
            return Deny(
                SecureCompletionRetirePublicationDecision.DeniedRetireFence,
                SecurePublicationLadderStep.CompletionPublication,
                "Secure retire publication requires explicit retire owner authority and an explicit retire fence.");
        }

        if (!CanRetireEvidence(request.EvidenceClass))
        {
            return Deny(
                SecureCompletionRetirePublicationDecision.DeniedRetireEvidence,
                SecurePublicationLadderStep.CompletionPublication,
                "Secure retire publication cannot expose host-owned runtime evidence.");
        }

        if (!CanRetireMigrationClass(request.MigrationClass))
        {
            return Deny(
                SecureCompletionRetirePublicationDecision.DeniedRetireMigrationClass,
                SecurePublicationLadderStep.CompletionPublication,
                "Secure retire publication requires recomputed or guest-architectural migration classification.");
        }

        return SecureCompletionRetirePublicationResult.PublishedRetire();
    }

    public SecureCompletionRetirePublicationResult AdmitCompletionFromBackendOwnerAdmission(
        SecureBackendOwnerAdmissionResult admission) =>
        admission.Decision == SecureBackendOwnerAdmissionDecision.AllowedProofOnlyNoExecution
            ? Deny(
                SecureCompletionRetirePublicationDecision.DeniedProofOnlyAdmission,
                SecurePublicationLadderStep.NoBackendResult,
                "Proof-only backend owner admission cannot publish secure completion.")
            : DenyNoBackendResult();

    public SecureCompletionRetirePublicationResult AdmitRetireFromBackendOwnerAdmission(
        SecureBackendOwnerAdmissionResult admission) =>
        admission.Decision == SecureBackendOwnerAdmissionDecision.AllowedProofOnlyNoExecution
            ? Deny(
                SecureCompletionRetirePublicationDecision.DeniedProofOnlyAdmission,
                SecurePublicationLadderStep.NoBackendResult,
                "Proof-only backend owner admission cannot publish secure retire effects.")
            : DenyNoBackendResult();

    public SecureCompletionRetirePublicationResult AdmitCompletionFromSecureIoHypercallAdmission(
        SecureIoHypercallAdmissionResult admission) =>
        admission.IsAdmittedDenied
            ? Deny(
                SecureCompletionRetirePublicationDecision.DeniedAdmittedDeniedAdmission,
                SecurePublicationLadderStep.NoBackendResult,
                "Admitted-denied secure hypercall recognition cannot publish secure completion.")
            : admission.IsPolicyAdmissionOnly
                ? Deny(
                    SecureCompletionRetirePublicationDecision.DeniedPolicyAdmissionOnly,
                    SecurePublicationLadderStep.NoBackendResult,
                    "Secure I/O policy admission cannot publish secure completion.")
                : DenyNoBackendResult();

    public SecureCompletionRetirePublicationResult AdmitRetireFromSecureIoHypercallAdmission(
        SecureIoHypercallAdmissionResult admission) =>
        admission.IsAdmittedDenied
            ? Deny(
                SecureCompletionRetirePublicationDecision.DeniedAdmittedDeniedAdmission,
                SecurePublicationLadderStep.NoBackendResult,
                "Admitted-denied secure hypercall recognition cannot publish secure retire effects.")
            : admission.IsPolicyAdmissionOnly
                ? Deny(
                    SecureCompletionRetirePublicationDecision.DeniedPolicyAdmissionOnly,
                    SecurePublicationLadderStep.NoBackendResult,
                    "Secure I/O policy admission cannot publish secure retire effects.")
                : DenyNoBackendResult();

    public SecureCompletionRetirePublicationResult AdmitCompletionFromHypercallContractAdmission(
        SecureHypercallBackendContractAdmissionResult admission) =>
        admission.IsProofOnly
            ? Deny(
                SecureCompletionRetirePublicationDecision.DeniedRegistryBackedProofOnlyAdmission,
                SecurePublicationLadderStep.NoBackendResult,
                "Registry-backed Phase 13 owner/service admission remains proof-only and cannot publish secure completion.")
            : DenyNoBackendResult();

    public SecureCompletionRetirePublicationResult AdmitRetireFromHypercallContractAdmission(
        SecureHypercallBackendContractAdmissionResult admission) =>
        admission.IsProofOnly
            ? Deny(
                SecureCompletionRetirePublicationDecision.DeniedRegistryBackedProofOnlyAdmission,
                SecurePublicationLadderStep.NoBackendResult,
                "Registry-backed Phase 13 owner/service admission remains proof-only and cannot publish secure retire effects.")
            : DenyNoBackendResult();

    public SecureCompletionRetirePublicationResult DenyGenericTrapRouteFlags(
        bool completionPublicationFlag,
        bool retirePublicationFlag) =>
        completionPublicationFlag || retirePublicationFlag
            ? Deny(
                SecureCompletionRetirePublicationDecision.DeniedGenericTrapRouteNotSecureAuthority,
                SecurePublicationLadderStep.NoBackendResult,
                "Generic trap route publication flags are not SecureCompute authority without secure owner/path reachability.")
            : DenyNoBackendResult();

    public SecureCompletionRetirePublicationResult DenyVmxCompatibilityProjectionOnly() =>
        Deny(
            SecureCompletionRetirePublicationDecision.DeniedVmxProjectionOnly,
            SecurePublicationLadderStep.NoBackendResult,
            "VMX compatibility projection is zero-authority for SecureCompute completion and retire publication.");

    private static SecureCompletionRetirePublicationResult DenyCurrentNonPublishingPath(
        SecurePublicationPathKind pathKind) =>
        pathKind switch
        {
            SecurePublicationPathKind.ProofOnlyBackendOwnerAdmission =>
                Deny(
                    SecureCompletionRetirePublicationDecision.DeniedProofOnlyAdmission,
                    SecurePublicationLadderStep.NoBackendResult,
                    "Proof-only backend owner admission cannot publish secure completion."),

            SecurePublicationPathKind.AdmittedDeniedSecureHypercallAdmission =>
                Deny(
                    SecureCompletionRetirePublicationDecision.DeniedAdmittedDeniedAdmission,
                    SecurePublicationLadderStep.NoBackendResult,
                    "Admitted-denied secure hypercall recognition cannot publish secure completion."),

            SecurePublicationPathKind.RegistryBackedHypercallContractAdmission =>
                Deny(
                    SecureCompletionRetirePublicationDecision.DeniedRegistryBackedProofOnlyAdmission,
                    SecurePublicationLadderStep.NoBackendResult,
                    "Registry-backed Phase 13 owner/service admission remains proof-only and cannot publish secure completion."),

            SecurePublicationPathKind.SecureIoPolicyAdmissionOnly =>
                Deny(
                    SecureCompletionRetirePublicationDecision.DeniedPolicyAdmissionOnly,
                    SecurePublicationLadderStep.NoBackendResult,
                    "Secure I/O policy admission cannot publish secure completion."),

            SecurePublicationPathKind.GenericTrapRouteFlags =>
                Deny(
                    SecureCompletionRetirePublicationDecision.DeniedGenericTrapRouteNotSecureAuthority,
                    SecurePublicationLadderStep.NoBackendResult,
                    "Generic trap route publication flags are not SecureCompute authority without secure owner/path reachability."),

            SecurePublicationPathKind.VmxCompatibilityProjectionOnly =>
                Deny(
                    SecureCompletionRetirePublicationDecision.DeniedVmxProjectionOnly,
                    SecurePublicationLadderStep.NoBackendResult,
                    "VMX compatibility projection is zero-authority for SecureCompute completion and retire publication."),

            _ => DenyNoBackendResult(),
        };

    private static SecureCompletionRetirePublicationResult? ValidateCompletionOwner(
        SecureBackendOwnerDescriptor? owner,
        SecureRevocationEpoch currentEpoch)
    {
        SecureCompletionRetirePublicationResult? common =
            ValidateOwner(
                owner,
                currentEpoch,
                SecureCompletionRetirePublicationDecision.DeniedCompletionOwnerMissing,
                SecureCompletionRetirePublicationDecision.DeniedCompletionOwnerSource,
                SecureCompletionRetirePublicationDecision.DeniedCompletionOwnerEpoch);
        if (common.HasValue)
        {
            return common;
        }

        return owner!.Value.CompletionFenceValidated
            ? null
            : Deny(
                SecureCompletionRetirePublicationDecision.DeniedCompletionOwnerProof,
                SecurePublicationLadderStep.InternalCompletionRecord,
                "Secure completion owner requires validated completion-fence proof.");
    }

    private static SecureCompletionRetirePublicationResult? ValidateRetireOwner(
        SecureBackendOwnerDescriptor? owner,
        SecureRevocationEpoch currentEpoch)
    {
        SecureCompletionRetirePublicationResult? common =
            ValidateOwner(
                owner,
                currentEpoch,
                SecureCompletionRetirePublicationDecision.DeniedRetireOwnerMissing,
                SecureCompletionRetirePublicationDecision.DeniedRetireOwnerSource,
                SecureCompletionRetirePublicationDecision.DeniedRetireOwnerEpoch);
        if (common.HasValue)
        {
            return common;
        }

        return owner!.Value.RetireFenceValidated
            ? null
            : Deny(
                SecureCompletionRetirePublicationDecision.DeniedRetireOwnerProof,
                SecurePublicationLadderStep.CompletionPublication,
                "Secure retire owner requires validated retire-fence proof.");
    }

    private static SecureCompletionRetirePublicationResult? ValidateOwner(
        SecureBackendOwnerDescriptor? owner,
        SecureRevocationEpoch currentEpoch,
        SecureCompletionRetirePublicationDecision missingDecision,
        SecureCompletionRetirePublicationDecision sourceDecision,
        SecureCompletionRetirePublicationDecision epochDecision)
    {
        if (owner is null ||
            !owner.Value.Materialized ||
            !owner.Value.HasIdentity)
        {
            return Deny(
                missingDecision,
                SecurePublicationLadderStep.InternalCompletionRecord,
                "Secure publication owner requires a materialized owner identity.");
        }

        if (!owner.Value.IsNeutralSource)
        {
            return Deny(
                sourceDecision,
                SecurePublicationLadderStep.InternalCompletionRecord,
                "Secure publication owner cannot be sourced from VMX, VMCS, VmxCaps or compatibility projection.");
        }

        if (!owner.Value.Epoch.Equals(currentEpoch))
        {
            return Deny(
                epochDecision,
                SecurePublicationLadderStep.InternalCompletionRecord,
                "Secure publication owner epoch must match the current policy epoch.");
        }

        return null;
    }

    private static bool IsCurrentNeutralOwner(
        SecureBackendOwnerDescriptor? owner,
        SecureRevocationEpoch currentEpoch) =>
        owner is { } value &&
        value.Materialized &&
        value.HasIdentity &&
        value.IsNeutralSource &&
        value.Epoch.Equals(currentEpoch);

    private static bool CanRetireEvidence(EvidenceVisibilityClass evidenceClass) =>
        evidenceClass is EvidenceVisibilityClass.GuestArchitecturalState
            or EvidenceVisibilityClass.CompatibilityAlias;

    private static bool CanRetireMigrationClass(TrapCompletionMigrationClass migrationClass) =>
        migrationClass is TrapCompletionMigrationClass.RecomputedAfterRestore
            or TrapCompletionMigrationClass.GuestArchitecturalState;

    private static SecureCompletionRetirePublicationResult DenyNoBackendResult() =>
        Deny(
            SecureCompletionRetirePublicationDecision.DeniedNoBackendResult,
            SecurePublicationLadderStep.NoBackendResult,
            "Secure completion/retire publication requires a neutral backend result and explicit publication owners.");

    private static SecureCompletionRetirePublicationResult Deny(
        SecureCompletionRetirePublicationDecision decision,
        SecurePublicationLadderStep ladderStep,
        string reason) =>
        SecureCompletionRetirePublicationResult.Denied(decision, ladderStep, reason);
}
