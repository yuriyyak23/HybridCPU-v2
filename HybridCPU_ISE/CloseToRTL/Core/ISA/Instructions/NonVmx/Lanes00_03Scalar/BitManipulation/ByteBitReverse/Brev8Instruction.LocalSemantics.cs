namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.ByteBitReverse;

public sealed partial class Brev8Instruction
{
    public const bool LocalSemanticsAvailable = true;
    public const bool LocalGoldenSeedAvailable = true;
    public const string LocalOpcodeGate = "Opcode 332 is allocated in the scalar extension block.";
    public const string LocalDecoderGate = "Canonical scalar shape is rd, rs1 with rs2=x0 and Immediate=0.";
    public const string LocalInstructionIrGate = "Unary scalar IR projection: Rd, Rs1, per-byte bit-order sideband.";
    public const string LocalMaterializerGate = "ScalarUnaryAlu.ReverseBitsInEachByte is published through the scalar unary register materializer.";
    public const string LocalMicroOpGate = "Scalar per-byte bit reverse publishes a runtime ScalarALUMicroOp.";
    public const string LocalExecuteGate = "Runtime Execute and ScalarAluOps share XLEN=64 per-byte bit reverse semantics.";
    public const string LocalRetireGate = "Retire writes rd and discards x0.";
    public const string LocalReplayGate = "Replay/rollback restores destination architectural register.";
    public const string LocalCompilerGate = "Compiler helper emission remains closed until scalar runtime evidence closes.";
    public const string LocalVmxGate = "No VMX-specific frontend path; generic legality/projection only if runtime evidence later closes.";
    public const string LocalEvidenceSeparationGate = "Scalar BREV8 evidence is separate from vector VBREV8 evidence.";

    public static ulong EvaluateXLen64(ulong value)
    {
        ulong result = 0;

        for (int byteIndex = 0; byteIndex < 8; byteIndex++)
        {
            byte sourceByte = unchecked((byte)(value >> (byteIndex * 8)));
            result |= (ulong)ReverseBitsInByte(sourceByte) << (byteIndex * 8);
        }

        return result;
    }

    public static bool RetireWritesDestination(int destinationRegister) =>
        destinationRegister != 0;

    public static ByteBitReverseGoldenVector[] GetLocalGoldenVectors() =>
    [
        new(0x0000_0000_0000_0000UL, 0x0000_0000_0000_0000UL),
        new(0xFFFF_FFFF_FFFF_FFFFUL, 0xFFFF_FFFF_FFFF_FFFFUL),
        new(0x0123_4567_89AB_CDEFUL, 0x80C4_A2E6_91D5_B3F7UL),
        new(0x8040_2010_0804_0201UL, 0x0102_0408_1020_4080UL),
    ];

    private static byte ReverseBitsInByte(byte value)
    {
        uint reversed = value;
        reversed = ((reversed & 0x55U) << 1) | ((reversed >> 1) & 0x55U);
        reversed = ((reversed & 0x33U) << 2) | ((reversed >> 2) & 0x33U);
        reversed = ((reversed & 0x0FU) << 4) | ((reversed >> 4) & 0x0FU);
        return unchecked((byte)reversed);
    }

    public readonly struct ByteBitReverseGoldenVector
    {
        public ByteBitReverseGoldenVector(ulong value, ulong result)
        {
            Value = value;
            Result = result;
        }

        public ulong Value { get; }

        public ulong Result { get; }
    }
}
