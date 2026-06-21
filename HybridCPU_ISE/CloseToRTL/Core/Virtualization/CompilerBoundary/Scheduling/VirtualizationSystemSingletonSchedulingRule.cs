namespace YAKSys_Hybrid_CPU.Core;

public enum VirtualizationSchedulingDecision : byte
{
    Allowed = 0,
    DeniedUnvalidatedDescriptor = 1,
    DeniedNotSystemSingleton = 2,
    DeniedNotHardPinned = 3,
    DeniedNotLane7 = 4,
    DeniedMultipleSystemSingletons = 5,
}

public readonly record struct VirtualizationSystemSingletonSchedulingRequest(
    SlotPlacementMetadata Placement,
    bool DescriptorValidated,
    byte SystemSingletonCount);

public sealed partial class VirtualizationSystemSingletonSchedulingRule
{
    public VirtualizationSchedulingDecision Evaluate(VirtualizationSystemSingletonSchedulingRequest request)
    {
        if (!request.DescriptorValidated)
        {
            return VirtualizationSchedulingDecision.DeniedUnvalidatedDescriptor;
        }

        if (request.Placement.RequiredSlotClass != SlotClass.SystemSingleton)
        {
            return VirtualizationSchedulingDecision.DeniedNotSystemSingleton;
        }

        if (request.Placement.PinningKind != SlotPinningKind.HardPinned)
        {
            return VirtualizationSchedulingDecision.DeniedNotHardPinned;
        }

        if (request.Placement.PinnedLaneId != VirtualizationLaneBindingPolicy.Lane7Id)
        {
            return VirtualizationSchedulingDecision.DeniedNotLane7;
        }

        if (request.SystemSingletonCount > 1)
        {
            return VirtualizationSchedulingDecision.DeniedMultipleSystemSingletons;
        }

        return VirtualizationSchedulingDecision.Allowed;
    }

    public bool CanSchedule(VirtualizationSystemSingletonSchedulingRequest request) =>
        Evaluate(request) == VirtualizationSchedulingDecision.Allowed;
}
