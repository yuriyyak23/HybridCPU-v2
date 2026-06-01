namespace YAKSys_Hybrid_CPU.Arch
{
    public static partial class OpcodeRegistry
    {
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

