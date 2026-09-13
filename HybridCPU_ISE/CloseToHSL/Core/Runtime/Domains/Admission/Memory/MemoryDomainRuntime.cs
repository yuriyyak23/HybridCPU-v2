namespace YAKSys_Hybrid_CPU.Core;

public enum MemoryDomainRuntimeDecision : byte
{
    Allowed = 0,
    MissingDescriptor = 1,
    DescriptorAuthorityDenied = 2,
    MissingAddressSpace = 3,
    MissingTranslationPolicy = 4,
    InvalidTranslationControl = 5,
    SecondStageTranslationDenied = 6,
    MissingDirtyTrackingDescriptor = 7,
}

public readonly record struct MemoryDomainRuntimeRequest(
    MemoryDomainDescriptor? Descriptor,
    bool RequiresAddressSpace,
    bool RequiresTranslationPolicy,
    bool RequiresSecondStageTranslation,
    bool RequiresDirtyTracking);

public readonly record struct MemoryDomainRuntimeResult(
    MemoryDomainRuntimeDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == MemoryDomainRuntimeDecision.Allowed;

    public static MemoryDomainRuntimeResult Allowed { get; } =
        new(MemoryDomainRuntimeDecision.Allowed, "Memory domain runtime admission allowed.");

    public static MemoryDomainRuntimeResult Denied(
        MemoryDomainRuntimeDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class MemoryDomainRuntime
{
    public MemoryDomainRuntimeResult Validate(MemoryDomainRuntimeRequest request)
    {
        if (request.Descriptor is null)
        {
            return MemoryDomainRuntimeResult.Denied(
                MemoryDomainRuntimeDecision.MissingDescriptor,
                "Memory domain runtime requires a memory-domain descriptor.");
        }

        if (!request.Descriptor.IsAuthoritativeMemoryStateOwner)
        {
            return MemoryDomainRuntimeResult.Denied(
                MemoryDomainRuntimeDecision.DescriptorAuthorityDenied,
                "Memory state authority must belong to the memory-domain descriptor.");
        }

        if (request.RequiresAddressSpace && !request.Descriptor.HasAddressSpace)
        {
            return MemoryDomainRuntimeResult.Denied(
                MemoryDomainRuntimeDecision.MissingAddressSpace,
                "Memory domain runtime requires an address-space descriptor.");
        }

        if (request.RequiresTranslationPolicy && !request.Descriptor.HasTranslationPolicy)
        {
            return MemoryDomainRuntimeResult.Denied(
                MemoryDomainRuntimeDecision.MissingTranslationPolicy,
                "Memory domain runtime requires a translation policy.");
        }

        if (!request.Descriptor.HasValidTranslationControl)
        {
            return MemoryDomainRuntimeResult.Denied(
                MemoryDomainRuntimeDecision.InvalidTranslationControl,
                "Memory translation control is invalid.");
        }

        if (request.RequiresSecondStageTranslation && !request.Descriptor.OwnsSecondStageTranslation)
        {
            return MemoryDomainRuntimeResult.Denied(
                MemoryDomainRuntimeDecision.SecondStageTranslationDenied,
                "Second-stage translation authority is not owned by the memory-domain descriptor.");
        }

        if (request.RequiresDirtyTracking && !request.Descriptor.HasDirtyTracking)
        {
            return MemoryDomainRuntimeResult.Denied(
                MemoryDomainRuntimeDecision.MissingDirtyTrackingDescriptor,
                "Memory domain runtime requires a dirty-tracking descriptor.");
        }

        return MemoryDomainRuntimeResult.Allowed;
    }

    public bool CanAdmit(MemoryDomainRuntimeRequest request) =>
        Validate(request).IsAllowed;
}
