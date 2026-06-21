using System.Numerics;

namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.BitCount;

public sealed partial class PopcntInstruction
{
    public const bool LocalSemanticsAvailable = true;
    public const bool LocalGoldenSeedAvailable = true;
    public const string LocalOpcodeGate = "POPCNT is not allocated as a runtime opcode; CPOP opcode 334 is canonical.";
    public const string LocalDecoderGate = "No POPCNT decoder row is published; parser-only alias acceptance would require a future explicit policy.";
    public const string LocalInstructionIrGate = "No POPCNT IR projection is published; facade alias policy is not runtime evidence.";
    public const string LocalMaterializerGate = "No POPCNT registry row is published; CPOP owns the scalar popcount materializer.";
    public const string LocalMicroOpGate = "No POPCNT runtime MicroOp is published.";
    public const string LocalExecuteGate = "Pure XLEN=64 popcount semantics remain local alias documentation, not runtime Execute authority.";
    public const string LocalRetireGate = "POPCNT publishes no retire path; CPOP owns canonical scalar writeback.";
    public const string LocalReplayGate = "POPCNT publishes no replay path; CPOP owns canonical rollback evidence.";
    public const string LocalCompilerGate = "Compiler helper emission and hidden POPCNT lowering remain closed.";
    public const string LocalVmxGate = "No VMX-specific frontend path; generic projection only if a future alias policy closes.";

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
