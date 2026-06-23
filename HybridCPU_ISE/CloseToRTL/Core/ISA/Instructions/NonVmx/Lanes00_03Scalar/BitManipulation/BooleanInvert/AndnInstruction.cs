// Production path for ANDN:
// \HybridCPU ISE\HybridCPU_ISE\Core\Common\CPU_Core.Enums.cs
// \HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\OpcodeInfo.Registry.Data.Scalar.cs
// \HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\InstructionSupportStatus.cs
// \HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\IsaV4Surface.cs
// \HybridCPU ISE\HybridCPU_ISE\Core\Decoder\VliwDecoderV4.cs
// \HybridCPU ISE\HybridCPU_ISE\NonRTL\Core\Diagnostics\InstructionRegistry.Initialize.Scalar.cs
// \HybridCPU ISE\HybridCPU_ISE\NonRTL\Core\Pipeline\InternalOpBuilder.cs
// \HybridCPU ISE\HybridCPU_ISE\Core\ALU\ScalarAluOps.cs
// \HybridCPU ISE\HybridCPU_ISE\Core\Execution\ExecutionDispatcherV4.Scalar.cs
// \HybridCPU ISE\HybridCPU_ISE.Tests\tests\NonVmxPhase01AndnExecutableTests.cs
// ANDN production metadata anchor: bitwise-invert boolean ALU is opened as a
// single scalar register-register opcode with no compiler helper authority.
// VMX does not receive a special frontend path for this ordinary scalar row.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.BooleanInvert;

public sealed partial class AndnInstruction
{
    public const string Mnemonic = "ANDN";
    public const string OperandShape = "rd, rs1, rs2";
    public const string ParameterDescriptor = "DestRegister, SourceRegister1, SourceRegister2, XLEN=64 boolean-invert sideband";
    public const string MicroOpShape = "ScalarBinaryAlu.AndNot, retire-owned register writeback, no flags";
    public const string ExecutionLaneBinding = "Lanes00_03Scalar";
    public const string EvidenceBoundary = "ExecutableScalarAlu";
    public const string Operation = "rs1 & ~rs2";

    public const int XLen = 64;

    public const bool RequiresFacadeOrHardwareDecision = false;
    public const bool RequiresBitwiseInvertSecondOperandAbi = false;
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

    public static ushort Opcode => (ushort)Processor.CPU_Core.InstructionsEnum.ANDN;

    public static bool WritesScalarRegister => true;

    public static bool HasSideEffects => false;

    public static ulong Execute(ulong left, ulong right) =>
        left & ~right;
}
