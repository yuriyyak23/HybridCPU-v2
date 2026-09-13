using System;

namespace YAKSys_Hybrid_CPU.Memory;

/// <summary>
/// Checked representation of one issued physical memory-bank geometry
/// generation. Representation does not prove subsystem ownership, geometry
/// publication, binding legality, request acceptance, replay identity,
/// completion, cancellation or store-publication authority.
/// </summary>
public readonly record struct MemoryBankGeometryGeneration
{
    public const ulong MinValue = 1UL;
    public const ulong MaxValue = ulong.MaxValue;

    public ulong Value { get; }

    /// <summary>
    /// Reports whether this value represents an issued generation. The default
    /// all-zero value is the unissued/absent representation.
    /// </summary>
    public bool IsIssued => IsRepresentable(Value);

    public MemoryBankGeometryGeneration(ulong value)
    {
        if (!IsRepresentable(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "A memory-bank geometry generation must be non-zero.");
        }

        Value = value;
    }

    /// <summary>
    /// Returns whether <paramref name="value"/> is representable as an issued
    /// memory-bank geometry generation. This is not an ownership, publication
    /// or binding-validity check.
    /// </summary>
    public static bool IsRepresentable(ulong value) => value >= MinValue;

    /// <summary>
    /// Reconstructs an issued generation from its retained UInt64 form.
    /// </summary>
    public static MemoryBankGeometryGeneration FromRawValue(ulong value) =>
        new(value);

    /// <summary>
    /// Projects this representation to its retained UInt64 form. For
    /// <c>default</c>, this returns the unissued/absent raw zero.
    /// </summary>
    public ulong ToRawValue() => Value;

    public static MemoryBankGeometryGeneration Create(ulong value) =>
        new(value);

    public static bool TryCreate(
        ulong value,
        out MemoryBankGeometryGeneration generation)
    {
        if (IsRepresentable(value))
        {
            generation = new(value);
            return true;
        }

        generation = default;
        return false;
    }

    public override string ToString() =>
        IsIssued ? $"memory-bank-geometry-generation{Value}" : "unissued";
}
