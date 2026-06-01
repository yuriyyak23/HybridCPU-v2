// Production paths: CPU_Core.Enums opcode/IsaOpcodeValues; InstructionSupportStatus/IsaV4Surface;
// OpcodeInfo.Registry.Data.Scalar; canonical scalar register ABI Word1=(rd,rs1,rs2), Immediate=0;
// VliwDecoderV4/InstructionIR/DecodedBundleTransportProjector; InstructionRegistry scalar address-generation
// factory/materializer; InternalOpBuilder/InternalOpKind; ScalarALUMicroOp + ScalarAluOps +
// ExecutionDispatcherV4; retire x0/writeback and replay/rollback; Phase 03 golden/conformance tests;
// compiler no-emission/helper guardrails; generic VMX boundary only.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration;

public sealed partial class Sh2addInstruction
{
    public const string Mnemonic = "SH2ADD";
    public const string OperandShape = "rd, rs1, rs2";
    public const string ParameterDescriptor = "rd receives rs2 + (rs1 << 2) under XLEN=64 scalar ALU rules";
    public const string MicroOpShape = "ScalarBinaryAlu.AddressGenerationShiftLeftTwoAdd, retire-owned register writeback, no LSU bypass";
    public const string ExecutionLaneBinding = "Lanes00_03Scalar";
    public const string EvidenceBoundary = "ExecutableScalarAlu";
    public const string Operation = "(rs1 << 2) + rs2";

    public const int XLen = 64;

    public const bool NoLsuBypassAuthority = true;
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

    public static ushort Opcode => (ushort)Processor.CPU_Core.InstructionsEnum.SH2ADD;

    public static bool WritesScalarRegister => true;

    public static bool HasSideEffects => false;

    public static ulong Execute(ulong source, ulong addend) =>
        unchecked((source << 2) + addend);

    public static ulong EvaluateXLen64(ulong source, ulong addend) =>
        Execute(source, addend);

    public static bool RetireWritesDestination(ushort destinationRegister) =>
        destinationRegister != 0;

    public static AddressGenerationGoldenVector[] GetLocalGoldenVectors() =>
        new AddressGenerationGoldenVector[]
        {
            new AddressGenerationGoldenVector(0UL, 0UL, 0UL),
            new AddressGenerationGoldenVector(1UL, 2UL, 6UL),
            new AddressGenerationGoldenVector(0x4000_0000_0000_0000UL, 5UL, 5UL),
            new AddressGenerationGoldenVector(ulong.MaxValue, 3UL, ulong.MaxValue),
        };

    public readonly struct AddressGenerationGoldenVector
    {
        public AddressGenerationGoldenVector(ulong source, ulong addend, ulong expected)
        {
            Source = source;
            Addend = addend;
            Expected = expected;
        }

        public ulong Source { get; }

        public ulong Addend { get; }

        public ulong Expected { get; }
    }
}
