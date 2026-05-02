using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

public enum AcceleratorClassId : ushort
{
    Matrix = 1
}

public enum AcceleratorDeviceId : ushort
{
    ReferenceMatMul = 1
}

public enum AcceleratorOperationKind : ushort
{
    MatMul = 1
}

public enum AcceleratorDatatype : ushort
{
    Float32 = 1,
    Float64 = 2,
    Int32 = 3
}

public enum AcceleratorShapeKind : ushort
{
    Matrix2D = 1
}

public enum AcceleratorPartialCompletionPolicy : ushort
{
    AllOrNone = 1
}

public enum AcceleratorDescriptorFault
{
    None = 0,
    DescriptorDecodeFault = 1,
    UnsupportedAbiVersion = 2,
    DescriptorSizeFault = 3,
    ReservedFieldFault = 4,
    UnsupportedAcceleratorClass = 5,
    UnsupportedAcceleratorId = 6,
    UnsupportedOperation = 7,
    UnsupportedDatatype = 8,
    UnsupportedShape = 9,
    UnsupportedPartialCompletionPolicy = 10,
    RangeOverflow = 11,
    AlignmentFault = 12,
    ZeroLengthFault = 13,
    DescriptorReferenceMismatch = 14,
    DescriptorIdentityHashMismatch = 15,
    NormalizedFootprintHashMismatch = 16,
    DescriptorCarrierDecodeFault = 17,
    OwnerDomainFault = 18,
    AliasAmbiguousFootprint = 19
}

public readonly record struct AcceleratorDescriptorReference(
    ulong DescriptorAddress,
    uint DescriptorSize,
    ulong DescriptorIdentityHash);

public readonly record struct AcceleratorMemoryRange(ulong Address, ulong Length);

public readonly record struct AcceleratorAlignmentRequirement(ushort MinimumAlignmentBytes);

public readonly record struct AcceleratorScratchRequirement(
    ulong RequiredBytes,
    IReadOnlyList<AcceleratorMemoryRange> Ranges);

public readonly record struct AcceleratorDescriptorIdentity(
    ulong DescriptorIdentityHash,
    ulong NormalizedFootprintHash);

public sealed record AcceleratorOwnerBinding
{
    public required ushort OwnerVirtualThreadId { get; init; }

    public required uint OwnerContextId { get; init; }

    public required uint OwnerCoreId { get; init; }

    public required uint OwnerPodId { get; init; }

    public required ulong DomainTag { get; init; }
}

public sealed record AcceleratorNormalizedFootprint
{
    public required IReadOnlyList<AcceleratorMemoryRange> SourceRanges { get; init; }

    public required IReadOnlyList<AcceleratorMemoryRange> DestinationRanges { get; init; }

    public required IReadOnlyList<AcceleratorMemoryRange> ScratchRanges { get; init; }

    public required ulong Hash { get; init; }
}

public readonly record struct AcceleratorDescriptorHeader(
    uint Magic,
    ushort AbiVersion,
    ushort HeaderSize,
    uint DescriptorSize,
    AcceleratorClassId AcceleratorClass,
    AcceleratorDeviceId AcceleratorId,
    AcceleratorOperationKind Operation,
    AcceleratorDatatype Datatype,
    AcceleratorShapeKind Shape,
    ushort ShapeRank,
    ushort SourceRangeCount,
    ushort DestinationRangeCount,
    ushort ScratchRangeCount,
    AcceleratorPartialCompletionPolicy PartialCompletionPolicy,
    AcceleratorAlignmentRequirement Alignment,
    ulong ElementCount,
    uint CapabilityVersion,
    AcceleratorDescriptorIdentity Identity,
    AcceleratorOwnerBinding OwnerBinding,
    ulong ScratchRequiredBytes);

public sealed record AcceleratorCommandDescriptor
{
    public required AcceleratorDescriptorReference DescriptorReference { get; init; }

    public required AcceleratorDescriptorHeader Header { get; init; }

    public required ushort AbiVersion { get; init; }

    public required ushort HeaderSize { get; init; }

