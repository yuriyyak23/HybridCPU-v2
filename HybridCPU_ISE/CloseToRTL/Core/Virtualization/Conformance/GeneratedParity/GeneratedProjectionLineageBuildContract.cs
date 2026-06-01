namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class GeneratedProjectionLineageBuildContract
{
    public const string BuildPropertyName = "EnableVmxProjectionLineageCheck";
    public const string BuildTargetName = "VerifyVmxProjectionLineage";
    public const string BuildTargetPhase = "CoreCompile";
    public const string VerifierScriptPath = "tools/VMXProjectionLineage/VerifyProjectionLineage.ps1";
    public const string VmcsFieldProjectionSchemaPath = "docs/VMXRefactoring/schemas/vmcs-field-projection-schema.v1.json";
    public const string CompatAliasSchemaPath = "docs/VMXRefactoring/schemas/compat-alias-schema.v1.json";
    public const string VmxCapsCapabilityBitSchemaPath = "docs/VMXRefactoring/schemas/vmxcaps-capability-bit-schema.v1.json";
    public const string CompatSpecArtifactSchemaPath = "docs/VMXRefactoring/schemas/compat-spec-artifact-schema.v1.json";
    public const string VmcsFieldProjectionSourcePath = "CloseToRTL/Core/Virtualization/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs";
    public const string CompatAliasSourcePath = "CloseToRTL/Core/Virtualization/Compatibility/Generated/AliasMaps/CompatAliasMap.cs";
    public const string VmxCapsCapabilityBitSourcePath = "CloseToRTL/Core/Virtualization/Compatibility/Generated/CapabilityProjection/CapabilityDescriptorSetSchema.cs";
    public const string CompatSpecArtifactSourcePath = "CloseToRTL/Core/Virtualization/Compatibility/Generated/SpecArtifacts/CompatSpecArtifactSet.cs";

    public static IReadOnlyList<string> RequiredVerifierFunctions { get; } = new[]
    {
        "New-VmcsFieldProjectionSchemaSource",
        "New-CompatAliasMapSource",
        "New-VmxCapsCapabilityBitSchemaSource",
        "New-CompatSpecArtifactSetSource",
        "Assert-GeneratedSourceMatches",
        "Assert-RequiredSchemaSwitches",
    };

    public static IReadOnlyList<string> RequiredGeneratedInputs { get; } = new[]
    {
        VmcsFieldProjectionSchemaPath,
        CompatAliasSchemaPath,
        VmxCapsCapabilityBitSchemaPath,
        CompatSpecArtifactSchemaPath,
    };

    public static IReadOnlyList<string> RequiredGeneratedOutputs { get; } = new[]
    {
        VmcsFieldProjectionSourcePath,
        CompatAliasSourcePath,
        VmxCapsCapabilityBitSourcePath,
        CompatSpecArtifactSourcePath,
    };

    public static IReadOnlyList<string> RequiredDriftFailureMarkers { get; } = new[]
    {
        "Generated projection drift detected",
        "Regenerated expectation:",
    };

    public static IReadOnlyList<string> ForbiddenRuntimeOwnerMarkers { get; } = new[]
    {
        "VmcsProjectionRuntimeManager",
        "VmcsV2Runtime",
        "VmcsV2RuntimeManager",
        "VmcsManagerAdapter",
    };

    public bool UsesBuildTimeTarget => true;

    public bool RegeneratesVmcsFieldProjectionSchema => true;

    public bool RegeneratesCompatAliasMap => true;

    public bool RegeneratesVmxCapsCapabilityBitSchema => true;

    public bool RegeneratesCompatSpecArtifactManifest => true;

    public bool FailsBuildOnGeneratedDrift => true;

    public bool LeavesRuntimeAuthorityUnchanged => true;

    public bool DoesNotCreateRuntimeOwner => true;

    public bool IsSatisfied() =>
        UsesBuildTimeTarget &&
        RegeneratesVmcsFieldProjectionSchema &&
        RegeneratesCompatAliasMap &&
        RegeneratesVmxCapsCapabilityBitSchema &&
        RegeneratesCompatSpecArtifactManifest &&
        FailsBuildOnGeneratedDrift &&
        LeavesRuntimeAuthorityUnchanged &&
        DoesNotCreateRuntimeOwner;
}
