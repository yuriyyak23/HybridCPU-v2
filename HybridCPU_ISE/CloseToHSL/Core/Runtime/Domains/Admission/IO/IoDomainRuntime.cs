namespace YAKSys_Hybrid_CPU.Core;

public enum IoDomainRuntimeDecision : byte
{
    Allowed = 0,
    MissingDescriptor = 1,
    DescriptorAuthorityDenied = 2,
    DmaAuthorityDenied = 3,
    IommuAuthorityDenied = 4,
    MissingVirtualizationBlock = 5,
    MissingDmaWindow = 6,
    CompatibilityProjectionDenied = 7,
}

public readonly record struct IoDomainRuntimeRequest(
    IoDomainDescriptor? Descriptor,
    bool RequiresDmaAuthority,
    bool RequiresIommuAuthority,
    bool RequiresVirtualizationBlock,
    bool RequiresDmaWindow,
    bool RequiresCompatibilityProjection);

public readonly record struct IoDomainRuntimeResult(
    IoDomainRuntimeDecision Decision,
    string Reason)
{
    public bool IsAllowed => Decision == IoDomainRuntimeDecision.Allowed;

    public static IoDomainRuntimeResult Allowed { get; } =
        new(IoDomainRuntimeDecision.Allowed, "I/O domain runtime admission allowed.");

    public static IoDomainRuntimeResult Denied(
        IoDomainRuntimeDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class IoDomainRuntime
{
    public IoDomainRuntimeResult Validate(IoDomainRuntimeRequest request)
    {
        if (request.Descriptor is null)
        {
            return IoDomainRuntimeResult.Denied(
                IoDomainRuntimeDecision.MissingDescriptor,
                "I/O domain runtime requires an I/O-domain descriptor.");
        }

        if (!request.Descriptor.IsAuthoritativeIoStateOwner)
        {
            return IoDomainRuntimeResult.Denied(
                IoDomainRuntimeDecision.DescriptorAuthorityDenied,
                "I/O state authority must belong to the I/O-domain descriptor.");
        }

        if (request.RequiresDmaAuthority && !request.Descriptor.OwnsDmaAuthority)
        {
            return IoDomainRuntimeResult.Denied(
                IoDomainRuntimeDecision.DmaAuthorityDenied,
                "DMA authority is not owned by the I/O-domain descriptor.");
        }

        if (request.RequiresIommuAuthority && !request.Descriptor.OwnsIommuAuthority)
        {
            return IoDomainRuntimeResult.Denied(
                IoDomainRuntimeDecision.IommuAuthorityDenied,
                "IOMMU authority is not owned by the I/O-domain descriptor.");
        }

        if (request.RequiresVirtualizationBlock && !request.Descriptor.HasVirtualizationBlock)
        {
            return IoDomainRuntimeResult.Denied(
                IoDomainRuntimeDecision.MissingVirtualizationBlock,
                "I/O domain runtime requires an I/O virtualization block.");
        }

        if (request.RequiresDmaWindow && !request.Descriptor.HasDmaWindow)
        {
            return IoDomainRuntimeResult.Denied(
                IoDomainRuntimeDecision.MissingDmaWindow,
                "I/O domain runtime requires a DMA window descriptor.");
        }

        if (request.RequiresCompatibilityProjection && !request.Descriptor.CompatibilityProjectionEnabled)
        {
            return IoDomainRuntimeResult.Denied(
                IoDomainRuntimeDecision.CompatibilityProjectionDenied,
                "I/O descriptor denies compatibility projection.");
        }

        return IoDomainRuntimeResult.Allowed;
    }

    public bool CanAdmit(IoDomainRuntimeRequest request) =>
        Validate(request).IsAllowed;
}
