using System;

namespace YAKSys_Hybrid_CPU.Core;

public enum VmxOpcodeAliasKind : byte
{
    Core = 0,
    PointerAndCall = 1,
    Extension = 2,
}

public readonly record struct VmxOpcodeAlias(
    ushort Opcode,
    VmxOpcodeAliasKind Kind,
    string Name);

public sealed partial class VmxOpcodeAliasSet
{
    private static readonly VmxOpcodeAlias[] FrozenAliasTable = new VmxOpcodeAlias[]
    {
        new(250, VmxOpcodeAliasKind.Core, "VMXON"),
        new(251, VmxOpcodeAliasKind.Core, "VMXOFF"),
        new(252, VmxOpcodeAliasKind.Core, "VMLAUNCH"),
        new(253, VmxOpcodeAliasKind.Core, "VMRESUME"),
        new(254, VmxOpcodeAliasKind.Core, "VMREAD"),
        new(255, VmxOpcodeAliasKind.Core, "VMWRITE"),
        new(256, VmxOpcodeAliasKind.Core, "VMCLEAR"),
        new(257, VmxOpcodeAliasKind.Core, "VMPTRLD"),
        new(258, VmxOpcodeAliasKind.PointerAndCall, "VMPTRST"),
        new(259, VmxOpcodeAliasKind.PointerAndCall, "VMCALL"),
        new(267, VmxOpcodeAliasKind.Extension, "INVEPT"),
        new(268, VmxOpcodeAliasKind.Extension, "INVVPID"),
        new(269, VmxOpcodeAliasKind.Extension, "VMFUNC"),
        new(270, VmxOpcodeAliasKind.Extension, "VMSAVEX"),
        new(271, VmxOpcodeAliasKind.Extension, "VMRESTX"),
    };

    public static ReadOnlySpan<VmxOpcodeAlias> FrozenAliases => FrozenAliasTable;

    public bool Contains(ushort opcode)
    {
        foreach (VmxOpcodeAlias alias in FrozenAliases)
        {
            if (alias.Opcode == opcode)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetAlias(ushort opcode, out VmxOpcodeAlias alias)
    {
        foreach (VmxOpcodeAlias candidate in FrozenAliases)
        {
            if (candidate.Opcode == opcode)
            {
                alias = candidate;
                return true;
            }
        }

        alias = default;
        return false;
    }
}
