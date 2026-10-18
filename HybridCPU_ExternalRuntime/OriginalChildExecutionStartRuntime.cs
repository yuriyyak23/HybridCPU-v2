using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime;

public sealed partial class HybridCpuExternalRuntime
{
    private sealed class OriginalExecutionStartFact(ExternalOriginalChildExecutionStartRequest? original,
        ExternalChildExecutionStartRequest request, ChildRecord child, GuestMappingRecord mapping,
        ExternalChildArtifactBindReceipt artifact, CpuBackedChildExecution execution)
    {
        public OriginalExecutionStartFact? OwnerSelf { get; private set; }
        public void EstablishSelf() => OwnerSelf = this;
        public ExternalChildDomainCreateReceipt? ChildAdmission { get; } = child.OriginalAdmission;
        public ExternalGuestMemoryMapReceipt? MappingAdmission { get; } = mapping.OriginalAdmission;
        public ExternalOriginalChildExecutionStartRequest? Original { get; } = original;
        public ExternalChildExecutionStartRequest Request { get; } = request;
        public ChildRecord Child { get; } = child;
        public GuestMappingRecord Mapping { get; } = mapping;
        public ExternalChildArtifactBindReceipt Artifact { get; } = artifact;
        public CpuBackedChildExecution Execution { get; } = execution;
        public ExternalChildResult<ExternalChildExecutionReceipt>? Observation { get; set; }
        public ExternalOriginalExecutionTerminalObservation? TerminalObservation { get; set; }
        public bool NoEffect { get; set; }
    }

    private bool TryReserveExecutionGeneration(out ulong generation)
    {
        generation = 0;
        while (true)
        {
            var current = Volatile.Read(ref nextExecutionGeneration);
            if (current < 0 || current == long.MaxValue) return false;
            var next = current + 1;
            if (Interlocked.CompareExchange(ref nextExecutionGeneration, next, current) == current)
            { generation = (ulong)next; return true; }
        }
    }
    private static bool OriginalStartRefusalsIntact(ChildRecord child)
    {
        if ((ulong)child.OriginalStartRefusals.Count != child.StartRefusalsIssued) return false;
        var facts = new HashSet<OriginalExecutionStartFact>(ReferenceEqualityComparer.Instance);
        var originals = new HashSet<ExternalOriginalChildExecutionStartRequest>(ReferenceEqualityComparer.Instance);
        var requests = new HashSet<ExternalChildExecutionStartRequest>(ReferenceEqualityComparer.Instance);
        var operations = new HashSet<ExternalOperationIdentity>();
        foreach (var fact in child.OriginalStartRefusals)
        {
            if (fact is null || fact.Original is null || fact.Request is null ||
                !ReferenceEquals(fact.OwnerSelf, fact) || !ReferenceEquals(fact.Child, child) ||
                !fact.NoEffect || fact.Observation is null || !ReferenceEquals(fact.Original.Start, fact.Request) ||
                !facts.Add(fact) || !originals.Add(fact.Original) || !requests.Add(fact.Request) ||
                !operations.Add(fact.Request.Operation)) return false;
        }
        return true;
    }
    private bool OriginalStartFactSources(ChildRecord child, OriginalExecutionStartFact fact) =>
        HasOriginalNestedGuestSources(child) && ReferenceEquals(fact.OwnerSelf, fact) && ReferenceEquals(fact.Child, child) &&
        ReferenceEquals(child.OriginalOwner, child) && fact.ChildAdmission is not null && fact.MappingAdmission is not null &&
        ReferenceEquals(fact.ChildAdmission, child.OriginalAdmission) && ReferenceEquals(fact.MappingAdmission, fact.Mapping.OriginalAdmission) &&
        TryFind(child.Lease.Parent, out var parent, out _, out _) && ReferenceEquals(parent, child.ParentSource) &&
        ReferenceEquals(child.ArtifactAdmissionOwner, child) && ReferenceEquals(child.ArtifactAdmissionMapping, fact.Mapping) &&
        ReferenceEquals(child.Artifact, fact.Artifact) &&
        child.Mappings.TryGetValue(fact.Mapping.Lease.Handle, out var owned) && ReferenceEquals(owned, fact.Mapping) &&
        TryFindMapping(fact.Mapping.Lease, out var current, out _, out _) && ReferenceEquals(current, fact.Mapping);

