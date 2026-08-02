namespace YAKSys_Hybrid_CPU.Arch
{
    public static partial class OpcodeRegistry
    {
        /// <summary>
        /// Typed C# declaration authority for static opcode metadata. Generated
        /// projections are derived from this table and never feed it back.
        /// </summary>
        public static readonly OpcodeInfo[] Opcodes = BuildOpcodes();

        private static OpcodeInfo[] BuildOpcodes() =>
        [
            .. CreateScalarOpcodes(),
            .. CreateVectorOpcodes(),
            .. CreateMemoryAndControlOpcodes(),
            .. CreateSystemOpcodes(),
        ];
    }
}
