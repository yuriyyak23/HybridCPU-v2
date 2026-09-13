using System.Diagnostics;

namespace YAKSys_Hybrid_CPU.Core;

internal enum DomainHypercallLifecycleState : byte
{
    DisabledFaultOnly = 0,
    ActiveExactProfile = 1,
    Draining = 2,
}

internal enum DomainHypercallTransitionKind : byte
{
    NewE2 = 0,
    E2ToE3 = 1,
    E3ToE5 = 2,
    E5ToE6 = 3,
}

internal readonly record struct DomainHypercallExactActivationRequest(
    string DecisionId,
    string SpecDigest,
    string OperationNamespace,
    ushort NumericLeaf,
    string OperationId,
    ulong OwnerId,
    uint OwnerPolicyVersion,
    uint OwnerEpoch)
{
    internal static DomainHypercallExactActivationRequest Phase38Exact => new(
        VirtualizationDecisionValidatorV2.ExpectedDecisionId,
        Phase38VirtualizationDecisionSpecV2.Instance.SpecDigest,
        VirtualizationDecisionValidatorV2.ExpectedOperationNamespace,
        0x0001,
        VirtualizationDecisionValidatorV2.ExpectedOperationId,
        VirtualizationDecisionValidatorV2.ExpectedOwnerId,
        1,
        1);

    internal bool IsPhase38Exact =>
        string.Equals(DecisionId, VirtualizationDecisionValidatorV2.ExpectedDecisionId, StringComparison.Ordinal) &&
        string.Equals(SpecDigest, Phase38VirtualizationDecisionSpecV2.Instance.SpecDigest, StringComparison.Ordinal) &&
        string.Equals(OperationNamespace, VirtualizationDecisionValidatorV2.ExpectedOperationNamespace, StringComparison.Ordinal) &&
        NumericLeaf == 0x0001 &&
        string.Equals(OperationId, VirtualizationDecisionValidatorV2.ExpectedOperationId, StringComparison.Ordinal) &&
        OwnerId == VirtualizationDecisionValidatorV2.ExpectedOwnerId &&
        OwnerPolicyVersion == 1 &&
        OwnerEpoch == 1;
}

internal readonly record struct DomainHypercallLifecycleSnapshot(
    ulong DomainTag,
    DomainHypercallLifecycleState State,
    ulong LifecycleEpoch,
    int TransitionsInFlight)
{
    internal bool AcceptsNewE2 => State == DomainHypercallLifecycleState.ActiveExactProfile;
    internal bool IsTransitionQuiescent => TransitionsInFlight == 0;
}

/// <summary>
/// Per-domain lifecycle/quiescence gate for the exact Phase-38 runtime profile.
/// It owns no operation authority. E2/E3/E5/E6 remain owned by their existing
/// neutral registries; this gate only prevents an authority handoff from being
/// mistaken for cross-registry quiescence.
/// </summary>
internal sealed class DomainHypercallLifecycleGate
{
    internal sealed class TransitionLease : IDisposable
    {
        private DomainHypercallLifecycleGate? _owner;

        internal TransitionLease(
            DomainHypercallLifecycleGate owner,
            DomainHypercallTransitionKind kind,
            ulong lifecycleEpoch)
        {
            _owner = owner;
            Kind = kind;
            LifecycleEpoch = lifecycleEpoch;
        }

        internal DomainHypercallTransitionKind Kind { get; }
        internal ulong LifecycleEpoch { get; }
        internal DomainHypercallLifecycleGate LifecycleGate =>
            _owner ?? throw new ObjectDisposedException(nameof(TransitionLease));

        public void Dispose()
        {
            DomainHypercallLifecycleGate? owner = Interlocked.Exchange(ref _owner, null);
            owner?.CompleteTransition(this);
        }
    }

    private readonly object _sync = new();
    private readonly ulong _domainTag;
    private DomainHypercallLifecycleState _state = DomainHypercallLifecycleState.DisabledFaultOnly;
    private ulong _lifecycleEpoch = 1;
    private int _transitionsInFlight;

#if TESTING
    internal Action<DomainHypercallTransitionKind>? TransitionGapTestHook { get; set; }
#endif

