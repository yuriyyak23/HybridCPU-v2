namespace YAKSys_Hybrid_CPU.Core;

public enum CompatibilityCapsProjectionViolation : byte
{
    None = 0,
    MissingDescriptorSet = 1,
    ProjectionMismatch = 2,
    ProjectionBypassesTypedGrants = 3,
}

public sealed partial class CompatibilityCapsProjectionContract
{
    public CompatibilityCapsProjectionViolation Evaluate(
        CapabilityDescriptorSet descriptorSet,
        ulong projectedCompatibilityCaps)
    {
        if (descriptorSet is null)
        {
            return CompatibilityCapsProjectionViolation.MissingDescriptorSet;
        }

        ulong expectedProjection =
            descriptorSet.CompatibilityCapsProjection &
            CapabilityDescriptorSetSchema.VmxCompatibility.CompatibilityPublicationMask;

        if (projectedCompatibilityCaps != expectedProjection)
        {
            return CompatibilityCapsProjectionViolation.ProjectionMismatch;
        }

        if (expectedProjection != descriptorSet.TypedGrants.EffectiveCompatibilityMask)
        {
            return CompatibilityCapsProjectionViolation.ProjectionBypassesTypedGrants;
        }

        return CompatibilityCapsProjectionViolation.None;
    }

    public bool IsSatisfied(
        CapabilityDescriptorSet descriptorSet,
        ulong projectedCompatibilityCaps) =>
        Evaluate(descriptorSet, projectedCompatibilityCaps) ==
        CompatibilityCapsProjectionViolation.None;
}
