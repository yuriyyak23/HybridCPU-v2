// Production paths: CPU_Core.Enums opcode/IsaOpcodeValues; InstructionSupportStatus/IsaV4Surface;
// OpcodeInfo.Registry.Data.Scalar; InstructionEncoder.EncodeScalarAddressGenerationImmediate;
// VliwDecoderV4 imm6 + Word1=(rd,rs1,x0) ABI; explicit .UW source-width ABI zero-extends
// low 32 bits of rs1 before shift; InstructionIR/DecodedBundleTransportProjector; InstructionRegistry
// scalar address-generation immediate factory/materializer; InternalOpBuilder/InternalOpKind;
// ScalarALUMicroOp + ScalarAluOps + ExecutionDispatcherV4; retire x0/writeback and replay/rollback;
// Phase 03 golden/conformance tests; compiler no-emission/helper guardrails; generic VMX boundary only.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration;

public sealed partial class SlliUwInstruction
{
    public const string Mnemonic = "SLLI.UW";
    public const string OperandShape = "rd, rs1, imm6";
    public const string ParameterDescriptor = "rd receives zero-extend32(rs1) << imm6 under XLEN=64 scalar ALU rules";
    public const string MicroOpShape = "ScalarImmediateAlu.AddressGenerationShiftLeftUnsignedWordImmediate, retire-owned register writeback, no LSU bypass";
    public const string ExecutionLaneBinding = "Lanes00_03Scalar";
    public const string EvidenceBoundary = "ExecutableScalarAlu";
    public const string SourceWidthPolicy = "Zero-extend low 32 bits of rs1 before shifting by unsigned imm6.";
    public const string ImmediatePolicy = "Immediate shift amount is an unsigned imm6 payload; decoder/encoder reject values outside [0, 63].";
    public const string Operation = "zero_extend_64(low32(rs1)) << imm6";

    public const int XLen = 64;
    public const int SourceWidth = 32;
    public const int ImmediateBits = 6;
    public const int ImmediateMask = 0x3F;

    public const bool RequiresUwSourceWidthAbi = false;
    public const bool RequiresImmediateEncodingAbi = false;
    public const bool RequiresImmediateRangeRejectionTests = false;
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

    public static ushort Opcode => (ushort)Processor.CPU_Core.InstructionsEnum.SLLI_UW;

    public static bool WritesScalarRegister => true;

    public static bool HasSideEffects => false;

    public static ulong Execute(ulong source, ulong immediate6) =>
        unchecked((ulong)(uint)source << (int)(immediate6 & ImmediateMask));

    public static ulong EvaluateXLen64(ulong source, ulong immediate6) =>
        Execute(source, immediate6);

    public static bool RetireWritesDestination(ushort destinationRegister) =>
        destinationRegister != 0;

    public static AddressGenerationImmediateGoldenVector[] GetLocalGoldenVectors() =>
        new AddressGenerationImmediateGoldenVector[]
        {
            new AddressGenerationImmediateGoldenVector(0xFFFF_FFFF_0000_0001UL, 0, 1UL),
            new AddressGenerationImmediateGoldenVector(0xFFFF_FFFF_0000_0001UL, 1, 2UL),
            new AddressGenerationImmediateGoldenVector(0x0000_0000_8000_0000UL, 1, 0x1_0000_0000UL),
            new AddressGenerationImmediateGoldenVector(0xFFFF_FFFF_FFFF_FFFFUL, 32, 0xFFFF_FFFF_0000_0000UL),
            new AddressGenerationImmediateGoldenVector(0xFFFF_FFFF_FFFF_FFFFUL, 63, 0x8000_0000_0000_0000UL),
        };

    public readonly struct AddressGenerationImmediateGoldenVector
    {
        public AddressGenerationImmediateGoldenVector(ulong source, ushort immediate6, ulong expected)
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
