namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.Rotates;

public sealed partial class RolInstruction
{
    public const string Mnemonic = "ROL";
    public const int XLen = 64;
    public const int ShiftMask = 0x3F;

    public static ushort Opcode => (ushort)Processor.CPU_Core.InstructionsEnum.ROL;

    public static bool WritesScalarRegister => true;

    public static bool HasSideEffects => false;

    public static ulong Execute(ulong source, ulong shiftSource)
    {
        int amount = (int)(shiftSource & ShiftMask);
        return amount == 0
            ? source
            : unchecked((source << amount) | (source >> (XLen - amount)));
    }
}
