using System;

namespace YAKSys_Hybrid_CPU.Memory;

/// <summary>
/// Discriminant for an authoritative physical memory-bank geometry update
/// result.
/// </summary>
public enum MemoryBankGeometryUpdateResultKind : byte
{
    Rejected = 0,
    Applied = 1,
}

/// <summary>
/// Exact reason that a physical memory-bank geometry update was rejected.
/// </summary>
public enum MemoryBankGeometryUpdateRejectReason : byte
{
    InvalidBankCount = 0,
    InvalidBankWidth = 1,
    Busy = 2,
    GenerationExhausted = 3,
    PlatformRejected = 4,
}

/// <summary>
/// Discriminated result for an authoritative physical memory-bank geometry
/// update. This representation does not validate or publish geometry, allocate
/// a generation, prove quiescence, prepare owner state, or grant lifecycle
/// authority.
/// </summary>
public readonly record struct MemoryBankGeometryUpdateResult
{
    private readonly MemoryBankGeometryUpdateRejectReason _rejectReason;

    private MemoryBankGeometryUpdateResult(
        MemoryBankGeometryUpdateResultKind kind,
        MemoryBankGeometryUpdateRejectReason rejectReason)
    {
        Kind = kind;
        _rejectReason = rejectReason;
    }

    public MemoryBankGeometryUpdateResultKind Kind { get; }

    public bool IsApplied =>
        Kind == MemoryBankGeometryUpdateResultKind.Applied;

    public MemoryBankGeometryUpdateRejectReason? RejectReason =>
        Kind == MemoryBankGeometryUpdateResultKind.Rejected
            ? _rejectReason
            : null;

    /// <summary>
    /// The default result fails closed as an invalid bank-count rejection.
    /// </summary>
    public static MemoryBankGeometryUpdateResult InvalidBankCount => default;

    public static MemoryBankGeometryUpdateResult Applied =>
        new(
            MemoryBankGeometryUpdateResultKind.Applied,
            default);

    public static MemoryBankGeometryUpdateResult Rejected(
        MemoryBankGeometryUpdateRejectReason reason)
    {
        if (!IsRepresentable(reason))
        {
            throw new ArgumentOutOfRangeException(
                nameof(reason),
                reason,
                "Unknown memory-bank geometry update rejection reason.");
        }

        return reason == MemoryBankGeometryUpdateRejectReason.InvalidBankCount
            ? default
            : new MemoryBankGeometryUpdateResult(
                MemoryBankGeometryUpdateResultKind.Rejected,
                reason);
    }

    public static bool IsRepresentable(
        MemoryBankGeometryUpdateRejectReason reason) =>
        reason is >= MemoryBankGeometryUpdateRejectReason.InvalidBankCount
            and <= MemoryBankGeometryUpdateRejectReason.PlatformRejected;

    public bool TryGetRejectReason(
        out MemoryBankGeometryUpdateRejectReason reason)
    {
        if (Kind == MemoryBankGeometryUpdateResultKind.Rejected &&
            IsRepresentable(_rejectReason))
        {
            reason = _rejectReason;
            return true;
        }

        reason = default;
        return false;
    }

    public override string ToString() => Kind switch
    {
        MemoryBankGeometryUpdateResultKind.Rejected
            when IsRepresentable(_rejectReason) =>
            $"Rejected({_rejectReason})",
        MemoryBankGeometryUpdateResultKind.Applied => "Applied",
        _ => throw new InvalidOperationException(
            $"Malformed memory-bank geometry update result kind: {Kind}."),
    };
}
