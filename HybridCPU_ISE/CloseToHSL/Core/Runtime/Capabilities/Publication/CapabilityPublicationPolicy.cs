namespace YAKSys_Hybrid_CPU.Core;

public enum CapabilityWriteDisposition : byte
{
    Reject = 0,
    CompatibilityNoEffect = 1,
}

public sealed partial class CapabilityPublicationPolicy
{
    public CapabilityPublicationPolicy()
        : this(
            allowCompatibilityAliasPublication: false,
            writeDisposition: CapabilityWriteDisposition.Reject)
    {
    }

    public CapabilityPublicationPolicy(
        bool allowCompatibilityAliasPublication,
        CapabilityWriteDisposition writeDisposition)
    {
        AllowCompatibilityAliasPublication = allowCompatibilityAliasPublication;
        WriteDisposition = writeDisposition;
    }

    public static CapabilityPublicationPolicy FailClosed { get; } = new();

    public static CapabilityPublicationPolicy ReadOnlyCompatibilityAlias { get; } =
        new(
            allowCompatibilityAliasPublication: true,
            writeDisposition: CapabilityWriteDisposition.Reject);

    public bool AllowCompatibilityAliasPublication { get; }

    public CapabilityWriteDisposition WriteDisposition { get; }

    public ulong ProjectCompatibilityAlias(CapabilityDescriptorSet descriptorSet)
    {
        if (!AllowCompatibilityAliasPublication || descriptorSet is null)
        {
            return 0;
        }

        return descriptorSet.CompatibilityCapsProjection;
    }

    public bool CanPublishCapability(
        CapabilityDescriptorSet descriptorSet,
        ulong capabilityMask) =>
        AllowCompatibilityAliasPublication &&
        descriptorSet is not null &&
        descriptorSet.TypedGrants.TryGetGrant(
            capabilityMask,
            CapabilityGrantScope.CompatibilityProjection,
            out CapabilityGrant grant) &&
        grant.HasTypedAuthority &&
        grant.IsPublishableCompatibilityGrant;

    public bool ShouldRejectCompatibilityWrite() =>
        WriteDisposition == CapabilityWriteDisposition.Reject;

    public bool IsCompatibilityWriteArchitecturallyNoEffect() =>
        WriteDisposition == CapabilityWriteDisposition.CompatibilityNoEffect;
}
