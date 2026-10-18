namespace YAKSys_Hybrid_CPU.Core;

public enum CapabilityGrantCollectionAuthorityViolation : byte
{
    None = 0,
    MissingCollection = 1,
    EmptyCollectionForEffectiveCaps = 2,
    MissingTypedAuthority = 3,
    EffectiveMaskMismatch = 4,
}

public sealed partial class CapabilityGrantCollectionAuthorityContract
{
    private readonly CapabilityGrantAuthorityContract _grantAuthority = new();

    public CapabilityGrantCollectionAuthorityViolation Evaluate(
        CapabilityDescriptorSet descriptorSet)
    {
        if (descriptorSet is null)
        {
            return CapabilityGrantCollectionAuthorityViolation.MissingCollection;
        }

        CapabilityGrantCollection collection = descriptorSet.TypedGrants;
        ulong projectedEffectiveMask =
            descriptorSet.CompatibilityCapsProjection &
            CapabilityDescriptorSetSchema.VmxCompatibility.CompatibilityPublicationMask;

        if (projectedEffectiveMask != 0 && collection.Count == 0)
        {
            return CapabilityGrantCollectionAuthorityViolation.EmptyCollectionForEffectiveCaps;
        }

        foreach (CapabilityGrant grant in collection.Grants)
        {
            if (!_grantAuthority.IsSatisfied(grant))
            {
                return CapabilityGrantCollectionAuthorityViolation.MissingTypedAuthority;
            }
        }

        return collection.EffectiveCompatibilityMask == projectedEffectiveMask
            ? CapabilityGrantCollectionAuthorityViolation.None
            : CapabilityGrantCollectionAuthorityViolation.EffectiveMaskMismatch;
    }

    public bool IsSatisfied(CapabilityDescriptorSet descriptorSet) =>
        Evaluate(descriptorSet) == CapabilityGrantCollectionAuthorityViolation.None;
}
