using System;

namespace YAKSys_Hybrid_CPU.Core.Decoder;

public enum ReplaySemanticShadowObservation : byte
{
    Miss = 0,
    EquivalentHit = 1,
    ContentMismatch = 2,
}

public readonly record struct ReplaySemanticShadowMetrics(
    ulong Observations,
    ulong Misses,
    ulong EquivalentHits,
    ulong ContentMismatches,
    ulong Invalidations)
{
    public ulong AccountedObservations => Misses + EquivalentHits + ContentMismatches;
}

/// <summary>
/// Bounded one-entry RF-09 shadow observer. It never returns cached canonical
/// content and therefore cannot become a production decode or issue source.
/// </summary>
internal sealed class ReplaySemanticShadowLookup
{
    private ReplayEntry? _entry;
    private ulong _observations;
    private ulong _misses;
    private ulong _equivalentHits;
    private ulong _contentMismatches;
    private ulong _invalidations;

    public ReplaySemanticShadowObservation ObserveLiveDecode(
        CanonicalBundle liveCanonicalBundle)
    {
        ArgumentNullException.ThrowIfNull(liveCanonicalBundle);
        ReplayEntry liveEntry = ReplayEntry.Create(liveCanonicalBundle);
        _observations = checked(_observations + 1UL);

        ReplayEntry? cachedEntry = _entry;
        if (cachedEntry == null ||
            !cachedEntry.SemanticKey.Equals(liveEntry.SemanticKey))
        {
            _misses = checked(_misses + 1UL);
            _entry = liveEntry;
            return ReplaySemanticShadowObservation.Miss;
        }

        if (cachedEntry.HasSameFrozenSemanticContent(liveEntry))
        {
            _equivalentHits = checked(_equivalentHits + 1UL);
            return ReplaySemanticShadowObservation.EquivalentHit;
        }

        _contentMismatches = checked(_contentMismatches + 1UL);
        _entry = liveEntry;
        return ReplaySemanticShadowObservation.ContentMismatch;
    }

    public void Invalidate()
    {
        if (_entry != null)
        {
            _entry = null;
            _invalidations = checked(_invalidations + 1UL);
        }
    }

    public ReplaySemanticShadowMetrics Metrics => new(
        _observations,
        _misses,
        _equivalentHits,
        _contentMismatches,
        _invalidations);

#if TESTING
    internal bool TestHasEntry => _entry != null;
#endif
}
