namespace YAKSys_Hybrid_CPU.Core
{
    public readonly record struct VmxExitQualification(
        ushort Leaf,
        VmxInvalidationScope Scope,
        ulong Descriptor)
    {
        public static VmxExitQualification None { get; } =
            new(Leaf: 0, Scope: VmxInvalidationScope.None, Descriptor: 0);

        public ulong Encode() =>
            Leaf |
            ((ulong)Scope << 16) |
            ((Descriptor & 0xFFFF_FFFFUL) << 32);
    }
}