    public ExternalOriginalChildExecutionStartObservation? StartOriginalChildExecution(ExternalOriginalChildExecutionStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Start is null || request.Artifact is null || !TryFindChild(request.Child, out var child, out _, out _)) return null;
        if (!TryGuestMapLineage(child!, out var lineage)) return null;
        lock (child!.ParentSource.Sync)
        return WithNestedLineage<ExternalOriginalChildExecutionStartObservation?>(lineage, 0, () =>
        {
            if (!HasOriginalNestedGuestSources(child)) return null;
            if (!OriginalStartRefusalsIntact(child)) return null;
            if (child.OriginalStartRefusals.Any(fact => ReferenceEquals(fact.Original, request)))
                return ResolveOriginalChildExecutionStart(request); // Same completed no-effect; no fresh admission or invocation.
            if (child.OriginalStartRefusals.Any(fact => ReferenceEquals(fact.Request, request.Start) || fact.Request.Operation == request.Start.Operation))
                return null; // Equal/copy correlation cannot replace an original refusal.
            if (child.StartRefusalsIssued == ulong.MaxValue) return null;
            child.OriginalStartRefusals.EnsureCapacity(child.OriginalStartRefusals.Count + 1);
            var raw = StartChildExecutionCore(request.Child, request.Start, request, out var noEffect);
            if (!noEffect) return ResolveOriginalChildExecutionStart(request);
            if (child.Artifact is { } artifact && ReferenceEquals(artifact, request.Artifact) && child.Execution is { } execution &&
                child.ArtifactBind is { } bind && ReferenceEquals(bind.OwnerSelf, bind) &&
                ReferenceEquals(bind.ChildAdmission, child.OriginalAdmission) && bind.ChildAdmission is not null &&
                bind.MappingAdmission is not null && ReferenceEquals(bind.MappingAdmission, bind.Mapping.OriginalAdmission))
            {
                var refusal = new OriginalExecutionStartFact(request, request.Start, child, bind.Mapping, artifact, execution)
                    { Observation = raw, NoEffect = true };
                refusal.EstablishSelf();
                if (OriginalStartFactSources(child, refusal))
                {
                    child.OriginalStartRefusals.Add(refusal);
                    child.StartRefusalsIssued++;
                }
            }
            return new(request, raw, true); // Explicit zero effect, independent from backing lifetime release.
        });
    }


    public ExternalOriginalChildExecutionStartObservation? ResolveOriginalChildExecutionStart(ExternalOriginalChildExecutionStartRequest originalRequest)
    {
        if (originalRequest is null || !TryFindChild(originalRequest.Child, out var child, out _, out _)) return null;
        if (!TryGuestMapLineage(child!, out var lineage)) return null;
        lock (child!.ParentSource.Sync)
        return WithNestedLineage<ExternalOriginalChildExecutionStartObservation?>(lineage, 0, () =>
        {
            if (!HasOriginalNestedGuestSources(child)) return null;
            if (!OriginalStartRefusalsIntact(child)) return null;
            var fact = child.OriginalStartRefusals.SingleOrDefault(item => ReferenceEquals(item.Original, originalRequest));
            if (fact is null)
            {
                if (child.ExecutionStartsIssued != 1 || child.OriginalExecutionStart is not { } invoked) return null;
                fact = invoked;
            }
            if (fact.Observation is not { } raw || !OriginalStartFactSources(child, fact) ||
                !ReferenceEquals(fact.Original, originalRequest) || !ReferenceEquals(fact.Request, originalRequest.Start) ||
                !ReferenceEquals(fact.Artifact, originalRequest.Artifact)) return null;
            return new(originalRequest, raw, fact.NoEffect); // Pure completed fact; no active permission or new effect.
        });
    }

    public bool ValidateCurrentOriginalExecutionTerminal(ExternalOriginalChildExecutionStartRequest originalRequest,
        ExternalOriginalExecutionTerminalObservation originalObservation) => originalObservation is not null &&
        ReferenceEquals(ResolveOriginalExecutionTerminal(originalRequest), originalObservation);

    public ExternalOriginalExecutionTerminalObservation? ResolveOriginalExecutionTerminal(
        ExternalOriginalChildExecutionStartRequest originalRequest)
    {
        if (originalRequest is null || !TryFindChild(originalRequest.Child, out var child, out _, out _)) return null;
        if (!TryGuestMapLineage(child!, out var lineage)) return null;
        lock (child!.ParentSource.Sync)
        return WithNestedLineage<ExternalOriginalExecutionTerminalObservation?>(lineage, 0, () =>
        {
            if (!OriginalStartRefusalsIntact(child) || child.ExecutionStartsIssued != 1 ||
                child.OriginalExecutionStart is not { NoEffect: false } fact ||
                !OriginalStartFactSources(child, fact) || !ReferenceEquals(fact.Original, originalRequest) ||
                !ReferenceEquals(fact.Request, originalRequest.Start) || !ReferenceEquals(fact.Artifact, originalRequest.Artifact) ||
                fact.Observation is not { Outcome: ExternalRuntimeOutcome.Succeeded, Receipt: { } receipt } ||
                fact.TerminalObservation is not { } terminal || !ReferenceEquals(terminal.Request, originalRequest) ||
                !ReferenceEquals(terminal.Receipt, receipt) || !receipt.IsTerminal || !Enum.IsDefined(terminal.Kind)) return null;
            return terminal; // Pure retained fact; neither another CPU run nor backing containment.
        });
    }
}
