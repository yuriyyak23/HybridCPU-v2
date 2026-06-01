namespace YAKSys_Hybrid_CPU.Core;

public enum VirtualizationLaneBindingDecision : byte
{
    Allowed = 0,
    DeniedUnvalidatedDescriptor = 1,
    DeniedVmxOwnedLaneResource = 2,
    DeniedWrongSlotClass = 3,
    DeniedWrongPinnedLane = 4,
}

public readonly record struct VirtualizationLaneBindingRequest(
    SlotPlacementMetadata Placement,
    bool DescriptorValidated,
    bool IsCompatibilityFrontend,
    bool IsLaneResourceOwnedByDescriptor);

public sealed partial class VirtualizationLaneBindingPolicy
{
    public const byte Lane6Id = 6;
    public const byte Lane7Id = 7;

    public static SlotPlacementMetadata CompatibilityFrontendPlacement { get; } =
        new()
        {
            RequiredSlotClass = SlotClass.SystemSingleton,
            PinningKind = SlotPinningKind.HardPinned,
            PinnedLaneId = Lane7Id,
        };

    public static SlotPlacementMetadata Lane6DescriptorPlacement { get; } =
        new()
        {
            RequiredSlotClass = SlotClass.DmaStreamClass,
            PinningKind = SlotPinningKind.HardPinned,
            PinnedLaneId = Lane6Id,
        };

    public VirtualizationLaneBindingDecision Evaluate(VirtualizationLaneBindingRequest request)
    {
        if (!request.DescriptorValidated)
        {
            return VirtualizationLaneBindingDecision.DeniedUnvalidatedDescriptor;
        }

        if (!request.IsCompatibilityFrontend && !request.IsLaneResourceOwnedByDescriptor)
        {
            return VirtualizationLaneBindingDecision.DeniedVmxOwnedLaneResource;
        }

        SlotPlacementMetadata expected = request.IsCompatibilityFrontend
            ? CompatibilityFrontendPlacement
            : Lane6DescriptorPlacement;

        if (request.Placement.RequiredSlotClass != expected.RequiredSlotClass ||
            request.Placement.PinningKind != SlotPinningKind.HardPinned)
        {
            return VirtualizationLaneBindingDecision.DeniedWrongSlotClass;
        }

        if (request.Placement.PinnedLaneId != expected.PinnedLaneId)
        {
            return VirtualizationLaneBindingDecision.DeniedWrongPinnedLane;
        }

        return VirtualizationLaneBindingDecision.Allowed;
    }

    public bool CanBind(VirtualizationLaneBindingRequest request) =>
        Evaluate(request) == VirtualizationLaneBindingDecision.Allowed;
}
