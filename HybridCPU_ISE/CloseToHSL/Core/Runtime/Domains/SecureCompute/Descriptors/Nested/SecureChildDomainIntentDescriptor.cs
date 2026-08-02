namespace YAKSys_Hybrid_CPU.Core;

public enum SecureChildDomainIntentState : byte
{
    Missing = 0,
    Declared = 1,
    ParentPolicyValidated = 2,
    Denied = 3,
}

public sealed partial class SecureChildDomainIntentDescriptor
{
    public SecureChildDomainIntentDescriptor()
        : this(
            parentDomainTag: 0,
            childDomainTag: 0,
            requestedSecurityLevel: SecureComputeSecurityLevel.Disabled,
            requestedBounds: SecureAuthorityBounds.None,
            derivation: SecurePolicyDerivationRecord.None,
            state: SecureChildDomainIntentState.Missing)
    {
    }

    public SecureChildDomainIntentDescriptor(
        ulong parentDomainTag,
        ulong childDomainTag,
        SecureComputeSecurityLevel requestedSecurityLevel,
        SecureAuthorityBounds requestedBounds,
        SecurePolicyDerivationRecord derivation,
        SecureChildDomainIntentState state)
    {
        ParentDomainTag = parentDomainTag;
        ChildDomainTag = childDomainTag;
        RequestedSecurityLevel = SecureComputeDomainDescriptor.NormalizeSecurityLevel(requestedSecurityLevel);
        RequestedBounds = requestedBounds;
        Derivation = derivation;
        State = state;
    }

    public static SecureChildDomainIntentDescriptor Missing { get; } = new();

    public ulong ParentDomainTag { get; }

    public ulong ChildDomainTag { get; }

    public SecureComputeSecurityLevel RequestedSecurityLevel { get; }

    public SecureAuthorityBounds RequestedBounds { get; }

    public SecurePolicyDerivationRecord Derivation { get; }

    public SecureChildDomainIntentState State { get; }

    public bool IsMaterialized =>
        State != SecureChildDomainIntentState.Missing &&
        ParentDomainTag != 0 &&
        ChildDomainTag != 0;

    public bool IsSecureRequest =>
        RequestedSecurityLevel != SecureComputeSecurityLevel.Disabled;

    public SecurePolicyDerivationDecision ValidateMonotonicDerivation(
        SecureAuthorityBounds parentBounds,
        SecureRevocationEpoch currentEpoch)
    {
        if (!IsMaterialized || !IsSecureRequest || parentBounds is null)
        {
            return SecurePolicyDerivationDecision.ParentMissing;
        }

        if (!Derivation.HasProvenance)
        {
            return SecurePolicyDerivationDecision.ProvenanceMissing;
        }

        if (!Derivation.MatchesEpoch(currentEpoch))
        {
            return SecurePolicyDerivationDecision.EpochMismatch;
        }

        if (!RequestedBounds.IsSubsetOf(parentBounds))
        {
            return SecurePolicyDerivationDecision.ChildExpandsAuthority;
        }

        return SecurePolicyDerivationDecision.AllowedSubset;
    }
}
