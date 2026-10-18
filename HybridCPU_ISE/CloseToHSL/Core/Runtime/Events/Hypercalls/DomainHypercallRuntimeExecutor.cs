using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace YAKSys_Hybrid_CPU.Core;

internal enum ExactProbeExecutionMode : byte
{
    Disabled = 0,
    ExactProbeOnly = 1,
}

internal enum DomainHypercallExecutionDecision : byte
{
    Executed = 0,
    Disabled = 1,
    MissingAdmission = 2,
    InvalidAdmission = 3,
    OperationBindingMismatch = 4,
    LifecycleGateDenied = 5,
}

internal readonly record struct DomainHypercallExecutionResult(
    DomainHypercallExecutionDecision Decision,
    DomainHypercallRuntimeExecutor.ExecutionReceipt? Receipt,
    string Reason)
{
    internal bool IsExecuted =>
        Decision == DomainHypercallExecutionDecision.Executed && Receipt is not null;
    internal bool CompletionPublicationAuthorized => false;
    internal bool RetirePublicationAuthorized => false;
}

internal enum DomainHypercallReceiptValidationDecision : byte
{
    Valid = 0,
    Missing = 1,
    ForeignIssuer = 2,
    NotLive = 3,
    RestoreGenerationMismatch = 4,
    DigestMismatch = 5,
}

internal readonly record struct DomainHypercallReceiptValidationResult(
    DomainHypercallReceiptValidationDecision Decision,
    string Reason)
{
    internal bool IsValid => Decision == DomainHypercallReceiptValidationDecision.Valid;
}

/// <summary>
/// Exact neutral owner executor for PROBE_NO_STATE_V1. It is not connected to
/// decode, VMX compatibility, completion or retire in PR-E and defaults disabled.
/// </summary>
internal sealed class DomainHypercallRuntimeExecutor
{
    internal sealed class ExecutionReceipt
    {
        private readonly object _issuerSeal;

        private ExecutionReceipt(
            object issuerSeal,
            ulong executionSequence,
            string e2Digest,
            ulong attemptId,
            int virtualThreadId,
            ulong domainTag,
            string decisionId,
            ulong ownerId,
            uint ownerPolicyVersion,
            uint ownerEpoch,
            string operationNamespace,
            string operationId,
            ushort numericLeaf,
            ulong restoreGeneration,
            VirtualizationDecisionEffectClassV2 effectClass,
            VirtualizationDecisionResultAbiV2 resultAbi,
            string effectDigest,
            string resultDigest,
            string receiptDigest)
        {
            _issuerSeal = issuerSeal;
            ExecutionSequence = executionSequence;
            E2Digest = e2Digest;
            AttemptId = attemptId;
            VirtualThreadId = virtualThreadId;
            DomainTag = domainTag;
            DecisionId = decisionId;
            OwnerId = ownerId;
            OwnerPolicyVersion = ownerPolicyVersion;
            OwnerEpoch = ownerEpoch;
            OperationNamespace = operationNamespace;
            OperationId = operationId;
            NumericLeaf = numericLeaf;
            RestoreGeneration = restoreGeneration;
            EffectClass = effectClass;
            ResultAbi = resultAbi;
            EffectDigest = effectDigest;
            ResultDigest = resultDigest;
            ReceiptDigest = receiptDigest;
        }

        internal uint SchemaVersion => 2;
        internal ulong ExecutionSequence { get; }
        internal string E2Digest { get; }
        internal ulong AttemptId { get; }
        internal int VirtualThreadId { get; }
        internal ulong DomainTag { get; }
        internal string DecisionId { get; }
        internal ulong OwnerId { get; }
        internal uint OwnerPolicyVersion { get; }
        internal uint OwnerEpoch { get; }
        internal string OperationNamespace { get; }
        internal string OperationId { get; }
        internal ushort NumericLeaf { get; }
        internal ulong RestoreGeneration { get; }
        internal VirtualizationDecisionEffectClassV2 EffectClass { get; }
        internal VirtualizationDecisionResultAbiV2 ResultAbi { get; }
        internal string EffectDigest { get; }
        internal string ResultDigest { get; }
        internal string ReceiptDigest { get; }
        internal bool HasPayload => false;
        internal bool HasStateEffect => false;
        internal bool CompletionPublicationAuthorized => false;
        internal bool RetirePublicationAuthorized => false;

        internal bool WasIssuedBy(object issuerSeal) => ReferenceEquals(_issuerSeal, issuerSeal);

