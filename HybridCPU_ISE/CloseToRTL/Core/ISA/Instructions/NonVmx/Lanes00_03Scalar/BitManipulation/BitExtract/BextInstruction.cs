// Production paths: CPU_Core.Enums opcode/IsaOpcodeValues; InstructionSupportStatus/IsaV4Surface;
// OpcodeInfo.Registry.Data.Scalar; InstructionEncoder.EncodeScalar register-register ABI with Immediate=0;
// VliwDecoderV4 canonical rd,rs1,rs2 rejection of immediate aliases; InstructionIR/DecodedBundleTransportProjector;
// InstructionRegistry scalar bitfield factory/materializer; InternalOpBuilder/InternalOpKind;
// ScalarALUMicroOp + ScalarAluOps + ExecutionDispatcherV4; retire x0/writeback and replay/rollback;
// Phase 02 golden/conformance tests; compiler no-emission/helper guardrails; generic VMX boundary only.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.BitExtract;

public sealed partial class BextInstruction
{
    public const string Mnemonic = "BEXT";
    public const string OperandShape = "rd, rs1, rs2";
    public const string ParameterDescriptor = "DestRegister, SourceRegister, IndexRegister, XLEN=64 bit-extract sideband";
    public const string MicroOpShape = "ScalarBinaryAlu.BitExtractRegisterIndex, retire-owned canonical 0/1 writeback, no flags";
    public const string ExecutionLaneBinding = "Lanes00_03Scalar";
    public const string EvidenceBoundary = "ExecutableScalarAlu";
    public const string IndexPolicy = "Register index is masked to XLEN=64 width with rs2 & 0x3F; result is canonical 0 or 1.";
    public const string Operation = "(rs1 >> (rs2 & 0x3F)) & 1";

    public const int XLen = 64;
    public const int IndexMask = 0x3F;

    public const bool RequiresIndexMaskingAbi = false;
    public const bool RequiresBitfieldResultAbi = false;
    public const bool RequiresCanonicalBooleanResultAbi = false;
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

    public static ushort Opcode => (ushort)Processor.CPU_Core.InstructionsEnum.BEXT;

    public static bool WritesScalarRegister => true;

    public static bool HasSideEffects => false;

    public static ulong Execute(ulong source, ulong index) =>
        (source >> (int)(index & IndexMask)) & 1UL;

    public static ulong EvaluateXLen64(ulong source, ulong index) =>
        Execute(source, index);

    public static bool RetireWritesDestination(ushort destinationRegister) =>
        destinationRegister != 0;

    public static BitfieldGoldenVector[] GetLocalGoldenVectors() =>
        new BitfieldGoldenVector[]
        {
            new BitfieldGoldenVector(0x0000_0000_0000_0001UL, 0, 1UL),
            new BitfieldGoldenVector(0x0000_0000_0000_0001UL, 1, 0UL),
            new BitfieldGoldenVector(0x8000_0000_0000_0000UL, 63, 1UL),
            new BitfieldGoldenVector(0x0000_0000_0000_0010UL, 68, 1UL),
            new BitfieldGoldenVector(0x0000_0000_0000_0000UL, 68, 0UL),
        };

    public readonly struct BitfieldGoldenVector
    {
        public BitfieldGoldenVector(ulong source, ulong index, ulong expected)
        {
            Source = source;
            Index = index;
            Expected = expected;
        }

        public ulong Source { get; }

        public ulong Index { get; }

        public ulong Expected { get; }
    }
}
