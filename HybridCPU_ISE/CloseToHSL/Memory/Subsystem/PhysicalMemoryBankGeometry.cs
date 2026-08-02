using System;

namespace YAKSys_Hybrid_CPU.Memory;

/// <summary>
/// Immutable checked representation of one physical memory-bank geometry
/// tuple. Representation does not prove subsystem ownership, allocation
/// feasibility, publication, request binding, admission, execution,
/// completion, cancellation, replay, retirement, store visibility or
/// telemetry authority.
/// </summary>
public readonly record struct PhysicalMemoryBankGeometry
{
    public const int MinBankCount = 1;
    public const int MaxBankCount = int.MaxValue;
    public const int MinBankWidthBytes = 1;
    public const int MaxBankWidthBytes = int.MaxValue;

    public int BankCount { get; }

    public int BankWidthBytes { get; }

    public MemoryBankGeometryGeneration Generation { get; }

    /// <summary>
    /// Reports whether all three tuple components are representationally
    /// valid. This does not prove publication or owner authority.
    /// </summary>
    public bool IsWellFormed =>
        AreComponentsRepresentable(BankCount, BankWidthBytes, Generation);

    public PhysicalMemoryBankGeometry(
        int bankCount,
        int bankWidthBytes,
        MemoryBankGeometryGeneration generation)
    {
        if (!IsBankCountRepresentable(bankCount))
        {
            throw new ArgumentOutOfRangeException(
                nameof(bankCount),
                bankCount,
                "A physical memory-bank count must be positive.");
        }

        if (!IsBankWidthRepresentable(bankWidthBytes))
        {
            throw new ArgumentOutOfRangeException(
                nameof(bankWidthBytes),
                bankWidthBytes,
                "A physical memory-bank width must be positive.");
        }

        if (!generation.IsIssued)
        {
            throw new ArgumentOutOfRangeException(
                nameof(generation),
                generation,
                "A physical memory-bank geometry generation must be issued.");
        }

        BankCount = bankCount;
        BankWidthBytes = bankWidthBytes;
        Generation = generation;
    }

    /// <summary>
    /// Returns whether a raw count is representable in a checked geometry.
    /// Allocation feasibility remains a platform-owner decision.
    /// </summary>
    public static bool IsBankCountRepresentable(int bankCount) =>
        bankCount >= MinBankCount;

    /// <summary>
    /// Returns whether a raw width is representable in a checked geometry.
    /// Allocation feasibility remains a platform-owner decision.
    /// </summary>
    public static bool IsBankWidthRepresentable(int bankWidthBytes) =>
        bankWidthBytes >= MinBankWidthBytes;

    /// <summary>
    /// Returns whether all components can form a checked geometry tuple.
    /// </summary>
    public static bool AreComponentsRepresentable(
        int bankCount,
        int bankWidthBytes,
        MemoryBankGeometryGeneration generation) =>
        IsBankCountRepresentable(bankCount) &&
        IsBankWidthRepresentable(bankWidthBytes) &&
        generation.IsIssued;

    public static PhysicalMemoryBankGeometry Create(
        int bankCount,
        int bankWidthBytes,
        MemoryBankGeometryGeneration generation) =>
        new(bankCount, bankWidthBytes, generation);

    public static bool TryCreate(
        int bankCount,
        int bankWidthBytes,
        MemoryBankGeometryGeneration generation,
        out PhysicalMemoryBankGeometry geometry)
    {
        if (AreComponentsRepresentable(
                bankCount,
                bankWidthBytes,
                generation))
        {
            geometry = new PhysicalMemoryBankGeometry(
                bankCount,
                bankWidthBytes,
                generation);
            return true;
        }

        geometry = default;
        return false;
    }

    public override string ToString() =>
        IsWellFormed
            ? $"physical-memory-bank-geometry(count={BankCount}, " +
              $"width-bytes={BankWidthBytes}, generation={Generation.Value})"
            : "no-physical-memory-bank-geometry";
}
