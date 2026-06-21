using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace YAKSys_Hybrid_CPU.Core.Execution.MemoryOrdering;

public enum GlobalMemoryConflictServiceMode : byte
{
    Absent = 0,
    PresentPassive = 1,
    PresentEnforcing = 2
}

public enum GlobalMemoryParticipantKind : byte
{
    CpuLoad = 0,
    CpuStore = 1,
    CpuAtomic = 2,
    DmaStreamComputeToken = 3,
    DmaController = 4,
    StreamEngine = 5,
    SrfAssist = 6,
    L7Accelerator = 7,
    CacheProtocol = 8
}

public enum GlobalMemoryOperationKind : byte
{
    Read = 0,
    Write = 1,
    ReadWrite = 2,
    Atomic = 3,
    Reservation = 4,
    Commit = 5,
    Fence = 6,
    Wait = 7,
    Poll = 8,
    Invalidate = 9,
    Flush = 10
}

public enum GlobalMemoryAddressSpaceKind : byte
{
    Physical = 0,
    IommuTranslated = 1,
    Unresolved = 2
}

[Flags]
public enum GlobalMemoryRangeAccessKind : byte
{
    None = 0,
    Read = 1,
    Write = 2,
    Atomic = 4,
    Reservation = 8
}

public enum GlobalMemoryConflictClass : byte
{
    None = 0,
    ServiceAbsent = 1,
    IncompleteFootprintTruth = 2,
    UnresolvedAddressSpaceRequiresPolicy = 3,
    AddressSpaceMismatchRequiresPolicy = 4,
    DomainMismatchRequiresPolicy = 5,
    MappingEpochMismatchRequiresPolicy = 6,
    MappingEpochRequiredForEnforcingMode = 7,
    ReadAfterWriteOverlap = 8,
    WriteAfterReadOverlap = 9,
    WriteWriteOverlap = 10,
    AtomicReservationOverlap = 11,
    FenceWaitPollFutureGated = 12
}

public enum GlobalMemoryConflictDecisionKind : byte
{
    Accept = 0,
    Stall = 1,
    Replay = 2,
    Serialize = 3,
    Reject = 4,
    Fault = 5,
    Cancel = 6
}

public readonly record struct GlobalMemoryFootprintRange
{
    public GlobalMemoryFootprintRange(
        ulong address,
        ulong length,
        GlobalMemoryRangeAccessKind accessKind)
    {
        Address = address;
        Length = length;
        AccessKind = accessKind;
    }

    public ulong Address { get; }

    public ulong Length { get; }

    public GlobalMemoryRangeAccessKind AccessKind { get; }

    public bool IsRead =>
        (AccessKind & GlobalMemoryRangeAccessKind.Read) != 0;

    public bool IsWrite =>
        (AccessKind & GlobalMemoryRangeAccessKind.Write) != 0;

    public bool IsAtomicOrReservation =>
        (AccessKind & (GlobalMemoryRangeAccessKind.Atomic | GlobalMemoryRangeAccessKind.Reservation)) != 0;

    public bool IsWellFormed =>
        Length != 0 &&
        Address <= ulong.MaxValue - Length &&
        AccessKind != GlobalMemoryRangeAccessKind.None;

    public bool Overlaps(GlobalMemoryFootprintRange other)
    {
        if (!IsWellFormed || !other.IsWellFormed)
        {
            return false;
        }

        ulong leftEnd = Address + Length;
        ulong rightEnd = other.Address + other.Length;
        return Address < rightEnd && other.Address < leftEnd;
    }
}

public sealed record GlobalMemoryParticipantIdentity
{
    public const uint CpuDeviceId = 0;

    public required ushort OwnerVirtualThreadId { get; init; }

    public required uint OwnerContextId { get; init; }

    public required uint OwnerCoreId { get; init; }

    public required uint OwnerPodId { get; init; }

    public required ulong MemoryDomainTag { get; init; }

    public required ulong ActiveDomainCertificate { get; init; }

    public required uint DeviceId { get; init; }

    public ulong MappingEpoch { get; init; }

    public bool MappingEpochKnown { get; init; }

    public ulong TokenId { get; init; }

    public ulong CommandId { get; init; }

    public ulong DescriptorIdentityHash { get; init; }

    public ulong NormalizedFootprintHash { get; init; }

    public ulong SubmissionSequence { get; init; }

    public ulong IssueAge { get; init; }

    public ulong ReplayGeneration { get; init; }

