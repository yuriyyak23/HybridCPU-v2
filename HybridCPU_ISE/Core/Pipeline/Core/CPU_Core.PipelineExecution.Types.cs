using System;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
                private enum ScalarExceptionDeliveryKind : byte
                {
                    None = 0,
                    SilentSpeculativeSquash = 1,
                    PreciseArchitecturalFault = 2
                }

                private readonly struct ScalarExceptionOrderingDecision
                {
                    public ScalarExceptionDeliveryKind DeliveryKind { get; }
                    public int VirtualThreadId { get; }
                    public ulong FaultingPC { get; }
                    public ulong OperationDomainTag { get; }
                    public ulong ActiveCert { get; }

                    public bool IsSilentSpeculativeSquash => DeliveryKind == ScalarExceptionDeliveryKind.SilentSpeculativeSquash;
                    public bool IsPreciseArchitecturalFault => DeliveryKind == ScalarExceptionDeliveryKind.PreciseArchitecturalFault;

                    private ScalarExceptionOrderingDecision(
                        ScalarExceptionDeliveryKind deliveryKind,
                        int virtualThreadId,
                        ulong faultingPc,
                        ulong operationDomainTag,
                        ulong activeCert)
                    {
                        DeliveryKind = deliveryKind;
                        VirtualThreadId = virtualThreadId;
                        FaultingPC = faultingPc;
                        OperationDomainTag = operationDomainTag;
                        ActiveCert = activeCert;
                    }

                    public static ScalarExceptionOrderingDecision None()
                    {
                        return new ScalarExceptionOrderingDecision(
                            ScalarExceptionDeliveryKind.None,
                            0,
                            0,
                            0,
                            0);
                    }

                    public static ScalarExceptionOrderingDecision SilentSpeculativeDomainSquash(
                        int virtualThreadId,
                        ulong faultingPc,
                        ulong operationDomainTag,
                        ulong activeCert)
                    {
                        return new ScalarExceptionOrderingDecision(
                            ScalarExceptionDeliveryKind.SilentSpeculativeSquash,
                            virtualThreadId,
                            faultingPc,
                            operationDomainTag,
                            activeCert);
                    }

                    public static ScalarExceptionOrderingDecision PreciseDomainFault(
                        int virtualThreadId,
                        ulong faultingPc,
                        ulong operationDomainTag,
                        ulong activeCert)
                    {
                        return new ScalarExceptionOrderingDecision(
                            ScalarExceptionDeliveryKind.PreciseArchitecturalFault,
                            virtualThreadId,
                            faultingPc,
                            operationDomainTag,
                            activeCert);
                    }
                }

                private enum DecodeStageDiagnosticsKind : byte
                {
                    None = 0,
                    NoInput = 1,
                    SilentSpeculativeSquash = 2,
                    BankPending = 3,
                    Issued = 4
                }

                private readonly struct DecodeStageResult
                {
                    private DecodeStageResult(
                        bool canAdvance,
                        PipelineStallKind stallReason,
                        bool bankConflict,
                        byte issuedSlots,
                        byte rejectedSlots,
                        ulong bundlePc,
                        DecodeStageDiagnosticsKind diagnostics)
                    {
                        CanAdvance = canAdvance;
                        StallReason = stallReason;
                        BankConflict = bankConflict;
                        IssuedSlots = issuedSlots;
                        RejectedSlots = rejectedSlots;
                        BundlePc = bundlePc;
                        Diagnostics = diagnostics;
                    }

                    public bool CanAdvance { get; }

                    public PipelineStallKind StallReason { get; }

                    public bool BankConflict { get; }

                    public byte IssuedSlots { get; }

                    public byte RejectedSlots { get; }

                    public ulong BundlePc { get; }

                    public DecodeStageDiagnosticsKind Diagnostics { get; }

                    public bool ShouldStall => !CanAdvance;

                    public static DecodeStageResult NoProgress(
                        ulong bundlePc,
                        DecodeStageDiagnosticsKind diagnostics = DecodeStageDiagnosticsKind.None,
                        byte rejectedSlots = 0)
                    {
                        return new DecodeStageResult(
                            canAdvance: true,
                            PipelineStallKind.None,
                            bankConflict: false,
                            issuedSlots: 0,
                            rejectedSlots: rejectedSlots,
                            bundlePc: bundlePc,
                            diagnostics: diagnostics);
                    }

                    public static DecodeStageResult Issued(
                        ulong bundlePc,
                        byte issuedSlots,
                        byte rejectedSlots = 0)
                    {
                        return new DecodeStageResult(
                            canAdvance: true,
                            PipelineStallKind.None,
                            bankConflict: false,
                            issuedSlots: issuedSlots,
                            rejectedSlots: rejectedSlots,
                            bundlePc: bundlePc,
                            diagnostics: DecodeStageDiagnosticsKind.Issued);
                    }

                    public static DecodeStageResult Stall(
                        ulong bundlePc,
                        PipelineStallKind stallReason,
                        bool bankConflict,
                        byte rejectedSlots,
                        DecodeStageDiagnosticsKind diagnostics)
                    {
                        if (stallReason == PipelineStallKind.None)
                        {
                            throw new InvalidOperationException(
                                "Decode-stage stall result cannot carry PipelineStallKind.None.");
                        }

                        return new DecodeStageResult(
                            canAdvance: false,
                            stallReason,
                            bankConflict,
                            issuedSlots: 0,
                            rejectedSlots: rejectedSlots,
                            bundlePc: bundlePc,
                            diagnostics: diagnostics);
                    }
                }

                private readonly struct PipelineCycleStallDecision
                {
                    private PipelineCycleStallDecision(
                        PipelineStallKind stallReason,
                        bool countMemoryStall,
                        bool countInvariantViolation,
                        bool countMshrScoreboardStall,
                        bool countBankConflictStall)
                    {
                        StallReason = stallReason;
                        CountMemoryStall = countMemoryStall;
                        CountInvariantViolation = countInvariantViolation;
                        CountMshrScoreboardStall = countMshrScoreboardStall;
                        CountBankConflictStall = countBankConflictStall;
                    }

                    public PipelineStallKind StallReason { get; }

                    public bool CountMemoryStall { get; }

                    public bool CountInvariantViolation { get; }

                    public bool CountMshrScoreboardStall { get; }

                    public bool CountBankConflictStall { get; }

                    public bool ShouldStall => StallReason != PipelineStallKind.None;

                    public static PipelineCycleStallDecision None() =>
                        new(
                            PipelineStallKind.None,
                            countMemoryStall: false,
                            countInvariantViolation: false,
                            countMshrScoreboardStall: false,
                            countBankConflictStall: false);

                    public static PipelineCycleStallDecision ForKind(PipelineStallKind stallReason)
                    {
                        if (stallReason == PipelineStallKind.None)
                        {
                            return None();
                        }

                        return new(
                            stallReason,
                            countMemoryStall: false,
                            countInvariantViolation: false,
                            countMshrScoreboardStall: false,
                            countBankConflictStall: false);
                    }

                    public static PipelineCycleStallDecision MemoryWait(
                        bool countMemoryStall,
                        bool countMshrScoreboardStall,
                        bool countBankConflictStall) =>
                        new(
                            PipelineStallKind.MemoryWait,
                            countMemoryStall,
                            countInvariantViolation: false,
                            countMshrScoreboardStall,
                            countBankConflictStall);

                    public static PipelineCycleStallDecision InvariantViolation() =>
                        new(
                            PipelineStallKind.InvariantViolation,
                            countMemoryStall: false,
                            countInvariantViolation: true,
                            countMshrScoreboardStall: false,
                            countBankConflictStall: false);

                    public static PipelineCycleStallDecision FromDecode(in DecodeStageResult decodeStageResult) =>
                        new(
                            decodeStageResult.StallReason,
                            countMemoryStall: false,
                            countInvariantViolation: false,
                            countMshrScoreboardStall: decodeStageResult.BankConflict,
                            countBankConflictStall: decodeStageResult.BankConflict);
                }

                private const int GeneratedRetireRecordCapacity = 2;
                internal const int DirectRetirePublicationRetireRecordCapacity = 3;
                private const int RetireWindowEffectCapacity = 8;

                internal enum RetireWindowEffectKind : byte
                {
                    None = 0,
                    DeferredStoreCommit = 1,
                    Csr = 2,
                    VectorConfig = 3,
                    Atomic = 4,
                    System = 5,
                    Vmx = 6,
                    SerializingBoundary = 7,
                    PipelineEvent = 8,
                    ScalarMemoryStore = 9,
                    PredicateState = 10
                }

                private enum RetireWindowTypedEffectKind : byte
                {
                    None = 0,
                    Csr = 1,
                    VectorConfig = 2,
                    System = 3,
                    Vmx = 4,
                    ScalarMemoryStore = 5,
                    PredicateState = 6
                }

                internal readonly struct RetiredTraceLaneSnapshot
                {
                    private RetiredTraceLaneSnapshot(
                        ulong pc,
                        uint opCode,
                        ulong fallbackValue,
                        int ownerThreadId,
                        int virtualThreadId,
                        bool wasFspInjected,
                        int originalThreadId)
                    {
                        PC = pc;
                        OpCode = opCode;
                        FallbackValue = fallbackValue;
                        OwnerThreadId = ownerThreadId;
                        VirtualThreadId = virtualThreadId;
                        WasFspInjected = wasFspInjected;
                        OriginalThreadId = originalThreadId;
                    }

                    public ulong PC { get; }

                    public uint OpCode { get; }

                    public ulong FallbackValue { get; }

                    public int OwnerThreadId { get; }

                    public int VirtualThreadId { get; }

                    public bool WasFspInjected { get; }

                    public int OriginalThreadId { get; }

                    public static RetiredTraceLaneSnapshot FromLane(in ScalarWriteBackLaneState lane) =>
                        new(
                            lane.PC,
                            lane.OpCode,
                            CPU_Core.ResolveRetiredTraceValue(lane),
                            lane.OwnerThreadId,
                            lane.VirtualThreadId,
                            lane.WasFspInjected,
                            lane.OriginalThreadId);
                }

                internal readonly struct RetireWindowEffect
                {
                    private const byte NoPipelineEventSlot = byte.MaxValue;

                    private RetireWindowEffect(
                        RetireWindowEffectKind kind,
                        byte deferredStoreLaneIndex,
                        Core.CsrRetireEffect csrEffect,
                        Core.VectorConfigRetireEffect vectorConfigEffect,
                        Core.AtomicRetireEffect atomicEffect,
                        Core.SystemEventKind systemEventKind,
                        byte pipelineEventSlot,
                        Core.SystemEventOrderGuarantee systemEventOrderGuarantee,
                        ulong systemEventPc,
                        int systemEventVtId,
                        ulong memoryAddress,
                        ulong memoryData,
                        byte memoryAccessSize,
                        byte predicateRegisterId,
                        ulong predicateMaskValue,
                        in RetiredTraceLaneSnapshot atomicTraceLane,
                        bool hasAtomicTraceLane,
                        Core.VmxRetireEffect vmxEffect,
                        int vmxEffectVtId,
                        in RetiredTraceLaneSnapshot vmxTraceLane,
                        bool hasVmxTraceLane)
                    {
                        Kind = kind;
                        DeferredStoreLaneIndex = deferredStoreLaneIndex;
                        CsrEffect = csrEffect;
                        VectorConfigEffect = vectorConfigEffect;
                        AtomicEffect = atomicEffect;
                        SystemEventKind = systemEventKind;
                        PipelineEventSlot = pipelineEventSlot;
                        SystemEventOrderGuarantee = systemEventOrderGuarantee;
                        SystemEventPc = systemEventPc;
                        SystemEventVtId = systemEventVtId;
                        MemoryAddress = memoryAddress;
                        MemoryData = memoryData;
                        MemoryAccessSize = memoryAccessSize;
                        PredicateRegisterId = predicateRegisterId;
                        PredicateMaskValue = predicateMaskValue;
                        AtomicTraceLane = atomicTraceLane;
                        HasAtomicTraceLane = hasAtomicTraceLane;
                        VmxEffect = vmxEffect;
                        VmxEffectVtId = vmxEffectVtId;
                        VmxTraceLane = vmxTraceLane;
                        HasVmxTraceLane = hasVmxTraceLane;
                    }

                    public RetireWindowEffectKind Kind { get; }

                    public byte DeferredStoreLaneIndex { get; }

                    public Core.CsrRetireEffect CsrEffect { get; }

                    public Core.VectorConfigRetireEffect VectorConfigEffect { get; }

                    public Core.AtomicRetireEffect AtomicEffect { get; }

                    public Core.SystemEventKind SystemEventKind { get; }

                    public byte PipelineEventSlot { get; }

                    public Core.SystemEventOrderGuarantee SystemEventOrderGuarantee { get; }

                    public ulong SystemEventPc { get; }

                    public int SystemEventVtId { get; }

                    public ulong MemoryAddress { get; }

                    public ulong MemoryData { get; }

                    public byte MemoryAccessSize { get; }

                    public byte PredicateRegisterId { get; }

                    public ulong PredicateMaskValue { get; }

                    public RetiredTraceLaneSnapshot AtomicTraceLane { get; }

                    public bool HasAtomicTraceLane { get; }

                    public Core.VmxRetireEffect VmxEffect { get; }

                    public int VmxEffectVtId { get; }

                    public RetiredTraceLaneSnapshot VmxTraceLane { get; }

                    public bool HasVmxTraceLane { get; }

                    public static RetireWindowEffect DeferredStoreCommit(byte laneIndex) =>
                        new(
                            RetireWindowEffectKind.DeferredStoreCommit,
                            laneIndex,
                            default,
                            default,
                            default,
                            default,
                            NoPipelineEventSlot,
                            Core.SystemEventOrderGuarantee.None,
                            0,
                            0,
                            0,
                            0,
                            0,
                            0,
                            0,
                            default,
                            hasAtomicTraceLane: false,
                            default,
                            0,
                            default,
                            hasVmxTraceLane: false);

                    public static RetireWindowEffect Csr(in Core.CsrRetireEffect csrEffect) =>
                        new(
                            RetireWindowEffectKind.Csr,
                            0,
                            csrEffect,
                            default,
                            default,
                            default,
                            NoPipelineEventSlot,
                            Core.SystemEventOrderGuarantee.None,
                            0,
                            0,
                            0,
                            0,
                            0,
                            0,
                            0,
                            default,
                            hasAtomicTraceLane: false,
                            default,
                            0,
                            default,
                            hasVmxTraceLane: false);

                    public static RetireWindowEffect VectorConfig(
                        in Core.VectorConfigRetireEffect vectorConfigEffect) =>
                        new(
                            RetireWindowEffectKind.VectorConfig,
                            0,
                            default,
                            vectorConfigEffect,
                            default,
                            default,
                            NoPipelineEventSlot,
                            Core.SystemEventOrderGuarantee.None,
                            0,
                            0,
                            0,
                            0,
                            0,
                            0,
                            0,
                            default,
                            hasAtomicTraceLane: false,
                            default,
                            0,
                            default,
                            hasVmxTraceLane: false);

                    public static RetireWindowEffect Atomic(
                        in Core.AtomicRetireEffect atomicEffect,
                        in RetiredTraceLaneSnapshot traceLane,
                        bool hasTraceLane) =>
                        new(
                            RetireWindowEffectKind.Atomic,
                            0,
                            default,
                            default,
                            atomicEffect,
                            default,
                            NoPipelineEventSlot,
                            Core.SystemEventOrderGuarantee.None,
                            0,
                            0,
                            0,
                            0,
                            0,
                            0,
                            0,
                            traceLane,
                            hasTraceLane,
                            default,
                            0,
                            default,
                            hasVmxTraceLane: false);

                    public static RetireWindowEffect System(
                        Core.SystemEventKind systemEventKind,
                        Core.SystemEventOrderGuarantee orderGuarantee,
                        ulong retiredPc,
                        int virtualThreadId) =>
                        new(
                            RetireWindowEffectKind.System,
                            0,
                            default,
                            default,
                            default,
                            systemEventKind,
                            NoPipelineEventSlot,
                            orderGuarantee,
                            retiredPc,
                            virtualThreadId,
                            0,
                            0,
                            0,
                            0,
                            0,
                            default,
                            hasAtomicTraceLane: false,
                            default,
                            0,
                            default,
                            hasVmxTraceLane: false);

                    public static RetireWindowEffect PipelineEvent(
                        byte pipelineEventSlot,
                        Core.SystemEventOrderGuarantee orderGuarantee,
                        ulong retiredPc,
                        int virtualThreadId) =>
                        new(
                            RetireWindowEffectKind.PipelineEvent,
                            0,
                            default,
                            default,
                            default,
                            default,
                            pipelineEventSlot,
                            orderGuarantee,
                            retiredPc,
                            virtualThreadId,
                            0,
                            0,
                            0,
                            0,
                            0,
                            default,
                            hasAtomicTraceLane: false,
                            default,
                            0,
                            default,
                            hasVmxTraceLane: false);

                    public static RetireWindowEffect Vmx(
                        in Core.VmxRetireEffect vmxEffect,
                        int virtualThreadId,
                        in RetiredTraceLaneSnapshot traceLane,
                        bool hasTraceLane) =>
                        new(
                            RetireWindowEffectKind.Vmx,
                            0,
                            default,
                            default,
                            default,
                            default,
                            NoPipelineEventSlot,
                            Core.SystemEventOrderGuarantee.None,
                            0,
                            0,
                            0,
                            0,
                            0,
                            0,
                            0,
                            default,
                            hasAtomicTraceLane: false,
                            vmxEffect,
                            virtualThreadId,
                            traceLane,
                            hasTraceLane);

                    public static RetireWindowEffect SerializingBoundary() =>
                        new(
                            RetireWindowEffectKind.SerializingBoundary,
                            0,
                            default,
                            default,
                            default,
                            default,
                            NoPipelineEventSlot,
                            Core.SystemEventOrderGuarantee.None,
                            0,
                            0,
                            0,
                            0,
                            0,
                            0,
                            0,
                            default,
                            hasAtomicTraceLane: false,
                            default,
                            0,
                            default,
                            hasVmxTraceLane: false);

                    public static RetireWindowEffect ScalarMemoryStore(
                        ulong memoryAddress,
                        ulong memoryData,
                        byte memoryAccessSize) =>
                        new(
                            RetireWindowEffectKind.ScalarMemoryStore,
                            0,
                            default,
                            default,
                            default,
                            default,
                            NoPipelineEventSlot,
                            Core.SystemEventOrderGuarantee.None,
                            0,
                            0,
                            memoryAddress,
                            memoryData,
                            memoryAccessSize,
                            0,
                            0,
                            default,
                            hasAtomicTraceLane: false,
                            default,
                            0,
                            default,
                            hasVmxTraceLane: false);

                    public static RetireWindowEffect PredicateState(
                        byte predicateRegisterId,
                        ulong predicateMaskValue) =>
                        new(
                            RetireWindowEffectKind.PredicateState,
                            0,
                            default,
                            default,
                            default,
                            default,
                            NoPipelineEventSlot,
                            Core.SystemEventOrderGuarantee.None,
                            0,
                            0,
                            0,
                            0,
                            0,
                            predicateRegisterId,
                            predicateMaskValue,
                            default,
                            hasAtomicTraceLane: false,
                            default,
                            0,
                            default,
                            hasVmxTraceLane: false);
                }

                internal ref struct RetireWindowBatch
                {
                    private readonly Span<RetireRecord> _retireRecords;
                    private readonly Span<RetireWindowEffect> _effects;
                    private readonly Core.Pipeline.PipelineEvent?[] _pipelineEvents;
                    private int _retireRecordCount;
                    private int _effectCount;
                    private int _pipelineEventCount;
                    private bool _assistBoundaryKilledThisRetireWindow;
                    private bool _serializingBoundaryCaptured;
                    private RetireWindowTypedEffectKind _typedEffectKind;
                    private bool _atomicEffectCaptured;

                    public RetireWindowBatch(
                        Span<RetireRecord> retireRecords,
                        Span<RetireWindowEffect> effects,
                        Core.Pipeline.PipelineEvent?[] pipelineEvents)
                    {
                        _retireRecords = retireRecords;
                        _effects = effects;
                        _pipelineEvents = pipelineEvents ?? throw new ArgumentNullException(nameof(pipelineEvents));
                        _retireRecordCount = 0;
                        _effectCount = 0;
                        _pipelineEventCount = 0;
                        _assistBoundaryKilledThisRetireWindow = false;
                        _serializingBoundaryCaptured = false;
                        _typedEffectKind = RetireWindowTypedEffectKind.None;
                        _atomicEffectCaptured = false;
                    }

                    public ReadOnlySpan<RetireRecord> RetireRecords => _retireRecords[.._retireRecordCount];
                    public ReadOnlySpan<RetireWindowEffect> Effects => _effects[.._effectCount];
                    public bool AssistBoundaryKilledThisRetireWindow => _assistBoundaryKilledThisRetireWindow;

                    public Core.Pipeline.PipelineEvent GetPipelineEventPayload(byte pipelineEventSlot)
                    {
                        if (pipelineEventSlot == byte.MaxValue ||
                            pipelineEventSlot >= _pipelineEventCount ||
                            pipelineEventSlot >= _pipelineEvents.Length ||
                            _pipelineEvents[pipelineEventSlot] == null)
                        {
                            throw new InvalidOperationException("Pipeline-event retire payload is missing.");
                        }

                        return _pipelineEvents[pipelineEventSlot]!;
                    }

                    public void AppendRetireRecord(in RetireRecord retireRecord)
                    {
                        CPU_Core.AppendRetireRecord(
                            _retireRecords,
                            ref _retireRecordCount,
                            retireRecord);
                    }

                    public void AppendDeferredStoreLane(byte laneIndex)
                    {
                        AppendEffect(RetireWindowEffect.DeferredStoreCommit(laneIndex));
                    }

                    public void CaptureGeneratedCsrEffect(
                        byte laneIndex,
                        int virtualThreadId,
                        in Core.CsrRetireEffect csrEffect)
                    {
                        CaptureSingletonTypedSideEffect(laneIndex, RetireWindowTypedEffectKind.Csr);
                        CPU_Core.EmitGeneratedCsrRetireRecords(
                            virtualThreadId,
                            csrEffect,
                            _retireRecords,
                            ref _retireRecordCount);
                        AppendEffect(RetireWindowEffect.Csr(csrEffect));
                    }

                    public void CaptureGeneratedVectorConfigEffect(
                        byte laneIndex,
                        int virtualThreadId,
                        in Core.VectorConfigRetireEffect vectorConfigEffect)
                    {
                        CaptureSingletonTypedSideEffect(laneIndex, RetireWindowTypedEffectKind.VectorConfig);
                        CPU_Core.EmitGeneratedVectorConfigRetireRecords(
                            virtualThreadId,
                            vectorConfigEffect,
                            _retireRecords,
                            ref _retireRecordCount);
                        AppendEffect(RetireWindowEffect.VectorConfig(vectorConfigEffect));
                    }

                    public void CaptureGeneratedAtomicEffect(
                        byte laneIndex,
                        in ScalarWriteBackLaneState traceLane,
                        in Core.AtomicRetireEffect atomicEffect)
                    {
                        CaptureAtomicSideEffect(laneIndex);
                        AppendEffect(
                            RetireWindowEffect.Atomic(
                                atomicEffect,
                                RetiredTraceLaneSnapshot.FromLane(traceLane),
                                hasTraceLane: true));
                    }

                    public void CaptureGeneratedSystemEvent(
                        byte laneIndex,
                        Core.SystemEventKind systemEventKind,
                        Core.SystemEventOrderGuarantee orderGuarantee,
                        ulong retiredPc,
                        int virtualThreadId)
                    {
                        CaptureSingletonTypedSideEffect(laneIndex, RetireWindowTypedEffectKind.System);
                        if (orderGuarantee == Core.SystemEventOrderGuarantee.FullSerialTrapBoundary)
                        {
                            NoteSerializingBoundary();
                        }

                        AppendEffect(
                            RetireWindowEffect.System(
                                systemEventKind,
                                orderGuarantee,
                                retiredPc,
                                virtualThreadId));
                    }

                    public void CaptureGeneratedPipelineEvent(
                        byte laneIndex,
                        Core.Pipeline.PipelineEvent pipelineEvent,
                        Core.SystemEventOrderGuarantee orderGuarantee,
                        ulong retiredPc,
                        int virtualThreadId)
                    {
                        ArgumentNullException.ThrowIfNull(pipelineEvent);
                        if (pipelineEvent is Core.Pipeline.TrapEntryEvent)
                        {
                            CaptureRetireVisibleSingletonTypedSideEffect(
                                laneIndex,
                                RetireWindowTypedEffectKind.System);
                        }
                        else
                        {
                            CaptureSingletonTypedSideEffect(laneIndex, RetireWindowTypedEffectKind.System);
                        }

                        if (orderGuarantee == Core.SystemEventOrderGuarantee.FullSerialTrapBoundary)
                        {
                            NoteSerializingBoundary();
                        }

                        if ((uint)_pipelineEventCount >= (uint)_pipelineEvents.Length)
                        {
                            throw new InvalidOperationException("WB pipeline-event payload buffer exhausted.");
                        }

                        byte pipelineEventSlot = (byte)_pipelineEventCount++;
                        _pipelineEvents[pipelineEventSlot] = pipelineEvent;
                        AppendEffect(
                            RetireWindowEffect.PipelineEvent(
                                pipelineEventSlot,
                                orderGuarantee,
                                retiredPc,
                                virtualThreadId));
                    }

                    public void CaptureGeneratedVmxEffect(
                        byte laneIndex,
                        in ScalarWriteBackLaneState traceLane,
                        in Core.VmxRetireEffect vmxEffect)
                    {
                        CaptureSingletonTypedSideEffect(laneIndex, RetireWindowTypedEffectKind.Vmx);
                        AppendEffect(
                            RetireWindowEffect.Vmx(
                                vmxEffect,
                                traceLane.VirtualThreadId,
                                RetiredTraceLaneSnapshot.FromLane(traceLane),
                                hasTraceLane: true));
                    }

                    public void EmitMicroOpRetireRecords(
                        ref Processor.CPU_Core core,
                        in ScalarWriteBackLaneState lane)
                    {
                        if (lane.MicroOp == null)
                        {
                            throw new InvalidOperationException("WB retire lane is missing MicroOp authority.");
                        }

                        lane.MicroOp.CapturePrimaryWriteBackResult(lane.ResultValue);
                        lane.MicroOp.EmitWriteBackRetireRecords(
                            ref core,
                            _retireRecords,
                            ref _retireRecordCount);
                    }

                    public void NoteSerializingBoundary()
                    {
                        if (_serializingBoundaryCaptured)
                        {
                            return;
                        }

                        AppendEffect(RetireWindowEffect.SerializingBoundary());
                        _serializingBoundaryCaptured = true;
                    }

                    public void AccumulateAssistBoundaryKilled(bool killed)
                    {
                        _assistBoundaryKilledThisRetireWindow |= killed;
                    }

                    public void CaptureRetireWindowCsrEffect(in Core.CsrRetireEffect csrEffect)
                    {
                        CaptureRetireWindowSingletonTypedSideEffect(RetireWindowTypedEffectKind.Csr);
                        AppendEffect(RetireWindowEffect.Csr(csrEffect));
                    }

                    public void CaptureRetireWindowAtomicEffect(in Core.AtomicRetireEffect atomicEffect)
                    {
                        if (_atomicEffectCaptured)
                        {
                            throw new InvalidOperationException(
                                "Retire batch encountered multiple Atomic retire-window effects.");
                        }

                        _atomicEffectCaptured = true;
                        AppendEffect(
                            RetireWindowEffect.Atomic(
                                atomicEffect,
                                default,
                                hasTraceLane: false));
                    }

                    public void CaptureRetireWindowPipelineEvent(
                        Core.Pipeline.PipelineEvent pipelineEvent,
                        Core.SystemEventOrderGuarantee orderGuarantee,
                        ulong retiredPc,
                        int virtualThreadId,
                        bool serializingBoundaryFollowThrough)
                    {
                        ArgumentNullException.ThrowIfNull(pipelineEvent);
                        CaptureRetireWindowSingletonTypedSideEffect(RetireWindowTypedEffectKind.System);

                        if ((uint)_pipelineEventCount >= (uint)_pipelineEvents.Length)
                        {
                            throw new InvalidOperationException("Retire batch pipeline-event payload buffer exhausted.");
                        }

                        byte pipelineEventSlot = (byte)_pipelineEventCount++;
                        _pipelineEvents[pipelineEventSlot] = pipelineEvent;
                        AppendEffect(
                            RetireWindowEffect.PipelineEvent(
                                pipelineEventSlot,
                                orderGuarantee,
                                retiredPc,
                                virtualThreadId));

                        if (orderGuarantee == Core.SystemEventOrderGuarantee.FullSerialTrapBoundary ||
                            serializingBoundaryFollowThrough)
                        {
                            NoteSerializingBoundary();
                        }
                    }

                    public void CaptureRetireWindowVmxEffect(
                        in Core.VmxRetireEffect vmxEffect,
                        int virtualThreadId)
                    {
                        CaptureRetireWindowSingletonTypedSideEffect(RetireWindowTypedEffectKind.Vmx);
                        AppendEffect(
                            RetireWindowEffect.Vmx(
                                vmxEffect,
                                virtualThreadId,
                                default,
                                hasTraceLane: false));
                        NoteSerializingBoundary();
                    }

                    public void CaptureRetireWindowScalarMemoryStore(
                        ulong memoryAddress,
                        ulong memoryData,
                        byte memoryAccessSize)
                    {
                        CaptureRetireWindowSingletonTypedSideEffect(RetireWindowTypedEffectKind.ScalarMemoryStore);
                        AppendEffect(
                            RetireWindowEffect.ScalarMemoryStore(
                                memoryAddress,
                                memoryData,
                                memoryAccessSize));
                    }

                    public void CaptureRetireWindowPredicateState(
                        byte predicateRegisterId,
                        ulong predicateMaskValue)
                    {
                        CaptureRetireWindowSingletonTypedSideEffect(RetireWindowTypedEffectKind.PredicateState);
                        AppendEffect(
                            RetireWindowEffect.PredicateState(
                                predicateRegisterId,
                                predicateMaskValue));
                    }

                    private void AppendEffect(in RetireWindowEffect effect)
                    {
                        if ((uint)_effectCount >= (uint)_effects.Length)
                        {
                            throw new InvalidOperationException("WB retire-window effect buffer exhausted.");
                        }

                        _effects[_effectCount++] = effect;
                    }

                    private void CaptureRetireWindowSingletonTypedSideEffect(
                        RetireWindowTypedEffectKind effectKind)
                    {
                        if (_typedEffectKind != RetireWindowTypedEffectKind.None)
                        {
                            throw new InvalidOperationException(
                                $"Retire batch encountered multiple singleton typed retire-window effects ({_typedEffectKind} then {effectKind}).");
                        }

                        _typedEffectKind = effectKind;
                    }

                    private void CaptureSingletonTypedSideEffect(
                        byte laneIndex,
                        RetireWindowTypedEffectKind effectKind)
                    {
                        if (_typedEffectKind != RetireWindowTypedEffectKind.None)
                        {
                            throw new InvalidOperationException(
                                $"WB retire window encountered multiple singleton typed effects ({_typedEffectKind} then {effectKind}).");
                        }

                        if (laneIndex != 7)
                        {
                            throw new InvalidOperationException(
                                $"WB retire window singleton typed effect {effectKind} was published from non-lane7 carrier {laneIndex}.");
                        }

                        _typedEffectKind = effectKind;
                    }

                    private void CaptureRetireVisibleSingletonTypedSideEffect(
                        byte laneIndex,
                        RetireWindowTypedEffectKind effectKind)
                    {
                        if (_typedEffectKind != RetireWindowTypedEffectKind.None)
                        {
                            throw new InvalidOperationException(
                                $"WB retire window encountered multiple singleton typed effects ({_typedEffectKind} then {effectKind}).");
                        }

                        if (laneIndex >= 6 && laneIndex != 7)
                        {
                            throw new InvalidOperationException(
                                $"WB retire window singleton typed effect {effectKind} was published from non-retire-visible carrier {laneIndex}.");
                        }

                        _typedEffectKind = effectKind;
                    }

                    private void CaptureAtomicSideEffect(byte laneIndex)
                    {
                        if (_atomicEffectCaptured)
                        {
                            throw new InvalidOperationException(
                                "WB retire window encountered multiple Atomic typed effects.");
                        }

                        if (laneIndex >= 6)
                        {
                            throw new InvalidOperationException(
                                $"WB retire window Atomic effect was published from non-retire-visible carrier {laneIndex}.");
                        }

                        _atomicEffectCaptured = true;
                    }
                }

        }
    }
}
