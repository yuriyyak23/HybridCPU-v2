using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core.Vmcs.V2;

public enum VmcsV2GprPersistenceKind : byte
{
    None = 0,
    LazyPending = 1,
    Complete = 2,
}

public sealed class VmxRootControlBlock
{
    public bool IsReadOnlyCompatibilityProjection => true;

    public ulong RootDescriptorAddress => 0;

    public ulong OwnershipEpoch => 0;
}

public sealed class VmxNptBlock
{
    public bool IsReadOnlyCompatibilityProjection => true;

    public MemoryTranslationControl LastControl => MemoryTranslationControl.Disabled;

    public ulong ControlEpoch => 0;
}

public sealed class ExitInfoBlock
{
    public VmExitReason ExitReason { get; private set; }

    public ulong ExitQualification { get; private set; }

    public ulong GuestPhysicalAddress { get; private set; }

    public ulong EptViolationQualification { get; private set; }

    public TranslationViolationInfo NptViolation { get; private set; }

    internal void RecordVectorException(VectorStreamExceptionInfo info)
    {
        ExitReason = VmExitReason.VectorException;
        ExitQualification = info.EncodeCompatibilityQualification();
        GuestPhysicalAddress = info.FaultingPc;
        EptViolationQualification = 0;
        NptViolation = default;
    }

    internal void RecordStreamDescriptorFault(VectorStreamDescriptorFaultInfo info)
    {
        ExitReason = VmExitReason.StreamDescriptorFault;
        ExitQualification = info.EncodeCompatibilityQualification();
        GuestPhysicalAddress = info.GuestDescriptorAddress;
        EptViolationQualification = 0;
        NptViolation = default;
    }

    internal void RecordStreamReplayRequired(
        ushort ownerVirtualThreadId,
        ushort addressSpaceTag,
        ulong streamReplayEpoch)
    {
        ExitReason = VmExitReason.StreamReplayRequired;
        ExitQualification =
            (((ulong)ownerVirtualThreadId & 0xFFFFUL) << 16) |
            (((ulong)addressSpaceTag & 0xFFFFUL) << 32);
        GuestPhysicalAddress = streamReplayEpoch;
        EptViolationQualification = 0;
        NptViolation = default;
    }

}

public sealed class VirtualCpuBlock
{
    public const int IntegerRegisterCount = 32;

    private readonly ulong[] _guestIntegerRegisters = new ulong[IntegerRegisterCount];

    public ulong GuestPc { get; private set; }

    public ulong GuestSp { get; private set; }

    public VmcsV2GprPersistenceKind GprPersistence { get; private set; }

    public IReadOnlyList<ulong> GuestIntegerRegisters => _guestIntegerRegisters;

}

public sealed class BundleExecutionBlock
{
    public bool IsReadOnlyCompatibilityProjection => true;

    public ulong BundlePc => 0;

    public ulong ExecutionEpoch => 0;
}

public sealed class VmxPreemptionTimerBlock
{
    public bool IsReadOnlyCompatibilityProjection => true;

    public ulong TimerEpoch => 0;
}

public sealed class VirtualInterruptFabricBlock
{
    public bool IsReadOnlyCompatibilityProjection => true;

    public ulong RoutingEpoch => 0;
}

public sealed class InterceptBitmapBlock
{
    public bool IsReadOnlyCompatibilityProjection => true;

    public ulong PolicyEpoch => 0;
}

public sealed class EventInjectionBlock
{
    public bool IsReadOnlyCompatibilityProjection => true;

    public ulong InjectionEpoch => 0;

    public ulong DeliveryEpoch => 0;

    public EventInjectionDescriptor LastDelivered => default;

    public int PendingCount => 0;

    public ulong DroppedCount => 0;

    public ulong InvalidDroppedCount => 0;

    public ulong CapacityDroppedCount => 0;

    public ulong RemapDroppedCount => 0;

    public ulong RemappedCount => 0;

    public ulong CoalescedCount => 0;

    public ulong RemapPolicyEpoch => 0;
}

public sealed class LaneCompletionRoutingBlock
{
    public bool IsReadOnlyCompatibilityProjection => true;

