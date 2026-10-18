using System;

namespace HybridCPU_ISE.CloseToHSL.Memory.DMA;

/// <summary>
/// Checked representation of one persistent DMA-controller channel position.
/// This value proves only the fixed 0..7 representation. It does not grant
/// controller ownership, transfer admission, channel availability, execution,
/// completion, interrupt, replay, retirement or publication authority.
/// </summary>
public readonly record struct DmaChannelId
{
    public const byte MinValue = 0;
    public const byte MaxValue = 7;

    public static DmaChannelId Zero { get; } =
        new(0, skipValidation: true);

    public byte Value { get; }

    [System.Text.Json.Serialization.JsonConstructor]
    public DmaChannelId(byte value)
    {
        if (!IsRepresentable(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value,
                "A persistent DMA channel identity must be in the range [0, 7].");
        }

        Value = value;
    }

    private DmaChannelId(byte value, bool skipValidation) => Value = value;

    public static bool IsRepresentable(byte value) => value <= MaxValue;

    public static DmaChannelId Create(byte value)
    {
        if (!TryCreate(value, out DmaChannelId channel))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value,
                "A persistent DMA channel identity must be in the range [0, 7].");
        }

        return channel;
    }

    public static bool TryCreate(byte value, out DmaChannelId channel)
    {
        if (IsRepresentable(value))
        {
            channel = new DmaChannelId(value, skipValidation: true);
            return true;
        }

        channel = default;
        return false;
    }

    public static DmaChannelId FromRawValue(byte value) => Create(value);

    public byte ToRawValue() => Value;

    public override string ToString() => $"dma-channel{Value}";

    public static implicit operator byte(DmaChannelId channel) => channel.Value;

    public static explicit operator DmaChannelId(byte value) => Create(value);
}
