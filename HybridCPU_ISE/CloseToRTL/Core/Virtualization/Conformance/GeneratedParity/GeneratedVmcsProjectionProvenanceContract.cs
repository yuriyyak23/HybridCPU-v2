namespace YAKSys_Hybrid_CPU.Core;

public enum GeneratedVmcsProjectionProvenanceViolation : byte
{
    None = 0,
    FieldSchemaParityFailed = 1,
    GeneratedParityFailed = 2,
    ArtifactHashMismatch = 3,
}

public readonly record struct GeneratedVmcsProjectionProvenanceRequest(
    VmcsFieldProjectionSchemaArtifact FieldSchema,
    DescriptorSidebandEnvelope SourceDescriptor,
    ulong ProjectionSourceHash,
    ulong ProjectionArtifactHash,
    bool IsGeneratedCompatibilityProjection,
    bool CarriesHostOwnedEvidence);

public sealed partial class GeneratedVmcsProjectionProvenanceContract
{
    private readonly VmcsFieldProjectionSchemaConformanceContract _fieldSchema;
    private readonly GeneratedProjectionParityContract _generatedParity;

    public GeneratedVmcsProjectionProvenanceContract()
        : this(
            new VmcsFieldProjectionSchemaConformanceContract(),
            new GeneratedProjectionParityContract())
    {
    }

    public GeneratedVmcsProjectionProvenanceContract(
        VmcsFieldProjectionSchemaConformanceContract fieldSchema,
        GeneratedProjectionParityContract generatedParity)
    {
        _fieldSchema = fieldSchema;
        _generatedParity = generatedParity;
    }

    public GeneratedVmcsProjectionProvenanceViolation Evaluate(
        GeneratedVmcsProjectionProvenanceRequest request)
    {
        if (!_fieldSchema.IsSatisfied(request.FieldSchema))
        {
            return GeneratedVmcsProjectionProvenanceViolation.FieldSchemaParityFailed;
        }

        if (request.ProjectionArtifactHash != request.FieldSchema.ArtifactHash)
        {
            return GeneratedVmcsProjectionProvenanceViolation.ArtifactHashMismatch;
        }

        var parity = new GeneratedProjectionParityRequest(
            request.SourceDescriptor,
            request.ProjectionSourceHash,
            request.ProjectionArtifactHash,
            request.IsGeneratedCompatibilityProjection,
            request.CarriesHostOwnedEvidence);

        return _generatedParity.IsSatisfied(parity)
            ? GeneratedVmcsProjectionProvenanceViolation.None
            : GeneratedVmcsProjectionProvenanceViolation.GeneratedParityFailed;
    }

    public bool IsSatisfied(GeneratedVmcsProjectionProvenanceRequest request) =>
        Evaluate(request) == GeneratedVmcsProjectionProvenanceViolation.None;
}
