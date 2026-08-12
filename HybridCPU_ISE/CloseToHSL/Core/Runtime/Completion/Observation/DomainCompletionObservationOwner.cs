namespace YAKSys_Hybrid_CPU.Core;

internal enum CompletionObservationMigrationClass : byte
{
    RecomputedCompletion = 1,
}

internal enum CompletionObservationDecision : byte
{
    Observed = 0,
    DeniedInvalidScope = 1,
    DeniedAbsent = 2,
    DeniedInactiveOwner = 3,
}

internal enum NeutralCompletionObservationField : byte
{
    Reason = 0,
    Qualification = 1,
    GuestPhysicalAddress = 2,
    SecondStageTranslationViolationAuxiliary = 3,
}

internal enum NeutralCompletionFieldDecision : byte
{
    Present = 0,
    DeniedAbsent = 1,
    DeniedSemanticMismatch = 2,
    DeniedScope = 3,
    DeniedInactiveOwner = 4,
}

internal readonly record struct CompletionObservationScope(
    ulong DomainId,
    int ContextId,
    int VirtualThreadId)
{
    internal bool IsValid =>
        DomainId != 0 && ContextId > 0 &&
        VirtualThreadId is >= 0 and < Processor.CPU_Core.SmtWays;
}

internal readonly record struct NeutralCompletionObservationSnapshot(
    CompletionObservationScope Scope,
    ulong CompletionGeneration,
    ulong CompletionIdentity,
    ulong ProducerOwnerIdentity,
    ulong ProducerOwnerEpoch,
    ulong AttemptId,
    ulong EventId,
    NeutralArchitecturalCompletionClass CompletionClass,
    string CompletionDigest,
    ulong CanonicalOrderSequence,
    ulong CommitSequence,
    ulong RestoreGeneration,
    NeutralArchitecturalCompletionFacts Facts);

internal readonly record struct CompletionObservationResult(
    CompletionObservationDecision Decision,
    NeutralCompletionObservationSnapshot? Snapshot)
{
    internal bool IsObserved =>
        Decision == CompletionObservationDecision.Observed && Snapshot.HasValue;
}

internal readonly record struct NeutralCompletionFieldResult(
    NeutralCompletionFieldDecision Decision,
    ulong Value)
{
    internal bool IsPresent => Decision == NeutralCompletionFieldDecision.Present;
}

internal sealed class CompletionGenerationAuthority
{
    private readonly object _gate = new();
    private ulong _current = 1;

    internal ulong Current
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    internal ulong Advance()
    {
        lock (_gate)
        {
            unchecked
            {
                _current++;
                if (_current == 0)
                    _current = 1;
            }
            return _current;
        }
    }
}

/// <summary>
/// Neutral, runtime-local, read-only observation of completions that have
/// crossed the architectural commit boundary. It stores no compatibility
/// projection fields or precomputed projection values.
/// </summary>
internal sealed class DomainCompletionObservationOwner
{
    internal sealed class CommitInstaller
    {
        private readonly DomainCompletionObservationOwner _owner;
        private readonly ArchitecturalCompletionCommitOwner _issuer;

        internal CommitInstaller(
            DomainCompletionObservationOwner owner,
            ArchitecturalCompletionCommitOwner issuer)
        {
            _owner = owner;
            _issuer = issuer;
        }

        internal bool Matches(
            DomainCompletionObservationOwner owner,
            ArchitecturalCompletionCommitOwner issuer) =>
            ReferenceEquals(_owner, owner) && ReferenceEquals(_issuer, issuer);
    }

    private readonly object _gate = new();
    private readonly CompletionGenerationAuthority _generationAuthority;
    private readonly Dictionary<CompletionObservationScope, NeutralCompletionObservationSnapshot>
        _snapshots = new();
    private ArchitecturalCompletionCommitOwner? _commitIssuer;
    private CommitInstaller? _liveInstaller;
    private bool _isActive = true;

    internal DomainCompletionObservationOwner(
        CompletionGenerationAuthority generationAuthority)
    {
        _generationAuthority = generationAuthority ??
            throw new ArgumentNullException(nameof(generationAuthority));
    }

    internal CompletionObservationMigrationClass MigrationClass =>
        CompletionObservationMigrationClass.RecomputedCompletion;

    internal ulong CurrentGeneration => _generationAuthority.Current;

