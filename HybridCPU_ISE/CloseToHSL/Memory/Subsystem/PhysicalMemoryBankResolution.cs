using System;

namespace YAKSys_Hybrid_CPU.Memory;

/// <summary>
/// Discriminant for a physical memory-bank resolution result. This is
/// distinct from scheduler-visible memory-bank resolution.
/// </summary>
public enum PhysicalMemoryBankResolutionKind : byte
{
    Unavailable = 0,
    Resolved = 1,
}

/// <summary>
/// Exact reason that physical memory-bank resolution is unavailable.
/// Unavailable results carry no bank index or geometry generation.
/// </summary>
public enum PhysicalMemoryBankUnavailableReason : byte
{
    NoPublishedGeometry = 0,
    InvalidBankCount = 1,
    InvalidBankWidth = 2,
    GenerationUnavailable = 3,
}

/// <summary>
/// Discriminated physical memory-bank resolution result. A resolved result
/// contains exactly one checked binding; an unavailable result contains only
/// one reason. Representation grants no legality, ownership, admission,
/// request acceptance, execution, completion, cancellation, replay,
/// retirement, store visibility or publication authority.
/// </summary>
public readonly record struct PhysicalMemoryBankResolution
{
    private readonly PhysicalMemoryBankBinding? _binding;
    private readonly PhysicalMemoryBankUnavailableReason _unavailableReason;

    private PhysicalMemoryBankResolution(
        PhysicalMemoryBankResolutionKind kind,
        PhysicalMemoryBankBinding? binding,
        PhysicalMemoryBankUnavailableReason unavailableReason)
    {
        Kind = kind;
        _binding = binding;
        _unavailableReason = unavailableReason;
    }

    public PhysicalMemoryBankResolutionKind Kind { get; }

    public bool IsResolved =>
        Kind == PhysicalMemoryBankResolutionKind.Resolved;

    public PhysicalMemoryBankBinding? Binding =>
        IsResolved ? _binding : null;

    public PhysicalMemoryBankUnavailableReason? UnavailableReason =>
        Kind == PhysicalMemoryBankResolutionKind.Unavailable
            ? _unavailableReason
            : null;

    /// <summary>
    /// The default result fails closed as no published geometry.
    /// </summary>
    public static PhysicalMemoryBankResolution NoPublishedGeometry => default;

    public static PhysicalMemoryBankResolution Resolved(
        PhysicalMemoryBankBinding binding)
    {
        if (!binding.IsWellFormed)
        {
            throw new ArgumentOutOfRangeException(
                nameof(binding),
                binding,
                "A resolved physical memory-bank result requires a " +
                "well-formed binding.");
        }

        return new PhysicalMemoryBankResolution(
            PhysicalMemoryBankResolutionKind.Resolved,
            binding,
            default);
    }

    public static PhysicalMemoryBankResolution Unavailable(
        PhysicalMemoryBankUnavailableReason reason)
    {
        if (!IsRepresentable(reason))
        {
            throw new ArgumentOutOfRangeException(
                nameof(reason),
                reason,
                "Unknown physical memory-bank unavailable reason.");
        }

        return reason == PhysicalMemoryBankUnavailableReason.NoPublishedGeometry
            ? default
            : new PhysicalMemoryBankResolution(
                PhysicalMemoryBankResolutionKind.Unavailable,
                binding: null,
                reason);
    }

    public static bool IsRepresentable(
        PhysicalMemoryBankUnavailableReason reason) =>
        reason is >= PhysicalMemoryBankUnavailableReason.NoPublishedGeometry
            and <= PhysicalMemoryBankUnavailableReason.GenerationUnavailable;

    public bool TryGetResolved(out PhysicalMemoryBankBinding binding)
    {
        if (IsResolved && _binding is { IsWellFormed: true } resolved)
        {
            binding = resolved;
            return true;
        }

        binding = default;
        return false;
    }

    public bool TryGetUnavailableReason(
        out PhysicalMemoryBankUnavailableReason reason)
    {
        if (Kind == PhysicalMemoryBankResolutionKind.Unavailable &&
            IsRepresentable(_unavailableReason))
        {
            reason = _unavailableReason;
            return true;
        }

        reason = default;
        return false;
    }

    public override string ToString() => Kind switch
    {
        PhysicalMemoryBankResolutionKind.Unavailable
            when IsRepresentable(_unavailableReason) =>
            $"Unavailable({_unavailableReason})",
        PhysicalMemoryBankResolutionKind.Resolved
            when _binding is { IsWellFormed: true } =>
            $"Resolved({_binding.Value})",
        _ => throw new InvalidOperationException(
            $"Malformed physical memory-bank resolution kind: {Kind}."),
    };
}
