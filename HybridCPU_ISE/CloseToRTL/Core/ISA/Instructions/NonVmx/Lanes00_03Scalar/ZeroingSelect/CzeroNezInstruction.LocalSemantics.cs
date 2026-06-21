namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.ZeroingSelect;

public sealed partial class CzeroNezInstruction
{
    public const bool LocalSemanticsAvailable = true;
    public const bool LocalGoldenSeedAvailable = true;
    public const string LocalOpcodeGate = "CZERO.NEZ opcode 333 is allocated in the Phase 01E scalar zeroing-select package.";
    public const string LocalDecoderGate = "Canonical runtime shape is rd, rs1, rs2 with Immediate=0.";
    public const string LocalInstructionIrGate = "Binary scalar IR projection carries rs1=value and rs2=condition; no hidden predicate state.";
    public const string LocalMaterializerGate = "Registry publishes ScalarALUMicroOp for CZERO.NEZ with register-register operands.";
    public const string LocalMicroOpGate = "InternalOpBuilder maps CZERO.NEZ to typed InternalOpKind.CzeroNez.";
    public const string LocalExecuteGate = "Pure XLEN=64 nonzero-condition zeroing semantics are runtime Execute authority.";
    public const string LocalRetireGate = "Retire writes rd and discards x0; no side effects are published.";
    public const string LocalReplayGate = "Replay restores the destination architectural register through the scalar rollback token.";
    public const string LocalCompilerGate = "Compiler helper emission and hidden lowering remain closed.";
    public const string LocalVmxGate = "No VMX-specific frontend path; generic legality/runtime projection only.";
    public const string LocalPolarityGate = "CZERO.NEZ polarity is closed as condition != 0 produces zero; closed CZERO.EQZ evidence is not reused.";

    public static ulong EvaluateXLen64(ulong value, ulong condition) =>
        condition != 0 ? 0UL : value;

    public static bool RetireWritesDestination(int destinationRegister) =>
        destinationRegister != 0;

    public static ZeroingSelectGoldenVector[] GetLocalGoldenVectors() =>
    [
        new(0x1111_1111_1111_1111UL, 0x0000_0000_0000_0000UL, 0x1111_1111_1111_1111UL),
        new(0x1111_1111_1111_1111UL, 0x0000_0000_0000_0001UL, 0x0000_0000_0000_0000UL),
        new(0xFFFF_FFFF_FFFF_FFFFUL, 0x8000_0000_0000_0000UL, 0x0000_0000_0000_0000UL),
        new(0x0000_0000_0000_0000UL, 0xFFFF_FFFF_FFFF_FFFFUL, 0x0000_0000_0000_0000UL),
    ];

    public readonly struct ZeroingSelectGoldenVector
    {
        public ZeroingSelectGoldenVector(ulong value, ulong condition, ulong result)
        {
            Value = value;
            Condition = condition;
            Result = result;
        }

        public ulong Value { get; }

        public ulong Condition { get; }

        public ulong Result { get; }
    }
}
