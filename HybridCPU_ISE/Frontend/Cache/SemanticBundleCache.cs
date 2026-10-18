using System.Security.Cryptography;
using System.Text.Json;
using System.Collections.Immutable;
using System.Diagnostics;

namespace HybridCpu.ShadowFrontend;

public enum ReplacementPolicy { Lru, QuotaBorrow }
public sealed record CacheSnapshot(bool Enabled, int ResidentBytes, ImmutableArray<int> Occupancy, TelemetrySnapshot Counters,
    ReplacementPolicy Policy, byte ActiveVirtualThreads, FreshnessSnapshot Freshness);
public sealed class SemanticBundleCache
{
    private sealed record Entry(LookupTag Tag, FrozenBytes Encoded, FrozenBytes Integrity, long LastUse, int Footprint);
    private readonly Entry?[] entries;
    private readonly int ways;
    private long clock;
    private readonly object ingressGate = new();
    private readonly FreshnessState freshness;
    private readonly Func<BundleRequest, DecodeContext> contextProvider;
    private readonly ICanonicalSource source;
    private readonly ICanonicalDecoder decoder;
    private readonly FrontendTelemetry telemetry = new();
    public TelemetrySnapshot Telemetry { get { FreshnessState.RejectCacheLockFromPublication(); lock (ingressGate) return telemetry.Snapshot(); } }
    private bool enabled = true;
    public bool Enabled { get { FreshnessState.RejectCacheLockFromPublication(); lock (ingressGate) return enabled; } set { FreshnessState.RejectCacheLockFromPublication(); lock (ingressGate) enabled = value; } }
    public int ByteBudget { get; }
    public byte ActiveVirtualThreads { get; private set; } = 15;
    public ReplacementPolicy Policy { get; private set; } = ReplacementPolicy.QuotaBorrow;
    public int Capacity => entries.Length;
    public int ResidentBytes { get { FreshnessState.RejectCacheLockFromPublication(); lock (ingressGate) return entries.Where(e => e is not null).Sum(e => e!.Footprint); } }
    public int[] Occupancy { get { FreshnessState.RejectCacheLockFromPublication(); lock (ingressGate) return Enumerable.Range(0, 4).Select(v => entries.Count(e => e?.Tag.VirtualThread == v)).ToArray(); } }
    public CacheSnapshot Snapshot()
    {
        FreshnessState.RejectCacheLockFromPublication();
        lock (ingressGate) lock (freshness.Gate)
            return new(enabled, ResidentBytes, Occupancy.ToImmutableArray(), telemetry.Snapshot(), Policy, ActiveVirtualThreads,freshness.Snapshot());
    }
    public SemanticBundleCache(FreshnessState freshness, Func<BundleRequest, DecodeContext> contextProvider,
        ICanonicalSource source, ICanonicalDecoder decoder, int capacity = 64, int ways = 4, int byteBudget = int.MaxValue)
    {
        if (capacity < ways || ways < 1 || capacity % ways != 0 || byteBudget <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        entries = new Entry[capacity]; this.ways = ways; ByteBudget = byteBudget;
        this.freshness = freshness; this.contextProvider = contextProvider; this.source = source; this.decoder = decoder;
    }
    public void Clear() { FreshnessState.RejectCacheLockFromPublication(); lock (ingressGate) Array.Clear(entries); }
    public void SetPolicy(ReplacementPolicy policy, byte activeVts)
    {
        FreshnessState.RejectCacheLockFromPublication();
        if (activeVts is 0 or > 15) throw new ArgumentOutOfRangeException(nameof(activeVts));
        lock (ingressGate) { Policy = policy; ActiveVirtualThreads = activeVts; Array.Clear(entries); }
    }
    public void SetActiveVirtualThreads(byte mask)
    {
        FreshnessState.RejectCacheLockFromPublication();
        if (mask is 0 or > 15) throw new ArgumentOutOfRangeException(nameof(mask));
        lock (ingressGate) ActiveVirtualThreads = mask;
    }
    public LookupTag CaptureTag(BundleRequest request)
    { request.Validate(); lock (freshness.Gate) return new(request.Pc, request.VirtualThread, contextProvider(request), freshness.Capture()); }
    public IngressResult Request(BundleRequest request, HotCandidate? candidate = null, HotInputs? hot = null, int maxRetries = 2)
    {
        FreshnessState.RejectCacheLockFromPublication();
        request.Validate(); if (maxRetries is < 0 or > 16) throw new ArgumentOutOfRangeException(nameof(maxRetries));
        long waitStart = Stopwatch.GetTimestamp();
        lock (ingressGate)
        {
            long acquired = Stopwatch.GetTimestamp();
            telemetry.HostIngressWaitTicks += acquired - waitStart;
            try
            {
            telemetry.Requests++;
            for (int retry = 0; retry <= maxRetries; retry++)
            {
                LookupTag tag = CaptureTag(request);
                telemetry.PermissionChecks++;
                AccessDecision access = source.CheckAccess(request, tag.Context);
                if (!access.Allowed)
                {
                    lock (freshness.Gate)
                    {
                        if (CaptureTag(request) != tag) { telemetry.Retries++; continue; }
                        return Fault(request, access);
                    }
                }
                DecodeResult? facts = null;
                IngressOutcome outcome = IngressOutcome.Cold;
                byte[]? fill = null;
                if (Enabled && candidate is not null && hot is not null)
                {
                    telemetry.HotCandidates++;
                    telemetry.TagGuardBytesRead += TagBytes(candidate.Tag) + JsonSerializer.SerializeToUtf8Bytes(candidate.Binding).Length;
                    HotGuard? reject = hot.Current.Cursor != request.Cursor ? HotGuard.Cursor :
                        HotCompatibility.Reject(candidate, tag, freshness.ReplayGeneration, hot, g => telemetry.HotGuardChecks++);
                    if (hot.ExpectedIdentity is not null)
                        telemetry.HotIdentityBytesRead += IdentityBytes(candidate.Identity) + IdentityBytes(hot.ExpectedIdentity);
                    if (reject is null)
                    { facts = candidate.Identity.Result; outcome = IngressOutcome.Hot; }
                    else telemetry.GuardRejects[reject.Value] = telemetry.GuardRejects.GetValueOrDefault(reject.Value) + 1;
                }
                if (facts is null && Enabled && tag.Context.IsBound)
                {
                    telemetry.DbcLookups++;
                    int start = SetStart(tag.Pc);
                    for (int i = start; i < start + ways; i++)
                    {
                        Entry? entry = entries[i];
                        if (entry is null) continue;
                        // Stored fill-time serialized tag size: do not serialize foreign-way tags merely for telemetry.
                        int storedTagBytes = entry.Footprint - entry.Encoded.Length - 32 - 16;
                        telemetry.TagGuardBytesRead += storedTagBytes;
                        // Off-key entries cannot serve. Authenticate a potential match before restore.
                        if (entry.Tag != tag) continue;
                        telemetry.LookupIntegrityChecks++;
                        telemetry.IntegrityBytesRead += entry.Encoded.Length + entry.Integrity.Length + storedTagBytes;
                        if (!EntryIntegrity(entry.Tag, entry.Encoded.Span).AsSpan().SequenceEqual(entry.Integrity.Span))
                        { entries[i] = null; telemetry.CorruptEntries++; continue; }
                        telemetry.RestoreAttempts++;
                        if (!CompactCodec.TryDecode(entry.Encoded.Span, out facts))
                        { entries[i] = null; facts = null; telemetry.CorruptEntries++; break; }
                        telemetry.SemanticPayloadBytesRead += entry.Encoded.Length;
                        entries[i] = entry with { LastUse = Tick() };
                        outcome = IngressOutcome.Warm; break;
                    }
                }
                if (facts is null)
                {
                    telemetry.SemanticMisses++; telemetry.MissesByVt[request.VirtualThread]++;
                    telemetry.FetchAttempts++;
                    CanonicalFetchResult fetched = source.FetchFullBundle(request, tag.Context);
                    if (fetched.Fault is not null)
                    {
                        lock (freshness.Gate)
                        {
                            if (CaptureTag(request) != tag) { telemetry.Retries++; continue; }
                            return Fault(request, new(false, fetched.Fault));
                        }
                    }
                    FrozenBytes raw = fetched.Raw ?? throw new InvalidOperationException("Canonical source returned neither raw bytes nor fault.");
                    if (raw.Length != 256) throw new InvalidOperationException("Canonical source violated full-bundle contract.");
                    telemetry.RawBundleFetches++; telemetry.CanonicalBytesRead += 256; telemetry.DecoderCalls++;
                    facts = decoder.Decode(raw, request, tag.Context);
                    if (!facts.Raw.Equals(raw)) throw new InvalidOperationException("Canonical decoder raw identity mismatch.");
                    if (Enabled && tag.Context.IsBound)
                    { fill = CompactCodec.Encode(facts); if (fill is null) telemetry.UnsupportedFills++; }
                }
                lock (freshness.Gate)
                {
                    // All committed provider changes must be ordered under FreshnessState.Gate.
                    // The second permission check is deliberate; permission faults never fill DBC.
                    telemetry.PermissionChecks++;
                    access = source.CheckAccess(request, tag.Context);
                    bool changed = CaptureTag(request) != tag ||
                        (outcome == IngressOutcome.Hot && candidate!.ReplayGeneration != freshness.ReplayGeneration);
                    if (changed) { telemetry.Retries++; continue; }
                    if (!access.Allowed) return Fault(request, access);
                    if (fill is not null) Fill(tag, fill);
                    switch (outcome)
                    {
                        case IngressOutcome.Cold: telemetry.ColdIngress++; break;
                        case IngressOutcome.Warm: telemetry.SemanticHits++; telemetry.HitsByVt[request.VirtualThread]++; telemetry.DecodeBypassCount++; break;
                        case IngressOutcome.Hot: telemetry.ValidatedHotHits++; telemetry.DecodeBypassCount++; break;
                    }
                    if (!facts.IsSuccess) telemetry.DecodeFaults++;
                    return new(request, outcome, facts, null);
                }
            }
            telemetry.RetryExhausted++;
            return new(request, IngressOutcome.RetryExhausted, null, null);
            }
            finally { telemetry.HostIngressHoldTicks += Stopwatch.GetTimestamp() - acquired; }
        }
    }
    private IngressResult Fault(BundleRequest r, AccessDecision a)
    { telemetry.AccessFaults++; return new(r, IngressOutcome.AccessFault, null, a.Fault); }
    private long Tick()
    {
        if (clock == long.MaxValue) { Array.Clear(entries); clock = 0; }
        return ++clock;
    }
    private int SetStart(ulong pc) => (int)((pc / 256) % (ulong)(entries.Length / ways)) * ways;
    public static int TagBytes(LookupTag tag) => JsonSerializer.SerializeToUtf8Bytes(tag).Length;
    private static long IdentityBytes(SemanticIdentity identity) => identity.Result.Raw.Length +
        (identity.Result.BundleSideband?.Content.Length ?? 0) + identity.Result.Slots.Sum(s =>
            (long)s.RawSlot.Content.Length + s.InstructionPayload.Content.Length + s.SlotSideband.Content.Length +
            s.StaticBinding.Content.Length + s.ScalarLoadAddressPlan.Content.Length);
    public RetainedGraphAccounting RetainedGraph()
    {
        FreshnessState.RejectCacheLockFromPublication();
        lock (ingressGate)
        {
            var live = entries.Where(e => e is not null).Select(e => e!).ToArray();
            var contexts = live.Select(e => e.Tag.Context).Distinct<DecodeContext>(ReferenceEqualityComparer.Instance).ToArray();
            var strings = new HashSet<string>(ReferenceEqualityComparer.Instance);
            foreach (DecodeContext c in contexts) foreach (var p in typeof(DecodeContext).GetProperties())
                if (p.PropertyType == typeof(string)) strings.Add((string)p.GetValue(c)!);
            return new(live.Length, contexts.Length, strings.Count, strings.Sum(s => 2L*s.Length),
                live.Sum(e => (long)e.Encoded.Length), live.Sum(e => (long)e.Integrity.Length),
                live.Length * 6L + contexts.Length * 11L + entries.Length, ResidentBytes,
                "Reachable cache entry/tag/context/string/FrozenBytes/backing-array inventory; UTF16/byte content and modeled reference slots. CLR headers/alignment and shared provider/telemetry/delegates excluded; not exact heap bytes or SRAM.");
        }
    }
    private static byte[] EntryIntegrity(LookupTag tag, ReadOnlySpan<byte> payload)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(tag));
        hash.AppendData(payload);
        return hash.GetHashAndReset();
    }
    private int Victim(IEnumerable<int> indices, int requestor)
    {
        int[] candidates = indices.ToArray();
        int empty = Array.FindIndex(candidates, i => entries[i] is null);
        if (empty >= 0) return candidates[empty];
        int[] occupancy = Occupancy;
        int active = Enumerable.Range(0, 4).Count(v => (ActiveVirtualThreads & (1 << v)) != 0);
        int quota = entries.Length / active;
        if (Policy == ReplacementPolicy.QuotaBorrow)
        {
            var preferred = candidates.Where(i =>
                (ActiveVirtualThreads & (1 << entries[i]!.Tag.VirtualThread)) == 0 ||
                occupancy[entries[i]!.Tag.VirtualThread] > quota).ToArray();
            if (preferred.Length > 0) candidates = preferred;
            else
            {
                var own = candidates.Where(i => entries[i]!.Tag.VirtualThread == requestor).ToArray();
                if (own.Length > 0) candidates = own;
            }
        }
        return candidates.OrderBy(i => entries[i]!.LastUse).ThenBy(i => i).First();
    }
    private void Evict(int index)
    { if (entries[index] is { } old) telemetry.EvictionsByVt[old.Tag.VirtualThread]++; entries[index] = null; }
    private void Fill(LookupTag tag, byte[] encoded)
    {
        int footprint = TagBytes(tag) + encoded.Length + 32 + 16; // integrity + replacement bookkeeping
        if (footprint > ByteBudget) { telemetry.UnsupportedFills++; return; }
        int start = SetStart(tag.Pc), index = Victim(Enumerable.Range(start, ways), tag.VirtualThread);
        Evict(index);
        while (ResidentBytes + footprint > ByteBudget)
            Evict(Victim(Enumerable.Range(0, entries.Length).Where(i => entries[i] is not null), tag.VirtualThread));
        entries[index] = new(tag, new(encoded), new(EntryIntegrity(tag, encoded)), Tick(), footprint);
        telemetry.Fills++;
        telemetry.FillBytesWritten += footprint;
    }
    // Controlled corruption experiment; no writable serving payload is exposed.
    public bool CorruptForExperiment(ulong pc)
    {
        FreshnessState.RejectCacheLockFromPublication();
        lock (ingressGate)
        {
            int i = Array.FindIndex(entries, e => e?.Tag.Pc == pc);
            if (i < 0) return false;
            Entry old = entries[i]!; byte[] bad = old.Encoded.Copy(); bad[^1] ^= 0x80;
            entries[i] = old with { Encoded = new(bad) }; return true;
        }
    }
    public bool CorruptTagForExperiment(ulong pc)
    {
        FreshnessState.RejectCacheLockFromPublication();
        lock (ingressGate)
        {
            int i = Array.FindIndex(entries, e => e?.Tag.Pc == pc);
            if (i < 0) return false;
            Entry old = entries[i]!;
            entries[i] = old with { Tag = old.Tag with { VirtualThread = (old.Tag.VirtualThread + 1) % 4 } };
            return true;
        }
    }
}
