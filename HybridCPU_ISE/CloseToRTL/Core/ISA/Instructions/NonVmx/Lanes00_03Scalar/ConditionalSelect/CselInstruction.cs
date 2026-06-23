// CSEL Phase 01E carrier-gate decision path, closed negative:
// - \HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\ISA\Instructions\NonVmx\Lanes00_03Scalar\ConditionalSelect\CselInstruction.cs
// - \HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\ISA\Instructions\NonVmx\Lanes00_03Scalar\ConditionalSelect\CselInstruction.LocalSemantics.cs
// - \HybridCPU ISE\HybridCPU_ISE\NonRTL\Arch\InstructionSupportStatus.cs
// - \HybridCPU ISE\HybridCPU_ISE.Tests\tests\NonVmxPhase01ScalarSelectExecutableTests.cs
// - \HybridCPU ISE\HybridCPU_ISE.Tests\tests\NonVmxIteration04BDeferredTemplateSurfaceTests.cs
// Future hardware CSEL requires a new scalar-select production package with an
// approved four-register carrier/sideband ABI plus decoder/encoder,
// InstructionIR/projection, registry/materializer, InternalOp, execute,
// retire, replay, golden, and no-emission evidence.
// CSEL remains metadata-only because current packed scalar IR carries only rd/rs1/rs2
// and Phase 01E/01 gate closure does not approve a four-register source carrier
// for rs_cond. VMX is not a direct integration point for this row.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.ConditionalSelect;

public sealed partial class CselInstruction
{
    public const string Mnemonic = "CSEL";
    public const string OperandShape = "rd, rs_true, rs_false, rs_cond";
    public const string ParameterDescriptor = "rd selects rs_true or rs_false from explicit condition register sideband";
    public const string MicroOpShape = "ScalarSelectMicroOp candidate; no MicroOp is published in this template";
    public const string ExecutionLaneBinding = "Lanes00_03Scalar";
    public const string EvidenceBoundary = "ScalarSelectAbiDeferredNoEmission";
    public const string CarrierGateDecision = "Phase01ECarrierGateClosedNoApprovedCarrier";
    public const bool RequiresFourRegisterCarrierAbi = true;
    public const bool FourSourceCarrierDecisionClosed = true;
    public const bool ExternalCarrierGateClosed = true;
    public const bool ApprovedFourSourceCarrier = false;
    public const bool ExternalCarrierApprovedInPhase01 = false;
    public const bool CurrentPackedScalarIrSupportsCarrier = false;
    public const bool RequiresExternalCarrierAbi = true;
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
