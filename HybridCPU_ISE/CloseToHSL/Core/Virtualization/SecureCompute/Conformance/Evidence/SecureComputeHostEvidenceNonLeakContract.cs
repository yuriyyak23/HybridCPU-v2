namespace YAKSys_Hybrid_CPU.Core;

public enum SecureComputeEvidenceLeakViolation : byte
{
    None = 0,
    HostEvidenceGuestVisible = 1,
    SchedulerEvidenceSerialized = 2,
    BackendBindingEvidenceSerialized = 3,
    NativeTokenEvidenceSerialized = 4,
    DebugTraceGuestState = 5,
}

public sealed partial class SecureComputeHostEvidenceNonLeakContract
{
    public SecureComputeEvidenceLeakViolation Validate(
        bool hostEvidenceGuestVisible,
        bool schedulerEvidenceSerialized,
        bool backendBindingEvidenceSerialized,
        bool nativeTokenEvidenceSerialized,
        bool debugTraceGuestState)
    {
        if (hostEvidenceGuestVisible)
        {
            return SecureComputeEvidenceLeakViolation.HostEvidenceGuestVisible;
        }

        if (schedulerEvidenceSerialized)
        {
            return SecureComputeEvidenceLeakViolation.SchedulerEvidenceSerialized;
        }

        if (backendBindingEvidenceSerialized)
        {
            return SecureComputeEvidenceLeakViolation.BackendBindingEvidenceSerialized;
        }

        if (nativeTokenEvidenceSerialized)
        {
            return SecureComputeEvidenceLeakViolation.NativeTokenEvidenceSerialized;
        }

        if (debugTraceGuestState)
        {
            return SecureComputeEvidenceLeakViolation.DebugTraceGuestState;
        }

        return SecureComputeEvidenceLeakViolation.None;
    }
}
