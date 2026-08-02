using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Execution;

namespace YAKSys_Hybrid_CPU.Core.Execution.Addressing;

public enum MemoryAddressSpaceKind : byte
{
    Physical = 0,
    IommuTranslated = 1,
    ReservedFuture = 2
}

public enum AddressingBackendAccessKind : byte
{
    Read = 0,
    Write = 1,
    ReadWrite = 2,
    Commit = 3
}

public enum AddressingBackendKind : byte
{
    None = 0,
    PhysicalMainMemory = 1,
    IommuBurst = 2
}

public enum AddressingBackendDecisionKind : byte
{
    Selected = 0,
    Rejected = 1,
    Faulted = 2
}

public enum AddressingBackendFaultKind : byte
{
    None = 0,
    UnsupportedAddressSpace = 1,
    CapabilityMismatch = 2,
    IncompleteRangeTruth = 3,
    OwnerDomainMismatch = 4,
    DeviceMismatch = 5,
    MissingDeviceBinding = 6,
    MissingMappingEpoch = 7,
    MappingEpochRevoked = 8,
    TranslationFault = 9,
    PermissionFault = 10,
    AlignmentFault = 11,
    BoundsFault = 12,
    DmaDeviceFault = 13,
    BackendMemoryFault = 14
}

[Flags]
public enum AddressingBackendCapabilities : ushort
{
    None = 0,
    ExplicitAddressSpaceContract = 1 << 0,
    PhysicalMainMemoryBackend = 1 << 1,
    IommuBurstBackend = 1 << 2,
    OwnerDomainDeviceBinding = 1 << 3,
    MappingEpoch = 1 << 4,
    TypedFaultClassification = 1 << 5
}

public readonly record struct AddressingBackendRange
{
    public AddressingBackendRange(ulong address, ulong length)
    {
        Address = address;
        Length = length;
    }

    public ulong Address { get; }

    public ulong Length { get; }

    public bool IsWellFormed =>
        Length != 0 &&
        Address <= ulong.MaxValue - Length;
}

public sealed record AddressingBackendParticipantIdentity
{
    public required ushort OwnerVirtualThreadId { get; init; }

    public required uint OwnerContextId { get; init; }

    public required uint OwnerCoreId { get; init; }

    public required uint OwnerPodId { get; init; }

    public required ulong MemoryDomainTag { get; init; }

    public required ulong ActiveDomainCertificate { get; init; }

    public required uint DeviceId { get; init; }

    public bool DeviceIdBound { get; init; }

    public ulong MappingEpoch { get; init; }

    public bool MappingEpochKnown { get; init; }

    public ulong TokenId { get; init; }

    public ulong CommandId { get; init; }

    public ulong DescriptorIdentityHash { get; init; }

    public ulong NormalizedFootprintHash { get; init; }

    public ulong IssueAge { get; init; }

    public ulong ReplayGeneration { get; init; }

    public bool HasOwnerDomainAuthority =>
        IsDomainCoveredByCertificate(MemoryDomainTag, ActiveDomainCertificate);

    public bool HasOwnerDeviceAndDomainBinding =>
        DeviceIdBound && HasOwnerDomainAuthority;

    public static AddressingBackendParticipantIdentity FromDmaStreamCompute(
        DmaStreamComputeDescriptor descriptor,
        ulong tokenId = 0,
        ulong issueAge = 0,
        ulong replayGeneration = 0,
        ulong mappingEpoch = 0,
        bool mappingEpochKnown = false)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        DmaStreamComputeOwnerBinding owner = descriptor.OwnerBinding;
        DmaStreamComputeOwnerGuardContext guard =
            descriptor.OwnerGuardDecision.RuntimeOwnerContext;

