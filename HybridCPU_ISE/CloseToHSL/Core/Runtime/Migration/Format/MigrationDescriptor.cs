namespace YAKSys_Hybrid_CPU.Core;

public enum MigrationPayloadClass : byte
{
    GuestArchitecturalState = 0,
    DomainDescriptorState = 1,
    CompatibilityProjectionMetadata = 2,
    HostOwnedRuntimeEvidence = 3,
    SchedulerEvidence = 4,
    BackendBindingEvidence = 5,
    NativeTokenEvidence = 6,
}

public sealed partial class MigrationDescriptor
{
    public MigrationDescriptor()
        : this(
            allowGuestArchitecturalState: false,
            allowDomainDescriptorState: false,
            allowCompatibilityProjectionMetadata: false)
    {
    }

    public MigrationDescriptor(
        bool allowGuestArchitecturalState,
        bool allowDomainDescriptorState,
        bool allowCompatibilityProjectionMetadata)
    {
        AllowGuestArchitecturalState = allowGuestArchitecturalState;
        AllowDomainDescriptorState = allowDomainDescriptorState;
        AllowCompatibilityProjectionMetadata = allowCompatibilityProjectionMetadata;
    }

    public static MigrationDescriptor FailClosed { get; } = new();

    public bool AllowGuestArchitecturalState { get; }

    public bool AllowDomainDescriptorState { get; }

    public bool AllowCompatibilityProjectionMetadata { get; }

    public bool CanSerialize(MigrationPayloadClass payloadClass) =>
        payloadClass switch
        {
            MigrationPayloadClass.GuestArchitecturalState => AllowGuestArchitecturalState,
            MigrationPayloadClass.DomainDescriptorState => AllowDomainDescriptorState,
            MigrationPayloadClass.CompatibilityProjectionMetadata => AllowCompatibilityProjectionMetadata,
            _ => false,
        };

    public bool CanRestore(MigrationPayloadClass payloadClass) =>
        CanSerialize(payloadClass);

    public bool MustRecomputeAfterRestore(MigrationPayloadClass payloadClass) =>
        payloadClass is MigrationPayloadClass.HostOwnedRuntimeEvidence
            or MigrationPayloadClass.SchedulerEvidence
            or MigrationPayloadClass.BackendBindingEvidence
            or MigrationPayloadClass.NativeTokenEvidence;

    public bool MustRejectImportedPayload(MigrationPayloadClass payloadClass) =>
        !CanSerialize(payloadClass);
}
