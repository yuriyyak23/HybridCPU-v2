using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using CpuCore = YAKSys_Hybrid_CPU.Processor.CPU_Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.Core.Execution
{
    public readonly struct ExecutionResult
    {
        public ulong Value { get; init; }
        public bool PcRedirected { get; init; }
        public ulong NewPc { get; init; }
        public bool TrapRaised { get; init; }
        public bool VmxFaulted { get; init; }

        public static ExecutionResult Ok(ulong value = 0) => new ExecutionResult { Value = value };
        public static ExecutionResult Redirect(ulong newPc, ulong rdValue = 0) => new ExecutionResult { Value = rdValue, PcRedirected = true, NewPc = newPc };
        public static ExecutionResult Trap() => new ExecutionResult { TrapRaised = true };
        public static ExecutionResult VmxFault() => new ExecutionResult { VmxFaulted = true };
    }

    public interface IAtomicMemoryBus
    {
        ulong AtomicRMW64(ulong address, Func<ulong, ulong> modify);
    }

    public sealed partial class ExecutionDispatcherV4
    {
        private readonly IAtomicMemoryBus? _atomicBus;
        private readonly IAtomicMemoryUnit _atomicMemoryUnit;
        private readonly MemoryUnit? _memoryUnit;
        private readonly CsrFile? _csrFile;
        private readonly VmxExecutionUnit? _vmxUnit;
        private readonly IV4TraceEventSink _traceSink;
        private readonly TelemetryCounters? _telemetry;
        private readonly IPipelineEventQueue _pipelineEventQueue;

        private static IAtomicMemoryUnit CreateDefaultAtomicMemoryUnit() =>
            new MainMemoryAtomicMemoryUnit(Processor.MainMemory);

        public ExecutionDispatcherV4(
            IAtomicMemoryBus? atomicBus = null,
            IAtomicMemoryUnit? atomicMemoryUnit = null,
            MemoryUnit? memoryUnit = null,
            CsrFile? csrFile = null,
            VmxExecutionUnit? vmxUnit = null,
            IV4TraceEventSink? traceSink = null,
            TelemetryCounters? telemetry = null,
            IPipelineEventQueue? pipelineEventQueue = null)
        {
            _atomicBus = atomicBus;
            _atomicMemoryUnit = atomicMemoryUnit ?? CreateDefaultAtomicMemoryUnit();
            _memoryUnit = memoryUnit;
            _csrFile = csrFile;
            _vmxUnit = vmxUnit;
            _traceSink = traceSink ?? NullV4TraceEventSink.Instance;
            _telemetry = telemetry;
            _pipelineEventQueue = pipelineEventQueue ?? NullPipelineEventQueue.Instance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort ResolveOpcode(InstructionIR instr) =>
            instr.CanonicalOpcode.Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string FormatOpcode(ushort opcode) =>
            OpcodeRegistry.GetMnemonicOrHex(opcode);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanRouteToConfiguredExecutionSurface(InstructionIR instr)
        {
            ushort opcode = ResolveOpcode(instr);

            if (InstructionRegistry.TryResolvePublishedStreamControlRetireContour(in instr, out _))
            {
                return false;
            }

            if (IsPipelineOnlyVectorExceptionControlCsrSurface(opcode))
            {
                return false;
            }

            if (IsPipelineOnlyVectorConfigSystemSurface(opcode))
            {
                return false;
            }

            if (IsUnsupportedRetainedInterruptSystemSurface(opcode))
            {
                return false;
            }

            if (RequiresWiredCsrFileForExecutionSurface(instr))
            {
                return false;
            }

            if (IsRetireWindowPublicationOnlySystemEagerExecuteSurface(opcode) ||
                IsRetireWindowPublicationOnlySmtVtEagerExecuteSurface(instr) ||
                IsRetireWindowPublicationOnlyVmxEagerExecuteSurface(opcode) ||
                IsPipelineOnlyMemoryEagerExecuteSurface(opcode))
            {
                return false;
            }

            if (RequiresWiredPipelineEventQueueForEagerExecute(instr) && _pipelineEventQueue is NullPipelineEventQueue)
            {
                return false;
            }

            return instr.Class switch
            {
                InstructionClass.Atomic => false,
                InstructionClass.Vmx => _vmxUnit is not null,
                _ => true
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnforceConfiguredExecutionSurfaceContract(InstructionIR instr)
        {
            ushort opcode = ResolveOpcode(instr);

            if (CanRouteToConfiguredExecutionSurface(instr))
            {
                return;
            }

            if (instr.Class == InstructionClass.Atomic)
            {
                throw new InvalidOperationException(
                    $"Atomic opcode {FormatOpcode(opcode)} reached ExecutionDispatcherV4 eager execute surface without an authoritative retire-window publication follow-through. " +
                    "Direct callers must use CaptureRetireWindowPublications(...) or the mainline retire path instead of publishing eager success/trace on the Atomic plane.");
            }

            if (InstructionRegistry.TryResolvePublishedStreamControlRetireContour(
                    in instr,
                    out bool requiresSerializingBoundaryFollowThrough))
            {
                if (requiresSerializingBoundaryFollowThrough)
                {
                    throw new InvalidOperationException(
                        "STREAM_WAIT reached ExecutionDispatcherV4 eager execute surface without an authoritative retire-window publication follow-through. " +
                        "Direct callers must use CaptureRetireWindowPublications(...) or the mainline retire path so the serializing-boundary effect is applied explicitly instead of publishing stub success/trace.");
                }

                throw new InvalidOperationException(
                    $"Stream-control opcode {FormatOpcode(opcode)} reached ExecutionDispatcherV4 eager execute surface without an authoritative direct follow-through. " +
                    "Direct callers must reject/defer it instead of publishing stub success/trace until an explicit retire/apply contour lands.");
            }

            if (IsPipelineOnlyVectorExceptionControlCsrSurface(opcode))
            {
                throw new InvalidOperationException(
                    $"Vector exception control opcode {FormatOpcode(opcode)} reached ExecutionDispatcherV4 eager execute surface without an authoritative direct follow-through. " +
                    "Direct callers must decode it through the canonical mainline CSR/materializer surface and retire it on the lane-7 pipeline path instead of publishing eager success/trace on a non-CsrFile-backed CSR plane.");
            }

            if (IsPipelineOnlyVectorConfigSystemSurface(opcode))
            {
                throw CreateUnsupportedVectorConfigEagerExecuteSurfaceException(instr);
            }

            if (IsUnsupportedRetainedInterruptSystemSurface(opcode))
            {
                throw CreateUnsupportedRetainedInterruptEagerExecuteSurfaceException(instr);
            }

            if (RequiresWiredCsrFileForExecutionSurface(instr))
            {
                throw CreateMissingCsrFileSurfaceException(instr);
            }

            if (IsPipelineOnlyMemoryEagerExecuteSurface(opcode))
            {
                throw new InvalidOperationException(
                    $"Memory opcode {FormatOpcode(opcode)} reached ExecutionDispatcherV4 eager execute surface without an authoritative direct retire/apply follow-through. " +
                    "Direct callers must reject/defer it instead of publishing EA-only success/trace on a non-representable memory surface; pipeline execution remains the supported path.");
            }

            if (IsRetireWindowPublicationOnlySystemEagerExecuteSurface(opcode))
            {
                throw new InvalidOperationException(
                    $"System opcode {FormatOpcode(opcode)} reached ExecutionDispatcherV4 eager execute surface without an authoritative retire-window publication follow-through. " +
                    "Direct callers must use CaptureRetireWindowPublications(...) or the mainline retire path instead of enqueueing the typed event directly while still publishing eager success/trace.");
            }

            if (IsRetireWindowPublicationOnlySmtVtEagerExecuteSurface(instr))
            {
                throw new InvalidOperationException(
                    $"SMT/VT opcode {FormatOpcode(opcode)} reached ExecutionDispatcherV4 eager execute surface without an authoritative retire-window publication follow-through. " +
                    "Direct callers must use CaptureRetireWindowPublications(...) or the mainline retire path instead of enqueueing the typed event directly while still publishing eager success/trace.");
            }

            if (IsRetireWindowPublicationOnlyVmxEagerExecuteSurface(opcode))
            {
                throw new InvalidOperationException(
                    _vmxUnit is null
                        ? $"VMX opcode {FormatOpcode(opcode)} reached ExecutionDispatcherV4 eager execute surface without an authoritative retire-window publication follow-through, and this dispatcher does not have a wired VmxExecutionUnit for CaptureRetireWindowPublications(...). Direct callers must wire the VMX unit and use the explicit retire-window publication contour or the mainline retire path instead of mutating VMX state through eager success/trace publication."
                        : $"VMX opcode {FormatOpcode(opcode)} reached ExecutionDispatcherV4 eager execute surface without an authoritative retire-window publication follow-through. Direct callers must use CaptureRetireWindowPublications(...) or the mainline retire path instead of mutating VMX state through eager success/trace publication.");
            }

            if (RequiresWiredPipelineEventQueueForEagerExecute(instr) && _pipelineEventQueue is NullPipelineEventQueue)
            {
                throw new InvalidOperationException(
                    $"Opcode {FormatOpcode(opcode)} reached ExecutionDispatcherV4 eager execute surface without a wired pipeline-event queue. " +
                    "Direct callers must provide an IPipelineEventQueue or use CaptureRetireWindowPublications(...) / the mainline retire path instead of dropping the typed event on NullPipelineEventQueue while still publishing success/trace.");
            }

            throw new InvalidOperationException(
                $"VMX opcode {FormatOpcode(opcode)} reached ExecutionDispatcherV4 without a wired VmxExecutionUnit. " +
                "Direct callers must reject/defer it through the explicit surface contract before execution.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnforceConfiguredRetireWindowPublicationSurfaceContract(InstructionIR instr)
        {
            ushort opcode = ResolveOpcode(instr);

            if (IsPipelineOnlyVectorExceptionControlCsrSurface(opcode))
            {
                throw new InvalidOperationException(
                    $"Vector exception control opcode {FormatOpcode(opcode)} reached ExecutionDispatcherV4.CaptureRetireWindowPublications(...) without an authoritative retire-window publication contour. " +
                    "Direct callers must use the canonical mainline retire path so the non-CsrFile-backed CSR write and serializing-boundary follow-through stay coupled on the authoritative lane-7 pipeline contour.");
            }

            if (IsPipelineOnlyVectorConfigSystemSurface(opcode))
            {
                throw CreateUnsupportedVectorConfigRetireWindowPublicationSurfaceException(instr);
            }

            if (IsUnsupportedRetainedInterruptSystemSurface(opcode))
            {
                throw CreateUnsupportedRetainedInterruptRetireWindowPublicationSurfaceException(instr);
            }

            if (RequiresWiredCsrFileForExecutionSurface(instr))
            {
                throw CreateMissingCsrFileSurfaceException(instr);
            }

            if (instr.Class == InstructionClass.Vmx && _vmxUnit is null)
            {
                throw new InvalidOperationException(
                    $"VMX opcode {FormatOpcode(opcode)} reached ExecutionDispatcherV4 without a wired VmxExecutionUnit. " +
                    "Direct callers must reject/defer it through the explicit surface contract before execution.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool RequiresWiredCsrFileForExecutionSurface(InstructionIR instr)
        {
            if (instr.Class != InstructionClass.Csr || _csrFile is not null)
            {
                return false;
            }

            if (InstructionRegistry.TryCreatePublishedCsrMicroOp(in instr, out CSRMicroOp? csrMicroOp))
            {
                return csrMicroOp is not CsrClearMicroOp;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPipelineOnlyVectorExceptionControlCsrSurface(ushort opcode)
            => opcode is IsaOpcodeValues.VSETVEXCPMASK or IsaOpcodeValues.VSETVEXCPPRI;

        private static InvalidOperationException CreateMissingCsrFileSurfaceException(InstructionIR instr)
        {
            ushort opcode = ResolveOpcode(instr);

            return new InvalidOperationException(
                $"CSR opcode {FormatOpcode(opcode)} reached ExecutionDispatcherV4 without a wired CsrFile. " +
                "This dispatcher does not expose an authoritative eager or retire-window publication contour for non-CsrFile-backed CSR surfaces; " +
                "direct callers must wire the CsrFile and use CaptureRetireWindowPublications(...) or the mainline retire path.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ExecutionResult Execute(InstructionIR instr, ICanonicalCpuState state)
            => Execute(instr, state, bundleSerial: 0, vtId: 0);

        public ExecutionResult Execute(InstructionIR instr, ICanonicalCpuState state, ulong bundleSerial, byte vtId)
        {
            EnforceConfiguredExecutionSurfaceContract(instr);

            var result = instr.Class switch
            {
                InstructionClass.ScalarAlu => ExecuteScalarAlu(instr, state, vtId),
                InstructionClass.Memory => ExecuteMemory(instr, state, vtId),
                InstructionClass.ControlFlow => ExecuteControlFlow(instr, state, vtId),
                InstructionClass.Atomic => ExecuteAtomic(instr, state, vtId),
                InstructionClass.System => ExecuteSystem(instr, state, bundleSerial, vtId),
                InstructionClass.Csr => ExecuteCsr(instr, state, vtId),
                InstructionClass.SmtVt => ExecuteSmtVt(instr, state, bundleSerial, vtId),
                InstructionClass.Vmx => ExecuteVmx(instr, state, vtId),
                _ => throw new UnreachableException($"Unknown InstructionClass {instr.Class}")
            };

            var kind = ClassifyTraceEvent(instr, result);
            var evt = V4TraceEvent.Create(bundleSerial, vtId, state.GetCurrentPipelineState(), kind, GetTracePayload(instr, kind));
            _traceSink.RecordV4Event(evt);
            _telemetry?.ApplyTraceEvent(evt);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void CaptureRetireWindowPublications(
            InstructionIR instr,
            ICanonicalCpuState state,
            ref CpuCore.RetireWindowBatch retireBatch)
            => CaptureRetireWindowPublications(instr, state, ref retireBatch, bundleSerial: 0, vtId: 0);

        internal void CaptureRetireWindowPublications(
            InstructionIR instr,
            ICanonicalCpuState state,
            ref CpuCore.RetireWindowBatch retireBatch,
            ulong bundleSerial,
            byte vtId)
        {
            EnforceConfiguredRetireWindowPublicationSurfaceContract(instr);

            switch (instr.Class)
            {
                case InstructionClass.ScalarAlu:
                    CaptureScalarAluRetireWindowPublications(instr, state, ref retireBatch, vtId);
                    return;

                case InstructionClass.ControlFlow:
                    CaptureControlFlowRetireWindowPublications(instr, state, ref retireBatch, vtId);
                    return;

                case InstructionClass.Csr:
                    CaptureCsrRetireWindowPublications(instr, state, ref retireBatch, vtId);
                    return;

                case InstructionClass.System:
                    CaptureSystemRetireWindowPublications(instr, state, ref retireBatch, bundleSerial, vtId);
                    return;

                case InstructionClass.Vmx:
                    CaptureVmxRetireWindowPublications(instr, state, ref retireBatch, vtId);
                    return;

                case InstructionClass.SmtVt:
                    CaptureSmtVtRetireWindowPublications(instr, state, ref retireBatch, bundleSerial, vtId);
                    return;

                case InstructionClass.Memory:
                    CaptureMemoryRetireWindowPublications(instr, state, ref retireBatch, vtId);
                    return;

                case InstructionClass.Atomic:
                    CaptureAtomicRetireWindowPublications(instr, state, ref retireBatch, vtId);
                    return;

                default:
                    throw new UnreachableException($"Unknown InstructionClass {instr.Class}");
            }
        }

    }
}

