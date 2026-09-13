namespace YAKSys_Hybrid_CPU.Core
{
    public readonly record struct VmxRootDescriptorReference(
        ulong Address,
        bool IsLegacyDefault)
    {
        public bool IsExplicit => !IsLegacyDefault && Address != 0;

        public bool IsValid => IsLegacyDefault || Address != 0;

        public static VmxRootDescriptorReference CompatibilityDefault { get; } =
            new(Address: 0, IsLegacyDefault: true);

        public static VmxRootDescriptorReference LegacyDefault { get; } =
            CompatibilityDefault;

        public static VmxRootDescriptorReference FromOperand(ulong address) =>
            address == 0
                ? CompatibilityDefault
                : new VmxRootDescriptorReference(address, IsLegacyDefault: false);
    }
}
