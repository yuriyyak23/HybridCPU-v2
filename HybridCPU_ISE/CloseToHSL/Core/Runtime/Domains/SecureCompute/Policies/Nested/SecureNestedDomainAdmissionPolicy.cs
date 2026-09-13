namespace YAKSys_Hybrid_CPU.Core;

public enum SecureNestedCheckpointPayloadClass : byte
{
    NeutralChildIntentDescriptor = 0,
    CompatibilityProjectionMetadata = 1,
    ShadowVmcsCompatibilityBridge = 2,
    Vmcs12Authority = 3,
    Vmcs02Authority = 4,
    MutableShadowVmcsAuthority = 5,
}

public enum SecureNestedDomainAdmissionDecision : byte
{
    AllowedNoEffect = 0,
    AllowedDesignFence = 1,
    DeniedMissingChildIntentOwner = 2,
    DeniedMissingParentSecureDescriptor = 3,
    DeniedUnmaterializedChildIntent = 4,
    DeniedChildIntentProvenanceMissing = 5,
    DeniedStaleParentChildEpoch = 6,
    DeniedChildPolicyExpansion = 7,
    DeniedChildCompatibilityProjectionExpansion = 8,
    DeniedChildMigrationPayloadExpansion = 9,
    DeniedHostEvidenceLeakage = 10,
    DeniedNestedProjectionExpansion = 11,
    DeniedNestedVmcsAuthority = 12,
    DeniedMutableShadowVmcsAuthority = 13,
}

public readonly record struct SecureNestedDomainAdmissionRequest(
    SecureComputeDomainDescriptor? ParentDescriptor,
    SecureAuthorityBounds? ParentBounds,
    SecureChildDomainIntentDescriptor? ChildIntent,
    SecureRevocationEpoch CurrentEpoch,
    bool HasNeutralChildIntentOwner,
    bool ParentHostEvidenceExposedToChild,
    bool ChildHostEvidenceExposedToParent,
    bool NestedProjectionExceedsParent,
    SecureNestedCheckpointPayloadClass CheckpointPayloadClass,
    bool ShadowVmcsStoresMutableAuthority);

public readonly record struct SecureNestedDomainAdmissionResult(
    SecureNestedDomainAdmissionDecision Decision,
    string Reason,
    bool BackendSuccessAuthorized,
    bool MutableNestedStateAuthorized)
{
    public bool IsAllowed =>
        Decision is SecureNestedDomainAdmissionDecision.AllowedNoEffect
            or SecureNestedDomainAdmissionDecision.AllowedDesignFence;

    public static SecureNestedDomainAdmissionResult AllowedNoEffect { get; } =
        new(
            SecureNestedDomainAdmissionDecision.AllowedNoEffect,
            string.Empty,
            BackendSuccessAuthorized: false,
            MutableNestedStateAuthorized: false);

    public static SecureNestedDomainAdmissionResult AllowedDesignFence { get; } =
        new(
            SecureNestedDomainAdmissionDecision.AllowedDesignFence,
            string.Empty,
            BackendSuccessAuthorized: false,
            MutableNestedStateAuthorized: false);

    public static SecureNestedDomainAdmissionResult Denied(
        SecureNestedDomainAdmissionDecision decision,
        string reason) =>
        new(
            decision,
            reason,
            BackendSuccessAuthorized: false,
            MutableNestedStateAuthorized: false);
}

public sealed partial class SecureNestedDomainAdmissionPolicy
{
    public static SecureNestedDomainAdmissionPolicy Default { get; } = new();

