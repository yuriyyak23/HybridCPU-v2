using System;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace HybridCPU_ISE.Tests.TestHelpers;

internal enum RetireWindowCaptureEffectKind : byte
{
    None = 0,
    Csr = 1,
    Atomic = 2,
    System = 3,
    Vmx = 4,
    ScalarMemoryStore = 5,
    PredicateState = 6,
    SerializingBoundary = 7
}

internal sealed class RetireWindowCaptureSnapshot
{
    private readonly PipelineEvent?[] _pipelineEvents;

    public RetireWindowCaptureSnapshot(
        RetireRecord[] retireRecords,
        Processor.CPU_Core.RetireWindowEffect[] effects,
        PipelineEvent?[] pipelineEvents,
        bool assistBoundaryKilledThisRetireWindow)
    {
        RetireRecords = retireRecords ?? throw new ArgumentNullException(nameof(retireRecords));
        Effects = effects ?? throw new ArgumentNullException(nameof(effects));
        _pipelineEvents = pipelineEvents ?? throw new ArgumentNullException(nameof(pipelineEvents));
        AssistBoundaryKilledThisRetireWindow = assistBoundaryKilledThisRetireWindow;
    }

    public RetireRecord[] RetireRecords { get; }

    public Processor.CPU_Core.RetireWindowEffect[] Effects { get; }

    public bool AssistBoundaryKilledThisRetireWindow { get; }

    public int RetireRecordCount => RetireRecords.Length;

    public RetireWindowCaptureEffectKind TypedEffectKind
    {
        get
        {
            foreach (Processor.CPU_Core.RetireWindowEffect effect in Effects)
            {
                switch (effect.Kind)
                {
                    case Processor.CPU_Core.RetireWindowEffectKind.Csr:
                        return RetireWindowCaptureEffectKind.Csr;

                    case Processor.CPU_Core.RetireWindowEffectKind.Atomic:
                        return RetireWindowCaptureEffectKind.Atomic;

                    case Processor.CPU_Core.RetireWindowEffectKind.PipelineEvent:
                    case Processor.CPU_Core.RetireWindowEffectKind.System:
                        return RetireWindowCaptureEffectKind.System;

                    case Processor.CPU_Core.RetireWindowEffectKind.Vmx:
                        return RetireWindowCaptureEffectKind.Vmx;

                    case Processor.CPU_Core.RetireWindowEffectKind.ScalarMemoryStore:
                        return RetireWindowCaptureEffectKind.ScalarMemoryStore;

                    case Processor.CPU_Core.RetireWindowEffectKind.PredicateState:
                        return RetireWindowCaptureEffectKind.PredicateState;
                }
            }

            return HasSerializingBoundaryEffect
                ? RetireWindowCaptureEffectKind.SerializingBoundary
                : RetireWindowCaptureEffectKind.None;
        }
    }

    public bool HasTypedEffect =>
        TypedEffectKind != RetireWindowCaptureEffectKind.None;

    public bool HasSerializingBoundaryEffect =>
        TryFindEffect(Processor.CPU_Core.RetireWindowEffectKind.SerializingBoundary, out _);

    public bool HasPipelineEventSerializingBoundaryFollowThrough =>
        PipelineEvent != null && HasSerializingBoundaryEffect;

    public bool HasCsrEffect =>
        TryFindEffect(Processor.CPU_Core.RetireWindowEffectKind.Csr, out _);

    public bool HasAtomicEffect =>
        TryFindEffect(Processor.CPU_Core.RetireWindowEffectKind.Atomic, out _);

    public bool HasVmxEffect =>
        TryFindEffect(Processor.CPU_Core.RetireWindowEffectKind.Vmx, out _);

    public bool HasPipelineEvent =>
        PipelineEvent is not null;

    public bool HasScalarMemoryStoreEffect =>
        TryFindEffect(Processor.CPU_Core.RetireWindowEffectKind.ScalarMemoryStore, out _);

    public CsrRetireEffect CsrEffect =>
        FindEffectOrDefault(Processor.CPU_Core.RetireWindowEffectKind.Csr).CsrEffect;

    public AtomicRetireEffect AtomicEffect =>
        FindEffectOrDefault(Processor.CPU_Core.RetireWindowEffectKind.Atomic).AtomicEffect;

    public VmxRetireEffect VmxEffect =>
        FindEffectOrDefault(Processor.CPU_Core.RetireWindowEffectKind.Vmx).VmxEffect;

    public PipelineEvent? PipelineEvent
    {
        get
        {
            if (!TryFindEffect(Processor.CPU_Core.RetireWindowEffectKind.PipelineEvent, out Processor.CPU_Core.RetireWindowEffect effect))
            {
                return null;
            }

            return TryGetPipelineEvent(effect.PipelineEventSlot, out PipelineEvent? pipelineEvent)
                ? pipelineEvent
                : null;
        }
    }

