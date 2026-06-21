namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.ByteBitReverse;

public sealed partial class Rev8Instruction
{
    public const bool LocalSemanticsAvailable = true;
    public const bool LocalGoldenSeedAvailable = true;
    public const string LocalOpcodeGate = "Opcode 331 is allocated in the scalar extension block.";
    public const string LocalDecoderGate = "Canonical scalar shape is rd, rs1 with rs2=x0 and Immediate=0.";
    public const string LocalInstructionIrGate = "Unary scalar IR projection: Rd, Rs1, XLEN=64 byte-order sideband.";
    public const string LocalMaterializerGate = "ScalarUnaryAlu.ReverseBytes is published through the scalar unary register materializer.";
    public const string LocalMicroOpGate = "Scalar byte-order reverse publishes a runtime ScalarALUMicroOp.";
    public const string LocalExecuteGate = "Runtime Execute and ScalarAluOps share XLEN=64 byte-order reverse semantics.";
    public const string LocalRetireGate = "Retire writes rd and discards x0.";
    public const string LocalReplayGate = "Replay/rollback restores destination architectural register.";
    public const string LocalCompilerGate = "Compiler helper emission remains closed until scalar runtime evidence closes.";
    public const string LocalVmxGate = "No VMX-specific frontend path; generic legality/projection only if runtime evidence later closes.";
    public const string LocalEvidenceSeparationGate = "Scalar REV8 evidence is separate from vector byte/bit reverse evidence.";

    public static ulong EvaluateXLen64(ulong value) =>
        ((value & 0x0000_0000_0000_00FFUL) << 56)
        | ((value & 0x0000_0000_0000_FF00UL) << 40)
        | ((value & 0x0000_0000_00FF_0000UL) << 24)
        | ((value & 0x0000_0000_FF00_0000UL) << 8)
        | ((value & 0x0000_00FF_0000_0000UL) >> 8)
        | ((value & 0x0000_FF00_0000_0000UL) >> 24)
        | ((value & 0x00FF_0000_0000_0000UL) >> 40)
        | ((value & 0xFF00_0000_0000_0000UL) >> 56);

    public static bool RetireWritesDestination(int destinationRegister) =>
        destinationRegister != 0;

    public static ByteBitReverseGoldenVector[] GetLocalGoldenVectors() =>
    [
        new(0x0000_0000_0000_0000UL, 0x0000_0000_0000_0000UL),
        new(0xFFFF_FFFF_FFFF_FFFFUL, 0xFFFF_FFFF_FFFF_FFFFUL),
        new(0x0123_4567_89AB_CDEFUL, 0xEFCD_AB89_6745_2301UL),
        new(0x8000_0000_0000_0001UL, 0x0100_0000_0000_0080UL),
    ];

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
