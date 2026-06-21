using System;

namespace YAKSys_Hybrid_CPU.Core;

public enum CompatSpecArtifactKind : byte
{
    FrozenOpcodeAliases = 0,
    FrozenCsrAliases = 1,
    VmcsFieldAliases = 2,
    CapabilityProjection = 3,
    CompletionProjection = 4,
    CompatAliasMap = 5,
}

public enum CompatSpecArtifactLineageKind : byte
{
    FrozenAbiVocabulary = 0,
    ExecutableGeneratedSource = 1,
    ProjectionContractOnly = 2,
}

public readonly record struct CompatSpecArtifact(
    CompatSpecArtifactKind Kind,
    string Name,
    string LineageSource,
    bool IsGenerated,
    bool IsFrozenAbi,
    CompatSpecArtifactLineageKind LineageKind);

public sealed partial class CompatSpecArtifactSet
{
    private static readonly CompatSpecArtifact[] ArtifactTable = new CompatSpecArtifact[]
    {
        new(CompatSpecArtifactKind.FrozenOpcodeAliases, "VmxOpcodeAliasSet", "FrozenOpcodeAbiVocabulary", false, true, CompatSpecArtifactLineageKind.FrozenAbiVocabulary),
        new(CompatSpecArtifactKind.FrozenCsrAliases, "VmxCsrAliasSet", "FrozenCsrAbiVocabulary", false, true, CompatSpecArtifactLineageKind.FrozenAbiVocabulary),
        new(CompatSpecArtifactKind.VmcsFieldAliases, "VmcsFieldAliasSet", "VmcsFieldProjectionSchema", true, true, CompatSpecArtifactLineageKind.ExecutableGeneratedSource),
        new(CompatSpecArtifactKind.CapabilityProjection, "VmxCapsProjection", "CapabilityDescriptorSetSchema", true, true, CompatSpecArtifactLineageKind.ExecutableGeneratedSource),
        new(CompatSpecArtifactKind.CompletionProjection, "CompletionProjectionService", "ProjectionContractOnly", false, true, CompatSpecArtifactLineageKind.ProjectionContractOnly),
        new(CompatSpecArtifactKind.CompatAliasMap, "CompatAliasMap", "CompatAliasMap", true, true, CompatSpecArtifactLineageKind.ExecutableGeneratedSource),
    };

    public static ReadOnlySpan<CompatSpecArtifact> Artifacts => ArtifactTable;

    public bool Contains(CompatSpecArtifactKind kind)
    {
        foreach (var artifact in Artifacts)
        {
            if (artifact.Kind == kind)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetArtifact(CompatSpecArtifactKind kind, out CompatSpecArtifact artifact)
    {
        foreach (var candidate in Artifacts)
        {
            if (candidate.Kind == kind)
            {
                artifact = candidate;
                return true;
            }
        }

        artifact = default;
        return false;
    }

    public bool HasExecutableLineage(CompatSpecArtifactKind kind)
    {
        return TryGetArtifact(kind, out var artifact) &&
            artifact.LineageKind == CompatSpecArtifactLineageKind.ExecutableGeneratedSource;
    }

    public bool RequiresGeneratedParity(CompatSpecArtifactKind kind)
    {
        return TryGetArtifact(kind, out var artifact) &&
            artifact.IsGenerated &&
            artifact.LineageKind == CompatSpecArtifactLineageKind.ExecutableGeneratedSource;
    }

    public bool RequiresAbiFreeze(CompatSpecArtifactKind kind)
    {
        return TryGetArtifact(kind, out var artifact) && artifact.IsFrozenAbi;
    }
}
