namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class DomainSchedulingAdmission
{
    public DomainValidationResult Admit(
        DomainRuntimeContext context,
        byte laneId,
        ulong operationsIssuedThisEpoch = 0)
    {
        if (context.Execution is null)
        {
            return DomainValidationResult.Fail(
                DomainValidationFailureReason.MissingExecutionDomain,
                "Scheduling admission requires an execution-domain descriptor.");
        }

        SchedulingBudgetDescriptor? descriptor = context.Execution.SchedulingBudget;
        if (descriptor is null)
        {
            return DomainValidationResult.Fail(
                DomainValidationFailureReason.MissingSchedulingBudgetDescriptor,
                "Scheduling admission requires a runtime-owned scheduling budget descriptor.");
        }

        if (!descriptor.IsRuntimeAuthoritative)
        {
            return DomainValidationResult.Fail(
                DomainValidationFailureReason.RuntimeAuthorityMissing,
                "Compatibility projection cannot own scheduling admission.");
        }

        if (!descriptor.AcceptsLane(laneId))
        {
            return DomainValidationResult.Fail(
                DomainValidationFailureReason.SchedulingLaneRejected,
                "The scheduling budget descriptor rejects the requested lane.");
        }

        if (descriptor.HasFiniteBudget &&
            operationsIssuedThisEpoch >= descriptor.MaxOperationsPerEpoch)
        {
            return DomainValidationResult.Fail(
                DomainValidationFailureReason.SchedulingBudgetExceeded,
                "The scheduling budget descriptor exhausted its current epoch budget.");
        }

        return DomainValidationResult.Passed;
    }
}
