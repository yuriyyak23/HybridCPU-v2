namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.CompatibilityConformance;

public static class Phase15CompatibilityConformanceSweepContract
{
    public const string Phase = "Phase15";
    public const string SweepName = "NonVmxCompatibilityConformanceSweep";
    public const string ClosureDecision = "CompatibilityConformanceAuditOnly";
    public const string VmxBoundary = "GenericRuntimeOnly";

    public const bool AddsInstructionSemantics = false;
    public const bool AllocatesOpcode = false;
    public const bool OpensDecoderEncoderAbi = false;
    public const bool OpensInstructionIrProjection = false;
    public const bool OpensRegistryMaterializer = false;
    public const bool PublishesTypedMicroOp = false;
    public const bool OpensExecutionPath = false;
    public const bool OpensRetireSideEffects = false;
    public const bool OpensCompilerHelper = false;
    public const bool OpensVmxSpecificPath = false;

    public static string[] ExecutableEvidenceChain { get; } =
    [
        "CAT",
        "OP",
        "DEC",
        "IR",
        "MAT",
        "OBJ",
        "UOP",
        "EXE",
        "RET",
        "RPL",
        "TST",
        "GLD",
        "NOE"
    ];

    public static string[] DeferredEvidenceGuards { get; } =
    [
        "NoOpcodeAllocation",
        "NoDecoderEncoderAbi",
        "NoInstructionIrProjection",
        "NoRegistryMaterializer",
        "NoTypedMicroOpPublication",
        "NoExecutionPath",
        "NoRetireSideEffectPublication",
        "NoCompilerHelper",
        "NoVmxSpecificPath"
    ];

    public static string[] SweepBoundaries { get; } =
    [
        "VectorLegalityMatrix",
        "Lane6DescriptorAuthority",
        "Lane7PrivilegeAdmission",
        "RetireOwnedPublication",
        "ReplayRollbackConformance",
        "GoldenArtifacts",
        "CompilerNoEmission",
        "HostEvidenceNonLeak"
    ];
}
