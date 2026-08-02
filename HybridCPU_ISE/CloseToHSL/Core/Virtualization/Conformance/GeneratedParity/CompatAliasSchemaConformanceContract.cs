using System;

namespace YAKSys_Hybrid_CPU.Core;

public enum CompatAliasSchemaConformanceViolation : byte
{
    None = 0,
    MissingSpecArtifactSet = 1,
    MissingAliasMap = 2,
    MissingCompatAliasArtifact = 3,
    GeneratedParityNotRequired = 4,
    AbiFreezeNotRequired = 5,
    MissingCanonicalSchema = 6,
    SchemaMismatch = 7,
    EmptyAliasName = 8,
    UnfrozenAliasEntry = 9,
}

public readonly record struct CompatAliasSchemaConformanceRequest(
    CompatAliasMap AliasMap,
    CompatSpecArtifactSet SpecArtifacts,
    CompatAliasSchemaArtifact SchemaArtifact);

public sealed partial class CompatAliasSchemaConformanceContract
{
    public CompatAliasSchemaConformanceViolation Evaluate(
        CompatAliasSchemaConformanceRequest request)
    {
        if (request.SpecArtifacts is null)
        {
            return CompatAliasSchemaConformanceViolation.MissingSpecArtifactSet;
        }

        if (request.AliasMap is null)
        {
            return CompatAliasSchemaConformanceViolation.MissingAliasMap;
        }

        if (!request.SpecArtifacts.Contains(CompatSpecArtifactKind.CompatAliasMap))
        {
            return CompatAliasSchemaConformanceViolation.MissingCompatAliasArtifact;
        }

        if (!request.SpecArtifacts.RequiresGeneratedParity(CompatSpecArtifactKind.CompatAliasMap))
        {
            return CompatAliasSchemaConformanceViolation.GeneratedParityNotRequired;
        }

        if (!request.SpecArtifacts.RequiresAbiFreeze(CompatSpecArtifactKind.CompatAliasMap))
        {
            return CompatAliasSchemaConformanceViolation.AbiFreezeNotRequired;
        }

        if (!request.SchemaArtifact.IsCanonical ||
            request.SchemaArtifact.SourceHash == 0 ||
            request.SchemaArtifact.ArtifactHash == 0 ||
            request.SchemaArtifact.EntryCount == 0)
        {
            return CompatAliasSchemaConformanceViolation.MissingCanonicalSchema;
        }

        if (!request.AliasMap.MatchesCanonicalSchema(request.SchemaArtifact))
        {
            return CompatAliasSchemaConformanceViolation.SchemaMismatch;
        }

        foreach (var entry in CompatAliasMap.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.SourceName) ||
                string.IsNullOrWhiteSpace(entry.TargetName))
            {
                return CompatAliasSchemaConformanceViolation.EmptyAliasName;
            }

            if (!entry.IsFrozenAbi)
            {
                return CompatAliasSchemaConformanceViolation.UnfrozenAliasEntry;
            }
        }

        return CompatAliasSchemaConformanceViolation.None;
    }

    public bool IsSatisfied(CompatAliasSchemaConformanceRequest request) =>
        Evaluate(request) == CompatAliasSchemaConformanceViolation.None;

    public bool IsCurrentSchemaSatisfied() =>
        IsSatisfied(new CompatAliasSchemaConformanceRequest(
            new CompatAliasMap(),
            new CompatSpecArtifactSet(),
            CompatAliasMap.CanonicalSchema));
}
