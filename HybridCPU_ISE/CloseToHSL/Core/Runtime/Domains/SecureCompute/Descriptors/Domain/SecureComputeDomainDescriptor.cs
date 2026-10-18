namespace YAKSys_Hybrid_CPU.Core;

public enum SecureComputeSecurityLevel : byte
{
    Disabled = 0,
    None = 0,
    Measured = 1,
    Private = 2,
    RestrictedInspection = 3,
    SealedRuntime = 4,
}

public sealed partial class SecureComputeDomainDescriptor
{
    public SecureComputeDomainDescriptor()
        : this(
            domainTag: 0,
            securityLevel: SecureComputeSecurityLevel.Disabled,
            measurementRequired: false,
            privateMemoryRequired: false,
            hostInspectionPolicy: SecureHostInspectionPolicy.DenyAll,
            evidenceVisibilityPolicy: SecureEvidencePolicy.FailClosed,
            migrationPolicy: SecureMigrationDescriptor.Disabled,
            ioPolicy: SecureIoDomainDescriptor.Disabled,
            hypercallPolicy: SecureHypercallDescriptor.Disabled,
            debugPolicy: SecureDebugPolicy.Denied,
            compatibilityProjectionPolicy: SecureCompatibilityProjectionPolicy.DenyAll)
    {
    }

    public SecureComputeDomainDescriptor(
        ulong domainTag,
        SecureComputeSecurityLevel securityLevel,
        bool measurementRequired,
        bool privateMemoryRequired,
        SecureHostInspectionPolicy hostInspectionPolicy,
        SecureEvidencePolicy evidenceVisibilityPolicy,
        SecureMigrationDescriptor migrationPolicy,
        SecureIoDomainDescriptor ioPolicy,
        SecureHypercallDescriptor hypercallPolicy,
        SecureDebugPolicy debugPolicy,
        SecureCompatibilityProjectionPolicy compatibilityProjectionPolicy)
    {
        DomainTag = domainTag;
        SecurityLevel = NormalizeSecurityLevel(securityLevel);
        MeasurementRequired = measurementRequired;
        PrivateMemoryRequired = privateMemoryRequired;
        HostInspectionPolicy = hostInspectionPolicy;
        EvidenceVisibilityPolicy = evidenceVisibilityPolicy;
        MigrationPolicy = migrationPolicy;
        IoPolicy = ioPolicy;
        HypercallPolicy = hypercallPolicy;
        DebugPolicy = debugPolicy;
        CompatibilityProjectionPolicy = compatibilityProjectionPolicy;
    }

    public static SecureComputeDomainDescriptor Disabled { get; } = new();

    public ulong DomainTag { get; }

    public SecureComputeSecurityLevel SecurityLevel { get; }

    public bool MeasurementRequired { get; }

    public bool PrivateMemoryRequired { get; }

    public SecureHostInspectionPolicy HostInspectionPolicy { get; }

    public SecureEvidencePolicy EvidenceVisibilityPolicy { get; }

    public SecureMigrationDescriptor MigrationPolicy { get; }

    public SecureIoDomainDescriptor IoPolicy { get; }

    public SecureHypercallDescriptor HypercallPolicy { get; }

    public SecureDebugPolicy DebugPolicy { get; }

    public SecureCompatibilityProjectionPolicy CompatibilityProjectionPolicy { get; }

    public bool IsEnabled => SecurityLevel != SecureComputeSecurityLevel.Disabled;

    public bool IsMaterialized => DomainTag != 0;

    public bool IsActive => IsEnabled && IsMaterialized;

    public bool IsNoEffect => !IsActive;

    public bool RequiresSecureMemoryPolicy =>
        IsActive && PrivateMemoryRequired;

    public bool RequiresMeasurement =>
        IsActive && MeasurementRequired;

    public static SecureComputeSecurityLevel NormalizeSecurityLevel(
        SecureComputeSecurityLevel securityLevel) =>
        securityLevel == SecureComputeSecurityLevel.None
            ? SecureComputeSecurityLevel.Disabled
            : securityLevel;
}
