using System;

namespace YAKSys_Hybrid_CPU.Core.Nested;

public static partial class NestedInterceptTranslator
{
    public static NestedInterceptTranslationResult Translate(
        TrapRequest request,
        TrapPolicyBitmap l1RequestedIntercepts,
        TrapPolicyBitmap l0MandatoryIntercepts,
        NestedInterceptPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(l1RequestedIntercepts);
        ArgumentNullException.ThrowIfNull(l0MandatoryIntercepts);

        VmxTrapProjectionMapper mapper = VmxTrapProjectionMapper.Default;

        if (l0MandatoryIntercepts.Evaluate(request) is { ShouldTrap: true } l0Result)
        {
            VmExitReason reason = mapper.ProjectReason(l0Result.Kind);
            return NestedInterceptTranslationResult.ToL0(
                request,
                reason,
                "L0 mandatory intercept dominates the nested L1 intercept policy.");
        }

        TrapPolicyClass requestClass = ToClass(request.TargetKind);
        if (requestClass == TrapPolicyClass.LaneOperation &&
            !policy.AllowNestedLaneReflection)
        {
            return NestedInterceptTranslationResult.ToL0(
                request,
                VmExitReason.SecurityPolicyViolation,
                "Nested Lane6/Lane7 passthrough is disabled for the first nested release.");
        }

        NeutralTrapResult l1Result = l1RequestedIntercepts.Evaluate(request);
        TrapDecision l1Decision = mapper.Project(l1Result);
        if (!l1Decision.ShouldExit)
        {
            return NestedInterceptTranslationResult.None(request);
        }

        if ((policy.VirtualizableClasses & requestClass) == requestClass)
        {
            return NestedInterceptTranslationResult.ToL1(l1Decision);
        }

        return NestedInterceptTranslationResult.ToL0(
            request,
            VmExitReason.SecurityPolicyViolation,
            "L1 requested an intercept class outside the L0 nested virtualization envelope.");
    }
}