    internal CommitInstaller RegisterCommitIssuer(
        ArchitecturalCompletionCommitOwner issuer)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        lock (_gate)
        {
            if (!_isActive || _commitIssuer is not null)
                throw new InvalidOperationException(
                    "Completion observation accepts exactly one live commit issuer.");
            _commitIssuer = issuer;
            _liveInstaller = new CommitInstaller(this, issuer);
            return _liveInstaller;
        }
    }

    internal void InstallCommittedCompletion(
        CommitInstaller installer,
        ArchitecturalCompletionCommitOwner issuer,
        in ArchitecturalCompletionReceiptBinding binding,
        in NeutralArchitecturalCompletionFacts facts)
    {
        lock (_gate)
        {
            if (!_isActive || _liveInstaller is null ||
                !ReferenceEquals(_liveInstaller, installer) ||
                !installer.Matches(this, issuer) ||
                !ReferenceEquals(_commitIssuer, issuer))
            {
                throw new InvalidOperationException(
                    "Completion observation installation requires its exact live commit issuer capability.");
            }

            var scope = new CompletionObservationScope(
                binding.DomainId,
                binding.ContextId,
                binding.VirtualThreadId);
            if (!scope.IsValid || binding.CompletionIdentity == 0 ||
                binding.CommitSequence == 0 || binding.RestoreGeneration == 0 ||
                facts.CompletionClass != binding.CompletionClass)
            {
                throw new InvalidOperationException(
                    "Committed completion binding is not valid for neutral observation.");
            }

            ulong generation = _generationAuthority.Advance();
            _snapshots[scope] = new NeutralCompletionObservationSnapshot(
                scope,
                generation,
                binding.CompletionIdentity,
                binding.ProducerOwnerIdentity,
                binding.ProducerOwnerEpoch,
                binding.AttemptId,
                binding.EventId,
                binding.CompletionClass,
                binding.CompletionDigest,
                binding.CanonicalOrderSequence,
                binding.CommitSequence,
                binding.RestoreGeneration,
                facts);
        }
    }

    internal CompletionObservationResult Observe(
        in CompletionObservationScope scope)
    {
        if (!scope.IsValid)
            return new(CompletionObservationDecision.DeniedInvalidScope, null);

        lock (_gate)
        {
            if (!_isActive)
                return new(CompletionObservationDecision.DeniedInactiveOwner, null);
            return _snapshots.TryGetValue(scope, out NeutralCompletionObservationSnapshot snapshot)
                ? new(CompletionObservationDecision.Observed, snapshot)
                : new(CompletionObservationDecision.DeniedAbsent, null);
        }
    }

    internal NeutralCompletionFieldResult ReadField(
        in CompletionObservationScope scope,
        NeutralCompletionObservationField field)
    {
        CompletionObservationResult observation = Observe(scope);
        if (!observation.IsObserved)
        {
            return new(
                observation.Decision switch
                {
                    CompletionObservationDecision.DeniedInactiveOwner =>
                        NeutralCompletionFieldDecision.DeniedInactiveOwner,
                    CompletionObservationDecision.DeniedAbsent =>
                        NeutralCompletionFieldDecision.DeniedAbsent,
                    _ => NeutralCompletionFieldDecision.DeniedScope,
                },
                0);
        }

        NeutralArchitecturalCompletionFacts facts = observation.Snapshot!.Value.Facts;
        return field switch
        {
            NeutralCompletionObservationField.Reason =>
                ScalarResult(facts.Reason),
            NeutralCompletionObservationField.Qualification =>
                ScalarResult(facts.Qualification),
            NeutralCompletionObservationField.GuestPhysicalAddress =>
                AddressResult(
                    facts.FaultAddress,
                    NeutralFaultAddressSemantic.GuestPhysicalAddress),
            NeutralCompletionObservationField.SecondStageTranslationViolationAuxiliary =>
                AuxiliaryResult(
                    facts.FaultAuxiliary,
                    NeutralFaultAuxiliarySemantic.SecondStageTranslationViolation),
            _ => new(NeutralCompletionFieldDecision.DeniedSemanticMismatch, 0),
        };
    }

    internal void Clear(in CompletionObservationScope scope)
    {
        if (!scope.IsValid)
            throw new ArgumentOutOfRangeException(nameof(scope));
        lock (_gate)
        {
            EnsureActive();
            _snapshots.Remove(scope);
            _generationAuthority.Advance();
        }
    }

    internal void Rebind(in CompletionObservationScope previousScope)
    {
        Clear(previousScope);
    }

    internal void ClearAfterRestore()
    {
        lock (_gate)
        {
            EnsureActive();
            _snapshots.Clear();
            _generationAuthority.Advance();
        }
    }

    internal DomainCompletionObservationOwner ReplaceOwner(
        ArchitecturalCompletionCommitOwner issuer)
    {
        lock (_gate)
        {
            EnsureActive();
            if (!ReferenceEquals(_commitIssuer, issuer))
                throw new InvalidOperationException("Only the exact commit issuer may replace observation ownership.");
            _snapshots.Clear();
            _isActive = false;
            _liveInstaller = null;
            _generationAuthority.Advance();
        }

        return new DomainCompletionObservationOwner(_generationAuthority);
    }

    private void EnsureActive()
    {
        if (!_isActive)
            throw new InvalidOperationException("Completion observation owner was replaced.");
    }

    private static NeutralCompletionFieldResult ScalarResult(
        in NeutralScalarFact fact) =>
        fact.IsPresent
            ? new(NeutralCompletionFieldDecision.Present, fact.Value)
            : new(NeutralCompletionFieldDecision.DeniedAbsent, 0);

    private static NeutralCompletionFieldResult AddressResult(
        in NeutralAddressFact fact,
        NeutralFaultAddressSemantic requiredSemantic)
    {
        if (!fact.IsPresent)
            return new(NeutralCompletionFieldDecision.DeniedAbsent, 0);
        return fact.Semantic == requiredSemantic
            ? new(NeutralCompletionFieldDecision.Present, fact.Value)
            : new(NeutralCompletionFieldDecision.DeniedSemanticMismatch, 0);
    }

    private static NeutralCompletionFieldResult AuxiliaryResult(
        in NeutralAuxiliaryFact fact,
        NeutralFaultAuxiliarySemantic requiredSemantic)
    {
        if (!fact.IsPresent)
            return new(NeutralCompletionFieldDecision.DeniedAbsent, 0);
        return fact.Semantic == requiredSemantic
            ? new(NeutralCompletionFieldDecision.Present, fact.Value)
            : new(NeutralCompletionFieldDecision.DeniedSemanticMismatch, 0);
    }
}
