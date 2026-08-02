using System;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Checked representation of one post-Stage-B physical lane.
/// Representation does not grant typed-slot admission, placement, execution,
/// completion, replay, retirement or publication authority.
/// </summary>
public readonly record struct LaneId
{
    public const int LaneCount = 8;
    public const byte MinValue = 0;
    public const byte MaxValue = LaneCount - 1;

    /// <summary>Valid physical lane zero; never flexible placement or absence.</summary>
    public static LaneId Zero { get; } = new(0, skipValidation: true);

    public byte Value { get; }

    [System.Text.Json.Serialization.JsonConstructor]
    public LaneId(byte value)
    {
        if (value > MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Physical lane id must be in [0, {MaxValue}].");
        }

        Value = value;
    }

    private LaneId(byte value, bool skipValidation) => Value = value;

    /// <summary>
    /// Returns whether <paramref name="value"/> is representable as a physical
    /// lane. This is not a pinning, legality, occupancy or placement check.
    /// </summary>
    public static bool IsRepresentable(int value) =>
        (uint)value < LaneCount;

    /// <summary>
    /// Reconstructs a checked physical lane from its retained byte form.
    /// </summary>
    public static LaneId FromRawValue(byte value) => new(value);

    /// <summary>
    /// Projects this valid physical lane to its retained byte form.
    /// </summary>
    public byte ToRawValue() => Value;

    public static LaneId Create(int value)
    {
        if (!TryCreate(value, out LaneId laneId))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Physical lane id must be in [0, {MaxValue}].");
        }

        return laneId;
    }

    public static bool TryCreate(int value, out LaneId laneId)
    {
        if (IsRepresentable(value))
        {
            laneId = new LaneId((byte)value, skipValidation: true);
            return true;
        }

        laneId = default;
        return false;
    }

    public override string ToString() => $"lane{Value}";

    public static implicit operator int(LaneId laneId) => laneId.Value;
    public static explicit operator byte(LaneId laneId) => laneId.Value;
    public static explicit operator LaneId(int value) => Create(value);
}
