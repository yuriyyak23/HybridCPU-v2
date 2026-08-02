namespace YAKSys_Hybrid_CPU.Core;

public enum CompatAliasGeneratorPipelineViolation : byte
{
    None = 0,
    MissingSchemaPath = 1,
    MissingGeneratorName = 2,
    MissingCanonicalSchema = 3,
    MissingGeneratedArtifact = 4,
    MissingGeneratedParityRequirement = 5,
    MissingAbiFreezeRequirement = 6,
    SchemaConformanceFailed = 7,
}

public readonly record struct CompatAliasGeneratorPipelineArtifact(
    string SchemaPath,
    string GeneratorName,
    CompatAliasSchemaArtifact CanonicalSchema,
    bool GeneratedAtBuild,
    bool RequiresConformanceParity);

public sealed partial class CompatAliasGeneratorPipelineContract
{
    public static CompatAliasGeneratorPipelineArtifact Current { get; } = new(
        "docs/VMXRefactoring/schemas/compat-alias-schema.v1.json",
        "CompatAliasMapGenerator",
        CompatAliasMap.CanonicalSchema,
        GeneratedAtBuild: true,
        RequiresConformanceParity: true);

    public CompatAliasGeneratorPipelineViolation Evaluate(
        CompatAliasGeneratorPipelineArtifact artifact,
        CompatSpecArtifactSet specArtifacts,
        CompatAliasSchemaConformanceContract schemaContract)
    {
        if (string.IsNullOrWhiteSpace(artifact.SchemaPath))
        {
            return CompatAliasGeneratorPipelineViolation.MissingSchemaPath;
        }

        if (string.IsNullOrWhiteSpace(artifact.GeneratorName))
        {
            return CompatAliasGeneratorPipelineViolation.MissingGeneratorName;
        }

        if (!artifact.CanonicalSchema.IsCanonical ||
            artifact.CanonicalSchema.SourceHash == 0)
        {
            return CompatAliasGeneratorPipelineViolation.MissingCanonicalSchema;
        }

        if (!artifact.GeneratedAtBuild ||
            artifact.CanonicalSchema.ArtifactHash == 0)
        {
            return CompatAliasGeneratorPipelineViolation.MissingGeneratedArtifact;
        }

        if (specArtifacts is null ||
            !specArtifacts.RequiresGeneratedParity(CompatSpecArtifactKind.CompatAliasMap) ||
            !artifact.RequiresConformanceParity)
        {
            return CompatAliasGeneratorPipelineViolation.MissingGeneratedParityRequirement;
        }

        if (!specArtifacts.RequiresAbiFreeze(CompatSpecArtifactKind.CompatAliasMap))
        {
            return CompatAliasGeneratorPipelineViolation.MissingAbiFreezeRequirement;
        }

        schemaContract ??= new CompatAliasSchemaConformanceContract();
        return schemaContract.IsSatisfied(new CompatAliasSchemaConformanceRequest(
            new CompatAliasMap(),
            specArtifacts,
            artifact.CanonicalSchema))
                ? CompatAliasGeneratorPipelineViolation.None
                : CompatAliasGeneratorPipelineViolation.SchemaConformanceFailed;
    }

    public bool IsSatisfied(CompatAliasGeneratorPipelineArtifact artifact) =>
        Evaluate(
            artifact,
            new CompatSpecArtifactSet(),
            new CompatAliasSchemaConformanceContract()) == CompatAliasGeneratorPipelineViolation.None;

    public bool IsCurrentPipelineSatisfied() =>
        IsSatisfied(Current);
}
