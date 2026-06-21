using YAKSys_Hybrid_CPU.Core.Nested;

namespace YAKSys_Hybrid_CPU.Core;

public enum CapabilityCallerMaskIngressAuthorityViolation : byte
{
    None = 0,
    NonTypedRequirementAccepted = 1,
    MissingTypedProjectionGrant = 2,
    CompatibilityAliasPublishedAsAuthority = 3,
    NestedCompatibilityAliasPublishedAsAuthority = 4,
    NestedCompatibilityMaskProofAccepted = 5,
    HostOwnedEvidenceExposedToCompatibilityProjection = 6,
}

public sealed partial class CapabilityCallerMaskIngressAuthorityRemovalContract
{
    public const string CapabilityBoundaryRequirementPath =
        "Core/Runtime/Capabilities/CapabilityBoundaryRequirement.cs";

    public const string RuntimeBoundaryAdmissionPath =
        "Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs";

    public const string DomainRuntimeContextPath =
        "Core/Runtime/Domains/Services/DomainRuntimeContext.cs";

    public const string DomainValidationResultPath =
        "Core/Runtime/Domains/Validation/DomainValidationResult.cs";

    public const string DomainRuntimeAuthorityPath =
        "Core/Runtime/Domains/Authority/DomainRuntimeAuthority.cs";

    public const string CapabilityPublicationPolicyPath =
        "Core/Runtime/Capabilities/Publication/CapabilityPublicationPolicy.cs";

    public const string NestedDomainControllerPath =
        "Core/VMX/Compatibility/Frontend/Projection/Nested/NestedDomainController.cs";

    public static readonly string[] AdmissionAuthorityPaths =
    {
        RuntimeBoundaryAdmissionPath,
        DomainRuntimeContextPath,
        DomainValidationResultPath,
        DomainRuntimeAuthorityPath,
    };

    public static readonly string[] ForbiddenAdmissionFallbackMarkers =
    {
        "HasEffectiveCapability(",
        "CompatibilityCapsProjection",
        "EffectiveCaps",
    };

    public static readonly string[] ForbiddenNestedCallerPressureMarkers =
    {
        "PublishedVmxCaps",
        "PublishedCapabilityWord",
        "FromCompatibilityMasks(",
    };

    public static readonly string[] RequiredNestedTypedProjectionMarkers =
    {
        "CompatibilityProjectionWord",
        "FromTypedDescriptorProofs",
        "FromTypedGrant(OwnerDomainId)",
    };

    public CapabilityCallerMaskIngressAuthorityViolation Evaluate(
        CapabilityDescriptorSet descriptorSet,
        ulong capabilityMask,
        ulong ownerDomainId)
    {
        var nonTypedRequirement = new CapabilityBoundaryRequirement(
            capabilityMask,
            CapabilityGrantScope.CompatibilityProjection,
            RequiresTypedGrant: false);

        if (nonTypedRequirement.IsSatisfiedBy(descriptorSet))
        {
            return CapabilityCallerMaskIngressAuthorityViolation.NonTypedRequirementAccepted;
        }

        var typedRequirement = CapabilityBoundaryRequirement.TypedGrant(
            capabilityMask,
            CapabilityGrantScope.CompatibilityProjection);

        if (!typedRequirement.IsSatisfiedBy(descriptorSet))
        {
            return CapabilityCallerMaskIngressAuthorityViolation.MissingTypedProjectionGrant;
        }

        var publicationPolicy = CapabilityPublicationPolicy.ReadOnlyCompatibilityAlias;
        if (!publicationPolicy.CanPublishCapability(descriptorSet, capabilityMask))
        {
            return CapabilityCallerMaskIngressAuthorityViolation.CompatibilityAliasPublishedAsAuthority;
        }

        if (!descriptorSet.TypedGrants.TryGetGrant(
                capabilityMask,
                CapabilityGrantScope.CompatibilityProjection,
                out CapabilityGrant projectionGrant) ||
            projectionGrant.EvidenceVisibility != CapabilityEvidenceVisibility.GuestVisibleProjection)
        {
            return CapabilityCallerMaskIngressAuthorityViolation.HostOwnedEvidenceExposedToCompatibilityProjection;
        }

        NestedCapabilityPublication compatibilityAliasPublication =
            NestedCapabilityPublication.FromCompatibilityAlias(
                capabilityMask,
                ownerDomainId);

        if (compatibilityAliasPublication.CanPublishRequiredCapability)
        {
            return CapabilityCallerMaskIngressAuthorityViolation.NestedCompatibilityAliasPublishedAsAuthority;
        }

        NestedCapabilityPublication typedNestedPublication =
            NestedCapabilityPublication.FromTypedGrant(ownerDomainId);
        if (!typedNestedPublication.CanPublishRequiredCapability)
        {
            return CapabilityCallerMaskIngressAuthorityViolation.MissingTypedProjectionGrant;
        }

        if (typedNestedPublication.Requirement.RequiredGrant.EvidenceVisibility !=
            CapabilityEvidenceVisibility.GuestVisibleProjection)
        {
            return CapabilityCallerMaskIngressAuthorityViolation.HostOwnedEvidenceExposedToCompatibilityProjection;
        }

        NestedEnablementProof failClosedMaskProof =
            NestedEnablementProof.FromTypedDescriptorProofs(
                NestedCapabilityGrantMask.RequiredForPhase7,
                NestedEnablementGate.RequiredForPhase7,
                NestedProofAuthoritySource.FailClosed);

        return failClosedMaskProof.IsAuthoritative
            ? CapabilityCallerMaskIngressAuthorityViolation.NestedCompatibilityMaskProofAccepted
            : CapabilityCallerMaskIngressAuthorityViolation.None;
    }

    public bool IsSatisfied(
        CapabilityDescriptorSet descriptorSet,
        ulong capabilityMask,
        ulong ownerDomainId) =>
        Evaluate(descriptorSet, capabilityMask, ownerDomainId) ==
        CapabilityCallerMaskIngressAuthorityViolation.None;
}
