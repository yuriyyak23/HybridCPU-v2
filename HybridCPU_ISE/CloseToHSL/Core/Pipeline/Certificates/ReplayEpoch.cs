using System;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Checked representation of one issued loop-buffer replay epoch.
/// Representation proves only the non-zero UInt64 shape. It does not grant
/// loop-buffer ownership, admission, scheduler reuse, execution, completion,
/// replay, retirement or publication authority.
/// </summary>
public readonly record struct ReplayEpoch
{
    public const ulong MinValue = 1UL;
    public const ulong MaxValue = ulong.MaxValue;

    public ulong Value { get; }

    /// <summary>True only for the issued non-zero representation.</summary>
    public bool IsIssued => IsRepresentable(Value);

    public ReplayEpoch(ulong value)
    {
        if (!IsRepresentable(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value, "A replay epoch must be non-zero.");
        }

        Value = value;
    }

    public static bool IsRepresentable(ulong value) => value >= MinValue;

    public static ReplayEpoch Create(ulong value) => new(value);

    public static bool TryCreate(ulong value, out ReplayEpoch epoch)
    {
        if (IsRepresentable(value))
        {
            epoch = new ReplayEpoch(value);
            return true;
        }

        epoch = default;
        return false;
    }

    /// <summary>Reconstructs an issued epoch from its retained UInt64 form.</summary>
    public static ReplayEpoch FromRawValue(ulong value) => new(value);

    /// <summary>Projects an issued value, or default absence, to raw UInt64.</summary>
    public ulong ToRawValue() => Value;

    public override string ToString() =>
        IsIssued ? $"replay-epoch{Value}" : "unissued";
}
