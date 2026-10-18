using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace YAKSys_Hybrid_CPU.Core;

internal enum DomainHypercallDrainDecision : byte
{
    CheckpointReady = 0,
    InFlightAuthority = 1,
    InvalidDomain = 2,
    InvalidCheckpoint = 3,
    AlreadyRestored = 4,
}

internal readonly record struct DomainHypercallLiveAuthorityCounts(
    int E2,
    int E3,
    int E5,
    int E6,
    int TransitionsInFlight = 0)
{
    internal bool IsDrained =>
        E2 == 0 && E3 == 0 && E5 == 0 && E6 == 0 && TransitionsInFlight == 0;
    internal int Total => checked(E2 + E3 + E5 + E6 + TransitionsInFlight);
}

internal sealed record DomainHypercallDrainCheckpoint
{
    internal DomainHypercallDrainCheckpoint(
        ulong domainTag,
        ulong checkpointEpoch,
        ulong restoreGeneration,
        string decisionId,
        string specDigest,
        string checkpointDigest)
    {
        DomainTag = domainTag;
        CheckpointEpoch = checkpointEpoch;
        RestoreGeneration = restoreGeneration;
        DecisionId = decisionId;
        SpecDigest = specDigest;
        CheckpointDigest = checkpointDigest;
    }

    internal uint SchemaVersion => 1;
    internal ulong DomainTag { get; }
    internal ulong CheckpointEpoch { get; }
    internal ulong RestoreGeneration { get; }
    internal string DecisionId { get; }
    internal string SpecDigest { get; }
    internal string CheckpointDigest { get; }
    internal DomainHypercallLiveAuthorityCounts LiveAuthorityCounts => new(0, 0, 0, 0, 0);
    internal bool ContainsE1 => false;
    internal bool ContainsOperandSnapshot => false;
    internal bool ContainsE2 => false;
    internal bool ContainsE3 => false;
    internal bool ContainsE5 => false;
    internal bool ContainsE6 => false;
    internal bool ContainsCapabilityHandle => false;
    internal bool ContainsOwnerSeal => false;
    internal bool ContainsBackendReceipt => false;
    internal bool ContainsCompatibilityProjection => false;
    internal bool ContainsHostOwnedEvidence => false;
    internal bool ContainsRuntimeAuthority =>
        ContainsE1 || ContainsOperandSnapshot || ContainsE2 || ContainsE3 || ContainsE5 || ContainsE6 ||
        ContainsCapabilityHandle || ContainsOwnerSeal || ContainsBackendReceipt ||
        ContainsCompatibilityProjection || ContainsHostOwnedEvidence;
}

internal readonly record struct DomainHypercallDrainResult(
    DomainHypercallDrainDecision Decision,
    DomainHypercallLiveAuthorityCounts Counts,
    DomainHypercallDrainCheckpoint? Checkpoint,
    int CancelledAuthorities,
    string Reason)
{
    internal bool IsCheckpointReady =>
        Decision == DomainHypercallDrainDecision.CheckpointReady && Checkpoint is not null;
}

internal readonly record struct DomainHypercallArchitecturalTrace(
    string OperationId,
    int VirtualThreadId,
    ulong DomainTag,
    bool Retired,
    bool Faulted,
    int RegisterWrites,
    int MemoryWrites,
    int VmStateWrites,
    int Redirects,
    string Digest)
{
    internal static DomainHypercallArchitecturalTrace ExactProbe(
        int virtualThreadId,
        ulong domainTag,
        bool retired,
        bool faulted)
    {
        const string operation = "PROBE_NO_STATE_V1";
        string canonical = string.Join('|', operation, virtualThreadId, domainTag,
            retired ? 1 : 0, faulted ? 1 : 0, 0, 0, 0, 0);
        string digest = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return new(operation, virtualThreadId, domainTag, retired, faulted, 0, 0, 0, 0, digest);
    }
}

