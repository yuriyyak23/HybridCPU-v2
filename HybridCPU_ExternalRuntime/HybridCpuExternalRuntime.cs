using HybridCPU.ExternalRuntime.Contracts;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU.ExternalRuntime;

public sealed partial class HybridCpuExternalRuntime : IHybridCpuExternalRuntime, IHybridCpuChildDomainRuntimeV3
{
    private enum Lifecycle : byte { Opening, Ready, Running, Parked, Draining, Closed, Revoked, Faulted, Quarantined }

    private sealed class DomainRecord
    {
        public required ExternalDomainLease Lease { get; init; }
        public DomainRuntimeContext? Context { get; set; }
        public RootAuthorityDescriptor? RootAuthority { get; set; }
        public DomainBindingEntry? Binding { get; set; }
        public required IExternalDomainExecutionOwner Owner { get; init; }
        public object Sync { get; } = new();
        public Lifecycle Lifecycle { get; set; } = Lifecycle.Opening;
        public ExternalOperationIdentity? InFlight { get; set; }
        public HashSet<ExternalOperationIdentity> Completed { get; } = [];
    }

    private readonly object registrySync = new();
    private readonly Dictionary<ExternalDomainLeaseHandle, DomainRecord> registry = [];
    private readonly HashSet<ExternalOperationIdentity> operationLedger = [];
    private readonly HybridCpuExternalFeatureManifest manifest;
    private readonly Func<IExternalDomainExecutionOwner> ownerFactory;
    private readonly IExternalDomainDiagnostics? diagnostics;
    private readonly TimeSpan backendCallDeadline;
    private long nextEpoch;
    private long nextPrivateIdentity;

    public HybridCpuExternalRuntime()
        : this(() => new AdmissionDomainLifetimeOwner(), diagnostics: null, manifestGeneration: 1,
            backendCallDeadline: Timeout.InfiniteTimeSpan) { }

    internal HybridCpuExternalRuntime(Func<IExternalDomainExecutionOwner> ownerFactory,
        IExternalDomainDiagnostics? diagnostics = null, ulong manifestGeneration = 1,
        TimeSpan? backendCallDeadline = null)
    {
        this.ownerFactory = ownerFactory ?? throw new ArgumentNullException(nameof(ownerFactory));
        this.diagnostics = diagnostics;
        this.backendCallDeadline = backendCallDeadline ?? Timeout.InfiniteTimeSpan;
        if (this.backendCallDeadline != Timeout.InfiniteTimeSpan && this.backendCallDeadline <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(backendCallDeadline));
        manifest = new(HybridCpuExternalContractVersion.V1_3, manifestGeneration,
        [
            new(HybridCpuExternalFeatureFamily.DomainLifecycle,
                HybridCpuExternalFeatureAvailability.RuntimeAdmission, 1),
            new(HybridCpuExternalFeatureFamily.ChildDomainLifecycle,
                HybridCpuExternalFeatureAvailability.RuntimeAdmission, 1),
            new(HybridCpuExternalFeatureFamily.ChildGuestMemory,
                HybridCpuExternalFeatureAvailability.RuntimeAdmission, 1),
            new(HybridCpuExternalFeatureFamily.ChildEventDelivery,
                HybridCpuExternalFeatureAvailability.RuntimeAdmission, 1),
            new(HybridCpuExternalFeatureFamily.ChildTrapDelivery,
                HybridCpuExternalFeatureAvailability.RuntimeAdmission, 1),
            new(HybridCpuExternalFeatureFamily.ChildExecutableImage,
                HybridCpuExternalFeatureAvailability.Executable, 1),
            new(HybridCpuExternalFeatureFamily.ChildVirtualIo,
                HybridCpuExternalFeatureAvailability.Executable, 1),
        ]);
    }

    public HybridCpuExternalFeatureManifest QueryFeatures() => manifest;

    public ExternalDomainBindResult BindDomain(ExternalDomainBindRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestId == Guid.Empty || !Enum.IsDefined(request.Profile) || !Valid(request.Operation))
            return new(ExternalRuntimeOutcome.Denied, null, "Bind request identity, profile, and operation must be defined and non-zero.");
        if (!TryReserveOperation(request.Operation))
            return new(ExternalRuntimeOutcome.Denied, null, "Operation identity was already reserved.");