        return new AddressingBackendParticipantIdentity
        {
            OwnerVirtualThreadId = owner.OwnerVirtualThreadId,
            OwnerContextId = owner.OwnerContextId,
            OwnerCoreId = owner.OwnerCoreId,
            OwnerPodId = owner.OwnerPodId,
            MemoryDomainTag = owner.OwnerDomainTag,
            ActiveDomainCertificate = guard.ActiveDomainCertificate,
            DeviceId = owner.DeviceId,
            DeviceIdBound = descriptor.OwnerGuardDecision.IsAllowed &&
                            owner.DeviceId == guard.DeviceId,
            MappingEpoch = mappingEpoch,
            MappingEpochKnown = mappingEpochKnown,
            TokenId = tokenId,
            DescriptorIdentityHash = descriptor.DescriptorIdentityHash,
            NormalizedFootprintHash = descriptor.NormalizedFootprintHash,
            IssueAge = issueAge,
            ReplayGeneration = replayGeneration
        };
    }

    public static AddressingBackendParticipantIdentity FromL7Accelerator(
        AcceleratorCommandDescriptor descriptor,
        ulong tokenId = 0,
        ulong commandId = 0,
        ulong issueAge = 0,
        ulong replayGeneration = 0)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        AcceleratorOwnerBinding owner = descriptor.OwnerBinding;
        return new AddressingBackendParticipantIdentity
        {
            OwnerVirtualThreadId = owner.OwnerVirtualThreadId,
            OwnerContextId = owner.OwnerContextId,
            OwnerCoreId = owner.OwnerCoreId,
            OwnerPodId = owner.OwnerPodId,
            MemoryDomainTag = owner.DomainTag,
            ActiveDomainCertificate =
                descriptor.OwnerGuardDecision.Evidence?.ActiveDomainCertificate ?? 0,
            DeviceId = (uint)descriptor.AcceleratorId,
            DeviceIdBound = descriptor.OwnerGuardDecision.IsAllowed,
            MappingEpoch = descriptor.OwnerGuardDecision.MappingEpoch.Value,
            MappingEpochKnown = descriptor.OwnerGuardDecision.Evidence is not null,
            TokenId = tokenId,
            CommandId = commandId,
            DescriptorIdentityHash = descriptor.Identity.DescriptorIdentityHash,
            NormalizedFootprintHash = descriptor.Identity.NormalizedFootprintHash,
            IssueAge = issueAge,
            ReplayGeneration = replayGeneration
        };
    }

    internal bool HasSameOwnerDomainAndDevice(
        AddressingBackendParticipantIdentity current,
        out AddressingBackendFaultKind faultKind)
    {
        faultKind = AddressingBackendFaultKind.None;
        if (OwnerVirtualThreadId != current.OwnerVirtualThreadId ||
            OwnerContextId != current.OwnerContextId ||
            OwnerCoreId != current.OwnerCoreId ||
            OwnerPodId != current.OwnerPodId ||
            MemoryDomainTag != current.MemoryDomainTag)
        {
            faultKind = AddressingBackendFaultKind.OwnerDomainMismatch;
            return false;
        }

        if (DeviceId != current.DeviceId ||
            DeviceIdBound != current.DeviceIdBound)
        {
            faultKind = AddressingBackendFaultKind.DeviceMismatch;
            return false;
        }

        return true;
    }

    private static bool IsDomainCoveredByCertificate(
        ulong memoryDomainTag,
        ulong activeDomainCertificate)
    {
        if (memoryDomainTag == 0)
        {
            return activeDomainCertificate == 0;
        }

        return activeDomainCertificate == 0 ||
               (memoryDomainTag & activeDomainCertificate) != 0;
    }
}

public sealed record AddressingBackendResolutionRequest
{
    public AddressingBackendResolutionRequest(
        MemoryAddressSpaceKind addressSpace,
        AddressingBackendAccessKind accessKind,
        IReadOnlyList<AddressingBackendRange> ranges,
        AddressingBackendParticipantIdentity acceptedIdentity,
        AddressingBackendCapabilities capabilities,
        AddressingBackendParticipantIdentity? currentAuthority = null)
    {
        ArgumentNullException.ThrowIfNull(acceptedIdentity);
        AddressSpace = addressSpace;
        AccessKind = accessKind;
        Ranges = FreezeRanges(ranges);
        AcceptedIdentity = acceptedIdentity;
        Capabilities = capabilities;
        CurrentAuthority = currentAuthority;
    }

    public MemoryAddressSpaceKind AddressSpace { get; }

    public AddressingBackendAccessKind AccessKind { get; }

