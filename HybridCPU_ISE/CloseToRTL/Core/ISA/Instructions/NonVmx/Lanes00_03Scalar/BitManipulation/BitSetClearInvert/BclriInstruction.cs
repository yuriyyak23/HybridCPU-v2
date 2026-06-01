// Production paths: CPU_Core.Enums opcode/IsaOpcodeValues; InstructionSupportStatus/IsaV4Surface;
// OpcodeInfo.Registry.Data.Scalar; InstructionEncoder.EncodeScalarBitfieldImmediate;
// VliwDecoderV4 imm6 + Word1=(rd,rs1,x0) ABI; InstructionIR/DecodedBundleTransportProjector;
// InstructionRegistry scalar bitfield-immediate factory/materializer; InternalOpBuilder/InternalOpKind;
// ScalarALUMicroOp + ScalarAluOps + ExecutionDispatcherV4; retire x0/writeback and replay/rollback;
// Phase 02 golden/conformance tests; compiler no-emission/helper guardrails; generic VMX boundary only.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.BitSetClearInvert;

public sealed partial class BclriInstruction
{
    public const string Mnemonic = "BCLRI";
    public const string OperandShape = "rd, rs1, imm6";
    public const string ParameterDescriptor = "DestRegister, SourceRegister, Immediate6, XLEN=64 bitfield sideband";
    public const string MicroOpShape = "ScalarImmediateAlu.BitClearImmediateIndex, retire-owned register writeback, no flags";
    public const string ExecutionLaneBinding = "Lanes00_03Scalar";
    public const string EvidenceBoundary = "ExecutableScalarAlu";
    public const string IndexPolicy = "Immediate index is an unsigned imm6 payload; decoder/encoder reject values outside [0, 63].";
    public const string Operation = "rs1 & ~(1UL << imm6)";

    public const int XLen = 64;
    public const int ImmediateBits = 6;
    public const int ImmediateMask = 0x3F;

    public const bool RequiresImmediateEncodingAbi = false;
    public const bool RequiresImmediateRangeRejectionTests = false;
    public const bool RequiresIndexMaskingAbi = false;
    public const bool RequiresBitfieldResultAbi = false;
    public const bool NoHiddenMultiOpEmission = true;
    public const bool RequiresDecoderEncoderAbi = false;
    public const bool RequiresInstructionIrProjection = false;
    public const bool RequiresRegistryMaterializer = false;
    public const bool RequiresRetireRegisterWriteback = false;
    public const bool RequiresReplayRollbackEvidence = false;
    public const bool NoVmxFrontendIntegrationRequired = true;
    public const bool RequiresVmxProjection = false;
    public const bool HasOpcodeAllocation = true;
    public const bool IsExecutable = true;
    public const bool CompilerHelperAllowed = false;

    public static ushort Opcode => (ushort)Processor.CPU_Core.InstructionsEnum.BCLRI;

    public static bool WritesScalarRegister => true;

    public static bool HasSideEffects => false;

    public static ulong Execute(ulong source, ulong immediate6) =>
        source & ~(1UL << (int)(immediate6 & ImmediateMask));

    public static ulong EvaluateXLen64(ulong source, ulong immediate6) =>
        Execute(source, immediate6);

    public static bool RetireWritesDestination(ushort destinationRegister) =>
        destinationRegister != 0;

    public static BitfieldImmediateGoldenVector[] GetLocalGoldenVectors() =>
        new BitfieldImmediateGoldenVector[]
        {
            new BitfieldImmediateGoldenVector(0xFFFF_FFFF_FFFF_FFFFUL, 0, 0xFFFF_FFFF_FFFF_FFFEUL),
            new BitfieldImmediateGoldenVector(0xFFFF_FFFF_FFFF_FFFFUL, 63, 0x7FFF_FFFF_FFFF_FFFFUL),
            new BitfieldImmediateGoldenVector(0x0000_0000_0000_0010UL, 4, 0x0000_0000_0000_0000UL),
            new BitfieldImmediateGoldenVector(0x0123_4567_89AB_CDFFUL, 4, 0x0123_4567_89AB_CDEFUL),
        };

    public readonly struct BitfieldImmediateGoldenVector
    {
        public BitfieldImmediateGoldenVector(ulong source, ushort immediate6, ulong expected)
        {
            Source = source;
            Immediate6 = immediate6;
            Expected = expected;
        }

        public ulong Source { get; }

        public ushort Immediate6 { get; }

        public ulong Expected { get; }
    }
}