/// <summary>
/// Neutral DrainOnly lifecycle owner for the exact Phase-38 operation. A
/// checkpoint is possible only after every live authority registry reports zero.
/// The checkpoint contains policy identity only and never serializes runtime
/// tokens, receipts, seals, compatibility projections, or host evidence.
/// </summary>
internal sealed class DomainHypercallDrainLifecycleOwner
{
    private static readonly byte[] Envelope = Encoding.ASCII.GetBytes("HCPUE7V1\0");
    private readonly object _sync = new();
    private readonly ulong _domainTag;
    private readonly DomainHypercallCanonicalComposition _composition;
    private readonly SafetyVerifier _verifier;
    private readonly DomainHypercallRuntimeExecutor _executor;
    private readonly DomainHypercallCompletionOwner _completionOwner;
    private readonly DomainHypercallRetireOwner _retireOwner;
    private readonly VirtualizationRestoreGenerationOwner _restoreOwner;
    private readonly DomainHypercallLifecycleGate _lifecycleGate;
    private ulong _nextCheckpointEpoch = 1;
    private readonly HashSet<string> _restoredCheckpointDigests = new(StringComparer.Ordinal);

    internal DomainHypercallDrainLifecycleOwner(
        ulong domainTag,
        DomainHypercallCanonicalComposition composition,
        SafetyVerifier verifier,
        DomainHypercallRuntimeExecutor executor,
        DomainHypercallCompletionOwner completionOwner,
        DomainHypercallRetireOwner retireOwner,
        VirtualizationRestoreGenerationOwner restoreOwner)
    {
        if (domainTag == 0)
            throw new ArgumentOutOfRangeException(nameof(domainTag));
        _domainTag = domainTag;
        _composition = composition ?? throw new ArgumentNullException(nameof(composition));
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _completionOwner = completionOwner ?? throw new ArgumentNullException(nameof(completionOwner));
        _retireOwner = retireOwner ?? throw new ArgumentNullException(nameof(retireOwner));
        _restoreOwner = restoreOwner ?? throw new ArgumentNullException(nameof(restoreOwner));
        _lifecycleGate = composition.LifecycleGate;
        if (_lifecycleGate.DomainTag != domainTag)
            throw new InvalidOperationException("Drain lifecycle owner requires the composition's matching per-domain lifecycle gate.");
    }

    internal DomainHypercallLiveAuthorityCounts ObserveLiveAuthorities()
    {
        DomainHypercallLifecycleSnapshot lifecycle = _lifecycleGate.Observe();
        return new(
            _verifier.CountLiveVirtualizationE2(_domainTag),
            _executor.CountLiveReceipts(_domainTag),
            _completionOwner.CountLiveTokens(_domainTag),
            _retireOwner.CountLiveGrants(_domainTag),
            lifecycle.TransitionsInFlight);
    }

    internal DomainHypercallDrainResult TryCheckpoint()
    {
        lock (_sync)
        {
            _composition.BeginDrain();
            DomainHypercallLiveAuthorityCounts counts = ObserveLiveAuthorities();
            if (!counts.IsDrained)
                return new(DomainHypercallDrainDecision.InFlightAuthority, counts, null, 0,
                    "DrainOnly checkpoint denied while E2/E3/E5/E6 authority is live.");

            ulong epoch = AllocateCheckpointEpoch();
            string specDigest = Phase38VirtualizationDecisionSpecV2.Instance.SpecDigest;
            string digest = ComputeCheckpointDigest(
                _domainTag, epoch, _restoreOwner.CurrentGeneration,
                VirtualizationDecisionValidatorV2.ExpectedDecisionId, specDigest);
            var checkpoint = new DomainHypercallDrainCheckpoint(
                _domainTag, epoch, _restoreOwner.CurrentGeneration,
                VirtualizationDecisionValidatorV2.ExpectedDecisionId, specDigest, digest);
            return new(DomainHypercallDrainDecision.CheckpointReady, counts, checkpoint, 0,
                "DrainOnly checkpoint contains policy identity and zero live runtime authority.");
        }
    }

