namespace YAKSys_Hybrid_CPU.Core;

public enum GeneratedProjectionParityViolation : byte
{
    None = 0,
    SourceNotDescriptorOwned = 1,
    MissingGeneratedProjectionMarker = 2,
    SourceHashMismatch = 3,
    ProjectionArtifactHashMissing = 4,
    HostEvidenceProjectionAttempt = 5,
}

public readonly record struct GeneratedProjectionParityRequest(
    DescriptorSidebandEnvelope SourceDescriptor,
    ulong ProjectionSourceHash,
    ulong ProjectionArtifactHash,
    bool IsGeneratedCompatibilityProjection,
    bool CarriesHostOwnedEvidence);

public sealed partial class GeneratedProjectionParityContract
{
    public GeneratedProjectionParityViolation Evaluate(GeneratedProjectionParityRequest request)
    {
        if (request.SourceDescriptor is null ||
            !request.SourceDescriptor.HasDescriptor ||
            !request.SourceDescriptor.IsValidated)
        {
            return GeneratedProjectionParityViolation.SourceNotDescriptorOwned;
        }

        if (request.CarriesHostOwnedEvidence)
        {
            return GeneratedProjectionParityViolation.HostEvidenceProjectionAttempt;
        }

        if (!request.IsGeneratedCompatibilityProjection)
        {
            return GeneratedProjectionParityViolation.MissingGeneratedProjectionMarker;
        }

        if (request.ProjectionSourceHash != request.SourceDescriptor.DescriptorHash)
        {
            return GeneratedProjectionParityViolation.SourceHashMismatch;
        }

        if (request.ProjectionArtifactHash == 0)
        {
            return GeneratedProjectionParityViolation.ProjectionArtifactHashMissing;
        }

        return GeneratedProjectionParityViolation.None;
    }

    public bool IsSatisfied(GeneratedProjectionParityRequest request) =>
        Evaluate(request) == GeneratedProjectionParityViolation.None;
}
