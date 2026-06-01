using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core.Nested;

public readonly record struct NestedExitMapping(
    NestedExitTarget Target,
    VmExitReason ExitReason,
    ulong Qualification,
    string Message)
{
    public bool ExitsToL1 => Target == NestedExitTarget.L1;

    public bool ExitsToL0 => Target == NestedExitTarget.L0;

    public static NestedExitMapping None { get; } =
        new(NestedExitTarget.None, VmExitReason.None, 0, string.Empty);
}

public static partial class NestedExitMapper
{
    public static NestedExitMapping FromIntercept(NestedInterceptTranslationResult intercept) =>
        intercept.Target switch
        {
            NestedExitTarget.L1 => new(
                NestedExitTarget.L1,
                intercept.ExitReason,
                intercept.Decision.ExitQualification.Encode(),
                intercept.Message),
            NestedExitTarget.L0 => new(
                NestedExitTarget.L0,
                intercept.ExitReason,
                intercept.Decision.ExitQualification.Encode(),
                intercept.Message),
            _ => NestedExitMapping.None,
        };

    public static NestedExitMapping InvalidChildDomainIntent(ushort fieldId, string message) =>
        new(
            NestedExitTarget.L0,
            VmExitReason.SecurityPolicyViolation,
            fieldId,
            message);

    public static NestedExitMapping InvalidVmcs12(ushort fieldId, string message) =>
        InvalidChildDomainIntent(fieldId, message);

    public static NestedExitMapping UnsupportedOperation(VmxOperationKind operation) =>
        new(
            NestedExitTarget.L0,
            VmExitReason.SecurityPolicyViolation,
            (ulong)operation,
            "Nested compatibility operation is unsupported by the L0 policy envelope.");

    public static NestedExitMapping VirtualInterrupt(
        EventInjectionDescriptor descriptor,
        bool reflectToL1)
    {
        if (!descriptor.IsValid)
        {
            return NestedExitMapping.None;
        }

        return new(
            reflectToL1 ? NestedExitTarget.L1 : NestedExitTarget.L0,
            VmExitReason.VirtualInterrupt,
            descriptor.Payload,
            reflectToL1
                ? "Virtual interrupt is deterministically reflected to L1."
                : "Virtual interrupt requires L0 handling by nested policy.");
    }

    public static NestedExitMapping PreemptionTimer(
        TrapDecision decision,
        bool reflectToL1)
    {
        if (!decision.ShouldExit)
        {
            return NestedExitMapping.None;
        }

        return new(
            reflectToL1 ? NestedExitTarget.L1 : NestedExitTarget.L0,
            VmExitReason.VmxPreemptionTimerExpired,
            decision.ExitQualification.Encode(),
            reflectToL1
                ? "L2 preemption timer expiry is reflected to L1."
                : "L2 preemption timer expiry is retained by L0 scheduling policy.");
    }

    public static NestedExitMapping LaneOperationBlocked(TrapRequest request) =>
        new(
            NestedExitTarget.L0,
            VmExitReason.SecurityPolicyViolation,
            request.Target | ((request.Auxiliary & 0xFFFFUL) << 16),
            "Nested Lane6/Lane7 passthrough is blocked; no host token, backend binding, or completion evidence is exposed.");
}