    public ulong RoutingEpoch => 0;

    public ulong RoutedCount => 0;

    public ulong IgnoredDisabledCount => 0;

    public ulong InvalidDroppedCount => 0;

    public ulong CapacityDroppedCount => 0;

    public ulong EventQueueDroppedCount => 0;
}

public sealed class VectorStreamStateBlock
{
    public const uint DefaultMaxStreamLength = 32;

    public bool IsReadOnlyCompatibilityProjection => true;

    public bool VirtualizationEnabled => false;

    public VectorStreamSaveMask SaveRestoreMask => VectorStreamSaveMask.None;

    public VectorExceptionAction ExceptionPolicy => VectorExceptionAction.Accumulate;

    public ulong StreamDescriptorTableBase => 0;

    public ulong StreamDescriptorTableLimit => 0;

    public uint MaxStreamLength => DefaultMaxStreamLength;

    public ulong StreamEpoch => 0;

    public ulong PolicyEpoch => 0;

    public ulong ArchitecturalEpoch => 0;

    public ulong DirtyEpoch => 0;

    public ulong StreamReplayEpoch => 0;

    public ulong StreamQueueEpoch => 0;

    public ulong StreamCompletionEpoch => 0;

    public VectorStreamSnapshot? LastSnapshot => null;

    public VectorStreamExceptionInfo LastVectorException => default;

    public VectorStreamDescriptorFaultInfo LastDescriptorFault => VectorStreamDescriptorFaultInfo.None;

    public bool HasDescriptorTable =>
        StreamDescriptorTableLimit > StreamDescriptorTableBase;

    public bool RequiresMigratableVectorState =>
        VirtualizationEnabled &&
        (SaveRestoreMask & VectorStreamSaveMask.Architectural) != VectorStreamSaveMask.None;

    public bool IsMigrationReady =>
        !RequiresMigratableVectorState || LastSnapshot is not null;
}

public sealed class SecurityIsolationBlock
{
    public bool IsReadOnlyCompatibilityProjection => true;

    public ulong IsolationEpoch => 0;
}

public sealed class CapabilityNegotiationBlock
{
    public bool IsReadOnlyCompatibilityProjection => true;

    public ulong CapabilityEpoch => 0;
}

public sealed class DirtyLogBlock
{
    public ushort Version => 1;

    public bool IsReadOnlyCompatibilityProjection => true;

    public bool Enabled => false;

    public ulong GuestPhysicalBase => 0;

    public ulong GuestPhysicalLimitExclusive => 0;

    public uint PageSize => VmxDirtyLogConfiguration.DefaultPageSize;

    public uint MaxDirtyPages => VmxDirtyLogConfiguration.DefaultMaxDirtyPages;

    public VmxDirtyLogOverflowPolicy OverflowPolicy => VmxDirtyLogOverflowPolicy.FailClosed;

    public ulong Generation => 0;

    public ulong WriteSequence => 0;

    public bool Overflowed => false;

    public ulong CpuWriteCount => 0;

    public ulong AtomicWriteCount => 0;

    public ulong DmaWriteCount => 0;

    public ulong Lane6WriteCount => 0;

    public ulong Lane7WriteCount => 0;

    public ulong NptWriteProtectCount => 0;

    public ulong DroppedWriteCount => 0;

    public bool ContainsHostEvidence(VmcsV2HostEvidenceKind evidence) => false;

    public VmxDirtyLogStatus SnapshotStatus() =>
        new(
            Enabled,
            PageSize,
            Generation,
            WriteSequence,
            Overflowed,
            DirtyPageCount: 0,
            CpuWriteCount,
            AtomicWriteCount,
            DmaWriteCount,
            Lane6WriteCount,
            Lane7WriteCount,
            NptWriteProtectCount,
            DroppedWriteCount);
}

public sealed class DebugTraceBlock
{
    public ushort Version => 1;

    public bool IsReadOnlyCompatibilityProjection => true;

    public bool ExportEnabled => false;

    public ulong PolicyEpoch => 0;

    public VmxDebugTraceCounters Counters => default;

    public bool ContainsHostEvidence(VmcsV2HostEvidenceKind evidence) => false;
}
