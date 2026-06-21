using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;

public sealed record AcceleratorMemoryRead(
    AcceleratorMemoryRange Range,
    ReadOnlyMemory<byte> Data);

public sealed record AcceleratorStagedWrite(
    AcceleratorTokenHandle TokenHandle,
    ulong Address,
    ReadOnlyMemory<byte> Data)
{
    public ulong Length => (ulong)Data.Length;
}

public sealed record AcceleratorMemoryPortalReadResult
{
    private AcceleratorMemoryPortalReadResult(
        bool isAccepted,
        AcceleratorTokenFaultCode faultCode,
        AcceleratorGuardDecision? guardDecision,
        IReadOnlyList<AcceleratorMemoryRead> reads,
        string message)
    {
        IsAccepted = isAccepted;
        FaultCode = faultCode;
        GuardDecision = guardDecision;
        Reads = reads;
        Message = message;
    }

    public bool IsAccepted { get; }

    public bool IsRejected => !IsAccepted;

    public AcceleratorTokenFaultCode FaultCode { get; }

    public AcceleratorGuardDecision? GuardDecision { get; }

    public IReadOnlyList<AcceleratorMemoryRead> Reads { get; }

    public string Message { get; }

    public bool CanPublishArchitecturalMemory => false;

    public bool CanPublishException => false;

    public ulong BytesRead
    {
        get
        {
            ulong bytes = 0;
            for (int index = 0; index < Reads.Count; index++)
            {
                bytes += (ulong)Reads[index].Data.Length;
            }

            return bytes;
        }
    }

    public static AcceleratorMemoryPortalReadResult Accepted(
        IReadOnlyList<AcceleratorMemoryRead> reads,
        AcceleratorGuardDecision guardDecision,
        string message)
    {
        ArgumentNullException.ThrowIfNull(reads);
        return new AcceleratorMemoryPortalReadResult(
            isAccepted: true,
            AcceleratorTokenFaultCode.None,
            guardDecision,
            FreezeReads(reads),
            message);
    }

    public static AcceleratorMemoryPortalReadResult Reject(
        AcceleratorTokenFaultCode faultCode,
        AcceleratorGuardDecision? guardDecision,
        string message)
    {
        if (faultCode == AcceleratorTokenFaultCode.None)
        {
            throw new ArgumentException(
                "Rejected L7-SDC memory portal reads require a fault code.",
                nameof(faultCode));
        }

        return new AcceleratorMemoryPortalReadResult(
            isAccepted: false,
            faultCode,
            guardDecision,
            Array.Empty<AcceleratorMemoryRead>(),
            message);
    }

    private static IReadOnlyList<AcceleratorMemoryRead> FreezeReads(
        IReadOnlyList<AcceleratorMemoryRead> reads)
    {
        if (reads.Count == 0)
        {
            return Array.Empty<AcceleratorMemoryRead>();
        }

        var copy = new AcceleratorMemoryRead[reads.Count];
        for (int index = 0; index < reads.Count; index++)
        {
            AcceleratorMemoryRead read = reads[index];
            copy[index] = read with
            {
                Data = CopyMemory(read.Data)
            };
        }

        return Array.AsReadOnly(copy);
    }

    private static ReadOnlyMemory<byte> CopyMemory(ReadOnlyMemory<byte> data)
    {
        byte[] copy = data.ToArray();
        return copy;
    }
}

public sealed record AcceleratorStagingResult
{
    private AcceleratorStagingResult(
        bool isAccepted,
        AcceleratorTokenFaultCode faultCode,
        AcceleratorGuardDecision? guardDecision,
        AcceleratorStagedWrite? stagedWrite,
        string message)
    {
        IsAccepted = isAccepted;
        FaultCode = faultCode;
        GuardDecision = guardDecision;
        StagedWrite = stagedWrite;
        Message = message;
    }

    public bool IsAccepted { get; }

    public bool IsRejected => !IsAccepted;

    public AcceleratorTokenFaultCode FaultCode { get; }

    public AcceleratorGuardDecision? GuardDecision { get; }

    public AcceleratorStagedWrite? StagedWrite { get; }

    public string Message { get; }

    public bool CanPublishArchitecturalMemory => false;

    public bool CanPublishException => false;

