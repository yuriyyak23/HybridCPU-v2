// PAUSE metadata anchor: scheduling hint remains deferred/no-execution until
// hint encoding, progress semantics, replay, and no-state-publication evidence close.
// It must not promise architectural progress or expose a VMX-specific integration path.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.Hints;

public sealed partial class PauseInstruction
{
    public const string Mnemonic = "PAUSE";
    public const string OperandShape = "no operands or approved hint immediate";
    public const string ParameterDescriptor = "No architectural operands; optional hint immediate only after ABI decision";
    public const string MicroOpShape = "Lane7SchedulingHint.Pause, no architectural writeback, no progress guarantee";
    public const string ExecutionLaneBinding = "Lane07SystemControl";
    public const string EvidenceBoundary = "Lane7HintNoExecutionGuarantee";

    public const bool IsSchedulingHint = true;
    public const bool RequiresHintEncodingAbi = true;
    public const bool RequiresNoArchitecturalStateLeakage = true;
    public const bool NoArchitecturalProgressGuarantee = true;
    public const bool RequiresDecoderEncoderAbi = true;
    public const bool RequiresInstructionIrProjection = true;
    public const bool RequiresRegistryMaterializer = true;
    public const bool RequiresRetireOwnedPublication = false;
    public const bool RequiresRetireRegisterWriteback = false;
    public const bool RequiresReplayRollbackEvidence = true;
    public const bool NoVmxFrontendIntegrationRequired = true;
    public const bool RequiresImmediateVmxProjection = false;
    public const bool RequiresFutureVirtualizationBoundaryPolicy = false;
    public const bool HasOpcodeAllocation = false;
    public const bool IsExecutable = false;
    public const bool CompilerHelperAllowed = false;
}
