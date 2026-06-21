namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.ScalarMinMax;

public sealed partial class MaxuInstruction
{
    public const bool LocalSemanticsAvailable = true;
    public const bool LocalGoldenSeedAvailable = true;
    public const string LocalOpcodeGate = "Opcode 330 is allocated in the scalar extension block.";
    public const string LocalDecoderGate = "Canonical scalar decoder accepts rd, rs1, rs2 and rejects non-zero immediate aliases.";
    public const string LocalInstructionIrGate = "Binary scalar IR projection is Rd, Rs1, Rs2, Imm=0, no vector payload.";
    public const string LocalMaterializerGate = "Registry publishes ScalarALUMicroOp for MAXU with register-register operands.";
    public const string LocalMicroOpGate = "InternalOpBuilder maps MAXU to typed InternalOpKind.MaxU.";
    public const string LocalExecuteGate = "Runtime Execute and ScalarAluOps share XLEN=64 unsigned max semantics.";
    public const string LocalRetireGate = "Retire writes rd and discards x0 through the scalar ALU retire path.";
    public const string LocalReplayGate = "Replay rollback restores the destination architectural register after writeback.";
    public const string LocalCompilerGate = "Compiler helper emission remains closed; no MAXU helper authority is opened.";
    public const string LocalVmxGate = "No VMX-specific frontend path; generic scalar legality only.";
    public const string LocalEvidenceSeparationGate = "Scalar evidence is separate from vector, AMO, and Lane6 descriptor min/max evidence.";

    public static ulong EvaluateXLen64(ulong left, ulong right) =>
        left >= right ? left : right;

    public static bool RetireWritesDestination(int destinationRegister) =>
        destinationRegister != 0;

    public static ScalarMinMaxGoldenVector[] GetLocalGoldenVectors() =>
    [
        new(0x0000_0000_0000_0000UL, 0x0000_0000_0000_0000UL, 0x0000_0000_0000_0000UL),
        new(0x0000_0000_0000_0001UL, 0x0000_0000_0000_0002UL, 0x0000_0000_0000_0002UL),
        new(0xFFFF_FFFF_FFFF_FFFFUL, 0x0000_0000_0000_0001UL, 0xFFFF_FFFF_FFFF_FFFFUL),
        new(0x8000_0000_0000_0000UL, 0x7FFF_FFFF_FFFF_FFFFUL, 0x8000_0000_0000_0000UL),
    ];

    public readonly struct ScalarMinMaxGoldenVector
    {
        public ScalarMinMaxGoldenVector(ulong left, ulong right, ulong result)
        {
            Left = left;
            Right = right;
            Result = result;
        }

        public ulong Left { get; }

        public ulong Right { get; }

        public ulong Result { get; }
    }
}
