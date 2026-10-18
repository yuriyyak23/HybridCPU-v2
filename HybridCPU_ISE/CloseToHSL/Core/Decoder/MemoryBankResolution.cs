namespace YAKSys_Hybrid_CPU.Core.Decoder;

/// <summary>
/// Classification of a scheduler-visible memory-bank resolution attempt.
/// This is representational state only; it grants no routing, admission,
/// execution, completion or publication authority.
/// </summary>
public enum MemoryBankResolutionKind : byte
{
    /// <summary>
    /// The live memory topology required for resolution is unavailable.
    /// This zero-valued case also makes the default result fail closed.
    /// </summary>
    UnavailableTopology = 0,

    /// <summary>
    /// Resolution produced one representable scheduler-visible bank.
    /// </summary>
    Resolved = 1,

    /// <summary>
    /// Supplied geometry cannot represent the architectural bank topology.
    /// </summary>
    InvalidGeometry = 2,
}

/// <summary>
/// Three-way result for scheduler-visible bank resolution. Non-resolved
/// results carry no <see cref="MemoryBankId"/>; bank zero remains a valid
/// resolved identity and is never used as absence.
/// </summary>
public readonly record struct MemoryBankResolution
{
    private readonly MemoryBankId? _bank;

    private MemoryBankResolution(
        MemoryBankResolutionKind kind,
        MemoryBankId? bank)
    {
        Kind = kind;
        _bank = bank;
    }

    public MemoryBankResolutionKind Kind { get; }

    public MemoryBankId? Bank =>
        Kind == MemoryBankResolutionKind.Resolved ? _bank : null;

    public bool IsResolved => Kind == MemoryBankResolutionKind.Resolved;

    public static MemoryBankResolution UnavailableTopology => default;

    public static MemoryBankResolution InvalidGeometry =>
        new(MemoryBankResolutionKind.InvalidGeometry, bank: null);

    public static MemoryBankResolution Resolved(MemoryBankId bank) =>
        new(MemoryBankResolutionKind.Resolved, bank);

    public bool TryGetResolved(out MemoryBankId bank)
    {
        if (Kind == MemoryBankResolutionKind.Resolved && _bank.HasValue)
        {
            bank = _bank.Value;
            return true;
        }

        bank = default;
        return false;
    }

    public override string ToString() => Kind switch
    {
        MemoryBankResolutionKind.Resolved =>
            $"Resolved({_bank.GetValueOrDefault()})",
        MemoryBankResolutionKind.UnavailableTopology =>
            nameof(MemoryBankResolutionKind.UnavailableTopology),
        MemoryBankResolutionKind.InvalidGeometry =>
            nameof(MemoryBankResolutionKind.InvalidGeometry),
        _ => throw new InvalidOperationException(
            $"Unknown memory-bank resolution kind: {Kind}."),
    };
}
