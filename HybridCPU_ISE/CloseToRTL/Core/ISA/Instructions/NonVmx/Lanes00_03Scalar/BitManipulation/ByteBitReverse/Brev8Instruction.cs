// BREV8 production path:
// - \HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\ISA\Instructions\NonVmx\Lanes00_03Scalar\BitManipulation\ByteBitReverse\Brev8Instruction.cs
// - \HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\ISA\Instructions\NonVmx\Lanes00_03Scalar\BitManipulation\ByteBitReverse\Brev8Instruction.LocalSemantics.cs
// - \HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Architecture\Common\CPU_Core.Enums.cs
// - \HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\InstructionSupportStatus.cs
// - \HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\IsaV4Surface.cs
// - \HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\OpcodeInfo.Registry.Data.Scalar.cs
// - \HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Frontend\Decode\VliwDecoderV4Bridge\VliwDecoderV4.cs
// - \HybridCPU ISE\HybridCPU_ISE\NonRTL\Core\Diagnostics\InstructionRegistry.Helpers.Core.cs
// - \HybridCPU ISE\HybridCPU_ISE\NonRTL\Core\Diagnostics\InstructionRegistry.Initialize.Scalar.cs
// - \HybridCPU ISE\HybridCPU_ISE\NonRTL\Core\Pipeline\InternalOpBuilder.cs
// - \HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Pipeline\MicroOps\Internal\InternalOp.cs
// - \HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Execution\Scalar\ALU\ScalarAluOps.cs
// - \HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Execution\Dispatch\ExecutionDispatcherV4.Scalar.cs
// - \HybridCPU ISE\HybridCPU_ISE.Tests\tests\NonVmxPhase01ByteBitReverseExecutableTests.cs
// BREV8 production metadata anchor: scalar per-byte bit reverse is opened as a
// unary XLEN=64 scalar ALU row. It remains separate from vector VBREV8; compiler
// helper emission and VMX-specific frontend integration remain closed.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.ByteBitReverse;

public sealed partial class Brev8Instruction
{
    public const string Mnemonic = "BREV8";
    public const string OperandShape = "rd, rs1";
    public const string ParameterDescriptor = "DestRegister, SourceRegister, XLEN=64 per-byte bit-order sideband";
    public const string MicroOpShape = "ScalarUnaryAlu.ReverseBitsInEachByte, retire-owned register writeback, no flags";
    public const string ExecutionLaneBinding = "Lanes00_03Scalar";
    public const string EvidenceBoundary = "ExecutableScalarAlu";
    public const string Operation = "Reverse bit order within each byte; byte positions are preserved.";

    public const int XLen = 64;

    public const bool RequiresByteBitOrderAbi = false;
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

    public static ushort Opcode => (ushort)YAKSys_Hybrid_CPU.Processor.CPU_Core.InstructionsEnum.BREV8;
    public static bool WritesScalarRegister => true;
    public static bool HasSideEffects => false;

    public static ulong Execute(ulong value) =>
        EvaluateXLen64(value);
}