    public bool HasTokenOrCommandIdentity =>
        TokenId != 0 || CommandId != 0 || SubmissionSequence != 0;

    public bool RequiresTokenIdentityForEnforcingMode =>
        TokenId == 0 && CommandId == 0;

    public bool RequiresMappingEpochForEnforcingMode =>
        !MappingEpochKnown;

    public static GlobalMemoryParticipantIdentity FromCpu(
        ushort ownerVirtualThreadId,
        uint ownerContextId,
        uint ownerCoreId,
        uint ownerPodId,
        ulong memoryDomainTag,
        ulong mappingEpoch = 0,
        bool mappingEpochKnown = false) =>
        new()
        {
            OwnerVirtualThreadId = ownerVirtualThreadId,
            OwnerContextId = ownerContextId,
            OwnerCoreId = ownerCoreId,
            OwnerPodId = ownerPodId,
            MemoryDomainTag = memoryDomainTag,
            ActiveDomainCertificate = memoryDomainTag,
            DeviceId = CpuDeviceId,
            MappingEpoch = mappingEpoch,
            MappingEpochKnown = mappingEpochKnown
        };
}

public sealed record GlobalMemoryFootprint
{
    public GlobalMemoryFootprint(
        GlobalMemoryParticipantKind participantKind,
        GlobalMemoryOperationKind operationKind,
        GlobalMemoryAddressSpaceKind addressSpaceKind,
        GlobalMemoryParticipantIdentity identity,
        IReadOnlyList<GlobalMemoryFootprintRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ParticipantKind = participantKind;
        OperationKind = operationKind;
        AddressSpaceKind = addressSpaceKind;
        Identity = identity;
        Ranges = FreezeRanges(ranges);
    }

    public GlobalMemoryParticipantKind ParticipantKind { get; }

    public GlobalMemoryOperationKind OperationKind { get; }

    public GlobalMemoryAddressSpaceKind AddressSpaceKind { get; }

    public GlobalMemoryParticipantIdentity Identity { get; }

    public IReadOnlyList<GlobalMemoryFootprintRange> Ranges { get; }

    public bool IsBoundaryObservation =>
        OperationKind is GlobalMemoryOperationKind.Fence
            or GlobalMemoryOperationKind.Wait
            or GlobalMemoryOperationKind.Poll;

