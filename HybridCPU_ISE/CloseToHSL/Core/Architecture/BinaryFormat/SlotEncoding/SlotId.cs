using System;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Checked representation of a canonical or working bundle position.
/// Representation does not grant decode legality, admission, placement,
/// execution, replay, retirement or publication authority.
/// </summary>
public readonly record struct SlotId
{
    public const int SlotCount = 8;
    public const byte MinValue = 0;
    public const byte MaxValue = SlotCount - 1;

    public static SlotId Zero { get; } = new(0, skipValidation: true);

    public byte Value { get; }

    [System.Text.Json.Serialization.JsonConstructor]
    public SlotId(byte value)
    {
        if (value > MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Bundle slot id must be in [0, {MaxValue}].");
        }

        Value = value;
    }

    private SlotId(byte value, bool skipValidation) => Value = value;

    /// <summary>
    /// Returns whether <paramref name="value"/> is representable as a bundle
    /// position. This is not a legality, occupancy or placement check.
    /// </summary>
    public static bool IsRepresentable(int value) =>
        (uint)value < SlotCount;

    /// <summary>
    /// Reconstructs a checked bundle position from its retained byte form.
    /// </summary>
    public static SlotId FromRawValue(byte value) => new(value);

    /// <summary>
    /// Projects this valid bundle position to its retained byte form.
    /// </summary>
    public byte ToRawValue() => Value;

    public static SlotId Create(int value)
    {
        if (!TryCreate(value, out SlotId slotId))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Bundle slot id must be in [0, {MaxValue}].");
        }

        return slotId;
    }

    public static bool TryCreate(int value, out SlotId slotId)
    {
        if (IsRepresentable(value))
        {
            slotId = new SlotId((byte)value, skipValidation: true);
            return true;
        }

        slotId = default;
        return false;
    }

    public override string ToString() => $"slot{Value}";

    public static implicit operator int(SlotId slotId) => slotId.Value;
    public static explicit operator byte(SlotId slotId) => slotId.Value;
    public static explicit operator SlotId(int value) => Create(value);
}
