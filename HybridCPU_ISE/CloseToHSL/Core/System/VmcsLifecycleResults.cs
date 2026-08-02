namespace YAKSys_Hybrid_CPU.Core
{
    public readonly record struct VmcsPointerResult(
        ulong RequestedVmcsAddress,
        bool HasActiveVmcs,
        bool HasLaunchedVmcs);

    public readonly record struct VmcsFieldReadResult(
        VmcsField Field,
        long Value,
        bool HasActiveVmcs);

    public readonly record struct VmcsFieldWriteResult(
        VmcsField Field,
        long Value,
        bool HasActiveVmcs);

    public readonly record struct VmEntryTransitionResult(
        bool Success,
        VmExitReason FailureReason,
        ulong? GuestPc,
        ulong? GuestSp,
        bool HasActiveVmcs,
        bool HasLaunchedVmcs);

    public readonly record struct VmExitTransitionResult(
        bool Success,
        VmExitReason ExitReason,
        ulong SavedGuestPc,
        ulong SavedGuestSp,
        ulong? HostPc,
        ulong? HostSp,
        bool HasActiveVmcs,
        bool HasLaunchedVmcs);
}
