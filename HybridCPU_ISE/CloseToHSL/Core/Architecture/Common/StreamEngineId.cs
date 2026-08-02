using System;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Checked representation of the fixed stream-resource selector (0..3).
/// This value proves representation only; it does not grant scheduler
/// admission, execution, completion, replay, retirement or publication
/// authority, and it is not a DMA channel, lane, slot, device or queue.
/// </summary>
public readonly record struct StreamEngineId
{
    public const int EngineCount = 4;
    public const byte MinValue = 0;
    public const byte MaxValue = EngineCount - 1;

    public static StreamEngineId Zero { get; } = new(0, skipValidation: true);

    public byte Value { get; }

    [System.Text.Json.Serialization.JsonConstructor]
    public StreamEngineId(byte value)
    {
        if (value > MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value,
                $"Stream engine id must be in [0, {MaxValue}].");
        }

        Value = value;
    }

    private StreamEngineId(byte value, bool skipValidation) => Value = value;

    /// <summary>Returns whether a raw integer is representable as a stream selector.</summary>
    public static bool IsRepresentable(int value) => (uint)value < EngineCount;

    /// <summary>Reconstructs a checked selector from its retained byte form.</summary>
    public static StreamEngineId FromRawValue(byte value) => new(value);

    /// <summary>Projects this valid selector to its retained byte form.</summary>
    public byte ToRawValue() => Value;

    public static StreamEngineId Create(int value)
    {
        if (!TryCreate(value, out StreamEngineId streamEngineId))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value,
                $"Stream engine id must be in [0, {MaxValue}].");
        }

        return streamEngineId;
    }

    public static bool TryCreate(int value, out StreamEngineId streamEngineId)
    {
        if (IsRepresentable(value))
        {
            streamEngineId = new StreamEngineId((byte)value, skipValidation: true);
            return true;
        }

        streamEngineId = default;
        return false;
    }

    public override string ToString() => $"stream{Value}";

    public static implicit operator int(StreamEngineId streamEngineId) => streamEngineId.Value;
    public static explicit operator byte(StreamEngineId streamEngineId) => streamEngineId.Value;
    public static explicit operator StreamEngineId(int value) => Create(value);
}
