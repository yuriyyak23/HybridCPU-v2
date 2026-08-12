using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace YAKSys_Hybrid_CPU.Core;

internal enum DomainHypercallRetireDecision : byte
{
    Issued = 0,
    MissingCompletion = 1,
    NotCanonicalHead = 2,
    InvalidCarrier = 3,
    IdentityMismatch = 4,
    StaleRestoreGeneration = 5,
    DuplicateOrForeignE5 = 6,
    InvalidWindow = 7,
    LifecycleGateDenied = 8,
}

internal readonly record struct DomainHypercallRetireEligibility(
    VmxMicroOp Carrier,
    int VirtualThreadId,
    ulong DomainTag,
    int SourceSlotId,
    int WorkingSlotId,
    ulong WorkingBundleSequence,
    ulong OperationAttempt,
    int PhysicalLaneId,
    byte RetireOrderIndex,
    ulong RetireWindowIdentity,
    ulong OrderEpoch,
    bool IsCanonicalHead,
    bool IsSquashed,
    bool HasWinningException);

internal readonly record struct DomainHypercallRetireResult(
    DomainHypercallRetireDecision Decision,
    DomainHypercallRetireOwner.VirtualizationRetireGrant? E6,
    string Reason)
{
    internal bool IsIssued => Decision == DomainHypercallRetireDecision.Issued && E6 is not null;
}

/// <summary>
/// Per-CPU neutral retire owner for the exact no-state Phase-38 operation. It can
/// issue E6 only from the already selected canonical WB retire head and consumes
/// both E5 and E6 exactly once. It is not a compatibility effect or registry.
/// </summary>
internal sealed class DomainHypercallRetireOwner
{
    internal sealed class VirtualizationRetireGrant
    {
        private readonly object _issuerSeal;

        private VirtualizationRetireGrant(
            object issuerSeal,
            ulong sequence,
            DomainHypercallCompletionOwner.CompletionPublicationToken e5,
            in DomainHypercallRetireEligibility eligibility,
            string digest)
        {
            _issuerSeal = issuerSeal;
            Sequence = sequence;
            AttemptId = e5.AttemptId;
            VirtualThreadId = eligibility.VirtualThreadId;
            DomainTag = eligibility.DomainTag;
            SourceSlotId = eligibility.SourceSlotId;
            WorkingSlotId = eligibility.WorkingSlotId;
            WorkingBundleSequence = eligibility.WorkingBundleSequence;
            OperationAttempt = eligibility.OperationAttempt;
            PhysicalLaneId = eligibility.PhysicalLaneId;
            RetireWindowIdentity = eligibility.RetireWindowIdentity;
            OrderEpoch = eligibility.OrderEpoch;
            RestoreGeneration = e5.RestoreGeneration;
            E5Digest = e5.TokenDigest;
            GrantDigest = digest;
        }

        internal uint SchemaVersion => 1;
        internal ulong Sequence { get; }
        internal ulong AttemptId { get; }
        internal int VirtualThreadId { get; }
        internal ulong DomainTag { get; }
        internal int SourceSlotId { get; }
        internal int WorkingSlotId { get; }
        internal ulong WorkingBundleSequence { get; }
        internal ulong OperationAttempt { get; }
        internal int PhysicalLaneId { get; }
        internal ulong RetireWindowIdentity { get; }
        internal ulong OrderEpoch { get; }
        internal ulong RestoreGeneration { get; }
        internal string E5Digest { get; }
        internal string GrantDigest { get; }
        internal bool WasIssuedBy(object seal) => ReferenceEquals(_issuerSeal, seal);

        internal static VirtualizationRetireGrant Create(
            object issuerSeal,
            ulong sequence,
            DomainHypercallCompletionOwner.CompletionPublicationToken e5,
            in DomainHypercallRetireEligibility eligibility,
            string digest) => new(issuerSeal, sequence, e5, eligibility, digest);
    }

    private sealed record LiveGrant(string Digest);
    private static readonly byte[] Envelope = Encoding.ASCII.GetBytes("HCPUE6V1\0");
    private readonly object _consumerSeal = new();
    private readonly object _issuerSeal = new();
    private readonly object _sync = new();
    private readonly ConditionalWeakTable<VirtualizationRetireGrant, LiveGrant> _live = new();
    private readonly HashSet<VirtualizationRetireGrant> _liveGrants = new();
    private ulong _nextSequence = 1;

