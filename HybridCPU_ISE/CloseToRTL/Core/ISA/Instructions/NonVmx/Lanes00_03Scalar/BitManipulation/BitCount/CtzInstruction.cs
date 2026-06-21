namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.BitCount;

public sealed partial class CtzInstruction
{
    public const string Mnemonic = "CTZ";
    public const int XLen = 64;

    public static ushort Opcode => (ushort)Processor.CPU_Core.InstructionsEnum.CTZ;

    public static bool WritesScalarRegister => true;

    public static bool HasSideEffects => false;

    public static ulong Execute(ulong source) =>
        source == 0
            ? 64UL
            : (ulong)System.Numerics.BitOperations.TrailingZeroCount(source);
}
