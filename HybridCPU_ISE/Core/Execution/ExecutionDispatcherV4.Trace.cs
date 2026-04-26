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
    public sealed partial class ExecutionDispatcherV4
    {
        // Trace event classification helpers.
        // These switch-based classifiers can collapse into canonical descriptor
        // lookups once OpcodeInfo carries the full authoritative trace surface:
        //   OpcodeInfo.Get((uint)instr.CanonicalOpcode).TraceEventKind

        private static TraceEventKind ClassifyTraceEvent(InstructionIR instr, ExecutionResult result)
        {
            return instr.Class switch
            {
                InstructionClass.ScalarAlu => TraceEventKind.AluExecuted,
                InstructionClass.Memory => ClassifyMemoryEvent(instr),
                InstructionClass.ControlFlow => ClassifyControlFlowEvent(instr, result),
                InstructionClass.Atomic => ClassifyAtomicEvent(instr, result),
                InstructionClass.System => ClassifySystemEvent(instr),
                InstructionClass.Csr => ClassifyCsrEvent(instr),
                InstructionClass.SmtVt => ClassifySmtVtEvent(instr),
                InstructionClass.Vmx => ClassifyVmxEvent(instr, result),
                _ => TraceEventKind.AluExecuted,
            };
        }

        private static TraceEventKind ClassifyMemoryEvent(InstructionIR instr)
        {
            return instr.SerializationClass == SerializationClass.MemoryOrdered
                ? TraceEventKind.StoreExecuted
                : TraceEventKind.LoadExecuted;
        }

        private static TraceEventKind ClassifyControlFlowEvent(InstructionIR instr, ExecutionResult result)
        {
            return ResolveOpcode(instr) switch
            {
                IsaOpcodeValues.JAL or IsaOpcodeValues.JALR => TraceEventKind.JumpExecuted,
                _ => result.PcRedirected ? TraceEventKind.BranchTaken : TraceEventKind.BranchNotTaken,
            };
        }

        private static TraceEventKind ClassifyAtomicEvent(InstructionIR instr, ExecutionResult result)
        {
            OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo((uint)instr.CanonicalOpcode);
            if (opcodeInfo.HasValue && opcodeInfo.Value.InstructionClass == InstructionClass.Atomic)
            {
                InstructionFlags flags = opcodeInfo.Value.Flags;
                if (opcodeInfo.Value.OperandCount == 1 &&
                    (flags & InstructionFlags.MemoryRead) != 0 &&
                    (flags & InstructionFlags.MemoryWrite) == 0)
                {
                    return TraceEventKind.LrExecuted;
                }

                if (opcodeInfo.Value.OperandCount == 2 &&
                    (flags & InstructionFlags.MemoryRead) == 0 &&
                    (flags & InstructionFlags.MemoryWrite) != 0)
                {
                    return result.Value == 0 ? TraceEventKind.ScSucceeded : TraceEventKind.ScFailed;
                }

                if ((flags & (InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite)) ==
                    (InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite))
                {
                    if (!InstructionRegistry.TryResolvePublishedAtomicAccessSize(in instr, out byte accessSize))
                    {
                        throw new InvalidOperationException(
                            $"Published atomic opcode {opcodeInfo.Value.Mnemonic} did not resolve an authoritative access size for trace classification.");
                    }

                    return accessSize > 4
                        ? TraceEventKind.AmoDwordExecuted
                        : TraceEventKind.AmoWordExecuted;
                }
            }

            return TraceEventKind.AmoWordExecuted;
        }

        private static TraceEventKind ClassifySystemEvent(InstructionIR instr)
        {
            if (InstructionRegistry.TryResolvePublishedSystemEventKind(in instr, out SystemEventKind systemEventKind))
            {
                return systemEventKind switch
                {
                    SystemEventKind.Ecall or SystemEventKind.Ebreak => TraceEventKind.TrapTaken,
                    SystemEventKind.Mret or SystemEventKind.Sret => TraceEventKind.PrivilegeReturn,
                    SystemEventKind.Wfi => TraceEventKind.WfiEntered,
                    _ => TraceEventKind.FenceExecuted,
                };
            }

            OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo((uint)instr.CanonicalOpcode);
            if (opcodeInfo.HasValue && opcodeInfo.Value.InstructionClass == InstructionClass.System)
            {
                if (!opcodeInfo.Value.IsVector &&
                    (opcodeInfo.Value.Flags & InstructionFlags.Privileged) == 0 &&
                    opcodeInfo.Value.OperandCount == 0)
                {
                    return TraceEventKind.FenceExecuted;
                }
            }

            return TraceEventKind.FenceExecuted;
        }

        private static TraceEventKind ClassifyCsrEvent(InstructionIR instr)
        {
            if (InstructionRegistry.TryCreatePublishedCsrMicroOp(in instr, out CSRMicroOp? csrMicroOp) &&
                csrMicroOp is not null)
            {
                return csrMicroOp switch
                {
                    CsrClearMicroOp or CsrReadWriteMicroOp or CsrReadWriteImmediateMicroOp => TraceEventKind.CsrWrite,
                    _ => TraceEventKind.CsrRead,
                };
            }

            return TraceEventKind.CsrRead;
        }

        private static TraceEventKind ClassifySmtVtEvent(InstructionIR instr)
        {
            if (InstructionRegistry.TryResolvePublishedSystemEventKind(in instr, out SystemEventKind systemEventKind))
            {
                return systemEventKind switch
                {
                    SystemEventKind.Wfe => TraceEventKind.VtWfe,
                    SystemEventKind.Sev => TraceEventKind.VtSev,
                    SystemEventKind.PodBarrier => TraceEventKind.PodBarrierEntered,
                    SystemEventKind.VtBarrier => TraceEventKind.VtBarrierEntered,
                    _ => TraceEventKind.VtYield,
                };
            }

            return TraceEventKind.VtYield;
        }

        private static TraceEventKind ClassifyVmxEvent(InstructionIR instr, ExecutionResult result)
        {
            if (InstructionRegistry.TryResolvePublishedVmxOperationKind(in instr, out VmxOperationKind operationKind))
            {
                return operationKind switch
                {
                    VmxOperationKind.VmxOn => TraceEventKind.VmxOn,
                    VmxOperationKind.VmxOff => TraceEventKind.VmxOff,
                    VmxOperationKind.VmLaunch or VmxOperationKind.VmResume =>
                        result.VmxFaulted ? TraceEventKind.VmEntryFailed : TraceEventKind.VmEntry,
                    VmxOperationKind.VmRead => TraceEventKind.VmcsRead,
                    VmxOperationKind.VmWrite or VmxOperationKind.VmClear or VmxOperationKind.VmPtrLd => TraceEventKind.VmcsWrite,
                    _ => TraceEventKind.VmxOn,
                };
            }

            return TraceEventKind.VmxOn;
        }

        private static ulong GetTracePayload(InstructionIR instr, TraceEventKind kind)
        {
            return kind switch
            {
                TraceEventKind.CsrRead or TraceEventKind.CsrWrite =>
                    ResolveCsrTracePayload(instr),
                _ => 0UL,
            };
        }

        private static ulong ResolveCsrTracePayload(InstructionIR instr)
        {
            if (InstructionRegistry.TryCreatePublishedCsrMicroOp(in instr, out CSRMicroOp? csrMicroOp) &&
                csrMicroOp is not null)
            {
                return csrMicroOp is CsrClearMicroOp
                    ? 0UL
                    : csrMicroOp.CSRAddress & 0xFFFUL;
            }

            return (ulong)(instr.Imm & 0xFFF);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ReadExecutionRegister(ICanonicalCpuState state, byte vtId, ushort regId) =>
            unchecked((ulong)state.ReadRegister(vtId, regId));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ReadExecutionPc(ICanonicalCpuState state, byte vtId) =>
            state.ReadPc(vtId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static InvalidOperationException CreateUnsupportedRetainedInterruptEagerExecuteSurfaceException(
            InstructionIR instr) =>
            new InvalidOperationException(
                $"Retained system opcode {FormatOpcode(ResolveOpcode(instr))} does not expose an authoritative eager execute contour. " +
                "Direct callers must fail closed instead of publishing system success/trace until a typed mainline retire/boundary carrier exists.");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static InvalidOperationException CreateUnsupportedRetainedInterruptRetireWindowPublicationSurfaceException(
            InstructionIR instr) =>
            new InvalidOperationException(
                $"Retained system opcode {FormatOpcode(ResolveOpcode(instr))} does not expose an authoritative retire-window publication contour. " +
                "Direct callers must fail closed instead of inventing a pipeline event or retire effect until a typed mainline retire/boundary carrier exists.");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static InvalidOperationException CreateUnsupportedVectorConfigEagerExecuteSurfaceException(
            InstructionIR instr) =>
            new InvalidOperationException(
                $"Vector-config opcode {FormatOpcode(ResolveOpcode(instr))} does not expose an authoritative eager execute contour. " +
                "Direct callers must use the canonical mainline retire path so lane-7 VectorConfig state updates and optional rd writeback stay coupled on the authoritative system-singleton carrier.");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static InvalidOperationException CreateUnsupportedVectorConfigRetireWindowPublicationSurfaceException(
            InstructionIR instr) =>
            new InvalidOperationException(
                $"Vector-config opcode {FormatOpcode(ResolveOpcode(instr))} does not expose an authoritative retire-window publication contour. " +
                "Direct callers must use the canonical mainline retire path so deferred VectorConfig state publication and serializing-boundary follow-through remain coupled on the authoritative lane-7 carrier.");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Core.Pipeline.PipelineEvent ThrowUnsupportedRetireWindowPublicationSystemOpcode(InstructionIR instr) =>
            throw new InvalidOperationException(
                $"System opcode {FormatOpcode(ResolveOpcode(instr))} does not have an authoritative retire-window publication carrier; pipeline execution remains the supported path.");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Core.Pipeline.PipelineEvent ThrowUnsupportedRetireWindowPublicationSmtVtOpcode(InstructionIR instr) =>
            throw new InvalidOperationException(
                $"SMT/VT opcode {FormatOpcode(ResolveOpcode(instr))} does not expose an authoritative path through retire-window publication here; pipeline execution remains the supported path.");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ExecutionResult ThrowUnsupportedEagerStreamControlOpcode(InstructionIR instr)
        {
            if (InstructionRegistry.TryResolvePublishedStreamControlRetireContour(
                    in instr,
                    out bool requiresSerializingBoundaryFollowThrough))
            {
                if (requiresSerializingBoundaryFollowThrough)
                {
                    throw new InvalidOperationException(
                        "STREAM_WAIT does not expose an authoritative eager execute contour inside ExecutionDispatcherV4. " +
                        "Direct callers must use CaptureRetireWindowPublications(...) or the mainline retire path instead of relying on an inner-unit stub success.");
                }

                OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo((uint)instr.CanonicalOpcode);

                throw new InvalidOperationException(
                    $"Stream-control opcode {(opcodeInfo.HasValue ? opcodeInfo.Value.Mnemonic : FormatOpcode(ResolveOpcode(instr)))} does not expose an authoritative eager execute contour inside ExecutionDispatcherV4. " +
                    "Direct callers must reject/defer it until an explicit retire/apply contour lands instead of relying on an inner-unit stub success.");
            }

            ushort opcode = ResolveOpcode(instr);

            throw new InvalidOperationException(
                $"Stream-control opcode {FormatOpcode(opcode)} does not expose an authoritative eager execute contour inside ExecutionDispatcherV4. " +
                "Direct callers must reject/defer it until an explicit retire/apply contour lands instead of relying on an inner-unit stub success.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RequiresWiredPipelineEventQueueForEagerExecute(InstructionIR instr)
        {
            if (instr.Class != InstructionClass.SmtVt)
            {
                return false;
            }

            if (InstructionRegistry.TryResolvePublishedStreamControlRetireContour(
                    in instr,
                    out bool requiresSerializingBoundaryFollowThrough))
            {
                return !requiresSerializingBoundaryFollowThrough;
            }

            return InstructionRegistry.TryResolvePublishedSystemEventKind(in instr, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsUnsupportedRetainedInterruptSystemSurface(ushort opcode)
        {
            return opcode is
                IsaOpcodeValues.Interrupt or
                IsaOpcodeValues.InterruptReturn;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPipelineOnlyVectorConfigSystemSurface(ushort opcode)
        {
            OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo((uint)opcode);
            if (opcodeInfo.HasValue)
            {
                return opcodeInfo.Value.InstructionClass == InstructionClass.System
                    && opcodeInfo.Value.IsVector;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsRetireWindowPublicationOnlySystemEagerExecuteSurface(ushort opcode)
        {
            OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo((uint)opcode);
            if (opcodeInfo.HasValue && opcodeInfo.Value.InstructionClass == InstructionClass.System)
            {
                return !opcodeInfo.Value.IsVector && opcodeInfo.Value.OperandCount == 0;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsRetireWindowPublicationOnlySmtVtEagerExecuteSurface(InstructionIR instr)
        {
            return instr.Class == InstructionClass.SmtVt;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsRetireWindowPublicationOnlyVmxEagerExecuteSurface(ushort opcode)
        {
            OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo((uint)opcode);
            if (opcodeInfo.HasValue)
            {
                return opcodeInfo.Value.InstructionClass == InstructionClass.Vmx;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPipelineOnlyMemoryEagerExecuteSurface(ushort opcode)
        {
            OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo((uint)opcode);
            if (opcodeInfo.HasValue)
            {
                return opcodeInfo.Value.InstructionClass == InstructionClass.Memory
                    && opcodeInfo.Value.IsVector;
            }

            return opcode is
                IsaOpcodeValues.Load or
                IsaOpcodeValues.Store or
                IsaOpcodeValues.MTILE_LOAD or
                IsaOpcodeValues.MTILE_STORE;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RequiresRetireWindowPublicationSerializingBoundaryFollowThrough(InstructionIR instr)
        {
            return instr.SerializationClass is
                SerializationClass.FullSerial or
                SerializationClass.VmxSerial;
        }
    }
}

