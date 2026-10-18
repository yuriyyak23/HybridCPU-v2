using System.Collections.Immutable;
using System.Security.Cryptography;

namespace HybridCpu.ShadowFrontend;

// No interpretation factor has been projected away. Binding is a trusted-port claim,
// not inferred from a hash or from a caller's numeric ASID.
public sealed record DecodeContext(string ManifestVersion, string ManifestHash,
    string Extensions, string DecoderEpoch, string DecoderVersion, string Privilege,
    string Domain, string AddressSpace, string VectorConfiguration,
    ulong ExecutableEpoch, ulong CodeEpoch, bool ReplayEligible,
    string ContextIncarnation, string SidebandIdentity)
{
    public bool IsBound => ReplayEligible && new[] { ManifestVersion, ManifestHash,
        Extensions, DecoderEpoch, DecoderVersion, Privilege, Domain, AddressSpace,
        VectorConfiguration, ContextIncarnation, SidebandIdentity }
        .All(s => !string.IsNullOrWhiteSpace(s) && s != "unbound");
}

public sealed record BundleRequest(ulong Pc, int VirtualThread, ulong TransportSerial, int Cursor = 0)
{
    public void Validate()
    {
        if (VirtualThread is < 0 or > 3 || Cursor is < 0 or > 8 || Pc > ulong.MaxValue - 255)
            throw new ArgumentOutOfRangeException(nameof(BundleRequest));
    }
}
public readonly record struct FreshnessToken(ulong Incarnation, ulong Generation);
public sealed record LookupTag(ulong Pc, int VirtualThread, DecodeContext Context, FreshnessToken Freshness);

// Immutable byte strings are deep copies; consumers cannot recover a writable backing array.
public sealed class FrozenBytes : IEquatable<FrozenBytes>
{
    private readonly ImmutableArray<byte> bytes;
    public FrozenBytes(ReadOnlySpan<byte> value) => bytes = ImmutableArray.Create(value.ToArray());
    public int Length => bytes.Length;
    public byte[] Copy() => bytes.ToArray();
    internal ReadOnlySpan<byte> Span => bytes.AsSpan();
    public bool Equals(FrozenBytes? other) => other is not null && Span.SequenceEqual(other.Span);
    public override bool Equals(object? obj) => obj is FrozenBytes other && Equals(other);
    public override int GetHashCode() => Length; // equality is always full-byte equality
}
public sealed record Snapshot(string Kind, FrozenBytes Content);
public sealed record SemanticSlot(int SlotIndex, bool IsOccupied, uint Opcode,
    int? InstructionClass, int? SerializationClass, byte Rd, byte Rs1, byte Rs2,
    long Immediate, ushort? CsrAddress, bool AcquireOrdering, bool ReleaseOrdering,
    Snapshot RawSlot, Snapshot InstructionPayload, Snapshot SlotSideband,
    Snapshot StaticBinding, Snapshot ScalarLoadAddressPlan);
public sealed record TypedDecodeFailure(ushort Code, int SlotIndex, string Field, string RawHash, string Message);

// PC/transport serial/cursor are intentionally absent from reusable facts.
public sealed class DecodeResult
{
    private DecodeResult(int schema, FrozenBytes raw, ImmutableArray<SemanticSlot> slots,
        Snapshot? sideband, TypedDecodeFailure? failure)
    { Schema = schema; Raw = raw; Slots = slots; BundleSideband = sideband; Failure = failure; }
    public int Schema { get; }
    public FrozenBytes Raw { get; }
    public ImmutableArray<SemanticSlot> Slots { get; }
    public Snapshot? BundleSideband { get; }
    public TypedDecodeFailure? Failure { get; }
    public bool IsSuccess => Failure is null;
    public static DecodeResult Success(FrozenBytes raw, IEnumerable<SemanticSlot> slots, Snapshot sideband, int schema = 1)
    {
        var frozen = slots.ToImmutableArray();
        if (raw.Length != 256 || frozen.Length != 8 || frozen.Where((s, i) => s.SlotIndex != i || s.RawSlot.Content.Length != 32).Any())
            throw new ArgumentException("Full canonical bundle required.");
        for (int i = 0; i < 8; i++)
            if (!raw.Span.Slice(i * 32, 32).SequenceEqual(frozen[i].RawSlot.Content.Span))
                throw new InvalidOperationException("Oracle raw slot and bundle disagree.");
        return new(schema, raw, frozen, sideband, null);
    }
    public static DecodeResult Illegal(FrozenBytes raw, TypedDecodeFailure failure, int schema = 1)
    {
        if (raw.Length != 256 || failure.SlotIndex is < -1 or > 7) throw new ArgumentException("Invalid failure attribution.");
        return new(schema, raw, [], null, failure);
    }
    public bool SameFacts(DecodeResult other) => Schema == other.Schema && Raw.Equals(other.Raw) &&
        Failure == other.Failure && BundleSideband == other.BundleSideband && Slots.SequenceEqual(other.Slots);
}
public sealed class SemanticIdentity : IEquatable<SemanticIdentity>
{
    public SemanticIdentity(DecodeContext context, DecodeResult result)
    { Context = context; Result = result; }
    public DecodeContext Context { get; }
    public DecodeResult Result { get; }
    public bool Equals(SemanticIdentity? other) => other is not null && Context == other.Context && Result.SameFacts(other.Result);
    public override bool Equals(object? obj) => obj is SemanticIdentity other && Equals(other);
    public override int GetHashCode() => Context.GetHashCode();
}
public enum IngressOutcome { Cold, Warm, Hot, AccessFault, RetryExhausted }
public sealed record RetainedGraphAccounting(int Entries, int UniqueContexts, int UniqueStrings,
    long StringContentBytes, long EncodedContentBytes, long IntegrityContentBytes,
    long ReferenceSlots, long ModeledEntryBytes, string Scope);
public sealed record IngressResult(BundleRequest Placement, IngressOutcome Outcome, DecodeResult? Facts, string? AccessFault);
public sealed record AccessDecision(bool Allowed, string? Fault)
{
    public static AccessDecision Permit { get; } = new(true, null);
}
public sealed record CanonicalFetchResult(FrozenBytes? Raw, string? Fault);
public interface ICanonicalSource
{
    // Must validate translation/execute permission for ALL covered executable regions.
    AccessDecision CheckAccess(BundleRequest request, DecodeContext context);
    CanonicalFetchResult FetchFullBundle(BundleRequest request, DecodeContext context);
}
public interface ICanonicalDecoder
{
    // Only canonical oracle adapters implement this port. No Shadow opcode decoder.
    DecodeResult Decode(FrozenBytes raw, BundleRequest request, DecodeContext context);
}