        internal static ExecutionReceipt Create(
            object issuerSeal,
            ulong executionSequence,
            SafetyVerifier.VirtualizationOperationAdmissionCertificate e2)
        {
            string digest = DomainHypercallExecutionReceiptDigest.Compute(
                executionSequence,
                e2.CertificateDigest,
                e2.AttemptId,
                e2.VirtualThreadId,
                e2.DomainTag,
                e2.DecisionId,
                e2.OwnerId,
                e2.OwnerPolicyVersion,
                e2.OwnerEpoch,
                e2.OperationNamespace,
                e2.OperationId,
                e2.NumericLeaf,
                e2.RestoreGeneration,
                VirtualizationDecisionEffectClassV2.NoStateNoPayload,
                VirtualizationDecisionResultAbiV2.NoPayload,
                DomainHypercallExecutionReceiptDigest.NoEffectDigest,
                DomainHypercallExecutionReceiptDigest.NoResultDigest);
            return new(
                issuerSeal,
                executionSequence,
                e2.CertificateDigest,
                e2.AttemptId,
                e2.VirtualThreadId,
                e2.DomainTag,
                e2.DecisionId,
                e2.OwnerId,
                e2.OwnerPolicyVersion,
                e2.OwnerEpoch,
                e2.OperationNamespace,
                e2.OperationId,
                e2.NumericLeaf,
                e2.RestoreGeneration,
                VirtualizationDecisionEffectClassV2.NoStateNoPayload,
                VirtualizationDecisionResultAbiV2.NoPayload,
                DomainHypercallExecutionReceiptDigest.NoEffectDigest,
                DomainHypercallExecutionReceiptDigest.NoResultDigest,
                digest);
        }
    }

    private sealed class LiveReceipt
    {
        internal LiveReceipt(
            SafetyVerifier.VirtualizationOperationAdmissionCertificate e2,
            VirtualizationRestoreGenerationOwner restoreOwner,
            DomainHypercallLifecycleGate lifecycleGate)
        {
            E2 = e2;
            RestoreOwner = restoreOwner;
            LifecycleGate = lifecycleGate;
        }

        internal SafetyVerifier.VirtualizationOperationAdmissionCertificate E2 { get; }
        internal VirtualizationRestoreGenerationOwner RestoreOwner { get; }
        internal DomainHypercallLifecycleGate LifecycleGate { get; }
        internal bool ConsumedByCompletionOwner { get; set; }
    }

    private static readonly object ExactConsumerSeal = new();
    private readonly object _issuerSeal = new();
    private readonly ConditionalWeakTable<ExecutionReceipt, LiveReceipt> _liveReceipts = new();
    private readonly HashSet<ExecutionReceipt> _unconsumedReceipts = new();
    private readonly ExactProbeExecutionMode _mode;
    private readonly object _executionSync = new();
    private object? _completionConsumerSeal;
    private ulong _nextExecutionSequence = 1;

    internal DomainHypercallRuntimeExecutor(
        ExactProbeExecutionMode mode = ExactProbeExecutionMode.Disabled)
    {
        _mode = mode;
    }

    internal ExactProbeExecutionMode Mode => _mode;

    internal DomainHypercallExecutionResult ExecuteExactProbe(
        SafetyVerifier verifier,
        SafetyVerifier.VirtualizationOperationAdmissionCertificate? e2,
        VirtualizationRestoreGenerationOwner? restoreOwner,
        DomainHypercallLifecycleGate? lifecycleGate)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        if (_mode != ExactProbeExecutionMode.ExactProbeOnly)
            return Deny(DomainHypercallExecutionDecision.Disabled, "Exact probe executor kill switch is disabled.");
        if (e2 is null || restoreOwner is null || lifecycleGate is null)
            return Deny(DomainHypercallExecutionDecision.MissingAdmission, "Execution requires live E2, restore owner, and lifecycle gate.");
        if (!IsExactBinding(e2))
            return Deny(DomainHypercallExecutionDecision.OperationBindingMismatch, "E2 does not bind the exact accepted probe operation.");
        if (!lifecycleGate.TryBeginTransition(
                e2.DomainTag,
                DomainHypercallTransitionKind.E2ToE3,
                out DomainHypercallLifecycleGate.TransitionLease? transition) ||
            transition is null)
            return Deny(DomainHypercallExecutionDecision.LifecycleGateDenied, "E2-to-E3 handoff denied by the disabled or draining lifecycle gate.");

