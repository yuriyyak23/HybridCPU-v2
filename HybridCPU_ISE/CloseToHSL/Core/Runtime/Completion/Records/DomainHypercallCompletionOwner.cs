using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace YAKSys_Hybrid_CPU.Core;

internal enum DomainHypercallCompletionDecision : byte
{
    Published = 0,
    MissingExecution = 1,
    RuntimeAdmissionDenied = 2,
    RouteDenied = 3,
    FenceDenied = 4,
    InvalidExecution = 5,
    DuplicatePublication = 6,
    LifecycleGateDenied = 7,
}

internal readonly record struct DomainHypercallCompletionPublicationResult(
    DomainHypercallCompletionDecision Decision,
    CompletionRecord Completion,
    DomainHypercallCompletionOwner.CompletionPublicationToken? E5,
    string Reason,
    DomainHypercallCompletionOwner? Owner = null,
    VirtualizationRestoreGenerationOwner? RestoreOwner = null)
{
    internal bool IsPublished =>
        Decision == DomainHypercallCompletionDecision.Published &&
        !Completion.IsEmpty && E5 is not null;
    internal bool RetirePublicationAuthorized => false;
}

internal sealed class DomainHypercallCompletionOwner
{
    private sealed record LivePublication(
        CompletionRecord Completion,
        DomainHypercallLifecycleGate LifecycleGate);

    internal sealed class CompletionPublicationToken
    {
        private readonly object _issuerSeal;

        private CompletionPublicationToken(
            object issuerSeal,
            ulong attemptId,
            int virtualThreadId,
            ulong domainTag,
            string decisionId,
            ulong ownerId,
            ulong executionSequence,
            ulong completionSequence,
            string e3Digest,
            string effectDigest,
            string completionDigest,
            ulong restoreGeneration,
            string tokenDigest)
        {
            _issuerSeal = issuerSeal;
            AttemptId = attemptId;
            VirtualThreadId = virtualThreadId;
            DomainTag = domainTag;
            DecisionId = decisionId;
            OwnerId = ownerId;
            ExecutionSequence = executionSequence;
            CompletionSequence = completionSequence;
            E3Digest = e3Digest;
            EffectDigest = effectDigest;
            CompletionDigest = completionDigest;
            RestoreGeneration = restoreGeneration;
            TokenDigest = tokenDigest;
        }

        internal uint SchemaVersion => 1;
        internal ulong AttemptId { get; }
        internal int VirtualThreadId { get; }
        internal ulong DomainTag { get; }
        internal string DecisionId { get; }
        internal ulong OwnerId { get; }
        internal ulong ExecutionSequence { get; }
        internal ulong CompletionSequence { get; }
        internal string E3Digest { get; }
        internal string EffectDigest { get; }
        internal string CompletionDigest { get; }
        internal EvidenceVisibilityClass EvidenceClass => EvidenceVisibilityClass.HostOwnedRuntimeEvidence;
        internal TrapCompletionMigrationClass MigrationClass => TrapCompletionMigrationClass.HostOwnedNonMigratable;
        internal ulong RestoreGeneration { get; }
        internal string TokenDigest { get; }
        internal bool RetirePublicationAuthorized => false;
        internal bool WasIssuedBy(object issuerSeal) => ReferenceEquals(_issuerSeal, issuerSeal);

        internal static CompletionPublicationToken Create(
            object issuerSeal,
            ulong sequence,
            DomainHypercallRuntimeExecutor.ExecutionReceipt receipt,
            CompletionRecord completion)
        {
            string completionDigest = DomainHypercallCompletionDigest.ComputeRecord(completion);
            string tokenDigest = DomainHypercallCompletionDigest.ComputeToken(
                receipt.AttemptId, receipt.VirtualThreadId, receipt.DomainTag,
                receipt.DecisionId, receipt.OwnerId, receipt.ExecutionSequence, sequence,
                receipt.ReceiptDigest, receipt.EffectDigest, completionDigest,
                receipt.RestoreGeneration);
            return new(
                issuerSeal, receipt.AttemptId, receipt.VirtualThreadId, receipt.DomainTag,
                receipt.DecisionId, receipt.OwnerId, receipt.ExecutionSequence, sequence,
                receipt.ReceiptDigest, receipt.EffectDigest, completionDigest,
                receipt.RestoreGeneration, tokenDigest);
        }
    }

    private readonly object _consumerSeal = new();
    private readonly object _issuerSeal = new();
    private readonly object _sync = new();
    private readonly ConditionalWeakTable<CompletionPublicationToken, LivePublication> _live = new();
    private readonly HashSet<CompletionPublicationToken> _liveTokens = new();
    private ulong _nextCompletionSequence = 1;
    private object? _retireConsumerSeal;

    internal void BindRetireOwner(DomainHypercallRetireOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        lock (_sync)
        {
            object seal = owner.ConsumerSeal;
            if (_retireConsumerSeal is not null && !ReferenceEquals(_retireConsumerSeal, seal))
                throw new InvalidOperationException("Completion owner is already bound to another canonical retire owner.");
            _retireConsumerSeal = seal;
        }
    }