    public static AcceleratorStagingResult Accepted(
        AcceleratorStagedWrite stagedWrite,
        AcceleratorGuardDecision guardDecision,
        string message)
    {
        ArgumentNullException.ThrowIfNull(stagedWrite);
        return new AcceleratorStagingResult(
            isAccepted: true,
            AcceleratorTokenFaultCode.None,
            guardDecision,
            stagedWrite,
            message);
    }

    public static AcceleratorStagingResult Reject(
        AcceleratorTokenFaultCode faultCode,
        AcceleratorGuardDecision? guardDecision,
        string message)
    {
        if (faultCode == AcceleratorTokenFaultCode.None)
        {
            throw new ArgumentException(
                "Rejected L7-SDC staging operations require a fault code.",
                nameof(faultCode));
        }

        return new AcceleratorStagingResult(
            isAccepted: false,
            faultCode,
            guardDecision,
            stagedWrite: null,
            message);
    }
}

public sealed record AcceleratorStagingReadResult
{
    private AcceleratorStagingReadResult(
        bool isAccepted,
        AcceleratorTokenFaultCode faultCode,
        AcceleratorGuardDecision? guardDecision,
        IReadOnlyList<AcceleratorStagedWrite> stagedWrites,
        string message)
    {
        IsAccepted = isAccepted;
        FaultCode = faultCode;
        GuardDecision = guardDecision;
        StagedWrites = stagedWrites;
        Message = message;
    }

    public bool IsAccepted { get; }

    public bool IsRejected => !IsAccepted;

    public AcceleratorTokenFaultCode FaultCode { get; }

    public AcceleratorGuardDecision? GuardDecision { get; }

    public IReadOnlyList<AcceleratorStagedWrite> StagedWrites { get; }

    public string Message { get; }

    public bool CanPublishArchitecturalMemory => false;

    public bool CanPublishException => false;

    public static AcceleratorStagingReadResult Accepted(
        IReadOnlyList<AcceleratorStagedWrite> stagedWrites,
        AcceleratorGuardDecision guardDecision,
        string message)
    {
        ArgumentNullException.ThrowIfNull(stagedWrites);
        return new AcceleratorStagingReadResult(
            isAccepted: true,
            AcceleratorTokenFaultCode.None,
            guardDecision,
            FreezeStagedWrites(stagedWrites),
            message);
    }

    public static AcceleratorStagingReadResult Reject(
        AcceleratorTokenFaultCode faultCode,
        AcceleratorGuardDecision? guardDecision,
        string message)
    {
        if (faultCode == AcceleratorTokenFaultCode.None)
        {
            throw new ArgumentException(
                "Rejected L7-SDC staging read operations require a fault code.",
                nameof(faultCode));
        }

        return new AcceleratorStagingReadResult(
            isAccepted: false,
            faultCode,
            guardDecision,
            Array.Empty<AcceleratorStagedWrite>(),
            message);
    }

    private static IReadOnlyList<AcceleratorStagedWrite> FreezeStagedWrites(
        IReadOnlyList<AcceleratorStagedWrite> stagedWrites)
    {
        if (stagedWrites.Count == 0)
        {
            return Array.Empty<AcceleratorStagedWrite>();
        }

        var copy = new AcceleratorStagedWrite[stagedWrites.Count];
        for (int index = 0; index < stagedWrites.Count; index++)
        {
            AcceleratorStagedWrite stagedWrite = stagedWrites[index];
            copy[index] = stagedWrite with
            {
                Data = stagedWrite.Data.ToArray()
            };
        }

        return Array.AsReadOnly(copy);
    }
}

public interface IAcceleratorMemoryPortal
{
    AcceleratorMemoryPortalReadResult ReadSourceRanges(
        AcceleratorToken token,
        AcceleratorCommandDescriptor descriptor,
        AcceleratorGuardEvidence? currentGuardEvidence);
}

public interface IAcceleratorStagingBuffer
{
    AcceleratorStagingResult StageWrite(
        AcceleratorToken token,
        AcceleratorMemoryRange destinationRange,
        ReadOnlySpan<byte> data,
        AcceleratorGuardEvidence? currentGuardEvidence);

    AcceleratorStagingReadResult GetStagedWriteSet(
        AcceleratorToken token,
        AcceleratorGuardEvidence? currentGuardEvidence);
}

public sealed class MainMemoryReadOnlyAcceleratorMemoryPortal : IAcceleratorMemoryPortal
{
    private readonly Processor.MainMemoryArea _mainMemory;

