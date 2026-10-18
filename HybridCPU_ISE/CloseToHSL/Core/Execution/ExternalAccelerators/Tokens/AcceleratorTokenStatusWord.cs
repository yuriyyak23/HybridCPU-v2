using System;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

public enum AcceleratorTokenState : byte
{
    Created = 0,
    Validated = 1,
    Queued = 2,
    Running = 3,
    DeviceComplete = 4,
    CommitPending = 5,
    Committed = 6,
    Faulted = 7,
    Canceled = 8,
    TimedOut = 9,
    Abandoned = 10
}

public enum AcceleratorTokenFaultCode : byte
{
    None = 0,
    InvalidHandle = 1,
    MissingGuardEvidence = 2,
    OwnerDomainRejected = 3,
    MappingEpochDrift = 4,
    IommuDomainEpochDrift = 5,
    DescriptorNotGuardBacked = 6,
    CapabilityNotAccepted = 7,
    SubmitAdmissionRejected = 8,
    IllegalTransition = 9,
    TerminalState = 10,
    CommitNotImplemented = 11,
    BackendExecutionUnavailable = 12,
    QueueExecutionUnavailable = 13,
    MemoryPublicationForbidden = 14,
    TokenHandleNotAuthority = 15,
    CapabilityRejected = 16,
    PreciseFault = 17,
    TimedOut = 18,
    QueueAdmissionRejected = 19,
    QueueFull = 20,
    DeviceBusy = 21,
    BackendRejected = 22,
    DirectWriteViolation = 23,
    SourceReadRejected = 24,
    StagingRejected = 25,
    ConflictRejected = 26,
    CommitCoordinatorRequired = 27,
    DescriptorIdentityMismatch = 28,
    NormalizedFootprintMismatch = 29,
    StagedCoverageMismatch = 30,
    CommitConflictRejected = 31,
    CommitMemoryFault = 32,
    RollbackFailed = 33,
    CancelRejected = 34,
    FenceRejected = 35,
    FaultPublicationRejected = 36,
    Lane7PressureRejected = 37,
    Unknown = 255
}

[Flags]
public enum AcceleratorTokenStatusFlags : byte
{
    None = 0,
    Terminal = 1 << 0,
    GuardRejected = 1 << 1,
    NonTrappingReject = 1 << 2,
    PreciseFault = 1 << 3,
    CommitPublicationForbidden = 1 << 4,
    ModelOnly = 1 << 5,
    TimeoutObserved = 1 << 6
}

public readonly record struct AcceleratorTokenStatusWord(
    AcceleratorTokenState State,
    AcceleratorTokenFaultCode FaultCode,
    AcceleratorTokenStatusFlags Flags,
    uint ImplementationStatusSequence)
{
    public const int TokenStateShift = 0;
    public const int FaultCodeShift = 8;
    public const int FlagsShift = 16;
    public const int ReservedZeroShift = 24;
    public const int ImplementationStatusSequenceShift = 32;

    public byte ReservedZero => 0;

    public bool IsTerminal =>
        State is AcceleratorTokenState.Committed
            or AcceleratorTokenState.Faulted
            or AcceleratorTokenState.Canceled
            or AcceleratorTokenState.TimedOut
            or AcceleratorTokenState.Abandoned;

    public ulong Pack()
    {
        ulong packed = (byte)State;
        packed |= (ulong)(byte)FaultCode << FaultCodeShift;
        packed |= (ulong)(byte)Flags << FlagsShift;
        packed |= (ulong)ImplementationStatusSequence << ImplementationStatusSequenceShift;
        return packed;
    }

    public static AcceleratorTokenStatusWord FromToken(AcceleratorToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return new AcceleratorTokenStatusWord(
            token.State,
            token.FaultCode,
            token.IsTerminal
                ? AcceleratorTokenStatusFlags.Terminal | AcceleratorTokenStatusFlags.ModelOnly
                : AcceleratorTokenStatusFlags.ModelOnly,
            token.StatusSequence);
    }

    public static AcceleratorTokenStatusWord Faulted(
        AcceleratorTokenFaultCode faultCode,
        AcceleratorTokenStatusFlags flags = AcceleratorTokenStatusFlags.None,
        uint implementationStatusSequence = 0)
    {
        if (faultCode == AcceleratorTokenFaultCode.None)
        {
            throw new ArgumentException(
                "A faulted token status word requires a non-None fault code.",
                nameof(faultCode));
        }

        return new AcceleratorTokenStatusWord(
            AcceleratorTokenState.Faulted,
            faultCode,
            flags | AcceleratorTokenStatusFlags.Terminal,
            implementationStatusSequence);
    }

    public static AcceleratorTokenStatusWord Unpack(ulong packed)
    {
        return new AcceleratorTokenStatusWord(
            (AcceleratorTokenState)(packed & 0xFF),
            (AcceleratorTokenFaultCode)((packed >> FaultCodeShift) & 0xFF),
            (AcceleratorTokenStatusFlags)((packed >> FlagsShift) & 0xFF),
            (uint)(packed >> ImplementationStatusSequenceShift));
    }

    public static bool HasReservedBitsSet(ulong packed) =>
        ((packed >> ReservedZeroShift) & 0xFFUL) != 0;
}