    internal DomainHypercallDrainResult CancelAndCheckpoint(TimeSpan? transitionTimeout = null)
    {
        lock (_sync)
        {
            _composition.BeginDrain();
            if (!_lifecycleGate.WaitForTransitionQuiescence(
                    transitionTimeout ?? TimeSpan.FromSeconds(30)))
            {
                DomainHypercallLiveAuthorityCounts inFlight = ObserveLiveAuthorities();
                return new(DomainHypercallDrainDecision.InFlightAuthority, inFlight, null, 0,
                    "Drain cancellation timed out waiting for transition-in-flight quiescence.");
            }
            int cancelled = 0;
            cancelled += _verifier.CancelLiveVirtualizationE2ForDrain(_domainTag);
            cancelled += _executor.CancelLiveReceiptsForDrain(_domainTag);
            cancelled += _completionOwner.CancelLiveTokensForDrain(_domainTag);
            cancelled += _retireOwner.CancelLiveGrantsForDrain(_domainTag);
            DomainHypercallDrainResult result = TryCheckpoint();
            return result with { CancelledAuthorities = cancelled };
        }
    }

    internal DomainHypercallDrainResult Restore(DomainHypercallDrainCheckpoint? checkpoint)
    {
        lock (_sync)
        {
            DomainHypercallLiveAuthorityCounts counts = ObserveLiveAuthorities();
            if (checkpoint is not null && _restoredCheckpointDigests.Contains(checkpoint.CheckpointDigest))
                return new(DomainHypercallDrainDecision.AlreadyRestored, counts, null, 0,
                    "A DrainOnly checkpoint may restore only once.");
            if (checkpoint is null || checkpoint.SchemaVersion != 1 || checkpoint.DomainTag != _domainTag ||
                checkpoint.CheckpointEpoch == 0 || !counts.IsDrained || checkpoint.LiveAuthorityCounts.Total != 0 ||
                checkpoint.ContainsRuntimeAuthority || checkpoint.RestoreGeneration != _restoreOwner.CurrentGeneration ||
                !string.Equals(checkpoint.DecisionId, VirtualizationDecisionValidatorV2.ExpectedDecisionId, StringComparison.Ordinal) ||
                !string.Equals(checkpoint.SpecDigest, Phase38VirtualizationDecisionSpecV2.Instance.SpecDigest, StringComparison.Ordinal) ||
                !string.Equals(checkpoint.CheckpointDigest, ComputeCheckpointDigest(
                    checkpoint.DomainTag, checkpoint.CheckpointEpoch, checkpoint.RestoreGeneration,
                    checkpoint.DecisionId, checkpoint.SpecDigest), StringComparison.Ordinal))
                return new(DomainHypercallDrainDecision.InvalidCheckpoint, counts, null, 0,
                    "Restore denied a stale, foreign, non-drained, or noncanonical checkpoint.");
            _restoredCheckpointDigests.Add(checkpoint.CheckpointDigest);

            _verifier.InvalidateVirtualizationAdmissions(ReplayPhaseInvalidationReason.DomainBoundary);
            _restoreOwner.AdvanceAfterRestore();
            if (!_composition.ResumeAfterValidatedRestore(checkpoint.SpecDigest))
                return new(DomainHypercallDrainDecision.InvalidCheckpoint, counts, null, 0,
                    "Local accepted D2/O1 reload failed after restore.");
            return new(DomainHypercallDrainDecision.CheckpointReady, ObserveLiveAuthorities(), checkpoint, 0,
                "Restore advanced generation, invalidated pre-restore authority, and reloaded exact local policy.");
        }
    }

    private ulong AllocateCheckpointEpoch()
    {
        ulong value = _nextCheckpointEpoch++;
        return value == 0 ? _nextCheckpointEpoch++ : value;
    }

    private static string ComputeCheckpointDigest(
        ulong domainTag,
        ulong epoch,
        ulong restoreGeneration,
        string decisionId,
        string specDigest)
    {
        using var stream = new MemoryStream();
        stream.Write(Envelope);
        WriteU64(stream, domainTag); WriteU64(stream, epoch); WriteU64(stream, restoreGeneration);
        WriteString(stream, decisionId); WriteString(stream, specDigest);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteString(Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value); WriteU32(stream, checked((uint)bytes.Length)); stream.Write(bytes);
    }

    private static void WriteU32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(bytes, value); stream.Write(bytes);
    }

    private static void WriteU64(Stream stream, ulong value)
    {
        Span<byte> bytes = stackalloc byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(bytes, value); stream.Write(bytes);
    }
}
