// CZERO.NEZ production path:
// - HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\ISA\Instructions\NonVmx\Lanes00_03Scalar\ZeroingSelect\CzeroNezInstruction.cs
// - HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\ISA\Instructions\NonVmx\Lanes00_03Scalar\ZeroingSelect\CzeroNezInstruction.LocalSemantics.cs
// - HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Architecture\Common\CPU_Core.Enums.cs
// - HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\InstructionSupportStatus.cs
// - HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\IsaV4Surface.cs
// - HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\OpcodeInfo.Registry.Data.Scalar.cs
// - HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Frontend\Decode\VliwDecoderV4Bridge\VliwDecoderV4.cs
// - HybridCPU ISE\HybridCPU_ISE\NonRTL\Core\Diagnostics\InstructionRegistry.Helpers.Core.cs
// - HybridCPU ISE\HybridCPU_ISE\NonRTL\Core\Diagnostics\InstructionRegistry.Initialize.Scalar.cs
// - HybridCPU ISE\HybridCPU_ISE\NonRTL\Core\Pipeline\InternalOpBuilder.cs
// - HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Pipeline\MicroOps\Internal\InternalOp.cs
// - HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Execution\Scalar\ALU\ScalarAluOps.cs
// - HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Execution\Dispatch\ExecutionDispatcherV4.Scalar.cs
// - HybridCPU ISE\HybridCPU_ISE.Tests\tests\NonVmxPhase01ScalarSelectExecutableTests.cs
// CZERO.NEZ production metadata anchor: scalar zeroing select is opened as a
// binary XLEN=64 scalar ALU row. Its nonzero polarity evidence is separate from
// closed CZERO.EQZ; VMX remains a generic runtime client only.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.ZeroingSelect;

public sealed partial class CzeroNezInstruction
{
    public const string Mnemonic = "CZERO.NEZ";
    public const string OperandShape = "rd, rs1, rs2";
    public const string ParameterDescriptor = "rd receives zero or rs1 according to the nonzero test of rs2";
    public const string MicroOpShape = "ScalarBinaryAlu.ZeroIfNotEqualZero, retire-owned register writeback, no flags";
    public const string ExecutionLaneBinding = "Lanes00_03Scalar";
    public const string EvidenceBoundary = "ExecutableScalarAlu";
    public const string Polarity = "ConditionNotEqualZeroProducesZero";

    public const int XLen = 64;

    public const bool RequiresPolarityProof = false;
    public const bool PolarityProofClosed = true;
    public const bool SeparateFromClosedCzeroEqz = true;
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

    public static ushort Opcode => (ushort)YAKSys_Hybrid_CPU.Processor.CPU_Core.InstructionsEnum.CZERO_NEZ;
    public static bool WritesScalarRegister => true;
    public static bool HasSideEffects => false;

    public static ulong Execute(ulong value, ulong condition) =>
        EvaluateXLen64(value, condition);
}
