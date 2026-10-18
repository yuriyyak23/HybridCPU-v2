namespace YAKSys_Hybrid_CPU.Core;

public enum SecureMemoryAccessKind : byte
{
    RuntimeTouch = 0,
    HostRead = 1,
    DmaRead = 2,
    DmaWrite = 3,
    Measurement = 4,
}

public enum SecureMemoryAccessOrigin : byte
{
    RuntimeBoundary = 0,
    IoDma = 1,
    HypercallArgument = 2,
}

public enum SecureMemoryAdmissionDecision : byte
{
    AllowedSecureMemory = 0,
    DeniedMissingDescriptor = 1,
    DeniedUnmaterializedDescriptor = 2,
    DeniedRegionNotFound = 3,
    DeniedStalePolicyEpoch = 4,
    DeniedPrivateHostRead = 5,
    DeniedHostInspection = 6,
    DeniedPrivateDma = 7,
    DeniedSharedRequiresExplicitPolicy = 8,
    DeniedDmaRequiresTypedGrant = 9,
    DeniedMeasuredRegionMissing = 10,
    DeniedSharedBufferBinding = 11,
    DeniedRuntimeMutableClassification = 12,
}

public readonly record struct SecureMemoryAccessRequest(
    SecureMemoryAccessKind Kind,
    ulong Address,
    ulong Length,
    SecureMemoryAccessOrigin Origin = SecureMemoryAccessOrigin.RuntimeBoundary,
    CapabilityBoundaryRequirement CapabilityRequirement = default,
    CapabilityDescriptorSet? Capabilities = null);

