using HybridCPU_ISE.CloseToRTL.Memory.MMU;
using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core;

public enum IommuDomainAuthority : byte
{
    Runtime = 0,
    IoDomainDescriptor = 1,
    CompatibilityProjection = 2,
}

public sealed partial class IommuDomainDescriptor
{
    public IommuDomainDescriptor()
        : this(
            authority: IommuDomainAuthority.Runtime,
            binding: default,
            requiredPermissions: IOMMUAccessPermissions.None,
            ownsIotlbInvalidation: true,
            allowsCompatibilityProjection: true)
    {
    }

    public IommuDomainDescriptor(
        IommuDomainAuthority authority,
        IommuDomainBinding binding,
        IOMMUAccessPermissions requiredPermissions,
        bool ownsIotlbInvalidation,
        bool allowsCompatibilityProjection)
    {
        Authority = authority;
        Binding = binding;
        RequiredPermissions = requiredPermissions;
        OwnsIotlbInvalidation = ownsIotlbInvalidation;
        AllowsCompatibilityProjection = allowsCompatibilityProjection;
    }

    public IommuDomainAuthority Authority { get; }

    public IommuDomainBinding Binding { get; }

    public IOMMUAccessPermissions RequiredPermissions { get; }

    public bool OwnsIotlbInvalidation { get; }

    public bool AllowsCompatibilityProjection { get; }

    public bool IsRuntimeAuthoritative =>
        Authority == IommuDomainAuthority.Runtime;

    public bool HasValidBinding =>
        Binding.IsValid;

    public bool HasPermissionGrant =>
        RequiredPermissions == IOMMUAccessPermissions.None ||
        Binding.Allows(RequiredPermissions);

    public bool CanInvalidateIotlb =>
        IsRuntimeAuthoritative &&
        OwnsIotlbInvalidation &&
        HasValidBinding;

    public bool CanProjectToCompatibilityFrontend =>
        AllowsCompatibilityProjection &&
        IsRuntimeAuthoritative &&
        HasValidBinding;

    public IommuDomainDescriptor WithBinding(
        IommuDomainBinding binding,
        IOMMUAccessPermissions requiredPermissions) =>
        new(
            Authority,
            binding,
            requiredPermissions,
            OwnsIotlbInvalidation,
            AllowsCompatibilityProjection);
}
