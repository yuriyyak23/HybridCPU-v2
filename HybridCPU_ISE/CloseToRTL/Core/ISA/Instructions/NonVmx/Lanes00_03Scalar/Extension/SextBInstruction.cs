namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.Extension;

public sealed partial class SextBInstruction
{
    public const string Mnemonic = "SEXT.B";
    public const int SourceBits = 8;
    public const int XLen = 64;

    public static ushort Opcode => (ushort)Processor.CPU_Core.InstructionsEnum.SEXT_B;

    public static bool WritesScalarRegister => true;

    public static bool HasSideEffects => false;

    public static ulong Execute(ulong source) =>
        unchecked((ulong)(long)(sbyte)(byte)source);
}
