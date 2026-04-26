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
        private ExecutionResult ExecuteSystem(InstructionIR instr, ICanonicalCpuState state, ulong bundleSerial, byte vtId)
        {
            ushort opcode = ResolveOpcode(instr);

            switch (opcode)
            {
                case IsaOpcodeValues.FENCE:
                {
                    return EnqueuePipelineEvent(
                        new FenceEvent { VtId = vtId, BundleSerial = bundleSerial, IsInstructionFence = false });
                }

                case IsaOpcodeValues.FENCE_I:
                {
                    return EnqueuePipelineEvent(
                        new FenceEvent { VtId = vtId, BundleSerial = bundleSerial, IsInstructionFence = true });
                }

                case IsaOpcodeValues.ECALL:
                {
                    // a7 (x17) holds the system-call number per RISC-V ABI.
                    var ecallCode = (long)ReadExecutionRegister(state, vtId, 17);
                    return EnqueuePipelineEvent(
                        new EcallEvent { VtId = vtId, BundleSerial = bundleSerial, EcallCode = ecallCode });
                }

                case IsaOpcodeValues.EBREAK:
                {
                    return EnqueuePipelineEvent(
                        new EbreakEvent { VtId = vtId, BundleSerial = bundleSerial });
                }

                case IsaOpcodeValues.MRET:
                {
                    return EnqueuePipelineEvent(
                        new MretEvent { VtId = vtId, BundleSerial = bundleSerial });
                }

                case IsaOpcodeValues.SRET:
                {
                    return EnqueuePipelineEvent(
                        new SretEvent { VtId = vtId, BundleSerial = bundleSerial });
                }

                case IsaOpcodeValues.WFI:
                {
                    return EnqueuePipelineEvent(
                        new WfiEvent { VtId = vtId, BundleSerial = bundleSerial });
                }

                case IsaOpcodeValues.STREAM_SETUP:
                case IsaOpcodeValues.STREAM_START:
                case IsaOpcodeValues.STREAM_WAIT:
                    return ThrowUnsupportedEagerStreamControlOpcode(instr);

                default:
                    throw new InvalidOpcodeException($"Invalid opcode {FormatOpcode(opcode)} in System execution unit", FormatOpcode(opcode), -1, false);
            }
        }

        // ── CSR execution unit (Phase 08 — full implementation) ──────────────

        private static void CaptureSystemRetireWindowPublications(
            InstructionIR instr,
            ICanonicalCpuState state,
            ref CpuCore.RetireWindowBatch retireBatch,
            ulong bundleSerial,
            byte vtId)
        {
            ushort opcode = ResolveOpcode(instr);

            Core.Pipeline.PipelineEvent systemEvent = opcode switch
            {
                IsaOpcodeValues.FENCE => new FenceEvent
                {
                    VtId = vtId,
                    BundleSerial = bundleSerial,
                    IsInstructionFence = false
                },
                IsaOpcodeValues.FENCE_I => new FenceEvent
                {
                    VtId = vtId,
                    BundleSerial = bundleSerial,
                    IsInstructionFence = true
                },
                IsaOpcodeValues.ECALL => new EcallEvent
                {
                    VtId = vtId,
                    BundleSerial = bundleSerial,
                    EcallCode = (long)ReadExecutionRegister(state, vtId, 17)
                },
                IsaOpcodeValues.EBREAK => new EbreakEvent
                {
                    VtId = vtId,
                    BundleSerial = bundleSerial
                },
                IsaOpcodeValues.MRET => new MretEvent
                {
                    VtId = vtId,
                    BundleSerial = bundleSerial
                },
                IsaOpcodeValues.SRET => new SretEvent
                {
                    VtId = vtId,
                    BundleSerial = bundleSerial
                },
                IsaOpcodeValues.WFI => new WfiEvent
                {
                    VtId = vtId,
                    BundleSerial = bundleSerial
                },
                IsaOpcodeValues.STREAM_SETUP or
                IsaOpcodeValues.STREAM_START or
                IsaOpcodeValues.STREAM_WAIT
                    => ThrowUnsupportedRetireWindowPublicationSystemOpcode(instr),
                _ => throw new InvalidOpcodeException(
                    $"Invalid opcode {FormatOpcode(opcode)} in System execution unit",
                    FormatOpcode(opcode),
                    -1,
                    false)
            };

            retireBatch.CaptureRetireWindowPipelineEvent(
                systemEvent,
                ResolveRetireWindowPublicationSystemOrderGuarantee(opcode),
                ReadExecutionPc(state, vtId),
                vtId,
                RequiresRetireWindowPublicationSerializingBoundaryFollowThrough(instr));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Core.SystemEventOrderGuarantee ResolveRetireWindowPublicationSystemOrderGuarantee(
            ushort opcode)
        {
            return opcode switch
            {
                IsaOpcodeValues.FENCE => Core.SystemEventOrderGuarantee.DrainMemory,
                IsaOpcodeValues.FENCE_I => Core.SystemEventOrderGuarantee.FlushPipeline,
                IsaOpcodeValues.ECALL or
                IsaOpcodeValues.EBREAK or
                IsaOpcodeValues.MRET or
                IsaOpcodeValues.SRET
                    => Core.SystemEventOrderGuarantee.FullSerialTrapBoundary,
                IsaOpcodeValues.WFI
                    => Core.SystemEventOrderGuarantee.DrainMemory,
                _ => Core.SystemEventOrderGuarantee.None
            };
        }
    }
}

