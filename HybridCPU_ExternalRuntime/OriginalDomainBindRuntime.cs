using HybridCPU.ExternalRuntime.Contracts;
namespace HybridCPU.ExternalRuntime;
public sealed partial class HybridCpuExternalRuntime : IHybridCpuOriginalDomainBindRuntimeV1, IHybridCpuOriginalDomainBindRetirementRuntimeV1
{
    private sealed class OriginalDomainBindFact : ExternalOriginalDomainBindRequest
    {
        public OriginalDomainBindFact(HybridCpuExternalRuntime source, ExternalDomainBindRequest request) : base(request)
        { OriginalOwner = this; Source = source; Version = source.manifest.ContractVersion; Generation = source.manifest.Generation; }
        public OriginalDomainBindFact OriginalOwner { get; }
        public HybridCpuExternalRuntime Source { get; }
        public HybridCpuExternalContractVersion Version { get; }
        public ulong Generation { get; }
        public DomainRecord? Record { get; set; }
        public BackendBindResult? BackendRaw { get; set; }
        public ExternalOriginalDomainBindObservation? Observation { get; set; }
        public bool Invoked, InFlight, FactoryStarted, Retired, OperationReserved;
    }
    private readonly object originalDomainBindSync = new();
    // Routing to the same provider admission facts, never a second authority/ledger.
    private readonly Dictionary<ExternalDomainBindRequest, OriginalDomainBindFact> originalDomainBinds = new(ReferenceEqualityComparer.Instance);
    private readonly List<OriginalDomainBindFact> originalDomainBindFacts = [];
    private bool HasOriginalDomainBindSource(OriginalDomainBindFact fact) =>
        ReferenceEquals(fact.OriginalOwner, fact) && ReferenceEquals(fact.Source, this) &&
        originalDomainBindFacts.Contains(fact) &&
        originalDomainBinds.TryGetValue(fact.Request, out var original) && ReferenceEquals(original, fact) &&
        fact.Version == manifest.ContractVersion && fact.Generation == manifest.Generation;
    public ExternalOriginalDomainBindRequest? PrepareOriginalDomainBind(ExternalDomainBindRequest request)
    {
        if (request is null || request.RequestId == Guid.Empty || !Enum.IsDefined(request.Profile) || !Valid(request.Operation)) return null;
        lock (originalDomainBindSync)
        {
            if (originalDomainBinds.TryGetValue(request, out var saved)) return HasOriginalDomainBindSource(saved) ? saved : null;
            // Surviving original owner is not reconstructed when its route/history is lost.
            if (originalDomainBindFacts.Any(f => ReferenceEquals(f.Request, request) || f.Request.RequestId == request.RequestId)) return null;
            var fact = new OriginalDomainBindFact(this, request) { OperationReserved = TryReserveOperation(request.Operation) };
            originalDomainBindFacts.Add(fact); originalDomainBinds.Add(request, fact); return fact;
        }
    }
    public ExternalOriginalDomainBindObservation? BindOriginalDomain(ExternalOriginalDomainBindRequest request)
    {
        if (request is not OriginalDomainBindFact fact) return null;
        lock (originalDomainBindSync)
        {
            if (!HasOriginalDomainBindSource(fact) || fact.Invoked || fact.InFlight || fact.Retired) return null;
            fact.Invoked = fact.InFlight = true; // Captured producing source fenced before factory/backend effects.
        }
        try
        {
            var raw = BindDomainCore(fact.Request, fact);
            lock (originalDomainBindSync)
                return fact.Observation ??= new(fact, raw, !fact.FactoryStarted && raw.Receipt is null);
        }
        catch (Exception exception) when (exception is not StackOverflowException)
        {
            lock (originalDomainBindSync)
                return fact.Observation ??= new(fact, new(ExternalRuntimeOutcome.Unknown, null, "Original Bind interrupted: " + exception.Message), false);
        }
        finally { lock (originalDomainBindSync) fact.InFlight = false; }
    }
    public ExternalOriginalDomainBindObservation? ResolveOriginalDomainBind(ExternalOriginalDomainBindRequest request)
    {
        if (request is not OriginalDomainBindFact fact) return null;
        lock (originalDomainBindSync)
        {
            if (!HasOriginalDomainBindSource(fact) || fact.InFlight || fact.Observation is not { } observation || !ReferenceEquals(observation.Request, fact)) return null;
            if (observation.NoEffect) return observation.Raw.Receipt is null ? observation : null;
            if (fact.Record is not { } record || !ReferenceEquals(record.OriginalDomainBind, fact) ||
                !TryFind(record.Lease, out var original, out _, out _) || !ReferenceEquals(original, record)) return null;
            if (observation.Raw.Outcome == ExternalRuntimeOutcome.Bound && !ReferenceEquals(record.OriginalAdmission, observation.Raw.Receipt)) return null;
            return observation;
        }
    }
    public ExternalOriginalDomainBindObservation? RetireOriginalDomainBind(ExternalOriginalDomainBindRequest request)
    {
        if (request is not OriginalDomainBindFact fact) return null;
        lock (originalDomainBindSync)
        {
            if (!HasOriginalDomainBindSource(fact) || fact.InFlight) return null;
            if (fact.Invoked || fact.Retired) return ResolveOriginalDomainBind(fact); // Pure known original outcome only.
            fact.Retired = true;
            return fact.Observation = new(fact,
                new(ExternalRuntimeOutcome.Denied, null, "Original prepared Bind retired before factory/backend invocation."), true);
        }
    }
}
