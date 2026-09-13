namespace YAKSys_Hybrid_CPU.Core.Vmcs.V2;

public enum VmxCompletionKind : byte
{
    Success = 0,
    VmFailValid = 1,
    VmFailInvalid = 2,
    VmAbort = 3,
    VmExit = 4,
}

public enum VmFailCode : ushort
{
    None = 0,
    UnknownVmcsField = 1,
    VmcsFieldAccessDenied = 2,
    GuestGprPersistenceIncomplete = 3,
    HostEvidenceAccessDenied = 4,
}

public enum VmAbortCode : ushort
{
    None = 0,
    CorruptVmcsDescriptor = 1,
}
