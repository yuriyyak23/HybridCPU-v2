using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core.Nested;

public static partial class NestedExitMapper
{
    public static NestedExitMapping FromNestedMemoryComposition(NestedMemoryCompositionResult result)
    {
        if (result.Succeeded || !result.Translation.IsSecondStageFault)
        {
            return NestedExitMapping.None;
        }

        VmExitReason reason = result.Translation.Status == NestedTranslationStatus.SecondStageMisconfiguration
            ? VmExitReason.EptMisconfiguration
            : VmExitReason.EptViolation;

        return result.FaultStage switch
        {
            NestedMemoryCompositionStage.ChildDomainTranslation => new(
                NestedExitTarget.L1,
                reason,
                result.Translation.Violation.QualificationBits,
                "Child-domain translation fault is reflected as an L2-to-L1 virtual VM-exit."),
            NestedMemoryCompositionStage.HostDomainTranslation => new(
                NestedExitTarget.L0,
                reason,
                result.Translation.Violation.QualificationBits,
                "Host-domain translation/IOMMU policy fault remains an L1-to-L0 VM-exit."),
            _ => new(
                NestedExitTarget.L0,
                VmExitReason.SecurityPolicyViolation,
                result.Translation.Violation.QualificationBits,
                "Nested translation fault could not be safely attributed to L1 policy."),
        };
    }
}