    public MainMemoryReadOnlyAcceleratorMemoryPortal(Processor.MainMemoryArea mainMemory)
    {
        _mainMemory = mainMemory ?? throw new ArgumentNullException(nameof(mainMemory));
    }

    public AcceleratorMemoryPortalReadResult ReadSourceRanges(
        AcceleratorToken token,
        AcceleratorCommandDescriptor descriptor,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(descriptor);
        if ((!ReferenceEquals(token.Descriptor, descriptor) && !token.Descriptor.Equals(descriptor)) ||
            token.State != AcceleratorTokenState.Running)
        {
            return AcceleratorMemoryPortalReadResult.Reject(
                AcceleratorTokenFaultCode.SourceReadRejected,
                guardDecision: null,
                "L7-SDC source reads require the token-bound descriptor in Running state; descriptor or token identity alone is not device-read authority.");
        }

        AcceleratorGuardDecision tokenGuardDecision =
            AcceleratorOwnerDomainGuard.Default.ValidateMappingEpoch(
                token.SubmitGuardDecision,
                currentGuardEvidence);
        if (!tokenGuardDecision.IsAllowed)
        {
            return AcceleratorMemoryPortalReadResult.Reject(
                AcceleratorTokenStore.MapGuardFault(tokenGuardDecision.Fault),
                tokenGuardDecision,
                "L7-SDC source reads require fresh token-bound owner/domain and epoch authority. " +
                tokenGuardDecision.Message);
        }

        AcceleratorGuardDecision guardDecision =
            AcceleratorOwnerDomainGuard.Default.EnsureBeforeDeviceExecution(
                descriptor,
                currentGuardEvidence);
        if (!guardDecision.IsAllowed)
        {
            return AcceleratorMemoryPortalReadResult.Reject(
                AcceleratorTokenStore.MapGuardFault(guardDecision.Fault),
                guardDecision,
                "L7-SDC source reads require fresh guard-backed device execution authority. " +
                guardDecision.Message);
        }

        if (tokenGuardDecision.DescriptorOwnerBinding is null ||
            guardDecision.DescriptorOwnerBinding is null ||
            !tokenGuardDecision.DescriptorOwnerBinding.Equals(guardDecision.DescriptorOwnerBinding))
        {
            return AcceleratorMemoryPortalReadResult.Reject(
                AcceleratorTokenFaultCode.SourceReadRejected,
                guardDecision,
                "L7-SDC source read token guard and device execution guard must bind the same descriptor owner.");
        }

        var reads = new List<AcceleratorMemoryRead>(descriptor.SourceRanges.Count);
        for (int index = 0; index < descriptor.SourceRanges.Count; index++)
        {
            AcceleratorMemoryRange range = descriptor.SourceRanges[index];
            if (range.Length > int.MaxValue ||
                !HasExactMainMemoryRange(range.Address, checked((int)range.Length)))
            {
                return AcceleratorMemoryPortalReadResult.Reject(
                    AcceleratorTokenFaultCode.SourceReadRejected,
                    guardDecision,
                    $"L7-SDC read-only memory portal rejected out-of-range source read at 0x{range.Address:X} for {range.Length} byte(s).");
            }

            byte[] buffer = new byte[(int)range.Length];
            if (!_mainMemory.TryReadPhysicalRange(range.Address, buffer))
            {
                return AcceleratorMemoryPortalReadResult.Reject(
                    AcceleratorTokenFaultCode.SourceReadRejected,
                    guardDecision,
                    $"L7-SDC read-only memory portal could not read source range at 0x{range.Address:X} for {range.Length} byte(s).");
            }

            reads.Add(new AcceleratorMemoryRead(range, buffer));
        }

        return AcceleratorMemoryPortalReadResult.Accepted(
            reads,
            guardDecision,
            "L7-SDC source ranges read through a guarded read-only memory portal.");
    }

    private bool HasExactMainMemoryRange(ulong address, int size)
    {
        if (size <= 0)
        {
            return false;
        }

        ulong memoryLength = (ulong)_mainMemory.Length;
        ulong requestSize = (ulong)size;
        return requestSize <= memoryLength &&
               address <= memoryLength - requestSize;
    }
}

public sealed class AcceleratorStagingBuffer : IAcceleratorStagingBuffer
{
    private readonly Dictionary<ulong, List<AcceleratorStagedWrite>> _writesByHandle = new();

