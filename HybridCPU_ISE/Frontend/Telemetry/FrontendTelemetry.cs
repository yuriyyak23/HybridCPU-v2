using System.Collections.Immutable;
namespace HybridCpu.ShadowFrontend;

public sealed class FrontendTelemetry
{
    public long Requests, ColdIngress, SemanticHits, SemanticMisses, HotCandidates, ValidatedHotHits;
    public long RawBundleFetches, DecoderCalls, CanonicalBytesRead, SemanticPayloadBytesRead;
    public long FetchAttempts;
    public long Fills;
    public long PermissionChecks, HotGuardChecks, RestoreAttempts, HotIdentityBytesRead;
    public long DbcLookups, LookupIntegrityChecks, HostIngressWaitTicks, HostIngressHoldTicks;
    public long TagGuardBytesRead, IntegrityBytesRead, FillBytesWritten, DecodeBypassCount;
    public long Retries, RetryExhausted, AccessFaults, DecodeFaults, UnsupportedFills, CorruptEntries;
    public long[] HitsByVt = new long[4], MissesByVt = new long[4], EvictionsByVt = new long[4];
    public Dictionary<HotGuard, long> GuardRejects { get; private set; } = new();
    public TelemetrySnapshot Snapshot() => new(this);
    public long Completed => ColdIngress + SemanticHits + ValidatedHotHits;
    public void VerifyAccounting()
    {
        if (Requests != Completed + AccessFaults + RetryExhausted ||
            DecodeBypassCount != SemanticHits + ValidatedHotHits ||
            RawBundleFetches != DecoderCalls || CanonicalBytesRead != 256 * RawBundleFetches ||
            HitsByVt.Sum() != SemanticHits || DecodeFaults > Completed || Fills > DecoderCalls || Fills < 0 || FetchAttempts < RawBundleFetches)
            throw new InvalidOperationException("Frontend accounting invariant failed.");
    }
}

// Read-only observation; no live mutable arrays or dictionaries escape the cache.
public sealed class TelemetrySnapshot
{
    private readonly ImmutableDictionary<string,long> values;
    public ImmutableArray<long> HitsByVt { get; }
    public ImmutableArray<long> MissesByVt { get; }
    public ImmutableArray<long> EvictionsByVt { get; }
    public ImmutableDictionary<HotGuard,long> GuardRejects { get; }
    internal TelemetrySnapshot(FrontendTelemetry source)
    {
        values = typeof(FrontendTelemetry).GetFields().Where(f => f.FieldType == typeof(long))
            .ToImmutableDictionary(f => f.Name, f => (long)f.GetValue(source)!);
        HitsByVt=source.HitsByVt.ToImmutableArray(); MissesByVt=source.MissesByVt.ToImmutableArray();
        EvictionsByVt=source.EvictionsByVt.ToImmutableArray(); GuardRejects=source.GuardRejects.ToImmutableDictionary();
    }
    public object Deterministic() => new { Scalars=values.Remove(nameof(HostIngressWaitTicks)).Remove(nameof(HostIngressHoldTicks)), HitsByVt,MissesByVt,EvictionsByVt,GuardRejects };
    public long Completed => ColdIngress+SemanticHits+ValidatedHotHits;
    public void VerifyAccounting()
    {
        if(Requests!=Completed+AccessFaults+RetryExhausted || DecodeBypassCount!=SemanticHits+ValidatedHotHits ||
            RawBundleFetches!=DecoderCalls || CanonicalBytesRead!=256*RawBundleFetches || HitsByVt.Sum()!=SemanticHits ||
            DecodeFaults>Completed || Fills>DecoderCalls || Fills<0 || FetchAttempts<RawBundleFetches)
            throw new InvalidOperationException("Frontend accounting invariant failed.");
    }
    public long Requests => values["Requests"];
    public long ColdIngress => values["ColdIngress"];
    public long SemanticHits => values["SemanticHits"];
    public long SemanticMisses => values["SemanticMisses"];
    public long HotCandidates => values["HotCandidates"];
    public long ValidatedHotHits => values["ValidatedHotHits"];
    public long RawBundleFetches => values["RawBundleFetches"];
    public long DecoderCalls => values["DecoderCalls"];
    public long CanonicalBytesRead => values["CanonicalBytesRead"];
    public long SemanticPayloadBytesRead => values["SemanticPayloadBytesRead"];
    public long FetchAttempts => values["FetchAttempts"];
    public long Fills => values["Fills"];
    public long PermissionChecks => values["PermissionChecks"];
    public long HotGuardChecks => values["HotGuardChecks"];
    public long RestoreAttempts => values["RestoreAttempts"];
    public long HotIdentityBytesRead => values["HotIdentityBytesRead"];
    public long DbcLookups => values["DbcLookups"];
    public long LookupIntegrityChecks => values["LookupIntegrityChecks"];
    public long HostIngressWaitTicks => values["HostIngressWaitTicks"];
    public long HostIngressHoldTicks => values["HostIngressHoldTicks"];
    public long TagGuardBytesRead => values["TagGuardBytesRead"];
    public long IntegrityBytesRead => values["IntegrityBytesRead"];
    public long FillBytesWritten => values["FillBytesWritten"];
    public long DecodeBypassCount => values["DecodeBypassCount"];
    public long Retries => values["Retries"];
    public long RetryExhausted => values["RetryExhausted"];
    public long AccessFaults => values["AccessFaults"];
    public long DecodeFaults => values["DecodeFaults"];
    public long UnsupportedFills => values["UnsupportedFills"];
    public long CorruptEntries => values["CorruptEntries"];
}
