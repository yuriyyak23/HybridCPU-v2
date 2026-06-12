// REV8 production path:
// - \HybridCPU_ISE\CloseToRTL\Core\ISA\Instructions\NonVmx\Lanes00_03Scalar\BitManipulation\ByteBitReverse\Rev8Instruction.cs
// - \HybridCPU_ISE\CloseToRTL\Core\ISA\Instructions\NonVmx\Lanes00_03Scalar\BitManipulation\ByteBitReverse\Rev8Instruction.LocalSemantics.cs
// - \HybridCPU_ISE\CloseToRTL\Core\Architecture\Common\CPU_Core.Enums.cs
// - \HybridCPU_ISE\NonRTL\Arch\InstructionSupportStatus.cs
// - \HybridCPU_ISE\NonRTL\Arch\IsaV4Surface.cs
// - \HybridCPU_ISE\NonRTL\Arch\OpcodeInfo.Registry.Data.Scalar.cs
// - \HybridCPU_ISE\CloseToRTL\Core\Frontend\Decode\VliwDecoderV4Bridge\VliwDecoderV4.cs
// - \HybridCPU_ISE\NonRTL\Core\Diagnostics\InstructionRegistry.Helpers.Core.cs
// - \HybridCPU_ISE\NonRTL\Core\Diagnostics\InstructionRegistry.Initialize.Scalar.cs
// - \HybridCPU_ISE\NonRTL\Core\Pipeline\InternalOpBuilder.cs
// - \HybridCPU_ISE\CloseToRTL\Core\Pipeline\MicroOps\Internal\InternalOp.cs
// - \HybridCPU_ISE\CloseToRTL\Core\Execution\Scalar\ALU\ScalarAluOps.cs
// - \HybridCPU_ISE\CloseToRTL\Core\Execution\Dispatch\ExecutionDispatcherV4.Scalar.cs
// - \HybridCPU_ISE.Tests\tests\NonVmxPhase01ByteBitReverseExecutableTests.cs
// REV8 production metadata anchor: scalar byte-order reverse is opened as a
// unary XLEN=64 scalar ALU row. VMX integration remains generic because this
// row has no virtualization boundary; compiler helper emission remains closed.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.ByteBitReverse;

public sealed partial class Rev8Instruction
{
    public const string Mnemonic = "REV8";
    public const string OperandShape = "rd, rs1";
    public const string ParameterDescriptor = "DestRegister, SourceRegister, XLEN=64 byte-order sideband";
    public const string MicroOpShape = "ScalarUnaryAlu.ReverseBytes, retire-owned register writeback, no flags";
    public const string ExecutionLaneBinding = "Lanes00_03Scalar";
    public const string EvidenceBoundary = "ExecutableScalarAlu";
    public const string Operation = "Reverse byte order within XLEN=64.";

    public const int XLen = 64;

    public const bool RequiresByteOrderAbi = false;
    public const bool RequiresBitOrderingAbi = false;
    public const bool SeparateFromVectorBrev8 = true;
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

    public static ushort Opcode => (ushort)YAKSys_Hybrid_CPU.Processor.CPU_Core.InstructionsEnum.REV8;
    public static bool WritesScalarRegister => true;
    public static bool HasSideEffects => false;

    public static ulong Execute(ulong value) =>
        EvaluateXLen64(value);
}
