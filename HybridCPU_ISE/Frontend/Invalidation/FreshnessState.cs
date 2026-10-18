using System.Collections.Immutable;
namespace HybridCpu.ShadowFrontend;

public enum FrontendEvent { ExecutableWrite, FenceI, Reset, Restore, Context, Domain, Privilege,
    Configuration, Sideband, Policy, Redirect, Trap, Rollback, ReplayPhase, Serializing }
public sealed record FreshnessSnapshot(FreshnessToken Token, ulong ReplayGeneration, ulong CommittedOrder,
    bool Poisoned, ImmutableDictionary<FrontendEvent,long> Invalidations);
public sealed class FreshnessState
{
    internal object Gate { get; } = new();
    private ulong incarnation = 1, generation, replayGeneration, committedOrder;
    private bool poisoned;
    [ThreadStatic] private static int publicationDepth;
    internal static void RejectCacheLockFromPublication()
    {
        if (publicationDepth != 0) throw new InvalidOperationException("Publication callbacks cannot enter cache APIs; lock order is ingress then freshness.");
    }
    private void EnsureUsable() { if (poisoned) throw new InvalidOperationException("Frontend publication failed; this freshness state is permanently poisoned."); }
    public FreshnessToken Capture() { lock (Gate) { EnsureUsable(); return new(incarnation, generation); } }
    public ulong ReplayGeneration { get { lock (Gate) { EnsureUsable(); return replayGeneration; } } }
    private readonly Dictionary<FrontendEvent, long> invalidations = new();
    public ImmutableDictionary<FrontendEvent,long> Invalidations { get { lock (Gate) return invalidations.ToImmutableDictionary(); } }
    public FreshnessSnapshot Snapshot() { lock (Gate) return new(new(incarnation,generation),replayGeneration,committedOrder,poisoned,invalidations.ToImmutableDictionary()); }
    public void Apply(FrontendEvent kind, ulong order, bool committed = true, Action? publish = null)
    {
        if (!committed) return;
        lock (Gate)
        {
            EnsureUsable();
            if (order <= committedOrder) throw new InvalidOperationException("Committed events require strictly ordered publication.");
            bool completed = false;
            try
            {
            publicationDepth++;
            try { publish?.Invoke(); } // No cache lock is allowed from a publication callback.
            finally { publicationDepth--; }
            committedOrder = order;
            invalidations[kind] = invalidations.GetValueOrDefault(kind) + 1;
            // Redirects invalidate only transient HOT evidence, not WARM semantics.
            if (kind is not (FrontendEvent.Redirect or FrontendEvent.Trap or FrontendEvent.Rollback or FrontendEvent.ReplayPhase or FrontendEvent.Serializing))
                Advance(ref generation);
            Advance(ref replayGeneration);
            completed = true;
            }
            finally { if (!completed) poisoned = true; } // Preserve the original exception; never serve partial publication.
        }
    }
    private void Advance(ref ulong value)
    {
        if (value == ulong.MaxValue)
        { incarnation = checked(incarnation + 1); generation = 0; replayGeneration = 0; }
        else value++;
    }
    // Explicit model initialization for overflow experiments, never a restore API.
    public static FreshnessState OverflowExperiment() => new() { generation = ulong.MaxValue, replayGeneration = ulong.MaxValue };
    // New isolated model lifetime only; never restore an existing cache or poisoned state.
    public static FreshnessState ObservationExperiment(FreshnessToken token, ulong replay, ulong order)
    {
        if (token.Incarnation == 0) throw new ArgumentOutOfRangeException(nameof(token));
        return new() { incarnation=token.Incarnation, generation=token.Generation, replayGeneration=replay, committedOrder=order };
    }
}
