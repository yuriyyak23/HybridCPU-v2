using HybridCPU_ISE.CloseToRTL.Memory.MMU;
using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core;

public enum DmaAuthorityDecision : byte
{
    Allowed = 0,
    MissingIoDomain = 1,
    MissingDmaWindow = 2,
    MissingIommuBinding = 3,
    RuntimeAuthorityMissing = 4,
    PermissionDenied = 5,
    RangeDenied = 6,
    FenceRequired = 7,
}

public readonly record struct DmaAuthorityResult(
    DmaAuthorityDecision Decision,
    DmaFault Fault)
{
    public bool IsAllowed => Decision == DmaAuthorityDecision.Allowed;

    public static DmaAuthorityResult Allowed { get; } =
        new(DmaAuthorityDecision.Allowed, DmaFault.None);

    public static DmaAuthorityResult Denied(
        DmaAuthorityDecision decision,
        DmaFault fault) =>
        new(decision, fault);
}

public sealed partial class DmaAuthorityService
{
    public DmaAuthorityResult ValidateAccess(
        DomainRuntimeContext context,
        IommuDomainBinding binding,
        ulong ioVirtualAddress,
        ulong accessSize,
        IOMMUAccessPermissions requestedPermissions,
        bool fenceSatisfied = false)
    {
        IoDomainDescriptor? io = context.Io;
        if (io is null)
        {
            return Deny(
                DmaAuthorityDecision.MissingIoDomain,
                DmaFaultKind.QueueOwnershipFault,
                ioVirtualAddress,
                requestedPermissions,
                "DMA authority requires an I/O-domain descriptor.");
        }

        if (!io.HasRequiredIoAuthority)
        {
            return Deny(
                DmaAuthorityDecision.RuntimeAuthorityMissing,
                DmaFaultKind.ValidationEvidenceMissing,
                ioVirtualAddress,
                requestedPermissions,
                "I/O-domain descriptor does not own DMA and IOMMU authority.");
        }

        DmaWindowDescriptor? window = io.DmaWindow;
        if (window is null)
        {
            return Deny(
                DmaAuthorityDecision.MissingDmaWindow,
                DmaFaultKind.DescriptorFault,
                ioVirtualAddress,
                requestedPermissions,
                "DMA authority requires a descriptor-owned DMA window.");
        }

        if (!window.IsRuntimeAuthoritative)
        {
            return Deny(
                DmaAuthorityDecision.RuntimeAuthorityMissing,
                DmaFaultKind.ValidationEvidenceMissing,
                ioVirtualAddress,
                requestedPermissions,
                "Compatibility projection cannot own DMA window authority.");
        }

        if (!binding.IsValid)
        {
            return Deny(
                DmaAuthorityDecision.MissingIommuBinding,
                DmaFaultKind.MissingDomainBinding,
                ioVirtualAddress,
                requestedPermissions,
                "DMA authority requires a valid IOMMU domain binding.");
        }

        if (!binding.Allows(requestedPermissions))
        {
            return Deny(
                DmaAuthorityDecision.PermissionDenied,
                DmaFaultKind.PermissionFault,
                ioVirtualAddress,
                requestedPermissions,
                "IOMMU domain binding denies the requested DMA permissions.");
        }

        if (!window.AllowsRange(
                ioVirtualAddress,
                accessSize,
                requestedPermissions))
        {
            return Deny(
                DmaAuthorityDecision.RangeDenied,
                DmaFaultKind.PermissionFault,
                ioVirtualAddress,
                requestedPermissions,
                "Descriptor-owned DMA window denies the requested range.");
        }

        if ((window.RequiresFence || binding.RequiresFence) &&
            !fenceSatisfied)
        {
            return DmaAuthorityResult.Denied(
                DmaAuthorityDecision.FenceRequired,
                DmaFault.Replay(
                    DmaFaultKind.NonCoherentFenceRequired,
                    ioVirtualAddress,
                    IsWrite(requestedPermissions),
                    "DMA authority requires a fence before this access can be admitted."));
        }

        return DmaAuthorityResult.Allowed;
    }

    private static DmaAuthorityResult Deny(
        DmaAuthorityDecision decision,
        DmaFaultKind faultKind,
        ulong ioVirtualAddress,
        IOMMUAccessPermissions requestedPermissions,
        string message) =>
        DmaAuthorityResult.Denied(
            decision,
            DmaFault.Abort(
                faultKind,
                ioVirtualAddress,
                IsWrite(requestedPermissions),
                message));

    private static bool IsWrite(IOMMUAccessPermissions permissions) =>
        (permissions & IOMMUAccessPermissions.Write) != 0;
}
