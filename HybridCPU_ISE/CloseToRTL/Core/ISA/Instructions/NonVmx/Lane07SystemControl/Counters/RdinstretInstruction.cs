// RDINSTRET metadata anchor: retired-instruction counter read remains deferred
// until retire accounting, replay rollback, decoder ABI, and publication evidence close.
// VMX must see any future execution only through generic counter/projection policy.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.Counters;

public sealed partial class RdinstretInstruction
{
    public const string Mnemonic = "RDINSTRET";
    public const string OperandShape = "rd";
    public const string ParameterDescriptor = "DestRegister, retire-counter accounting domain sideband";
    public const string MicroOpShape = "Lane7SystemCounter.ReadInstret, retire-owned register writeback, no memory side effects";
    public const string ExecutionLaneBinding = "Lane07SystemControl";
    public const string EvidenceBoundary = "Lane7CounterReplayDeferred";

    public const bool IsSystemCounter = true;
    public const bool RequiresCounterSourceAbi = true;
    public const bool RequiresRetireAccountingModel = true;
    public const bool RequiresReplayStableCounterModel = true;
    public const bool RequiresPrivilegePolicy = true;
    public const bool RequiresDecoderEncoderAbi = true;
    public const bool RequiresInstructionIrProjection = true;
    public const bool RequiresRegistryMaterializer = true;
    public const bool RequiresRetireOwnedPublication = true;
    public const bool RequiresRetireRegisterWriteback = true;
    public const bool RequiresReplayRollbackEvidence = true;
    public const bool SeparateFromClosedRdcycle = true;
    public const bool NoVmxFrontendIntegrationRequired = true;
    public const bool RequiresImmediateVmxProjection = false;
    public const bool RequiresFutureVirtualizationBoundaryPolicy = true;
    public const bool HasOpcodeAllocation = false;
    public const bool IsExecutable = false;
    public const bool CompilerHelperAllowed = false;
}
