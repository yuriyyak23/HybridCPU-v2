// Production paths: CPU_Core.Enums opcode/IsaOpcodeValues; InstructionSupportStatus/IsaV4Surface;
// OpcodeInfo.Registry.Data.Scalar; canonical scalar register ABI Word1=(rd,rs1,rs2), Immediate=0;
// explicit .UW source-width ABI zero-extends low 32 bits of rs1 before shift/add; InstructionIR/projector;
// InstructionRegistry scalar address-generation factory/materializer; InternalOpBuilder/InternalOpKind;
// ScalarALUMicroOp + ScalarAluOps + ExecutionDispatcherV4; retire x0/writeback and replay/rollback;
// Phase 03 golden/conformance tests; compiler no-emission/helper guardrails; generic VMX boundary only.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration;

public sealed partial class Sh1addUwInstruction
{
    public const string Mnemonic = "SH1ADD.UW";
    public const string OperandShape = "rd, rs1, rs2";
    public const string ParameterDescriptor = "rd receives rs2 + (zero-extend32(rs1) << 1) under XLEN=64 scalar ALU rules";
    public const string MicroOpShape = "ScalarBinaryAlu.AddressGenerationShiftLeftOneAddUnsignedWord, retire-owned register writeback, no LSU bypass";
    public const string ExecutionLaneBinding = "Lanes00_03Scalar";
    public const string EvidenceBoundary = "ExecutableScalarAlu";
    public const string SourceWidthPolicy = "Zero-extend low 32 bits of rs1 before shifting; rs2 remains full XLEN=64.";
    public const string Operation = "(zero_extend_64(low32(rs1)) << 1) + rs2";

    public const int XLen = 64;
    public const int SourceWidth = 32;

    public const bool RequiresUwSourceWidthAbi = false;
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

    public static ushort Opcode => (ushort)Processor.CPU_Core.InstructionsEnum.SH1ADD_UW;

    public static bool WritesScalarRegister => true;

    public static bool HasSideEffects => false;

    public static ulong Execute(ulong source, ulong addend) =>
        unchecked(((ulong)(uint)source << 1) + addend);

    public static ulong EvaluateXLen64(ulong source, ulong addend) =>
        Execute(source, addend);

    public static bool RetireWritesDestination(ushort destinationRegister) =>
        destinationRegister != 0;

    public static AddressGenerationGoldenVector[] GetLocalGoldenVectors() =>
        new AddressGenerationGoldenVector[]
        {
            new AddressGenerationGoldenVector(0xFFFF_FFFF_0000_0001UL, 2UL, 4UL),
            new AddressGenerationGoldenVector(0xFFFF_FFFF_FFFF_FFFFUL, 1UL, 0x1_FFFF_FFFFUL),
            new AddressGenerationGoldenVector(0x0000_0000_8000_0000UL, 0UL, 0x1_0000_0000UL),
            new AddressGenerationGoldenVector(0x0000_0000_FFFF_FFFFUL, ulong.MaxValue, 0x1_FFFF_FFFDUL),
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
