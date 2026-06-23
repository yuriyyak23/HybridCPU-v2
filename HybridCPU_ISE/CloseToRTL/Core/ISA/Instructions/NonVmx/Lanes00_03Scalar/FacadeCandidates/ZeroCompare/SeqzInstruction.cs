// SEQZ facade decision path:
// - \HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\ISA\Instructions\NonVmx\Lanes00_03Scalar\FacadeCandidates\ZeroCompare\SeqzInstruction.cs
// - \HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\ISA\Instructions\NonVmx\Lanes00_03Scalar\FacadeCandidates\ZeroCompare\SeqzInstruction.LocalNoEmissionContract.cs
// - \HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\InstructionSupportStatus.cs
// - \HybridCPU ISE\HybridCPU_ISE.Tests\CompilerTests\CompilerNoEmissionBoundaryTests.cs
// SEQZ is closed as facade-only/no-emission for this Phase 01F decision. No
// hardware opcode, decoder row, compiler helper, or hidden lowering is opened.
// VMX observes only the generic runtime model.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.FacadeCandidates.ZeroCompare;

public sealed partial class SeqzInstruction
{
    public const string Mnemonic = "SEQZ";
    public const string OperandShape = "rd, rs1";
    public const string ParameterDescriptor = "rd receives rs1 == 0 as 0/1 if a hardware opcode is approved";
    public const string MicroOpShape = "ScalarUnaryAluMicroOp candidate; no MicroOp is published in this template";
    public const string ExecutionLaneBinding = "Lanes00_03Scalar";
    public const string EvidenceBoundary = "FacadeOnlyNoEmissionClosed";
    public const bool RequiresHardwareOpcodeDecision = false;
    public const bool FacadeDecisionClosed = true;
    public const bool SelectedHardwareOpcode = false;
    public const bool SelectedFacadeOnly = true;
    public const bool HiddenLoweringAllowed = false;
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
