namespace YAKSys_Hybrid_CPU.Core;

public enum VmxCapsBitSchemaConformanceViolation : byte
{
    None = 0,
    MissingSchema = 1,
    MissingCanonicalArtifact = 2,
    SchemaMismatch = 3,
    MissingTypedGrantSource = 4,
    MissingProjectionPolicy = 5,
    HostOnlyEvidenceProjection = 6,
    DescriptorSetClaimedAsTypedGrantSource = 7,
}

public sealed partial class VmxCapsBitSchemaConformanceContract
{
    public VmxCapsBitSchemaConformanceViolation Evaluate(
        CapabilityDescriptorSetSchema? schema,
        CapabilityBitSchemaArtifact artifact)
    {
        if (schema is null)
        {
            return VmxCapsBitSchemaConformanceViolation.MissingSchema;
        }

        if (!artifact.IsCanonical ||
            artifact.EntryCount == 0 ||
            artifact.SourceHash == 0 ||
            artifact.ArtifactHash == 0)
        {
            return VmxCapsBitSchemaConformanceViolation.MissingCanonicalArtifact;
        }

        if (!schema.MatchesCanonicalVmxCompatibilityBitSchema(artifact))
        {
            return VmxCapsBitSchemaConformanceViolation.SchemaMismatch;
        }

        foreach (CapabilityBitSchemaEntry entry in CapabilityDescriptorSetSchema.VmxCompatibilityBits)
        {
            if (string.IsNullOrWhiteSpace(entry.TypedGrantSource) ||
                !entry.TypedGrantSource.Contains("TypedGrant", System.StringComparison.Ordinal) &&
                !entry.TypedGrantSource.Contains("TypedGrants", System.StringComparison.Ordinal))
            {
                return VmxCapsBitSchemaConformanceViolation.MissingTypedGrantSource;
            }

            if (entry.TypedGrantSource.StartsWith(
                    "CapabilityDescriptorSet.",
                    System.StringComparison.Ordinal))
            {
                return VmxCapsBitSchemaConformanceViolation.DescriptorSetClaimedAsTypedGrantSource;
            }

            if (entry.FrontendProjectionPolicy != CapabilityFrontendProjectionPolicy.ProjectIfCompatible)
            {
                return VmxCapsBitSchemaConformanceViolation.MissingProjectionPolicy;
            }

            if (entry.EvidenceVisibility == CapabilityEvidenceVisibility.HostOnly)
            {
                return VmxCapsBitSchemaConformanceViolation.HostOnlyEvidenceProjection;
            }
        }

        return VmxCapsBitSchemaConformanceViolation.None;
    }

    public bool IsSatisfied(
        CapabilityDescriptorSetSchema? schema,
        CapabilityBitSchemaArtifact artifact) =>
        Evaluate(schema, artifact) == VmxCapsBitSchemaConformanceViolation.None;

    public bool IsCurrentSchemaSatisfied() =>
        IsSatisfied(
            CapabilityDescriptorSetSchema.VmxCompatibility,
            CapabilityDescriptorSetSchema.VmxCompatibilityBitSchema);
}
