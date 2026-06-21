using HybridCPU_ISE.CloseToRTL.Memory.MMU;

namespace YAKSys_Hybrid_CPU.Core;

public enum DmaWindowAuthority : byte
{
    IoDomain = 0,
    CompatibilityProjection = 1,
}

public sealed partial class DmaWindowDescriptor
{
    public DmaWindowDescriptor()
        : this(
            authority: DmaWindowAuthority.IoDomain,
            baseAddress: 0,
            length: 0,
            permissions: IOMMUAccessPermissions.None,
            requiresFence: true,
            allowsCompatibilityProjection: true)
    {
    }

    public DmaWindowDescriptor(
        DmaWindowAuthority authority,
        ulong baseAddress,
        ulong length,
        IOMMUAccessPermissions permissions,
        bool requiresFence,
        bool allowsCompatibilityProjection)
    {
        Authority = authority;
        BaseAddress = baseAddress;
        Length = length;
        Permissions = permissions;
        RequiresFence = requiresFence;
        AllowsCompatibilityProjection = allowsCompatibilityProjection;
    }

    public DmaWindowAuthority Authority { get; }

    public ulong BaseAddress { get; }

    public ulong Length { get; }

    public IOMMUAccessPermissions Permissions { get; }

    public bool RequiresFence { get; }

    public bool AllowsCompatibilityProjection { get; }

    public bool IsRuntimeAuthoritative =>
        Authority == DmaWindowAuthority.IoDomain;

    public bool IsEmpty => Length == 0;

    public bool IsValid =>
        !IsEmpty &&
        Permissions != IOMMUAccessPermissions.None &&
        BaseAddress <= ulong.MaxValue - (Length - 1);

    public bool CanProjectToCompatibilityFrontend =>
        AllowsCompatibilityProjection &&
        IsRuntimeAuthoritative;

    public bool AllowsRange(
        ulong ioVirtualAddress,
        ulong accessSize,
        IOMMUAccessPermissions requestedPermissions)
    {
        if (!IsValid ||
            accessSize == 0 ||
            requestedPermissions == IOMMUAccessPermissions.None ||
            ioVirtualAddress > ulong.MaxValue - (accessSize - 1) ||
            (Permissions & requestedPermissions) != requestedPermissions)
        {
            return false;
        }

        ulong inclusiveEnd = ioVirtualAddress + accessSize - 1;
        ulong windowEnd = BaseAddress + Length - 1;
        return ioVirtualAddress >= BaseAddress &&
               inclusiveEnd <= windowEnd;
    }

    public DmaWindowDescriptor WithCompatibilityProjection(bool enabled) =>
        new(Authority, BaseAddress, Length, Permissions, RequiresFence, enabled);
}