        ulong epoch = NextNonZero(ref nextEpoch);
        ulong privateDomain = NextNonZero(ref nextPrivateIdentity);
        ulong privateAddressSpace = NextNonZero(ref nextPrivateIdentity);
        var lease = new ExternalDomainLease(new(Guid.NewGuid()), new(epoch));
        var execution = new ExecutionDomainDescriptor(privateDomain, null, null, null, compatibilityProjectionEnabled: false);
        var memory = new MemoryDomainDescriptor(null, null, MemoryDomainTranslationControl.Disabled, null, ownsSecondStageTranslation: true);
        var io = new IoDomainDescriptor(null, null, ownsDmaAuthority: true, ownsIommuAuthority: true, compatibilityProjectionEnabled: false);
        var context = new DomainRuntimeContext(execution, memory, io, CapabilityDescriptorSet.Empty, null,
            privateDomain, privateAddressSpace);
        var root = new RootAuthorityDescriptor(RootAuthorityClass.RuntimeRoot, epoch, 0,
            allowCompatibilityFrontendActivation: false, allowAuthoritativeStateMutation: true);
        var binding = new DomainBindingEntry(DomainBindingAuthority.Runtime, privateDomain, context,
            AllowsCompatibilityProjection: false);

        string? denial = ValidateMaterialization(execution, memory, io, context, root, binding, privateDomain);
        if (denial is not null) return new(ExternalRuntimeOutcome.Denied, null, denial);

        IExternalDomainExecutionOwner owner;
        try { owner = ownerFactory(); }
        catch (Exception ex) { return new(ExternalRuntimeOutcome.Faulted, null, $"Backend owner creation failed before acceptance: {ex.GetType().Name}."); }
        if (owner is null) return new(ExternalRuntimeOutcome.Faulted, null, "Backend owner creation returned no owner before acceptance.");

        var record = new DomainRecord { Lease = lease, Context = context, RootAuthority = root, Binding = binding, Owner = owner };
        lock (registrySync) registry.Add(lease.Handle, record);

        BackendBindResult? backend;
        BackendInvocation invocation = InvokeBackend(
            () => owner.Open(lease, request.Operation, manifest.ContractVersion, manifest.Generation),
            record, request.Operation, out backend, out Exception? backendError);
        if (invocation != BackendInvocation.Completed)
        {
            lock (record.Sync)
            {
                record.Lifecycle = Lifecycle.Quarantined;
                if (invocation == BackendInvocation.Faulted)
                {
                    record.InFlight = null;
                    Monitor.PulseAll(record.Sync);
                }
            }
            string kind = invocation == BackendInvocation.TimedOut ? "deadline elapsed" : backendError!.GetType().Name;
            return new(ExternalRuntimeOutcome.Unknown, null, $"Backend open result is ambiguous: {kind}.");
        }

        if (backend!.Outcome is BackendOutcome.Denied or BackendOutcome.Unsupported or BackendOutcome.Faulted)
        {
            lock (registrySync) registry.Remove(lease.Handle);
            return new(Map(backend.Outcome), null, backend.Reason);
        }
        if (backend.Outcome != BackendOutcome.Accepted || !ExactBind(backend.Receipt, lease, request.Operation))
        {
            lock (record.Sync) record.Lifecycle = backend.Outcome == BackendOutcome.Revoked ? Lifecycle.Revoked : Lifecycle.Quarantined;
            return new(backend.Outcome == BackendOutcome.Revoked ? ExternalRuntimeOutcome.Revoked : ExternalRuntimeOutcome.Unknown,
                null, backend.Reason.Length == 0 ? "Backend bind acceptance was not proven by an exact receipt." : backend.Reason);
        }

