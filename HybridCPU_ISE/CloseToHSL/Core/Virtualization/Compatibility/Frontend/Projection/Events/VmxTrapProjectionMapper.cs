namespace YAKSys_Hybrid_CPU.Core;

public sealed class VmxTrapProjectionMapper
{
    public static VmxTrapProjectionMapper Default { get; } = new();

    public TrapDecision Project(NeutralTrapResult result) =>
        result.ShouldTrap
            ? TrapDecision.Exit(result.Request, ProjectReason(result))
            : TrapDecision.NoExit(result.Request);

    public VmExitReason ProjectReason(NeutralTrapResult result) =>
        result.Kind == NeutralTrapResultKind.CompatibilityOperationIntercept
            ? ProjectCompatibilityOperationReason(result.Request)
            : ProjectReason(result.Kind);

    public VmExitReason ProjectReason(NeutralTrapResultKind kind) =>
        kind switch
        {
            NeutralTrapResultKind.None => VmExitReason.None,
            NeutralTrapResultKind.InstructionIntercept => VmExitReason.InstructionIntercept,
            NeutralTrapResultKind.CsrIntercept => VmExitReason.CsrIntercept,
            NeutralTrapResultKind.CompatibilityOperationIntercept => VmExitReason.VmxOperationIntercept,
            NeutralTrapResultKind.MemoryIntercept => VmExitReason.MemoryIntercept,
            NeutralTrapResultKind.LaneOperationIntercept => VmExitReason.LaneOperationIntercept,
            NeutralTrapResultKind.PreemptionTimerExpired => VmExitReason.VmxPreemptionTimerExpired,
            NeutralTrapResultKind.SecurityPolicyViolation => VmExitReason.SecurityPolicyViolation,
            _ => VmExitReason.SecurityPolicyViolation,
        };

    private static VmExitReason ProjectCompatibilityOperationReason(TrapRequest request) =>
        request.TargetKind == TrapTargetKind.CompatibilityOperation &&
        unchecked((byte)request.Target) == (byte)VmxOperationKind.VmCall
            ? VmExitReason.VmCall
            : VmExitReason.VmxOperationIntercept;
}

public readonly partial record struct TrapRequest
{
    public static TrapRequest ForVmxOperation(
        VmxOperationKind operation,
        ushort opcode,
        byte vtId,
        ushort executionDomainTag = 0,
        ushort addressSpaceTag = 0) =>
        ForCompatibilityOperation((byte)operation, opcode, vtId, executionDomainTag, addressSpaceTag);
}

public sealed partial class TrapPolicyBitmap
{
    public void EnableVmxOperation(VmxOperationKind operation) =>
        EnableCompatibilityOperation((byte)operation);

    public bool InterceptsVmxOperation(VmxOperationKind operation) =>
        InterceptsCompatibilityOperation((byte)operation);
}
