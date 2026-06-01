namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.Extension;

public sealed partial class ZextHInstruction
{
    public const string Mnemonic = "ZEXT.H";
    public const int SourceBits = 16;
    public const int XLen = 64;

    public static ushort Opcode => (ushort)Processor.CPU_Core.InstructionsEnum.ZEXT_H;

    public static bool WritesScalarRegister => true;

    public static bool HasSideEffects => false;

    public static ulong Execute(ulong source) =>
        unchecked((ushort)source);
}