        using (transition)
        {
            lock (_executionSync)
            {
                VirtualizationE2Result validation = verifier.ValidateVirtualizationE2(e2, restoreOwner);
                if (!validation.IsLive)
                    return Deny(DomainHypercallExecutionDecision.InvalidAdmission, validation.Reason);

                ulong sequence = AllocateExecutionSequence();
                ExecutionReceipt receipt = ExecutionReceipt.Create(_issuerSeal, sequence, e2);
                VirtualizationE2ConsumptionResult consumption =
                    verifier.ConsumeVirtualizationE2FromExactExecutor(e2, restoreOwner, lifecycleGate, ExactConsumerSeal);
                if (!consumption.IsConsumed)
                    return Deny(DomainHypercallExecutionDecision.InvalidAdmission, consumption.Reason);

#if TESTING
                lifecycleGate.NotifyTransitionGapForTesting(DomainHypercallTransitionKind.E2ToE3);
#endif
                _liveReceipts.Add(receipt, new LiveReceipt(e2, restoreOwner, lifecycleGate));
                _unconsumedReceipts.Add(receipt);
                return new(
                    DomainHypercallExecutionDecision.Executed,
                    receipt,
                    "Exact no-state/no-payload probe executed once; receipt grants no publication authority.");
            }
        }
    }

    internal DomainHypercallReceiptValidationResult ValidateReceipt(
        ExecutionReceipt? receipt,
        VirtualizationRestoreGenerationOwner? restoreOwner)
    {
        if (receipt is null || restoreOwner is null)
            return Invalid(DomainHypercallReceiptValidationDecision.Missing, "E3 receipt or restore owner is missing.");
        if (!receipt.WasIssuedBy(_issuerSeal))
            return Invalid(DomainHypercallReceiptValidationDecision.ForeignIssuer, "E3 issuer mismatch.");
        if (!_liveReceipts.TryGetValue(receipt, out LiveReceipt? live) ||
            !ReferenceEquals(live.RestoreOwner, restoreOwner) ||
            !string.Equals(live.E2.CertificateDigest, receipt.E2Digest, StringComparison.Ordinal) ||
            live.ConsumedByCompletionOwner)
            return Invalid(DomainHypercallReceiptValidationDecision.NotLive, "E3 is not live in this executor registry.");
        if (restoreOwner.CurrentGeneration == 0 ||
            receipt.RestoreGeneration != restoreOwner.CurrentGeneration)
            return Invalid(DomainHypercallReceiptValidationDecision.RestoreGenerationMismatch, "E3 was invalidated by restore generation change.");

        string digest = DomainHypercallExecutionReceiptDigest.Compute(
            receipt.ExecutionSequence,
            receipt.E2Digest,
            receipt.AttemptId,
            receipt.VirtualThreadId,
            receipt.DomainTag,
            receipt.DecisionId,
            receipt.OwnerId,
            receipt.OwnerPolicyVersion,
            receipt.OwnerEpoch,
            receipt.OperationNamespace,
            receipt.OperationId,
            receipt.NumericLeaf,
            receipt.RestoreGeneration,
            receipt.EffectClass,
            receipt.ResultAbi,
            receipt.EffectDigest,
            receipt.ResultDigest);
        if (!string.Equals(receipt.ReceiptDigest, digest, StringComparison.Ordinal) ||
            !string.Equals(receipt.EffectDigest, DomainHypercallExecutionReceiptDigest.NoEffectDigest, StringComparison.Ordinal) ||
            !string.Equals(receipt.ResultDigest, DomainHypercallExecutionReceiptDigest.NoResultDigest, StringComparison.Ordinal))
            return Invalid(DomainHypercallReceiptValidationDecision.DigestMismatch, "E3 canonical digest mismatch.");

        return new(DomainHypercallReceiptValidationDecision.Valid, "E3 is a live exact execution receipt only.");
    }

    internal DomainHypercallReceiptValidationResult ConsumeReceiptForCompletion(
        ExecutionReceipt? receipt,
        VirtualizationRestoreGenerationOwner? restoreOwner,
        DomainHypercallLifecycleGate lifecycleGate,
        object consumerSeal)
    {
        if (!ReferenceEquals(consumerSeal, _completionConsumerSeal))
            return Invalid(DomainHypercallReceiptValidationDecision.ForeignIssuer, "Only the neutral completion owner may consume E3.");

        lock (_executionSync)
        {
            DomainHypercallReceiptValidationResult validation = ValidateReceipt(receipt, restoreOwner);
            if (!validation.IsValid || receipt is null)
                return validation;
            LiveReceipt live = _liveReceipts.GetValue(receipt, _ => throw new InvalidOperationException());
            if (live.ConsumedByCompletionOwner || !ReferenceEquals(live.LifecycleGate, lifecycleGate))
                return Invalid(DomainHypercallReceiptValidationDecision.NotLive, "E3 was already consumed by the completion owner.");
            live.ConsumedByCompletionOwner = true;
            _unconsumedReceipts.Remove(receipt);
            return new(DomainHypercallReceiptValidationDecision.Valid, "Neutral completion owner consumed E3 exactly once.");
        }
    }

    internal void BindCompletionOwner(DomainHypercallCompletionOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        lock (_executionSync)
        {
            if (_completionConsumerSeal is not null)
                throw new InvalidOperationException("Exact executor already has a neutral completion owner binding.");
            _completionConsumerSeal = owner.ConsumerSeal;
        }
    }

    internal int CountLiveReceipts(ulong domainTag)
    {
        lock (_executionSync)
            return _unconsumedReceipts.Count(receipt => receipt.DomainTag == domainTag);
    }

    internal int CancelLiveReceiptsForDrain(ulong domainTag)
    {
        lock (_executionSync)
        {
            ExecutionReceipt[] cancelled =
                _unconsumedReceipts.Where(receipt => receipt.DomainTag == domainTag).ToArray();
            foreach (ExecutionReceipt receipt in cancelled)
            {
                if (_liveReceipts.TryGetValue(receipt, out LiveReceipt? live))
                    live.ConsumedByCompletionOwner = true;
                _unconsumedReceipts.Remove(receipt);
            }
            return cancelled.Length;
        }
    }

    internal static bool IsExactConsumerSeal(object consumerSeal) =>
        ReferenceEquals(consumerSeal, ExactConsumerSeal);

    private static bool IsExactBinding(
        SafetyVerifier.VirtualizationOperationAdmissionCertificate e2) =>
        e2.AttemptId != 0 &&
        e2.RestoreGeneration != 0 &&
        e2.OwnerId == VirtualizationDecisionValidatorV2.ExpectedOwnerId &&
        e2.OwnerPolicyVersion == 1 &&
        e2.OwnerEpoch == 1 &&
        string.Equals(e2.DecisionId, VirtualizationDecisionValidatorV2.ExpectedDecisionId, StringComparison.Ordinal) &&
        string.Equals(e2.OperationNamespace, VirtualizationDecisionValidatorV2.ExpectedOperationNamespace, StringComparison.Ordinal) &&
        string.Equals(e2.OperationId, VirtualizationDecisionValidatorV2.ExpectedOperationId, StringComparison.Ordinal) &&
        e2.NumericLeaf == 1;

    private ulong AllocateExecutionSequence()
    {
        ulong sequence = _nextExecutionSequence++;
        if (sequence == 0)
            sequence = _nextExecutionSequence++;
        return sequence;
    }

    private static DomainHypercallExecutionResult Deny(
        DomainHypercallExecutionDecision decision,
        string reason) => new(decision, null, reason);

    private static DomainHypercallReceiptValidationResult Invalid(
        DomainHypercallReceiptValidationDecision decision,
        string reason) => new(decision, reason);
}

