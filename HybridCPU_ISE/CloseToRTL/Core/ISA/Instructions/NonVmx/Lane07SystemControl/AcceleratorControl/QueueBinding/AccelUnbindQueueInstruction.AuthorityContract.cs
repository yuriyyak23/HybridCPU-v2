namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.AcceleratorControl.QueueBinding;

public sealed partial class AccelUnbindQueueInstruction
{
    public const string ProductionDecision = "Phase14NegativeDecisionGate";
    public const string AcceleratorAuthorityBoundary = "Lane7AcceleratorQueueBindingProductionPathOnly";
    public const string VmxBoundary = "GenericRuntimeOnly";

    public const bool IsAcceleratorQueueBindingControl = true;
    public const bool RequiresAcceleratorRuntimeAuthority = true;
    public const bool RequiresQueueAuthority = true;
    public const bool RequiresTokenAuthority = true;
    public const bool RequiresLane6TokenAuthorityGate = true;
    public const bool RequiresOwnerDomainGuard = true;
    public const bool RequiresBindUnbindQueueAbi = true;
    public const bool RequiresQueueOwnershipModel = true;
    public const bool RequiresCommandQueueSemantics = true;
    public const bool RequiresNoHostEvidenceLeak = true;
    public const bool NoHostEvidenceLeak = true;
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
    public const bool ExistingLane6DmaEvidenceIsInsufficient = true;
    public const bool ExistingLane7ControlPlaneEvidenceIsInsufficient = true;
}
