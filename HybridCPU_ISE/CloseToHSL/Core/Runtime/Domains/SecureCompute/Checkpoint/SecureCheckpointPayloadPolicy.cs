namespace YAKSys_Hybrid_CPU.Core;

public enum SecureCheckpointPayloadClass : byte
{
    GuestVisibleState = 0,
    SecurePolicyDescriptor = 1,
    SecureSharedMemory = 2,
    SecurePrivateMemory = 3,
    HostOwnedEvidence = 4,
    SchedulerEvidence = 5,
    BackendBindingEvidence = 6,
    NativeTokenEvidence = 7,
    DebugTrace = 8,
    VmcsProjectionMetadata = 9,
    RawMeasurementSecret = 10,
    CompatibilityProjectionMetadata = 11,
    ActiveHostPointer = 12,
    RawSealingKey = 13,
}

public enum SecureCheckpointPayloadDecision : byte
{
    Allowed = 0,
    DeniedHostOwnedEvidence = 1,
    DeniedCompatibilityProjectionAuthority = 2,
    DeniedPrivateMemoryWithoutSealedPayload = 3,
    DeniedDebugTraceAsGuestState = 4,
    DeniedRawMeasurementSecret = 5,
    DeniedActiveHostPointer = 6,
    DeniedRawSealingKey = 7,
}

public sealed partial class SecureCheckpointPayloadPolicy
{
    public SecureCheckpointPayloadPolicy()
        : this(allowPrivateSealedPayload: false)
    {
    }

    public SecureCheckpointPayloadPolicy(bool allowPrivateSealedPayload)
    {
        AllowPrivateSealedPayload = allowPrivateSealedPayload;
    }

    public static SecureCheckpointPayloadPolicy FailClosed { get; } = new();

    public bool AllowPrivateSealedPayload { get; }

    public SecureCheckpointPayloadDecision Classify(SecureCheckpointPayloadClass payloadClass) =>
        payloadClass switch
        {
            SecureCheckpointPayloadClass.HostOwnedEvidence
                or SecureCheckpointPayloadClass.SchedulerEvidence
                or SecureCheckpointPayloadClass.BackendBindingEvidence
                or SecureCheckpointPayloadClass.NativeTokenEvidence =>
                    SecureCheckpointPayloadDecision.DeniedHostOwnedEvidence,

            SecureCheckpointPayloadClass.DebugTrace =>
                SecureCheckpointPayloadDecision.DeniedDebugTraceAsGuestState,

            SecureCheckpointPayloadClass.RawMeasurementSecret =>
                SecureCheckpointPayloadDecision.DeniedRawMeasurementSecret,

            SecureCheckpointPayloadClass.RawSealingKey =>
                SecureCheckpointPayloadDecision.DeniedRawSealingKey,

            SecureCheckpointPayloadClass.ActiveHostPointer =>
                SecureCheckpointPayloadDecision.DeniedActiveHostPointer,

            SecureCheckpointPayloadClass.VmcsProjectionMetadata
                or SecureCheckpointPayloadClass.CompatibilityProjectionMetadata =>
                SecureCheckpointPayloadDecision.DeniedCompatibilityProjectionAuthority,

            SecureCheckpointPayloadClass.SecurePrivateMemory when !AllowPrivateSealedPayload =>
                SecureCheckpointPayloadDecision.DeniedPrivateMemoryWithoutSealedPayload,

            _ => SecureCheckpointPayloadDecision.Allowed,
        };
}
