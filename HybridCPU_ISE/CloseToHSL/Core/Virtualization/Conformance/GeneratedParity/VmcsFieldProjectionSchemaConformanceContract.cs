using System;
using System.Collections.Generic;
using System.Linq;

namespace YAKSys_Hybrid_CPU.Core;

public enum VmcsFieldProjectionSchemaConformanceViolation : byte
{
    None = 0,
    MissingCanonicalArtifact = 1,
    MissingFrozenField = 2,
    DuplicateField = 3,
    MissingGeneratedAlias = 4,
    MissingOwner = 5,
    EntryCountMismatch = 6,
    MissingAccessPolicy = 7,
    MissingMigrationPolicy = 8,
    CompletionFieldWritable = 9,
    HostAliasWritable = 10,
    HostEvidenceExposed = 11,
    ProjectionOnlyGuestArchitecturalState = 12,
    FieldAliasWritable = 13,
}

public sealed partial class VmcsFieldProjectionSchemaConformanceContract
{
    public VmcsFieldProjectionSchemaConformanceViolation Evaluate(
        VmcsFieldProjectionSchemaArtifact artifact)
    {
        if (!artifact.IsCanonical ||
            artifact.SchemaVersion != VmcsFieldProjectionSchema.CurrentVersion ||
            artifact.SourceHash == 0 ||
            artifact.ArtifactHash == 0)
        {
            return VmcsFieldProjectionSchemaConformanceViolation.MissingCanonicalArtifact;
        }

        var seen = new HashSet<VmcsField>();
        foreach (var entry in VmcsFieldProjectionSchema.Entries)
        {
            if (!seen.Add(entry.Field))
            {
                return VmcsFieldProjectionSchemaConformanceViolation.DuplicateField;
            }

            if (!entry.IsGeneratedAlias)
            {
                return VmcsFieldProjectionSchemaConformanceViolation.MissingGeneratedAlias;
            }

            if (!Enum.IsDefined(typeof(VmcsFieldProjectionOwner), entry.Owner))
            {
                return VmcsFieldProjectionSchemaConformanceViolation.MissingOwner;
            }

            if (!Enum.IsDefined(typeof(VmcsFieldProjectionAccessPolicy), entry.AccessPolicy) ||
                entry.AccessPolicy == VmcsFieldProjectionAccessPolicy.Denied)
            {
                return VmcsFieldProjectionSchemaConformanceViolation.MissingAccessPolicy;
            }

            if (!Enum.IsDefined(typeof(VmcsFieldProjectionMigrationPolicy), entry.MigrationPolicy) ||
                entry.MigrationPolicy == VmcsFieldProjectionMigrationPolicy.None)
            {
                return VmcsFieldProjectionSchemaConformanceViolation.MissingMigrationPolicy;
            }

            if (entry.Owner == VmcsFieldProjectionOwner.CompletionRecord &&
                VmcsFieldProjectionSchema.CanWrite(entry))
            {
                return VmcsFieldProjectionSchemaConformanceViolation.CompletionFieldWritable;
            }

            if (IsHostCompatibilityAlias(entry.Field) &&
                VmcsFieldProjectionSchema.CanWrite(entry))
            {
                return VmcsFieldProjectionSchemaConformanceViolation.HostAliasWritable;
            }

            if (entry.EvidenceClass == EvidenceVisibilityClass.HostOwnedRuntimeEvidence)
            {
                return VmcsFieldProjectionSchemaConformanceViolation.HostEvidenceExposed;
            }

            if (VmcsFieldProjectionSchema.CanWrite(entry))
            {
                return VmcsFieldProjectionSchemaConformanceViolation.FieldAliasWritable;
            }

            if (entry.EvidenceClass == EvidenceVisibilityClass.GuestArchitecturalState &&
                entry.MigrationPolicy == VmcsFieldProjectionMigrationPolicy.ProjectionOnly)
            {
                return VmcsFieldProjectionSchemaConformanceViolation.ProjectionOnlyGuestArchitecturalState;
            }
        }

        foreach (VmcsField field in Enum.GetValues(typeof(VmcsField)).Cast<VmcsField>())
        {
            if (!seen.Contains(field))
            {
                return VmcsFieldProjectionSchemaConformanceViolation.MissingFrozenField;
            }
        }

        return artifact.EntryCount == seen.Count
            ? VmcsFieldProjectionSchemaConformanceViolation.None
            : VmcsFieldProjectionSchemaConformanceViolation.EntryCountMismatch;
    }

    public bool IsSatisfied(VmcsFieldProjectionSchemaArtifact artifact) =>
        Evaluate(artifact) == VmcsFieldProjectionSchemaConformanceViolation.None;

    public bool IsCurrentSchemaSatisfied() =>
        IsSatisfied(VmcsFieldProjectionSchema.CanonicalArtifact);

    private static bool IsHostCompatibilityAlias(VmcsField field) =>
        field == VmcsField.HostPc ||
        field == VmcsField.HostSp ||
        field == VmcsField.HostFlags ||
        field == VmcsField.HostCr0 ||
        field == VmcsField.HostCr3;
}
