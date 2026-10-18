namespace YAKSys_Hybrid_CPU.Core;

public enum VmxCapsWriteResult : byte
{
    Rejected = 0,
    CompatibilityNoEffect = 1,
}

public sealed partial class VmxCapsProjection
{
    public VmxCapsProjection()
        : this(
            CapabilityPublicationPolicy.ReadOnlyCompatibilityAlias,
            CapabilityDescriptorSetSchema.VmxCompatibility)
    {
    }

    public VmxCapsProjection(CapabilityPublicationPolicy publicationPolicy)
        : this(publicationPolicy, CapabilityDescriptorSetSchema.VmxCompatibility)
    {
    }

    public VmxCapsProjection(
        CapabilityPublicationPolicy publicationPolicy,
        CapabilityDescriptorSetSchema schema)
        : this(
            publicationPolicy,
            schema,
            compatibilityWriteFenceEnabled: false)
    {
    }

    public VmxCapsProjection(
        CapabilityPublicationPolicy publicationPolicy,
        CapabilityDescriptorSetSchema schema,
        bool compatibilityWriteFenceEnabled)
    {
        PublicationPolicy = publicationPolicy ?? CapabilityPublicationPolicy.FailClosed;
        Schema = schema ?? new CapabilityDescriptorSetSchema();
        CompatibilityWriteFenceEnabled = compatibilityWriteFenceEnabled;
    }

    public CapabilityPublicationPolicy PublicationPolicy { get; }

    public CapabilityDescriptorSetSchema Schema { get; }

    public bool CompatibilityWriteFenceEnabled { get; }

    public ulong Read(CapabilityDescriptorSet descriptorSet) =>
        Schema.FilterCompatibilityCaps(
            PublicationPolicy.ProjectCompatibilityAlias(descriptorSet));

    public bool CanPublishCapability(
        CapabilityDescriptorSet descriptorSet,
        ulong capabilityMask) =>
        Schema.ContainsOnlyKnownCompatibilityCaps(capabilityMask) &&
        PublicationPolicy.CanPublishCapability(descriptorSet, capabilityMask);

    public VmxCapsWriteResult EvaluateWrite(ulong ignoredValue) =>
        PublicationPolicy.ShouldRejectCompatibilityWrite()
        || !CompatibilityWriteFenceEnabled
            ? VmxCapsWriteResult.Rejected
            : VmxCapsWriteResult.CompatibilityNoEffect;
}
