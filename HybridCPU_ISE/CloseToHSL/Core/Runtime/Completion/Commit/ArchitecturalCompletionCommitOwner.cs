using System.Security.Cryptography;
using System.Text;

namespace YAKSys_Hybrid_CPU.Core;

internal enum ArchitecturalCompletionCommitDecision : byte
{
    Committed = 0,
    DeniedUnregisteredProducer = 1,
    DeniedProducerPolicy = 2,
    DeniedMissingIdentity = 3,
    DeniedDuplicateOrReplay = 4,
    DeniedStaleCanonicalOrder = 5,
}

internal readonly record struct ArchitecturalCompletionCommitResult(
    ArchitecturalCompletionCommitDecision Decision,
    ArchitecturalCompletionCommitOwner.ArchitecturalCompletionCommitReceipt? Receipt,
    string Reason)
{
    internal bool IsCommitted =>
        Decision == ArchitecturalCompletionCommitDecision.Committed && Receipt is not null;
}

internal readonly record struct ArchitecturalCompletionProducerPolicy(
    string OwnerName,
    NeutralArchitecturalCompletionClass CompletionClass,
    bool RequiresReason,
    bool AllowsQualification,
    NeutralFaultAddressSemantic AllowedAddressSemantic,
    NeutralFaultAuxiliarySemantic AllowedAuxiliarySemantic);

internal readonly record struct ArchitecturalCompletionReceiptBinding(
    ulong CompletionIdentity,
    ulong ProducerOwnerIdentity,
    ulong ProducerOwnerEpoch,
    ulong DomainId,
    int ContextId,
    int VirtualThreadId,
    ulong AttemptId,
    ulong EventId,
    NeutralArchitecturalCompletionClass CompletionClass,
    string CompletionDigest,
    ulong CanonicalOrderSequence,
    ulong CommitSequence,
    ulong RestoreGeneration);

/// <summary>
/// Neutral issuer for evidence that a typed completion crossed the canonical
/// architectural write-back retire boundary. This owner does not store generic
/// retire effects and has no compatibility-frontend or projection authority.
/// </summary>
internal sealed class ArchitecturalCompletionCommitOwner
{
    internal sealed class ProducerRegistration
    {
        private readonly ArchitecturalCompletionCommitOwner _issuer;

        internal ProducerRegistration(
            ArchitecturalCompletionCommitOwner issuer,
            ulong ownerIdentity,
            ulong ownerEpoch,
            ArchitecturalCompletionProducerPolicy policy)
        {
            _issuer = issuer;
            OwnerIdentity = ownerIdentity;
            OwnerEpoch = ownerEpoch;
            Policy = policy;
        }

        internal ulong OwnerIdentity { get; }
        internal ulong OwnerEpoch { get; }
        internal ArchitecturalCompletionProducerPolicy Policy { get; }
        internal bool IsIssuedBy(ArchitecturalCompletionCommitOwner owner) =>
            ReferenceEquals(_issuer, owner);
    }

    internal sealed class ArchitecturalCompletionCommitReceipt
    {
        internal ArchitecturalCompletionCommitReceipt(
            ArchitecturalCompletionCommitOwner issuer,
            ArchitecturalCompletionReceiptBinding binding,
            string issuerSeal)
        {
            Issuer = issuer;
            Binding = binding;
            IssuerSeal = issuerSeal;
        }

        private ArchitecturalCompletionCommitOwner Issuer { get; }
        private string IssuerSeal { get; }
        internal ArchitecturalCompletionReceiptBinding Binding { get; }
        internal bool IsIssuedBy(ArchitecturalCompletionCommitOwner owner) =>
            ReferenceEquals(Issuer, owner);

        internal bool SealEquals(string seal) =>
            IsWellFormedSeal(IssuerSeal) && IsWellFormedSeal(seal) &&
            CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(IssuerSeal),
                Convert.FromHexString(seal));

