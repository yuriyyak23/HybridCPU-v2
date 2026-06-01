using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core;

public readonly record struct CapabilityBitSchemaEntry(
    string Name,
    ulong CapabilityMask,
    CapabilityGrantScope ProjectionScope,
    string TypedGrantSource,
    CapabilityFrontendProjectionPolicy FrontendProjectionPolicy,
    CapabilityEvidenceVisibility EvidenceVisibility);

public readonly record struct CapabilityBitSchemaArtifact(
    string SchemaName,
    int SchemaVersion,
    ulong SourceHash,
    ulong ArtifactHash,
    ulong CompatibilityPublicationMask,
    int EntryCount,
    bool IsCanonical);

public sealed partial class CapabilityDescriptorSetSchema
{
    public CapabilityDescriptorSetSchema()
        : this(compatibilityPublicationMask: 0)
    {
    }

    public CapabilityDescriptorSetSchema(ulong compatibilityPublicationMask)
    {
        CompatibilityPublicationMask = compatibilityPublicationMask;
    }

    public const int CurrentVersion = 1;

    public static ulong KnownVmxV2CompatibilityMask =>
        VmxV2InstructionCaps.VmPtrSt |
        VmxV2InstructionCaps.VmCall |
        VmxV2InstructionCaps.Invept |
        VmxV2InstructionCaps.Invvpid |
        VmxV2InstructionCaps.VmFunc |
        VmxV2InstructionCaps.RootDescriptorOperand |
        VmxV2InstructionCaps.VmSaveX |
        VmxV2InstructionCaps.VmRestX |
        VmxV2InstructionCaps.NestedVmx;

    private static readonly CapabilityBitSchemaEntry[] VmxCompatibilityBitTable =
    {
        new("VmPtrSt", VmxV2InstructionCaps.VmPtrSt, CapabilityGrantScope.CompatibilityProjection, "CapabilityGrantCollection.TypedGrant", CapabilityFrontendProjectionPolicy.ProjectIfCompatible, CapabilityEvidenceVisibility.GuestVisibleProjection),
        new("VmCall", VmxV2InstructionCaps.VmCall, CapabilityGrantScope.CompatibilityProjection, "CapabilityGrantCollection.TypedGrant", CapabilityFrontendProjectionPolicy.ProjectIfCompatible, CapabilityEvidenceVisibility.GuestVisibleProjection),
        new("Invept", VmxV2InstructionCaps.Invept, CapabilityGrantScope.CompatibilityProjection, "CapabilityGrantCollection.TypedGrant", CapabilityFrontendProjectionPolicy.ProjectIfCompatible, CapabilityEvidenceVisibility.GuestVisibleProjection),
        new("Invvpid", VmxV2InstructionCaps.Invvpid, CapabilityGrantScope.CompatibilityProjection, "CapabilityGrantCollection.TypedGrant", CapabilityFrontendProjectionPolicy.ProjectIfCompatible, CapabilityEvidenceVisibility.GuestVisibleProjection),
        new("VmFunc", VmxV2InstructionCaps.VmFunc, CapabilityGrantScope.CompatibilityProjection, "CapabilityGrantCollection.TypedGrant", CapabilityFrontendProjectionPolicy.ProjectIfCompatible, CapabilityEvidenceVisibility.GuestVisibleProjection),
        new("RootDescriptorOperand", VmxV2InstructionCaps.RootDescriptorOperand, CapabilityGrantScope.CompatibilityProjection, "CapabilityGrantCollection.TypedGrant", CapabilityFrontendProjectionPolicy.ProjectIfCompatible, CapabilityEvidenceVisibility.GuestVisibleProjection),
        new("VmSaveX", VmxV2InstructionCaps.VmSaveX, CapabilityGrantScope.CompatibilityProjection, "CapabilityGrantCollection.TypedGrant", CapabilityFrontendProjectionPolicy.ProjectIfCompatible, CapabilityEvidenceVisibility.GuestVisibleProjection),
        new("VmRestX", VmxV2InstructionCaps.VmRestX, CapabilityGrantScope.CompatibilityProjection, "CapabilityGrantCollection.TypedGrant", CapabilityFrontendProjectionPolicy.ProjectIfCompatible, CapabilityEvidenceVisibility.GuestVisibleProjection),
        new("NestedVmx", VmxV2InstructionCaps.NestedVmx, CapabilityGrantScope.CompatibilityProjection, "NestedDomainCapability.TypedGrant", CapabilityFrontendProjectionPolicy.ProjectIfCompatible, CapabilityEvidenceVisibility.GuestVisibleProjection),
    };

    public static CapabilityDescriptorSetSchema VmxCompatibility { get; } =
        new(KnownVmxV2CompatibilityMask);

    public static ReadOnlySpan<CapabilityBitSchemaEntry> VmxCompatibilityBits =>
        VmxCompatibilityBitTable;

    public static CapabilityBitSchemaArtifact VmxCompatibilityBitSchema { get; } = new(
        "vmxcaps-capability-bit-schema",
        CurrentVersion,
        SourceHash: 0x6A4D_2C78_1B90_E317UL,
        ArtifactHash: 0xB781_C25E_9D4A_603FUL,
        KnownVmxV2CompatibilityMask,
        VmxCompatibilityBitTable.Length,
        IsCanonical: true);

    public ulong CompatibilityPublicationMask { get; }

    public ulong FilterCompatibilityCaps(ulong capabilityMask) =>
        capabilityMask & CompatibilityPublicationMask;

    public ulong GetUnknownCompatibilityCaps(ulong capabilityMask) =>
        capabilityMask & ~CompatibilityPublicationMask;

    public bool ContainsOnlyKnownCompatibilityCaps(ulong capabilityMask) =>
        GetUnknownCompatibilityCaps(capabilityMask) == 0;

    public bool MatchesCanonicalVmxCompatibilityBitSchema(
        CapabilityBitSchemaArtifact artifact)
    {
        if (!artifact.IsCanonical ||
            artifact.SchemaVersion != CurrentVersion ||
            artifact.SourceHash == 0 ||
            artifact.ArtifactHash == 0 ||
            artifact.CompatibilityPublicationMask != CompatibilityPublicationMask ||
            artifact.EntryCount != VmxCompatibilityBitTable.Length)
        {
            return false;
        }

        ulong declaredMask = 0;
        var seenMasks = new HashSet<ulong>();

        foreach (var entry in VmxCompatibilityBits)
        {
            if (string.IsNullOrWhiteSpace(entry.Name) ||
                string.IsNullOrWhiteSpace(entry.TypedGrantSource) ||
                entry.CapabilityMask == 0 ||
                entry.ProjectionScope != CapabilityGrantScope.CompatibilityProjection ||
                entry.FrontendProjectionPolicy != CapabilityFrontendProjectionPolicy.ProjectIfCompatible ||
                entry.EvidenceVisibility != CapabilityEvidenceVisibility.GuestVisibleProjection ||
                !seenMasks.Add(entry.CapabilityMask))
            {
                return false;
            }

            declaredMask |= entry.CapabilityMask;
        }

        return declaredMask == artifact.CompatibilityPublicationMask;
    }
}
