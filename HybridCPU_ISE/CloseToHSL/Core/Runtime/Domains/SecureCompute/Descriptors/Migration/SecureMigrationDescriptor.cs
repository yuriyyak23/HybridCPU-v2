namespace YAKSys_Hybrid_CPU.Core;

public enum SecureMigrationMode : byte
{
    Disabled = 0,
    LocalOnly = 1,
    ReattestRequired = 2,
    PolicyCompatible = 3,
}

public enum SecurePrivateMemoryMigrationPolicy : byte
{
    Denied = 0,
    ReinitializeAfterRestore = 1,
    SealedEncryptedPayloadRequired = 2,
}

public enum SecureMeasurementRestorePolicy : byte
{
    Denied = 0,
    ReuseMaterializedHandle = 1,
    Revalidate = 2,
    Remeasure = 3,
    Reattest = 4,
}

public enum SecureGrantRestorePolicy : byte
{
    Denied = 0,
    PreserveTypedGrants = 1,
    Rederive = 2,
}

public sealed partial class SecureMigrationDescriptor
{
    public SecureMigrationDescriptor()
        : this(
            SecureMigrationMode.Disabled,
            SecurePrivateMemoryMigrationPolicy.Denied,
            SecureRevocationEpoch.Unmaterialized,
            allowGuestVisibleEvidence: false,
            allowCompatibilityProjectionMetadata: false)
    {
    }

    public SecureMigrationDescriptor(
        SecureMigrationMode mode,
        SecurePrivateMemoryMigrationPolicy privateMemoryPolicy,
        SecureRevocationEpoch policyEpoch,
        bool allowGuestVisibleEvidence,
        bool allowCompatibilityProjectionMetadata)
        : this(
            mode,
            privateMemoryPolicy,
            policyEpoch,
            allowGuestVisibleEvidence,
            allowCompatibilityProjectionMetadata,
            mode == SecureMigrationMode.ReattestRequired
                ? SecureMeasurementRestorePolicy.Reattest
                : SecureMeasurementRestorePolicy.Revalidate,
            SecureGrantRestorePolicy.Rederive)
    {
    }

    public SecureMigrationDescriptor(
        SecureMigrationMode mode,
        SecurePrivateMemoryMigrationPolicy privateMemoryPolicy,
        SecureRevocationEpoch policyEpoch,
        bool allowGuestVisibleEvidence,
        bool allowCompatibilityProjectionMetadata,
        SecureMeasurementRestorePolicy measurementRestorePolicy,
        SecureGrantRestorePolicy grantRestorePolicy)
    {
        Mode = mode;
        PrivateMemoryPolicy = privateMemoryPolicy;
        PolicyEpoch = policyEpoch;
        AllowGuestVisibleEvidence = allowGuestVisibleEvidence;
        AllowCompatibilityProjectionMetadata = allowCompatibilityProjectionMetadata;
        MeasurementRestorePolicy = measurementRestorePolicy;
        GrantRestorePolicy = grantRestorePolicy;
    }

    public static SecureMigrationDescriptor Disabled { get; } = new();

    public SecureMigrationMode Mode { get; }

    public SecurePrivateMemoryMigrationPolicy PrivateMemoryPolicy { get; }

    public SecureRevocationEpoch PolicyEpoch { get; }

    public bool AllowGuestVisibleEvidence { get; }

    public bool AllowCompatibilityProjectionMetadata { get; }

    public SecureMeasurementRestorePolicy MeasurementRestorePolicy { get; }

    public SecureGrantRestorePolicy GrantRestorePolicy { get; }

    public bool AllowsMigration =>
        Mode != SecureMigrationMode.Disabled;

    public bool AllowsPrivateMemoryPayload =>
        AllowsMigration &&
        PrivateMemoryPolicy == SecurePrivateMemoryMigrationPolicy.SealedEncryptedPayloadRequired;

    public bool RequiresReattestation =>
        Mode == SecureMigrationMode.ReattestRequired ||
        MeasurementRestorePolicy == SecureMeasurementRestorePolicy.Reattest;

    public bool RequiresMeasurementRevalidation =>
        RequiresReattestation ||
        MeasurementRestorePolicy is SecureMeasurementRestorePolicy.Revalidate
            or SecureMeasurementRestorePolicy.Remeasure;
}
