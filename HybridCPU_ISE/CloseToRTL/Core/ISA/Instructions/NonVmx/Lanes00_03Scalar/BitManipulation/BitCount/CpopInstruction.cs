// CPOP production path:
// - \HybridCPU_ISE\CloseToRTL\Core\ISA\Instructions\NonVmx\Lanes00_03Scalar\BitManipulation\BitCount\CpopInstruction.cs
// - \HybridCPU_ISE\CloseToRTL\Core\ISA\Instructions\NonVmx\Lanes00_03Scalar\BitManipulation\BitCount\CpopInstruction.LocalSemantics.cs
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
// - \HybridCPU_ISE.Tests\tests\NonVmxPhase01BitCountExecutableTests.cs
// CPOP is the canonical XLEN=64 scalar population-count runtime mnemonic.
// POPCNT remains a no-emission alias boundary. VMX observes this row only through
// generic legality/projection.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.BitCount;

public sealed partial class CpopInstruction
{
    public const string Mnemonic = "CPOP";
    public const string OperandShape = "rd, rs1";
    public const string ParameterDescriptor = "DestRegister, SourceRegister, XLEN=64 population-count sideband";
    public const string MicroOpShape = "ScalarUnaryAlu.PopCount, retire-owned register writeback, no flags";
    public const string ExecutionLaneBinding = "Lanes00_03Scalar";
    public const string EvidenceBoundary = "ExecutableScalarAlu";
    public const string AliasPolicy = "CPOP is the canonical hardware mnemonic; POPCNT remains no-emission alias/reserved.";

    public const int XLen = 64;

    public const bool RequiresCanonicalMnemonicDecision = false;
    public const bool CanonicalMnemonicDecisionClosed = true;
    public const bool PopcntAliasNoEmissionPolicyClosed = true;
    public const bool RequiresAliasNoEmissionPolicy = false;
    public const bool RequiresPopcountSemantics = false;
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

    public static ushort Opcode => (ushort)YAKSys_Hybrid_CPU.Processor.CPU_Core.InstructionsEnum.CPOP;
    public static bool WritesScalarRegister => true;
    public static bool HasSideEffects => false;

    public static ulong Execute(ulong source) =>
        EvaluateXLen64(source);
}
