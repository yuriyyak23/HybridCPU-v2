using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core;

public enum SecureComputeProjectionOwnerKind : byte
{
    None = 0,
    SecureDomainDescriptor = 1,
    SecureMemoryDescriptor = 2,
    SecureEvidencePolicy = 3,
    SecureMigrationDescriptor = 4,
    SecureCompatibilityProjectionPolicy = 5,
}

public readonly record struct SecureComputeProjectionSchemaEntry(
    ulong FieldId,
    SecureComputeProjectionOwnerKind Owner,
    bool ReadOnly,
    bool SecureSensitive,
    bool MigrationClassified);

public sealed partial class SecureComputeCompatibilityProjectionSchema
{
    public SecureComputeCompatibilityProjectionSchema()
        : this(System.Array.Empty<SecureComputeProjectionSchemaEntry>())
    {
    }

    public SecureComputeCompatibilityProjectionSchema(
        IReadOnlyList<SecureComputeProjectionSchemaEntry> entries)
    {
        Entries = entries;
    }

    public static SecureComputeCompatibilityProjectionSchema Empty { get; } = new();

    public IReadOnlyList<SecureComputeProjectionSchemaEntry> Entries { get; }

    public bool IsProjectionOnly => true;
}
