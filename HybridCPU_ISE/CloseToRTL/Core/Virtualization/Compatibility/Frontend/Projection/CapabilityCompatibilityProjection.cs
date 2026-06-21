using System;

namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class CapabilityDescriptorSet
{
    public CapabilityDescriptorSet(
        ulong globalHardwareCaps,
        ulong runtimeEnabledCaps,
        ulong domainGrantedCaps)
        : this(CapabilityGrantCollection.FromCompatibilityMasks(
            globalHardwareCaps,
            runtimeEnabledCaps,
            domainGrantedCaps,
            CapabilityDescriptorSetSchema.VmxCompatibility))
    {
    }

    public CapabilityDescriptorSet WithRuntimeEnabledCaps(ulong runtimeEnabledCaps) =>
        new(GlobalHardwareCaps, runtimeEnabledCaps, DomainGrantedCaps);

    public CapabilityDescriptorSet WithDomainGrantedCaps(ulong domainGrantedCaps) =>
        new(GlobalHardwareCaps, RuntimeEnabledCaps, domainGrantedCaps);
}

public sealed partial class CapabilityGrantCollection
{
    public static CapabilityGrantCollection FromCompatibilityMasks(
        ulong globalHardwareCaps,
        ulong runtimeEnabledCaps,
        ulong domainGrantedCaps,
        CapabilityDescriptorSetSchema schema,
        ulong ownerDomainId = CapabilityGrant.DefaultRuntimeOwnerDomainId)
    {
        schema ??= CapabilityDescriptorSetSchema.VmxCompatibility;
        ulong effectiveCaps =
            globalHardwareCaps &
            runtimeEnabledCaps &
            domainGrantedCaps &
            schema.CompatibilityPublicationMask;

        CapabilityGrant[] grants = new CapabilityGrant[CapabilityDescriptorSetSchema.VmxCompatibilityBits.Length * 4];
        int count = 0;

        foreach (CapabilityBitSchemaEntry entry in CapabilityDescriptorSetSchema.VmxCompatibilityBits)
        {
            if ((globalHardwareCaps & entry.CapabilityMask) == entry.CapabilityMask)
            {
                grants[count++] = CreateInternalGrant(
                    entry.CapabilityMask,
                    CapabilityGrantScope.HardwareAvailable,
                    ownerDomainId);
            }

            if ((runtimeEnabledCaps & entry.CapabilityMask) == entry.CapabilityMask)
            {
                grants[count++] = CreateInternalGrant(
                    entry.CapabilityMask,
                    CapabilityGrantScope.RuntimeEnabled,
                    ownerDomainId);
            }

            if ((domainGrantedCaps & entry.CapabilityMask) == entry.CapabilityMask)
            {
                grants[count++] = CreateInternalGrant(
                    entry.CapabilityMask,
                    CapabilityGrantScope.DomainGranted,
                    ownerDomainId);
            }

            if ((effectiveCaps & entry.CapabilityMask) == entry.CapabilityMask)
            {
                grants[count++] = new CapabilityGrant(
                    entry.CapabilityMask,
                    entry.ProjectionScope,
                    isGranted: true,
                    ownerDomainId,
                    CapabilityDelegationPolicy.NonDelegable,
                    CapabilityRevocationPolicy.RuntimeRevocable,
                    CapabilityMigrationClass.DomainLocal,
                    entry.EvidenceVisibility,
                    entry.FrontendProjectionPolicy);
            }
        }

        if (count == grants.Length)
        {
            return new CapabilityGrantCollection(grants);
        }

        Array.Resize(ref grants, count);
        return new CapabilityGrantCollection(grants);
    }
}
