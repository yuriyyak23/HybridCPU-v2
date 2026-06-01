namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.BooleanInvert;

public sealed partial class XnorInstruction
{
    public const bool LocalSemanticsAvailable = true;
    public const bool LocalGoldenSeedAvailable = true;
    public const string LocalOpcodeGate = "Closed by production XNOR opcode allocation.";
    public const string LocalDecoderGate = "Closed canonical shape is rd, rs1, rs2 with Immediate=0.";
    public const string LocalInstructionIrGate = "Closed binary scalar IR projection: Rd, Rs1, Rs2, Imm=0.";
    public const string LocalMaterializerGate = "Closed through ScalarALUMicroOp materialization.";
    public const string LocalMicroOpGate = "Closed through scalar binary ALU xnor runtime MicroOp publication.";
    public const string LocalExecuteGate = "Pure XLEN=64 ~(rs1 ^ rs2) semantics are runtime Execute authority for XNOR.";
    public const string LocalRetireGate = "Closed: retire writes rd and discards x0.";
    public const string LocalReplayGate = "Closed: rollback token restores destination architectural register.";
    public const string LocalCompilerGate = "Compiler helper emission and hidden multi-op lowering remain closed.";
    public const string LocalVmxGate = "No VMX-specific frontend path; generic legality/projection only.";

    public static ulong EvaluateXLen64(ulong left, ulong right) =>
        ~(left ^ right);

    public static bool RetireWritesDestination(int destinationRegister) =>
        destinationRegister != 0;

    public static BooleanInvertGoldenVector[] GetLocalGoldenVectors() =>
    [
        new(0x0000_0000_0000_0000UL, 0x0000_0000_0000_0000UL, 0xFFFF_FFFF_FFFF_FFFFUL),
        new(0x0000_0000_0000_0000UL, 0xFFFF_FFFF_FFFF_FFFFUL, 0x0000_0000_0000_0000UL),
        new(0xFFFF_FFFF_FFFF_FFFFUL, 0xFFFF_FFFF_FFFF_FFFFUL, 0xFFFF_FFFF_FFFF_FFFFUL),
        new(0xAAAA_AAAA_5555_5555UL, 0x0F0F_0F0F_F0F0_F0F0UL, 0x5A5A_5A5A_5A5A_5A5AUL),
    ];

    public readonly struct BooleanInvertGoldenVector
    {
        public BooleanInvertGoldenVector(ulong left, ulong right, ulong result)
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