    public int TotalStagedWriteCount
    {
        get
        {
            int count = 0;
            foreach (List<AcceleratorStagedWrite> writes in _writesByHandle.Values)
            {
                count += writes.Count;
            }

            return count;
        }
    }

    public AcceleratorStagingResult StageWrite(
        AcceleratorToken token,
        AcceleratorMemoryRange destinationRange,
        ReadOnlySpan<byte> data,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (token.State != AcceleratorTokenState.Running)
        {
            return AcceleratorStagingResult.Reject(
                AcceleratorTokenFaultCode.StagingRejected,
                guardDecision: null,
                "L7-SDC staged writes require a Running token produced by guarded queue/backend execution.");
        }

        AcceleratorGuardDecision guardDecision =
            AcceleratorOwnerDomainGuard.Default.ValidateMappingEpoch(
                token.SubmitGuardDecision,
                currentGuardEvidence);
        if (!guardDecision.IsAllowed)
        {
            return AcceleratorStagingResult.Reject(
                AcceleratorTokenStore.MapGuardFault(guardDecision.Fault),
                guardDecision,
                "L7-SDC staged writes require fresh token owner/domain and epoch authority. " +
                guardDecision.Message);
        }

        if ((ulong)data.Length != destinationRange.Length ||
            !IsRangeCoveredByFootprint(
                destinationRange,
                token.Descriptor.NormalizedFootprint.DestinationRanges))
        {
            return AcceleratorStagingResult.Reject(
                AcceleratorTokenFaultCode.StagingRejected,
                guardDecision,
                "L7-SDC fake backend staged write must exactly target the accepted normalized destination footprint.");
        }

        byte[] copy = data.ToArray();
        var stagedWrite = new AcceleratorStagedWrite(
            token.Handle,
            destinationRange.Address,
            copy);
        if (!_writesByHandle.TryGetValue(token.Handle.Value, out List<AcceleratorStagedWrite>? writes))
        {
            writes = new List<AcceleratorStagedWrite>();
            _writesByHandle.Add(token.Handle.Value, writes);
        }

        writes.Add(stagedWrite);
        return AcceleratorStagingResult.Accepted(
            stagedWrite,
            guardDecision,
            "L7-SDC fake backend staged bytes into backend-private staging only; no architectural memory was published.");
    }

    public AcceleratorStagingReadResult GetStagedWriteSet(
        AcceleratorToken token,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(token);
        AcceleratorGuardDecision guardDecision =
            AcceleratorOwnerDomainGuard.Default.ValidateMappingEpoch(
                token.SubmitGuardDecision,
                currentGuardEvidence);
        if (!guardDecision.IsAllowed)
        {
            return AcceleratorStagingReadResult.Reject(
                AcceleratorTokenStore.MapGuardFault(guardDecision.Fault),
                guardDecision,
                "L7-SDC staging observation requires fresh token owner/domain and epoch authority. " +
                guardDecision.Message);
        }

        if (!token.Handle.IsValid ||
            !_writesByHandle.TryGetValue(token.Handle.Value, out List<AcceleratorStagedWrite>? writes) ||
            writes.Count == 0)
        {
            return AcceleratorStagingReadResult.Accepted(
                Array.Empty<AcceleratorStagedWrite>(),
                guardDecision,
                "L7-SDC staging buffer has no staged writes for the guarded token.");
        }

        return AcceleratorStagingReadResult.Accepted(
            writes,
            guardDecision,
            "L7-SDC staging write-set observed after token guard revalidation; data remains non-architectural.");
    }

    private static bool IsRangeCoveredByFootprint(
        AcceleratorMemoryRange range,
        IReadOnlyList<AcceleratorMemoryRange> footprint)
    {
        if (range.Length == 0 ||
            range.Address > ulong.MaxValue - range.Length ||
            footprint is null ||
            footprint.Count == 0)
        {
            return false;
        }

        ulong end = range.Address + range.Length;
        for (int index = 0; index < footprint.Count; index++)
        {
            AcceleratorMemoryRange accepted = footprint[index];
            if (accepted.Length == 0 ||
                accepted.Address > ulong.MaxValue - accepted.Length)
            {
                continue;
            }

            ulong acceptedEnd = accepted.Address + accepted.Length;
            if (range.Address >= accepted.Address && end <= acceptedEnd)
            {
                return true;
            }
        }

        return false;
    }
}
