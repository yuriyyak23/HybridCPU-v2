namespace YAKSys_Hybrid_CPU.Core;

public enum DomainRuntimeOperationKind : byte
{
    ActivateCompatibilityFrontend = 0,
    DeactivateCompatibilityFrontend = 1,
    EnterDomain = 2,
    ResumeDomain = 3,
    ReadCompatibilityProjection = 4,
    WriteCompatibilityProjection = 5,
    InvalidateTranslation = 6,
    InvokeCapability = 7,
    SaveDomainState = 8,
    RestoreDomainState = 9,
    ProjectCompatibilityTrap = 10,
}

public enum DomainRuntimeOperationSource : byte
{
    CompatibilityFrontend = 0,
    RuntimeService = 1,
    MigrationReplay = 2,
}

public sealed partial class DomainRuntimeOperation
{
    public DomainRuntimeOperation()
        : this(
            DomainRuntimeOperationKind.ActivateCompatibilityFrontend,
            DomainRuntimeOperationSource.CompatibilityFrontend,
            requiresCapabilityGrant: false,
            isProjectionOnly: false)
    {
    }

    public DomainRuntimeOperation(
        DomainRuntimeOperationKind kind,
        DomainRuntimeOperationSource source,
        bool requiresCapabilityGrant,
        bool isProjectionOnly)
    {
        Kind = kind;
        Source = source;
        RequiresCapabilityGrant = requiresCapabilityGrant;
        IsProjectionOnly = isProjectionOnly;
    }

    public DomainRuntimeOperationKind Kind { get; }

    public DomainRuntimeOperationSource Source { get; }

    public bool RequiresCapabilityGrant { get; }

    public bool IsProjectionOnly { get; }

    public static DomainRuntimeOperation FromCompatibilityFrontend(
        DomainRuntimeOperationKind kind,
        bool requiresCapabilityGrant = false,
        bool isProjectionOnly = false) =>
        new(
            kind,
            DomainRuntimeOperationSource.CompatibilityFrontend,
            requiresCapabilityGrant,
            isProjectionOnly);

    public bool CanMutateAuthoritativeState =>
        !IsProjectionOnly &&
        Source != DomainRuntimeOperationSource.MigrationReplay;
}