    public required uint DescriptorSize { get; init; }

    public required AcceleratorClassId AcceleratorClass { get; init; }

    public required AcceleratorDeviceId AcceleratorId { get; init; }

    public required AcceleratorOperationKind Operation { get; init; }

    public required AcceleratorDatatype Datatype { get; init; }

    public required AcceleratorShapeKind Shape { get; init; }

    public required ushort ShapeRank { get; init; }

    public required ulong ElementCount { get; init; }

    public required uint CapabilityVersion { get; init; }

    public required AcceleratorAlignmentRequirement Alignment { get; init; }

    public required AcceleratorScratchRequirement ScratchRequirement { get; init; }

    public required AcceleratorPartialCompletionPolicy PartialCompletionPolicy { get; init; }

    public required AcceleratorOwnerBinding OwnerBinding { get; init; }

    public required AcceleratorGuardDecision OwnerGuardDecision { get; init; }

    public required AcceleratorDescriptorIdentity Identity { get; init; }

    public required IReadOnlyList<AcceleratorMemoryRange> SourceRanges { get; init; }

    public required IReadOnlyList<AcceleratorMemoryRange> DestinationRanges { get; init; }

    public required IReadOnlyList<AcceleratorMemoryRange> ScratchRanges { get; init; }

    public required AcceleratorNormalizedFootprint NormalizedFootprint { get; init; }

    public AcceleratorCommandDescriptor Freeze()
    {
        IReadOnlyList<AcceleratorMemoryRange> sourceRanges =
            FreezeRanges(SourceRanges);
        IReadOnlyList<AcceleratorMemoryRange> destinationRanges =
            FreezeRanges(DestinationRanges);
        IReadOnlyList<AcceleratorMemoryRange> scratchRanges =
            FreezeRanges(ScratchRanges);
        IReadOnlyList<AcceleratorMemoryRange> scratchRequirementRanges =
            FreezeRanges(ScratchRequirement.Ranges);
        IReadOnlyList<AcceleratorMemoryRange> normalizedSourceRanges =
            FreezeRanges(NormalizedFootprint.SourceRanges);
        IReadOnlyList<AcceleratorMemoryRange> normalizedDestinationRanges =
            FreezeRanges(NormalizedFootprint.DestinationRanges);
        IReadOnlyList<AcceleratorMemoryRange> normalizedScratchRanges =
            FreezeRanges(NormalizedFootprint.ScratchRanges);

        return this with
        {
            SourceRanges = sourceRanges,
            DestinationRanges = destinationRanges,
            ScratchRanges = scratchRanges,
            ScratchRequirement = ScratchRequirement with
            {
                Ranges = scratchRequirementRanges
            },
            NormalizedFootprint = NormalizedFootprint with
            {
                SourceRanges = normalizedSourceRanges,
                DestinationRanges = normalizedDestinationRanges,
                ScratchRanges = normalizedScratchRanges
            }
        };
    }

    private static IReadOnlyList<AcceleratorMemoryRange> FreezeRanges(
        IReadOnlyList<AcceleratorMemoryRange> ranges)
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

public sealed record AcceleratorDescriptorValidationResult
{
    private AcceleratorDescriptorValidationResult(
        AcceleratorDescriptorFault fault,
        AcceleratorCommandDescriptor? descriptor,
        string message)
    {
        Fault = fault;
        Descriptor = descriptor;
        Message = message;
    }

    public bool IsValid => Fault == AcceleratorDescriptorFault.None && Descriptor is not null;

    public AcceleratorDescriptorFault Fault { get; }

    public AcceleratorCommandDescriptor? Descriptor { get; }

    public string Message { get; }

    public static AcceleratorDescriptorValidationResult Valid(
        AcceleratorCommandDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return new AcceleratorDescriptorValidationResult(
            AcceleratorDescriptorFault.None,
            descriptor.Freeze(),
            "L7-SDC descriptor accepted as guard-backed typed sideband; execution remains fail-closed.");
    }

