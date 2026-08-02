using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Commit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Telemetry;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Conflicts;

public enum AcceleratorConflictClass : byte
{
    None = 0,
    IncompleteFootprintTruth = 1,
    SubmitReservation = 2,
    CpuStoreOverlapsAcceleratorRead = 3,
    CpuStoreOverlapsAcceleratorWrite = 4,
    CpuLoadOverlapsAcceleratorWrite = 5,
    DmaStreamComputeOverlapsAcceleratorWrite = 6,
    AcceleratorWriteOverlapsSrfWarmedWindow = 7,
    AssistIngressOverlapsAcceleratorWrite = 8,
    AcceleratorWriteWriteOverlap = 9,
    FenceOrSerializingBoundaryWhileTokenActive = 10,
    VmDomainMappingTransitionWhileTokenActive = 11,
    CommitFootprintReservationMissing = 12,
    CommitConflictValidationRejected = 13
}

public enum AcceleratorConflictDecisionKind : byte
{
    Accepted = 0,
    Serialize = 1,
    Rejected = 2,
    Fault = 3
}

public sealed record AcceleratorConflictFault
{
    public AcceleratorConflictFault(
        AcceleratorConflictClass conflictClass,
        AcceleratorTokenFaultCode tokenFaultCode,
        string message,
        ulong faultAddress = 0,
        bool isWrite = false,
        AcceleratorTokenHandle conflictingTokenHandle = default,
        AcceleratorGuardDecision? guardDecision = null)
    {
        if (conflictClass == AcceleratorConflictClass.None)
        {
            throw new ArgumentException(
                "L7-SDC conflict faults require a non-None conflict class.",
                nameof(conflictClass));
        }

        if (tokenFaultCode == AcceleratorTokenFaultCode.None)
        {
            throw new ArgumentException(
                "L7-SDC conflict faults require a non-None token fault code.",
                nameof(tokenFaultCode));
        }

        ConflictClass = conflictClass;
        TokenFaultCode = tokenFaultCode;
        Message = string.IsNullOrWhiteSpace(message)
            ? $"L7-SDC conflict rejected: {conflictClass}."
            : message;
        FaultAddress = faultAddress;
        IsWrite = isWrite;
        ConflictingTokenHandle = conflictingTokenHandle;
        GuardDecision = guardDecision;
    }

    public AcceleratorConflictClass ConflictClass { get; }

    public AcceleratorTokenFaultCode TokenFaultCode { get; }

    public string Message { get; }

    public ulong FaultAddress { get; }

    public bool IsWrite { get; }

    public AcceleratorTokenHandle ConflictingTokenHandle { get; }

    public AcceleratorGuardDecision? GuardDecision { get; }
}

public sealed record AcceleratorConflictDecision
{
    private AcceleratorConflictDecision(
        AcceleratorConflictDecisionKind kind,
        AcceleratorConflictClass conflictClass,
        AcceleratorFootprintReservation? reservation,
        AcceleratorConflictFault? fault,
        AcceleratorGuardDecision? guardDecision,
        string message)
    {
        Kind = kind;
        ConflictClass = conflictClass;
        Reservation = reservation;
        Fault = fault;
        GuardDecision = guardDecision;
        Message = message;
    }

    public AcceleratorConflictDecisionKind Kind { get; }

    public bool IsAccepted => Kind == AcceleratorConflictDecisionKind.Accepted;

    public bool RequiresSerialization => Kind == AcceleratorConflictDecisionKind.Serialize;

    public bool IsRejected => Kind is AcceleratorConflictDecisionKind.Rejected or AcceleratorConflictDecisionKind.Fault;

    public bool ShouldFaultToken => Kind == AcceleratorConflictDecisionKind.Fault;

    public AcceleratorConflictClass ConflictClass { get; }

    public AcceleratorFootprintReservation? Reservation { get; }

    public AcceleratorConflictFault? Fault { get; }

    public AcceleratorTokenFaultCode TokenFaultCode =>
        Fault?.TokenFaultCode ?? AcceleratorTokenFaultCode.None;

    public AcceleratorGuardDecision? GuardDecision { get; }

    public string Message { get; }

    public bool CanPublishArchitecturalMemory => false;

    public bool UserVisiblePublicationAllowed => false;

    public static AcceleratorConflictDecision Accepted(
        AcceleratorConflictClass conflictClass,
        string message,
        AcceleratorFootprintReservation? reservation = null,
        AcceleratorGuardDecision? guardDecision = null) =>
        new(
            AcceleratorConflictDecisionKind.Accepted,
            conflictClass,
            reservation,
            fault: null,
            guardDecision,
            message);

    public static AcceleratorConflictDecision Serialize(
        AcceleratorConflictClass conflictClass,
        AcceleratorFootprintReservation reservation,
        AcceleratorGuardDecision guardDecision,
        AcceleratorMemoryRange conflictRange,
        string message,
        bool isWrite = true) =>
        new(
            AcceleratorConflictDecisionKind.Serialize,
            conflictClass,
            reservation,
            new AcceleratorConflictFault(
                conflictClass,
                AcceleratorTokenFaultCode.ConflictRejected,
                message,
                conflictRange.Address,
                isWrite,
                reservation.TokenHandle,
                guardDecision),
            guardDecision,
            message);

    public static AcceleratorConflictDecision Reject(
        AcceleratorConflictClass conflictClass,
        AcceleratorTokenFaultCode tokenFaultCode,
        string message,
        AcceleratorMemoryRange conflictRange = default,
        bool isWrite = false,
        AcceleratorTokenHandle conflictingTokenHandle = default,
        AcceleratorGuardDecision? guardDecision = null) =>
        new(
            AcceleratorConflictDecisionKind.Rejected,
            conflictClass,
            reservation: null,
            new AcceleratorConflictFault(
                conflictClass,
                tokenFaultCode,
                message,
                conflictRange.Address,
                isWrite,
                conflictingTokenHandle,
                guardDecision),
            guardDecision,
            message);

