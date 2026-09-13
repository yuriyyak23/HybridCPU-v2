using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime;

public sealed partial class HybridCpuExternalRuntime
{
    private const ExternalChildAuthority ParentAuthorityCeiling =
        ExternalChildAuthority.Execute |
        ExternalChildAuthority.GuestMemory |
        ExternalChildAuthority.EventInjection |
        ExternalChildAuthority.TrapDelivery |
        ExternalChildAuthority.VirtualIo;

    private sealed class ChildRecord
    {
        public required ExternalChildDomainLease Lease { get; init; }
        public required ExternalChildAuthority Authority { get; init; }
        public required ulong GuestMemoryLimitBytes { get; init; }
        public object Sync { get; } = new();
        public ExternalChildDomainState State { get; set; } = ExternalChildDomainState.Ready;
        public HashSet<ExternalOperationIdentity> Completed { get; } = [];
        public Dictionary<ExternalGuestMappingHandle, GuestMappingRecord> Mappings { get; } = [];
        public ulong LastEventSequence { get; set; }
        public ulong LastTrapSequence { get; set; }
        public uint ClosedMappings { get; set; }
        public CpuBackedChildExecution? Execution { get; set; }
        public ExternalChildArtifactBindReceipt? Artifact { get; set; }
        public ExternalChildExecutionGeneration ExecutionGeneration { get; set; }
        public Dictionary<ExternalChildVirtualIoHandle, VirtualIoRecord> VirtualIo { get; } = [];
    }

    private sealed class GuestMappingRecord
    {
        public required ExternalGuestMappingLease Lease { get; init; }
        public required ulong Offset { get; init; }
        public required ulong Length { get; init; }
        public bool Closed { get; set; }
    }

    private sealed class VirtualIoRecord
    {
        public required ExternalChildVirtualIoBindReceipt Receipt { get; init; }
        public bool Closed { get; set; }
    }

    private readonly object childRegistrySync = new();
    private readonly Dictionary<ExternalChildDomainHandle, ChildRecord> childRegistry = [];
    private readonly Dictionary<ExternalGuestMappingHandle, GuestMappingRecord> guestMappingRegistry = [];
    private long nextChildEpoch;
    private long nextGuestMappingEpoch;
    private long nextArtifactEpoch;
    private long nextExecutionGeneration;
    private long nextVirtualIoEpoch;

    public ExternalChildResult<ExternalChildDomainCreateReceipt> CreateChildDomain(
        ExternalDomainLease parent, ExternalChildDomainCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestId == Guid.Empty || !Valid(request.Operation) || request.GuestMemoryLimitBytes == 0)
            return Denied<ExternalChildDomainCreateReceipt>("Child request identity, operation, and memory limit must be non-zero.");
        if (!ValidAuthority(request.RequestedAuthority) || request.RequestedAuthority == ExternalChildAuthority.None)
            return Denied<ExternalChildDomainCreateReceipt>("Requested child authority is undefined or empty.");
        if ((request.RequestedAuthority & ~ParentAuthorityCeiling) != 0)
            return Denied<ExternalChildDomainCreateReceipt>("Requested child authority exceeds the parent authority ceiling.");
        if (!TryFind(parent, out DomainRecord? parentRecord, out ExternalRuntimeOutcome lookup, out string reason))
            return Result<ExternalChildDomainCreateReceipt>(lookup, reason);

