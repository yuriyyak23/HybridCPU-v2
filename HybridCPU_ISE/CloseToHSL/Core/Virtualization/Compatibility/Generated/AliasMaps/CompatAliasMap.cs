using System;

namespace YAKSys_Hybrid_CPU.Core;

public enum CompatAliasSourceKind : byte
{
    Opcode = 0,
    Csr = 1,
    VmcsField = 2,
    Completion = 3,
    FunctionLeaf = 4,
}

public enum CompatAliasTargetKind : byte
{
    DomainRuntimeOperation = 0,
    DescriptorField = 1,
    CapabilityGrant = 2,
    CompletionRecord = 3,
    CompatibilityProjection = 4,
}

public readonly record struct CompatAliasMapEntry(
    CompatAliasSourceKind SourceKind,
    string SourceName,
    CompatAliasTargetKind TargetKind,
    string TargetName,
    bool IsFrozenAbi);

public readonly record struct CompatAliasSchemaArtifact(
    string SchemaName,
    int SchemaVersion,
    ulong SourceHash,
    ulong ArtifactHash,
    int EntryCount,
    bool IsCanonical);

public sealed partial class CompatAliasMap
{
    private const ulong CanonicalSchemaHash = 0x8B55_62D0_F19A_4C31UL;
    private const ulong GeneratedArtifactHash = 0xB0A7_11A5_95E4_2227UL;

    private static readonly CompatAliasMapEntry[] EntryTable = new CompatAliasMapEntry[]
    {
        new(CompatAliasSourceKind.Opcode, "VMXON", CompatAliasTargetKind.DomainRuntimeOperation, "ActivateCompatibilityFrontend", true),
        new(CompatAliasSourceKind.Opcode, "VMXOFF", CompatAliasTargetKind.DomainRuntimeOperation, "DeactivateCompatibilityFrontend", true),
        new(CompatAliasSourceKind.Opcode, "VMREAD", CompatAliasTargetKind.CompatibilityProjection, "VmcsFieldAliasProjection.Read", true),
        new(CompatAliasSourceKind.Opcode, "VMWRITE", CompatAliasTargetKind.CompatibilityProjection, "VmcsFieldAliasProjection.Write", true),
        new(CompatAliasSourceKind.Opcode, "VMCALL", CompatAliasTargetKind.CompatibilityProjection, "VmxTrapProjectionMapper.Project", true),
        new(CompatAliasSourceKind.Csr, "VmxCaps", CompatAliasTargetKind.CapabilityGrant, "CapabilityDescriptorSet", true),
        new(CompatAliasSourceKind.Csr, "VmxExitReason", CompatAliasTargetKind.CompletionRecord, "CompletionRecord.ExitReason", true),
        new(CompatAliasSourceKind.Csr, "VmxExitQual", CompatAliasTargetKind.CompletionRecord, "CompletionRecord.ExitQualification", true),
        new(CompatAliasSourceKind.VmcsField, "VmcsField", CompatAliasTargetKind.DescriptorField, "GeneratedVmcsProjection", true),
        new(CompatAliasSourceKind.Completion, "VmxCompletion", CompatAliasTargetKind.CompletionRecord, "CompletionProjectionService", true),
        new(CompatAliasSourceKind.FunctionLeaf, "VmxFunctionLeaf", CompatAliasTargetKind.CapabilityGrant, "CapabilityNegotiationService", true),
    };

    public static ReadOnlySpan<CompatAliasMapEntry> Entries => EntryTable;

    public static CompatAliasSchemaArtifact CanonicalSchema { get; } = new(
        "compat-alias-schema",
        SchemaVersion: 1,
        CanonicalSchemaHash,
        GeneratedArtifactHash,
        EntryTable.Length,
        IsCanonical: true);

    public bool MatchesCanonicalSchema(CompatAliasSchemaArtifact artifact) =>
        artifact.IsCanonical &&
        artifact.SchemaVersion == CanonicalSchema.SchemaVersion &&
        artifact.SourceHash == CanonicalSchema.SourceHash &&
        artifact.ArtifactHash == CanonicalSchema.ArtifactHash &&
        artifact.EntryCount == EntryTable.Length;

    public bool ContainsSource(CompatAliasSourceKind sourceKind, string sourceName)
    {
        return TryGetEntry(sourceKind, sourceName, out _);
    }

    public bool TryGetEntry(
        CompatAliasSourceKind sourceKind,
        string sourceName,
        out CompatAliasMapEntry entry)
    {
        foreach (var candidate in Entries)
        {
            if (candidate.SourceKind == sourceKind
                && string.Equals(candidate.SourceName, sourceName, StringComparison.Ordinal))
            {
                entry = candidate;
                return true;
            }
        }

        entry = default;
        return false;
    }

    public bool IsFrozenAbiSource(CompatAliasSourceKind sourceKind, string sourceName)
    {
        return TryGetEntry(sourceKind, sourceName, out var entry) && entry.IsFrozenAbi;
    }
}