    public static AcceleratorConflictDecision Faulted(
        AcceleratorConflictClass conflictClass,
        AcceleratorTokenFaultCode tokenFaultCode,
        string message,
        AcceleratorMemoryRange conflictRange = default,
        bool isWrite = false,
        AcceleratorTokenHandle conflictingTokenHandle = default,
        AcceleratorGuardDecision? guardDecision = null) =>
        new(
            AcceleratorConflictDecisionKind.Fault,
            conflictClass,
            reservation: null,
            new AcceleratorConflictFault(
                conflictClass,
                tokenFaultCode,
                message,
                conflictRange.Address,
                isWrite,
                conflictingTokenHandle,
                guardDecision),
            guardDecision,
            message);
}

public sealed record AcceleratorFootprintReservation
{
    internal AcceleratorFootprintReservation(
        AcceleratorToken token,
        AcceleratorGuardDecision reservationGuardDecision)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (!reservationGuardDecision.IsAllowed)
        {
            throw new ArgumentException(
                "L7-SDC footprint reservations require an accepted guard decision.",
                nameof(reservationGuardDecision));
        }

        TokenHandle = token.Handle;
        TokenId = token.TokenId;
        DescriptorIdentityHash = token.Descriptor.Identity.DescriptorIdentityHash;
        NormalizedFootprintHash = token.Descriptor.NormalizedFootprint.Hash;
        OwnerBinding = token.Descriptor.OwnerBinding;
        ReservationGuardDecision = reservationGuardDecision;
        SourceRanges = FreezeRanges(token.Descriptor.NormalizedFootprint.SourceRanges);
        DestinationRanges = FreezeRanges(token.Descriptor.NormalizedFootprint.DestinationRanges);
        ScratchRanges = FreezeRanges(token.Descriptor.NormalizedFootprint.ScratchRanges);
    }

    public AcceleratorTokenHandle TokenHandle { get; }

    public ulong TokenId { get; }

    public ulong DescriptorIdentityHash { get; }

    public ulong NormalizedFootprintHash { get; }

    public AcceleratorOwnerBinding OwnerBinding { get; }

    public AcceleratorGuardDecision ReservationGuardDecision { get; }

    public IReadOnlyList<AcceleratorMemoryRange> SourceRanges { get; }

    public IReadOnlyList<AcceleratorMemoryRange> DestinationRanges { get; }

    public IReadOnlyList<AcceleratorMemoryRange> ScratchRanges { get; }

    public bool HasCompleteFootprintTruth =>
        TokenHandle.IsValid &&
        SourceRanges.Count != 0 &&
        DestinationRanges.Count != 0 &&
        AreRangesWellFormed(SourceRanges) &&
        AreRangesWellFormed(DestinationRanges) &&
        AreRangesWellFormed(ScratchRanges);

    public bool IsBoundTo(AcceleratorToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return TokenHandle.Equals(token.Handle) &&
               DescriptorIdentityHash == token.Descriptor.Identity.DescriptorIdentityHash &&
               NormalizedFootprintHash == token.Descriptor.NormalizedFootprint.Hash &&
               OwnerBinding.Equals(token.Descriptor.OwnerBinding);
    }

    public bool SourceOverlaps(AcceleratorMemoryRange range, out AcceleratorMemoryRange overlap) =>
        FirstOverlap(SourceRanges, range, out overlap);

    public bool DestinationOverlaps(AcceleratorMemoryRange range, out AcceleratorMemoryRange overlap) =>
        FirstOverlap(DestinationRanges, range, out overlap);

    internal static bool FirstOverlap(
        IReadOnlyList<AcceleratorMemoryRange> ranges,
        AcceleratorMemoryRange probe,
        out AcceleratorMemoryRange overlap)
    {
        overlap = default;
        if (!IsRangeWellFormed(probe))
        {
            return false;
        }

        for (int index = 0; index < ranges.Count; index++)
        {
            AcceleratorMemoryRange range = ranges[index];
            if (!IsRangeWellFormed(range) ||
                !RangesOverlap(range, probe))
            {
                continue;
            }

            ulong start = Math.Max(range.Address, probe.Address);
            ulong end = Math.Min(range.Address + range.Length, probe.Address + probe.Length);
            overlap = new AcceleratorMemoryRange(start, end - start);
            return true;
        }

        return false;
    }

    internal static bool RangesOverlap(
        AcceleratorMemoryRange left,
        AcceleratorMemoryRange right)
    {
        if (!IsRangeWellFormed(left) || !IsRangeWellFormed(right))
        {
            return false;
        }

        ulong leftEnd = left.Address + left.Length;
        ulong rightEnd = right.Address + right.Length;
        return left.Address < rightEnd && right.Address < leftEnd;
    }

    internal static bool IsRangeWellFormed(AcceleratorMemoryRange range) =>
        range.Length != 0 &&
        range.Address <= ulong.MaxValue - range.Length;

    internal static bool AreRangesWellFormed(IReadOnlyList<AcceleratorMemoryRange> ranges)
    {
        if (ranges is null)
        {
            return false;
        }

        for (int index = 0; index < ranges.Count; index++)
        {
            if (!IsRangeWellFormed(ranges[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<AcceleratorMemoryRange> FreezeRanges(
        IReadOnlyList<AcceleratorMemoryRange>? ranges)
    {
        if (ranges is null || ranges.Count == 0)
        {
            return Array.Empty<AcceleratorMemoryRange>();
        }

        var copy = new AcceleratorMemoryRange[ranges.Count];
        for (int index = 0; index < ranges.Count; index++)
        {
            copy[index] = ranges[index];
        }

        return Array.AsReadOnly(copy);
    }
}

public sealed class AcceleratorActiveFootprintTable
{
    private readonly Dictionary<ulong, AcceleratorFootprintReservation> _reservations = new();

    public int Count => _reservations.Count;

    public IReadOnlyList<AcceleratorFootprintReservation> ActiveReservations
    {
        get
        {
            var reservations = new AcceleratorFootprintReservation[_reservations.Count];
            _reservations.Values.CopyTo(reservations, 0);
            return Array.AsReadOnly(reservations);
        }
    }

    public bool TryGet(
        AcceleratorTokenHandle handle,
        out AcceleratorFootprintReservation? reservation)
    {
        if (!handle.IsValid)
        {
            reservation = null;
            return false;
        }

        return _reservations.TryGetValue(handle.Value, out reservation);
    }

    public AcceleratorConflictDecision TryReserve(
        AcceleratorFootprintReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        if (!reservation.HasCompleteFootprintTruth)
        {
            return AcceleratorConflictDecision.Reject(
                AcceleratorConflictClass.IncompleteFootprintTruth,
                AcceleratorTokenFaultCode.ConflictRejected,
                "L7-SDC active footprint reservation rejected because normalized footprint truth is incomplete.",
                conflictingTokenHandle: reservation.TokenHandle,
                guardDecision: reservation.ReservationGuardDecision);
        }

        if (_reservations.ContainsKey(reservation.TokenHandle.Value))
        {
            AcceleratorFootprintReservation existing = _reservations[reservation.TokenHandle.Value];
            if (existing.TokenId == reservation.TokenId &&
                existing.DescriptorIdentityHash == reservation.DescriptorIdentityHash &&
                existing.NormalizedFootprintHash == reservation.NormalizedFootprintHash &&
                existing.OwnerBinding.Equals(reservation.OwnerBinding))
            {
                return AcceleratorConflictDecision.Accepted(
                    AcceleratorConflictClass.SubmitReservation,
                    "L7-SDC active footprint reservation already exists for the guarded token.",
                    reservation,
                    reservation.ReservationGuardDecision);
            }

            return AcceleratorConflictDecision.Reject(
                AcceleratorConflictClass.AcceleratorWriteWriteOverlap,
                AcceleratorTokenFaultCode.TokenHandleNotAuthority,
                "L7-SDC active footprint table observed an opaque handle collision with different descriptor or footprint evidence; token handle identity is not conflict authority.",
                FirstRangeOrDefault(existing.DestinationRanges),
                isWrite: true,
                existing.TokenHandle,
                reservation.ReservationGuardDecision);
        }

        foreach (AcceleratorFootprintReservation active in _reservations.Values)
        {
            if (FirstWriteWriteOverlap(
                    active,
                    reservation,
                    out AcceleratorMemoryRange overlap))
            {
                return AcceleratorConflictDecision.Reject(
                    AcceleratorConflictClass.AcceleratorWriteWriteOverlap,
                    AcceleratorTokenFaultCode.ConflictRejected,
                    "L7-SDC submit reservation rejected because two active accelerator tokens would write the same region.",
                    overlap,
                    isWrite: true,
                    active.TokenHandle,
                    reservation.ReservationGuardDecision);
            }
        }

        _reservations.Add(reservation.TokenHandle.Value, reservation);
        return AcceleratorConflictDecision.Accepted(
            AcceleratorConflictClass.SubmitReservation,
            "L7-SDC active footprint reservation accepted after guard-backed conflict validation.",
            reservation,
            reservation.ReservationGuardDecision);
    }

    public bool Release(AcceleratorTokenHandle handle) =>
        handle.IsValid && _reservations.Remove(handle.Value);

    internal AcceleratorConflictDecision FirstActiveDestinationOverlap(
        AcceleratorMemoryRange range,
        AcceleratorConflictClass conflictClass,
        string message,
        Func<AcceleratorFootprintReservation, AcceleratorGuardDecision?> guardSelector)
    {
        foreach (AcceleratorFootprintReservation reservation in _reservations.Values)
        {
            if (!reservation.DestinationOverlaps(range, out AcceleratorMemoryRange overlap))
            {
                continue;
            }

            AcceleratorGuardDecision? guardDecision = guardSelector(reservation);
            if (guardDecision.HasValue && !guardDecision.Value.IsAllowed)
            {
                return AcceleratorConflictDecision.Reject(
                    AcceleratorConflictClass.VmDomainMappingTransitionWhileTokenActive,
                    AcceleratorTokenStore.MapGuardFault(guardDecision.Value.Fault),
                    "L7-SDC active conflict notification observed owner/domain or epoch drift before overlap handling. " +
                    guardDecision.Value.Message,
                    overlap,
                    isWrite: true,
                    reservation.TokenHandle,
                    guardDecision);
            }

            return AcceleratorConflictDecision.Serialize(
                conflictClass,
                reservation,
                guardDecision ?? reservation.ReservationGuardDecision,
                overlap,
                message,
                isWrite: true);
        }

        return AcceleratorConflictDecision.Accepted(
            AcceleratorConflictClass.None,
            "L7-SDC conflict table observed no active accelerator destination overlap.");
    }

    private static bool FirstWriteWriteOverlap(
        AcceleratorFootprintReservation active,
        AcceleratorFootprintReservation incoming,
        out AcceleratorMemoryRange overlap)
    {
        overlap = default;
        for (int index = 0; index < incoming.DestinationRanges.Count; index++)
        {
            if (AcceleratorFootprintReservation.FirstOverlap(
                    active.DestinationRanges,
                    incoming.DestinationRanges[index],
                    out overlap))
            {
                return true;
            }
        }

        return false;
    }

    private static AcceleratorMemoryRange FirstRangeOrDefault(
        IReadOnlyList<AcceleratorMemoryRange> ranges) =>
        ranges is { Count: > 0 }
            ? ranges[0]
            : default;
}

public sealed class ExternalAcceleratorConflictManager
{
    private readonly AcceleratorActiveFootprintTable _activeFootprints = new();
    private readonly List<AcceleratorMemoryRange> _srfWarmWindows = new();
    private readonly List<AcceleratorMemoryRange> _assistIngressWindows = new();
    private readonly AcceleratorTelemetry? _telemetry;

    public ExternalAcceleratorConflictManager(AcceleratorTelemetry? telemetry = null)
    {
        _telemetry = telemetry;
    }

    public AcceleratorActiveFootprintTable ActiveFootprints => _activeFootprints;

    public int ActiveReservationCount => _activeFootprints.Count;

    public int SrfWarmWindowCount => _srfWarmWindows.Count;

    public int AssistIngressWindowCount => _assistIngressWindows.Count;

    public AcceleratorConflictDecision TryReserveOnSubmit(
        AcceleratorToken token,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(token);

        AcceleratorGuardDecision guardDecision =
            ValidateTokenAndDescriptorGuard(
                token,
                currentGuardEvidence,
                AcceleratorGuardSurface.SubmitAdmission);
        if (!guardDecision.IsAllowed)
        {
            return RecordConflictDecision(
                AcceleratorConflictDecision.Reject(
                    AcceleratorConflictClass.SubmitReservation,
                    AcceleratorTokenStore.MapGuardFault(guardDecision.Fault),
                    "L7-SDC submit footprint reservation requires fresh owner/domain and mapping/IOMMU authority. " +
                    guardDecision.Message,
                    guardDecision: guardDecision));
        }

        var reservation = new AcceleratorFootprintReservation(token, guardDecision);
        if (!reservation.HasCompleteFootprintTruth)
        {
            return RecordConflictDecision(
                AcceleratorConflictDecision.Reject(
                    AcceleratorConflictClass.IncompleteFootprintTruth,
                    AcceleratorTokenFaultCode.ConflictRejected,
                    "L7-SDC submit footprint reservation rejected because descriptor footprint truth is incomplete.",
                    conflictingTokenHandle: token.Handle,
                    guardDecision: guardDecision));
        }

        if (FirstWindowOverlap(
                reservation.DestinationRanges,
                _srfWarmWindows,
                out AcceleratorMemoryRange srfOverlap))
        {
            return RecordConflictDecision(
                AcceleratorConflictDecision.Reject(
                    AcceleratorConflictClass.AcceleratorWriteOverlapsSrfWarmedWindow,
                    AcceleratorTokenFaultCode.ConflictRejected,
                    "L7-SDC submit rejected because accelerator writes overlap an SRF warmed window; v1 does not relax this dependency.",
                    srfOverlap,
                    isWrite: true,
                    token.Handle,
                    guardDecision));
        }

        if (FirstWindowOverlap(
                reservation.DestinationRanges,
                _assistIngressWindows,
                out AcceleratorMemoryRange assistOverlap))
        {
            return RecordConflictDecision(
                AcceleratorConflictDecision.Reject(
                    AcceleratorConflictClass.AssistIngressOverlapsAcceleratorWrite,
                    AcceleratorTokenFaultCode.ConflictRejected,
                    "L7-SDC submit rejected because accelerator writes overlap assist ingress warm evidence; assist evidence is not device authority.",
                    assistOverlap,
                    isWrite: true,
                    token.Handle,
                    guardDecision));
        }

        return RecordConflictDecision(_activeFootprints.TryReserve(reservation));
    }

    public AcceleratorConflictDecision NotifyCpuLoad(
        AcceleratorMemoryRange loadRange,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        if (!AcceleratorFootprintReservation.IsRangeWellFormed(loadRange))
        {
            return RecordConflictDecision(
                RejectIncompleteRange(
                    AcceleratorConflictClass.CpuLoadOverlapsAcceleratorWrite,
                    loadRange,
                    isWrite: false));
        }

        return RecordConflictDecision(
            _activeFootprints.FirstActiveDestinationOverlap(
                loadRange,
                AcceleratorConflictClass.CpuLoadOverlapsAcceleratorWrite,
                "CPU load overlaps an active accelerator write footprint; v1 requires serialization or rejection before the load observes memory.",
                reservation => ValidateReservationGuard(reservation, currentGuardEvidence)));
    }

    public AcceleratorConflictDecision NotifyCpuStore(
        AcceleratorMemoryRange storeRange,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        if (!AcceleratorFootprintReservation.IsRangeWellFormed(storeRange))
        {
            return RecordConflictDecision(
                RejectIncompleteRange(
                    AcceleratorConflictClass.CpuStoreOverlapsAcceleratorWrite,
                    storeRange,
                    isWrite: true));
        }

        foreach (AcceleratorFootprintReservation reservation in _activeFootprints.ActiveReservations)
        {
            AcceleratorGuardDecision guardDecision =
                ValidateReservationGuard(reservation, currentGuardEvidence);
            if (!guardDecision.IsAllowed)
            {
                return RecordConflictDecision(
                    RejectDrift(
                        reservation,
                        guardDecision,
                        storeRange));
            }

            if (reservation.DestinationOverlaps(storeRange, out AcceleratorMemoryRange writeOverlap))
            {
                return RecordConflictDecision(
                    AcceleratorConflictDecision.Serialize(
                        AcceleratorConflictClass.CpuStoreOverlapsAcceleratorWrite,
                        reservation,
                        guardDecision,
                        writeOverlap,
                        "CPU store overlaps an active accelerator write footprint; v1 requires serialization or rejection before either write becomes visible."));
            }

            if (reservation.SourceOverlaps(storeRange, out AcceleratorMemoryRange readOverlap))
            {
                return RecordConflictDecision(
                    AcceleratorConflictDecision.Serialize(
                        AcceleratorConflictClass.CpuStoreOverlapsAcceleratorRead,
                        reservation,
                        guardDecision,
                        readOverlap,
                        "CPU store overlaps an active accelerator read footprint; v1 requires serialization or rejection before the source image can drift.",
                        isWrite: true));
            }
        }

        return AcceleratorConflictDecision.Accepted(
            AcceleratorConflictClass.None,
            "CPU store observed no active L7-SDC source/destination overlap.");
    }

    public AcceleratorConflictDecision NotifyDmaStreamComputeAdmission(
        IReadOnlyList<AcceleratorMemoryRange> readRanges,
        IReadOnlyList<AcceleratorMemoryRange> writeRanges,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        if (!AreRangesWellFormed(readRanges) ||
            !AreRangesWellFormed(writeRanges))
        {
            return RecordConflictDecision(
                AcceleratorConflictDecision.Reject(
                    AcceleratorConflictClass.IncompleteFootprintTruth,
                    AcceleratorTokenFaultCode.ConflictRejected,
                    "DmaStreamCompute admission conflict notification requires complete normalized read/write footprint evidence."));
        }

        foreach (AcceleratorFootprintReservation reservation in _activeFootprints.ActiveReservations)
        {
            AcceleratorGuardDecision guardDecision =
                ValidateReservationGuard(reservation, currentGuardEvidence);
            if (!guardDecision.IsAllowed)
            {
                return RecordConflictDecision(
                    RejectDrift(
                        reservation,
                        guardDecision,
                        FirstRangeOrDefault(writeRanges)));
            }

            if (FirstWindowOverlap(
                    reservation.DestinationRanges,
                    readRanges,
                    out AcceleratorMemoryRange readOverlap) ||
                FirstWindowOverlap(
                    reservation.DestinationRanges,
                    writeRanges,
                    out readOverlap))
            {
                return RecordConflictDecision(
                    AcceleratorConflictDecision.Reject(
                        AcceleratorConflictClass.DmaStreamComputeOverlapsAcceleratorWrite,
                        AcceleratorTokenFaultCode.ConflictRejected,
                        "DmaStreamCompute lane6 admission overlaps an active L7-SDC accelerator write footprint; v1 rejects one side and does not route either contour as fallback.",
                        readOverlap,
                        isWrite: true,
                        reservation.TokenHandle,
                        guardDecision));
            }
        }

        return AcceleratorConflictDecision.Accepted(
            AcceleratorConflictClass.None,
            "DmaStreamCompute admission observed no active L7-SDC accelerator write overlap.");
    }

    public AcceleratorConflictDecision NotifySrfWarmWindow(
        AcceleratorMemoryRange warmedWindow,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        if (!AcceleratorFootprintReservation.IsRangeWellFormed(warmedWindow))
        {
            return RecordConflictDecision(
                RejectIncompleteRange(
                    AcceleratorConflictClass.AcceleratorWriteOverlapsSrfWarmedWindow,
                    warmedWindow,
                    isWrite: false));
        }

        AcceleratorConflictDecision activeOverlap =
            _activeFootprints.FirstActiveDestinationOverlap(
                warmedWindow,
                AcceleratorConflictClass.AcceleratorWriteOverlapsSrfWarmedWindow,
                "SRF warm window overlaps an active accelerator write footprint; v1 rejects the warm/accelerator overlap instead of treating warm evidence as authority.",
                reservation => ValidateReservationGuard(reservation, currentGuardEvidence));
        if (!activeOverlap.IsAccepted)
        {
            return RecordConflictDecision(
                AcceleratorConflictDecision.Reject(
                    AcceleratorConflictClass.AcceleratorWriteOverlapsSrfWarmedWindow,
                    activeOverlap.TokenFaultCode == AcceleratorTokenFaultCode.None
                        ? AcceleratorTokenFaultCode.ConflictRejected
                        : activeOverlap.TokenFaultCode,
                    activeOverlap.Message,
                    activeOverlap.Fault is null
                        ? warmedWindow
                        : new AcceleratorMemoryRange(activeOverlap.Fault.FaultAddress, warmedWindow.Length),
                    isWrite: true,
                    activeOverlap.Fault?.ConflictingTokenHandle ?? default,
                    activeOverlap.GuardDecision));
        }

        _srfWarmWindows.Add(warmedWindow);
        return AcceleratorConflictDecision.Accepted(
            AcceleratorConflictClass.AcceleratorWriteOverlapsSrfWarmedWindow,
            "SRF warm window recorded as non-authoritative conflict evidence for future L7-SDC submit checks.");
    }

    public AcceleratorConflictDecision NotifyAssistIngressWindow(
        AcceleratorMemoryRange ingressWindow,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        if (!AcceleratorFootprintReservation.IsRangeWellFormed(ingressWindow))
        {
            return RecordConflictDecision(
                RejectIncompleteRange(
                    AcceleratorConflictClass.AssistIngressOverlapsAcceleratorWrite,
                    ingressWindow,
                    isWrite: false));
        }

        AcceleratorConflictDecision activeOverlap =
            _activeFootprints.FirstActiveDestinationOverlap(
                ingressWindow,
                AcceleratorConflictClass.AssistIngressOverlapsAcceleratorWrite,
                "Assist ingress window overlaps an active accelerator write footprint; v1 rejects the overlap and assist evidence remains non-authoritative.",
                reservation => ValidateReservationGuard(reservation, currentGuardEvidence));
        if (!activeOverlap.IsAccepted)
        {
            return RecordConflictDecision(
                AcceleratorConflictDecision.Reject(
                    AcceleratorConflictClass.AssistIngressOverlapsAcceleratorWrite,
                    activeOverlap.TokenFaultCode == AcceleratorTokenFaultCode.None
                        ? AcceleratorTokenFaultCode.ConflictRejected
                        : activeOverlap.TokenFaultCode,
                    activeOverlap.Message,
                    activeOverlap.Fault is null
                        ? ingressWindow
                        : new AcceleratorMemoryRange(activeOverlap.Fault.FaultAddress, ingressWindow.Length),
                    isWrite: true,
                    activeOverlap.Fault?.ConflictingTokenHandle ?? default,
                    activeOverlap.GuardDecision));
        }

        _assistIngressWindows.Add(ingressWindow);
        return AcceleratorConflictDecision.Accepted(
            AcceleratorConflictClass.AssistIngressOverlapsAcceleratorWrite,
            "Assist ingress window recorded as non-authoritative conflict evidence for future L7-SDC submit checks.");
    }

    public AcceleratorConflictDecision ValidateBeforeCommit(
        AcceleratorToken token,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(token);

        AcceleratorGuardDecision guardDecision =
            ValidateTokenAndDescriptorGuard(
                token,
                currentGuardEvidence,
                AcceleratorGuardSurface.Commit);
        if (!guardDecision.IsAllowed)
        {
            return RecordConflictDecision(
                AcceleratorConflictDecision.Faulted(
                    AcceleratorConflictClass.CommitConflictValidationRejected,
                    AcceleratorTokenStore.MapGuardFault(guardDecision.Fault),
                    "L7-SDC commit conflict validation requires fresh owner/domain and mapping/IOMMU authority. " +
                    guardDecision.Message,
                    FirstRangeOrDefault(token.Descriptor.NormalizedFootprint.DestinationRanges),
                    isWrite: true,
                    token.Handle,
                    guardDecision));
        }

        if (!_activeFootprints.TryGet(
                token.Handle,
                out AcceleratorFootprintReservation? reservation) ||
            reservation is null)
        {
            return RecordConflictDecision(
                AcceleratorConflictDecision.Faulted(
                    AcceleratorConflictClass.CommitFootprintReservationMissing,
                    AcceleratorTokenFaultCode.CommitConflictRejected,
                    "L7-SDC commit rejected because the conflict manager has no active footprint reservation for the token; conflict success is not commit authority.",
                    FirstRangeOrDefault(token.Descriptor.NormalizedFootprint.DestinationRanges),
                    isWrite: true,
                    token.Handle,
                    guardDecision));
        }

        if (!reservation.IsBoundTo(token))
        {
            return RecordConflictDecision(
                AcceleratorConflictDecision.Faulted(
                    AcceleratorConflictClass.CommitConflictValidationRejected,
                    AcceleratorTokenFaultCode.CommitConflictRejected,
                    "L7-SDC commit rejected because active footprint truth no longer matches the token-bound descriptor identity and footprint hash.",
                    FirstRangeOrDefault(token.Descriptor.NormalizedFootprint.DestinationRanges),
                    isWrite: true,
                    token.Handle,
                    guardDecision));
        }

        if (FirstWindowOverlap(
                reservation.DestinationRanges,
                _srfWarmWindows,
                out AcceleratorMemoryRange srfOverlap))
        {
            return RecordConflictDecision(
                AcceleratorConflictDecision.Faulted(
                    AcceleratorConflictClass.AcceleratorWriteOverlapsSrfWarmedWindow,
                    AcceleratorTokenFaultCode.CommitConflictRejected,
                    "L7-SDC commit rejected because a recorded SRF warmed window overlaps the token write footprint.",
                    srfOverlap,
                    isWrite: true,
                    token.Handle,
                    guardDecision));
        }

        if (FirstWindowOverlap(
                reservation.DestinationRanges,
                _assistIngressWindows,
                out AcceleratorMemoryRange assistOverlap))
        {
            return RecordConflictDecision(
                AcceleratorConflictDecision.Faulted(
                    AcceleratorConflictClass.AssistIngressOverlapsAcceleratorWrite,
                    AcceleratorTokenFaultCode.CommitConflictRejected,
                    "L7-SDC commit rejected because a recorded assist ingress window overlaps the token write footprint.",
                    assistOverlap,
                    isWrite: true,
                    token.Handle,
                    guardDecision));
        }

        return AcceleratorConflictDecision.Accepted(
            AcceleratorConflictClass.CommitConflictValidationRejected,
            "L7-SDC conflict manager validated the active footprint before commit; Phase 08 coordinator preconditions still own publication authority.",
            reservation,
            guardDecision);
    }

    public AcceleratorConflictDecision ReleaseTokenFootprint(
        AcceleratorToken token,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(token);

        AcceleratorGuardDecision guardDecision =
            ValidateTokenAndDescriptorGuard(
                token,
                currentGuardEvidence,
                AcceleratorGuardSurface.MappingEpochValidation);
        if (!guardDecision.IsAllowed)
        {
            return RecordConflictDecision(AcceleratorConflictDecision.Reject(
                AcceleratorConflictClass.VmDomainMappingTransitionWhileTokenActive,
                AcceleratorTokenStore.MapGuardFault(guardDecision.Fault),
                "L7-SDC footprint release requires current guard-plane owner/domain and epoch authority. " +
                guardDecision.Message,
                FirstRangeOrDefault(token.Descriptor.NormalizedFootprint.DestinationRanges),
                isWrite: true,
                token.Handle,
                guardDecision));
        }

        bool released = _activeFootprints.Release(token.Handle);
        return AcceleratorConflictDecision.Accepted(
            AcceleratorConflictClass.None,
            released
                ? "L7-SDC active footprint reservation released after guarded token resolution."
                : "L7-SDC active footprint release observed no reservation; no authority was inferred from the token handle.",
            guardDecision: guardDecision);
    }

    public AcceleratorConflictDecision InvalidateSrfCacheOnCommit(
        AcceleratorToken token,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(token);

        if (token.State != AcceleratorTokenState.Committed)
        {
            return RecordConflictDecision(AcceleratorConflictDecision.Reject(
                AcceleratorConflictClass.CommitConflictValidationRejected,
                AcceleratorTokenFaultCode.CommitCoordinatorRequired,
                $"L7-SDC conflict-manager SRF/cache cleanup requires a Phase 08 committed token, but token is {token.State}.",
                FirstRangeOrDefault(token.Descriptor.NormalizedFootprint.DestinationRanges),
                isWrite: true,
                token.Handle));
        }

        AcceleratorGuardDecision guardDecision =
            ValidateTokenAndDescriptorGuard(
                token,
                currentGuardEvidence,
                AcceleratorGuardSurface.Commit);
        if (!guardDecision.IsAllowed)
        {
            return RecordConflictDecision(AcceleratorConflictDecision.Reject(
                AcceleratorConflictClass.CommitConflictValidationRejected,
                AcceleratorTokenStore.MapGuardFault(guardDecision.Fault),
                "L7-SDC conflict-manager SRF/cache cleanup requires fresh guard authority and cannot replace Phase 08 invalidation. " +
                guardDecision.Message,
                FirstRangeOrDefault(token.Descriptor.NormalizedFootprint.DestinationRanges),
                isWrite: true,
                token.Handle,
                guardDecision));
        }

        RemoveOverlappingWindows(_srfWarmWindows, token.Descriptor.NormalizedFootprint.DestinationRanges);
        RemoveOverlappingWindows(_assistIngressWindows, token.Descriptor.NormalizedFootprint.DestinationRanges);
        return AcceleratorConflictDecision.Accepted(
            AcceleratorConflictClass.None,
            "L7-SDC conflict-manager warmed-window evidence was retired after Phase 08 commit invalidation boundary completed.",
            guardDecision: guardDecision);
    }

    public AcceleratorConflictDecision NotifySerializingBoundary(
        AcceleratorToken token,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(token);

        if (!_activeFootprints.TryGet(
                token.Handle,
                out AcceleratorFootprintReservation? reservation) ||
            reservation is null ||
            token.IsTerminal)
        {
            return AcceleratorConflictDecision.Accepted(
                AcceleratorConflictClass.FenceOrSerializingBoundaryWhileTokenActive,
                "Serializing boundary observed no active L7-SDC footprint reservation for the scoped token.");
        }

        AcceleratorGuardDecision guardDecision =
            ValidateReservationGuard(reservation, currentGuardEvidence);
        if (!guardDecision.IsAllowed)
        {
            return RecordConflictDecision(RejectDrift(
                reservation,
                guardDecision,
                FirstRangeOrDefault(reservation.DestinationRanges)));
        }

        return RecordConflictDecision(AcceleratorConflictDecision.Reject(
            AcceleratorConflictClass.FenceOrSerializingBoundaryWhileTokenActive,
            AcceleratorTokenFaultCode.FenceRejected,
            "Serializing boundary reached an active L7-SDC footprint; v1 requires guarded drain, cancel, fault, or conservative rejection.",
            FirstRangeOrDefault(reservation.DestinationRanges),
            isWrite: true,
            token.Handle,
            guardDecision));
    }

    public AcceleratorConflictDecision NotifyVmDomainOrMappingTransition(
        AcceleratorToken token,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(token);

        if (!_activeFootprints.TryGet(
                token.Handle,
                out AcceleratorFootprintReservation? reservation) ||
            reservation is null ||
            token.IsTerminal)
        {
            return AcceleratorConflictDecision.Accepted(
                AcceleratorConflictClass.VmDomainMappingTransitionWhileTokenActive,
                "VM/domain/mapping transition observed no active L7-SDC footprint reservation for the scoped token.");
        }

        AcceleratorGuardDecision guardDecision =
            ValidateReservationGuard(reservation, currentGuardEvidence);
        if (!guardDecision.IsAllowed)
        {
            return RecordConflictDecision(AcceleratorConflictDecision.Faulted(
                AcceleratorConflictClass.VmDomainMappingTransitionWhileTokenActive,
                AcceleratorTokenStore.MapGuardFault(guardDecision.Fault),
                "VM/domain/mapping transition invalidated an active L7-SDC token footprint; commit must be blocked by Phase 08 guard validation. " +
                guardDecision.Message,
                FirstRangeOrDefault(reservation.DestinationRanges),
                isWrite: true,
                token.Handle,
                guardDecision));
        }

        return AcceleratorConflictDecision.Serialize(
            AcceleratorConflictClass.VmDomainMappingTransitionWhileTokenActive,
            reservation,
            guardDecision,
            FirstRangeOrDefault(reservation.DestinationRanges),
            "VM/domain/mapping transition encountered an active L7-SDC token; v1 requires serialization, drain, cancel, or fault before authority changes.");
    }

    private static AcceleratorConflictDecision RejectIncompleteRange(
        AcceleratorConflictClass conflictClass,
        AcceleratorMemoryRange range,
        bool isWrite) =>
        AcceleratorConflictDecision.Reject(
            conflictClass,
            AcceleratorTokenFaultCode.ConflictRejected,
            "L7-SDC conflict notification rejected because the memory range is zero length or overflows.",
            range,
            isWrite);

    private AcceleratorConflictDecision RecordConflictDecision(
        AcceleratorConflictDecision decision)
    {
        if (decision.IsRejected)
        {
            _telemetry?.RecordFootprintConflictReject(
                decision.ConflictClass,
                decision.TokenFaultCode == AcceleratorTokenFaultCode.None
                    ? AcceleratorTokenFaultCode.ConflictRejected
                    : decision.TokenFaultCode,
                decision.Message);
            _telemetry?.RecordGuardReject(
                decision.GuardDecision,
                decision.Message);
        }

        return decision;
    }

    private AcceleratorGuardDecision ValidateTokenAndDescriptorGuard(
        AcceleratorToken token,
        AcceleratorGuardEvidence? currentGuardEvidence,
        AcceleratorGuardSurface surface)
    {
        AcceleratorGuardDecision tokenGuardDecision =
            AcceleratorOwnerDomainGuard.Default.ValidateMappingEpoch(
                token.SubmitGuardDecision,
                currentGuardEvidence);
        if (!tokenGuardDecision.IsAllowed)
        {
            return tokenGuardDecision;
        }

        AcceleratorGuardDecision descriptorDecision = surface switch
        {
            AcceleratorGuardSurface.Commit =>
                AcceleratorOwnerDomainGuard.Default.EnsureBeforeCommit(
                    token.Descriptor,
                    currentGuardEvidence),
            AcceleratorGuardSurface.SubmitAdmission =>
                AcceleratorOwnerDomainGuard.Default.EnsureBeforeSubmit(
                    token.Descriptor,
                    currentGuardEvidence),
            _ =>
                AcceleratorOwnerDomainGuard.Default.ValidateMappingEpoch(
                    token.SubmitGuardDecision,
                    currentGuardEvidence)
        };
        if (!descriptorDecision.IsAllowed)
        {
            return descriptorDecision;
        }

        if (tokenGuardDecision.DescriptorOwnerBinding is null ||
            descriptorDecision.DescriptorOwnerBinding is null ||
            !tokenGuardDecision.DescriptorOwnerBinding.Equals(descriptorDecision.DescriptorOwnerBinding))
        {
            return AcceleratorGuardDecision.Reject(
                surface,
                AcceleratorGuardFault.DescriptorOwnerBindingMismatch,
                token.Descriptor.OwnerBinding,
                currentGuardEvidence,
                RejectKind.OwnerMismatch,
                "L7-SDC conflict manager token guard and descriptor guard bind different owners.");
        }

        return descriptorDecision;
    }

    private static AcceleratorGuardDecision ValidateReservationGuard(
        AcceleratorFootprintReservation reservation,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        return AcceleratorOwnerDomainGuard.Default.ValidateMappingEpoch(
            reservation.ReservationGuardDecision,
            currentGuardEvidence);
    }

    private static AcceleratorConflictDecision RejectDrift(
        AcceleratorFootprintReservation reservation,
        AcceleratorGuardDecision guardDecision,
        AcceleratorMemoryRange faultRange) =>
        AcceleratorConflictDecision.Reject(
            AcceleratorConflictClass.VmDomainMappingTransitionWhileTokenActive,
            AcceleratorTokenStore.MapGuardFault(guardDecision.Fault),
            "L7-SDC conflict notification observed active-token owner/domain or epoch drift before overlap resolution. " +
            guardDecision.Message,
            faultRange,
            isWrite: true,
            reservation.TokenHandle,
            guardDecision);

    private static bool AreRangesWellFormed(IReadOnlyList<AcceleratorMemoryRange>? ranges)
    {
        return ranges is { Count: > 0 } &&
               AcceleratorFootprintReservation.AreRangesWellFormed(ranges);
    }

    private static bool FirstWindowOverlap(
        IReadOnlyList<AcceleratorMemoryRange> leftRanges,
        IReadOnlyList<AcceleratorMemoryRange> rightRanges,
        out AcceleratorMemoryRange overlap)
    {
        overlap = default;
        if (leftRanges.Count == 0 || rightRanges.Count == 0)
        {
            return false;
        }

        for (int index = 0; index < rightRanges.Count; index++)
        {
            if (AcceleratorFootprintReservation.FirstOverlap(
                    leftRanges,
                    rightRanges[index],
                    out overlap))
            {
                return true;
            }
        }

        return false;
    }

    private static AcceleratorMemoryRange FirstRangeOrDefault(
        IReadOnlyList<AcceleratorMemoryRange> ranges) =>
        ranges is { Count: > 0 }
            ? ranges[0]
            : default;

    private static void RemoveOverlappingWindows(
        List<AcceleratorMemoryRange> windows,
        IReadOnlyList<AcceleratorMemoryRange> committedRanges)
    {
        for (int index = windows.Count - 1; index >= 0; index--)
        {
            AcceleratorMemoryRange window = windows[index];
            if (AcceleratorFootprintReservation.FirstOverlap(
                    committedRanges,
                    window,
                    out _))
            {
                windows.RemoveAt(index);
            }
        }
    }
}
