using System;

namespace YAKSys_Hybrid_CPU.Memory;

/// <summary>
/// Checked representation of a topology-local physical memory-bank
/// queue/array position. Representation does not prove membership in a
/// geometry snapshot and grants no admission, acceptance, execution,
/// cancellation, completion, replay, retirement, store-visibility or
/// publication authority.
/// </summary>
public readonly record struct PhysicalMemoryBankIndex
{
    public const int MinValue = 0;
    public const int MaxValue = int.MaxValue;

    public static PhysicalMemoryBankIndex Zero { get; } =
        new(0, skipValidation: true);

    public int Value { get; }

    [System.Text.Json.Serialization.JsonConstructor]
    public PhysicalMemoryBankIndex(int value)
    {
        if (!IsRepresentable(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "A physical memory-bank index must be non-negative.");
        }

        Value = value;
    }

    private PhysicalMemoryBankIndex(int value, bool skipValidation) =>
        Value = value;

    /// <summary>
    /// Returns whether <paramref name="value"/> is representable as a physical
    /// bank position. This is not a geometry-membership or queue-legality
    /// check.
    /// </summary>
    public static bool IsRepresentable(int value) => value >= MinValue;

    /// <summary>
    /// Reconstructs a checked physical position from its retained Int32 form.
    /// </summary>
    public static PhysicalMemoryBankIndex FromRawValue(int value) =>
        new(value);

    /// <summary>
    /// Projects this checked physical position to its retained Int32 form.
    /// </summary>
    public int ToRawValue() => Value;

    public static PhysicalMemoryBankIndex Create(int value)
    {
        if (!TryCreate(value, out PhysicalMemoryBankIndex bankIndex))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "A physical memory-bank index must be non-negative.");
        }

        return bankIndex;
    }

    public static bool TryCreate(
        int value,
        out PhysicalMemoryBankIndex bankIndex)
    {
        if (IsRepresentable(value))
        {
            bankIndex =
                new PhysicalMemoryBankIndex(value, skipValidation: true);
            return true;
        }

        bankIndex = default;
        return false;
    }

    public override string ToString() => $"physical-bank{Value}";

    public static implicit operator int(PhysicalMemoryBankIndex bankIndex) =>
        bankIndex.Value;

    public static explicit operator PhysicalMemoryBankIndex(int value) =>
        Create(value);
}
