namespace YAKSys_Hybrid_CPU.Core;

public enum ProjectionSchemaPipelineViolation : byte
{
    None = 0,
    MissingSchemaPath = 1,
    MissingGeneratorName = 2,
    MissingCanonicalSourceHash = 3,
    MissingGeneratedArtifactHash = 4,
    MissingEntryCount = 5,
    MissingBuildGeneration = 6,
    MissingConformanceParity = 7,
    MissingAbiFreeze = 8,
}

public readonly record struct ProjectionSchemaPipelineArtifact(
    string SchemaPath,
    string GeneratorName,
    ulong SourceHash,
    ulong ArtifactHash,
    int EntryCount,
    bool GeneratedAtBuild,
    bool RequiresConformanceParity,
    bool RequiresAbiFreeze);

public sealed partial class ProjectionSchemaPipelineContract
{
    public static ProjectionSchemaPipelineArtifact CurrentVmcsFieldProjection { get; } = new(
        "docs/VMXRefactoring/schemas/vmcs-field-projection-schema.v1.json",
        "VmcsFieldProjectionGenerator",
        VmcsFieldProjectionSchema.CanonicalArtifact.SourceHash,
        VmcsFieldProjectionSchema.CanonicalArtifact.ArtifactHash,
        VmcsFieldProjectionSchema.CanonicalArtifact.EntryCount,
        GeneratedAtBuild: true,
        RequiresConformanceParity: true,
        RequiresAbiFreeze: true);

    public static ProjectionSchemaPipelineArtifact CurrentVmxCapsBitProjection { get; } = new(
        "docs/VMXRefactoring/schemas/vmxcaps-capability-bit-schema.v1.json",
        "VmxCapsCapabilityBitGenerator",
        CapabilityDescriptorSetSchema.VmxCompatibilityBitSchema.SourceHash,
        CapabilityDescriptorSetSchema.VmxCompatibilityBitSchema.ArtifactHash,
        CapabilityDescriptorSetSchema.VmxCompatibilityBitSchema.EntryCount,
        GeneratedAtBuild: true,
        RequiresConformanceParity: true,
        RequiresAbiFreeze: true);

    public ProjectionSchemaPipelineViolation Evaluate(ProjectionSchemaPipelineArtifact artifact)
    {
        if (string.IsNullOrWhiteSpace(artifact.SchemaPath))
        {
            return ProjectionSchemaPipelineViolation.MissingSchemaPath;
        }

        if (string.IsNullOrWhiteSpace(artifact.GeneratorName))
        {
            return ProjectionSchemaPipelineViolation.MissingGeneratorName;
        }

        if (artifact.SourceHash == 0)
        {
            return ProjectionSchemaPipelineViolation.MissingCanonicalSourceHash;
        }

        if (artifact.ArtifactHash == 0)
        {
            return ProjectionSchemaPipelineViolation.MissingGeneratedArtifactHash;
        }

        if (artifact.EntryCount <= 0)
        {
            return ProjectionSchemaPipelineViolation.MissingEntryCount;
        }

        if (!artifact.GeneratedAtBuild)
        {
            return ProjectionSchemaPipelineViolation.MissingBuildGeneration;
        }

        if (!artifact.RequiresConformanceParity)
        {
            return ProjectionSchemaPipelineViolation.MissingConformanceParity;
        }

        return artifact.RequiresAbiFreeze
            ? ProjectionSchemaPipelineViolation.None
            : ProjectionSchemaPipelineViolation.MissingAbiFreeze;
    }

    public bool IsSatisfied(ProjectionSchemaPipelineArtifact artifact) =>
        Evaluate(artifact) == ProjectionSchemaPipelineViolation.None;

    public bool IsCurrentVmcsFieldProjectionSatisfied() =>
        IsSatisfied(CurrentVmcsFieldProjection);

    public bool IsCurrentVmxCapsBitProjectionSatisfied() =>
        IsSatisfied(CurrentVmxCapsBitProjection);
}
