namespace YAKSys_Hybrid_CPU.Core;

public enum SecureCompatibilityProjectionDecision : byte
{
    DeniedByDefault = 0,
    AllowedReadOnlyProjection = 1,
    MissingNeutralOwner = 2,
    MissingReadOnlySource = 3,
    EvidenceDenied = 4,
    MigrationClassMissing = 5,
    WriteDenied = 6,
}

public sealed partial class SecureCompatibilityProjectionPolicy
{
    public SecureCompatibilityProjectionPolicy()
        : this(allowReadOnlyAliases: false, allowedAliasMask: 0)
    {
    }

    public SecureCompatibilityProjectionPolicy(
        bool allowReadOnlyAliases,
        ulong allowedAliasMask)
    {
        AllowReadOnlyAliases = allowReadOnlyAliases;
        AllowedAliasMask = allowedAliasMask;
    }

    public static SecureCompatibilityProjectionPolicy DenyAll { get; } = new();

    public bool AllowReadOnlyAliases { get; }

    public ulong AllowedAliasMask { get; }

    public bool CanProjectAlias(ulong aliasBit) =>
        AllowReadOnlyAliases &&
        aliasBit != 0 &&
        (AllowedAliasMask & aliasBit) == aliasBit;

    public SecureCompatibilityProjectionDecision ValidateReadOnlyProjection(
        ulong aliasBit,
        bool hasNeutralOwner,
        bool hasReadOnlySource,
        bool evidenceAllowed,
        bool migrationClassified)
    {
        if (!hasNeutralOwner)
        {
            return SecureCompatibilityProjectionDecision.MissingNeutralOwner;
        }

        if (!hasReadOnlySource)
        {
            return SecureCompatibilityProjectionDecision.MissingReadOnlySource;
        }

        if (!evidenceAllowed)
        {
            return SecureCompatibilityProjectionDecision.EvidenceDenied;
        }

        if (!migrationClassified)
        {
            return SecureCompatibilityProjectionDecision.MigrationClassMissing;
        }

        return CanProjectAlias(aliasBit)
            ? SecureCompatibilityProjectionDecision.AllowedReadOnlyProjection
            : SecureCompatibilityProjectionDecision.DeniedByDefault;
    }
}
