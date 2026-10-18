using System;

namespace YAKSys_Hybrid_CPU.Core.Nested;

public enum NestedExitTarget : byte
{
    None = 0,
    L1 = 1,
    L0 = 2,
}

public readonly record struct NestedInterceptPolicy(
    TrapPolicyClass VirtualizableClasses,
    bool AllowNestedLaneReflection)
{
    public static NestedInterceptPolicy FirstReleaseNoLanePassthrough { get; } =
        new(
            TrapPolicyClass.Instruction |
            TrapPolicyClass.Csr |
            TrapPolicyClass.Memory |
            TrapPolicyClass.CompatibilityOperation,
            AllowNestedLaneReflection: false);
}

public readonly record struct NestedInterceptTranslationResult(
    NestedExitTarget Target,
    VmExitReason ExitReason,
    TrapDecision Decision,
    string Message)
{
    public bool ShouldExit => Target != NestedExitTarget.None;

    public static NestedInterceptTranslationResult None(TrapRequest request) =>
        new(
            NestedExitTarget.None,
            VmExitReason.None,
            TrapDecision.NoExit(request),
            string.Empty);

    public static NestedInterceptTranslationResult ToL1(TrapDecision decision) =>
        new(
            NestedExitTarget.L1,
            decision.ExitReason,
            decision,
            "L1 requested intercept is virtualized inside the L0 policy envelope.");

    public static NestedInterceptTranslationResult ToL0(
        TrapRequest request,
        VmExitReason reason,
        string message) =>
        new(
            NestedExitTarget.L0,
            reason,
            TrapDecision.Exit(request, reason),
            message);
}

public static partial class NestedInterceptTranslator
{
    private static TrapPolicyClass ToClass(TrapTargetKind targetKind) =>
        targetKind switch
        {
            TrapTargetKind.InstructionOpcode => TrapPolicyClass.Instruction,
            TrapTargetKind.CsrAddress => TrapPolicyClass.Csr,
            TrapTargetKind.MemoryRange => TrapPolicyClass.Memory,
            TrapTargetKind.CompatibilityOperation => TrapPolicyClass.CompatibilityOperation,
            TrapTargetKind.LaneOperation => TrapPolicyClass.LaneOperation,
            _ => TrapPolicyClass.None,
        };
}
