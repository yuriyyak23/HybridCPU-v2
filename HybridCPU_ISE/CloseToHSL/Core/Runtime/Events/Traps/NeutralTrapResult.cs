namespace YAKSys_Hybrid_CPU.Core;

public enum NeutralTrapResultKind : byte
{
    None = 0,
    InstructionIntercept = 1,
    CsrIntercept = 2,
    CompatibilityOperationIntercept = 3,
    MemoryIntercept = 4,
    LaneOperationIntercept = 5,
    PreemptionTimerExpired = 6,
    SecurityPolicyViolation = 7,
}

public readonly record struct NeutralTrapResult(
    bool ShouldTrap,
    NeutralTrapResultKind Kind,
    TrapRequest Request)
{
    public static NeutralTrapResult Continue(TrapRequest request) =>
        new(false, NeutralTrapResultKind.None, request);

    public static NeutralTrapResult Trap(
        TrapRequest request,
        NeutralTrapResultKind kind) =>
        new(
            kind != NeutralTrapResultKind.None,
            kind,
            request);

    public static NeutralTrapResult Denied(TrapRequest request) =>
        Trap(request, NeutralTrapResultKind.SecurityPolicyViolation);
}