    internal DomainHypercallLifecycleGate(ulong domainTag)
    {
        if (domainTag == 0)
            throw new ArgumentOutOfRangeException(nameof(domainTag));
        _domainTag = domainTag;
    }

    internal ulong DomainTag => _domainTag;

    internal DomainHypercallLifecycleSnapshot Observe()
    {
        lock (_sync)
            return new(_domainTag, _state, _lifecycleEpoch, _transitionsInFlight);
    }

    internal bool TryActivateExact(DomainHypercallExactActivationRequest request)
    {
        if (!request.IsPhase38Exact)
            return false;

        lock (_sync)
        {
            if (_state != DomainHypercallLifecycleState.DisabledFaultOnly || _transitionsInFlight != 0)
                return false;
            _state = DomainHypercallLifecycleState.ActiveExactProfile;
            AdvanceEpoch();
            return true;
        }
    }

    internal bool TryBeginTransition(
        ulong domainTag,
        DomainHypercallTransitionKind kind,
        out TransitionLease? lease)
    {
        lock (_sync)
        {
            if (domainTag != _domainTag ||
                _state != DomainHypercallLifecycleState.ActiveExactProfile)
            {
                lease = null;
                return false;
            }

            checked { _transitionsInFlight++; }
            lease = new TransitionLease(this, kind, _lifecycleEpoch);
            return true;
        }
    }

    internal void BeginDrain()
    {
        lock (_sync)
        {
            if (_state == DomainHypercallLifecycleState.ActiveExactProfile)
            {
                _state = DomainHypercallLifecycleState.Draining;
                AdvanceEpoch();
            }
        }
    }

    internal bool WaitForTransitionQuiescence(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        lock (_sync)
        {
            if (_transitionsInFlight == 0)
                return true;

            if (timeout == Timeout.InfiniteTimeSpan)
            {
                while (_transitionsInFlight != 0)
                    Monitor.Wait(_sync);
                return true;
            }

            var stopwatch = Stopwatch.StartNew();
            TimeSpan remaining = timeout;
            while (_transitionsInFlight != 0 && remaining > TimeSpan.Zero)
            {
                Monitor.Wait(_sync, remaining);
                remaining = timeout - stopwatch.Elapsed;
            }
            return _transitionsInFlight == 0;
        }
    }

    internal bool ResumeAfterValidatedRestore(string specDigest)
    {
        lock (_sync)
        {
            if (_state != DomainHypercallLifecycleState.Draining || _transitionsInFlight != 0 ||
                !string.Equals(specDigest, Phase38VirtualizationDecisionSpecV2.Instance.SpecDigest, StringComparison.Ordinal))
                return false;
            _state = DomainHypercallLifecycleState.ActiveExactProfile;
            AdvanceEpoch();
            return true;
        }
    }

    internal bool RevokeExactBinding()
    {
        lock (_sync)
        {
            if (_transitionsInFlight != 0)
                return false;
            _state = DomainHypercallLifecycleState.DisabledFaultOnly;
            AdvanceEpoch();
            return true;
        }
    }

#if TESTING
    internal void NotifyTransitionGapForTesting(DomainHypercallTransitionKind kind) =>
        TransitionGapTestHook?.Invoke(kind);
#endif

    private void CompleteTransition(TransitionLease lease)
    {
        lock (_sync)
        {
            if (lease.LifecycleEpoch == 0 || _transitionsInFlight <= 0)
                throw new InvalidOperationException("Exact lifecycle transition accounting underflow.");
            _transitionsInFlight--;
            if (_transitionsInFlight == 0)
                Monitor.PulseAll(_sync);
        }
    }

    private void AdvanceEpoch()
    {
        unchecked
        {
            _lifecycleEpoch++;
            if (_lifecycleEpoch == 0)
                _lifecycleEpoch = 1;
        }
    }
}
