using System;
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Core;

public enum VmxCsrAliasKind : byte
{
    Enable = 0,
    CapabilityProjection = 1,
    Control = 2,
    CompletionProjection = 3,
}

public readonly record struct VmxCsrAlias(
    ushort Address,
    VmxCsrAliasKind Kind,
    string Name,
    bool IsReadOnlyProjection);

public sealed partial class VmxCsrAliasSet
{
    private static readonly VmxCsrAlias[] FrozenAliasTable = new VmxCsrAlias[]
    {
        new(CsrAddresses.VmxEnable, VmxCsrAliasKind.Enable, "VmxEnable", false),
        new(CsrAddresses.VmxCaps, VmxCsrAliasKind.CapabilityProjection, "VmxCaps", true),
        new(CsrAddresses.VmxControl, VmxCsrAliasKind.Control, "VmxControl", false),
        new(CsrAddresses.VmxExitReason, VmxCsrAliasKind.CompletionProjection, "VmxExitReason", false),
        new(CsrAddresses.VmxExitQual, VmxCsrAliasKind.CompletionProjection, "VmxExitQual", false),
    };

    public static ReadOnlySpan<VmxCsrAlias> FrozenAliases => FrozenAliasTable;

    public bool Contains(ushort address)
    {
        foreach (var alias in FrozenAliases)
        {
            if (alias.Address == address)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetAlias(ushort address, out VmxCsrAlias alias)
    {
        foreach (var candidate in FrozenAliases)
        {
            if (candidate.Address == address)
            {
                alias = candidate;
                return true;
            }
        }

        alias = default;
        return false;
    }

    public bool IsReadOnlyProjection(ushort address)
    {
        return TryGetAlias(address, out var alias) && alias.IsReadOnlyProjection;
    }
}
