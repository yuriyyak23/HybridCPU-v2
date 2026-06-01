using System.Numerics;

namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.BitCount;

public sealed partial class CpopInstruction
{
    public const bool LocalSemanticsAvailable = true;
    public const bool LocalGoldenSeedAvailable = true;
    public const string LocalOpcodeGate = "CPOP opcode 334 is allocated as the canonical Phase 01A population-count runtime row.";
    public const string LocalDecoderGate = "Canonical runtime shape is rd, rs1, rs2=x0 with Immediate=0.";
    public const string LocalInstructionIrGate = "Unary scalar IR projection carries Rd and Rs1; no POPCNT alias IR row is published.";
    public const string LocalMaterializerGate = "Registry publishes ScalarALUMicroOp for CPOP with register-unary operands.";
    public const string LocalMicroOpGate = "InternalOpBuilder maps CPOP to typed InternalOpKind.Cpop.";
    public const string LocalExecuteGate = "Pure XLEN=64 popcount semantics are runtime Execute authority.";
    public const string LocalRetireGate = "Retire writes rd and discards x0; no side effects are published.";
    public const string LocalReplayGate = "Replay restores the destination architectural register through the scalar rollback token.";
    public const string LocalCompilerGate = "Compiler helper emission remains closed; POPCNT stays a no-emission alias boundary.";
    public const string LocalVmxGate = "No VMX-specific frontend path; generic legality/runtime projection only.";

    public static ulong EvaluateXLen64(ulong source) =>
        (ulong)BitOperations.PopCount(source);

    public static bool RetireWritesDestination(int destinationRegister) =>
        destinationRegister != 0;

    public static PopulationCountGoldenVector[] GetLocalGoldenVectors() =>
    [
        new(0x0000_0000_0000_0000UL, 0UL),
        new(0x0000_0000_0000_0001UL, 1UL),
        new(0x8000_0000_0000_0000UL, 1UL),
        new(0x0123_4567_89AB_CDEFUL, 32UL),
        new(0xFFFF_FFFF_FFFF_FFFFUL, 64UL),
    ];

    public readonly struct PopulationCountGoldenVector
    {
        public PopulationCountGoldenVector(ulong source, ulong result)
        {
            Source = source;
            Result = result;
        }

        public ulong Source { get; }

        public ulong Result { get; }
    }
}
