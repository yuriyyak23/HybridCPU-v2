namespace YAKSys_Hybrid_CPU.Core.Nested;

public enum NestedCapabilityPublicationContractViolation : byte
{
    None = 0,
    MissingCompatibilityProjection = 1,
    MissingTypedAuthority = 2,
    MissingProjectedNestedCapability = 3,
    RequestBypassesPublicationAuthority = 4,
}

public sealed partial class NestedCapabilityPublicationContract
{
    public NestedCapabilityPublicationContractViolation Evaluate(
        NestedEnablementRequest request)
    {
        NestedCapabilityPublication publication = request.CapabilityPublication;

        if (!publication.Projection.IsProjectionOnly)
        {
            return NestedCapabilityPublicationContractViolation.MissingCompatibilityProjection;
        }

        if (!publication.Requirement.IsTypedAuthority)
        {
            return NestedCapabilityPublicationContractViolation.MissingTypedAuthority;
        }

        if (!publication.CanPublishRequiredCapability)
        {
            return NestedCapabilityPublicationContractViolation.MissingProjectedNestedCapability;
        }

        if (!request.HasExplicitVmxCapability)
        {
            return NestedCapabilityPublicationContractViolation.RequestBypassesPublicationAuthority;
        }

        return NestedCapabilityPublicationContractViolation.None;
    }

    public bool IsSatisfied(NestedEnablementRequest request) =>
        Evaluate(request) == NestedCapabilityPublicationContractViolation.None;
}