    public SecureNestedDomainAdmissionResult Admit(SecureNestedDomainAdmissionRequest request)
    {
        SecureNestedDomainAdmissionResult checkpoint =
            AdmitCheckpointPayload(
                request.CheckpointPayloadClass,
                request.ShadowVmcsStoresMutableAuthority);
        if (!checkpoint.IsAllowed)
        {
            return checkpoint;
        }

        SecureChildDomainIntentDescriptor? childIntent = request.ChildIntent;
        if (childIntent is null || !childIntent.IsSecureRequest)
        {
            return SecureNestedDomainAdmissionResult.AllowedNoEffect;
        }

        if (!request.HasNeutralChildIntentOwner)
        {
            return Deny(
                SecureNestedDomainAdmissionDecision.DeniedMissingChildIntentOwner,
                "Nested secure child admission requires a neutral runtime-owned child intent descriptor.");
        }

        if (request.ParentDescriptor?.IsActive != true)
        {
            return Deny(
                SecureNestedDomainAdmissionDecision.DeniedMissingParentSecureDescriptor,
                "Nested secure child admission requires a materialized enabled parent secure descriptor.");
        }

        if (!childIntent.IsMaterialized)
        {
            return Deny(
                SecureNestedDomainAdmissionDecision.DeniedUnmaterializedChildIntent,
                "Nested secure child intent must name materialized parent and child domain tags.");
        }

        if (request.ParentHostEvidenceExposedToChild ||
            request.ChildHostEvidenceExposedToParent)
        {
            return Deny(
                SecureNestedDomainAdmissionDecision.DeniedHostEvidenceLeakage,
                "Host-owned nested secure evidence must remain quarantined from child and parent guest-visible state.");
        }

        if (request.NestedProjectionExceedsParent)
        {
            return Deny(
                SecureNestedDomainAdmissionDecision.DeniedNestedProjectionExpansion,
                "Nested compatibility projection cannot expose more than the parent policy allows.");
        }

        SecurePolicyDerivationDecision derivation =
            childIntent.ValidateMonotonicDerivation(
                request.ParentBounds ?? SecureAuthorityBounds.None,
                request.CurrentEpoch);

        return derivation switch
        {
            SecurePolicyDerivationDecision.AllowedSubset =>
                SecureNestedDomainAdmissionResult.AllowedDesignFence,
            SecurePolicyDerivationDecision.ProvenanceMissing =>
                Deny(
                    SecureNestedDomainAdmissionDecision.DeniedChildIntentProvenanceMissing,
                    "Nested secure child policy derivation requires runtime provenance."),
            SecurePolicyDerivationDecision.EpochMismatch =>
                Deny(
                    SecureNestedDomainAdmissionDecision.DeniedStaleParentChildEpoch,
                    "Nested secure parent and child policy derivation must match the current epoch."),
            SecurePolicyDerivationDecision.ChildExpandsAuthority =>
                DenyChildExpansion(
                    request.ParentBounds ?? SecureAuthorityBounds.None,
                    childIntent.RequestedBounds),
            _ =>
                Deny(
                    SecureNestedDomainAdmissionDecision.DeniedMissingParentSecureDescriptor,
                    "Nested secure child derivation requires a materialized parent authority boundary."),
        };
    }

    public SecureNestedDomainAdmissionResult AdmitCheckpointPayload(
        SecureNestedCheckpointPayloadClass payloadClass,
        bool shadowVmcsStoresMutableAuthority = false)
    {
        if (payloadClass is SecureNestedCheckpointPayloadClass.Vmcs12Authority
            or SecureNestedCheckpointPayloadClass.Vmcs02Authority)
        {
            return Deny(
                SecureNestedDomainAdmissionDecision.DeniedNestedVmcsAuthority,
                "Nested secure checkpoint authority cannot be sourced from VMCS12 or VMCS02 state.");
        }

        if (payloadClass == SecureNestedCheckpointPayloadClass.MutableShadowVmcsAuthority ||
            shadowVmcsStoresMutableAuthority)
        {
            return Deny(
                SecureNestedDomainAdmissionDecision.DeniedMutableShadowVmcsAuthority,
                "Shadow VMCS remains a compatibility bridge and cannot own mutable nested secure authority.");
        }

        return SecureNestedDomainAdmissionResult.AllowedDesignFence;
    }

    private static SecureNestedDomainAdmissionResult DenyChildExpansion(
        SecureAuthorityBounds parentBounds,
        SecureAuthorityBounds childBounds)
    {
        if (childBounds.AllowsCompatibilityProjection &&
            !parentBounds.AllowsCompatibilityProjection)
        {
            return Deny(
                SecureNestedDomainAdmissionDecision.DeniedChildCompatibilityProjectionExpansion,
                "Nested child compatibility projection policy cannot exceed the parent policy.");
        }

        if (childBounds.AllowsMigration &&
            !parentBounds.AllowsMigration)
        {
            return Deny(
                SecureNestedDomainAdmissionDecision.DeniedChildMigrationPayloadExpansion,
                "Nested child migration payload policy cannot exceed the parent policy.");
        }

        return Deny(
            SecureNestedDomainAdmissionDecision.DeniedChildPolicyExpansion,
            "Nested child secure policy cannot widen parent memory, I/O, hypercall or debug authority.");
    }

    private static SecureNestedDomainAdmissionResult Deny(
        SecureNestedDomainAdmissionDecision decision,
        string reason) =>
        SecureNestedDomainAdmissionResult.Denied(decision, reason);
}
