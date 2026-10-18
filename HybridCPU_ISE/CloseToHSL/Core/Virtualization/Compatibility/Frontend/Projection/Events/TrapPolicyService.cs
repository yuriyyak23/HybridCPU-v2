namespace YAKSys_Hybrid_CPU.Core;

public readonly record struct TrapPolicyEvaluationResult(
    DomainValidationResult Validation,
    TrapDecision Decision)
{
    public bool IsValid => Validation.IsValid;

    public static TrapPolicyEvaluationResult Fail(
        DomainValidationFailureReason reason,
        TrapRequest request,
        string message) =>
        new(
            DomainValidationResult.Fail(reason, message),
            VmxTrapProjectionMapper.Default.Project(NeutralTrapResult.Denied(request)));
}

public sealed partial class TrapPolicyService
{
    public TrapPolicyEvaluationResult Evaluate(
        TrapPolicyDescriptor? descriptor,
        TrapPolicyBitmap? bitmap,
        TrapRequest request,
        bool domainValidated = true)
    {
        if (descriptor is null)
        {
            return TrapPolicyEvaluationResult.Fail(
                DomainValidationFailureReason.MissingTrapPolicyDescriptor,
                request,
                "Trap evaluation requires a runtime-owned trap policy descriptor.");
        }

        if (!descriptor.IsRuntimeAuthoritative)
        {
            return TrapPolicyEvaluationResult.Fail(
                DomainValidationFailureReason.RuntimeAuthorityMissing,
                request,
                "Compatibility projection cannot own trap policy evaluation.");
        }

        if (descriptor.RequiresValidatedDomain && !domainValidated)
        {
            return TrapPolicyEvaluationResult.Fail(
                DomainValidationFailureReason.RuntimeAuthorityMissing,
                request,
                "Trap policy evaluation requires a validated domain descriptor.");
        }

        TrapPolicyClass policyClass = ToPolicyClass(request.TargetKind);
        if (policyClass == TrapPolicyClass.None ||
            !descriptor.AllowsClass(policyClass))
        {
            return TrapPolicyEvaluationResult.Fail(
                DomainValidationFailureReason.TrapPolicyClassDenied,
                request,
                "Trap policy descriptor denies the requested trap class.");
        }

        NeutralTrapResult neutralResult = bitmap is null
            ? NeutralTrapResult.Continue(request)
            : bitmap.Evaluate(request);
        return new(
            DomainValidationResult.Passed,
            VmxTrapProjectionMapper.Default.Project(neutralResult));
    }

    private static TrapPolicyClass ToPolicyClass(TrapTargetKind targetKind) =>
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
