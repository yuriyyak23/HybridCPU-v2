namespace YAKSys_Hybrid_CPU.Core;

internal enum PinBasedControlsProjectionDecision : byte
{
    Granted = 0,
    DeniedMissingOwnerOrSnapshot = 1,
    DeniedStaleForeignOrCrossDomainSnapshot = 2,
    DeniedSemanticMismatch = 3,
}

internal readonly record struct PinBasedControlsProjectionResult(
    PinBasedControlsProjectionDecision Decision,
    ulong? Value,
    string Reason)
{
    internal bool IsGranted => Decision == PinBasedControlsProjectionDecision.Granted;
}

internal static class Phase64PinBasedControlsVmReadProjectionD2
{
    internal const string DecisionId = "D2-HV-VMREAD-PIN-BASED-CONTROLS-0005";
    internal const VmcsField ExactField = VmcsField.PinBasedControls;
    internal const ulong RuntimeTrapPolicyRequiredBit = 1UL << 0;
    internal const ulong NeutralTrapResultRequiredBit = 1UL << 1;
    internal const ulong PublicationFenceRequiredBit = 1UL << 2;
    internal const ulong SupportedMask =
        RuntimeTrapPolicyRequiredBit |
        NeutralTrapResultRequiredBit |
        PublicationFenceRequiredBit;
    internal const ulong UnsupportedMask = ~SupportedMask;
    internal static bool RuntimeAuthorityGranted => false;
    internal static bool ProductionCompositionAuthorized => false;

    internal static PinBasedControlsProjectionResult Project(
        CompatibilityControlPolicyOwner? owner,
        CompatibilityControlPolicySnapshot? snapshot)
    {
        if (owner is null || snapshot is null)
        {
            return Deny(
                PinBasedControlsProjectionDecision.DeniedMissingOwnerOrSnapshot,
                "Exact PinBasedControls projection requires a present neutral owner and snapshot.");
        }
        if (!owner.IsCurrent(snapshot))
        {
            return Deny(
                PinBasedControlsProjectionDecision.DeniedStaleForeignOrCrossDomainSnapshot,
                "Exact PinBasedControls projection requires the owner's current exact-domain snapshot.");
        }

        CompatibilityEventRoutingPolicy policy = snapshot.EventRoutingPolicy;
        if ((policy & ~CompatibilityControlPolicyOwner.KnownEventRoutingPolicyMask) != 0)
        {
            return Deny(
                PinBasedControlsProjectionDecision.DeniedSemanticMismatch,
                "Neutral event-routing policy contains no exact HybridCPU VMX8 mapping.");
        }

        ulong value = 0;
        if ((policy & CompatibilityEventRoutingPolicy.RuntimeTrapPolicyRequired) != 0)
            value |= RuntimeTrapPolicyRequiredBit;
        if ((policy & CompatibilityEventRoutingPolicy.NeutralTrapResultRequired) != 0)
            value |= NeutralTrapResultRequiredBit;
        if ((policy & CompatibilityEventRoutingPolicy.PublicationFenceRequired) != 0)
            value |= PublicationFenceRequiredBit;

        if ((value & UnsupportedMask) != 0)
        {
            return Deny(
                PinBasedControlsProjectionDecision.DeniedSemanticMismatch,
                "Unsupported PinBasedControls bits cannot be projected.");
        }

        return new(
            PinBasedControlsProjectionDecision.Granted,
            value,
            "Current neutral policy mapped exactly to the frozen HybridCPU VMX8 field.");
    }

    private static PinBasedControlsProjectionResult Deny(
        PinBasedControlsProjectionDecision decision,
        string reason) => new(decision, null, reason);
}
