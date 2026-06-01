using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute
{
    public enum DmaStreamComputePressureRejectKind : byte
    {
        None = 0,
        Lane6Unavailable = 1,
        DmaCredits = 2,
        SrfCredits = 3,
        MemorySubsystemPressure = 4,
        OutstandingTokenCap = 5
    }

    public readonly record struct DmaStreamComputePressurePolicy
    {
        public DmaStreamComputePressurePolicy(
            byte requiredDmaCredits,
            ushort requiredSrfCredits,
            byte requiredMemoryCredits,
            ushort outstandingTokenCap)
        {
            RequiredDmaCredits = requiredDmaCredits == 0 ? (byte)1 : requiredDmaCredits;
            RequiredSrfCredits = requiredSrfCredits == 0 ? (ushort)1 : requiredSrfCredits;
            RequiredMemoryCredits = requiredMemoryCredits == 0 ? (byte)1 : requiredMemoryCredits;
            OutstandingTokenCap = outstandingTokenCap == 0 ? (ushort)1 : outstandingTokenCap;
        }

        public byte RequiredDmaCredits { get; }

        public ushort RequiredSrfCredits { get; }

        public byte RequiredMemoryCredits { get; }

        public ushort OutstandingTokenCap { get; }

        public static DmaStreamComputePressurePolicy Default { get; } =
            new(requiredDmaCredits: 1, requiredSrfCredits: 1, requiredMemoryCredits: 1, outstandingTokenCap: 16);
    }

    public readonly record struct DmaStreamComputePressureSnapshot
    {
        public DmaStreamComputePressureSnapshot(
            bool lane6Available,
            byte dmaCreditsAvailable,
            ushort srfCreditsAvailable,
            byte memorySubsystemCreditsAvailable,
            ushort outstandingTokens)
        {
            Lane6Available = lane6Available;
            DmaCreditsAvailable = dmaCreditsAvailable;
            SrfCreditsAvailable = srfCreditsAvailable;
            MemorySubsystemCreditsAvailable = memorySubsystemCreditsAvailable;
            OutstandingTokens = outstandingTokens;
        }

        public bool Lane6Available { get; }

        public byte DmaCreditsAvailable { get; }

        public ushort SrfCreditsAvailable { get; }

        public byte MemorySubsystemCreditsAvailable { get; }

        public ushort OutstandingTokens { get; }

        public static DmaStreamComputePressureSnapshot Permissive(DmaStreamComputePressurePolicy policy) =>
            new(
                lane6Available: true,
                dmaCreditsAvailable: policy.RequiredDmaCredits,
                srfCreditsAvailable: policy.RequiredSrfCredits,
                memorySubsystemCreditsAvailable: policy.RequiredMemoryCredits,
                outstandingTokens: 0);

        public static DmaStreamComputePressureSnapshot FromHardwareOccupancy(
            HardwareOccupancySnapshot128 snapshot,
            bool lane6Available = true,
            byte dmaCreditsAvailable = 1,
            ushort srfCreditsAvailable = 1,
            ushort outstandingTokens = 0)
        {
            return new DmaStreamComputePressureSnapshot(
                lane6Available,
                dmaCreditsAvailable,
                srfCreditsAvailable,
                snapshot.MemoryIssueBudget,
                outstandingTokens);
        }
    }

    public sealed record DmaStreamComputeTelemetrySnapshot
    {
        public long DescriptorParseAttempts { get; init; }

        public long DescriptorAccepted { get; init; }

        public long DescriptorRejected { get; init; }

        public long ComputeAccepted { get; init; }

        public long ComputeActive { get; init; }

        public long ComputeStaged { get; init; }

        public long ComputeCommitted { get; init; }

        public long ComputeCanceled { get; init; }

        public long ComputeFaulted { get; init; }

        public long ComputeRejected { get; init; }

        public long DescriptorFaults { get; init; }

        public long OwnerDomainFaults { get; init; }

        public long DeviceFaults { get; init; }

        public long TokenFaults { get; init; }

        public long QuotaRejects { get; init; }

        public long BackpressureRejects { get; init; }

        public long Lane6BackpressureRejects { get; init; }

        public long DmaCreditRejects { get; init; }

        public long SrfCreditRejects { get; init; }

        public long MemorySubsystemPressureRejects { get; init; }

        public long OutstandingTokenCapRejects { get; init; }

        public long UnsupportedCarrierRejects { get; init; }

        public long ReplayEnvelopeReuseHits { get; init; }

        public long ReplayEnvelopeRejects { get; init; }

        public ulong BytesRead { get; init; }

        public ulong BytesStaged { get; init; }

        public ulong ElementOperations { get; init; }

        public ulong CopyOperations { get; init; }

        public ulong AddOperations { get; init; }

        public ulong MulOperations { get; init; }

        public ulong FmaOperations { get; init; }

        public ulong ReduceOperations { get; init; }

        public DmaStreamComputeValidationFault LastValidationFault { get; init; }

        public DmaStreamComputeTokenFaultKind LastTokenFaultKind { get; init; }

        public DmaStreamComputePressureRejectKind LastPressureRejectKind { get; init; }

        public ReplayPhaseInvalidationReason LastReplayInvalidationReason { get; init; }

        public string? LastReplayMismatchField { get; init; }

        public bool LastLane6Available { get; init; }

        public byte LastDmaCreditsAvailable { get; init; }

        public ushort LastSrfCreditsAvailable { get; init; }

        public byte LastMemorySubsystemCreditsAvailable { get; init; }

        public ushort LastOutstandingTokens { get; init; }

        public ushort LastOutstandingTokenCap { get; init; }
    }

    public sealed class DmaStreamComputeTelemetryCounters
    {
        private readonly HashSet<ulong> _acceptedTokens = new();
        private readonly HashSet<ulong> _activeTokens = new();
        private readonly HashSet<ulong> _stagedTokens = new();
        private readonly HashSet<ulong> _committedTokens = new();
        private readonly HashSet<ulong> _canceledTokens = new();
        private readonly HashSet<ulong> _faultedTokens = new();

        private long _descriptorParseAttempts;
        private long _descriptorAccepted;
        private long _descriptorRejected;
        private long _computeRejected;
        private long _descriptorFaults;
        private long _ownerDomainFaults;
        private long _deviceFaults;
        private long _tokenFaults;
        private long _quotaRejects;
        private long _backpressureRejects;
        private long _lane6BackpressureRejects;
        private long _dmaCreditRejects;
        private long _srfCreditRejects;
        private long _memorySubsystemPressureRejects;
        private long _outstandingTokenCapRejects;
        private long _unsupportedCarrierRejects;
        private long _replayEnvelopeReuseHits;
        private long _replayEnvelopeRejects;
        private ulong _bytesRead;
        private ulong _bytesStaged;
        private ulong _elementOperations;
        private ulong _copyOperations;
        private ulong _addOperations;
        private ulong _mulOperations;
        private ulong _fmaOperations;
        private ulong _reduceOperations;
        private DmaStreamComputeValidationFault _lastValidationFault;
        private DmaStreamComputeTokenFaultKind _lastTokenFaultKind;
        private DmaStreamComputePressureRejectKind _lastPressureRejectKind;
        private ReplayPhaseInvalidationReason _lastReplayInvalidationReason;
        private string? _lastReplayMismatchField;
        private bool _lastLane6Available;
        private byte _lastDmaCreditsAvailable;
        private ushort _lastSrfCreditsAvailable;
        private byte _lastMemorySubsystemCreditsAvailable;
        private ushort _lastOutstandingTokens;
        private ushort _lastOutstandingTokenCap;

        public DmaStreamComputeTelemetrySnapshot Snapshot() =>
            new()
            {
                DescriptorParseAttempts = _descriptorParseAttempts,
                DescriptorAccepted = _descriptorAccepted,
                DescriptorRejected = _descriptorRejected,
                ComputeAccepted = _acceptedTokens.Count,
                ComputeActive = _activeTokens.Count,
                ComputeStaged = _stagedTokens.Count,
                ComputeCommitted = _committedTokens.Count,
                ComputeCanceled = _canceledTokens.Count,
                ComputeFaulted = _faultedTokens.Count,
                ComputeRejected = _computeRejected,
                DescriptorFaults = _descriptorFaults,
                OwnerDomainFaults = _ownerDomainFaults,
                DeviceFaults = _deviceFaults,
                TokenFaults = _tokenFaults,
                QuotaRejects = _quotaRejects,
                BackpressureRejects = _backpressureRejects,
                Lane6BackpressureRejects = _lane6BackpressureRejects,
                DmaCreditRejects = _dmaCreditRejects,
                SrfCreditRejects = _srfCreditRejects,
                MemorySubsystemPressureRejects = _memorySubsystemPressureRejects,
                OutstandingTokenCapRejects = _outstandingTokenCapRejects,
                UnsupportedCarrierRejects = _unsupportedCarrierRejects,
                ReplayEnvelopeReuseHits = _replayEnvelopeReuseHits,
                ReplayEnvelopeRejects = _replayEnvelopeRejects,
                BytesRead = _bytesRead,
                BytesStaged = _bytesStaged,
                ElementOperations = _elementOperations,
                CopyOperations = _copyOperations,
                AddOperations = _addOperations,
                MulOperations = _mulOperations,
                FmaOperations = _fmaOperations,
                ReduceOperations = _reduceOperations,
                LastValidationFault = _lastValidationFault,
                LastTokenFaultKind = _lastTokenFaultKind,
                LastPressureRejectKind = _lastPressureRejectKind,
                LastReplayInvalidationReason = _lastReplayInvalidationReason,
                LastReplayMismatchField = _lastReplayMismatchField,
                LastLane6Available = _lastLane6Available,
                LastDmaCreditsAvailable = _lastDmaCreditsAvailable,
                LastSrfCreditsAvailable = _lastSrfCreditsAvailable,
                LastMemorySubsystemCreditsAvailable = _lastMemorySubsystemCreditsAvailable,
                LastOutstandingTokens = _lastOutstandingTokens,
                LastOutstandingTokenCap = _lastOutstandingTokenCap
            };

        public void RecordDescriptorParseAttempt()
        {
            _descriptorParseAttempts++;
        }

        public void RecordDescriptorAccepted(DmaStreamComputeDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            _descriptorAccepted++;
            _lastValidationFault = DmaStreamComputeValidationFault.None;
        }

        public void RecordDescriptorRejected(DmaStreamComputeValidationFault fault)
        {
            if (fault == DmaStreamComputeValidationFault.None)
            {
                return;
            }

            _descriptorRejected++;
            _computeRejected++;
            _lastValidationFault = fault;
            RecordValidationFaultFamily(fault);
        }

        public void RecordOwnerGuardRejected(
            DmaStreamComputeOwnerBinding structuralOwnerBinding,
            DmaStreamComputeOwnerGuardDecision ownerGuardDecision)
        {
            ArgumentNullException.ThrowIfNull(structuralOwnerBinding);

            _descriptorRejected++;
            _computeRejected++;
            _lastValidationFault = DmaStreamComputeValidationFault.OwnerDomainFault;

            if (structuralOwnerBinding.DeviceId != DmaStreamComputeDescriptor.CanonicalLane6DeviceId ||
                structuralOwnerBinding.DeviceId != ownerGuardDecision.RuntimeOwnerContext.DeviceId)
            {
                _deviceFaults++;
                return;
            }

            _ownerDomainFaults++;
        }

        public void RecordPressureSnapshot(
            DmaStreamComputePressurePolicy policy,
            DmaStreamComputePressureSnapshot snapshot)
        {
            _lastLane6Available = snapshot.Lane6Available;
            _lastDmaCreditsAvailable = snapshot.DmaCreditsAvailable;
            _lastSrfCreditsAvailable = snapshot.SrfCreditsAvailable;
            _lastMemorySubsystemCreditsAvailable = snapshot.MemorySubsystemCreditsAvailable;
            _lastOutstandingTokens = snapshot.OutstandingTokens;
            _lastOutstandingTokenCap = policy.OutstandingTokenCap;
        }

        public void RecordAdmissionReject(
            DmaStreamComputePressureRejectKind rejectKind,
            DmaStreamComputeValidationFault validationFault)
        {
            if (rejectKind == DmaStreamComputePressureRejectKind.None)
            {
                return;
            }

            _computeRejected++;
            _lastPressureRejectKind = rejectKind;
            _lastValidationFault = validationFault;

            switch (rejectKind)
            {
                case DmaStreamComputePressureRejectKind.OutstandingTokenCap:
                    _quotaRejects++;
                    _outstandingTokenCapRejects++;
                    break;
                case DmaStreamComputePressureRejectKind.Lane6Unavailable:
                    _backpressureRejects++;
                    _lane6BackpressureRejects++;
                    break;
                case DmaStreamComputePressureRejectKind.DmaCredits:
                    _backpressureRejects++;
                    _dmaCreditRejects++;
                    break;
                case DmaStreamComputePressureRejectKind.SrfCredits:
                    _backpressureRejects++;
                    _srfCreditRejects++;
                    break;
                case DmaStreamComputePressureRejectKind.MemorySubsystemPressure:
                    _backpressureRejects++;
                    _memorySubsystemPressureRejects++;
                    break;
            }
        }

        public void RecordTokenAccepted(DmaStreamComputeToken token)
        {
            ArgumentNullException.ThrowIfNull(token);
            _acceptedTokens.Add(token.TokenId);
        }

        public void RecordTokenActive(DmaStreamComputeToken token)
        {
            ArgumentNullException.ThrowIfNull(token);
            _activeTokens.Add(token.TokenId);
        }

        public void RecordTokenStaged(DmaStreamComputeToken token, ulong byteCount)
        {
            ArgumentNullException.ThrowIfNull(token);
            _stagedTokens.Add(token.TokenId);
            _bytesStaged += byteCount;
        }

        public void RecordTokenCanceled(DmaStreamComputeToken token)
        {
            ArgumentNullException.ThrowIfNull(token);
            _canceledTokens.Add(token.TokenId);
        }

        public void RecordTokenCommitted(DmaStreamComputeToken token)
        {
            ArgumentNullException.ThrowIfNull(token);
            _committedTokens.Add(token.TokenId);
        }

        public void RecordTokenFaulted(
            DmaStreamComputeToken token,
            DmaStreamComputeTokenFaultKind faultKind)
        {
            ArgumentNullException.ThrowIfNull(token);
            _faultedTokens.Add(token.TokenId);
            _lastTokenFaultKind = faultKind;
            RecordTokenFaultFamily(faultKind);
        }

        public void RecordTokenAdmissionFault(DmaStreamComputeValidationFault validationFault)
        {
            _computeRejected++;
            _lastValidationFault = validationFault;
            RecordValidationFaultFamily(validationFault);
        }

        public void RecordRuntimeRead(ulong byteCount)
        {
            _bytesRead += byteCount;
        }

        public void RecordElementOperations(
            DmaStreamComputeOperationKind operation,
            ulong elementCount)
        {
            ulong count = elementCount == 0 ? 1UL : elementCount;
            _elementOperations += count;

            switch (operation)
            {
                case DmaStreamComputeOperationKind.Copy:
                    _copyOperations += count;
                    break;
                case DmaStreamComputeOperationKind.Add:
                    _addOperations += count;
                    break;
                case DmaStreamComputeOperationKind.Mul:
                    _mulOperations += count;
                    break;
                case DmaStreamComputeOperationKind.Fma:
                    _fmaOperations += count;
                    break;
                case DmaStreamComputeOperationKind.Reduce:
                    _reduceOperations += count;
                    break;
            }
        }

        public void RecordReplayEvidenceComparison(
            DmaStreamComputeReplayEvidenceComparison comparison)
        {
            if (comparison.CanReuse)
            {
                _replayEnvelopeReuseHits++;
                _lastReplayInvalidationReason = ReplayPhaseInvalidationReason.None;
                _lastReplayMismatchField = string.Empty;
                return;
            }

            _replayEnvelopeRejects++;
            _computeRejected++;
            _lastReplayInvalidationReason = comparison.InvalidationReason;
            _lastReplayMismatchField = comparison.MismatchField;
        }

        private void RecordValidationFaultFamily(DmaStreamComputeValidationFault fault)
        {
            switch (fault)
            {
                case DmaStreamComputeValidationFault.OwnerDomainFault:
                    _ownerDomainFaults++;
                    break;
                case DmaStreamComputeValidationFault.DescriptorCarrierDecodeFault:
                case DmaStreamComputeValidationFault.DescriptorReferenceLost:
                    _unsupportedCarrierRejects++;
                    _descriptorFaults++;
                    break;
                case DmaStreamComputeValidationFault.QuotaAdmissionReject:
                case DmaStreamComputeValidationFault.TokenCapAdmissionReject:
                    _quotaRejects++;
                    break;
                case DmaStreamComputeValidationFault.BackpressureAdmissionReject:
                    _backpressureRejects++;
                    break;
                default:
                    _descriptorFaults++;
                    break;
            }
        }

        private void RecordTokenFaultFamily(DmaStreamComputeTokenFaultKind faultKind)
        {
            if (faultKind == DmaStreamComputeTokenFaultKind.None)
            {
                return;
            }

            _tokenFaults++;
            switch (faultKind)
            {
                case DmaStreamComputeTokenFaultKind.DomainViolation:
                case DmaStreamComputeTokenFaultKind.OwnerContextViolation:
                    _ownerDomainFaults++;
                    break;
                case DmaStreamComputeTokenFaultKind.DmaDeviceFault:
                    _deviceFaults++;
                    break;
                case DmaStreamComputeTokenFaultKind.DescriptorDecodeFault:
                case DmaStreamComputeTokenFaultKind.UnsupportedAbiOrOperation:
                case DmaStreamComputeTokenFaultKind.ExecutionDisabled:
                    _descriptorFaults++;
                    break;
            }
        }
    }

    public static class DmaStreamComputeAdmissionController
    {
        public static DmaStreamComputeTokenAdmissionResult TryAdmit(
            DmaStreamComputeValidationResult validationResult,
            ulong tokenId,
            DmaStreamComputeTelemetryCounters? telemetry)
        {
            return TryAdmit(
                validationResult,
                tokenId,
                DmaStreamComputePressurePolicy.Default,
                DmaStreamComputePressureSnapshot.Permissive(DmaStreamComputePressurePolicy.Default),
                telemetry);
        }

        public static DmaStreamComputeTokenAdmissionResult TryAdmit(
            DmaStreamComputeValidationResult validationResult,
            ulong tokenId,
            DmaStreamComputePressurePolicy pressurePolicy,
            DmaStreamComputePressureSnapshot pressureSnapshot,
            DmaStreamComputeTelemetryCounters? telemetry)
        {
            ArgumentNullException.ThrowIfNull(validationResult);

            if (!validationResult.IsValid)
            {
                return DmaStreamComputeToken.TryAdmit(validationResult, tokenId, telemetry);
            }

            telemetry?.RecordPressureSnapshot(pressurePolicy, pressureSnapshot);
            if (!TryPassPressure(
                    pressurePolicy,
                    pressureSnapshot,
                    out DmaStreamComputePressureRejectKind rejectKind,
                    out DmaStreamComputeValidationFault validationFault,
                    out string message))
            {
                telemetry?.RecordAdmissionReject(rejectKind, validationFault);
                return DmaStreamComputeTokenAdmissionResult.TelemetryReject(
                    validationFault,
                    message);
            }

            return DmaStreamComputeTokenAdmissionResult.Accepted(
                new DmaStreamComputeToken(
                    validationResult.RequireDescriptorForAdmission(),
                    tokenId,
                    telemetry));
        }

        private static bool TryPassPressure(
            DmaStreamComputePressurePolicy policy,
            DmaStreamComputePressureSnapshot snapshot,
            out DmaStreamComputePressureRejectKind rejectKind,
            out DmaStreamComputeValidationFault validationFault,
            out string message)
        {
            rejectKind = DmaStreamComputePressureRejectKind.None;
            validationFault = DmaStreamComputeValidationFault.None;
            message = string.Empty;

            if (!snapshot.Lane6Available)
            {
                return Reject(
                    DmaStreamComputePressureRejectKind.Lane6Unavailable,
                    DmaStreamComputeValidationFault.BackpressureAdmissionReject,
                    "DmaStreamCompute lane6 placement surface is unavailable; admission fails closed.",
                    out rejectKind,
                    out validationFault,
                    out message);
            }

            if (snapshot.DmaCreditsAvailable < policy.RequiredDmaCredits)
            {
                return Reject(
                    DmaStreamComputePressureRejectKind.DmaCredits,
                    DmaStreamComputeValidationFault.BackpressureAdmissionReject,
                    "DmaStreamCompute DMA credit pressure rejected admission before token creation.",
                    out rejectKind,
                    out validationFault,
                    out message);
            }

            if (snapshot.SrfCreditsAvailable < policy.RequiredSrfCredits)
            {
                return Reject(
                    DmaStreamComputePressureRejectKind.SrfCredits,
                    DmaStreamComputeValidationFault.BackpressureAdmissionReject,
                    "DmaStreamCompute stream-register credit pressure rejected admission before token creation.",
                    out rejectKind,
                    out validationFault,
                    out message);
            }

            if (snapshot.MemorySubsystemCreditsAvailable < policy.RequiredMemoryCredits)
            {
                return Reject(
                    DmaStreamComputePressureRejectKind.MemorySubsystemPressure,
                    DmaStreamComputeValidationFault.BackpressureAdmissionReject,
                    "DmaStreamCompute memory-subsystem pressure rejected admission before token creation.",
                    out rejectKind,
                    out validationFault,
                    out message);
            }

            if (snapshot.OutstandingTokens >= policy.OutstandingTokenCap)
            {
                return Reject(
                    DmaStreamComputePressureRejectKind.OutstandingTokenCap,
                    DmaStreamComputeValidationFault.TokenCapAdmissionReject,
                    "DmaStreamCompute outstanding token cap rejected admission before token creation.",
                    out rejectKind,
                    out validationFault,
                    out message);
            }

            return true;
        }

        private static bool Reject(
            DmaStreamComputePressureRejectKind reject,
            DmaStreamComputeValidationFault fault,
            string failureMessage,
            out DmaStreamComputePressureRejectKind rejectKind,
            out DmaStreamComputeValidationFault validationFault,
            out string message)
        {
            rejectKind = reject;
            validationFault = fault;
            message = failureMessage;
            return false;
        }
    }
}
