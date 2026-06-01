namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.TranslationFences;

public sealed partial class SfenceVmaInstruction
{
    public const string ProductionDecision = "Phase13NegativeDecisionGate";
    public const string MaintenanceAuthorityBoundary = "Lane7TranslationFenceProductionPathOnly";
    public const string VmxBoundary = "GenericRuntimeOnly";

    public const bool RequiresTranslationFenceAbi = true;
    public const bool RequiresAddressSpaceSelectorAbi = true;
    public const bool RequiresTranslationStateOwnershipModel = true;
    public const bool RequiresPageTableWalkOwnershipModel = true;
    public const bool RequiresPrivilegeAndAdmissionPolicy = true;
    public const bool RequiresCrossCoreShootdownPolicy = true;
    public const bool RequiresRetireOwnedSideEffectPublication = true;
    public const bool RequiresDecoderEncoderAbiPublication = true;
    public const bool RequiresInstructionIrProjectionPublication = true;
    public const bool RequiresLane7MaterializerPublication = true;
    public const bool RequiresTypedMaintenanceMicroOpPublication = true;
    public const bool RequiresConformanceAndGoldenArtifacts = true;
    public const bool NoGenericFenceFallback = true;
    public const bool NoVmxEptVpidNptSemanticAlias = true;
    public const bool NoGuestVisibleHostEvidence = true;
    public const bool NoHostEvidenceLeak = true;
    public const bool NoHostOwnedEvidencePublication = true;
    public const bool NoHiddenScalarLowering = true;
    public const bool NoMultiOpEmission = true;
    public const bool NoLane6DmaFallback = true;
    public const bool NoLane7AcceleratorFallback = true;
    public const bool NoExternalBackendFallback = true;
    public const bool NoVmxSpecificPath = true;
    public const bool ExistingFenceEvidenceIsInsufficient = true;
    public const bool ExistingFenceIEvidenceIsInsufficient = true;
    public const bool ExistingAtomicOrderingEvidenceIsInsufficient = true;
    public const bool ExistingLane7ControlPlaneEvidenceIsInsufficient = true;
}
