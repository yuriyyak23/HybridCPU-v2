namespace YAKSys_Hybrid_CPU.Core;

public enum CapabilityDescriptorSetGrantFirstAuthorityViolation : byte
{
    None = 0,
    MissingDescriptorSet = 1,
    HardwareProjectionBypassesTypedGrants = 2,
    RuntimeProjectionBypassesTypedGrants = 3,
    DomainProjectionBypassesTypedGrants = 4,
    CompatibilityProjectionBypassesTypedGrants = 5,
    EffectiveCapabilityBypassesTypedGrant = 6,
    CompatibilitySchemaClaimsDescriptorSetAuthority = 7,
}

public sealed partial class CapabilityDescriptorSetGrantFirstAuthorityContract
{
    public const string DescriptorSetPath =
        "Core/Runtime/Capabilities/Descriptors/CapabilityDescriptorSet.cs";

    public const string GrantCollectionPath =
        "Core/Runtime/Capabilities/Grants/CapabilityGrant.cs";

    public const string CompatibilityProjectionPath =
        "Core/VMX/Compatibility/Frontend/Projection/CapabilityCompatibilityProjection.cs";

    public static readonly string[] ForbiddenDescriptorBackingMarkers =
    {
        "GlobalHardwareCaps = globalHardwareCaps",
        "RuntimeEnabledCaps = runtimeEnabledCaps",
        "DomainGrantedCaps = domainGrantedCaps",
        "GlobalHardwareCaps &",
        "RuntimeEnabledCaps &",
        "DomainGrantedCaps;",
        "private readonly ulong _global",
        "private readonly ulong _runtime",
        "private readonly ulong _domain",
    };

    public static readonly string[] RequiredGrantFirstMarkers =
    {
        "public CapabilityDescriptorSet(CapabilityGrantCollection typedGrants)",
        "TypedGrants.ProjectMask(CapabilityGrantScope.HardwareAvailable)",
        "TypedGrants.ProjectMask(CapabilityGrantScope.RuntimeEnabled)",
        "TypedGrants.ProjectMask(CapabilityGrantScope.DomainGranted)",
        "TypedGrants.EffectiveCompatibilityMask",
        "HasTypedGrant(capabilityMask, CapabilityGrantScope.CompatibilityProjection)",
    };

    public static readonly string[] ForbiddenGrantCollectionMarkers =
    {
        "FromMasks(",
        "FromCompatibilityMasks(",
        "CapabilityDescriptorSetSchema",
        "VmxCompatibility",
        "VmxCompatibilityBits",
    };

    public CapabilityDescriptorSetGrantFirstAuthorityViolation Evaluate(
        CapabilityDescriptorSet descriptorSet,
        ulong capabilityMask)
    {
        if (descriptorSet is null)
        {
            return CapabilityDescriptorSetGrantFirstAuthorityViolation.MissingDescriptorSet;
        }

        if (descriptorSet.GlobalHardwareCaps !=
            descriptorSet.TypedGrants.ProjectMask(CapabilityGrantScope.HardwareAvailable))
        {
            return CapabilityDescriptorSetGrantFirstAuthorityViolation.HardwareProjectionBypassesTypedGrants;
        }

        if (descriptorSet.RuntimeEnabledCaps !=
            descriptorSet.TypedGrants.ProjectMask(CapabilityGrantScope.RuntimeEnabled))
        {
            return CapabilityDescriptorSetGrantFirstAuthorityViolation.RuntimeProjectionBypassesTypedGrants;
        }

        if (descriptorSet.DomainGrantedCaps !=
            descriptorSet.TypedGrants.ProjectMask(CapabilityGrantScope.DomainGranted))
        {
            return CapabilityDescriptorSetGrantFirstAuthorityViolation.DomainProjectionBypassesTypedGrants;
        }

        if (descriptorSet.CompatibilityCapsProjection !=
            descriptorSet.TypedGrants.EffectiveCompatibilityMask)
        {
            return CapabilityDescriptorSetGrantFirstAuthorityViolation.CompatibilityProjectionBypassesTypedGrants;
        }

        if (capabilityMask != 0)
        {
            bool grantExists =
                descriptorSet.TypedGrants.TryGetGrant(
                    capabilityMask,
                    CapabilityGrantScope.CompatibilityProjection,
                    out CapabilityGrant grant) &&
                grant.HasTypedAuthority;

            if (descriptorSet.HasEffectiveCapability(capabilityMask) != grantExists)
            {
                return CapabilityDescriptorSetGrantFirstAuthorityViolation.EffectiveCapabilityBypassesTypedGrant;
            }
        }

        foreach (CapabilityBitSchemaEntry entry in CapabilityDescriptorSetSchema.VmxCompatibilityBits)
        {
            if (entry.TypedGrantSource.StartsWith(
                    "CapabilityDescriptorSet.",
                    System.StringComparison.Ordinal))
            {
                return CapabilityDescriptorSetGrantFirstAuthorityViolation.CompatibilitySchemaClaimsDescriptorSetAuthority;
            }
        }

        return CapabilityDescriptorSetGrantFirstAuthorityViolation.None;
    }

    public bool IsSatisfied(
        CapabilityDescriptorSet descriptorSet,
        ulong capabilityMask) =>
        Evaluate(descriptorSet, capabilityMask) ==
        CapabilityDescriptorSetGrantFirstAuthorityViolation.None;
}
