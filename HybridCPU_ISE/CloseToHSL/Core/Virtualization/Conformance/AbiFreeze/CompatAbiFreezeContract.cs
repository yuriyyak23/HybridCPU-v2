using System;
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Core;

public enum CompatAbiFreezeViolation : byte
{
    None = 0,
    OpcodeOutsideFrozenSet = 1,
    CsrOutsideFrozenAliasSet = 2,
    VmcsFieldOutsideFrozenAliasSet = 3,
}

public sealed partial class CompatAbiFreezeContract
{
    public const ushort FrozenCoreOpcodeFirst = 250;
    public const ushort FrozenCoreOpcodeLast = 257;
    public const ushort FrozenPointerStoreOpcode = 258;
    public const ushort FrozenCallOpcode = 259;
    public const ushort FrozenExtensionOpcodeFirst = 267;
    public const ushort FrozenExtensionOpcodeLast = 271;

    public static ReadOnlySpan<ushort> FrozenCsrAliases =>
    [
        CsrAddresses.VmxEnable,
        CsrAddresses.VmxCaps,
        CsrAddresses.VmxControl,
        CsrAddresses.VmxExitReason,
        CsrAddresses.VmxExitQual,
    ];

    public bool IsFrozenOpcode(ushort opcode) =>
        (opcode >= FrozenCoreOpcodeFirst && opcode <= FrozenCoreOpcodeLast) ||
        opcode == FrozenPointerStoreOpcode ||
        opcode == FrozenCallOpcode ||
        (opcode >= FrozenExtensionOpcodeFirst && opcode <= FrozenExtensionOpcodeLast);

    public bool IsFrozenCsrAlias(ushort csrAddress)
    {
        foreach (ushort frozen in FrozenCsrAliases)
        {
            if (csrAddress == frozen)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsFrozenVmcsFieldAlias(ushort fieldId) =>
        Enum.IsDefined(typeof(VmcsField), fieldId);

    public CompatAbiFreezeViolation ValidateOpcode(ushort opcode) =>
        IsFrozenOpcode(opcode)
            ? CompatAbiFreezeViolation.None
            : CompatAbiFreezeViolation.OpcodeOutsideFrozenSet;

    public CompatAbiFreezeViolation ValidateCsrAlias(ushort csrAddress) =>
        IsFrozenCsrAlias(csrAddress)
            ? CompatAbiFreezeViolation.None
            : CompatAbiFreezeViolation.CsrOutsideFrozenAliasSet;

    public CompatAbiFreezeViolation ValidateVmcsFieldAlias(ushort fieldId) =>
        IsFrozenVmcsFieldAlias(fieldId)
            ? CompatAbiFreezeViolation.None
            : CompatAbiFreezeViolation.VmcsFieldOutsideFrozenAliasSet;
}