    public static AcceleratorDescriptorValidationResult Fail(
        AcceleratorDescriptorFault fault,
        string message)
    {
        if (fault == AcceleratorDescriptorFault.None)
        {
            throw new ArgumentException(
                "Use Valid for successful descriptor validation.",
                nameof(fault));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Validation failure message is required.", nameof(message));
        }

        return new AcceleratorDescriptorValidationResult(fault, null, message);
    }

    public AcceleratorCommandDescriptor RequireDescriptor()
    {
        if (IsValid && Descriptor is not null)
        {
            return Descriptor;
        }

        throw new InvalidOperationException(
            $"L7-SDC descriptor is not valid: {Fault}. {Message}");
    }
}

public sealed record AcceleratorDescriptorStructuralReadResult
{
    private AcceleratorDescriptorStructuralReadResult(
        AcceleratorDescriptorFault fault,
        AcceleratorDescriptorReference descriptorReference,
        ulong descriptorIdentityHash,
        uint descriptorSize,
        AcceleratorOwnerBinding? ownerBinding,
        string message)
    {
        Fault = fault;
        DescriptorReference = descriptorReference;
        DescriptorIdentityHash = descriptorIdentityHash;
        DescriptorSize = descriptorSize;
        OwnerBinding = ownerBinding;
        Message = message;
    }

    public bool IsValid => Fault == AcceleratorDescriptorFault.None && OwnerBinding is not null;

    public AcceleratorDescriptorFault Fault { get; }

    public AcceleratorDescriptorReference DescriptorReference { get; }

    public ulong DescriptorIdentityHash { get; }

    public uint DescriptorSize { get; }

    public AcceleratorOwnerBinding? OwnerBinding { get; }

    public string Message { get; }

    public static AcceleratorDescriptorStructuralReadResult Valid(
        AcceleratorDescriptorReference descriptorReference,
        ulong descriptorIdentityHash,
        uint descriptorSize,
        AcceleratorOwnerBinding ownerBinding)
    {
        ArgumentNullException.ThrowIfNull(ownerBinding);
        return new AcceleratorDescriptorStructuralReadResult(
            AcceleratorDescriptorFault.None,
            descriptorReference,
            descriptorIdentityHash,
            descriptorSize,
            ownerBinding,
            "L7-SDC structural owner/domain fields located; descriptor acceptance still requires guard-plane authority.");
    }

    public static AcceleratorDescriptorStructuralReadResult Fail(
        AcceleratorDescriptorFault fault,
        string message)
    {
        if (fault == AcceleratorDescriptorFault.None)
        {
            throw new ArgumentException(
                "Use Valid for successful structural descriptor reads.",
                nameof(fault));
        }

        return new AcceleratorDescriptorStructuralReadResult(
            fault,
            default,
            descriptorIdentityHash: 0,
            descriptorSize: 0,
            ownerBinding: null,
            message);
    }

    public AcceleratorOwnerBinding RequireOwnerBindingForGuard()
    {
        if (IsValid && OwnerBinding is not null)
        {
            return OwnerBinding;
        }

        throw new InvalidOperationException(
            $"L7-SDC structural owner/domain read failed: {Fault}. {Message}");
    }
}

public sealed record AcceleratorCarrierValidationResult
{
    private AcceleratorCarrierValidationResult(
        AcceleratorDescriptorFault fault,
        string message)
    {
        Fault = fault;
        Message = message;
    }

    public bool IsValid => Fault == AcceleratorDescriptorFault.None;

    public AcceleratorDescriptorFault Fault { get; }

    public string Message { get; }

    public static AcceleratorCarrierValidationResult Valid() =>
        new(AcceleratorDescriptorFault.None, "Carrier accepted for typed sideband projection.");

    public static AcceleratorCarrierValidationResult Fail(
        AcceleratorDescriptorFault fault,
        string message)
    {
        if (fault == AcceleratorDescriptorFault.None)
        {
            throw new ArgumentException(
                "Use Valid for successful carrier validation.",
                nameof(fault));
        }

        return new AcceleratorCarrierValidationResult(fault, message);
    }
}
