using System;

namespace YAKSys_Hybrid_CPU.Core;

public readonly record struct VmxCompletionProjection(
    VmExitReason ExitReason,
    ulong ExitQualification,
    ulong GuestPhysicalAddress,
    ulong EptViolationQualification)
{
    public static VmxCompletionProjection None { get; } =
        new(VmExitReason.None, 0, 0, 0);
}

public sealed partial class CompletionProjectionService
{
    public VmxCompletionProjection ProjectToVmx(CompletionRecord record)
    {
        if (!CanProjectToVmx(record))
        {
            return VmxCompletionProjection.None;
        }

        return new VmxCompletionProjection(
            ProjectReason(record),
            record.Qualification,
            record.FaultAddress,
            record.FaultAux);
    }

    public VmExitReason ProjectReason(CompletionRecord record)
    {
        if (!CanProjectToVmx(record))
        {
            return VmExitReason.None;
        }

        if (Enum.IsDefined(typeof(VmExitReason), record.ReasonCode))
        {
            return (VmExitReason)record.ReasonCode;
        }

        return VmExitReason.SecurityPolicyViolation;
    }

    public bool CanProjectToVmx(CompletionRecord record) =>
        record is not null &&
        record.IsCompatibilityProjectionSource;
}