    public bool HasWriteAccess
    {
        get
        {
            for (int index = 0; index < Ranges.Count; index++)
            {
                if (Ranges[index].IsWrite)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool HasReadAccess
    {
        get
        {
            for (int index = 0; index < Ranges.Count; index++)
            {
                if (Ranges[index].IsRead)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool HasCompleteFootprintTruth
    {
        get
        {
            if (IsBoundaryObservation)
            {
                return true;
            }

            if (Ranges.Count == 0)
            {
                return false;
            }

            for (int index = 0; index < Ranges.Count; index++)
            {
                if (!Ranges[index].IsWellFormed)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public bool RequiresMappingEpochBeforeEnforcing =>
        AddressSpaceKind != GlobalMemoryAddressSpaceKind.Physical &&
        Identity.RequiresMappingEpochForEnforcingMode;

    public static GlobalMemoryFootprint CpuScalarLoad(
        ushort ownerVirtualThreadId,
        uint ownerContextId,
        uint ownerCoreId,
        uint ownerPodId,
        ulong memoryDomainTag,
        ulong address,
        ulong length,
        ulong mappingEpoch = 0,
        bool mappingEpochKnown = false) =>
        new(
            GlobalMemoryParticipantKind.CpuLoad,
            GlobalMemoryOperationKind.Read,
            GlobalMemoryAddressSpaceKind.Physical,
            GlobalMemoryParticipantIdentity.FromCpu(
                ownerVirtualThreadId,
                ownerContextId,
                ownerCoreId,
                ownerPodId,
                memoryDomainTag,
                mappingEpoch,
                mappingEpochKnown),
            new[]
            {
                new GlobalMemoryFootprintRange(address, length, GlobalMemoryRangeAccessKind.Read)
            });

    public static GlobalMemoryFootprint CpuScalarStore(
        ushort ownerVirtualThreadId,
        uint ownerContextId,
        uint ownerCoreId,
        uint ownerPodId,
        ulong memoryDomainTag,
        ulong address,
        ulong length,
        ulong mappingEpoch = 0,
        bool mappingEpochKnown = false) =>
        new(
            GlobalMemoryParticipantKind.CpuStore,
            GlobalMemoryOperationKind.Write,
            GlobalMemoryAddressSpaceKind.Physical,
            GlobalMemoryParticipantIdentity.FromCpu(
                ownerVirtualThreadId,
                ownerContextId,
                ownerCoreId,
                ownerPodId,
                memoryDomainTag,
                mappingEpoch,
                mappingEpochKnown),
            new[]
            {
                new GlobalMemoryFootprintRange(address, length, GlobalMemoryRangeAccessKind.Write)
            });

    public static GlobalMemoryFootprint CpuAtomic(
        ushort ownerVirtualThreadId,
        uint ownerContextId,
        uint ownerCoreId,
        uint ownerPodId,
        ulong memoryDomainTag,
        ulong address,
        ulong length,
        bool isReservation,
        ulong mappingEpoch = 0,
        bool mappingEpochKnown = false) =>
        new(
            GlobalMemoryParticipantKind.CpuAtomic,
            isReservation ? GlobalMemoryOperationKind.Reservation : GlobalMemoryOperationKind.Atomic,
            GlobalMemoryAddressSpaceKind.Physical,
            GlobalMemoryParticipantIdentity.FromCpu(
                ownerVirtualThreadId,
                ownerContextId,
                ownerCoreId,
                ownerPodId,
                memoryDomainTag,
                mappingEpoch,
                mappingEpochKnown),
            new[]
            {
                new GlobalMemoryFootprintRange(
                    address,
                    length,
                    GlobalMemoryRangeAccessKind.Read |
                    GlobalMemoryRangeAccessKind.Write |
                    GlobalMemoryRangeAccessKind.Atomic |
                    (isReservation ? GlobalMemoryRangeAccessKind.Reservation : GlobalMemoryRangeAccessKind.None))
            });

    public static GlobalMemoryFootprint FutureBoundary(
        GlobalMemoryOperationKind operationKind,
        GlobalMemoryParticipantIdentity identity)
    {
        if (operationKind is not (
            GlobalMemoryOperationKind.Fence or
            GlobalMemoryOperationKind.Wait or
            GlobalMemoryOperationKind.Poll))
        {
            throw new ArgumentOutOfRangeException(
                nameof(operationKind),
                operationKind,
                "Only fence, wait, and poll are accepted as future boundary observations.");
        }

        return new GlobalMemoryFootprint(
            GlobalMemoryParticipantKind.CpuLoad,
            operationKind,
            GlobalMemoryAddressSpaceKind.Unresolved,
            identity,
            Array.Empty<GlobalMemoryFootprintRange>());
    }

    public static GlobalMemoryFootprint FromDmaStreamCompute(
        DmaStreamComputeDescriptor descriptor,
        ulong tokenId = 0,
        ulong issueAge = 0,
        ulong replayGeneration = 0)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        DmaStreamComputeOwnerBinding owner = descriptor.OwnerBinding;
        DmaStreamComputeOwnerGuardContext guardContext =
            descriptor.OwnerGuardDecision.RuntimeOwnerContext;
        return new GlobalMemoryFootprint(
            GlobalMemoryParticipantKind.DmaStreamComputeToken,
            GlobalMemoryOperationKind.ReadWrite,
            GlobalMemoryAddressSpaceKind.Physical,
            new GlobalMemoryParticipantIdentity
            {
                OwnerVirtualThreadId = owner.OwnerVirtualThreadId,
                OwnerContextId = owner.OwnerContextId,
                OwnerCoreId = owner.OwnerCoreId,
                OwnerPodId = owner.OwnerPodId,
                MemoryDomainTag = owner.OwnerDomainTag,
                ActiveDomainCertificate = guardContext.ActiveDomainCertificate,
                DeviceId = owner.DeviceId,
                MappingEpochKnown = false,
                TokenId = tokenId,
                DescriptorIdentityHash = descriptor.DescriptorIdentityHash,
                NormalizedFootprintHash = descriptor.NormalizedFootprintHash,
                IssueAge = issueAge,
                ReplayGeneration = replayGeneration
            },
            MergeRanges(
                descriptor.NormalizedReadMemoryRanges,
                descriptor.NormalizedWriteMemoryRanges));
    }

    public static GlobalMemoryFootprint FromDmaStreamCompute(
        DmaStreamComputeActiveTokenEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return FromDmaStreamCompute(
            entry.Token.Descriptor,
            entry.Handle.TokenId,
            entry.Metadata.IssueCycle,
            entry.Metadata.ReplayEpoch);
    }

    public static GlobalMemoryFootprint FromL7Accelerator(
        AcceleratorCommandDescriptor descriptor,
        ulong tokenId = 0,
        ulong submissionSequence = 0,
        ulong issueAge = 0,
        ulong replayGeneration = 0)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        AcceleratorOwnerBinding owner = descriptor.OwnerBinding;
        return new GlobalMemoryFootprint(
            GlobalMemoryParticipantKind.L7Accelerator,
            GlobalMemoryOperationKind.ReadWrite,
            GlobalMemoryAddressSpaceKind.IommuTranslated,
            new GlobalMemoryParticipantIdentity
            {
                OwnerVirtualThreadId = owner.OwnerVirtualThreadId,
                OwnerContextId = owner.OwnerContextId,
                OwnerCoreId = owner.OwnerCoreId,
                OwnerPodId = owner.OwnerPodId,
                MemoryDomainTag = owner.DomainTag,
                ActiveDomainCertificate =
                    descriptor.OwnerGuardDecision.Evidence?.ActiveDomainCertificate ?? 0,
                DeviceId = (uint)descriptor.AcceleratorId,
                MappingEpoch = descriptor.OwnerGuardDecision.MappingEpoch.Value,
                MappingEpochKnown = descriptor.OwnerGuardDecision.Evidence is not null,
                TokenId = tokenId,
                DescriptorIdentityHash = descriptor.Identity.DescriptorIdentityHash,
                NormalizedFootprintHash = descriptor.Identity.NormalizedFootprintHash,
                SubmissionSequence = submissionSequence,
                IssueAge = issueAge,
                ReplayGeneration = replayGeneration
            },
            MergeRanges(
                descriptor.NormalizedFootprint.SourceRanges,
                descriptor.NormalizedFootprint.DestinationRanges,
                descriptor.NormalizedFootprint.ScratchRanges));
    }

    public static GlobalMemoryFootprint FromDmaControllerTransfer(
        ulong sourceAddress,
        ulong destinationAddress,
        ulong length,
        ulong memoryDomainTag,
        uint deviceId,
        ulong commandId = 0,
        bool useIommuTranslation = false,
        ulong mappingEpoch = 0,
        bool mappingEpochKnown = false) =>
        new(
            GlobalMemoryParticipantKind.DmaController,
            GlobalMemoryOperationKind.ReadWrite,
            useIommuTranslation
                ? GlobalMemoryAddressSpaceKind.IommuTranslated
                : GlobalMemoryAddressSpaceKind.Physical,
            new GlobalMemoryParticipantIdentity
            {
                OwnerVirtualThreadId = 0,
                OwnerContextId = 0,
                OwnerCoreId = 0,
                OwnerPodId = 0,
                MemoryDomainTag = memoryDomainTag,
                ActiveDomainCertificate = memoryDomainTag,
                DeviceId = deviceId,
                MappingEpoch = mappingEpoch,
                MappingEpochKnown = mappingEpochKnown,
                CommandId = commandId
            },
            new[]
            {
                new GlobalMemoryFootprintRange(sourceAddress, length, GlobalMemoryRangeAccessKind.Read),
                new GlobalMemoryFootprintRange(destinationAddress, length, GlobalMemoryRangeAccessKind.Write)
            });

    private static IReadOnlyList<GlobalMemoryFootprintRange> MergeRanges(
        IReadOnlyList<DmaStreamComputeMemoryRange> readRanges,
        IReadOnlyList<DmaStreamComputeMemoryRange> writeRanges)
    {
        int readCount = readRanges?.Count ?? 0;
        int writeCount = writeRanges?.Count ?? 0;
        var result = new GlobalMemoryFootprintRange[readCount + writeCount];
        int cursor = 0;
        for (int index = 0; index < readCount; index++)
        {
            result[cursor++] = new GlobalMemoryFootprintRange(
                readRanges![index].Address,
                readRanges[index].Length,
                GlobalMemoryRangeAccessKind.Read);
        }

        for (int index = 0; index < writeCount; index++)
        {
            result[cursor++] = new GlobalMemoryFootprintRange(
                writeRanges![index].Address,
                writeRanges[index].Length,
                GlobalMemoryRangeAccessKind.Write);
        }

        return result;
    }

    private static IReadOnlyList<GlobalMemoryFootprintRange> MergeRanges(
        IReadOnlyList<AcceleratorMemoryRange> readRanges,
        IReadOnlyList<AcceleratorMemoryRange> writeRanges,
        IReadOnlyList<AcceleratorMemoryRange> scratchRanges)
    {
        int readCount = readRanges?.Count ?? 0;
        int writeCount = writeRanges?.Count ?? 0;
        int scratchCount = scratchRanges?.Count ?? 0;
        var result = new GlobalMemoryFootprintRange[readCount + writeCount + scratchCount];
        int cursor = 0;
        for (int index = 0; index < readCount; index++)
        {
            result[cursor++] = new GlobalMemoryFootprintRange(
                readRanges![index].Address,
                readRanges[index].Length,
                GlobalMemoryRangeAccessKind.Read);
        }

        for (int index = 0; index < writeCount; index++)
        {
            result[cursor++] = new GlobalMemoryFootprintRange(
                writeRanges![index].Address,
                writeRanges[index].Length,
                GlobalMemoryRangeAccessKind.Write);
        }

        for (int index = 0; index < scratchCount; index++)
        {
            result[cursor++] = new GlobalMemoryFootprintRange(
                scratchRanges![index].Address,
                scratchRanges[index].Length,
                GlobalMemoryRangeAccessKind.Read | GlobalMemoryRangeAccessKind.Write);
        }

        return result;
    }

    private static IReadOnlyList<GlobalMemoryFootprintRange> FreezeRanges(
        IReadOnlyList<GlobalMemoryFootprintRange>? ranges)
    {
        if (ranges is null || ranges.Count == 0)
        {
            return Array.Empty<GlobalMemoryFootprintRange>();
        }

        var copy = new GlobalMemoryFootprintRange[ranges.Count];
        for (int index = 0; index < ranges.Count; index++)
        {
            copy[index] = ranges[index];
        }

        return Array.AsReadOnly(copy);
    }
}

public sealed record GlobalMemoryConflictObservation
{
    public GlobalMemoryConflictObservation(
        GlobalMemoryConflictServiceMode serviceMode,
        GlobalMemoryConflictDecisionKind decisionKind,
        GlobalMemoryConflictClass conflictClass,
        GlobalMemoryFootprint incomingFootprint,
        GlobalMemoryFootprint? conflictingFootprint,
        GlobalMemoryFootprintRange conflictRange,
        string message)
    {
        ArgumentNullException.ThrowIfNull(incomingFootprint);
        ServiceMode = serviceMode;
        DecisionKind = decisionKind;
        ConflictClass = conflictClass;
        IncomingFootprint = incomingFootprint;
        ConflictingFootprint = conflictingFootprint;
        ConflictRange = conflictRange;
        Message = string.IsNullOrWhiteSpace(message)
            ? $"Global memory conflict observation: {conflictClass}."
            : message;
    }

    public GlobalMemoryConflictServiceMode ServiceMode { get; }

    public GlobalMemoryConflictDecisionKind DecisionKind { get; }

    public GlobalMemoryConflictClass ConflictClass { get; }

    public GlobalMemoryFootprint IncomingFootprint { get; }

    public GlobalMemoryFootprint? ConflictingFootprint { get; }

    public GlobalMemoryFootprintRange ConflictRange { get; }

    public string Message { get; }

    public bool IsConflict =>
        ConflictClass is not GlobalMemoryConflictClass.None
            and not GlobalMemoryConflictClass.ServiceAbsent;

    public bool IsPassiveOnly =>
        ServiceMode == GlobalMemoryConflictServiceMode.PresentPassive;

    public bool IsCurrentArchitecturalEffect => false;

    public bool ChangesArchitecturalMemoryResults => false;

    public bool CanPublishArchitecturalMemory => false;

    public bool FutureEnforcingDecisionWouldBlock =>
        DecisionKind is GlobalMemoryConflictDecisionKind.Stall
            or GlobalMemoryConflictDecisionKind.Replay
            or GlobalMemoryConflictDecisionKind.Serialize
            or GlobalMemoryConflictDecisionKind.Reject
            or GlobalMemoryConflictDecisionKind.Fault
            or GlobalMemoryConflictDecisionKind.Cancel;
}

public sealed class GlobalMemoryConflictService
{
    private readonly List<GlobalMemoryFootprint> _activeFootprints = new();
    private readonly List<GlobalMemoryConflictObservation> _observations = new();

    private GlobalMemoryConflictService(GlobalMemoryConflictServiceMode mode)
    {
        Mode = mode;
    }

    public GlobalMemoryConflictServiceMode Mode { get; }

    public int ActiveFootprintCount => _activeFootprints.Count;

    public IReadOnlyList<GlobalMemoryConflictObservation> Observations =>
        _observations.AsReadOnly();

    public static GlobalMemoryConflictService CreateAbsent() =>
        new(GlobalMemoryConflictServiceMode.Absent);

    public static GlobalMemoryConflictService CreatePresentPassive() =>
        new(GlobalMemoryConflictServiceMode.PresentPassive);

    public static GlobalMemoryConflictService CreatePresentEnforcingForFutureGate() =>
        new(GlobalMemoryConflictServiceMode.PresentEnforcing);

    public GlobalMemoryConflictObservation RegisterActive(
        GlobalMemoryFootprint footprint)
    {
        ArgumentNullException.ThrowIfNull(footprint);
        if (Mode == GlobalMemoryConflictServiceMode.Absent)
        {
            return AbsentObservation(footprint);
        }

        GlobalMemoryConflictObservation observation = ClassifyAgainstActive(footprint);
        _observations.Add(observation);

        if (Mode == GlobalMemoryConflictServiceMode.PresentPassive ||
            observation.DecisionKind == GlobalMemoryConflictDecisionKind.Accept)
        {
            _activeFootprints.Add(footprint);
        }

        return observation;
    }

    public GlobalMemoryConflictObservation ObserveAccess(
        GlobalMemoryFootprint footprint)
    {
        ArgumentNullException.ThrowIfNull(footprint);
        if (Mode == GlobalMemoryConflictServiceMode.Absent)
        {
            return AbsentObservation(footprint);
        }

        GlobalMemoryConflictObservation observation = ClassifyAgainstActive(footprint);
        _observations.Add(observation);
        return observation;
    }

    public int ReleaseByTokenOrCommand(
        GlobalMemoryParticipantKind participantKind,
        ulong tokenId,
        ulong commandId = 0)
    {
        if (Mode == GlobalMemoryConflictServiceMode.Absent ||
            (tokenId == 0 && commandId == 0))
        {
            return 0;
        }

        int removed = 0;
        for (int index = _activeFootprints.Count - 1; index >= 0; index--)
        {
            GlobalMemoryFootprint footprint = _activeFootprints[index];
            if (footprint.ParticipantKind != participantKind)
            {
                continue;
            }

            bool tokenMatches =
                tokenId != 0 &&
                footprint.Identity.TokenId == tokenId;
            bool commandMatches =
                commandId != 0 &&
                footprint.Identity.CommandId == commandId;
            if (!tokenMatches && !commandMatches)
            {
                continue;
            }

            _activeFootprints.RemoveAt(index);
            removed++;
        }

        return removed;
    }

    private GlobalMemoryConflictObservation ClassifyAgainstActive(
        GlobalMemoryFootprint incoming)
    {
        if (incoming.IsBoundaryObservation)
        {
            return BoundaryObservation(incoming);
        }

        if (!incoming.HasCompleteFootprintTruth)
        {
            return Observation(
                GlobalMemoryConflictDecisionKind.Reject,
                GlobalMemoryConflictClass.IncompleteFootprintTruth,
                incoming,
                conflicting: null,
                conflictRange: default,
                "Global memory footprint is incomplete; future enforcing mode cannot treat it as safe overlap.");
        }

        for (int index = 0; index < _activeFootprints.Count; index++)
        {
            GlobalMemoryConflictObservation observation =
                ClassifyPair(_activeFootprints[index], incoming);
            if (observation.IsConflict)
            {
                return observation;
            }
        }

        return Observation(
            GlobalMemoryConflictDecisionKind.Accept,
            GlobalMemoryConflictClass.None,
            incoming,
            conflicting: null,
            conflictRange: default,
            "Global memory conflict service observed no overlapping active footprint.");
    }

    private GlobalMemoryConflictObservation ClassifyPair(
        GlobalMemoryFootprint active,
        GlobalMemoryFootprint incoming)
    {
        if (!active.HasCompleteFootprintTruth)
        {
            return Observation(
                GlobalMemoryConflictDecisionKind.Reject,
                GlobalMemoryConflictClass.IncompleteFootprintTruth,
                incoming,
                active,
                default,
                "Active global memory footprint truth is incomplete.");
        }

        if (!TryFindFirstOverlap(
                active,
                incoming,
                out GlobalMemoryFootprintRange activeRange,
                out GlobalMemoryFootprintRange incomingRange,
                out GlobalMemoryFootprintRange overlap))
        {
            return Observation(
                GlobalMemoryConflictDecisionKind.Accept,
                GlobalMemoryConflictClass.None,
                incoming,
                conflicting: null,
                conflictRange: default,
                "Global memory conflict service observed no range overlap for this active footprint.");
        }

        if (active.AddressSpaceKind == GlobalMemoryAddressSpaceKind.Unresolved ||
            incoming.AddressSpaceKind == GlobalMemoryAddressSpaceKind.Unresolved)
        {
            return Observation(
                GlobalMemoryConflictDecisionKind.Serialize,
                GlobalMemoryConflictClass.UnresolvedAddressSpaceRequiresPolicy,
                incoming,
                active,
                overlap,
                "Unresolved address-space kind requires future serialization, rejection, or fault policy.");
        }

        if (active.AddressSpaceKind != incoming.AddressSpaceKind)
        {
            return Observation(
                GlobalMemoryConflictDecisionKind.Serialize,
                GlobalMemoryConflictClass.AddressSpaceMismatchRequiresPolicy,
                incoming,
                active,
                overlap,
                "Address-space kind mismatch requires a future translation/domain policy before overlap can execute.");
        }

        if (active.RequiresMappingEpochBeforeEnforcing ||
            incoming.RequiresMappingEpochBeforeEnforcing)
        {
            return Observation(
                GlobalMemoryConflictDecisionKind.Reject,
                GlobalMemoryConflictClass.MappingEpochRequiredForEnforcingMode,
                incoming,
                active,
                overlap,
                "Translated or unresolved footprint requires a mapping epoch before enforcing mode can accept overlap.");
        }

        if (active.Identity.MappingEpochKnown &&
            incoming.Identity.MappingEpochKnown &&
            active.Identity.MappingEpoch != incoming.Identity.MappingEpoch)
        {
            return Observation(
                GlobalMemoryConflictDecisionKind.Reject,
                GlobalMemoryConflictClass.MappingEpochMismatchRequiresPolicy,
                incoming,
                active,
                overlap,
                "Mapping epoch mismatch requires future reject, serialize, or fault policy.");
        }

        if (active.Identity.MemoryDomainTag != incoming.Identity.MemoryDomainTag)
        {
            return Observation(
                GlobalMemoryConflictDecisionKind.Serialize,
                GlobalMemoryConflictClass.DomainMismatchRequiresPolicy,
                incoming,
                active,
                overlap,
                "Overlapping footprints carry different memory domains; future policy must serialize, reject, or fault explicitly.");
        }

        bool activeWrites = activeRange.IsWrite;
        bool incomingWrites = incomingRange.IsWrite;
        bool activeReads = activeRange.IsRead;
        bool incomingReads = incomingRange.IsRead;
        bool atomicOrReservation =
            active.OperationKind is GlobalMemoryOperationKind.Atomic
                or GlobalMemoryOperationKind.Reservation ||
            incoming.OperationKind is GlobalMemoryOperationKind.Atomic
                or GlobalMemoryOperationKind.Reservation ||
            activeRange.IsAtomicOrReservation ||
            incomingRange.IsAtomicOrReservation;

        if (atomicOrReservation && (activeWrites || incomingWrites))
        {
            return Observation(
                GlobalMemoryConflictDecisionKind.Stall,
                GlobalMemoryConflictClass.AtomicReservationOverlap,
                incoming,
                active,
                overlap,
                "Atomic or reservation footprint overlaps a write-capable participant.");
        }

        if (activeWrites && incomingWrites)
        {
            return Observation(
                GlobalMemoryConflictDecisionKind.Serialize,
                GlobalMemoryConflictClass.WriteWriteOverlap,
                incoming,
                active,
                overlap,
                "Two write-capable global memory footprints overlap.");
        }

        if (activeWrites && incomingReads)
        {
            return Observation(
                GlobalMemoryConflictDecisionKind.Serialize,
                GlobalMemoryConflictClass.ReadAfterWriteOverlap,
                incoming,
                active,
                overlap,
                "Incoming read overlaps an active write footprint.");
        }

        if (activeReads && incomingWrites)
        {
            return Observation(
                GlobalMemoryConflictDecisionKind.Serialize,
                GlobalMemoryConflictClass.WriteAfterReadOverlap,
                incoming,
                active,
                overlap,
                "Incoming write overlaps an active read footprint.");
        }

        return Observation(
            GlobalMemoryConflictDecisionKind.Accept,
            GlobalMemoryConflictClass.None,
            incoming,
            conflicting: null,
            conflictRange: default,
            "Read-only overlap does not create a Phase05 conflict observation.");
    }

    private static bool TryFindFirstOverlap(
        GlobalMemoryFootprint left,
        GlobalMemoryFootprint right,
        out GlobalMemoryFootprintRange leftRange,
        out GlobalMemoryFootprintRange rightRange,
        out GlobalMemoryFootprintRange overlap)
    {
        leftRange = default;
        rightRange = default;
        overlap = default;
        bool foundReadOnlyOverlap = false;
        GlobalMemoryFootprintRange savedLeftRange = default;
        GlobalMemoryFootprintRange savedRightRange = default;
        GlobalMemoryFootprintRange savedOverlap = default;

        for (int leftIndex = 0; leftIndex < left.Ranges.Count; leftIndex++)
        {
            GlobalMemoryFootprintRange leftCandidate = left.Ranges[leftIndex];
            for (int rightIndex = 0; rightIndex < right.Ranges.Count; rightIndex++)
            {
                GlobalMemoryFootprintRange rightCandidate = right.Ranges[rightIndex];
                if (!leftCandidate.Overlaps(rightCandidate))
                {
                    continue;
                }

                ulong start = Math.Max(leftCandidate.Address, rightCandidate.Address);
                ulong end = Math.Min(
                    leftCandidate.Address + leftCandidate.Length,
                    rightCandidate.Address + rightCandidate.Length);
                leftRange = leftCandidate;
                rightRange = rightCandidate;
                overlap = new GlobalMemoryFootprintRange(
                    start,
                    end - start,
                    leftCandidate.AccessKind | rightCandidate.AccessKind);

                bool writeOrAtomicRelevant =
                    leftCandidate.IsWrite ||
                    rightCandidate.IsWrite ||
                    leftCandidate.IsAtomicOrReservation ||
                    rightCandidate.IsAtomicOrReservation;
                if (!writeOrAtomicRelevant)
                {
                    foundReadOnlyOverlap = true;
                    savedLeftRange = leftRange;
                    savedRightRange = rightRange;
                    savedOverlap = overlap;
                    continue;
                }

                return true;
            }
        }

        if (!foundReadOnlyOverlap)
        {
            return false;
        }

        leftRange = savedLeftRange;
        rightRange = savedRightRange;
        overlap = savedOverlap;
        return true;
    }

    private GlobalMemoryConflictObservation BoundaryObservation(
        GlobalMemoryFootprint incoming)
    {
        GlobalMemoryConflictDecisionKind decisionKind = incoming.OperationKind switch
        {
            GlobalMemoryOperationKind.Poll => GlobalMemoryConflictDecisionKind.Accept,
            GlobalMemoryOperationKind.Wait => GlobalMemoryConflictDecisionKind.Stall,
            _ => GlobalMemoryConflictDecisionKind.Serialize
        };

        return Observation(
            decisionKind,
            GlobalMemoryConflictClass.FenceWaitPollFutureGated,
            incoming,
            conflicting: null,
            conflictRange: default,
            "Fence, wait, and poll are Phase05 future-gated observations only and cannot publish memory.");
    }

    private GlobalMemoryConflictObservation Observation(
        GlobalMemoryConflictDecisionKind decisionKind,
        GlobalMemoryConflictClass conflictClass,
        GlobalMemoryFootprint incoming,
        GlobalMemoryFootprint? conflicting,
        GlobalMemoryFootprintRange conflictRange,
        string message) =>
        new(
            Mode,
            decisionKind,
            conflictClass,
            incoming,
            conflicting,
            conflictRange,
            message);

    private static GlobalMemoryConflictObservation AbsentObservation(
        GlobalMemoryFootprint incoming) =>
        new(
            GlobalMemoryConflictServiceMode.Absent,
            GlobalMemoryConflictDecisionKind.Accept,
            GlobalMemoryConflictClass.ServiceAbsent,
            incoming,
            conflictingFootprint: null,
            conflictRange: default,
            "Global memory conflict service is absent; current scalar/DSC/L7 behavior is unchanged.");
}
