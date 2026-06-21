namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.FacadeCandidates.ZeroCompare;

public sealed partial class SeqzInstruction
{
    public const bool LocalNoEmissionContractAvailable = true;
    public const bool LocalHardwareCandidateGoldenSeedAvailable = true;
    public const string LocalOpcodeGate = "Phase 01F selects facade-only/no-emission; no SEQZ opcode is allocated.";
    public const string LocalDecoderGate = "No SEQZ decoder row is published; hardware form would need a future full scalar path.";
    public const string LocalInstructionIrGate = "No SEQZ IR projection is published; facade policy is not runtime evidence.";
    public const string LocalMaterializerGate = "No SEQZ materializer row is published.";
    public const string LocalMicroOpGate = "No SEQZ runtime MicroOp is published.";
    public const string LocalExecuteGate = "Canonical 0/1 hardware-candidate result is available as EvaluateHardwareCandidateXLen64; not runtime Execute authority.";
    public const string LocalRetireGate = "Future hardware retire would write rd and discard x0; no retire engine path is modified here.";
    public const string LocalReplayGate = "Future hardware replay would restore destination architectural register; no token path is modified here.";
    public const string LocalCompilerGate = "Compiler helper emission and hidden lowering remain closed.";
    public const string LocalVmxGate = "No VMX-specific frontend path; generic legality/projection only if runtime evidence later closes.";
    public const string LocalFacadeEvidenceGate = "Facade-only/no-emission decision is closed; hidden lowering remains forbidden.";

    public static ulong EvaluateHardwareCandidateXLen64(ulong value) =>
        value == 0 ? 1UL : 0UL;

    public static bool WouldRetireWriteDestinationIfHardwareSelected(int destinationRegister) =>
        destinationRegister != 0;

    public static ZeroCompareFacadeGoldenVector[] GetLocalHardwareCandidateGoldenVectors() =>
    [
        new(0x0000_0000_0000_0000UL, 0x0000_0000_0000_0001UL),
        new(0x0000_0000_0000_0001UL, 0x0000_0000_0000_0000UL),
        new(0xFFFF_FFFF_FFFF_FFFFUL, 0x0000_0000_0000_0000UL),
        new(0x8000_0000_0000_0000UL, 0x0000_0000_0000_0000UL),
    ];

    public readonly struct ZeroCompareFacadeGoldenVector
    {
        public ZeroCompareFacadeGoldenVector(ulong value, ulong result)
        {
            Value = value;
            Result = result;
        }

        public ulong Value { get; }

        public ulong Result { get; }
    }
}
