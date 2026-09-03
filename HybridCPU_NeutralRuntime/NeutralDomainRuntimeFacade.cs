namespace YAKSys_Hybrid_CPU.Core;

public enum NeutralDomainProfile : byte
{
    OrdinaryService = 0,
}

public readonly record struct NeutralDomainBindingHandle(ulong Value);
public readonly record struct NeutralDomainBindingEpoch(ulong Value);

public readonly record struct NeutralDomainBindingLease(
    NeutralDomainBindingHandle Handle,
    NeutralDomainBindingEpoch Epoch)
{
    public bool IsMaterialized => Handle.Value != 0 && Epoch.Value != 0;
}

public enum NeutralDomainBindDecision : byte
{
    Bound = 0,
    UnsupportedProfile,
    Faulted,
}

public readonly record struct NeutralDomainBindResult(
    NeutralDomainBindDecision Decision,
    NeutralDomainBindingLease Lease,
    string Reason)
{
    public bool IsBound =>
        Decision == NeutralDomainBindDecision.Bound && Lease.IsMaterialized;

    internal static NeutralDomainBindResult Bound(NeutralDomainBindingLease lease) =>
        new(
            NeutralDomainBindDecision.Bound,
            lease,
            "Neutral runtime domain binding materialized.");

    internal static NeutralDomainBindResult Denied(
        NeutralDomainBindDecision decision,
        string reason) =>
        new(decision, default, reason);
}

public enum NeutralExecutionState : byte
{
    Ready = 0,
    Running,
    Parked,
}

public enum NeutralExecutionTransition : byte
{
    Start = 0,
    Park,
    Resume,
}

public enum NeutralExecutionTransitionDecision : byte
{
    Transitioned = 0,
    InvalidTransition,
    NotFound,
    Stale,
    Revoked,
    Faulted,
}

public readonly record struct NeutralExecutionTransitionResult(
    NeutralExecutionTransitionDecision Decision,
    NeutralDomainBindingLease Lease,
    NeutralExecutionTransition Transition,
    NeutralExecutionState State,
    string Reason)
{
    public bool IsTransitioned =>
        Decision == NeutralExecutionTransitionDecision.Transitioned;
}

public enum NeutralDomainCloseDecision : byte
{
    Closed = 0,
    NotFound,
    Stale,
    Revoked,
    Faulted,
}

public readonly record struct NeutralDomainCloseResult(
    NeutralDomainCloseDecision Decision,
    string Reason)
{
    public bool IsClosed => Decision == NeutralDomainCloseDecision.Closed;
}

internal readonly record struct NeutralExecutionDomainOwner(
    ulong DomainTag,
    bool CompatibilityProjectionEnabled,
    bool HasMaterializedGuestArchitecturalState);

internal readonly record struct NeutralMemoryDomainOwner(
    ulong AddressSpaceTag,
    bool OwnsSecondStageTranslation);

internal readonly record struct NeutralIoDomainOwner(
    bool OwnsDmaAuthority,
    bool OwnsIommuAuthority,
    bool CompatibilityProjectionEnabled);

internal sealed record NeutralDomainRuntimeContext(
    NeutralExecutionDomainOwner Execution,
    NeutralMemoryDomainOwner Memory,
    NeutralIoDomainOwner Io)
{
    public bool HasRequiredNeutralOwners =>
        Execution.DomainTag != 0 && Memory.AddressSpaceTag != 0;
}

/// <summary>
/// Provider-facing owner for the minimal neutral HybridCPU runtime-domain lifecycle.
/// The returned lease is opaque integration identity: it is not a domain tag,
/// address-space tag, typed capability grant, completion receipt, or compatibility artifact.
/// Execution transitions are semantic lifecycle decisions and expose no scheduler/lane/ISA state.
/// </summary>
public sealed class NeutralDomainRuntimeFacade
{
    private sealed class BindingRecord(
        NeutralDomainBindingLease lease,
        NeutralDomainRuntimeContext context)
    {
        public NeutralDomainBindingLease Lease { get; } = lease;
        public NeutralDomainRuntimeContext Context { get; } = context;
        public NeutralExecutionState ExecutionState { get; set; } = NeutralExecutionState.Ready;
        public bool Revoked { get; set; }
    }

    private readonly Dictionary<NeutralDomainBindingHandle, BindingRecord> _bindings = [];
    private ulong _nextBindingId = 1;
    private ulong _nextEpoch = 1;
    private ulong _nextDomainTag = 0x1000;
    private ulong _nextAddressSpaceTag = 0x100000;

    public int ActiveBindingCount => _bindings.Values.Count(static record => !record.Revoked);

