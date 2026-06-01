// MAX production path:
// - HybridCPU ISE\HybridCPU_ISE\Core\Common\CPU_Core.Enums.cs
// - HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\OpcodeInfo.Registry.Data.Scalar.cs
// - HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\InstructionSupportStatus.cs
// - HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\IsaV4Surface.cs
// - HybridCPU ISE\HybridCPU_ISE\Core\Decoder\VliwDecoderV4.cs
// - HybridCPU ISE\HybridCPU_ISE\NonRTL\Core\Diagnostics\InstructionRegistry.Initialize.Scalar.cs
// - HybridCPU ISE\HybridCPU_ISE\NonRTL\Core\Pipeline\InternalOpBuilder.cs
// - HybridCPU ISE\HybridCPU_ISE\Core\ALU\ScalarAluOps.cs
// - HybridCPU ISE\HybridCPU_ISE\Core\Execution\ExecutionDispatcherV4.Scalar.cs
// - HybridCPU ISE\HybridCPU_ISE.Tests\tests\NonVmxPhase01ScalarMinMaxExecutableTests.cs
// MAX production metadata anchor: scalar signed max is runtime-owned by the scalar ALU evidence chain.
// It is distinct from vector, AMO, and Lane6 descriptor min/max surfaces; no VMX-specific path is added.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.ScalarMinMax;

public sealed partial class MaxInstruction
{
    public const string Mnemonic = "MAX";
    public const string OperandShape = "rd, rs1, rs2";
    public const string ParameterDescriptor = "DestRegister, SourceRegister1, SourceRegister2, XLEN=64 signed comparison sideband";
    public const string MicroOpShape = "ScalarBinaryAlu.MaxSigned, retire-owned register writeback, no flags";
    public const string ExecutionLaneBinding = "Lanes00_03Scalar";
    public const string EvidenceBoundary = "ExecutableScalarAlu";
    public const string Signedness = "Signed";

    public const int XLen = 64;

    public const bool RequiresSignednessAbi = false;
    public const bool UsesSignedComparison = true;
    public const bool SeparateFromVectorMinMax = true;
    public const bool SeparateFromAmoMinMax = true;
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

    public static ushort Opcode => (ushort)YAKSys_Hybrid_CPU.Processor.CPU_Core.InstructionsEnum.MAX;
    public static bool WritesScalarRegister => true;
    public static bool HasSideEffects => false;

    public static ulong Execute(ulong left, ulong right) =>
        EvaluateXLen64(left, right);
}
