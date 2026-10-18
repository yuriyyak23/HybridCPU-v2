using System;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace YAKSys_Hybrid_CPU.Core.Vmx.Compatibility;

public sealed class VmcsCompatMapper
{
    private readonly VmcsV2BlockDirectory _directory;

    public VmcsCompatMapper(VmcsV2BlockDirectory directory)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
    }

    public bool IsPrimaryLayoutOwner => false;

    public bool TryMap(VmcsField legacyField, out VmcsV2FieldDescriptor descriptor)
    {
        if (!Enum.IsDefined(typeof(VmcsField), legacyField))
        {
            descriptor = default;
            return false;
        }

        return _directory.TryGetLegacyAlias(legacyField, out descriptor);
    }

    public VmcsV2FieldDescriptor MapOrThrow(VmcsField legacyField)
    {
        if (TryMap(legacyField, out VmcsV2FieldDescriptor descriptor))
        {
            return descriptor;
        }

        throw new InvalidOperationException(
            $"VMCS field 0x{(ushort)legacyField:X4} is not a frozen VMX8 compatibility alias.");
    }
}
