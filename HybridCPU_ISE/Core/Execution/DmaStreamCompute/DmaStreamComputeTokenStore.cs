using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute
{
    public enum DmaStreamComputeIssueAdmissionStatus : byte
    {
        Accepted = 0,
        Rejected = 1,
        ArchitecturalFault = 2
    }

    public enum DmaStreamComputeIssueAdmissionRejectKind : byte
    {
        None = 0,
        InvalidIssuePlacement = 1,
        OwnerDomainMismatch = 2,
        StoreCapacity = 3,
        DomainQuota = 4,
        Pressure = 5
    }

    public readonly record struct DmaStreamComputeTokenHandle
    {
        public DmaStreamComputeTokenHandle(
            ulong tokenId,
            ushort ownerVirtualThreadId,
            uint ownerContextId,
            uint ownerCoreId,
            uint ownerPodId,
            ulong ownerDomainTag,
            uint deviceId,
            ulong generation)
        {
            TokenId = tokenId;
            OwnerVirtualThreadId = ownerVirtualThreadId;
            OwnerContextId = ownerContextId;
            OwnerCoreId = ownerCoreId;
            OwnerPodId = ownerPodId;
            OwnerDomainTag = ownerDomainTag;
            DeviceId = deviceId;
            Generation = generation;
        }

        public ulong TokenId { get; }

        public ushort OwnerVirtualThreadId { get; }

        public uint OwnerContextId { get; }

        public uint OwnerCoreId { get; }

        public uint OwnerPodId { get; }

        public ulong OwnerDomainTag { get; }

        public uint DeviceId { get; }

        public ulong Generation { get; }

        public bool IsDefault => TokenId == 0 || Generation == 0;

        public bool MatchesOwner(DmaStreamComputeOwnerBinding ownerBinding)
        {
            ArgumentNullException.ThrowIfNull(ownerBinding);
            return OwnerVirtualThreadId == ownerBinding.OwnerVirtualThreadId &&
                   OwnerContextId == ownerBinding.OwnerContextId &&
                   OwnerCoreId == ownerBinding.OwnerCoreId &&
                   OwnerPodId == ownerBinding.OwnerPodId &&
                   OwnerDomainTag == ownerBinding.OwnerDomainTag &&
                   DeviceId == ownerBinding.DeviceId;
        }
    }

    public readonly record struct DmaStreamComputeIssueAdmissionMetadata
    {
        public DmaStreamComputeIssueAdmissionMetadata(
            ulong issuingPc,
            ulong bundleId,
            byte slotIndex,
            byte laneIndex,
            ulong issueCycle,
            ulong replayEpoch)
        {
            IssuingPc = issuingPc;
            BundleId = bundleId;
            SlotIndex = slotIndex;
            LaneIndex = laneIndex;
            IssueCycle = issueCycle;
            ReplayEpoch = replayEpoch;
        }

        public ulong IssuingPc { get; }

        public ulong BundleId { get; }

        public byte SlotIndex { get; }

        public byte LaneIndex { get; }

        public ulong IssueCycle { get; }

        public ulong ReplayEpoch { get; }

        public bool IsLane6IssuePlacement => SlotIndex == 6 && LaneIndex == 6;

        public static DmaStreamComputeIssueAdmissionMetadata Lane6(
            ulong issuingPc = 0,
            ulong bundleId = 0,
            ulong issueCycle = 0,
            ulong replayEpoch = 0) =>
            new(issuingPc, bundleId, slotIndex: 6, laneIndex: 6, issueCycle, replayEpoch);
    }

    public readonly record struct DmaStreamComputeTokenStoreOptions
    {
        public DmaStreamComputeTokenStoreOptions(
            ushort activeTokenCapacity,
            ushort perDomainTokenQuota,
            ulong generation = 1,
            ulong firstTokenId = 1)
        {
            ActiveTokenCapacity = activeTokenCapacity == 0 ? (ushort)1 : activeTokenCapacity;
            PerDomainTokenQuota = perDomainTokenQuota == 0 ? (ushort)1 : perDomainTokenQuota;
            Generation = generation == 0 ? 1 : generation;
            FirstTokenId = firstTokenId == 0 ? 1 : firstTokenId;
        }

        public ushort ActiveTokenCapacity { get; }

        public ushort PerDomainTokenQuota { get; }

        public ulong Generation { get; }

        public ulong FirstTokenId { get; }

        public static DmaStreamComputeTokenStoreOptions Default { get; } =
            new(activeTokenCapacity: 16, perDomainTokenQuota: 16);
    }

    public sealed record DmaStreamComputeIssueAdmissionRequest
    {
        public DmaStreamComputeIssueAdmissionRequest(
            DmaStreamComputeMicroOp carrier,
            DmaStreamComputeOwnerGuardDecision issueGuardDecision,
            DmaStreamComputeIssueAdmissionMetadata metadata,
            DmaStreamComputePressurePolicy pressurePolicy,
            DmaStreamComputePressureSnapshot pressureSnapshot,
            DmaStreamComputeTelemetryCounters? telemetry = null)
        {
            ArgumentNullException.ThrowIfNull(carrier);
            Carrier = carrier;
            IssueGuardDecision = issueGuardDecision;
            Metadata = metadata;
            PressurePolicy = pressurePolicy;
            PressureSnapshot = pressureSnapshot;
            Telemetry = telemetry;
        }

        public DmaStreamComputeMicroOp Carrier { get; }

        public DmaStreamComputeOwnerGuardDecision IssueGuardDecision { get; }

        public DmaStreamComputeIssueAdmissionMetadata Metadata { get; }

        public DmaStreamComputePressurePolicy PressurePolicy { get; }

        public DmaStreamComputePressureSnapshot PressureSnapshot { get; }

        public DmaStreamComputeTelemetryCounters? Telemetry { get; }

        public static DmaStreamComputeIssueAdmissionRequest ForLane6(
            DmaStreamComputeMicroOp carrier,
            DmaStreamComputeIssueAdmissionMetadata metadata,
            DmaStreamComputeTelemetryCounters? telemetry = null)
        {
            ArgumentNullException.ThrowIfNull(carrier);
            DmaStreamComputePressurePolicy policy = DmaStreamComputePressurePolicy.Default;
            return new DmaStreamComputeIssueAdmissionRequest(
                carrier,
                carrier.Descriptor.OwnerGuardDecision,
                metadata,
                policy,
                DmaStreamComputePressureSnapshot.Permissive(policy),
                telemetry);
        }
    }

    public sealed record DmaStreamComputeActiveTokenEntry
    {
        public DmaStreamComputeActiveTokenEntry(
            DmaStreamComputeTokenHandle handle,
            DmaStreamComputeToken token,
            DmaStreamComputeIssueAdmissionMetadata metadata,
            ulong descriptorIdentityHash,
            ulong normalizedFootprintHash)
        {
            ArgumentNullException.ThrowIfNull(token);
            Handle = handle;
            Token = token;
            Metadata = metadata;
            DescriptorIdentityHash = descriptorIdentityHash;
            NormalizedFootprintHash = normalizedFootprintHash;
        }

        public DmaStreamComputeTokenHandle Handle { get; }

        public DmaStreamComputeToken Token { get; }

        public DmaStreamComputeIssueAdmissionMetadata Metadata { get; }

        public ulong DescriptorIdentityHash { get; }

        public ulong NormalizedFootprintHash { get; }

        public DmaStreamComputeTokenLifecycleEvidence ExportLifecycleEvidence() =>
            Token.ExportLifecycleEvidence();
    }

    public sealed record DmaStreamComputeIssueAdmissionResult
    {
        private DmaStreamComputeIssueAdmissionResult(
            DmaStreamComputeIssueAdmissionStatus status,
            DmaStreamComputeIssueAdmissionRejectKind rejectKind,
            DmaStreamComputeValidationFault validationFault,
            DmaStreamComputePressureRejectKind pressureRejectKind,
            DmaStreamComputeActiveTokenEntry? entry,
            DmaStreamComputeFaultRecord? fault,
            string message)
        {
            Status = status;
            RejectKind = rejectKind;
            ValidationFault = validationFault;
            PressureRejectKind = pressureRejectKind;
            Entry = entry;
            Fault = fault;
            Message = message;
        }

        public DmaStreamComputeIssueAdmissionStatus Status { get; }

        public DmaStreamComputeIssueAdmissionRejectKind RejectKind { get; }

        public DmaStreamComputeValidationFault ValidationFault { get; }

        public DmaStreamComputePressureRejectKind PressureRejectKind { get; }

        public DmaStreamComputeActiveTokenEntry? Entry { get; }

        public DmaStreamComputeToken? Token => Entry?.Token;

        public DmaStreamComputeTokenHandle Handle => Entry?.Handle ?? default;

        public DmaStreamComputeFaultRecord? Fault { get; }

        public string Message { get; }

        public bool IsAccepted =>
            Status == DmaStreamComputeIssueAdmissionStatus.Accepted && Entry is not null;

        public bool HasAllocatedToken => IsAccepted && !Handle.IsDefault;

        public bool RequiresRetireExceptionPublication =>
            Fault?.RequiresRetireExceptionPublication == true;

        public static DmaStreamComputeIssueAdmissionResult Accepted(
            DmaStreamComputeActiveTokenEntry entry) =>
            new(
                DmaStreamComputeIssueAdmissionStatus.Accepted,
                DmaStreamComputeIssueAdmissionRejectKind.None,
                DmaStreamComputeValidationFault.None,
                DmaStreamComputePressureRejectKind.None,
                entry,
                fault: null,
                "DmaStreamCompute token allocated by explicit issue/admission store; execution remains gated.");

        public static DmaStreamComputeIssueAdmissionResult Rejected(
            DmaStreamComputeIssueAdmissionRejectKind rejectKind,
            DmaStreamComputeValidationFault validationFault,
            DmaStreamComputePressureRejectKind pressureRejectKind,
            string message) =>
            new(
                DmaStreamComputeIssueAdmissionStatus.Rejected,
                rejectKind,
                validationFault,
                pressureRejectKind,
                entry: null,
                fault: null,
                message);

        public static DmaStreamComputeIssueAdmissionResult ArchitecturalFault(
            DmaStreamComputeIssueAdmissionRejectKind rejectKind,
            DmaStreamComputeValidationFault validationFault,
            DmaStreamComputeFaultRecord fault,
            string message) =>
            new(
                DmaStreamComputeIssueAdmissionStatus.ArchitecturalFault,
                rejectKind,
                validationFault,
                DmaStreamComputePressureRejectKind.None,
                entry: null,
                fault,
                message);
    }

    public sealed class DmaStreamComputeTokenStore
    {
        private readonly Dictionary<DmaStreamComputeTokenHandle, DmaStreamComputeActiveTokenEntry> _activeTokens = new();
        private readonly Dictionary<ulong, int> _activeTokensByDomain = new();
        private ulong _nextTokenId;

        public DmaStreamComputeTokenStore()
            : this(DmaStreamComputeTokenStoreOptions.Default)
        {
        }

        public DmaStreamComputeTokenStore(DmaStreamComputeTokenStoreOptions options)
        {
            Options = options.Equals(default)
                ? DmaStreamComputeTokenStoreOptions.Default
                : options;
            _nextTokenId = Options.FirstTokenId;
        }

        public DmaStreamComputeTokenStoreOptions Options { get; }

        public int ActiveTokenCount => _activeTokens.Count;

        public ulong Generation => Options.Generation;

        public DmaStreamComputeIssueAdmissionResult TryAllocateAtIssueAdmission(
            DmaStreamComputeIssueAdmissionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            DmaStreamComputeMicroOp carrier = request.Carrier;
            DmaStreamComputeDescriptor descriptor = carrier.Descriptor;
            request.Telemetry?.RecordPressureSnapshot(
                request.PressurePolicy,
                request.PressureSnapshot);

            if (!request.Metadata.IsLane6IssuePlacement ||
                carrier.Placement.RequiredSlotClass != SlotClass.DmaStreamClass)
            {
                return DmaStreamComputeIssueAdmissionResult.Rejected(
                    DmaStreamComputeIssueAdmissionRejectKind.InvalidIssuePlacement,
                    DmaStreamComputeValidationFault.BackpressureAdmissionReject,
                    DmaStreamComputePressureRejectKind.Lane6Unavailable,
                    "DmaStreamCompute token store allocates only at materialized lane6 issue/admission.");
            }

            if (!TryValidateIssueGuard(
                    descriptor,
                    request.IssueGuardDecision,
                    out DmaStreamComputeIssueAdmissionResult? guardReject))
            {
                return guardReject!;
            }

            if (!TryPassPressure(
                    request,
                    out DmaStreamComputeIssueAdmissionResult? pressureReject))
            {
                return pressureReject!;
            }

            if (_activeTokens.Count >= Options.ActiveTokenCapacity)
            {
                request.Telemetry?.RecordAdmissionReject(
                    DmaStreamComputePressureRejectKind.OutstandingTokenCap,
                    DmaStreamComputeValidationFault.TokenCapAdmissionReject);
                return DmaStreamComputeIssueAdmissionResult.Rejected(
                    DmaStreamComputeIssueAdmissionRejectKind.StoreCapacity,
                    DmaStreamComputeValidationFault.TokenCapAdmissionReject,
                    DmaStreamComputePressureRejectKind.OutstandingTokenCap,
                    "DmaStreamCompute token store capacity rejected issue/admission before token creation.");
            }

            ulong ownerDomainTag = descriptor.OwnerBinding.OwnerDomainTag;
            if (GetDomainActiveCount(ownerDomainTag) >= Options.PerDomainTokenQuota)
            {
                request.Telemetry?.RecordAdmissionReject(
                    DmaStreamComputePressureRejectKind.OutstandingTokenCap,
                    DmaStreamComputeValidationFault.QuotaAdmissionReject);
                return DmaStreamComputeIssueAdmissionResult.Rejected(
                    DmaStreamComputeIssueAdmissionRejectKind.DomainQuota,
                    DmaStreamComputeValidationFault.QuotaAdmissionReject,
                    DmaStreamComputePressureRejectKind.OutstandingTokenCap,
                    "DmaStreamCompute per-domain token quota rejected issue/admission before token creation.");
            }

            DmaStreamComputeTokenHandle handle = AllocateHandle(descriptor.OwnerBinding);
            var token = new DmaStreamComputeToken(
                descriptor,
                handle.TokenId,
                request.Telemetry);
            var entry = new DmaStreamComputeActiveTokenEntry(
                handle,
                token,
                request.Metadata,
                descriptor.DescriptorIdentityHash,
                descriptor.NormalizedFootprintHash);

            _activeTokens.Add(handle, entry);
            _activeTokensByDomain[ownerDomainTag] = GetDomainActiveCount(ownerDomainTag) + 1;
            return DmaStreamComputeIssueAdmissionResult.Accepted(entry);
        }

        public bool TryGet(
            DmaStreamComputeTokenHandle handle,
            DmaStreamComputeOwnerBinding ownerBinding,
            out DmaStreamComputeActiveTokenEntry? entry)
        {
            ArgumentNullException.ThrowIfNull(ownerBinding);
            entry = null;

            if (handle.IsDefault ||
                !handle.MatchesOwner(ownerBinding) ||
                !_activeTokens.TryGetValue(handle, out DmaStreamComputeActiveTokenEntry? candidate))
            {
                return false;
            }

            entry = candidate;
            return true;
        }

        public bool TryCancel(
            DmaStreamComputeTokenHandle handle,
            DmaStreamComputeOwnerBinding ownerBinding,
            DmaStreamComputeTokenCancelReason reason)
        {
            if (!TryGet(handle, ownerBinding, out DmaStreamComputeActiveTokenEntry? entry))
            {
                return false;
            }

            if (entry!.Token.State is
                DmaStreamComputeTokenState.Committed or
                DmaStreamComputeTokenState.Faulted or
                DmaStreamComputeTokenState.Canceled)
            {
                return false;
            }

            entry.Token.Cancel(reason);
            return true;
        }

        public DmaStreamComputeTokenHandle[] SnapshotActiveHandles()
        {
            var handles = new DmaStreamComputeTokenHandle[_activeTokens.Count];
            _activeTokens.Keys.CopyTo(handles, 0);
            return handles;
        }

        private DmaStreamComputeTokenHandle AllocateHandle(
            DmaStreamComputeOwnerBinding ownerBinding)
        {
            ulong tokenId = _nextTokenId++;
            if (_nextTokenId == 0)
            {
                _nextTokenId = 1;
            }

            return new DmaStreamComputeTokenHandle(
                tokenId,
                ownerBinding.OwnerVirtualThreadId,
                ownerBinding.OwnerContextId,
                ownerBinding.OwnerCoreId,
                ownerBinding.OwnerPodId,
                ownerBinding.OwnerDomainTag,
                ownerBinding.DeviceId,
                Options.Generation);
        }

        private int GetDomainActiveCount(ulong ownerDomainTag) =>
            _activeTokensByDomain.TryGetValue(ownerDomainTag, out int count) ? count : 0;

        private static bool TryValidateIssueGuard(
            DmaStreamComputeDescriptor descriptor,
            DmaStreamComputeOwnerGuardDecision issueGuardDecision,
            out DmaStreamComputeIssueAdmissionResult? result)
        {
            result = null;
            DmaStreamComputeOwnerBinding owner = descriptor.OwnerBinding;
            DmaStreamComputeOwnerGuardContext context =
                issueGuardDecision.RuntimeOwnerContext;

            if (!issueGuardDecision.IsAllowed ||
                issueGuardDecision.DescriptorOwnerBinding is null ||
                !issueGuardDecision.DescriptorOwnerBinding.Equals(owner) ||
                issueGuardDecision.LegalityDecision.AuthoritySource != LegalityAuthoritySource.GuardPlane ||
                issueGuardDecision.LegalityDecision.AttemptedReplayCertificateReuse ||
                context.OwnerVirtualThreadId != owner.OwnerVirtualThreadId ||
                context.OwnerContextId != owner.OwnerContextId ||
                context.OwnerCoreId != owner.OwnerCoreId ||
                context.OwnerPodId != owner.OwnerPodId ||
                context.OwnerDomainTag != owner.OwnerDomainTag ||
                context.DeviceId != owner.DeviceId)
            {
                DmaStreamComputeTokenFaultKind faultKind =
                    context.DeviceId != owner.DeviceId
                        ? DmaStreamComputeTokenFaultKind.DmaDeviceFault
                        : DmaStreamComputeTokenFaultKind.DomainViolation;
                var fault = new DmaStreamComputeFaultRecord(
                    faultKind,
                    "DmaStreamCompute issue/admission guard rejected before token allocation.",
                    descriptor.DescriptorReference.DescriptorAddress,
                    isWrite: false,
                    owner.OwnerVirtualThreadId,
                    owner.OwnerDomainTag,
                    context.ActiveDomainCertificate);

                result = DmaStreamComputeIssueAdmissionResult.ArchitecturalFault(
                    DmaStreamComputeIssueAdmissionRejectKind.OwnerDomainMismatch,
                    DmaStreamComputeValidationFault.OwnerDomainFault,
                    fault,
                    fault.Message);
                return false;
            }

            return true;
        }

        private static bool TryPassPressure(
            DmaStreamComputeIssueAdmissionRequest request,
            out DmaStreamComputeIssueAdmissionResult? result)
        {
            result = null;
            DmaStreamComputePressurePolicy policy = request.PressurePolicy;
            DmaStreamComputePressureSnapshot snapshot = request.PressureSnapshot;

            if (!snapshot.Lane6Available)
            {
                return RejectPressure(
                    request,
                    DmaStreamComputePressureRejectKind.Lane6Unavailable,
                    DmaStreamComputeValidationFault.BackpressureAdmissionReject,
                    "DmaStreamCompute lane6 pressure rejected issue/admission before token creation.",
                    out result);
            }

            if (snapshot.DmaCreditsAvailable < policy.RequiredDmaCredits)
            {
                return RejectPressure(
                    request,
                    DmaStreamComputePressureRejectKind.DmaCredits,
                    DmaStreamComputeValidationFault.BackpressureAdmissionReject,
                    "DmaStreamCompute DMA credit pressure rejected issue/admission before token creation.",
                    out result);
            }

            if (snapshot.SrfCreditsAvailable < policy.RequiredSrfCredits)
            {
                return RejectPressure(
                    request,
                    DmaStreamComputePressureRejectKind.SrfCredits,
                    DmaStreamComputeValidationFault.BackpressureAdmissionReject,
                    "DmaStreamCompute SRF credit pressure rejected issue/admission before token creation.",
                    out result);
            }

            if (snapshot.MemorySubsystemCreditsAvailable < policy.RequiredMemoryCredits)
            {
                return RejectPressure(
                    request,
                    DmaStreamComputePressureRejectKind.MemorySubsystemPressure,
                    DmaStreamComputeValidationFault.BackpressureAdmissionReject,
                    "DmaStreamCompute memory pressure rejected issue/admission before token creation.",
                    out result);
            }

            if (snapshot.OutstandingTokens >= policy.OutstandingTokenCap)
            {
                return RejectPressure(
                    request,
                    DmaStreamComputePressureRejectKind.OutstandingTokenCap,
                    DmaStreamComputeValidationFault.TokenCapAdmissionReject,
                    "DmaStreamCompute outstanding-token pressure rejected issue/admission before token creation.",
                    out result);
            }

            return true;
        }

        private static bool RejectPressure(
            DmaStreamComputeIssueAdmissionRequest request,
            DmaStreamComputePressureRejectKind pressureRejectKind,
            DmaStreamComputeValidationFault validationFault,
            string message,
            out DmaStreamComputeIssueAdmissionResult? result)
        {
            request.Telemetry?.RecordAdmissionReject(pressureRejectKind, validationFault);
            result = DmaStreamComputeIssueAdmissionResult.Rejected(
                DmaStreamComputeIssueAdmissionRejectKind.Pressure,
                validationFault,
                pressureRejectKind,
                message);
            return false;
        }
    }
}
