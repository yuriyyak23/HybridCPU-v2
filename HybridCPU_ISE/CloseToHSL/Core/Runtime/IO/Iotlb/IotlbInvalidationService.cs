namespace YAKSys_Hybrid_CPU.Core;

public enum IotlbInvalidationScope : byte
{
    All = 0,
    IoDomainTag = 1,
    Domain = 2,
    Device = 3,
}

public enum IotlbInvalidationDecision : byte
{
    Allowed = 0,
    MissingIoDomain = 1,
    MissingVirtualizationBlock = 2,
    RuntimeAuthorityMissing = 3,
    InvalidDescriptor = 4,
}

public readonly record struct IotlbInvalidationResult(
    IotlbInvalidationDecision Decision,
    int InvalidatedEntries,
    string Message)
{
    public bool IsAllowed => Decision == IotlbInvalidationDecision.Allowed;

    public static IotlbInvalidationResult Allowed(int invalidatedEntries) =>
        new(IotlbInvalidationDecision.Allowed, invalidatedEntries, string.Empty);

    public static IotlbInvalidationResult Denied(
        IotlbInvalidationDecision decision,
        string message) =>
        new(decision, 0, message);
}

public sealed partial class IotlbInvalidationService
{
    public IotlbInvalidationResult Invalidate(
        DomainRuntimeContext context,
        IotlbInvalidationScope scope,
        ushort ioDomainTag = 0,
        uint domainId = 0,
        uint deviceId = 0)
    {
        IoDomainDescriptor? io = context.Io;
        if (io is null)
        {
            return Deny(
                IotlbInvalidationDecision.MissingIoDomain,
                "IOTLB invalidation requires an I/O-domain descriptor.");
        }

        if (!io.HasRequiredIoAuthority)
        {
            return Deny(
                IotlbInvalidationDecision.RuntimeAuthorityMissing,
                "I/O-domain descriptor does not own IOMMU invalidation authority.");
        }

        IoVirtualizationBlock? virtualizationBlock = io.VirtualizationBlock;
        if (virtualizationBlock is null)
        {
            return Deny(
                IotlbInvalidationDecision.MissingVirtualizationBlock,
                "IOTLB invalidation requires an explicit I/O virtualization block.");
        }

        return scope switch
        {
            IotlbInvalidationScope.All =>
                IotlbInvalidationResult.Allowed(virtualizationBlock.InvalidateIotlbAll()),
            IotlbInvalidationScope.IoDomainTag when ioDomainTag != 0 =>
                IotlbInvalidationResult.Allowed(virtualizationBlock.InvalidateIotlbByIoDomainTag(ioDomainTag)),
            IotlbInvalidationScope.Domain when ioDomainTag != 0 && domainId != 0 =>
                IotlbInvalidationResult.Allowed(virtualizationBlock.InvalidateIotlbByIoDomain(ioDomainTag, domainId)),
            IotlbInvalidationScope.Device when ioDomainTag != 0 && domainId != 0 && deviceId != 0 =>
                IotlbInvalidationResult.Allowed(virtualizationBlock.InvalidateIotlbByDevice(ioDomainTag, domainId, deviceId)),
            _ => Deny(
                IotlbInvalidationDecision.InvalidDescriptor,
                "IOTLB invalidation descriptor is missing required domain identifiers."),
        };
    }

    private static IotlbInvalidationResult Deny(
        IotlbInvalidationDecision decision,
        string message) =>
        IotlbInvalidationResult.Denied(decision, message);
}