    public SystemEventOrderGuarantee PipelineEventOrderGuarantee =>
        FindEffectOrDefault(Processor.CPU_Core.RetireWindowEffectKind.PipelineEvent).SystemEventOrderGuarantee;

    public ulong PipelineEventRetiredPc =>
        FindEffectOrDefault(Processor.CPU_Core.RetireWindowEffectKind.PipelineEvent).SystemEventPc;

    public ulong MemoryAddress =>
        FindEffectOrDefault(Processor.CPU_Core.RetireWindowEffectKind.ScalarMemoryStore).MemoryAddress;

    public ulong MemoryData =>
        FindEffectOrDefault(Processor.CPU_Core.RetireWindowEffectKind.ScalarMemoryStore).MemoryData;

    public byte MemoryAccessSize =>
        FindEffectOrDefault(Processor.CPU_Core.RetireWindowEffectKind.ScalarMemoryStore).MemoryAccessSize;

    public RetireRecord GetRetireRecord(int index) => RetireRecords[index];

    private Processor.CPU_Core.RetireWindowEffect FindEffectOrDefault(
        Processor.CPU_Core.RetireWindowEffectKind kind)
    {
        return TryFindEffect(kind, out Processor.CPU_Core.RetireWindowEffect effect)
            ? effect
            : default;
    }

    private bool TryFindEffect(
        Processor.CPU_Core.RetireWindowEffectKind kind,
        out Processor.CPU_Core.RetireWindowEffect effect)
    {
        foreach (Processor.CPU_Core.RetireWindowEffect candidate in Effects)
        {
            if (candidate.Kind == kind)
            {
                effect = candidate;
                return true;
            }
        }

        effect = default;
        return false;
    }

    private bool TryGetPipelineEvent(
        byte slot,
        out PipelineEvent? pipelineEvent)
    {
        if (slot == byte.MaxValue || slot >= _pipelineEvents.Length)
        {
            pipelineEvent = null;
            return false;
        }

        pipelineEvent = _pipelineEvents[slot];
        return pipelineEvent is not null;
    }
}

internal static class RetireWindowCaptureTestHelper
{
    public static void ApplyExecutionDispatcherRetireWindowPublications(
        ref Processor.CPU_Core core,
        ExecutionDispatcherV4 dispatcher,
        InstructionIR instruction,
        ICanonicalCpuState state,
        ulong bundleSerial = 0,
        byte vtId = 0)
    {
        core.TestApplyExecutionDispatcherRetireWindowPublications(
            dispatcher,
            instruction,
            state,
            bundleSerial,
            vtId);
    }

    public static RetireWindowCaptureSnapshot CaptureExecutionDispatcherRetireWindowPublications(
        ExecutionDispatcherV4 dispatcher,
        InstructionIR instruction,
        ICanonicalCpuState state,
        ulong bundleSerial = 0,
        byte vtId = 0)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(state);

        Span<RetireRecord> retireRecords =
            stackalloc RetireRecord[Processor.CPU_Core.DirectRetirePublicationRetireRecordCapacity];
        Span<Processor.CPU_Core.RetireWindowEffect> retireEffects =
            stackalloc Processor.CPU_Core.RetireWindowEffect[3];
        PipelineEvent?[] pipelineEvents = new PipelineEvent?[1];
        Processor.CPU_Core.RetireWindowBatch retireBatch =
            new(retireRecords, retireEffects, pipelineEvents);

        dispatcher.CaptureRetireWindowPublications(
            instruction,
            state,
            ref retireBatch,
            bundleSerial,
            vtId);

        var snapshot = new RetireWindowCaptureSnapshot(
            retireBatch.RetireRecords.ToArray(),
            retireBatch.Effects.ToArray(),
            (PipelineEvent?[])pipelineEvents.Clone(),
            retireBatch.AssistBoundaryKilledThisRetireWindow);

        return snapshot;
    }

    public static RetireWindowCaptureSnapshot CaptureAndApplyExecutionDispatcherRetireWindowPublications(
        ref Processor.CPU_Core core,
        ExecutionDispatcherV4 dispatcher,
        InstructionIR instruction,
        ICanonicalCpuState state,
        ulong bundleSerial = 0,
        byte vtId = 0)
    {
        RetireWindowCaptureSnapshot snapshot =
            CaptureExecutionDispatcherRetireWindowPublications(
                dispatcher,
                instruction,
                state,
                bundleSerial,
                vtId);
        ApplyExecutionDispatcherRetireWindowPublications(
            ref core,
            dispatcher,
            instruction,
            state,
            bundleSerial,
            vtId);
        return snapshot;
    }
}

