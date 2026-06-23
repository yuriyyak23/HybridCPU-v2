// POPCNT alias decision path:
// - \HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\ISA\Instructions\NonVmx\Lanes00_03Scalar\BitManipulation\BitCount\PopcntInstruction.cs
// - \HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\ISA\Instructions\NonVmx\Lanes00_03Scalar\BitManipulation\BitCount\PopcntInstruction.LocalSemantics.cs
// - \HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\InstructionSupportStatus.cs
// - \HybridCPU ISE\HybridCPU_ISE.Tests\CompilerTests\CompilerNoEmissionBoundaryTests.cs
// - \HybridCPU ISE\HybridCPU_ISE.Tests\CompilerTests\CompilerFacadeAbiPhase01Tests.cs
// POPCNT is not a separate runtime opcode. CPOP is the canonical hardware row;
// POPCNT remains reserved/no-emission until a future parser-only alias policy is
// explicitly approved. VMX has no direct integration point for this alias.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.BitManipulation.BitCount;

public sealed partial class PopcntInstruction
{
    public const string Mnemonic = "POPCNT";
    public const string OperandShape = "rd, rs1";
    public const string ParameterDescriptor = "DestRegister, SourceRegister, XLEN=64 population-count sideband";
    public const string MicroOpShape = "ScalarUnaryAlu.PopCount, retire-owned register writeback, no flags";
    public const string ExecutionLaneBinding = "Lanes00_03Scalar";
    public const string EvidenceBoundary = "FacadeAliasNoEmissionClosed";
    public const string AliasPolicy = "CPOP is the canonical hardware mnemonic; POPCNT remains no-emission alias/reserved.";

    public const int XLen = 64;

    public const bool RequiresCanonicalMnemonicDecision = false;
    public const bool CanonicalMnemonicDecisionClosed = true;
    public const bool SelectedAsRuntimeMnemonic = false;
    public const bool SelectedAsNoEmissionAlias = true;
    public const bool RequiresAliasNoEmissionPolicy = false;
    public const bool AliasNoEmissionPolicyClosed = true;
    public const bool RequiresPopcountSemantics = true;
    public const bool RequiresDecoderEncoderAbi = true;
    public const bool RequiresInstructionIrProjection = true;
    public const bool RequiresRegistryMaterializer = true;
    public const bool RequiresRetireRegisterWriteback = true;
    public const bool RequiresReplayRollbackEvidence = true;
    public const bool NoVmxFrontendIntegrationRequired = true;
    public const bool RequiresVmxProjection = false;
    public const bool HasOpcodeAllocation = false;
    public const bool IsExecutable = false;
    public const bool CompilerHelperAllowed = false;
}