internal static class DomainHypercallExecutionReceiptDigest
{
    private static readonly byte[] Envelope = Encoding.ASCII.GetBytes("HCPUE3V2\0");
    internal static string NoEffectDigest { get; } =
        HashConstant("HybridCPU.PROBE_NO_STATE_V1.NO_EFFECT.v1");
    internal static string NoResultDigest { get; } =
        HashConstant("HybridCPU.PROBE_NO_STATE_V1.NO_RESULT.v1");

    internal static string Compute(
        ulong sequence,
        string e2Digest,
        ulong attemptId,
        int virtualThreadId,
        ulong domainTag,
        string decisionId,
        ulong ownerId,
        uint ownerPolicyVersion,
        uint ownerEpoch,
        string operationNamespace,
        string operationId,
        ushort numericLeaf,
        ulong restoreGeneration,
        VirtualizationDecisionEffectClassV2 effectClass,
        VirtualizationDecisionResultAbiV2 resultAbi,
        string effectDigest,
        string resultDigest)
    {
        using var stream = new MemoryStream();
        stream.Write(Envelope);
        WriteU64(stream, sequence);
        stream.Write(Convert.FromHexString(e2Digest));
        WriteU64(stream, attemptId);
        WriteU32(stream, checked((uint)virtualThreadId));
        WriteU64(stream, domainTag);
        WriteText(stream, decisionId);
        WriteU64(stream, ownerId);
        WriteU32(stream, ownerPolicyVersion);
        WriteU32(stream, ownerEpoch);
        WriteText(stream, operationNamespace);
        WriteText(stream, operationId);
        WriteU16(stream, numericLeaf);
        WriteU64(stream, restoreGeneration);
        stream.WriteByte((byte)effectClass);
        stream.WriteByte((byte)resultAbi);
        stream.Write(Convert.FromHexString(effectDigest));
        stream.Write(Convert.FromHexString(resultDigest));
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static string HashConstant(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(value))).ToLowerInvariant();

    private static void WriteText(Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        WriteU32(stream, checked((uint)bytes.Length));
        stream.Write(bytes);
    }

    private static void WriteU16(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
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
