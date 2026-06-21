using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute
{
    public enum DmaStreamComputeTokenState : byte
    {
        Admitted = 0,
        Issued = 1,
        ReadsComplete = 2,
        ComputeComplete = 3,
        CommitPending = 4,
        Committed = 5,
        Faulted = 6,
        Canceled = 7
    }

    public enum DmaStreamComputeTokenCancelReason : byte
    {
        None = 0,
        Flush = 1,
        ReplayDiscard = 2,
        Trap = 3
    }

    public enum DmaStreamComputeTokenFaultKind : byte
    {
        None = 0,
        DescriptorDecodeFault = 1,
        UnsupportedAbiOrOperation = 2,
        TranslationFault = 3,
        PermissionFault = 4,
        DomainViolation = 5,
        OwnerContextViolation = 6,
        AlignmentFault = 7,
        AliasOverlapFault = 8,
        DmaDeviceFault = 9,
        PartialCompletionFault = 10,
        ReplayInvalidationBeforeCommit = 11,
        MemoryFault = 12,
        ExecutionDisabled = 13
    }

    public enum DmaStreamComputeFaultSourcePhase : byte
    {
        Unknown = 0,
        CarrierDecode = 1,
        DescriptorParse = 2,
        Admission = 3,
        Read = 4,
        Compute = 5,
        Stage = 6,
        Completion = 7,
        Commit = 8,
        Backend = 9,
        Iommu = 10,
        Ordering = 11,
        Cancellation = 12
    }

    public enum DmaStreamComputeFaultPublicationContract : byte
    {
        ModelRetireStyleOnly = 0,
        FuturePreciseRetireRequiresPublicationMetadata = 1
    }

    public enum DmaStreamComputeFaultPriorityClass : byte
    {
        PreIssueCancellation = 1,
        CarrierDecodeAndTypedSlot = 2,
        DescriptorAdmission = 3,
        QuotaBackpressureTokenCap = 4,
        RuntimeReadBackendIommu = 5,
        ComputeStageCoverage = 6,
        CommitGuard = 7,
        CommitPhysicalWriteRollback = 8,
        PostIssueCancellation = 9
    }

    public enum DmaStreamComputeTokenAdmissionStatus : byte
    {
        Accepted = 0,
        TelemetryReject = 1,
        ArchitecturalFault = 2
    }

    public sealed record DmaStreamComputeStagedWrite
    {
        public DmaStreamComputeStagedWrite(ulong address, ReadOnlySpan<byte> data)
        {
            Address = address;
            byte[] copy = new byte[data.Length];
            data.CopyTo(copy);
            Data = copy;
        }

        public ulong Address { get; }

        public ReadOnlyMemory<byte> Data { get; }

        public ulong Length => (ulong)Data.Length;
    }

    public sealed record DmaStreamComputeProgressDiagnostics
    {
        public DmaStreamComputeProgressDiagnostics(
            ulong bytesRead,
            ulong bytesStaged,
            ulong elementOperations,
            ulong modeledLatencyCycles,
            ulong backendStepCount)
        {
            BytesRead = bytesRead;
            BytesStaged = bytesStaged;
            ElementOperations = elementOperations;
            ModeledLatencyCycles = modeledLatencyCycles;
            BackendStepCount = backendStepCount;
        }

        public ulong BytesRead { get; }

        public ulong BytesStaged { get; }

        public ulong ElementOperations { get; }

        public ulong ModeledLatencyCycles { get; }

        public ulong BackendStepCount { get; }

        public bool IsAuthoritative => false;

        public bool CanIssueToken => false;

        public bool CanSetSucceeded => false;

        public bool CanSetCommitted => false;

        public bool CanPublishMemory => false;

        public bool IsRetirePublication => false;

        public static DmaStreamComputeProgressDiagnostics Empty { get; } =
            new(
                bytesRead: 0,
                bytesStaged: 0,
                elementOperations: 0,
                modeledLatencyCycles: 0,
                backendStepCount: 0);

        public DmaStreamComputeProgressDiagnostics Add(
            ulong bytesRead,
            ulong bytesStaged,
            ulong elementOperations,
            ulong modeledLatencyCycles,
            ulong backendStepCount) =>
            new(
                SaturatingAdd(BytesRead, bytesRead),
                SaturatingAdd(BytesStaged, bytesStaged),
                SaturatingAdd(ElementOperations, elementOperations),
                SaturatingAdd(ModeledLatencyCycles, modeledLatencyCycles),
                SaturatingAdd(BackendStepCount, backendStepCount));

        private static ulong SaturatingAdd(ulong left, ulong right) =>
            ulong.MaxValue - left < right ? ulong.MaxValue : left + right;
    }

    public sealed record DmaStreamComputeFaultRecord
    {
        public DmaStreamComputeFaultRecord(
            DmaStreamComputeTokenFaultKind faultKind,
            string message,
            ulong faultAddress,
            bool isWrite,
            int virtualThreadId,
            ulong ownerDomainTag,
            ulong activeDomainCertificate,
            DmaStreamComputeFaultSourcePhase sourcePhase = DmaStreamComputeFaultSourcePhase.Unknown,
            bool backendExceptionNormalized = false,
            string? normalizedHostExceptionType = null,
            DmaStreamComputeFaultPublicationContract publicationContract =
                DmaStreamComputeFaultPublicationContract.ModelRetireStyleOnly)
        {
            if (faultKind == DmaStreamComputeTokenFaultKind.None)
            {
                throw new ArgumentException(
                    "Use a non-None fault kind for DmaStreamCompute fault records.",
                    nameof(faultKind));
            }

            FaultKind = faultKind;
            Message = string.IsNullOrWhiteSpace(message)
                ? $"DmaStreamCompute token fault: {faultKind}."
                : message;
            FaultAddress = faultAddress;
            IsWrite = isWrite;
            VirtualThreadId = virtualThreadId;
            OwnerDomainTag = ownerDomainTag;
            ActiveDomainCertificate = activeDomainCertificate;
            SourcePhase = sourcePhase;
            BackendExceptionNormalized = backendExceptionNormalized;
            NormalizedHostExceptionType = normalizedHostExceptionType;
            PublicationContract = publicationContract;
        }

        public DmaStreamComputeTokenFaultKind FaultKind { get; }

        public string Message { get; }

        public ulong FaultAddress { get; }

        public bool IsWrite { get; }

        public int VirtualThreadId { get; }

        public ulong OwnerDomainTag { get; }

        public ulong ActiveDomainCertificate { get; }

        public DmaStreamComputeFaultSourcePhase SourcePhase { get; }

        public bool BackendExceptionNormalized { get; }

        public string? NormalizedHostExceptionType { get; }

        public DmaStreamComputeFaultPublicationContract PublicationContract { get; }

        public bool RequiresRetireExceptionPublication => true;

        public bool IsFullPipelinePreciseArchitecturalException => false;

        public bool RequiresFuturePrecisePublicationMetadata =>
            PublicationContract ==
            DmaStreamComputeFaultPublicationContract.FuturePreciseRetireRequiresPublicationMetadata;

        public static DmaStreamComputeFaultRecord FromBackendException(
            DmaStreamComputeDescriptor descriptor,
            Exception exception,
            ulong faultAddress,
            bool isWrite,
            DmaStreamComputeFaultSourcePhase sourcePhase = DmaStreamComputeFaultSourcePhase.Backend)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            ArgumentNullException.ThrowIfNull(exception);

            DmaStreamComputeOwnerGuardContext guardContext =
                descriptor.OwnerGuardDecision.RuntimeOwnerContext;
            return new DmaStreamComputeFaultRecord(
                DmaStreamComputeTokenFaultKind.DmaDeviceFault,
                $"DmaStreamCompute backend exception was normalized before retire publication: {exception.GetType().Name}. {exception.Message}",
                faultAddress,
                isWrite,
                descriptor.OwnerBinding.OwnerVirtualThreadId,
                descriptor.OwnerBinding.OwnerDomainTag,
                guardContext.ActiveDomainCertificate,
                sourcePhase,
                backendExceptionNormalized: true,
                normalizedHostExceptionType: exception.GetType().FullName,
                DmaStreamComputeFaultPublicationContract.FuturePreciseRetireRequiresPublicationMetadata);
        }

        public Exception CreateRetireException()
        {
            return FaultKind switch
            {
                DmaStreamComputeTokenFaultKind.DomainViolation or
                DmaStreamComputeTokenFaultKind.OwnerContextViolation
                    => new DomainFaultException(
                        VirtualThreadId,
                        pc: 0,
                        OwnerDomainTag,
                        ActiveDomainCertificate),

                DmaStreamComputeTokenFaultKind.TranslationFault or
                DmaStreamComputeTokenFaultKind.PermissionFault or
                DmaStreamComputeTokenFaultKind.AlignmentFault or
                DmaStreamComputeTokenFaultKind.DmaDeviceFault or
                DmaStreamComputeTokenFaultKind.PartialCompletionFault or
                DmaStreamComputeTokenFaultKind.ReplayInvalidationBeforeCommit or
                DmaStreamComputeTokenFaultKind.MemoryFault
                    => new PageFaultException(Message, FaultAddress, IsWrite),

                _ => new InvalidOperationException(Message)
            };
        }
    }

    public sealed record DmaStreamComputeCommitResult
    {
        private DmaStreamComputeCommitResult(
            bool succeeded,
            bool isCanceled,
            DmaStreamComputeTokenState tokenState,
            DmaStreamComputeFaultRecord? fault)
        {
            Succeeded = succeeded;
            IsCanceled = isCanceled;
            TokenState = tokenState;
            Fault = fault;
        }

        public bool Succeeded { get; }

        public bool IsCanceled { get; }

        public DmaStreamComputeTokenState TokenState { get; }

        public DmaStreamComputeFaultRecord? Fault { get; }

        public bool RequiresRetireExceptionPublication =>
            Fault?.RequiresRetireExceptionPublication == true;

        public Exception CreateRetireException()
        {
            if (Fault == null)
            {
                throw new InvalidOperationException(
                    "DmaStreamCompute commit result does not carry a retire exception fault.");
            }

            return Fault.CreateRetireException();
        }

        public static DmaStreamComputeCommitResult Pending(DmaStreamComputeTokenState tokenState) =>
            new(succeeded: false, isCanceled: false, tokenState, fault: null);

        public static DmaStreamComputeCommitResult Success() =>
            new(succeeded: true, isCanceled: false, DmaStreamComputeTokenState.Committed, fault: null);

        public static DmaStreamComputeCommitResult Canceled() =>
            new(succeeded: false, isCanceled: true, DmaStreamComputeTokenState.Canceled, fault: null);

        public static DmaStreamComputeCommitResult Faulted(DmaStreamComputeFaultRecord fault) =>
            new(succeeded: false, isCanceled: false, DmaStreamComputeTokenState.Faulted, fault);
    }

    public sealed record DmaStreamComputeTokenAdmissionResult
    {
        private DmaStreamComputeTokenAdmissionResult(
            DmaStreamComputeTokenAdmissionStatus status,
            DmaStreamComputeValidationFault validationFault,
            DmaStreamComputeToken? token,
            DmaStreamComputeFaultRecord? fault,
            string message)
        {
            Status = status;
            ValidationFault = validationFault;
            Token = token;
            Fault = fault;
            Message = message;
        }

        public DmaStreamComputeTokenAdmissionStatus Status { get; }

        public DmaStreamComputeValidationFault ValidationFault { get; }

        public DmaStreamComputeToken? Token { get; }

        public DmaStreamComputeFaultRecord? Fault { get; }

        public string Message { get; }

        public bool IsAccepted => Status == DmaStreamComputeTokenAdmissionStatus.Accepted && Token != null;

        public bool IsTelemetryOnlyReject => Status == DmaStreamComputeTokenAdmissionStatus.TelemetryReject;

        public bool RequiresRetireExceptionPublication =>
            Fault?.RequiresRetireExceptionPublication == true;

        public static DmaStreamComputeTokenAdmissionResult Accepted(DmaStreamComputeToken token) =>
            new(
                DmaStreamComputeTokenAdmissionStatus.Accepted,
                DmaStreamComputeValidationFault.None,
                token,
                fault: null,
                "DmaStreamCompute token admitted after descriptor, owner/domain, and footprint guards.");

        public static DmaStreamComputeTokenAdmissionResult TelemetryReject(
            DmaStreamComputeValidationFault validationFault,
            string message) =>
            new(
                DmaStreamComputeTokenAdmissionStatus.TelemetryReject,
                validationFault,
                token: null,
                fault: null,
                message);

        public static DmaStreamComputeTokenAdmissionResult ArchitecturalFault(
            DmaStreamComputeValidationFault validationFault,
            DmaStreamComputeFaultRecord fault,
            string message) =>
            new(
                DmaStreamComputeTokenAdmissionStatus.ArchitecturalFault,
                validationFault,
                token: null,
                fault,
                message);
    }

    public sealed class DmaStreamComputeToken
    {
        private readonly List<DmaStreamComputeStagedWrite> _stagedWrites = new();
        private DmaStreamComputeProgressDiagnostics _progressDiagnostics =
            DmaStreamComputeProgressDiagnostics.Empty;

        public DmaStreamComputeToken(
            DmaStreamComputeDescriptor descriptor,
            ulong tokenId,
            DmaStreamComputeTelemetryCounters? telemetry = null)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            Descriptor = descriptor;
            TokenId = tokenId;
            _telemetry = telemetry;
            State = DmaStreamComputeTokenState.Admitted;

            if (TryValidateDescriptorGuards(
                    descriptor,
                    descriptor.OwnerGuardDecision,
                    "DmaStreamCompute token admission",
                    out DmaStreamComputeFaultRecord? fault))
            {
                _telemetry?.RecordTokenAccepted(this);
                return;
            }

            State = DmaStreamComputeTokenState.Faulted;
            LastFault = fault;
            _telemetry?.RecordTokenFaulted(this, fault!.FaultKind);
            throw new InvalidOperationException(fault!.Message);
        }

        private readonly DmaStreamComputeTelemetryCounters? _telemetry;

        public ulong TokenId { get; }

        public DmaStreamComputeDescriptor Descriptor { get; }

        public DmaStreamComputeTokenState State { get; private set; }

        public DmaStreamComputeFaultRecord? LastFault { get; private set; }

        public DmaStreamComputeTokenCancelReason CancelReason { get; private set; }

        public bool HasCommitted => State == DmaStreamComputeTokenState.Committed;

        public int StagedWriteCount => _stagedWrites.Count;

        public DmaStreamComputeProgressDiagnostics ProgressDiagnostics =>
            _progressDiagnostics;

        public IReadOnlyList<DmaStreamComputeStagedWrite> GetStagedWriteSnapshot() =>
            _stagedWrites.ToArray();

        public DmaStreamComputeTokenLifecycleEvidence ExportLifecycleEvidence() =>
            DmaStreamComputeTokenLifecycleEvidence.FromToken(this);

        public static DmaStreamComputeTokenAdmissionResult TryAdmit(
            DmaStreamComputeValidationResult validationResult,
            ulong tokenId,
            DmaStreamComputeTelemetryCounters? telemetry = null)
        {
            ArgumentNullException.ThrowIfNull(validationResult);

            if (validationResult.IsValid && validationResult.Descriptor != null)
            {
                return DmaStreamComputeTokenAdmissionResult.Accepted(
                    new DmaStreamComputeToken(validationResult.Descriptor, tokenId, telemetry));
            }

            if (IsTelemetryOnlyAdmissionReject(validationResult.Fault))
            {
                telemetry?.RecordTokenAdmissionFault(validationResult.Fault);
                return DmaStreamComputeTokenAdmissionResult.TelemetryReject(
                    validationResult.Fault,
                    validationResult.Message);
            }

            telemetry?.RecordTokenAdmissionFault(validationResult.Fault);
            DmaStreamComputeFaultRecord fault = CreateFaultFromValidationFailure(
                validationResult.Fault,
                validationResult.Message);
            return DmaStreamComputeTokenAdmissionResult.ArchitecturalFault(
                validationResult.Fault,
                fault,
                validationResult.Message);
        }

        public void MarkIssued()
        {
            EnsureState(DmaStreamComputeTokenState.Admitted, nameof(MarkIssued));
            State = DmaStreamComputeTokenState.Issued;
            _telemetry?.RecordTokenActive(this);
        }

        public DmaStreamComputeProgressDiagnostics RecordProgressDiagnostics(
            ulong bytesRead = 0,
            ulong bytesStaged = 0,
            ulong elementOperations = 0,
            ulong modeledLatencyCycles = 0,
            ulong backendStepCount = 0)
        {
            _progressDiagnostics = _progressDiagnostics.Add(
                bytesRead,
                bytesStaged,
                elementOperations,
                modeledLatencyCycles,
                backendStepCount);
            return _progressDiagnostics;
        }

        public void MarkReadsComplete()
        {
            EnsureState(DmaStreamComputeTokenState.Issued, nameof(MarkReadsComplete));
            State = DmaStreamComputeTokenState.ReadsComplete;
        }

        public void StageDestinationWrite(ulong address, ReadOnlySpan<byte> data)
        {
            EnsureState(DmaStreamComputeTokenState.ReadsComplete, nameof(StageDestinationWrite));
            if (data.Length == 0 ||
                address > ulong.MaxValue - (ulong)data.Length ||
                !IsRangeCoveredByFootprint(
                    address,
                    (ulong)data.Length,
                    Descriptor.NormalizedWriteMemoryRanges))
            {
                DmaStreamComputeFaultRecord fault = CreateFault(
                    DmaStreamComputeTokenFaultKind.PartialCompletionFault,
                    "DmaStreamCompute staged destination write is outside the accepted normalized write footprint.",
                    address,
                    isWrite: true);
                PublishFault(fault);
                throw new InvalidOperationException(fault.Message);
            }

            _stagedWrites.Add(new DmaStreamComputeStagedWrite(address, data));
            _telemetry?.RecordTokenStaged(this, (ulong)data.Length);
        }

        public DmaStreamComputeCommitResult MarkComputeComplete()
        {
            if (State == DmaStreamComputeTokenState.Faulted)
            {
                return DmaStreamComputeCommitResult.Faulted(LastFault!);
            }

            EnsureState(DmaStreamComputeTokenState.ReadsComplete, nameof(MarkComputeComplete));
            State = DmaStreamComputeTokenState.ComputeComplete;

            if (!HasExactStagedWriteCoverage(out ulong faultAddress, out string? failureMessage))
            {
                return PublishFault(
                    DmaStreamComputeTokenFaultKind.PartialCompletionFault,
                    failureMessage ?? "DmaStreamCompute staged destination writes do not exactly cover the normalized write footprint.",
                    faultAddress,
                    isWrite: true);
            }

            State = DmaStreamComputeTokenState.CommitPending;
            return DmaStreamComputeCommitResult.Pending(State);
        }

        public void Cancel(DmaStreamComputeTokenCancelReason reason)
        {
            if (State == DmaStreamComputeTokenState.Committed)
            {
                throw new InvalidOperationException(
                    "DmaStreamCompute token cannot be canceled after architectural commit.");
            }

            CancelReason = reason == DmaStreamComputeTokenCancelReason.None
                ? DmaStreamComputeTokenCancelReason.Flush
                : reason;
            _stagedWrites.Clear();
            State = DmaStreamComputeTokenState.Canceled;
            _telemetry?.RecordTokenCanceled(this);
        }

        public DmaStreamComputeCommitResult Commit(
            Processor.MainMemoryArea mainMemory,
            DmaStreamComputeOwnerGuardDecision commitGuardDecision,
            MemoryCoherencyObserver? coherencyObserver = null)
        {
            ArgumentNullException.ThrowIfNull(mainMemory);

            if (State == DmaStreamComputeTokenState.Canceled)
            {
                return DmaStreamComputeCommitResult.Canceled();
            }

            if (State == DmaStreamComputeTokenState.Faulted)
            {
                return DmaStreamComputeCommitResult.Faulted(LastFault!);
            }

            EnsureState(DmaStreamComputeTokenState.CommitPending, nameof(Commit));

            if (!TryValidateDescriptorGuards(
                    Descriptor,
                    commitGuardDecision,
                    "DmaStreamCompute token commit",
                    out DmaStreamComputeFaultRecord? guardFault))
            {
                return PublishFault(guardFault!);
            }

            if (!HasExactStagedWriteCoverage(out ulong partialFaultAddress, out string? failureMessage))
            {
                return PublishFault(
                    DmaStreamComputeTokenFaultKind.PartialCompletionFault,
                    failureMessage ?? "DmaStreamCompute commit reached an incomplete staged destination buffer.",
                    partialFaultAddress,
                    isWrite: true);
            }

            if (!TryCommitAllOrNone(mainMemory, out DmaStreamComputeFaultRecord? memoryFault))
            {
                return PublishFault(memoryFault!);
            }

            NotifyCommittedWrites(coherencyObserver);
            State = DmaStreamComputeTokenState.Committed;
            _telemetry?.RecordTokenCommitted(this);
            return DmaStreamComputeCommitResult.Success();
        }

        public DmaStreamComputeCommitResult PublishFault(
            DmaStreamComputeTokenFaultKind faultKind,
            string message,
            ulong faultAddress,
            bool isWrite)
        {
            return PublishFault(CreateFault(faultKind, message, faultAddress, isWrite));
        }

        public DmaStreamComputeCommitResult PublishFault(DmaStreamComputeFaultRecord fault)
        {
            ArgumentNullException.ThrowIfNull(fault);
            LastFault = fault;
            _stagedWrites.Clear();
            State = DmaStreamComputeTokenState.Faulted;
            _telemetry?.RecordTokenFaulted(this, fault.FaultKind);
            return DmaStreamComputeCommitResult.Faulted(fault);
        }

        private static bool IsTelemetryOnlyAdmissionReject(DmaStreamComputeValidationFault fault) =>
            fault is DmaStreamComputeValidationFault.QuotaAdmissionReject
                or DmaStreamComputeValidationFault.BackpressureAdmissionReject
                or DmaStreamComputeValidationFault.TokenCapAdmissionReject;

        private bool TryCommitAllOrNone(
            Processor.MainMemoryArea mainMemory,
            out DmaStreamComputeFaultRecord? fault)
        {
            fault = null;
            var backups = new List<DmaStreamComputeStagedWrite>(_stagedWrites.Count);
            for (int i = 0; i < _stagedWrites.Count; i++)
            {
                DmaStreamComputeStagedWrite stagedWrite = _stagedWrites[i];
                int length = checked((int)stagedWrite.Length);
                if (!HasExactMainMemoryRange(mainMemory, stagedWrite.Address, length))
                {
                    fault = CreateFault(
                        DmaStreamComputeTokenFaultKind.MemoryFault,
                        $"DmaStreamCompute token commit reached out-of-range destination 0x{stagedWrite.Address:X} covering {length} byte(s).",
                        stagedWrite.Address,
                        isWrite: true);
                    return false;
                }

                byte[] backup = new byte[length];
                if (!mainMemory.TryReadPhysicalRange(stagedWrite.Address, backup))
                {
                    fault = CreateFault(
                        DmaStreamComputeTokenFaultKind.MemoryFault,
                        $"DmaStreamCompute token commit could not snapshot destination 0x{stagedWrite.Address:X} before all-or-none commit.",
                        stagedWrite.Address,
                        isWrite: true);
                    return false;
                }

                backups.Add(new DmaStreamComputeStagedWrite(stagedWrite.Address, backup));
            }

            for (int i = 0; i < _stagedWrites.Count; i++)
            {
                DmaStreamComputeStagedWrite stagedWrite = _stagedWrites[i];
                if (mainMemory.TryWritePhysicalRange(stagedWrite.Address, stagedWrite.Data.Span))
                {
                    continue;
                }

                for (int rollbackIndex = i; rollbackIndex >= 0; rollbackIndex--)
                {
                    DmaStreamComputeStagedWrite backup = backups[rollbackIndex];
                    if (!mainMemory.TryWritePhysicalRange(backup.Address, backup.Data.Span))
                    {
                        throw new InvalidOperationException(
                            $"DmaStreamCompute token commit failed to roll back destination 0x{backup.Address:X} after a partial write failure.");
                    }
                }

                fault = CreateFault(
                    DmaStreamComputeTokenFaultKind.MemoryFault,
                    $"DmaStreamCompute token commit failed to write destination 0x{stagedWrite.Address:X}; staged writes were not reported as visible success.",
                    stagedWrite.Address,
                    isWrite: true);
                return false;
            }

            return true;
        }

        private void NotifyCommittedWrites(
            MemoryCoherencyObserver? coherencyObserver)
        {
            if (coherencyObserver is null)
            {
                return;
            }

            ulong ownerDomainTag = Descriptor.OwnerBinding.OwnerDomainTag;
            for (int index = 0; index < _stagedWrites.Count; index++)
            {
                DmaStreamComputeStagedWrite stagedWrite = _stagedWrites[index];
                coherencyObserver.NotifyWrite(
                    new MemoryCoherencyWriteNotification(
                        stagedWrite.Address,
                        stagedWrite.Length,
                        ownerDomainTag,
                        MemoryCoherencyWriteSourceKind.DmaStreamComputeCommit));
            }
        }

        private bool HasExactStagedWriteCoverage(
            out ulong faultAddress,
            out string? failureMessage)
        {
            faultAddress = Descriptor.NormalizedWriteMemoryRanges.Count > 0
                ? Descriptor.NormalizedWriteMemoryRanges[0].Address
                : 0;
            failureMessage = null;

            if (_stagedWrites.Count == 0)
            {
                failureMessage =
                    "DmaStreamCompute all-or-none policy requires a staged write image for every normalized destination range.";
                return false;
            }

            DmaStreamComputeMemoryRange[] stagedRanges = new DmaStreamComputeMemoryRange[_stagedWrites.Count];
            for (int i = 0; i < _stagedWrites.Count; i++)
            {
                stagedRanges[i] = new DmaStreamComputeMemoryRange(
                    _stagedWrites[i].Address,
                    _stagedWrites[i].Length);
            }

            Array.Sort(
                stagedRanges,
                static (left, right) =>
                {
                    int addressCompare = left.Address.CompareTo(right.Address);
                    return addressCompare != 0
                        ? addressCompare
                        : left.Length.CompareTo(right.Length);
                });

            var mergedRanges = new List<DmaStreamComputeMemoryRange>(stagedRanges.Length);
            mergedRanges.Add(stagedRanges[0]);

            for (int i = 1; i < stagedRanges.Length; i++)
            {
                DmaStreamComputeMemoryRange previous = mergedRanges[^1];
                ulong previousEnd = previous.Address + previous.Length;
                if (stagedRanges[i].Address < previousEnd)
                {
                    faultAddress = stagedRanges[i].Address;
                    failureMessage =
                        "DmaStreamCompute staged destination writes overlap; v1 commit policy is all-or-none.";
                    return false;
                }

                if (stagedRanges[i].Address == previousEnd)
                {
                    mergedRanges[^1] = new DmaStreamComputeMemoryRange(
                        previous.Address,
                        previous.Length + stagedRanges[i].Length);
                    continue;
                }

                mergedRanges.Add(stagedRanges[i]);
            }

            if (mergedRanges.Count != Descriptor.NormalizedWriteMemoryRanges.Count)
            {
                failureMessage =
                    "DmaStreamCompute staged destination writes do not cover every normalized descriptor write range.";
                return false;
            }

            for (int i = 0; i < mergedRanges.Count; i++)
            {
                DmaStreamComputeMemoryRange expected = Descriptor.NormalizedWriteMemoryRanges[i];
                if (mergedRanges[i].Address == expected.Address &&
                    mergedRanges[i].Length == expected.Length)
                {
                    continue;
                }

                faultAddress = expected.Address;
                failureMessage =
                    "DmaStreamCompute staged destination writes do not exactly match the normalized descriptor write footprint.";
                return false;
            }

            return true;
        }

        private void EnsureState(
            DmaStreamComputeTokenState expectedState,
            string operation)
        {
            if (State == expectedState)
            {
                return;
            }

            throw new InvalidOperationException(
                $"{operation} requires DmaStreamCompute token state {expectedState}, but token {TokenId} is {State}.");
        }

        private DmaStreamComputeFaultRecord CreateFault(
            DmaStreamComputeTokenFaultKind faultKind,
            string message,
            ulong faultAddress,
            bool isWrite)
        {
            DmaStreamComputeOwnerGuardContext guardContext =
                Descriptor.OwnerGuardDecision.RuntimeOwnerContext;
            return new DmaStreamComputeFaultRecord(
                faultKind,
                message,
                faultAddress,
                isWrite,
                virtualThreadId: Descriptor.OwnerBinding.OwnerVirtualThreadId,
                ownerDomainTag: Descriptor.OwnerBinding.OwnerDomainTag,
                activeDomainCertificate: guardContext.ActiveDomainCertificate);
        }

        private static DmaStreamComputeFaultRecord CreateFaultFromValidationFailure(
            DmaStreamComputeValidationFault validationFault,
            string message)
        {
            DmaStreamComputeTokenFaultKind tokenFault = validationFault switch
            {
                DmaStreamComputeValidationFault.UnsupportedAbiVersion or
                DmaStreamComputeValidationFault.UnsupportedOperation or
                DmaStreamComputeValidationFault.UnsupportedElementType or
                DmaStreamComputeValidationFault.UnsupportedShape
                    => DmaStreamComputeTokenFaultKind.UnsupportedAbiOrOperation,

                DmaStreamComputeValidationFault.AlignmentFault
                    => DmaStreamComputeTokenFaultKind.AlignmentFault,

                DmaStreamComputeValidationFault.AliasOverlapFault
                    => DmaStreamComputeTokenFaultKind.AliasOverlapFault,

                DmaStreamComputeValidationFault.OwnerDomainFault
                    => DmaStreamComputeTokenFaultKind.DomainViolation,

                DmaStreamComputeValidationFault.ExecutionDisabled
                    => DmaStreamComputeTokenFaultKind.ExecutionDisabled,

                _ => DmaStreamComputeTokenFaultKind.DescriptorDecodeFault
            };

            return new DmaStreamComputeFaultRecord(
                tokenFault,
                message,
                faultAddress: 0,
                isWrite: false,
                virtualThreadId: 0,
                ownerDomainTag: 0,
                activeDomainCertificate: 0);
        }

        private static bool TryValidateDescriptorGuards(
            DmaStreamComputeDescriptor descriptor,
            DmaStreamComputeOwnerGuardDecision guardDecision,
            string surface,
            out DmaStreamComputeFaultRecord? fault)
        {
            fault = null;

            if (descriptor.PartialCompletionPolicy != DmaStreamComputePartialCompletionPolicy.AllOrNone)
            {
                fault = CreateDescriptorFault(
                    descriptor,
                    DmaStreamComputeTokenFaultKind.PartialCompletionFault,
                    $"{surface} requires all-or-none partial completion policy.");
                return false;
            }

            if (descriptor.WriteMemoryRanges is null ||
                descriptor.WriteMemoryRanges.Count == 0 ||
                descriptor.NormalizedWriteMemoryRanges is null ||
                descriptor.NormalizedWriteMemoryRanges.Count == 0)
            {
                fault = CreateDescriptorFault(
                    descriptor,
                    DmaStreamComputeTokenFaultKind.PartialCompletionFault,
                    $"{surface} requires a mandatory normalized write footprint before token authority exists.");
                return false;
            }

            if (!guardDecision.IsAllowed)
            {
                fault = CreateGuardFault(
                    descriptor,
                    guardDecision,
                    guardDecision.LegalityDecision.RejectKind == RejectKind.DomainMismatch
                        ? DmaStreamComputeTokenFaultKind.DomainViolation
                        : DmaStreamComputeTokenFaultKind.OwnerContextViolation,
                    $"{surface} requires an allowed owner/domain guard decision. {guardDecision.Message}");
                return false;
            }

            if (guardDecision.LegalityDecision.AuthoritySource != LegalityAuthoritySource.GuardPlane ||
                guardDecision.LegalityDecision.AttemptedReplayCertificateReuse)
            {
                fault = CreateGuardFault(
                    descriptor,
                    guardDecision,
                    DmaStreamComputeTokenFaultKind.OwnerContextViolation,
                    $"{surface} requires guard-plane authority; replay/certificate identity is evidence, not commit authority.");
                return false;
            }

            if (guardDecision.DescriptorOwnerBinding is null ||
                !guardDecision.DescriptorOwnerBinding.Equals(descriptor.OwnerBinding))
            {
                fault = CreateGuardFault(
                    descriptor,
                    guardDecision,
                    DmaStreamComputeTokenFaultKind.OwnerContextViolation,
                    $"{surface} owner binding does not match the accepted descriptor.");
                return false;
            }

            DmaStreamComputeOwnerGuardContext context = guardDecision.RuntimeOwnerContext;
            if (descriptor.OwnerBinding.OwnerVirtualThreadId != context.OwnerVirtualThreadId ||
                descriptor.OwnerBinding.OwnerContextId != context.OwnerContextId ||
                descriptor.OwnerBinding.OwnerCoreId != context.OwnerCoreId ||
                descriptor.OwnerBinding.OwnerPodId != context.OwnerPodId)
            {
                fault = CreateGuardFault(
                    descriptor,
                    guardDecision,
                    DmaStreamComputeTokenFaultKind.OwnerContextViolation,
                    $"{surface} owner/context/core/pod guard changed before token commit.");
                return false;
            }

            if (descriptor.OwnerBinding.DeviceId != DmaStreamComputeDescriptor.CanonicalLane6DeviceId ||
                descriptor.OwnerBinding.DeviceId != context.DeviceId)
            {
                fault = CreateGuardFault(
                    descriptor,
                    guardDecision,
                    DmaStreamComputeTokenFaultKind.DmaDeviceFault,
                    $"{surface} device guard changed before token commit.");
                return false;
            }

            if (descriptor.OwnerBinding.OwnerDomainTag != context.OwnerDomainTag ||
                !IsDomainCoveredByCertificate(context.OwnerDomainTag, context.ActiveDomainCertificate))
            {
                fault = CreateGuardFault(
                    descriptor,
                    guardDecision,
                    DmaStreamComputeTokenFaultKind.DomainViolation,
                    $"{surface} owner/domain guard changed before token commit.");
                return false;
            }

            return true;
        }

        private static bool IsDomainCoveredByCertificate(
            ulong ownerDomainTag,
            ulong activeDomainCertificate)
        {
            if (ownerDomainTag == 0)
            {
                return activeDomainCertificate == 0;
            }

            return activeDomainCertificate == 0 ||
                   (ownerDomainTag & activeDomainCertificate) != 0;
        }

        private static DmaStreamComputeFaultRecord CreateDescriptorFault(
            DmaStreamComputeDescriptor descriptor,
            DmaStreamComputeTokenFaultKind faultKind,
            string message)
        {
            ulong faultAddress = descriptor.NormalizedWriteMemoryRanges is { Count: > 0 }
                ? descriptor.NormalizedWriteMemoryRanges[0].Address
                : descriptor.DescriptorReference.DescriptorAddress;
            return new DmaStreamComputeFaultRecord(
                faultKind,
                message,
                faultAddress,
                isWrite: true,
                virtualThreadId: descriptor.OwnerBinding.OwnerVirtualThreadId,
                ownerDomainTag: descriptor.OwnerBinding.OwnerDomainTag,
                activeDomainCertificate: descriptor.OwnerGuardDecision.RuntimeOwnerContext.ActiveDomainCertificate);
        }

        private static DmaStreamComputeFaultRecord CreateGuardFault(
            DmaStreamComputeDescriptor descriptor,
            DmaStreamComputeOwnerGuardDecision guardDecision,
            DmaStreamComputeTokenFaultKind faultKind,
            string message)
        {
            return new DmaStreamComputeFaultRecord(
                faultKind,
                message,
                descriptor.DescriptorReference.DescriptorAddress,
                isWrite: false,
                virtualThreadId: descriptor.OwnerBinding.OwnerVirtualThreadId,
                ownerDomainTag: descriptor.OwnerBinding.OwnerDomainTag,
                activeDomainCertificate: guardDecision.RuntimeOwnerContext.ActiveDomainCertificate);
        }

        private static bool IsRangeCoveredByFootprint(
            ulong address,
            ulong length,
            IReadOnlyList<DmaStreamComputeMemoryRange> footprint)
        {
            if (length == 0 || footprint is null || footprint.Count == 0)
            {
                return false;
            }

            ulong end = address + length;
            for (int i = 0; i < footprint.Count; i++)
            {
                DmaStreamComputeMemoryRange range = footprint[i];
                ulong rangeEnd = range.Address + range.Length;
                if (address >= range.Address && end <= rangeEnd)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasExactMainMemoryRange(
            Processor.MainMemoryArea mainMemory,
            ulong address,
            int size)
        {
            if (size <= 0)
            {
                return false;
            }

            ulong memoryLength = (ulong)mainMemory.Length;
            ulong requestSize = (ulong)size;
            return requestSize <= memoryLength &&
                   address <= memoryLength - requestSize;
        }
    }
}