    internal object ConsumerSeal => _consumerSeal;

    internal DomainHypercallRetireResult Issue(
        DomainHypercallCompletionOwner completionOwner,
        DomainHypercallCompletionPublicationResult publication,
        VirtualizationRestoreGenerationOwner restoreOwner,
        in DomainHypercallRetireEligibility eligibility)
    {
        ArgumentNullException.ThrowIfNull(completionOwner);
        ArgumentNullException.ThrowIfNull(restoreOwner);
        if (!publication.IsPublished || publication.E5 is null || publication.Completion.IsEmpty)
            return Deny(DomainHypercallRetireDecision.MissingCompletion, "E6 requires one atomic live CompletionRecord/E5 publication.");
        if (!eligibility.IsCanonicalHead || eligibility.RetireOrderIndex != 0 ||
            eligibility.IsSquashed || eligibility.HasWinningException)
            return Deny(DomainHypercallRetireDecision.NotCanonicalHead, "E6 requires the unsquashed exception-free canonical retire head.");
        if (eligibility.RetireWindowIdentity == 0 || eligibility.OrderEpoch == 0 ||
            eligibility.OperationAttempt == 0 ||
            eligibility.PhysicalLaneId < 0 || eligibility.PhysicalLaneId > 7)
            return Deny(DomainHypercallRetireDecision.InvalidWindow, "E6 retire-window/order identity is incomplete.");

        VmxMicroOp carrier = eligibility.Carrier;
        SafetyVerifier.VirtualizationAdmissionCertificate? e1 = carrier?.VirtualizationAdmission;
        DomainHypercallCompletionOwner.CompletionPublicationToken e5 = publication.E5;
        if (carrier is null || !ReferenceEquals(carrier.ExactHypercallCompletionPublication?.E5, e5) ||
            e1 is null || e1.Operation != VmxOperationKind.VmCall)
            return Deny(DomainHypercallRetireDecision.InvalidCarrier, "E6 requires the original exact VMCALL carrier and its E1/E5 chain.");
        if (e5.RestoreGeneration != restoreOwner.CurrentGeneration)
            return Deny(DomainHypercallRetireDecision.StaleRestoreGeneration, "E5 restore generation is stale at retire.");
        if (e5.AttemptId != e1.AttemptId || e5.VirtualThreadId != eligibility.VirtualThreadId ||
            e5.DomainTag != eligibility.DomainTag || e1.VirtualThreadId != eligibility.VirtualThreadId ||
            e1.DomainTag != eligibility.DomainTag || e1.SourceSlotId != eligibility.SourceSlotId ||
            e1.WorkingSlotId != eligibility.WorkingSlotId || e1.BundleIdentity != eligibility.WorkingBundleSequence)
            return Deny(DomainHypercallRetireDecision.IdentityMismatch, "E1/E5 and canonical WB attempt identity do not match.");
        if (!completionOwner.ValidateLive(e5, publication.Completion, restoreOwner))
            return Deny(DomainHypercallRetireDecision.DuplicateOrForeignE5, "E5 is duplicate, stale, or no longer live.");

        if (!completionOwner.TryBeginE5ToE6Transition(
                e5,
                out DomainHypercallLifecycleGate.TransitionLease? transition) ||
            transition is null)
            return Deny(DomainHypercallRetireDecision.LifecycleGateDenied, "E5-to-E6 handoff denied by the disabled or draining lifecycle gate.");

        using (transition)
        {
            lock (_sync)
            {
                if (!completionOwner.ConsumeForRetire(e5, publication.Completion, restoreOwner, _consumerSeal))
                    return Deny(DomainHypercallRetireDecision.DuplicateOrForeignE5, "E5 is duplicate, stale, or bound to another retire owner.");

#if TESTING
                transition.LifecycleGate.NotifyTransitionGapForTesting(DomainHypercallTransitionKind.E5ToE6);
#endif
                ulong sequence = AllocateSequence();
                string digest = ComputeDigest(sequence, e5, eligibility);
                VirtualizationRetireGrant grant =
                    VirtualizationRetireGrant.Create(_issuerSeal, sequence, e5, eligibility, digest);
                _live.Add(grant, new LiveGrant(digest));
                _liveGrants.Add(grant);
                return new(DomainHypercallRetireDecision.Issued, grant, "Canonical CPU retire owner issued one opaque no-state E6.");
            }
        }
    }