public readonly record struct SecureMemoryAdmissionResult(
    SecureMemoryAdmissionDecision Decision,
    string Reason)
{
    public bool IsAllowed =>
        Decision == SecureMemoryAdmissionDecision.AllowedSecureMemory;

    public static SecureMemoryAdmissionResult AllowedSecureMemory { get; } =
        new(SecureMemoryAdmissionDecision.AllowedSecureMemory, string.Empty);

    public static SecureMemoryAdmissionResult Denied(
        SecureMemoryAdmissionDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class SecureMemoryAdmissionPolicy
{
    public SecureMemoryAdmissionResult Admit(
        SecureMemoryDomainDescriptor? descriptor,
        SecureMemoryAccessRequest request) =>
        Admit(descriptor, request, secureIoPolicy: null);

    public SecureMemoryAdmissionResult Admit(
        SecureMemoryDomainDescriptor? descriptor,
        SecureMemoryAccessRequest request,
        SecureIoDomainDescriptor? secureIoPolicy)
    {
        if (descriptor is null)
        {
            return Deny(
                SecureMemoryAdmissionDecision.DeniedMissingDescriptor,
                "Secure memory access requires a secure memory descriptor.");
        }

        if (!descriptor.IsMaterialized)
        {
            return Deny(
                SecureMemoryAdmissionDecision.DeniedUnmaterializedDescriptor,
                "Secure memory descriptor must be materialized before Stage B memory policy is active.");
        }

        if (!descriptor.TryFindRegion(request.Address, request.Length, out SecureMemoryRegionDescriptor region))
        {
            return Deny(
                request.Kind == SecureMemoryAccessKind.Measurement
                    ? SecureMemoryAdmissionDecision.DeniedMeasuredRegionMissing
                    : SecureMemoryAdmissionDecision.DeniedRegionNotFound,
                "Secure memory access must target an explicit secure memory region.");
        }

        if (!region.IsCurrentFor(descriptor.PolicyEpoch))
        {
            return Deny(
                SecureMemoryAdmissionDecision.DeniedStalePolicyEpoch,
                "Secure memory region policy epoch is stale or unmaterialized.");
        }

        return request.Kind switch
        {
            SecureMemoryAccessKind.RuntimeTouch => AdmitRuntimeTouch(region),
            SecureMemoryAccessKind.HostRead => AdmitHostRead(region),
            SecureMemoryAccessKind.DmaRead or SecureMemoryAccessKind.DmaWrite =>
                AdmitDma(descriptor, secureIoPolicy, region, request),
            SecureMemoryAccessKind.Measurement => AdmitMeasurement(region),
            _ => Deny(
                SecureMemoryAdmissionDecision.DeniedRegionNotFound,
                "Secure memory access kind is not recognized by the secure memory policy."),
        };
    }

    private static SecureMemoryAdmissionResult AdmitHostRead(SecureMemoryRegionDescriptor region)
    {
        if (region.IsPrivate)
        {
            return Deny(
                SecureMemoryAdmissionDecision.DeniedPrivateHostRead,
                "Private secure memory is not host readable.");
        }

        if (!region.IsHostReadable)
        {
            return Deny(
                SecureMemoryAdmissionDecision.DeniedHostInspection,
                "Host inspection requires an explicit shared secure memory region.");
        }

        return SecureMemoryAdmissionResult.AllowedSecureMemory;
    }

    private static SecureMemoryAdmissionResult AdmitDma(
        SecureMemoryDomainDescriptor descriptor,
        SecureIoDomainDescriptor? secureIoPolicy,
        SecureMemoryRegionDescriptor region,
        SecureMemoryAccessRequest request)
    {
        if (region.IsPrivate)
        {
            return Deny(
                SecureMemoryAdmissionDecision.DeniedPrivateDma,
                "DMA to private secure memory is denied, including I/O and hypercall argument paths.");
        }

        if (!region.IsShared ||
            !region.IsHostReadable ||
            !descriptor.AllowsExplicitSharedDma)
        {
            return Deny(
                SecureMemoryAdmissionDecision.DeniedSharedRequiresExplicitPolicy,
                "Secure DMA is allowed only for explicit shared buffers under secure memory DMA policy.");
        }

        if (!TryFindSharedBuffer(
                descriptor,
                secureIoPolicy,
                request,
                out SecureSharedBufferDescriptor sharedBuffer))
        {
            return Deny(
                SecureMemoryAdmissionDecision.DeniedSharedBufferBinding,
                "Secure DMA requires a materialized shared-buffer descriptor with direction, owner, lifetime and evidence class.");
        }

        if (!request.CapabilityRequirement.RequiresTypedGrant ||
            !request.CapabilityRequirement.IsSatisfiedBy(request.Capabilities ?? CapabilityDescriptorSet.Empty))
        {
            return Deny(
                SecureMemoryAdmissionDecision.DeniedDmaRequiresTypedGrant,
                "Secure DMA to an explicit shared buffer requires a typed grant.");
        }

        if (!sharedBuffer.Grant.MatchesEpoch(descriptor.PolicyEpoch))
        {
            return Deny(
                SecureMemoryAdmissionDecision.DeniedSharedBufferBinding,
                "Secure DMA shared-buffer grant must match the secure memory policy epoch.");
        }

        return SecureMemoryAdmissionResult.AllowedSecureMemory;
    }

    private static bool TryFindSharedBuffer(
        SecureMemoryDomainDescriptor descriptor,
        SecureIoDomainDescriptor? secureIoPolicy,
        SecureMemoryAccessRequest request,
        out SecureSharedBufferDescriptor sharedBuffer)
    {
        SecureSharedBufferDirection requiredDirection = request.Kind switch
        {
            SecureMemoryAccessKind.DmaRead => SecureSharedBufferDirection.DomainToDevice,
            SecureMemoryAccessKind.DmaWrite => SecureSharedBufferDirection.DeviceToDomain,
            _ => SecureSharedBufferDirection.None,
        };

        sharedBuffer = default;
        return secureIoPolicy is not null &&
               secureIoPolicy.TryFindSharedBuffer(
            request.Address,
            request.Length,
            requiredDirection,
            descriptor.DomainTag,
            descriptor.PolicyEpoch,
            out sharedBuffer);
    }

    private static SecureMemoryAdmissionResult AdmitMeasurement(SecureMemoryRegionDescriptor region) =>
        region.IsMeasured
            ? SecureMemoryAdmissionResult.AllowedSecureMemory
            : Deny(
                SecureMemoryAdmissionDecision.DeniedMeasuredRegionMissing,
                "Secure measurement requires an explicit measured memory region.");

    private static SecureMemoryAdmissionResult AdmitRuntimeTouch(SecureMemoryRegionDescriptor region) =>
        region.HasRuntimeMutableClassification
            ? SecureMemoryAdmissionResult.AllowedSecureMemory
            : Deny(
                SecureMemoryAdmissionDecision.DeniedRuntimeMutableClassification,
                "Runtime-mutable secure memory requires dirty policy and migration classification.");

    private static SecureMemoryAdmissionResult Deny(
        SecureMemoryAdmissionDecision decision,
        string reason) =>
        SecureMemoryAdmissionResult.Denied(decision, reason);
}
