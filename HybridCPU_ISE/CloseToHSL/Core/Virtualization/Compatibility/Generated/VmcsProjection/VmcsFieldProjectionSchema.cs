using System;

namespace YAKSys_Hybrid_CPU.Core;

public enum VmcsFieldProjectionOwner : byte
{
    ExecutionDomainDescriptor = 0,
    MemoryDomainDescriptor = 1,
    CompletionRecord = 2,
    CompatibilityControlDescriptor = 3,
}

public enum VmcsFieldProjectionAccessPolicy : byte
{
    Denied = 0,
    ReadOnly = 1,
    ReadWrite = 2,
}

public enum VmcsFieldProjectionMigrationPolicy : byte
{
    None = 0,
    DescriptorOwned = 1,
    RecomputedCompletion = 2,
    ProjectionOnly = 3,
}

public readonly record struct VmcsFieldProjectionSchemaEntry(
    VmcsField Field,
    VmcsFieldProjectionOwner Owner,
    EvidenceVisibilityClass EvidenceClass,
    VmcsFieldProjectionAccessPolicy AccessPolicy,
    VmcsFieldProjectionMigrationPolicy MigrationPolicy,
    bool IsGeneratedAlias);

public readonly record struct VmcsFieldProjectionSchemaArtifact(
    string SchemaName,
    int SchemaVersion,
    ulong SourceHash,
    ulong ArtifactHash,
    int EntryCount,
    bool IsCanonical);

public static class VmcsFieldProjectionSchema
{
    public const int CurrentVersion = 1;

    private static readonly VmcsFieldProjectionSchemaEntry[] EntryTable = new VmcsFieldProjectionSchemaEntry[]
    {
        new(VmcsField.GuestPc, VmcsFieldProjectionOwner.ExecutionDomainDescriptor, EvidenceVisibilityClass.GuestArchitecturalState, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.DescriptorOwned, true),
        new(VmcsField.GuestSp, VmcsFieldProjectionOwner.ExecutionDomainDescriptor, EvidenceVisibilityClass.GuestArchitecturalState, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.DescriptorOwned, true),
        new(VmcsField.GuestFlags, VmcsFieldProjectionOwner.ExecutionDomainDescriptor, EvidenceVisibilityClass.GuestArchitecturalState, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.DescriptorOwned, true),
        new(VmcsField.GuestCr0, VmcsFieldProjectionOwner.ExecutionDomainDescriptor, EvidenceVisibilityClass.GuestArchitecturalState, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.DescriptorOwned, true),
        new(VmcsField.GuestCr3, VmcsFieldProjectionOwner.MemoryDomainDescriptor, EvidenceVisibilityClass.GuestArchitecturalState, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.DescriptorOwned, true),
        new(VmcsField.GuestCr4, VmcsFieldProjectionOwner.ExecutionDomainDescriptor, EvidenceVisibilityClass.GuestArchitecturalState, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.DescriptorOwned, true),
        new(VmcsField.HostPc, VmcsFieldProjectionOwner.ExecutionDomainDescriptor, EvidenceVisibilityClass.CompatibilityAlias, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.ProjectionOnly, true),
        new(VmcsField.HostSp, VmcsFieldProjectionOwner.ExecutionDomainDescriptor, EvidenceVisibilityClass.CompatibilityAlias, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.ProjectionOnly, true),
        new(VmcsField.HostFlags, VmcsFieldProjectionOwner.ExecutionDomainDescriptor, EvidenceVisibilityClass.CompatibilityAlias, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.ProjectionOnly, true),
        new(VmcsField.HostCr0, VmcsFieldProjectionOwner.ExecutionDomainDescriptor, EvidenceVisibilityClass.CompatibilityAlias, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.ProjectionOnly, true),
        new(VmcsField.HostCr3, VmcsFieldProjectionOwner.MemoryDomainDescriptor, EvidenceVisibilityClass.CompatibilityAlias, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.ProjectionOnly, true),
        new(VmcsField.PinBasedControls, VmcsFieldProjectionOwner.CompatibilityControlDescriptor, EvidenceVisibilityClass.CompatibilityAlias, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.ProjectionOnly, true),
        new(VmcsField.ProcBasedControls, VmcsFieldProjectionOwner.CompatibilityControlDescriptor, EvidenceVisibilityClass.CompatibilityAlias, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.ProjectionOnly, true),
        new(VmcsField.ExitControls, VmcsFieldProjectionOwner.CompatibilityControlDescriptor, EvidenceVisibilityClass.CompatibilityAlias, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.ProjectionOnly, true),
        new(VmcsField.EntryControls, VmcsFieldProjectionOwner.CompatibilityControlDescriptor, EvidenceVisibilityClass.CompatibilityAlias, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.ProjectionOnly, true),
        new(VmcsField.EptPointer, VmcsFieldProjectionOwner.MemoryDomainDescriptor, EvidenceVisibilityClass.CompatibilityAlias, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.DescriptorOwned, true),
        new(VmcsField.Vpid, VmcsFieldProjectionOwner.MemoryDomainDescriptor, EvidenceVisibilityClass.CompatibilityAlias, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.DescriptorOwned, true),
        new(VmcsField.SecondaryProcControls, VmcsFieldProjectionOwner.CompatibilityControlDescriptor, EvidenceVisibilityClass.CompatibilityAlias, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.ProjectionOnly, true),
        new(VmcsField.Cr3TargetCount, VmcsFieldProjectionOwner.MemoryDomainDescriptor, EvidenceVisibilityClass.CompatibilityAlias, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.DescriptorOwned, true),
        new(VmcsField.ExitReason, VmcsFieldProjectionOwner.CompletionRecord, EvidenceVisibilityClass.CompatibilityAlias, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.RecomputedCompletion, true),
        new(VmcsField.ExitQualification, VmcsFieldProjectionOwner.CompletionRecord, EvidenceVisibilityClass.CompatibilityAlias, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.RecomputedCompletion, true),
        new(VmcsField.GuestPhysicalAddress, VmcsFieldProjectionOwner.CompletionRecord, EvidenceVisibilityClass.CompatibilityAlias, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.RecomputedCompletion, true),
        new(VmcsField.EptViolationQualification, VmcsFieldProjectionOwner.CompletionRecord, EvidenceVisibilityClass.CompatibilityAlias, VmcsFieldProjectionAccessPolicy.ReadOnly, VmcsFieldProjectionMigrationPolicy.RecomputedCompletion, true),
    };

    public static ReadOnlySpan<VmcsFieldProjectionSchemaEntry> Entries => EntryTable;

    public static VmcsFieldProjectionSchemaArtifact CanonicalArtifact { get; } = new(
        "vmcs-field-projection-schema",
        CurrentVersion,
        SourceHash: 0xC71D_41B2_0A6F_9E23UL,
        ArtifactHash: 0xA99E_512C_4F80_73D1UL,
        EntryTable.Length,
        IsCanonical: true);

    public static bool TryGet(VmcsField field, out VmcsFieldProjectionSchemaEntry entry)
    {
        foreach (var candidate in Entries)
        {
            if (candidate.Field == field)
            {
                entry = candidate;
                return true;
            }
        }

        entry = default;
        return false;
    }

    public static bool CanRead(VmcsFieldProjectionSchemaEntry entry) =>
        entry.AccessPolicy == VmcsFieldProjectionAccessPolicy.ReadOnly ||
        entry.AccessPolicy == VmcsFieldProjectionAccessPolicy.ReadWrite;

    public static bool CanWrite(VmcsFieldProjectionSchemaEntry entry) => false;
}
