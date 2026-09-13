namespace YAKSys_Hybrid_CPU.Core;

public enum VirtualizationBundleLegalityDecision : byte
{
    Allowed = 0,
    MissingDescriptor = 1,
    DescriptorValidationDenied = 2,
    RuntimeAuthorityRequired = 3,
    RuntimeValidationDenied = 4,
    CompilerEvidenceAsAuthorityDenied = 5,
    CompatibilityProjectionDenied = 6,
    LaneBindingDenied = 7,
    SchedulingDenied = 8,
}

public readonly record struct VirtualizationBundleLegalityRequest(
    BundleLegalityDescriptor? Descriptor,
    VirtualizationLaneBindingDecision LaneBindingDecision,
    VirtualizationSchedulingDecision SchedulingDecision,
    bool DescriptorValidated,
    bool RuntimeValidated,
    bool UsesCompilerEvidenceAsAuthority,
    bool RequiresCompatibilityProjection);

public readonly record struct VirtualizationBundleLegalityResult(
    VirtualizationBundleLegalityDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == VirtualizationBundleLegalityDecision.Allowed;

    public static VirtualizationBundleLegalityResult Allowed() =>
        new(VirtualizationBundleLegalityDecision.Allowed, "Bundle legality is runtime-authorized and validated.");

    public static VirtualizationBundleLegalityResult Denied(
        VirtualizationBundleLegalityDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class VirtualizationBundleLegalityRule
{
    public VirtualizationBundleLegalityResult Evaluate(VirtualizationBundleLegalityRequest request)
    {
        if (request.Descriptor is null)
        {
            return VirtualizationBundleLegalityResult.Denied(
                VirtualizationBundleLegalityDecision.MissingDescriptor,
                "Bundle legality requires an execution-domain descriptor.");
        }

        if (!request.DescriptorValidated)
        {
            return VirtualizationBundleLegalityResult.Denied(
                VirtualizationBundleLegalityDecision.DescriptorValidationDenied,
                "Bundle legality cannot be evaluated from an unvalidated descriptor.");
        }

        if (!request.Descriptor.IsRuntimeAuthoritative)
        {
            return VirtualizationBundleLegalityResult.Denied(
                VirtualizationBundleLegalityDecision.RuntimeAuthorityRequired,
                "Runtime-owned bundle legality is required.");
        }

        if (request.Descriptor.RequiresRuntimeValidation && !request.RuntimeValidated)
        {
            return VirtualizationBundleLegalityResult.Denied(
                VirtualizationBundleLegalityDecision.RuntimeValidationDenied,
                "Runtime validation is required before bundling.");
        }

        if (request.UsesCompilerEvidenceAsAuthority || request.Descriptor.CanUseCompilerEvidenceAsAuthority)
        {
            return VirtualizationBundleLegalityResult.Denied(
                VirtualizationBundleLegalityDecision.CompilerEvidenceAsAuthorityDenied,
                "Compiler evidence may accompany bundling but cannot be bundle-legality authority.");
        }

        if (request.RequiresCompatibilityProjection && !request.Descriptor.CanProjectToCompatibilityFrontend)
        {
            return VirtualizationBundleLegalityResult.Denied(
                VirtualizationBundleLegalityDecision.CompatibilityProjectionDenied,
                "Compatibility projection is only allowed from runtime-authoritative descriptors.");
        }

        if (request.LaneBindingDecision != VirtualizationLaneBindingDecision.Allowed)
        {
            return VirtualizationBundleLegalityResult.Denied(
                VirtualizationBundleLegalityDecision.LaneBindingDenied,
                "Lane binding denied the virtualization bundle placement.");
        }

        if (request.SchedulingDecision != VirtualizationSchedulingDecision.Allowed)
        {
            return VirtualizationBundleLegalityResult.Denied(
                VirtualizationBundleLegalityDecision.SchedulingDenied,
                "System-singleton scheduling denied the virtualization bundle placement.");
        }

        return VirtualizationBundleLegalityResult.Allowed();
    }

    public bool CanBundle(VirtualizationBundleLegalityRequest request) =>
        Evaluate(request).IsAllowed;
}