    public NeutralDomainBindResult Bind(NeutralDomainProfile profile)
    {
        if (!Enum.IsDefined(profile) || profile != NeutralDomainProfile.OrdinaryService)
        {
            return NeutralDomainBindResult.Denied(
                NeutralDomainBindDecision.UnsupportedProfile,
                "The requested neutral runtime domain profile is not supported.");
        }

        try
        {
            var context = new NeutralDomainRuntimeContext(
                new NeutralExecutionDomainOwner(
                    NextNonZero(ref _nextDomainTag),
                    CompatibilityProjectionEnabled: false,
                    HasMaterializedGuestArchitecturalState: false),
                new NeutralMemoryDomainOwner(
                    NextNonZero(ref _nextAddressSpaceTag),
                    OwnsSecondStageTranslation: false),
                new NeutralIoDomainOwner(
                    OwnsDmaAuthority: false,
                    OwnsIommuAuthority: false,
                    CompatibilityProjectionEnabled: false));

            if (!context.HasRequiredNeutralOwners)
            {
                return NeutralDomainBindResult.Denied(
                    NeutralDomainBindDecision.Faulted,
                    "The neutral runtime context did not materialize all required owners.");
            }

            var lease = new NeutralDomainBindingLease(
                new NeutralDomainBindingHandle(NextNonZero(ref _nextBindingId)),
                new NeutralDomainBindingEpoch(NextNonZero(ref _nextEpoch)));
            _bindings.Add(lease.Handle, new BindingRecord(lease, context));
            return NeutralDomainBindResult.Bound(lease);
        }
        catch (Exception)
        {
            return NeutralDomainBindResult.Denied(
                NeutralDomainBindDecision.Faulted,
                "Neutral runtime domain materialization faulted.");
        }
    }

    public NeutralExecutionTransitionResult TransitionExecution(
        NeutralDomainBindingLease lease,
        NeutralExecutionTransition transition)
    {
        if (!lease.IsMaterialized)
        {
            return TransitionDenied(
                NeutralExecutionTransitionDecision.NotFound,
                lease,
                transition,
                NeutralExecutionState.Ready,
                "Neutral runtime domain lease is not materialized.");
        }

        if (!_bindings.TryGetValue(lease.Handle, out var record))
        {
            return TransitionDenied(
                NeutralExecutionTransitionDecision.NotFound,
                lease,
                transition,
                NeutralExecutionState.Ready,
                "Neutral runtime domain binding was not found.");
        }

        if (record.Lease.Epoch != lease.Epoch)
        {
            return TransitionDenied(
                NeutralExecutionTransitionDecision.Stale,
                lease,
                transition,
                record.ExecutionState,
                "Neutral runtime domain lease epoch is stale.");
        }

        if (record.Revoked)
        {
            return TransitionDenied(
                NeutralExecutionTransitionDecision.Revoked,
                lease,
                transition,
                record.ExecutionState,
                "Neutral runtime domain binding has already been closed.");
        }

        if (!Enum.IsDefined(transition))
        {
            return TransitionDenied(
                NeutralExecutionTransitionDecision.Faulted,
                lease,
                transition,
                record.ExecutionState,
                "The neutral execution transition is undefined.");
        }

        var nextState = (record.ExecutionState, transition) switch
        {
            (NeutralExecutionState.Ready, NeutralExecutionTransition.Start) =>
                NeutralExecutionState.Running,
            (NeutralExecutionState.Running, NeutralExecutionTransition.Park) =>
                NeutralExecutionState.Parked,
            (NeutralExecutionState.Parked, NeutralExecutionTransition.Resume) =>
                NeutralExecutionState.Running,
            _ => (NeutralExecutionState?)null,
        };

        if (nextState is null)
        {
            return TransitionDenied(
                NeutralExecutionTransitionDecision.InvalidTransition,
                lease,
                transition,
                record.ExecutionState,
                $"Cannot apply {transition} while neutral execution is {record.ExecutionState}.");
        }

        record.ExecutionState = nextState.Value;
        return new NeutralExecutionTransitionResult(
            NeutralExecutionTransitionDecision.Transitioned,
            record.Lease,
            transition,
            record.ExecutionState,
            $"Neutral execution transitioned to {record.ExecutionState}.");
    }

    public NeutralDomainCloseResult Close(NeutralDomainBindingLease lease)
    {
        if (!lease.IsMaterialized)
        {
            return new NeutralDomainCloseResult(
                NeutralDomainCloseDecision.NotFound,
                "Neutral runtime domain lease is not materialized.");
        }

        if (!_bindings.TryGetValue(lease.Handle, out var record))
        {
            return new NeutralDomainCloseResult(
                NeutralDomainCloseDecision.NotFound,
                "Neutral runtime domain binding was not found.");
        }

        if (record.Lease.Epoch != lease.Epoch)
        {
            return new NeutralDomainCloseResult(
                NeutralDomainCloseDecision.Stale,
                "Neutral runtime domain lease epoch is stale.");
        }

        if (record.Revoked)
        {
            return new NeutralDomainCloseResult(
                NeutralDomainCloseDecision.Revoked,
                "Neutral runtime domain binding has already been closed.");
        }

        record.Revoked = true;
        return new NeutralDomainCloseResult(
            NeutralDomainCloseDecision.Closed,
            "Neutral runtime domain binding closed.");
    }

    internal NeutralDomainRuntimeContext? ResolveActiveContextForTesting(
        NeutralDomainBindingLease lease)
    {
        if (!_bindings.TryGetValue(lease.Handle, out var record) ||
            record.Lease.Epoch != lease.Epoch ||
            record.Revoked)
        {
            return null;
        }

        return record.Context;
    }

    private static NeutralExecutionTransitionResult TransitionDenied(
        NeutralExecutionTransitionDecision decision,
        NeutralDomainBindingLease lease,
        NeutralExecutionTransition transition,
        NeutralExecutionState state,
        string reason) =>
        new(decision, lease, transition, state, reason);

    private static ulong NextNonZero(ref ulong next)
    {
        var value = next;
        unchecked { next++; }
        if (value == 0)
            throw new InvalidOperationException("Neutral runtime identity space is exhausted.");
        return value;
    }
}