    internal DomainHypercallCompletionPublicationResult PublishExactProbe(
        DomainHypercallRuntimeExecutor executor,
        DomainHypercallRuntimeExecutor.ExecutionReceipt? receipt,
        VirtualizationRestoreGenerationOwner restoreOwner,
        DomainRuntimeContext context,
        RootAuthorityDescriptor root,
        DomainHypercallLifecycleGate lifecycleGate)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(restoreOwner);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(root);
        if (receipt is null)
            return Deny(DomainHypercallCompletionDecision.MissingExecution, "E5 requires a live E3 receipt.");
        if (!lifecycleGate.TryBeginTransition(
                receipt.DomainTag,
                DomainHypercallTransitionKind.E3ToE5,
                out DomainHypercallLifecycleGate.TransitionLease? transition) ||
            transition is null)
            return Deny(DomainHypercallCompletionDecision.LifecycleGateDenied, "E3-to-E5 handoff denied by the disabled or draining lifecycle gate.");

        using (transition)
        {
            lock (_sync)
            {
                var admissionService = new RuntimeBoundaryAdmissionService();
                RuntimeBoundaryAdmissionResult admission = admissionService.Validate(new(
                context,
                root,
                EvidencePolicy: null,
                new DomainRuntimeOperation(
                    DomainRuntimeOperationKind.InvokeHypercall,
                    DomainRuntimeOperationSource.RuntimeService,
                    requiresCapabilityGrant: true,
                    DomainRuntimeOperationAuthorityClass.NoStateExecution),
                DomainBoundaryDescriptor.ExecutionOnly,
                CapabilityBoundaryRequirement.TypedGrant(
                    RuntimeCapabilityIds.VmCallProbeNoStateV1Mask,
                    CapabilityGrantScope.DomainGranted),
                EvidenceBoundaryRequirement.None));
                if (!admission.IsAllowed)
                    return Deny(DomainHypercallCompletionDecision.RuntimeAdmissionDenied, admission.Message);
                if (receipt.VirtualThreadId < byte.MinValue || receipt.VirtualThreadId > byte.MaxValue ||
                    receipt.DomainTag == 0 || receipt.DomainTag > ushort.MaxValue)
                    return Deny(DomainHypercallCompletionDecision.InvalidExecution, "E3 VT/domain identity is outside the neutral trap transport width.");

            NeutralTrapResult neutral = NeutralTrapResult.Trap(
                TrapRequest.ForInstruction(
                    YAKSys_Hybrid_CPU.Processor.CPU_Core.IsaOpcodeValues.VMCALL,
                    checked((byte)receipt.VirtualThreadId),
                    checked((ushort)receipt.DomainTag)),
                NeutralTrapResultKind.InstructionIntercept);
            var request = new TrapCompletionRouteRequest(
                neutral,
                admission,
                TrapCompletionRouteDescriptor.RuntimeOwnedCompletionPublication,
                DomainValidated: context.IsBoundToDomain(receipt.DomainTag),
                BackendExecutionAuthorized: true,
                CompletionEvidenceClass: EvidenceVisibilityClass.HostOwnedRuntimeEvidence,
                CompletionMigrationClass: TrapCompletionMigrationClass.HostOwnedNonMigratable);
            TrapCompletionRouteResult route = TrapCompletionRouteService.Default.Authorize(request);
            if (!route.CompletionPublicationAuthorizedOnly)
                return Deny(DomainHypercallCompletionDecision.RouteDenied, route.Reason);

            TrapCompletionOwnerPolicyResult policy =
                TrapCompletionPublicationFence.Default.EvaluateOwnerPolicy(request, route);
            if (!policy.IsAllowed)
                return Deny(DomainHypercallCompletionDecision.FenceDenied, policy.Reason);

                DomainHypercallReceiptValidationResult consumed =
                    executor.ConsumeReceiptForCompletion(receipt, restoreOwner, lifecycleGate, _consumerSeal);
                if (!consumed.IsValid)
                    return Deny(DomainHypercallCompletionDecision.InvalidExecution, consumed.Reason);

#if TESTING
                lifecycleGate.NotifyTransitionGapForTesting(DomainHypercallTransitionKind.E3ToE5);
#endif

            var completion = new CompletionRecord(
                CompletionRecordClass.Event,
                route.NeutralReasonCode,
                qualification: 0,
                faultAddress: 0,
                faultAux: 0);
            ulong sequence = AllocateSequence();
            CompletionPublicationToken token =
                CompletionPublicationToken.Create(_issuerSeal, sequence, receipt, completion);
                _live.Add(token, new LivePublication(completion, lifecycleGate));
                _liveTokens.Add(token);
                return new(
                    DomainHypercallCompletionDecision.Published,
                    completion,
                    token,
                    "Neutral completion owner atomically published one record and opaque E5; retire remains denied.",
                    this,
                    restoreOwner);
            }
        }
    }

    internal bool ValidateLive(
        CompletionPublicationToken? token,
        CompletionRecord? completion,
        VirtualizationRestoreGenerationOwner? restoreOwner)
    {
        if (token is null || completion is null || restoreOwner is null ||
            !token.WasIssuedBy(_issuerSeal) ||
            !_live.TryGetValue(token, out LivePublication? published) ||
            !ReferenceEquals(published.Completion, completion) ||
            token.RestoreGeneration != restoreOwner.CurrentGeneration)
            return false;
        return string.Equals(token.CompletionDigest, DomainHypercallCompletionDigest.ComputeRecord(completion), StringComparison.Ordinal) &&
            string.Equals(token.TokenDigest, DomainHypercallCompletionDigest.ComputeToken(
                token.AttemptId, token.VirtualThreadId, token.DomainTag, token.DecisionId,
                token.OwnerId, token.ExecutionSequence, token.CompletionSequence,
                token.E3Digest, token.EffectDigest, token.CompletionDigest,
                token.RestoreGeneration), StringComparison.Ordinal);
    }

    internal bool ConsumeForRetire(
        CompletionPublicationToken? token,
        CompletionRecord? completion,
        VirtualizationRestoreGenerationOwner? restoreOwner,
        object retireConsumerSeal)
    {
        lock (_sync)
        {
            if (_retireConsumerSeal is null ||
                !ReferenceEquals(_retireConsumerSeal, retireConsumerSeal) ||
                !ValidateLive(token, completion, restoreOwner) ||
                token is null)
                return false;

            _liveTokens.Remove(token);
            return _live.Remove(token);
        }
    }

    internal bool TryBeginE5ToE6Transition(
        CompletionPublicationToken? token,
        out DomainHypercallLifecycleGate.TransitionLease? transition)
    {
        lock (_sync)
        {
            if (token is null || !token.WasIssuedBy(_issuerSeal) ||
                !_live.TryGetValue(token, out LivePublication? publication))
            {
                transition = null;
                return false;
            }

            return publication.LifecycleGate.TryBeginTransition(
                token.DomainTag,
                DomainHypercallTransitionKind.E5ToE6,
                out transition);
        }
    }

    internal int CountLiveTokens(ulong domainTag)
    {
        lock (_sync)
            return _liveTokens.Count(token => token.DomainTag == domainTag);
    }

    internal int CancelLiveTokensForDrain(ulong domainTag)
    {
        lock (_sync)
        {
            CompletionPublicationToken[] cancelled =
                _liveTokens.Where(token => token.DomainTag == domainTag).ToArray();
            foreach (CompletionPublicationToken token in cancelled)
            {
                _liveTokens.Remove(token);
                _live.Remove(token);
            }
            return cancelled.Length;
        }
    }

    internal object ConsumerSeal => _consumerSeal;

    private ulong AllocateSequence()
    {
        ulong value = _nextCompletionSequence++;
        return value == 0 ? _nextCompletionSequence++ : value;
    }

    private static DomainHypercallCompletionPublicationResult Deny(
        DomainHypercallCompletionDecision decision,
        string reason) => new(decision, CompletionRecord.None, null, reason);
}