        lock (record.Sync) { record.Lifecycle = Lifecycle.Ready; record.Completed.Add(request.Operation); }
        TryDiagnostics(() => diagnostics?.Attach(lease, manifest.Generation));
        return new(ExternalRuntimeOutcome.Bound, backend.Receipt, string.Empty);
    }

    public ExternalDomainTransitionResult TransitionDomain(ExternalDomainLease lease,
        ExternalDomainTransition transition, ExternalOperationIdentity operation)
    {
        if (!Enum.IsDefined(transition) || !Valid(operation))
            return new(ExternalRuntimeOutcome.Denied, null, "Transition and operation identity must be valid.");
        if (!TryFind(lease, out DomainRecord? record, out ExternalRuntimeOutcome lookup, out string reason))
            return new(lookup, null, reason);
        DomainRecord found = record!;

        lock (found.Sync)
        {
            ExternalRuntimeOutcome? stateError = Reserve(found, transition, operation, out reason);
            if (stateError is not null) return new(stateError.Value, null, reason);
            if (!TryReserveOperation(operation))
            {
                found.InFlight = null;
                return new(ExternalRuntimeOutcome.Denied, null, "Operation identity was already reserved.");
            }
        }

        BackendTransitionResult? backend;
        BackendInvocation invocation = InvokeBackend(
            () => found.Owner.Transition(lease, transition, operation, manifest.ContractVersion, manifest.Generation),
            found, operation, out backend, out Exception? backendError);
        if (invocation != BackendInvocation.Completed)
        {
            lock (found.Sync)
            {
                found.Lifecycle = Lifecycle.Quarantined;
                if (invocation == BackendInvocation.Faulted)
                {
                    found.InFlight = null;
                    Monitor.PulseAll(found.Sync);
                }
            }
            string kind = invocation == BackendInvocation.TimedOut ? "deadline elapsed" : backendError!.GetType().Name;
            return new(ExternalRuntimeOutcome.Unknown, null, $"Backend transition result is ambiguous: {kind}.");
        }

        lock (found.Sync)
        {
            found.InFlight = null;
            Monitor.PulseAll(found.Sync);
            if (backend!.Outcome is BackendOutcome.Unsupported or BackendOutcome.Denied)
                return new(Map(backend.Outcome), null, backend.Reason);
            if (backend.Outcome == BackendOutcome.Revoked) { found.Lifecycle = Lifecycle.Revoked; return new(ExternalRuntimeOutcome.Revoked, null, backend.Reason); }
            if (backend.Outcome == BackendOutcome.Faulted) { found.Lifecycle = Lifecycle.Faulted; return new(ExternalRuntimeOutcome.Faulted, null, backend.Reason); }
            if (backend.Outcome != BackendOutcome.Accepted || !ExactTransition(backend.Receipt, lease, operation, transition, ExpectedState(transition)))
            {
                found.Lifecycle = Lifecycle.Quarantined;
                return new(ExternalRuntimeOutcome.Unknown, null, "Backend transition acceptance was not proven by an exact receipt.");
            }
            if (found.Lifecycle != Lifecycle.Draining)
                found.Lifecycle = transition == ExternalDomainTransition.Park ? Lifecycle.Parked : Lifecycle.Running;
            found.Completed.Add(operation);
            return new(ExternalRuntimeOutcome.Succeeded, backend.Receipt, string.Empty);
        }
    }

    public ExternalDomainCloseResult CloseDomain(ExternalDomainLease lease, ExternalOperationIdentity operation)
    {
        if (!Valid(operation)) return new(ExternalRuntimeOutcome.Denied, null, "Close operation identity must be non-zero.");
        if (!TryFind(lease, out DomainRecord? record, out ExternalRuntimeOutcome lookup, out string reason))
            return new(lookup, null, reason);
        DomainRecord found = record!;
        lock (found.Sync)
        {
            if (HasLiveChildren(lease))
                return new(ExternalRuntimeOutcome.Denied, null, "Parent domain has live child domains.");
            if (found.Lifecycle == Lifecycle.Closed) return new(ExternalRuntimeOutcome.Stale, null, "Domain is already closed.");
            if (found.Completed.Contains(operation)) return new(ExternalRuntimeOutcome.Denied, null, "Operation identity was already completed.");
            if (found.Lifecycle == Lifecycle.Revoked) return new(ExternalRuntimeOutcome.Revoked, null, "Domain lease is revoked.");
            if (found.Lifecycle == Lifecycle.Faulted) return new(ExternalRuntimeOutcome.Faulted, null, "Domain owner is faulted.");
            if (!TryReserveOperation(operation)) return new(ExternalRuntimeOutcome.Denied, null, "Operation identity was already reserved.");
            found.Lifecycle = Lifecycle.Draining;
            while (found.InFlight is not null) Monitor.Wait(found.Sync);
            if (found.Lifecycle == Lifecycle.Revoked) return new(ExternalRuntimeOutcome.Revoked, null, "Domain lease was revoked while draining.");
            if (found.Lifecycle == Lifecycle.Faulted) return new(ExternalRuntimeOutcome.Faulted, null, "Domain owner faulted while draining.");
            found.InFlight = operation;
        }

        BackendCloseResult? backend;
        BackendInvocation invocation = InvokeBackend(
            () => found.Owner.Close(lease, operation, manifest.ContractVersion, manifest.Generation),
            found, operation, out backend, out Exception? backendError);
        if (invocation != BackendInvocation.Completed)
        {
            lock (found.Sync)
            {
                found.Lifecycle = Lifecycle.Quarantined;
                if (invocation == BackendInvocation.Faulted)
                {
                    found.InFlight = null;
                    Monitor.PulseAll(found.Sync);
                }
            }
            string kind = invocation == BackendInvocation.TimedOut ? "deadline elapsed" : backendError!.GetType().Name;
            return new(ExternalRuntimeOutcome.Unknown, null, $"Backend close result is ambiguous: {kind}.");
        }

        lock (found.Sync)
        {
            found.InFlight = null;
            if (backend!.Outcome == BackendOutcome.Revoked) { found.Lifecycle = Lifecycle.Revoked; return new(ExternalRuntimeOutcome.Revoked, null, backend.Reason); }
            if (backend.Outcome == BackendOutcome.Faulted) { found.Lifecycle = Lifecycle.Faulted; return new(ExternalRuntimeOutcome.Faulted, null, backend.Reason); }
            if (backend.Outcome != BackendOutcome.Accepted || !ExactClose(backend.Receipt, lease, operation))
            {
                found.Lifecycle = Lifecycle.Quarantined;
                return new(ExternalRuntimeOutcome.Unknown, null, "Backend terminal close was not proven by an exact receipt.");
            }
            found.Lifecycle = Lifecycle.Closed;
            found.Completed.Add(operation);
            found.Context = null;
            found.RootAuthority = null;
            found.Binding = null;
        }
        TryDiagnostics(() => diagnostics?.Detach(lease, manifest.Generation));
        return new(ExternalRuntimeOutcome.Closed, backend.Receipt, string.Empty);
    }

    private string? ValidateMaterialization(ExecutionDomainDescriptor execution, MemoryDomainDescriptor memory,
        IoDomainDescriptor io, DomainRuntimeContext context, RootAuthorityDescriptor root,
        DomainBindingEntry binding, ulong privateDomain)
    {
        ExecutionDomainRuntimeResult e = new ExecutionDomainRuntime().Validate(new(execution, false, false, false));
        if (!e.IsAllowed) return e.Reason;
        MemoryDomainRuntimeResult m = new MemoryDomainRuntime().Validate(new(memory, false, false, false, false));
        if (!m.IsAllowed) return m.Reason;
        IoDomainRuntimeResult i = new IoDomainRuntime().Validate(new(io, false, false, false, false, false));
        if (!i.IsAllowed) return i.Reason;
        DomainBindingResult b = new DomainBindingTable().Validate(new(binding, privateDomain, false));
        if (!b.IsAllowed) return b.Reason;
        var operation = new DomainRuntimeOperation(DomainRuntimeOperationKind.EnterDomain,
            DomainRuntimeOperationSource.RuntimeService, requiresCapabilityGrant: false,
            DomainRuntimeOperationAuthorityClass.NoStateExecution);
        RuntimeBoundaryAdmissionResult admission = new RuntimeBoundaryAdmissionService().Validate(new(
            context, root, EvidencePolicyDescriptor.FailClosed, operation, DomainBoundaryDescriptor.FullDomainRuntime,
            CapabilityBoundaryRequirement.None, EvidenceBoundaryRequirement.None));
        return admission.IsAllowed ? null : admission.Message;
    }

    private bool TryFind(ExternalDomainLease lease, out DomainRecord? record,
        out ExternalRuntimeOutcome outcome, out string reason)
    {
        if (lease.Handle.Value == Guid.Empty || lease.Epoch.Value == 0)
        { record = null; outcome = ExternalRuntimeOutcome.NotFound; reason = "Lease identity is absent."; return false; }
        lock (registrySync) registry.TryGetValue(lease.Handle, out record);
        if (record is null) { outcome = ExternalRuntimeOutcome.NotFound; reason = "Lease handle was not found."; return false; }
        if (record.Lease.Epoch != lease.Epoch) { outcome = ExternalRuntimeOutcome.Stale; reason = "Lease epoch is stale."; return false; }
        outcome = default; reason = string.Empty; return true;
    }

    private ExternalRuntimeOutcome? Reserve(DomainRecord record, ExternalDomainTransition transition,
        ExternalOperationIdentity operation, out string reason)
    {
        if (record.Lifecycle == Lifecycle.Revoked) { reason = "Domain lease is revoked."; return ExternalRuntimeOutcome.Revoked; }
        if (record.Lifecycle == Lifecycle.Faulted) { reason = "Domain owner is faulted."; return ExternalRuntimeOutcome.Faulted; }
        if (record.Lifecycle is Lifecycle.Quarantined or Lifecycle.Draining or Lifecycle.Closed or Lifecycle.Opening)
        { reason = "Domain lifecycle does not admit a transition."; return ExternalRuntimeOutcome.Denied; }
        bool legal = transition switch
        {
            ExternalDomainTransition.Start => record.Lifecycle == Lifecycle.Ready,
            ExternalDomainTransition.Park => record.Lifecycle == Lifecycle.Running,
            ExternalDomainTransition.Resume => record.Lifecycle == Lifecycle.Parked,
            _ => false,
        };
        if (!legal) { reason = "Illegal lifecycle transition."; return ExternalRuntimeOutcome.Denied; }
        if (record.InFlight is not null) { reason = "Another operation is in flight."; return ExternalRuntimeOutcome.Denied; }
        if (record.Completed.Contains(operation)) { reason = "Operation identity was already completed."; return ExternalRuntimeOutcome.Denied; }
        record.InFlight = operation; reason = string.Empty; return null;
    }

    private bool ExactBind(ExternalDomainBindReceipt? receipt, ExternalDomainLease lease, ExternalOperationIdentity operation) =>
        receipt is not null && receipt.Lease == lease && receipt.Operation == operation &&
        receipt.ContractVersion == manifest.ContractVersion && receipt.ManifestGeneration == manifest.Generation &&
        receipt.State == ExternalDomainState.Ready;

    private bool ExactTransition(ExternalDomainTransitionReceipt? receipt, ExternalDomainLease lease,
        ExternalOperationIdentity operation, ExternalDomainTransition transition, ExternalDomainState state) =>
        receipt is not null && receipt.Lease == lease && receipt.Operation == operation &&
        receipt.ContractVersion == manifest.ContractVersion && receipt.ManifestGeneration == manifest.Generation &&
        receipt.Transition == transition && receipt.ResultingState == state;

    private bool ExactClose(ExternalDomainCloseReceipt? receipt, ExternalDomainLease lease, ExternalOperationIdentity operation) =>
        receipt is not null && receipt.Lease == lease && receipt.Operation == operation &&
        receipt.ContractVersion == manifest.ContractVersion && receipt.ManifestGeneration == manifest.Generation &&
        receipt.ResultingState == ExternalDomainState.Closed && receipt.IsTerminal;

    private static ExternalDomainState ExpectedState(ExternalDomainTransition transition) =>
        transition == ExternalDomainTransition.Park ? ExternalDomainState.Parked : ExternalDomainState.Running;
    private static bool Valid(ExternalOperationIdentity operation) =>
        operation.Handle.Value != Guid.Empty && operation.Generation.Value != 0;
    private bool TryReserveOperation(ExternalOperationIdentity operation)
    {
        lock (registrySync) return operationLedger.Add(operation);
    }
    private static ulong NextNonZero(ref long value) => checked((ulong)Interlocked.Increment(ref value));
    private enum BackendInvocation : byte { Completed, Faulted, TimedOut }
    private BackendInvocation InvokeBackend<T>(Func<T> call, DomainRecord record, ExternalOperationIdentity operation,
        out T? result, out Exception? error) where T : class
    {
        if (backendCallDeadline == Timeout.InfiniteTimeSpan)
        {
            try { result = call(); error = null; return BackendInvocation.Completed; }
            catch (Exception ex) { result = null; error = ex; return BackendInvocation.Faulted; }
        }

        Task<T> task = Task.Run(call);
        try
        {
            result = task.WaitAsync(backendCallDeadline).GetAwaiter().GetResult();
            error = null;
            return BackendInvocation.Completed;
        }
        catch (TimeoutException ex)
        {
            _ = task.ContinueWith(completed =>
            {
                _ = completed.Exception;
                lock (record.Sync)
                {
                    if (record.InFlight == operation) record.InFlight = null;
                    Monitor.PulseAll(record.Sync);
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            result = null;
            error = ex;
            return BackendInvocation.TimedOut;
        }
        catch (Exception ex)
        {
            result = null;
            error = ex;
            return BackendInvocation.Faulted;
        }
    }
    private static ExternalRuntimeOutcome Map(BackendOutcome outcome) => outcome switch
    {
        BackendOutcome.Unsupported => ExternalRuntimeOutcome.Unsupported,
        BackendOutcome.Denied => ExternalRuntimeOutcome.Denied,
        BackendOutcome.Revoked => ExternalRuntimeOutcome.Revoked,
        BackendOutcome.Faulted => ExternalRuntimeOutcome.Faulted,
        _ => ExternalRuntimeOutcome.Unknown,
    };
    private static void TryDiagnostics(Action action) { try { action(); } catch { /* diagnostics never owns authority */ } }
}