        lock (parentRecord!.Sync)
        {
            if (parentRecord.Lifecycle is Lifecycle.Closed or Lifecycle.Draining or Lifecycle.Revoked or
                Lifecycle.Faulted or Lifecycle.Quarantined or Lifecycle.Opening)
                return Denied<ExternalChildDomainCreateReceipt>("Parent lifecycle does not admit child creation.");
            if (!TryReserveOperation(request.Operation))
                return Denied<ExternalChildDomainCreateReceipt>("Operation identity was already reserved.");

            var lease = new ExternalChildDomainLease(
                new(Guid.NewGuid()), new(NextNonZero(ref nextChildEpoch)), parent);
            var child = new ChildRecord
            {
                Lease = lease,
                Authority = request.RequestedAuthority,
                GuestMemoryLimitBytes = request.GuestMemoryLimitBytes,
            };
            child.Completed.Add(request.Operation);
            lock (childRegistrySync) childRegistry.Add(lease.Handle, child);
            var receipt = new ExternalChildDomainCreateReceipt(
                lease, child.Authority, child.GuestMemoryLimitBytes, request.Operation,
                manifest.ContractVersion, manifest.Generation, ExternalChildDomainState.Ready);
            return Success(ExternalRuntimeOutcome.Bound, receipt);
        }
    }

    public ExternalChildResult<ExternalChildDomainTransitionReceipt> TransitionChildDomain(
        ExternalChildDomainLease child, ExternalChildDomainTransition transition,
        ExternalOperationIdentity operation)
    {
        if (!Enum.IsDefined(transition) || !Valid(operation))
            return Denied<ExternalChildDomainTransitionReceipt>("Transition and operation identity must be valid.");
        if (!TryFindChild(child, out ChildRecord? record, out ExternalRuntimeOutcome lookup, out string reason))
            return Result<ExternalChildDomainTransitionReceipt>(lookup, reason);
        lock (record!.Sync)
        {
            if (!record.Authority.HasFlag(ExternalChildAuthority.Execute))
                return Denied<ExternalChildDomainTransitionReceipt>("Child has no execution authority.");
            if (transition == ExternalChildDomainTransition.Start && record.Artifact is not null)
                return Result<ExternalChildDomainTransitionReceipt>(ExternalRuntimeOutcome.Unsupported,
                    "Executable artifacts must start through the V3 correlated execution operation.");
            if (record.State == ExternalChildDomainState.Closed)
                return Result<ExternalChildDomainTransitionReceipt>(ExternalRuntimeOutcome.Stale, "Child is closed.");
            bool legal = transition switch
            {
                ExternalChildDomainTransition.Start => record.State == ExternalChildDomainState.Ready,
                ExternalChildDomainTransition.Park => record.State == ExternalChildDomainState.Running,
                ExternalChildDomainTransition.Resume => record.State == ExternalChildDomainState.Parked,
                _ => false,
            };
            if (!legal) return Denied<ExternalChildDomainTransitionReceipt>("Illegal child lifecycle transition.");
            if (!ReserveChildOperation(record, operation, out reason))
                return Denied<ExternalChildDomainTransitionReceipt>(reason);
            bool transitioned = record.Execution is null || transition switch
            {
                ExternalChildDomainTransition.Start => record.Execution?.Start(out reason) == true,
                ExternalChildDomainTransition.Park => record.Execution?.Park(out reason) == true,
                ExternalChildDomainTransition.Resume => record.Execution?.Resume(out reason) == true,
                _ => false,
            };
            if (!transitioned)
                return Result<ExternalChildDomainTransitionReceipt>(ExternalRuntimeOutcome.Unsupported,
                    reason);
            record.State = transition == ExternalChildDomainTransition.Park
                ? ExternalChildDomainState.Parked : ExternalChildDomainState.Running;
            var receipt = new ExternalChildDomainTransitionReceipt(
                child, operation, manifest.ContractVersion, manifest.Generation, transition, record.State);
            return Success(ExternalRuntimeOutcome.Succeeded, receipt);
        }
    }

    public ExternalChildResult<ExternalGuestMemoryMapReceipt> MapChildGuestMemory(
        ExternalChildDomainLease child, ExternalGuestMemoryMapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Valid(request.Operation) || request.LengthBytes == 0 ||
            request.ChildOffsetBytes > ulong.MaxValue - request.LengthBytes)
            return Denied<ExternalGuestMemoryMapReceipt>("Mapping operation and bounded range must be valid.");
        if (!TryFindChild(child, out ChildRecord? record, out ExternalRuntimeOutcome lookup, out string reason))
            return Result<ExternalGuestMemoryMapReceipt>(lookup, reason);
        lock (record!.Sync)
        {
            if (record.State == ExternalChildDomainState.Closed)
                return Result<ExternalGuestMemoryMapReceipt>(ExternalRuntimeOutcome.Stale, "Child is closed.");
            if (!record.Authority.HasFlag(ExternalChildAuthority.GuestMemory))
                return Denied<ExternalGuestMemoryMapReceipt>("Child has no guest-memory authority.");
            ulong end = request.ChildOffsetBytes + request.LengthBytes;
            if (end > record.GuestMemoryLimitBytes)
                return Denied<ExternalGuestMemoryMapReceipt>("Guest mapping exceeds the admitted child memory limit.");
            if (record.Mappings.Values.Any(mapping => !mapping.Closed &&
                request.ChildOffsetBytes < mapping.Offset + mapping.Length && mapping.Offset < end))
                return Denied<ExternalGuestMemoryMapReceipt>("Guest mapping overlaps an active mapping.");
            if (!ReserveChildOperation(record, request.Operation, out reason))
                return Denied<ExternalGuestMemoryMapReceipt>(reason);

            var lease = new ExternalGuestMappingLease(
                new(Guid.NewGuid()), new(NextNonZero(ref nextGuestMappingEpoch)), child);
            var mapping = new GuestMappingRecord
            {
                Lease = lease,
                Offset = request.ChildOffsetBytes,
                Length = request.LengthBytes,
            };
            record.Mappings.Add(lease.Handle, mapping);
            lock (childRegistrySync) guestMappingRegistry.Add(lease.Handle, mapping);
            var receipt = new ExternalGuestMemoryMapReceipt(
                lease, mapping.Offset, mapping.Length, request.Operation,
                manifest.ContractVersion, manifest.Generation);
            return Success(ExternalRuntimeOutcome.Succeeded, receipt);
        }
    }

    public ExternalChildResult<ExternalGuestMemoryUnmapReceipt> UnmapChildGuestMemory(
        ExternalGuestMappingLease mapping, ExternalOperationIdentity operation)
    {
        if (!Valid(operation)) return Denied<ExternalGuestMemoryUnmapReceipt>("Unmap operation must be valid.");
        if (!TryFindMapping(mapping, out GuestMappingRecord? found, out ExternalRuntimeOutcome lookup, out string reason))
            return Result<ExternalGuestMemoryUnmapReceipt>(lookup, reason);
        if (!TryFindChild(mapping.Child, out ChildRecord? child, out lookup, out reason))
            return Result<ExternalGuestMemoryUnmapReceipt>(lookup, reason);
        lock (child!.Sync)
        {
            if (found!.Closed)
                return Result<ExternalGuestMemoryUnmapReceipt>(ExternalRuntimeOutcome.Stale, "Guest mapping is already closed.");
            if (!ReserveChildOperation(child, operation, out reason))
                return Denied<ExternalGuestMemoryUnmapReceipt>(reason);
            found.Closed = true;
            child.ClosedMappings++;
            var receipt = new ExternalGuestMemoryUnmapReceipt(
                mapping, operation, manifest.ContractVersion, manifest.Generation, IsTerminal: true);
            return Success(ExternalRuntimeOutcome.Closed, receipt);
        }
    }

    public ExternalChildResult<ExternalChildEventReceipt> InjectChildEvent(
        ExternalChildDomainLease child, ExternalChildEventRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Kind) || request.Sequence == 0 || !Valid(request.Operation))
            return Denied<ExternalChildEventReceipt>("Event kind, sequence, and operation must be valid.");
        if (!TryFindChild(child, out ChildRecord? record, out ExternalRuntimeOutcome lookup, out string reason))
            return Result<ExternalChildEventReceipt>(lookup, reason);
        lock (record!.Sync)
        {
            if (!record.Authority.HasFlag(ExternalChildAuthority.EventInjection))
                return Denied<ExternalChildEventReceipt>("Child has no event-injection authority.");
            if (record.State is ExternalChildDomainState.Ready or ExternalChildDomainState.Closed)
                return Denied<ExternalChildEventReceipt>("Child lifecycle does not admit event injection.");
            if (request.Sequence <= record.LastEventSequence)
                return Result<ExternalChildEventReceipt>(ExternalRuntimeOutcome.Stale, "Event sequence is stale or replayed.");
            if (!ReserveChildOperation(record, request.Operation, out reason))
                return Denied<ExternalChildEventReceipt>(reason);
            record.LastEventSequence = request.Sequence;
            var receipt = new ExternalChildEventReceipt(
                child, request.Kind, request.Sequence, request.Operation,
                manifest.ContractVersion, manifest.Generation);
            return Success(ExternalRuntimeOutcome.Succeeded, receipt);
        }
    }

    public ExternalChildResult<ExternalChildTrapReceipt> ReportChildTrap(
        ExternalChildDomainLease child, ExternalChildTrapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Kind) || request.Sequence == 0 || !Valid(request.Operation))
            return Denied<ExternalChildTrapReceipt>("Trap kind, sequence, and operation must be valid.");
        if (!TryFindChild(child, out ChildRecord? record, out ExternalRuntimeOutcome lookup, out string reason))
            return Result<ExternalChildTrapReceipt>(lookup, reason);
        lock (record!.Sync)
        {
            if (!record.Authority.HasFlag(ExternalChildAuthority.TrapDelivery))
                return Denied<ExternalChildTrapReceipt>("Child has no trap-delivery authority.");
            if (record.State != ExternalChildDomainState.Running)
                return Denied<ExternalChildTrapReceipt>("Only a running child may publish a trap.");
            if (request.Sequence <= record.LastTrapSequence)
                return Result<ExternalChildTrapReceipt>(ExternalRuntimeOutcome.Stale, "Trap sequence is stale or replayed.");
            if (!ReserveChildOperation(record, request.Operation, out reason))
                return Denied<ExternalChildTrapReceipt>(reason);
            record.LastTrapSequence = request.Sequence;
            record.State = ExternalChildDomainState.Parked;
            ExternalChildTrapDisposition disposition = request.Kind == ExternalChildTrapKind.IllegalInstruction
                ? ExternalChildTrapDisposition.TerminationRequired
                : ExternalChildTrapDisposition.ResumePermitted;
            var receipt = new ExternalChildTrapReceipt(
                child, request.Kind, request.Sequence, disposition, request.Operation,
                manifest.ContractVersion, manifest.Generation);
            return Success(ExternalRuntimeOutcome.Succeeded, receipt);
        }
    }

    public ExternalChildResult<ExternalChildDomainCloseReceipt> CloseChildDomain(
        ExternalChildDomainLease child, ExternalOperationIdentity operation)
    {
        if (!Valid(operation)) return Denied<ExternalChildDomainCloseReceipt>("Close operation must be valid.");
        if (!TryFindChild(child, out ChildRecord? record, out ExternalRuntimeOutcome lookup, out string reason))
            return Result<ExternalChildDomainCloseReceipt>(lookup, reason);
        lock (record!.Sync)
        {
            if (record.State == ExternalChildDomainState.Closed)
                return Result<ExternalChildDomainCloseReceipt>(ExternalRuntimeOutcome.Stale, "Child is already closed.");
            if (record.Mappings.Values.Any(static mapping => !mapping.Closed))
                return Denied<ExternalChildDomainCloseReceipt>("Child has live guest-memory mappings.");
            if (record.VirtualIo.Values.Any(static binding => !binding.Closed))
                return Denied<ExternalChildDomainCloseReceipt>("Child has live bounded virtual-I/O bindings.");
            if (record.Execution is not null && !record.Execution.Close(out string executionReason))
                return Result<ExternalChildDomainCloseReceipt>(ExternalRuntimeOutcome.Faulted, executionReason);
            if (!ReserveChildOperation(record, operation, out reason))
                return Denied<ExternalChildDomainCloseReceipt>(reason);
            record.State = ExternalChildDomainState.Closed;
            var receipt = new ExternalChildDomainCloseReceipt(
                child, operation, manifest.ContractVersion, manifest.Generation,
                ExternalChildDomainState.Closed, IsTerminal: true, record.ClosedMappings);
            return Success(ExternalRuntimeOutcome.Closed, receipt);
        }
    }

    public ExternalChildResult<ExternalChildExecutableImageReceipt> LoadChildExecutableImage(
        ExternalChildDomainLease child, ExternalChildExecutableImageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Valid(request.Operation) || request.PackageBytes is null || request.PackageBytes.Length == 0 ||
            request.MaximumPipelineCycles <= 0)
            return Denied<ExternalChildExecutableImageReceipt>("Executable image request is empty or unbounded.");
        if (!TryFindChild(child, out ChildRecord? record, out ExternalRuntimeOutcome lookup, out string reason))
            return Result<ExternalChildExecutableImageReceipt>(lookup, reason);
        lock (record!.Sync)
        {
            if (record.State != ExternalChildDomainState.Ready || record.Execution is not null)
                return Denied<ExternalChildExecutableImageReceipt>("Executable image may be bound exactly once while the child is ready.");
            if (!record.Authority.HasFlag(ExternalChildAuthority.Execute))
                return Denied<ExternalChildExecutableImageReceipt>("Child has no execution authority.");
            if (!ReserveChildOperation(record, request.Operation, out reason))
                return Denied<ExternalChildExecutableImageReceipt>(reason);
            if (!CpuBackedChildExecution.TryCreate(request.PackageBytes, request.MaximumPipelineCycles,
                    out CpuBackedChildExecution? execution, out reason))
                return Result<ExternalChildExecutableImageReceipt>(ExternalRuntimeOutcome.Denied, reason);
            record.Execution = execution;
            return Success(ExternalRuntimeOutcome.Succeeded, new ExternalChildExecutableImageReceipt(child.Handle, child.Epoch, child.Parent,
                execution!.PackageSha256, request.MaximumPipelineCycles, request.Operation,
                manifest.ContractVersion, manifest.Generation));
        }
    }

    private bool HasLiveChildren(ExternalDomainLease parent)
    {
        lock (childRegistrySync)
            return childRegistry.Values.Any(child => child.Lease.Parent == parent &&
                child.State != ExternalChildDomainState.Closed);
    }

    private bool TryFindChild(ExternalChildDomainLease lease, out ChildRecord? record,
        out ExternalRuntimeOutcome outcome, out string reason)
    {
        if (lease.Handle.Value == Guid.Empty || lease.Epoch.Value == 0 ||
            lease.Parent.Handle.Value == Guid.Empty || lease.Parent.Epoch.Value == 0)
        {
            record = null; outcome = ExternalRuntimeOutcome.NotFound;
            reason = "Child lease identity is absent."; return false;
        }
        lock (childRegistrySync) childRegistry.TryGetValue(lease.Handle, out record);
        if (record is null)
        {
            outcome = ExternalRuntimeOutcome.NotFound; reason = "Child lease handle was not found."; return false;
        }
        if (record.Lease.Epoch != lease.Epoch || record.Lease.Parent != lease.Parent)
        {
            outcome = ExternalRuntimeOutcome.Stale; reason = "Child lease epoch or parent binding is stale."; return false;
        }
        if (!TryFind(lease.Parent, out _, out outcome, out reason)) return false;
        outcome = default; reason = string.Empty; return true;
    }

    private bool TryFindMapping(ExternalGuestMappingLease lease, out GuestMappingRecord? record,
        out ExternalRuntimeOutcome outcome, out string reason)
    {
        if (lease.Handle.Value == Guid.Empty || lease.Epoch.Value == 0)
        {
            record = null; outcome = ExternalRuntimeOutcome.NotFound;
            reason = "Guest mapping identity is absent."; return false;
        }
        lock (childRegistrySync) guestMappingRegistry.TryGetValue(lease.Handle, out record);
        if (record is null)
        {
            outcome = ExternalRuntimeOutcome.NotFound; reason = "Guest mapping handle was not found."; return false;
        }
        if (record.Lease.Epoch != lease.Epoch || record.Lease.Child != lease.Child)
        {
            outcome = ExternalRuntimeOutcome.Stale; reason = "Guest mapping epoch or child binding is stale."; return false;
        }
        outcome = default; reason = string.Empty; return true;
    }

    private bool ReserveChildOperation(ChildRecord child, ExternalOperationIdentity operation, out string reason)
    {
        if (child.Completed.Contains(operation))
        {
            reason = "Operation identity was already completed."; return false;
        }
        if (!TryReserveOperation(operation))
        {
            reason = "Operation identity was already reserved."; return false;
        }
        child.Completed.Add(operation);
        reason = string.Empty;
        return true;
    }

    private static bool ValidAuthority(ExternalChildAuthority authority) =>
        (authority & ~(ExternalChildAuthority.Execute | ExternalChildAuthority.GuestMemory |
            ExternalChildAuthority.EventInjection | ExternalChildAuthority.TrapDelivery |
            ExternalChildAuthority.VirtualIo)) == 0;

    private static ExternalChildResult<T> Success<T>(ExternalRuntimeOutcome outcome, T receipt) where T : class =>
        new(outcome, receipt, string.Empty);
    private static ExternalChildResult<T> Denied<T>(string reason) where T : class =>
        new(ExternalRuntimeOutcome.Denied, null, reason);
    private static ExternalChildResult<T> Result<T>(ExternalRuntimeOutcome outcome, string reason) where T : class =>
        new(outcome, null, reason);
}
