using YAKSys_Hybrid_CPU.Arch.Generated;

namespace YAKSys_Hybrid_CPU.Arch
{
    public static partial class OpcodeRegistry
    {
        /// <summary>
        /// Runtime compatibility facade for manifest-derived static opcode metadata.
        /// The independent copy preserves the public mutable-array boundary.
        /// </summary>
        public static readonly OpcodeInfo[] Opcodes = [.. GeneratedIsaCatalog.Opcodes];
    }
}