    internal bool ConsumeAtPreciseRetire(
        VirtualizationRetireGrant? grant,
        VirtualizationRestoreGenerationOwner restoreOwner,
        ulong retireWindowIdentity,
        ulong orderEpoch)
    {
        ArgumentNullException.ThrowIfNull(restoreOwner);
        lock (_sync)
        {
            if (grant is null || !grant.WasIssuedBy(_issuerSeal) ||
                grant.RestoreGeneration != restoreOwner.CurrentGeneration ||
                grant.RetireWindowIdentity != retireWindowIdentity || grant.OrderEpoch != orderEpoch ||
                !_live.TryGetValue(grant, out LiveGrant? live) ||
                !string.Equals(live.Digest, grant.GrantDigest, StringComparison.Ordinal))
                return false;
            _liveGrants.Remove(grant);
            return _live.Remove(grant);
        }
    }

    internal bool ValidateLive(
        VirtualizationRetireGrant? grant,
        VirtualizationRestoreGenerationOwner? restoreOwner,
        ulong retireWindowIdentity,
        ulong orderEpoch)
    {
        lock (_sync)
        {
            return grant is not null && restoreOwner is not null &&
                grant.WasIssuedBy(_issuerSeal) &&
                grant.RestoreGeneration == restoreOwner.CurrentGeneration &&
                grant.RetireWindowIdentity == retireWindowIdentity && grant.OrderEpoch == orderEpoch &&
                _live.TryGetValue(grant, out LiveGrant? live) &&
                string.Equals(live.Digest, grant.GrantDigest, StringComparison.Ordinal);
        }
    }

    internal int CountLiveGrants(ulong domainTag)
    {
        lock (_sync)
            return _liveGrants.Count(grant => grant.DomainTag == domainTag);
    }

    internal int CancelLiveGrantsForDrain(ulong domainTag)
    {
        lock (_sync)
        {
            VirtualizationRetireGrant[] cancelled =
                _liveGrants.Where(grant => grant.DomainTag == domainTag).ToArray();
            foreach (VirtualizationRetireGrant grant in cancelled)
            {
                _liveGrants.Remove(grant);
                _live.Remove(grant);
            }
            return cancelled.Length;
        }
    }

    private ulong AllocateSequence()
    {
        ulong value = _nextSequence++;
        return value == 0 ? _nextSequence++ : value;
    }

    private static string ComputeDigest(
        ulong sequence,
        DomainHypercallCompletionOwner.CompletionPublicationToken e5,
        in DomainHypercallRetireEligibility eligibility)
    {
        using var stream = new MemoryStream();
        stream.Write(Envelope);
        WriteU64(stream, sequence); WriteU64(stream, e5.AttemptId);
        WriteU32(stream, checked((uint)eligibility.VirtualThreadId)); WriteU64(stream, eligibility.DomainTag);
        WriteU32(stream, checked((uint)eligibility.SourceSlotId)); WriteU32(stream, checked((uint)eligibility.WorkingSlotId));
        WriteU64(stream, eligibility.WorkingBundleSequence); WriteU64(stream, eligibility.OperationAttempt);
        WriteU32(stream, checked((uint)eligibility.PhysicalLaneId));
        WriteU64(stream, eligibility.RetireWindowIdentity); WriteU64(stream, eligibility.OrderEpoch);
        WriteU64(stream, e5.RestoreGeneration);
        byte[] e5Digest = Convert.FromHexString(e5.TokenDigest); WriteU32(stream, checked((uint)e5Digest.Length)); stream.Write(e5Digest);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteU32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(bytes, value); stream.Write(bytes);
    }

    private static void WriteU64(Stream stream, ulong value)
    {
        Span<byte> bytes = stackalloc byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(bytes, value); stream.Write(bytes);
    }

    private static DomainHypercallRetireResult Deny(DomainHypercallRetireDecision decision, string reason) => new(decision, null, reason);
}