    public IReadOnlyList<AddressingBackendRange> Ranges { get; }

    public AddressingBackendParticipantIdentity AcceptedIdentity { get; }

    public AddressingBackendParticipantIdentity? CurrentAuthority { get; }

    public AddressingBackendCapabilities Capabilities { get; }

    public bool HasCapability(AddressingBackendCapabilities capability) =>
        (Capabilities & capability) == capability;

    public bool HasCompleteRangeTruth
    {
        get
        {
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

    public static AddressingBackendResolutionRequest ForDmaStreamCompute(
        DmaStreamComputeDescriptor descriptor,
        MemoryAddressSpaceKind addressSpace,
        AddressingBackendCapabilities capabilities,
        ulong tokenId = 0,
        ulong issueAge = 0,
        ulong replayGeneration = 0,
        ulong mappingEpoch = 0,
        bool mappingEpochKnown = false,
        AddressingBackendParticipantIdentity? currentAuthority = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return new AddressingBackendResolutionRequest(
            addressSpace,
            AddressingBackendAccessKind.ReadWrite,
            MergeRanges(
                descriptor.NormalizedReadMemoryRanges,
                descriptor.NormalizedWriteMemoryRanges),
            AddressingBackendParticipantIdentity.FromDmaStreamCompute(
                descriptor,
                tokenId,
                issueAge,
                replayGeneration,
                mappingEpoch: mappingEpoch,
                mappingEpochKnown: mappingEpochKnown),
            capabilities,
            currentAuthority);
    }

    public static AddressingBackendResolutionRequest ForL7Accelerator(
        AcceleratorCommandDescriptor descriptor,
        MemoryAddressSpaceKind addressSpace,
        AddressingBackendCapabilities capabilities,
        ulong tokenId = 0,
        ulong commandId = 0,
        ulong issueAge = 0,
        ulong replayGeneration = 0,
        AddressingBackendParticipantIdentity? currentAuthority = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return new AddressingBackendResolutionRequest(
            addressSpace,
            AddressingBackendAccessKind.ReadWrite,
            MergeRanges(
                descriptor.NormalizedFootprint.SourceRanges,
                descriptor.NormalizedFootprint.DestinationRanges,
                descriptor.NormalizedFootprint.ScratchRanges),
            AddressingBackendParticipantIdentity.FromL7Accelerator(
                descriptor,
                tokenId,
                commandId,
                issueAge,
                replayGeneration),
            capabilities,
            currentAuthority);
    }

    private static IReadOnlyList<AddressingBackendRange> MergeRanges(
        IReadOnlyList<DmaStreamComputeMemoryRange> readRanges,
        IReadOnlyList<DmaStreamComputeMemoryRange> writeRanges)
    {
        int readCount = readRanges?.Count ?? 0;
        int writeCount = writeRanges?.Count ?? 0;
        var ranges = new AddressingBackendRange[readCount + writeCount];
        int cursor = 0;
        for (int index = 0; index < readCount; index++)
        {
            ranges[cursor++] = new AddressingBackendRange(
                readRanges![index].Address,
                readRanges[index].Length);
        }

        for (int index = 0; index < writeCount; index++)
        {
            ranges[cursor++] = new AddressingBackendRange(
                writeRanges![index].Address,
                writeRanges[index].Length);
        }

        return ranges;
    }

    private static IReadOnlyList<AddressingBackendRange> MergeRanges(
        IReadOnlyList<AcceleratorMemoryRange> sourceRanges,
        IReadOnlyList<AcceleratorMemoryRange> destinationRanges,
        IReadOnlyList<AcceleratorMemoryRange> scratchRanges)
    {
        int sourceCount = sourceRanges?.Count ?? 0;
        int destinationCount = destinationRanges?.Count ?? 0;
        int scratchCount = scratchRanges?.Count ?? 0;
        var ranges = new AddressingBackendRange[sourceCount + destinationCount + scratchCount];
        int cursor = 0;
        for (int index = 0; index < sourceCount; index++)
        {
            ranges[cursor++] = new AddressingBackendRange(
                sourceRanges![index].Address,
                sourceRanges[index].Length);
        }

        for (int index = 0; index < destinationCount; index++)
        {
            ranges[cursor++] = new AddressingBackendRange(
                destinationRanges![index].Address,
                destinationRanges[index].Length);
        }

        for (int index = 0; index < scratchCount; index++)
        {
            ranges[cursor++] = new AddressingBackendRange(
                scratchRanges![index].Address,
                scratchRanges[index].Length);
        }

        return ranges;
    }

    private static IReadOnlyList<AddressingBackendRange> FreezeRanges(
        IReadOnlyList<AddressingBackendRange>? ranges)
    {
        if (ranges is null || ranges.Count == 0)
        {
            return Array.Empty<AddressingBackendRange>();
        }

        var copy = new AddressingBackendRange[ranges.Count];
        for (int index = 0; index < ranges.Count; index++)
        {
            copy[index] = ranges[index];
        }

        return Array.AsReadOnly(copy);
    }
}

public sealed record AddressingBackendResolution
{
    private AddressingBackendResolution(
        AddressingBackendDecisionKind decisionKind,
        AddressingBackendKind backendKind,
        IBurstBackend? backend,
        AddressingBackendFaultKind faultKind,
        string message)
    {
        DecisionKind = decisionKind;
        BackendKind = backendKind;
        Backend = backend;
        FaultKind = faultKind;
        Message = message;
    }

    public AddressingBackendDecisionKind DecisionKind { get; }

    public AddressingBackendKind BackendKind { get; }

    public IBurstBackend? Backend { get; }

    public AddressingBackendFaultKind FaultKind { get; }

    public string Message { get; }

    public bool IsSelected => DecisionKind == AddressingBackendDecisionKind.Selected;

    public bool IsRejected => DecisionKind == AddressingBackendDecisionKind.Rejected;

    public bool IsFaulted => DecisionKind == AddressingBackendDecisionKind.Faulted;

    public bool IsCurrentArchitecturalExecution => false;

    public bool ChangesCurrentDscHelperPath => false;

    public bool ChangesCurrentL7CarrierExecution => false;

    public bool AllowsSilentFallbackToPhysical => false;

    internal static AddressingBackendResolution Selected(
        AddressingBackendKind backendKind,
        IBurstBackend backend,
        string message) =>
        new(
            AddressingBackendDecisionKind.Selected,
            backendKind,
            backend,
            AddressingBackendFaultKind.None,
            message);

    internal static AddressingBackendResolution Rejected(
        AddressingBackendFaultKind faultKind,
        string message) =>
        new(
            AddressingBackendDecisionKind.Rejected,
            AddressingBackendKind.None,
            backend: null,
            faultKind,
            message);

    internal static AddressingBackendResolution Faulted(
        AddressingBackendFaultKind faultKind,
        string message) =>
        new(
            AddressingBackendDecisionKind.Faulted,
            AddressingBackendKind.None,
            backend: null,
            faultKind,
            message);
}

public sealed class AddressingBackendResolver
{
    private readonly IBurstBackend? _physicalBackend;
    private readonly IBurstBackend? _iommuBackend;

    public AddressingBackendResolver(
        IBurstBackend? physicalBackend = null,
        IBurstBackend? iommuBackend = null)
    {
        _physicalBackend = physicalBackend;
        _iommuBackend = iommuBackend;
    }

    public AddressingBackendResolution Resolve(
        AddressingBackendResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.HasCompleteRangeTruth)
        {
            return AddressingBackendResolution.Faulted(
                AddressingBackendFaultKind.IncompleteRangeTruth,
                "Addressing backend resolution requires complete, non-empty, non-overflowing range truth.");
        }

        if (!request.HasCapability(AddressingBackendCapabilities.ExplicitAddressSpaceContract))
        {
            return AddressingBackendResolution.Rejected(
                AddressingBackendFaultKind.CapabilityMismatch,
                "Executable backend selection requires an explicit address-space contract; DSC1 reserved fields and carrier hints are not authority.");
        }

        if (!request.HasCapability(AddressingBackendCapabilities.TypedFaultClassification))
        {
            return AddressingBackendResolution.Rejected(
                AddressingBackendFaultKind.CapabilityMismatch,
                "Executable backend selection requires typed fault classification before memory access.");
        }

        AddressingBackendResolution? authorityFailure = ValidateAuthority(request);
        if (authorityFailure is not null)
        {
            return authorityFailure;
        }

        return request.AddressSpace switch
        {
            MemoryAddressSpaceKind.Physical => ResolvePhysical(request),
            MemoryAddressSpaceKind.IommuTranslated => ResolveIommu(request),
            _ => AddressingBackendResolution.Rejected(
                AddressingBackendFaultKind.UnsupportedAddressSpace,
                "Unsupported or future address-space mode rejects before memory access and cannot fall back to physical memory.")
        };
    }

    public DmaStreamComputeFaultRecord CreateDmaStreamComputeFaultRecord(
        DmaStreamComputeDescriptor descriptor,
        AddressingBackendFaultKind faultKind,
        ulong faultAddress,
        bool isWrite,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (faultKind == AddressingBackendFaultKind.None)
        {
            throw new ArgumentException(
                "Use a non-None addressing backend fault kind.",
                nameof(faultKind));
        }

        DmaStreamComputeOwnerGuardContext guard =
            descriptor.OwnerGuardDecision.RuntimeOwnerContext;
        return new DmaStreamComputeFaultRecord(
            MapFaultKind(faultKind),
            string.IsNullOrWhiteSpace(message)
                ? $"Phase06 addressing backend fault: {faultKind}."
                : message,
            faultAddress,
            isWrite,
            descriptor.OwnerBinding.OwnerVirtualThreadId,
            descriptor.OwnerBinding.OwnerDomainTag,
            guard.ActiveDomainCertificate,
            MapSourcePhase(faultKind),
            backendExceptionNormalized: false,
            normalizedHostExceptionType: null,
            DmaStreamComputeFaultPublicationContract.FuturePreciseRetireRequiresPublicationMetadata);
    }

    private AddressingBackendResolution ResolvePhysical(
        AddressingBackendResolutionRequest request)
    {
        if (!request.HasCapability(AddressingBackendCapabilities.PhysicalMainMemoryBackend) ||
            _physicalBackend is null)
        {
            return AddressingBackendResolution.Rejected(
                AddressingBackendFaultKind.CapabilityMismatch,
                "Physical address-space selection requires an explicit physical main-memory backend wrapper.");
        }

        return AddressingBackendResolution.Selected(
            AddressingBackendKind.PhysicalMainMemory,
            _physicalBackend,
            "Physical address-space selection chose the explicit physical main-memory backend wrapper.");
    }

    private AddressingBackendResolution ResolveIommu(
        AddressingBackendResolutionRequest request)
    {
        if (!request.HasCapability(AddressingBackendCapabilities.IommuBurstBackend) ||
            _iommuBackend is null)
        {
            return AddressingBackendResolution.Rejected(
                AddressingBackendFaultKind.CapabilityMismatch,
                "IOMMU-translated address-space selection requires an explicit IOMMU backend; physical fallback is forbidden.");
        }

        if (!request.HasCapability(AddressingBackendCapabilities.MappingEpoch) ||
            !request.AcceptedIdentity.MappingEpochKnown)
        {
            return AddressingBackendResolution.Rejected(
                AddressingBackendFaultKind.MissingMappingEpoch,
                "IOMMU-translated address-space selection requires a captured mapping epoch before memory access.");
        }

        return AddressingBackendResolution.Selected(
            AddressingBackendKind.IommuBurst,
            _iommuBackend,
            "IOMMU-translated address-space selection chose the explicit IOMMU burst backend.");
    }

    private static AddressingBackendResolution? ValidateAuthority(
        AddressingBackendResolutionRequest request)
    {
        if (!request.HasCapability(AddressingBackendCapabilities.OwnerDomainDeviceBinding))
        {
            return AddressingBackendResolution.Rejected(
                AddressingBackendFaultKind.CapabilityMismatch,
                "Backend selection requires owner/domain/device binding authority.");
        }

        AddressingBackendParticipantIdentity identity = request.AcceptedIdentity;
        if (!identity.DeviceIdBound)
        {
            return AddressingBackendResolution.Rejected(
                AddressingBackendFaultKind.MissingDeviceBinding,
                "Backend selection requires an explicit bound device ID; thread/context inference is not authority.");
        }

        if (!identity.HasOwnerDomainAuthority)
        {
            return AddressingBackendResolution.Rejected(
                AddressingBackendFaultKind.OwnerDomainMismatch,
                "Backend selection requires owner/domain authority from the guard plane.");
        }

        if (request.CurrentAuthority is null)
        {
            return null;
        }

        AddressingBackendParticipantIdentity current = request.CurrentAuthority;
        if (!current.HasOwnerDomainAuthority)
        {
            return AddressingBackendResolution.Rejected(
                AddressingBackendFaultKind.OwnerDomainMismatch,
                "Current owner/domain authority is no longer covered by the guard-plane certificate.");
        }

        if (!identity.HasSameOwnerDomainAndDevice(
                current,
                out AddressingBackendFaultKind faultKind))
        {
            return AddressingBackendResolution.Rejected(
                faultKind,
                "Current owner/domain/device authority no longer matches accepted backend-selection evidence.");
        }

        if (request.AddressSpace == MemoryAddressSpaceKind.IommuTranslated &&
            !current.MappingEpochKnown)
        {
            return AddressingBackendResolution.Rejected(
                AddressingBackendFaultKind.MissingMappingEpoch,
                "Current IOMMU authority must carry a mapping epoch before translated memory access.");
        }

        if (request.AddressSpace == MemoryAddressSpaceKind.IommuTranslated &&
            identity.MappingEpochKnown &&
            current.MappingEpochKnown &&
            identity.MappingEpoch != current.MappingEpoch)
        {
            return AddressingBackendResolution.Rejected(
                AddressingBackendFaultKind.MappingEpochRevoked,
                "IOMMU mapping epoch changed after accepted backend-selection evidence.");
        }

        return null;
    }

    private static DmaStreamComputeTokenFaultKind MapFaultKind(
        AddressingBackendFaultKind faultKind) =>
        faultKind switch
        {
            AddressingBackendFaultKind.TranslationFault or
            AddressingBackendFaultKind.MissingMappingEpoch or
            AddressingBackendFaultKind.MappingEpochRevoked
                => DmaStreamComputeTokenFaultKind.TranslationFault,

            AddressingBackendFaultKind.PermissionFault
                => DmaStreamComputeTokenFaultKind.PermissionFault,

            AddressingBackendFaultKind.OwnerDomainMismatch
                => DmaStreamComputeTokenFaultKind.DomainViolation,

            AddressingBackendFaultKind.DeviceMismatch or
            AddressingBackendFaultKind.MissingDeviceBinding or
            AddressingBackendFaultKind.DmaDeviceFault
                => DmaStreamComputeTokenFaultKind.DmaDeviceFault,

            AddressingBackendFaultKind.AlignmentFault
                => DmaStreamComputeTokenFaultKind.AlignmentFault,

            AddressingBackendFaultKind.UnsupportedAddressSpace or
            AddressingBackendFaultKind.CapabilityMismatch
                => DmaStreamComputeTokenFaultKind.UnsupportedAbiOrOperation,

            _ => DmaStreamComputeTokenFaultKind.MemoryFault
        };

    private static DmaStreamComputeFaultSourcePhase MapSourcePhase(
        AddressingBackendFaultKind faultKind) =>
        faultKind switch
        {
            AddressingBackendFaultKind.TranslationFault or
            AddressingBackendFaultKind.PermissionFault or
            AddressingBackendFaultKind.MissingMappingEpoch or
            AddressingBackendFaultKind.MappingEpochRevoked
                => DmaStreamComputeFaultSourcePhase.Iommu,

            AddressingBackendFaultKind.OwnerDomainMismatch or
            AddressingBackendFaultKind.DeviceMismatch or
            AddressingBackendFaultKind.MissingDeviceBinding or
            AddressingBackendFaultKind.CapabilityMismatch or
            AddressingBackendFaultKind.UnsupportedAddressSpace
                => DmaStreamComputeFaultSourcePhase.Admission,

            _ => DmaStreamComputeFaultSourcePhase.Backend
        };
}
