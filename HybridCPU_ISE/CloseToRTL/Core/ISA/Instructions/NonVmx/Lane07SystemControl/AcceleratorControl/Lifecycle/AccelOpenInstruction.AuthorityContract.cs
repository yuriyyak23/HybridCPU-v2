namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.AcceleratorControl.Lifecycle;

public sealed partial class AccelOpenInstruction
{
    public const string ProductionDecision = "Phase14NegativeDecisionGate";
    public const string AcceleratorAuthorityBoundary = "Lane7AcceleratorLifecycleProductionPathOnly";
    public const string VmxBoundary = "GenericRuntimeOnly";

    public const bool IsAcceleratorLifecycleControl = true;
    public const bool RequiresAcceleratorRuntimeAuthority = true;
    public const bool RequiresDeviceAuthority = true;
    public const bool RequiresTokenAuthority = true;
    public const bool RequiresOwnerDomainGuard = true;
    public const bool RequiresHandleNamespaceAbi = true;
    public const bool RequiresOpenCloseLifecycleAbi = true;
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
    public const bool ExistingLane7ControlPlaneEvidenceIsInsufficient = true;
}
