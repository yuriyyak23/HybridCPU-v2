using System;

namespace YAKSys_Hybrid_CPU.Memory;

/// <summary>
/// Immutable checked representation of the physical-bank location evidence
/// for one canonical vector transfer's ordered logical source elements.
/// Representation does not prove geometry membership, same-snapshot
/// provenance, accepted-shape correspondence, request ownership, execution,
/// completion, cancellation, replay, retirement, store visibility or
/// publication authority.
/// </summary>
public readonly struct CanonicalVectorPhysicalBankEnvelope
{
    private readonly PhysicalMemoryBankIndex[]? _sourceBankIndexes;

    public MemoryBankGeometryGeneration Generation { get; }

    public int Count => _sourceBankIndexes?.Length ?? 0;

    public ulong ElementCount => (ulong)Count;

    /// <summary>
    /// Ordered physical indexes for the logical source element base
    /// addresses. The returned span cannot mutate the envelope's private
    /// copy.
    /// </summary>
    public ReadOnlySpan<PhysicalMemoryBankIndex> SourceBankIndexes =>
        _sourceBankIndexes;

    /// <summary>
    /// Reports whether this carrier has an issued common generation and a
    /// non-empty ordered list of representable physical indexes. Geometry
    /// membership and accepted-shape correspondence remain owner decisions.
    /// </summary>
    public bool IsWellFormed =>
        AreComponentsRepresentable(Generation, SourceBankIndexes);

    public CanonicalVectorPhysicalBankEnvelope(
        MemoryBankGeometryGeneration generation,
        ReadOnlySpan<PhysicalMemoryBankIndex> sourceBankIndexes)
    {
        if (!generation.IsIssued)
        {
            throw new ArgumentOutOfRangeException(
                nameof(generation),
                generation,
                "A canonical vector physical-bank envelope generation must be issued.");
        }

        if (sourceBankIndexes.IsEmpty)
        {
            throw new ArgumentException(
                "A canonical vector physical-bank envelope must contain at least one source-bank index.",
                nameof(sourceBankIndexes));
        }

        for (int index = 0; index < sourceBankIndexes.Length; index++)
        {
            if (!PhysicalMemoryBankIndex.IsRepresentable(
                    sourceBankIndexes[index].Value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sourceBankIndexes),
                    sourceBankIndexes[index],
                    "Every canonical vector source-bank index must be non-negative.");
            }
        }

        Generation = generation;
        _sourceBankIndexes = sourceBankIndexes.ToArray();
    }

    public static bool AreComponentsRepresentable(
        MemoryBankGeometryGeneration generation,
        ReadOnlySpan<PhysicalMemoryBankIndex> sourceBankIndexes)
    {
        if (!generation.IsIssued || sourceBankIndexes.IsEmpty)
        {
            return false;
        }

        foreach (PhysicalMemoryBankIndex bankIndex in sourceBankIndexes)
        {
            if (!PhysicalMemoryBankIndex.IsRepresentable(bankIndex.Value))
            {
                return false;
            }
        }

        return true;
    }

    public static CanonicalVectorPhysicalBankEnvelope Create(
        MemoryBankGeometryGeneration generation,
        ReadOnlySpan<PhysicalMemoryBankIndex> sourceBankIndexes) =>
        new(generation, sourceBankIndexes);

    public static bool TryCreate(
        MemoryBankGeometryGeneration generation,
        ReadOnlySpan<PhysicalMemoryBankIndex> sourceBankIndexes,
        out CanonicalVectorPhysicalBankEnvelope envelope)
    {
        if (AreComponentsRepresentable(generation, sourceBankIndexes))
        {
            envelope = new CanonicalVectorPhysicalBankEnvelope(
                generation,
                sourceBankIndexes);
            return true;
        }

        // The default value is a malformed carrier returned only through the
        // false Try-pattern arm. It is not an absence, bank-zero or
        // generation-zero envelope.
        envelope = default;
        return false;
    }

    public PhysicalMemoryBankIndex GetSourceBankIndex(int elementIndex)
    {
        if (_sourceBankIndexes is null)
        {
            throw new InvalidOperationException(
                "A malformed canonical vector physical-bank envelope has no source-bank indexes.");
        }

        return _sourceBankIndexes[elementIndex];
    }

    /// <summary>
    /// Returns a new mutable copy for retained raw adapters or tests. Mutating
    /// the returned array cannot change this envelope.
    /// </summary>
    public PhysicalMemoryBankIndex[] CopySourceBankIndexes() =>
        _sourceBankIndexes?.ToArray() ??
        Array.Empty<PhysicalMemoryBankIndex>();

    public override string ToString() =>
        IsWellFormed
            ? $"canonical-vector-physical-bank-envelope(" +
              $"generation={Generation.Value}, elements={Count})"
            : "malformed-canonical-vector-physical-bank-envelope";
}
