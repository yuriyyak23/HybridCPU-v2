namespace YAKSys_Hybrid_CPU.Core;

public enum PrivilegedControlRegisterKind : byte
{
    GuestCr0 = 1,
    GuestCr4 = 2,
}

public readonly record struct PrivilegedControlRegisterValue(
    PrivilegedControlRegisterKind Kind,
    ulong Value);

public readonly record struct PrivilegedExecutionStateEpoch(ulong Current)
{
    public static PrivilegedExecutionStateEpoch Unmaterialized { get; } = new(0);

    public bool IsMaterialized => Current != 0;

    public bool IsCurrent(PrivilegedExecutionStateEpoch candidate) =>
        IsMaterialized &&
        candidate.IsMaterialized &&
        Current == candidate.Current;
}

public enum PrivilegedExecutionStateEvidenceClass : byte
{
    Unclassified = 0,
    GuestVisibleReadOnlyProjection = 1,
    HostOwnedQuarantined = 2,
    CompatibilityAlias = 3,
}

public enum PrivilegedExecutionStateMigrationClass : byte
{
    Unclassified = 0,
    RevalidatedAfterRestore = 1,
    DomainLocal = 2,
}

public readonly record struct PrivilegedControlRegisterLegalityPolicy(
    ulong GuestCr0AllowedMask,
    ulong GuestCr0RequiredMask,
    ulong GuestCr4AllowedMask,
    ulong GuestCr4RequiredMask,
    bool Materialized)
{
    public static PrivilegedControlRegisterLegalityPolicy Unmaterialized { get; } =
        new(0, 0, 0, 0, Materialized: false);

    public bool IsMaterialized =>
        Materialized &&
        (GuestCr0RequiredMask & ~GuestCr0AllowedMask) == 0 &&
        (GuestCr4RequiredMask & ~GuestCr4AllowedMask) == 0;

    public bool HasReservedBits(PrivilegedControlRegisterValue value) =>
        (value.Value & ~AllowedMask(value.Kind)) != 0;

    public bool HasAllRequiredBits(PrivilegedControlRegisterValue value)
    {
        ulong requiredMask = RequiredMask(value.Kind);
        return (value.Value & requiredMask) == requiredMask;
    }

    private ulong AllowedMask(PrivilegedControlRegisterKind kind) =>
        kind switch
        {
            PrivilegedControlRegisterKind.GuestCr0 => GuestCr0AllowedMask,
            PrivilegedControlRegisterKind.GuestCr4 => GuestCr4AllowedMask,
            _ => 0,
        };

    private ulong RequiredMask(PrivilegedControlRegisterKind kind) =>
        kind switch
        {
            PrivilegedControlRegisterKind.GuestCr0 => GuestCr0RequiredMask,
            PrivilegedControlRegisterKind.GuestCr4 => GuestCr4RequiredMask,
            _ => ulong.MaxValue,
        };
}

public readonly record struct PrivilegedExecutionStateDescriptor(
    ulong DomainTag,
    ulong AddressSpaceTag,
    PrivilegedExecutionStateEpoch PolicyEpoch,
    bool Materialized,
    PrivilegedControlRegisterValue GuestCr0,
    PrivilegedControlRegisterValue GuestCr4,
    PrivilegedControlRegisterLegalityPolicy LegalityPolicy,
    PrivilegedExecutionStateEvidenceClass EvidenceClass,
    PrivilegedExecutionStateMigrationClass MigrationClass)
{
    public static PrivilegedExecutionStateDescriptor Unmaterialized { get; } =
        new(
            DomainTag: 0,
            AddressSpaceTag: 0,
            PolicyEpoch: PrivilegedExecutionStateEpoch.Unmaterialized,
            Materialized: false,
            GuestCr0: new PrivilegedControlRegisterValue(PrivilegedControlRegisterKind.GuestCr0, 0),
            GuestCr4: new PrivilegedControlRegisterValue(PrivilegedControlRegisterKind.GuestCr4, 0),
            LegalityPolicy: PrivilegedControlRegisterLegalityPolicy.Unmaterialized,
            EvidenceClass: PrivilegedExecutionStateEvidenceClass.Unclassified,
            MigrationClass: PrivilegedExecutionStateMigrationClass.Unclassified);

    public bool IsMaterialized =>
        Materialized &&
        DomainTag != 0 &&
        AddressSpaceTag != 0 &&
        PolicyEpoch.IsMaterialized &&
        LegalityPolicy.IsMaterialized;

    public bool HasCanonicalRegisterKinds =>
        GuestCr0.Kind == PrivilegedControlRegisterKind.GuestCr0 &&
        GuestCr4.Kind == PrivilegedControlRegisterKind.GuestCr4;
}