        private static bool IsWellFormedSeal(string seal)
        {
            if (seal.Length != 64)
                return false;
            for (int index = 0; index < seal.Length; index++)
            {
                if (!char.IsAsciiHexDigit(seal[index]))
                    return false;
            }
            return true;
        }
    }

    private sealed record LiveReceipt(
        ArchitecturalCompletionCommitReceipt Receipt,
        string Seal);

    private readonly object _gate = new();
    private readonly VirtualizationRestoreGenerationOwner _restoreOwner;
    private DomainCompletionObservationOwner _observationOwner;
    private DomainCompletionObservationOwner.CommitInstaller _observationInstaller;
    private readonly byte[] _sealKey = RandomNumberGenerator.GetBytes(32);
    private readonly Dictionary<ulong, ProducerRegistration> _registrations = new();
    private readonly Dictionary<ulong, LiveReceipt> _liveReceipts = new();
    private readonly HashSet<string> _committedCompletionKeys = new(StringComparer.Ordinal);
    private ulong _nextProducerIdentity = 1;
    private ulong _nextCompletionIdentity = 1;
    private ulong _nextCommitSequence = 1;
    private ulong _nextCanonicalOrderSequence = 1;
    private ArchitecturalCompletionCommitReceipt? _latestReceipt;

    internal ArchitecturalCompletionCommitOwner(
        VirtualizationRestoreGenerationOwner restoreOwner,
        DomainCompletionObservationOwner observationOwner)
    {
        _restoreOwner = restoreOwner ?? throw new ArgumentNullException(nameof(restoreOwner));
        _observationOwner = observationOwner ??
            throw new ArgumentNullException(nameof(observationOwner));
        _observationInstaller = _observationOwner.RegisterCommitIssuer(this);
    }

    internal DomainCompletionObservationOwner ObservationOwner
    {
        get
        {
            lock (_gate)
                return _observationOwner;
        }
    }

    internal ulong CurrentRestoreGeneration
        => _restoreOwner.CurrentGeneration;

    internal int LiveReceiptCount
    {
        get
        {
            lock (_gate)
                return _liveReceipts.Count;
        }
    }

    internal ProducerRegistration RegisterProducer(
        ArchitecturalCompletionProducerPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy.OwnerName);
        if (policy.CompletionClass == NeutralArchitecturalCompletionClass.None)
            throw new ArgumentOutOfRangeException(nameof(policy));

        lock (_gate)
        {
            ulong identity = AllocateNonZero(ref _nextProducerIdentity);
            var registration = new ProducerRegistration(this, identity, 1, policy);
            _registrations.Add(identity, registration);
            return registration;
        }
    }

    internal ArchitecturalCompletionCommitResult CommitAtCanonicalRetireBoundary(
        ProducerRegistration? producer,
        in ArchitecturalCompletionCandidate candidate)
    {
        lock (_gate)
        {
            if (producer is null || !producer.IsIssuedBy(this) ||
                !_registrations.TryGetValue(producer.OwnerIdentity, out ProducerRegistration? live) ||
                !ReferenceEquals(live, producer) || live.OwnerEpoch != producer.OwnerEpoch)
            {
                return Denied(
                    ArchitecturalCompletionCommitDecision.DeniedUnregisteredProducer,
                    "Completion commit requires the exact live producer registration.");
            }

            if (!HasCompleteIdentity(candidate))
            {
                return Denied(
                    ArchitecturalCompletionCommitDecision.DeniedMissingIdentity,
                    "Completion commit requires non-zero domain/context/VT attempt and event identity.");
            }

            if (!PolicyAllows(producer.Policy, candidate.Facts))
            {
                return Denied(
                    ArchitecturalCompletionCommitDecision.DeniedProducerPolicy,
                    "Completion facts do not match the exact registered producer policy.");
            }

            string completionDigest = ComputeCompletionDigest(candidate.Facts);
            string completionKey = string.Join(
                ":",
                producer.OwnerIdentity,
                producer.OwnerEpoch,
                candidate.DomainId,
                candidate.ContextId,
                candidate.VirtualThreadId,
                candidate.AttemptId,
                candidate.EventId);
            if (!_committedCompletionKeys.Add(completionKey))
            {
                return Denied(
                    ArchitecturalCompletionCommitDecision.DeniedDuplicateOrReplay,
                    "Completion identity was already committed.");
            }

            ulong canonicalOrderSequence = AllocateNonZero(ref _nextCanonicalOrderSequence);
            ulong commitSequence = AllocateNonZero(ref _nextCommitSequence);
            if (_latestReceipt is not null &&
                canonicalOrderSequence <= _latestReceipt.Binding.CanonicalOrderSequence)
            {
                return Denied(
                    ArchitecturalCompletionCommitDecision.DeniedStaleCanonicalOrder,
                    "Canonical completion order did not advance.");
            }

            var binding = new ArchitecturalCompletionReceiptBinding(
                AllocateNonZero(ref _nextCompletionIdentity),
                producer.OwnerIdentity,
                producer.OwnerEpoch,
                candidate.DomainId,
                candidate.ContextId,
                candidate.VirtualThreadId,
                candidate.AttemptId,
                candidate.EventId,
                candidate.Facts.CompletionClass,
                completionDigest,
                canonicalOrderSequence,
                commitSequence,
                _restoreOwner.CurrentGeneration);
            string seal = ComputeSeal(binding);
            var receipt = new ArchitecturalCompletionCommitReceipt(this, binding, seal);
            _observationOwner.InstallCommittedCompletion(
                _observationInstaller,
                this,
                binding,
                candidate.Facts);
            _liveReceipts.Add(binding.CommitSequence, new LiveReceipt(receipt, seal));
            _latestReceipt = receipt;
            return new(
                ArchitecturalCompletionCommitDecision.Committed,
                receipt,
                "Completion committed at the canonical architectural retire boundary.");
        }
    }

    internal bool ValidateLiveReceipt(
        ArchitecturalCompletionCommitReceipt? receipt,
        in ArchitecturalCompletionReceiptBinding expected)
    {
        lock (_gate)
            return ValidateLiveReceiptUnderLock(receipt, expected);
    }

    internal bool TryConsumeReceipt(
        ArchitecturalCompletionCommitReceipt? receipt,
        in ArchitecturalCompletionReceiptBinding expected)
    {
        lock (_gate)
        {
            if (!ValidateLiveReceiptUnderLock(receipt, expected))
                return false;
            return _liveReceipts.Remove(expected.CommitSequence);
        }
    }

    internal bool TryGetLatestLiveReceipt(
        out ArchitecturalCompletionCommitReceipt? receipt)
    {
        lock (_gate)
        {
            receipt = _latestReceipt;
            return receipt is not null &&
                _liveReceipts.ContainsKey(receipt.Binding.CommitSequence);
        }
    }

    internal void InvalidateAfterRestore()
    {
        lock (_gate)
        {
            _restoreOwner.AdvanceAfterRestore();
            _observationOwner.ClearAfterRestore();
            _liveReceipts.Clear();
            _committedCompletionKeys.Clear();
            _latestReceipt = null;
        }
    }

    internal void ClearObservation(in CompletionObservationScope scope)
    {
        lock (_gate)
            _observationOwner.Clear(scope);
    }

    internal void RebindObservation(in CompletionObservationScope previousScope)
    {
        lock (_gate)
            _observationOwner.Rebind(previousScope);
    }

    internal DomainCompletionObservationOwner ReplaceObservationOwner()
    {
        lock (_gate)
        {
            _observationOwner = _observationOwner.ReplaceOwner(this);
            _observationInstaller = _observationOwner.RegisterCommitIssuer(this);
            return _observationOwner;
        }
    }

    private bool ValidateLiveReceiptUnderLock(
        ArchitecturalCompletionCommitReceipt? receipt,
        in ArchitecturalCompletionReceiptBinding expected)
    {
        if (receipt is null || !receipt.IsIssuedBy(this) || receipt.Binding != expected ||
            expected.RestoreGeneration != _restoreOwner.CurrentGeneration ||
            !_liveReceipts.TryGetValue(expected.CommitSequence, out LiveReceipt? live) ||
            !ReferenceEquals(live.Receipt, receipt))
        {
            return false;
        }

        string expectedSeal = ComputeSeal(expected);
        return receipt.SealEquals(expectedSeal) &&
            CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(live.Seal),
                Convert.FromHexString(expectedSeal));
    }

    private static bool HasCompleteIdentity(in ArchitecturalCompletionCandidate candidate) =>
        candidate.DomainId != 0 && candidate.ContextId > 0 &&
        candidate.VirtualThreadId is >= 0 and < Processor.CPU_Core.SmtWays &&
        candidate.AttemptId != 0 &&
        candidate.EventId != 0 &&
        candidate.Facts.CompletionClass != NeutralArchitecturalCompletionClass.None;

    private static bool PolicyAllows(
        in ArchitecturalCompletionProducerPolicy policy,
        in NeutralArchitecturalCompletionFacts facts)
    {
        if (facts.CompletionClass != policy.CompletionClass ||
            (policy.RequiresReason && !facts.Reason.IsPresent) ||
            (!policy.AllowsQualification && facts.Qualification.IsPresent))
        {
            return false;
        }

        if (facts.FaultAddress.IsPresent)
        {
            if (facts.FaultAddress.Semantic == NeutralFaultAddressSemantic.None ||
                facts.FaultAddress.Semantic != policy.AllowedAddressSemantic)
            {
                return false;
            }
        }
        else if (facts.FaultAddress.Semantic != NeutralFaultAddressSemantic.None)
        {
            return false;
        }

        if (facts.FaultAuxiliary.IsPresent)
        {
            if (facts.FaultAuxiliary.Semantic == NeutralFaultAuxiliarySemantic.None ||
                facts.FaultAuxiliary.Semantic != policy.AllowedAuxiliarySemantic)
            {
                return false;
            }
        }
        else if (facts.FaultAuxiliary.Semantic != NeutralFaultAuxiliarySemantic.None)
        {
            return false;
        }

        return true;
    }

    private static string ComputeCompletionDigest(
        in NeutralArchitecturalCompletionFacts facts)
    {
        string canonical = string.Join(
            "|",
            (byte)facts.CompletionClass,
            facts.Reason.IsPresent ? 1 : 0,
            facts.Reason.Value,
            facts.Qualification.IsPresent ? 1 : 0,
            facts.Qualification.Value,
            facts.FaultAddress.IsPresent ? 1 : 0,
            facts.FaultAddress.Value,
            (byte)facts.FaultAddress.Semantic,
            facts.FaultAuxiliary.IsPresent ? 1 : 0,
            facts.FaultAuxiliary.Value,
            (byte)facts.FaultAuxiliary.Semantic);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    private string ComputeSeal(in ArchitecturalCompletionReceiptBinding binding)
    {
        string canonical = string.Join(
            "|",
            binding.CompletionIdentity,
            binding.ProducerOwnerIdentity,
            binding.ProducerOwnerEpoch,
            binding.DomainId,
            binding.ContextId,
            binding.VirtualThreadId,
            binding.AttemptId,
            binding.EventId,
            (byte)binding.CompletionClass,
            binding.CompletionDigest,
            binding.CanonicalOrderSequence,
            binding.CommitSequence,
            binding.RestoreGeneration);
        return Convert.ToHexString(HMACSHA256.HashData(
                _sealKey,
                Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    private static ulong AllocateNonZero(ref ulong next)
    {
        ulong value = next;
        AdvanceNonZero(ref next);
        return value == 0 ? AllocateNonZero(ref next) : value;
    }

    private static void AdvanceNonZero(ref ulong value)
    {
        unchecked
        {
            value++;
            if (value == 0)
                value = 1;
        }
    }

    private static ArchitecturalCompletionCommitResult Denied(
        ArchitecturalCompletionCommitDecision decision,
        string reason) => new(decision, null, reason);
}
