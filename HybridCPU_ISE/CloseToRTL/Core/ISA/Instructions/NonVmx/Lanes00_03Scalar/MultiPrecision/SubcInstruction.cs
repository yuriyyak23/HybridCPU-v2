// SUBC is a multi-precision negative-decision gate: no opcode/materializer/helper
// authority until borrow-out publication ABI is retire-owned. VMX stays generic.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.MultiPrecision;

public sealed partial class SubcInstruction
{
    public const string Mnemonic = "SUBC";
    public const string OperandShape = "explicit borrow-out ABI TBD";
    public const string ParameterDescriptor = "rd, rs1, rs2, explicit borrow-out/result sideband";
    public const string MicroOpShape = "ScalarMultiPrecisionMicroOp candidate; no implicit flags and no MicroOp is published in this template";
    public const string ExecutionLaneBinding = "Lanes00_03Scalar";
    public const string EvidenceBoundary = "MultiPrecisionCarryAbiDeferredNoEmission";
    public const string AbiDecision = "NoAllocationUntilExplicitCarryBorrowPublicationAbi";
    public const string CarryBorrowPublicationPolicy = "No implicit architectural flags; borrow-out must publish through an explicit retire-owned register or sideband ABI.";
    public const string BorrowOutputPolicy = "Borrow-out must be replayable and rollback-safe before execution opens.";
    public const bool RequiresBorrowOutAbi = true;
    public const bool RequiresCarryBorrowPublicationAbi = true;
    public const bool RequiresExplicitBorrowOutputTransportAbi = true;
    public const bool NoImplicitFlags = true;
    public const bool RejectHiddenArchitecturalFlags = true;
    public const bool NoHiddenMultiOpEmission = true;
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
