using System;

namespace YAKSys_Hybrid_CPU.Core;

public enum VirtualizationGoldenArtifactKind : byte
{
    CompatAbiSnapshot = 0,
    GeneratedProjectionParity = 1,
    AliasMapCompleteness = 2,
    HostEvidenceNonLeak = 3,
    MigrationReplay = 4,
    NestedComposition = 5,
}

public readonly record struct VirtualizationGoldenArtifact(
    VirtualizationGoldenArtifactKind Kind,
    string Name,
    bool IsHardArchitecturalGate,
    bool RequiresGeneratedSource);

public sealed partial class VirtualizationGoldenArtifactManifest
{
    private static readonly VirtualizationGoldenArtifact[] ArtifactTable = new VirtualizationGoldenArtifact[]
    {
        new(VirtualizationGoldenArtifactKind.CompatAbiSnapshot, "CompatAbiFreezeContract", true, false),
        new(VirtualizationGoldenArtifactKind.GeneratedProjectionParity, "GeneratedProjectionParityContract", true, true),
        new(VirtualizationGoldenArtifactKind.AliasMapCompleteness, "CompatAliasMap", true, true),
        new(VirtualizationGoldenArtifactKind.HostEvidenceNonLeak, "HostEvidenceNonLeakContract", true, false),
        new(VirtualizationGoldenArtifactKind.MigrationReplay, "MigrationReplayContract", true, false),
        new(VirtualizationGoldenArtifactKind.NestedComposition, "NestedCompositionContract", true, false),
    };

    public static ReadOnlySpan<VirtualizationGoldenArtifact> Artifacts => ArtifactTable;

    public bool Contains(VirtualizationGoldenArtifactKind kind)
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

    public bool TryGetArtifact(
        VirtualizationGoldenArtifactKind kind,
        out VirtualizationGoldenArtifact artifact)
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

    public bool IsHardArchitecturalGate(VirtualizationGoldenArtifactKind kind)
    {
        return TryGetArtifact(kind, out var artifact) && artifact.IsHardArchitecturalGate;
    }

    public bool RequiresGeneratedSource(VirtualizationGoldenArtifactKind kind)
    {
        return TryGetArtifact(kind, out var artifact) && artifact.RequiresGeneratedSource;
    }
}
