// Production paths: CPU_Core.Enums opcode/IsaOpcodeValues; InstructionSupportStatus/IsaV4Surface;
// OpcodeInfo.Registry.Data.Scalar; InstructionEncoder.EncodeScalarRotateImmediate;
// VliwDecoderV4 imm6 + Word1=(rd,rs1,x0) ABI; InstructionIR/DecodedBundleTransportProjector;
// InstructionRegistry scalar rotate-immediate factory/materializer; InternalOpBuilder/InternalOpKind;
// ScalarALUMicroOp + ScalarAluOps + ExecutionDispatcherV4; retire x0/writeback and replay/rollback;
// Phase 02 golden/conformance tests; compiler no-emission/helper guardrails; generic VMX boundary only.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.Rotates;

public sealed partial class RoliInstruction
{
    public const string Mnemonic = "ROLI";
    public const string OperandShape = "rd, rs1, imm6";
    public const string ParameterDescriptor = "DestRegister, SourceRegister, Immediate6, XLEN=64 rotate count sideband";
    public const string MicroOpShape = "ScalarImmediateAlu.RotateLeft, retire-owned register writeback, no flags";
    public const string ExecutionLaneBinding = "Lanes00_03Scalar";
    public const string EvidenceBoundary = "ExecutableScalarAlu";
    public const string CompatibilityBoundary = "Register-register ROL remains separate; rotate-immediate uses canonical imm6 ABI.";

    public const int XLen = 64;
    public const int ImmediateBits = 6;
    public const int ImmediateMask = 0x3F;

    public const bool RequiresRotateImmediateEncodingAbi = false;
    public const bool RequiresImmediateRangeRejectionTests = false;
    public const bool SeparateFromClosedRegisterRotate = true;
    public const bool NoRegisterRotateEvidenceReuse = true;
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

    public static ushort Opcode => (ushort)Processor.CPU_Core.InstructionsEnum.ROLI;

    public static bool WritesScalarRegister => true;

    public static bool HasSideEffects => false;

    public static ulong Execute(ulong source, ulong immediate6)
    {
        int amount = (int)(immediate6 & ImmediateMask);
        return amount == 0
            ? source
            : unchecked((source << amount) | (source >> (XLen - amount)));
    }

    public static ulong EvaluateXLen64(ulong source, ulong immediate6) =>
        Execute(source, immediate6);

    public static bool RetireWritesDestination(ushort destinationRegister) =>
        destinationRegister != 0;

    public static RotateImmediateGoldenVector[] GetLocalGoldenVectors() =>
        new RotateImmediateGoldenVector[]
        {
            new RotateImmediateGoldenVector(0x0000_0000_0000_0001UL, 0, 0x0000_0000_0000_0001UL),
            new RotateImmediateGoldenVector(0x0000_0000_0000_0001UL, 1, 0x0000_0000_0000_0002UL),
            new RotateImmediateGoldenVector(0x8000_0000_0000_0000UL, 1, 0x0000_0000_0000_0001UL),
            new RotateImmediateGoldenVector(0x0123_4567_89AB_CDEFUL, 4, 0x1234_5678_9ABC_DEF0UL),
            new RotateImmediateGoldenVector(0x0000_0000_0000_0001UL, 63, 0x8000_0000_0000_0000UL),
        };

    public readonly struct RotateImmediateGoldenVector
    {
        public RotateImmediateGoldenVector(ulong source, ushort immediate6, ulong expected)
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
