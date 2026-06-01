namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask;

public sealed partial class VmsofInstruction
{
    public const string Mnemonic = "VMSOF";
    public const string OperandShape = "predicate destination/source sideband";
    public const string EvidenceBoundary = "VectorPredicateOnlyContourFailClosed";
    public const string PredicateDestinationRepresentationPolicy = "Unresolved; predicate-only destination representation ABI required";
    public const string PrefixSuffixMaskSemanticsPolicy = "Unresolved; only-first semantics require explicit predicate publication ABI";
    public const string TailMaskPolicy = "Unresolved; tail/mask policy ABI required";
    public const string StagedPredicatePublicationPolicy = "Unresolved; retire-staged predicate publication ABI required";
    public const string ExecutionLaneBinding = "Lanes00_03Vector";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const bool RequiresPredicateMaskSideband = true;
    public const bool RequiresPredicateOnlyPublication = true;
    public const bool RequiresPredicateOnlyDestinationRepresentation = true;
    public const bool RequiresPrefixSuffixMaskSemantics = true;
    public const bool RequiresTailMaskPolicyAbi = true;
    public const bool RequiresVectorLegalityMatrixClosure = true;
    public const bool RequiresDecoderEncoderAbi = true;
    public const bool RequiresInstructionIrProjection = true;
    public const bool RequiresRegistryMaterializer = true;
    public const bool RequiresTypedVectorMicroOp = true;
    public const bool RequiresRetireStagedPublication = true;
    public const bool RequiresReplayRollbackConformance = true;
    public const bool RequiresGoldenArtifacts = true;
    public const bool NoVectorRfExposure = true;
    public const bool NoDescriptorFallback = true;
    public const bool NoHiddenStreamEngineFallback = true;
    public const bool NoHiddenDmaFallback = true;
    public const bool NoHiddenScalarLowering = true;
    public const bool NoMultiOpEmission = true;
    public const bool NoVmxSpecificPath = true;
    public const bool IsExecutable = false;
    public const bool CompilerHelperAllowed = false;
}
