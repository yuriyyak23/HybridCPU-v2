namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.AcceleratorControl.Topology;

public sealed partial class AccelQueryTopologyInstruction
{
    public const string ProductionDecision = "Phase14NegativeDecisionGate";
    public const string CapabilityAuthorityBoundary = "Lane7AcceleratorCapabilityProductionPathOnly";
    public const string VmxBoundary = "GenericRuntimeOnly";

    public const bool IsAcceleratorCapabilityQuery = true;
    public const bool RequiresCapabilityAuthority = true;
    public const bool RequiresAcceleratorTopologyAbi = true;
    public const bool RequiresBoundedTopologyResultFootprint = true;
    public const bool RequiresResultScrubbingPolicy = true;
    public const bool RequiresOwnerDomainGuard = true;
    public const bool RequiresCommandQueueSemantics = true;
    public const bool RequiresNoHostEvidenceLeak = true;
    public const bool RequiresRetireOwnedPublication = true;
    public const bool RequiresReplayStableCapabilityModel = true;
    public const bool RequiresMigrationCheckpointPolicy = true;
    public const bool RequiresVirtualizationBoundaryPolicy = true;
    public const bool RequiresDecoderEncoderAbiPublication = true;
    public const bool RequiresInstructionIrProjectionPublication = true;
    public const bool RequiresLane7MaterializerPublication = true;
    public const bool RequiresTypedAcceleratorControlMicroOpPublication = true;
    public const bool RequiresConformanceAndGoldenArtifacts = true;
    public const bool NoHostOwnedEvidencePublication = true;
    public const bool NoHiddenScalarLowering = true;
    public const bool NoMultiOpEmission = true;
    public const bool NoGenericSystemOpFallback = true;
    public const bool NoLane6DmaFallback = true;
    public const bool NoLane7SubmitFallback = true;
    public const bool NoExternalBackendFallback = true;
    public const bool NoVmxSpecificPath = true;
    public const bool ExistingAccelSubmitEvidenceIsInsufficient = true;
    public const bool ExistingAccelQueryCapsEvidenceIsInsufficient = true;
    public const bool ExistingTopologyQueueTaxonomyEvidenceIsInsufficient = true;
    public const bool ExistingLane7ControlPlaneEvidenceIsInsufficient = true;
}