internal static class DomainHypercallCompletionDigest
{
    private static readonly byte[] TokenEnvelope = Encoding.ASCII.GetBytes("HCPUE5V1\0");
    private static readonly byte[] RecordEnvelope = Encoding.ASCII.GetBytes("HCPUCV1\0");

    internal static string ComputeRecord(CompletionRecord record)
    {
        using var stream = new MemoryStream();
        stream.Write(RecordEnvelope);
        stream.WriteByte((byte)record.RecordClass);
        WriteU32(stream, record.ReasonCode);
        WriteU64(stream, record.Qualification);
        WriteU64(stream, record.FaultAddress);
        WriteU64(stream, record.FaultAux);
        return Hash(stream);
    }

    internal static string ComputeToken(
        ulong attemptId, int virtualThreadId, ulong domainTag, string decisionId,
        ulong ownerId, ulong executionSequence, ulong completionSequence,
        string e3Digest, string effectDigest, string completionDigest,
        ulong restoreGeneration)
    {
        using var stream = new MemoryStream();
        stream.Write(TokenEnvelope);
        WriteU64(stream, attemptId);
        WriteU32(stream, checked((uint)virtualThreadId));
        WriteU64(stream, domainTag);
        WriteText(stream, decisionId);
        WriteU64(stream, ownerId);
        WriteU64(stream, executionSequence);
        WriteU64(stream, completionSequence);
        stream.Write(Convert.FromHexString(e3Digest));
        stream.Write(Convert.FromHexString(effectDigest));
        stream.Write(Convert.FromHexString(completionDigest));
        stream.WriteByte((byte)EvidenceVisibilityClass.HostOwnedRuntimeEvidence);
        stream.WriteByte((byte)TrapCompletionMigrationClass.HostOwnedNonMigratable);
        WriteU64(stream, restoreGeneration);
        return Hash(stream);
    }

    private static string Hash(MemoryStream stream) =>
        Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();

    private static void WriteText(Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        WriteU32(stream, checked((uint)bytes.Length));
        stream.Write(bytes);
    }

    private static void WriteU32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteU64(Stream stream, ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        stream.Write(bytes);
    }
}
