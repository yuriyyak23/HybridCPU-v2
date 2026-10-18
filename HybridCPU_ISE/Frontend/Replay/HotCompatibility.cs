namespace HybridCpu.ShadowFrontend;

public enum HotGuard { Freshness, Integrity, Structural, Certificate, Owner, Context, Domain,
    Boundary, Serializing, ReplayPhase, Cursor, Legality, Capacity, Hazard, Scoreboard,
    Backpressure, Speculation, Assist, Fsp, Vdsa }
public sealed record HotBinding(string Structural, string Certificate, string Owner, string Boundary,
    ulong Serializing, ulong Phase, int Cursor);
public sealed record HotCandidate(LookupTag Tag, SemanticIdentity Identity, HotBinding Binding, ulong ReplayGeneration);
public sealed record HotInputs(HotBinding Current, Func<HotGuard, bool> CurrentCheck, SemanticIdentity? ExpectedIdentity = null);

public static class HotCompatibility
{
    // Consumes an externally supplied immutable existing replay observation. No store,
    // certificate issuance, authority, placement or issued-attempt allocation lives here.
    public static HotGuard? Reject(HotCandidate candidate, LookupTag current, ulong replayGeneration, HotInputs inputs, Action<HotGuard>? observe = null)
    {
        if (!current.Context.IsBound || candidate.Tag.Freshness != current.Freshness || candidate.Tag.Pc != current.Pc) return HotGuard.Freshness;
        if (!candidate.Identity.Result.IsSuccess || candidate.Identity.Context != candidate.Tag.Context ||
            inputs.ExpectedIdentity is null || !candidate.Identity.Equals(inputs.ExpectedIdentity)) return HotGuard.Integrity;
        if (!Bound(candidate.Binding.Structural) || !Bound(inputs.Current.Structural) || candidate.Binding.Structural != inputs.Current.Structural) return HotGuard.Structural;
        if (!Bound(candidate.Binding.Certificate) || !Bound(inputs.Current.Certificate) || candidate.Binding.Certificate != inputs.Current.Certificate) return HotGuard.Certificate;
        if (!Bound(candidate.Binding.Owner) || !Bound(inputs.Current.Owner) || candidate.Tag.VirtualThread != current.VirtualThread || candidate.Binding.Owner != inputs.Current.Owner) return HotGuard.Owner;
        if (candidate.Tag.Context.Domain != current.Context.Domain) return HotGuard.Domain;
        if (candidate.Tag.Context != current.Context) return HotGuard.Context;
        if (!Bound(candidate.Binding.Boundary) || !Bound(inputs.Current.Boundary) || candidate.Binding.Boundary != inputs.Current.Boundary) return HotGuard.Boundary;
        if (candidate.Binding.Serializing != inputs.Current.Serializing) return HotGuard.Serializing;
        if (candidate.ReplayGeneration != replayGeneration || candidate.Binding.Phase != inputs.Current.Phase) return HotGuard.ReplayPhase;
        if (candidate.Binding.Cursor != inputs.Current.Cursor) return HotGuard.Cursor;
        foreach (HotGuard guard in Enum.GetValues<HotGuard>()) { observe?.Invoke(guard); if (!inputs.CurrentCheck(guard)) return guard; }
        return null;
    }
    private static bool Bound(string value) => !string.IsNullOrWhiteSpace(value) && value != "unbound";
}
