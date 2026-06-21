namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.ConditionalSelect;

public sealed partial class CselInstruction
{
    public const bool LocalSemanticsAvailable = true;
    public const bool LocalGoldenSeedAvailable = true;
    public const string LocalCarrierGate = "Phase 01E closes the external CSEL carrier gate negatively; no four-register carrier is approved in Phase 01.";
    public const string LocalOpcodeGate = "Phase 01E carrier gate is closed negative; no CSEL opcode is allocated here.";
    public const string LocalDecoderGate = "Canonical local shape is rd, rs_true, rs_false, rs_cond only in a future production package with an approved four-source carrier ABI.";
    public const string LocalInstructionIrGate = "Current packed scalar IR carries only rd/rs1/rs2; no four-register CSEL IR row is published here.";
    public const string LocalMaterializerGate = "ScalarSelect materializer contract only; Phase 01 publishes no registry row.";
    public const string LocalMicroOpGate = "Scalar conditional select contract only; no runtime MicroOp is published here.";
    public const string LocalExecuteGate = "Pure XLEN=64 condition-nonzero select semantics available as EvaluateXLen64; not runtime Execute authority.";
    public const string LocalRetireGate = "Future retire writes rd and discards x0; no retire engine path is modified here.";
    public const string LocalReplayGate = "Future replay restores destination architectural register; no token path is modified here.";
    public const string LocalCompilerGate = "Compiler helper emission and hidden lowering remain closed.";
    public const string LocalVmxGate = "No VMX-specific frontend path; generic legality/projection only if runtime evidence later closes.";
    public const string LocalPredicateStateGate = "No hidden predicate register state is introduced by this local contract.";

    public static ulong EvaluateXLen64(ulong trueValue, ulong falseValue, ulong condition) =>
        condition != 0 ? trueValue : falseValue;

    public static bool RetireWritesDestination(int destinationRegister) =>
        destinationRegister != 0;

    public static ScalarSelectGoldenVector[] GetLocalGoldenVectors() =>
    [
        new(0x1111_1111_1111_1111UL, 0x2222_2222_2222_2222UL, 0x0000_0000_0000_0000UL, 0x2222_2222_2222_2222UL),
        new(0x1111_1111_1111_1111UL, 0x2222_2222_2222_2222UL, 0x0000_0000_0000_0001UL, 0x1111_1111_1111_1111UL),
        new(0x0000_0000_0000_0000UL, 0xFFFF_FFFF_FFFF_FFFFUL, 0x8000_0000_0000_0000UL, 0x0000_0000_0000_0000UL),
        new(0xFFFF_FFFF_FFFF_FFFFUL, 0x0000_0000_0000_0000UL, 0xFFFF_FFFF_FFFF_FFFFUL, 0xFFFF_FFFF_FFFF_FFFFUL),
    ];

    public readonly struct ScalarSelectGoldenVector
    {
        public ScalarSelectGoldenVector(ulong trueValue, ulong falseValue, ulong condition, ulong result)
        {
            TrueValue = trueValue;
            FalseValue = falseValue;
            Condition = condition;
            Result = result;
        }

        public ulong TrueValue { get; }

        public ulong FalseValue { get; }

        public ulong Condition { get; }

        public ulong Result { get; }
    }
}
