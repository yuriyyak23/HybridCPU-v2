using System;

namespace YAKSys_Hybrid_CPU.Memory;

/// <summary>
/// Immutable checked representation of one topology-local physical bank
/// position paired with one issued geometry generation. Representation does
/// not prove geometry ownership, index membership, same-snapshot provenance,
/// request acceptance, queue membership, execution, completion,
/// cancellation, replay, retirement, store visibility or publication
/// authority.
/// </summary>
public readonly record struct PhysicalMemoryBankBinding
{
    public PhysicalMemoryBankIndex BankIndex { get; }

    public MemoryBankGeometryGeneration Generation { get; }

    /// <summary>
    /// Reports whether both components are representationally usable as a
    /// binding. This does not prove membership in or provenance from a
    /// published geometry.
    /// </summary>
    public bool IsWellFormed =>
        AreComponentsRepresentable(BankIndex, Generation);

    public PhysicalMemoryBankBinding(
        PhysicalMemoryBankIndex bankIndex,
        MemoryBankGeometryGeneration generation)
    {
        if (!PhysicalMemoryBankIndex.IsRepresentable(bankIndex.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(bankIndex),
                bankIndex,
                "A physical memory-bank binding index must be non-negative.");
        }

        if (!generation.IsIssued)
        {
            throw new ArgumentOutOfRangeException(
                nameof(generation),
                generation,
                "A physical memory-bank binding generation must be issued.");
        }

        BankIndex = bankIndex;
        Generation = generation;
    }

    /// <summary>
    /// Returns whether both checked component representations can form a
    /// binding carrier. Geometry membership remains an owner decision.
    /// </summary>
    public static bool AreComponentsRepresentable(
        PhysicalMemoryBankIndex bankIndex,
        MemoryBankGeometryGeneration generation) =>
        PhysicalMemoryBankIndex.IsRepresentable(bankIndex.Value) &&
        generation.IsIssued;

    public static PhysicalMemoryBankBinding Create(
        PhysicalMemoryBankIndex bankIndex,
        MemoryBankGeometryGeneration generation) =>
        new(bankIndex, generation);

    public static bool TryCreate(
        PhysicalMemoryBankIndex bankIndex,
        MemoryBankGeometryGeneration generation,
        out PhysicalMemoryBankBinding binding)
    {
        if (AreComponentsRepresentable(bankIndex, generation))
        {
            binding = new PhysicalMemoryBankBinding(bankIndex, generation);
            return true;
        }

        binding = default;
        return false;
    }

    public override string ToString() =>
        IsWellFormed
            ? $"physical-memory-bank-binding(index={BankIndex.Value}, " +
              $"generation={Generation.Value})"
            : "no-physical-memory-bank-binding";
}
