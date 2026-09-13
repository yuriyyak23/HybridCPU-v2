using System;

namespace YAKSys_Hybrid_CPU.Core.Vmcs.V2;

public enum VmcsV2BlockKind : byte
{
    VmxRootControl = 0,
    VirtualCpu = 1,
    BundleExecution = 2,
    SchedulingBudgetTimer = 3,
    VirtualInterruptFabric = 4,
    VectorStreamState = 5,
    SecurityIsolation = 6,
    CapabilityNegotiation = 7,
    NestedTranslation = 8,
    ExitInformation = 9,
    InterceptBitmap = 10,
    EventInjection = 11,
    LaneCompletionRouting = 12,
    IoVirt = 13,
    Lane6State = 14,
    Lane7State = 15,
    ShadowVmcs = 16,
    DirtyLog = 17,
    DebugTrace = 18,
}

public enum VmcsV2FieldValueType : byte
{
    UInt64 = 0,
    Boolean = 1,
    Epoch = 2,
    GuestIntegerRegisterFile = 3,
    DescriptorReference = 4,
}

public enum VmcsV2FieldAccessKind : byte
{
    GuestArchitecturalState = 0,
    HostArchitecturalState = 1,
    RootOwnedControl = 2,
    ExitInformation = 3,
    HostRuntimeEvidence = 4,
}

public enum VmcsV2ValidationCode : byte
{
    Success = 0,
    UnknownField = 1,
    AccessDenied = 2,
    TypeMismatch = 3,
    ReservedBitsSet = 4,
    GuestGprPersistenceIncomplete = 5,
    HostEvidenceNotGuestVisible = 6,
    VectorStreamStateIncomplete = 7,
    Lane7StateIncomplete = 8,
    NestedVmxDisabled = 9,
    NestedCapabilityMissing = 10,
    NestedPolicyGateFailed = 11,
    InvalidVmcs12 = 12,
    DirtyLogDisabled = 13,
    DirtyLogOverflow = 14,
    CheckpointFormatMismatch = 15,
    CheckpointCapabilityMismatch = 16,
    CheckpointSecurityPolicyMismatch = 17,
    CheckpointGenerationMismatch = 18,
}

public enum VmcsV2ValidationPolicyKind : byte
{
    None = 0,
    Boolean = 1,
    ReservedMustBeZero = 2,
    GuestGprPersistenceRequired = 3,
    HostEvidenceForbidden = 4,
}

public enum VmcsV2MigrationPolicyKind : byte
{
    MigratableGuestState = 0,
    RootLocalState = 1,
    ReconstructableRuntimeState = 2,
    HostOnlyEvidence = 3,
}

public readonly record struct VmcsV2AccessPolicy(
    VmcsV2FieldAccessKind Kind,
    bool CanReadViaVmRead,
    bool CanWriteViaVmWrite,
    bool GuestVisible,
    bool HostRuntimeEvidence)
{
    public static VmcsV2AccessPolicy GuestState { get; } =
        new(VmcsV2FieldAccessKind.GuestArchitecturalState, true, true, true, false);

    public static VmcsV2AccessPolicy HostState { get; } =
        new(VmcsV2FieldAccessKind.HostArchitecturalState, true, true, false, false);

    public static VmcsV2AccessPolicy RootControl { get; } =
        new(VmcsV2FieldAccessKind.RootOwnedControl, true, true, false, false);

    public static VmcsV2AccessPolicy ExitInfo { get; } =
        new(VmcsV2FieldAccessKind.ExitInformation, true, true, true, false);

    public static VmcsV2AccessPolicy HostEvidence { get; } =
        new(VmcsV2FieldAccessKind.HostRuntimeEvidence, false, false, false, true);
}

public readonly record struct VmcsV2ValidationPolicy(
    VmcsV2ValidationPolicyKind Kind,
    ulong ReservedMask = 0)
{
    public static VmcsV2ValidationPolicy None { get; } =
        new(VmcsV2ValidationPolicyKind.None);

    public static VmcsV2ValidationPolicy Boolean { get; } =
        new(VmcsV2ValidationPolicyKind.Boolean);

    public static VmcsV2ValidationPolicy GuestGprPersistenceRequired { get; } =
        new(VmcsV2ValidationPolicyKind.GuestGprPersistenceRequired);

    public static VmcsV2ValidationPolicy HostEvidenceForbidden { get; } =
        new(VmcsV2ValidationPolicyKind.HostEvidenceForbidden);
}

public readonly record struct VmcsV2MigrationPolicy(
    VmcsV2MigrationPolicyKind Kind,
    bool RequiresMaterializedGuestState)
{
    public static VmcsV2MigrationPolicy MigratableGuestState { get; } =
        new(VmcsV2MigrationPolicyKind.MigratableGuestState, true);

    public static VmcsV2MigrationPolicy RootLocalState { get; } =
        new(VmcsV2MigrationPolicyKind.RootLocalState, false);

    public static VmcsV2MigrationPolicy ReconstructableRuntimeState { get; } =
        new(VmcsV2MigrationPolicyKind.ReconstructableRuntimeState, false);

    public static VmcsV2MigrationPolicy HostOnlyEvidence { get; } =
        new(VmcsV2MigrationPolicyKind.HostOnlyEvidence, false);
}

public readonly record struct VmcsV2ValidationResult(
    bool Succeeded,
    VmcsV2ValidationCode Code,
    ushort FieldId,
    string Message)
{
    public static VmcsV2ValidationResult Success(ushort fieldId = 0) =>
        new(true, VmcsV2ValidationCode.Success, fieldId, string.Empty);

    public static VmcsV2ValidationResult Fail(
        VmcsV2ValidationCode code,
        ushort fieldId,
        string message) =>
        new(false, code, fieldId, message);
}

public readonly record struct VmcsV2FieldDescriptor(
    ushort FieldId,
    string Name,
    VmcsV2BlockKind Block,
    VmcsV2FieldValueType ValueType,
    VmcsV2AccessPolicy AccessPolicy,
    VmcsV2ValidationPolicy ValidationPolicy,
    VmcsV2MigrationPolicy MigrationPolicy,
    ulong InvalidationEpoch)
{
    public bool IsVmReadVisible => AccessPolicy.CanReadViaVmRead;

    public bool IsVmWriteVisible => AccessPolicy.CanWriteViaVmWrite;
}
