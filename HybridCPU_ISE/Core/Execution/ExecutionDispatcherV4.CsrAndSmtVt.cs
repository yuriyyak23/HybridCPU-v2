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
        private ExecutionResult ExecuteCsr(InstructionIR instr, ICanonicalCpuState state, byte vtId)
        {
            CsrRetireEffect effect = ResolveCsrEffect(instr, state, vtId);
            ApplyCsrEffect(effect, state, vtId);
            return ExecutionResult.Ok(effect.ReadValue);

        }

        private CsrRetireEffect ResolveCsrEffect(InstructionIR instr, ICanonicalCpuState state, byte vtId)
        {
            ushort opcode = ResolveOpcode(instr);

            if (opcode == IsaOpcodeValues.CSR_CLEAR)
            {
                return CsrRetireEffect.ClearExceptionCounters();
            }

            bool known = opcode is
                IsaOpcodeValues.CSRRW  or IsaOpcodeValues.CSRRS  or IsaOpcodeValues.CSRRC or
                IsaOpcodeValues.CSRRWI or IsaOpcodeValues.CSRRSI or IsaOpcodeValues.CSRRCI;
            if (!known)
            {
                throw new InvalidOpcodeException(
                    $"Invalid opcode {FormatOpcode(opcode)} in CSR execution unit",
                    FormatOpcode(opcode),
                    -1,
                    false);
            }

            if (_csrFile is null)
            {
                throw CreateMissingCsrFileSurfaceException(instr);
            }

            const PrivilegeLevel privilege = PrivilegeLevel.Machine;
            ushort csrAddr = (ushort)(instr.Imm & 0xFFF);
            ulong oldValue = _csrFile.Read(csrAddr, privilege);
            bool hasRegisterWriteback = instr.Rd != 0;
            ushort destRegId = instr.Rd;
            ulong immediateValue = (ulong)(instr.Rs1 & 0x1F);

            return opcode switch
            {
                IsaOpcodeValues.CSRRW => CsrRetireEffect.Create(
                    CsrStorageSurface.WiredCsrFile,
                    csrAddr,
                    oldValue,
                    hasRegisterWriteback,
                    destRegId,
                    hasCsrWrite: true,
                    ReadExecutionRegister(state, vtId, instr.Rs1)),

                IsaOpcodeValues.CSRRS => CsrRetireEffect.Create(
                    CsrStorageSurface.WiredCsrFile,
                    csrAddr,
                    oldValue,
                    hasRegisterWriteback,
                    destRegId,
                    hasCsrWrite: instr.Rs1 != 0,
                    instr.Rs1 != 0
                        ? oldValue | ReadExecutionRegister(state, vtId, instr.Rs1)
                        : 0),

                IsaOpcodeValues.CSRRC => CsrRetireEffect.Create(
                    CsrStorageSurface.WiredCsrFile,
                    csrAddr,
                    oldValue,
                    hasRegisterWriteback,
                    destRegId,
                    hasCsrWrite: instr.Rs1 != 0,
                    instr.Rs1 != 0
                        ? oldValue & ~ReadExecutionRegister(state, vtId, instr.Rs1)
                        : 0),

                IsaOpcodeValues.CSRRWI => CsrRetireEffect.Create(
                    CsrStorageSurface.WiredCsrFile,
                    csrAddr,
                    oldValue,
                    hasRegisterWriteback,
                    destRegId,
                    hasCsrWrite: true,
                    immediateValue),

                IsaOpcodeValues.CSRRSI => CsrRetireEffect.Create(
                    CsrStorageSurface.WiredCsrFile,
                    csrAddr,
                    oldValue,
                    hasRegisterWriteback,
                    destRegId,
                    hasCsrWrite: immediateValue != 0,
                    immediateValue != 0 ? oldValue | immediateValue : 0),

                IsaOpcodeValues.CSRRCI => CsrRetireEffect.Create(
                    CsrStorageSurface.WiredCsrFile,
                    csrAddr,
                    oldValue,
                    hasRegisterWriteback,
                    destRegId,
                    hasCsrWrite: immediateValue != 0,
                    immediateValue != 0 ? oldValue & ~immediateValue : 0),

                _ => throw new InvalidOpcodeException(
                    $"Invalid opcode {FormatOpcode(opcode)} in CSR execution unit",
                    FormatOpcode(opcode),
                    -1,
                    false)
            };
        }

        private void CaptureCsrRetireWindowPublications(
            InstructionIR instr,
            ICanonicalCpuState state,
            ref CpuCore.RetireWindowBatch retireBatch,
            byte vtId)
        {
            CsrRetireEffect effect;
            try
            {
                effect = ResolveCsrEffect(instr, state, vtId);
            }
            catch (CsrUnknownAddressException ex)
            {
                ushort opcode = ResolveOpcode(instr);
                throw new InvalidOperationException(
                    $"CSR opcode {FormatOpcode(opcode)} address 0x{(instr.Imm & 0xFFF):X3} does not expose an authoritative direct compat retire contract here; pipeline execution remains the authoritative path for non-CsrFile-backed CSR surfaces.",
                    ex);
            }

            if (effect.HasRegisterWriteback)
            {
                retireBatch.AppendRetireRecord(
                    RetireRecord.RegisterWrite(
                        vtId,
                        effect.DestRegId,
                        effect.ReadValue));
                effect = CsrRetireEffect.Create(
                    effect.StorageSurface,
                    effect.CsrAddress,
                    effect.ReadValue,
                    hasRegisterWriteback: false,
                    destRegId: 0,
                    hasCsrWrite: effect.HasCsrWrite,
                    csrWriteValue: effect.CsrWriteValue);
            }

            if (!effect.ClearsArchitecturalExceptionState &&
                !effect.HasCsrWrite &&
                !effect.HasRegisterWriteback)
            {
                return;
            }

            retireBatch.CaptureRetireWindowCsrEffect(effect);
        }

        private void ApplyCsrEffect(CsrRetireEffect effect, ICanonicalCpuState state, byte vtId)
        {
            if (effect.ClearsArchitecturalExceptionState)
            {
                state.ClearExceptionCounters();
            }

            if (effect.HasRegisterWriteback)
            {
                state.WriteRegister(vtId, effect.DestRegId, effect.ReadValue);
            }

            if (effect.HasCsrWrite && _csrFile is not null)
            {
                _csrFile.Write(effect.CsrAddress, effect.CsrWriteValue, PrivilegeLevel.Machine);
            }
        }

        private ExecutionResult ExecuteSmtVt(InstructionIR instr, ICanonicalCpuState state, ulong bundleSerial, byte vtId)
        {
            ushort opcode = ResolveOpcode(instr);

            switch (opcode)
            {
                case IsaOpcodeValues.YIELD:
                {
                    return EnqueuePipelineEvent(
                        new YieldEvent { VtId = vtId, BundleSerial = bundleSerial });
                }

                case IsaOpcodeValues.WFE:
                {
                    return EnqueuePipelineEvent(
                        new WfeEvent { VtId = vtId, BundleSerial = bundleSerial });
                }

                case IsaOpcodeValues.SEV:
                {
                    return EnqueuePipelineEvent(
                        new SevEvent { VtId = vtId, BundleSerial = bundleSerial });
                }

                case IsaOpcodeValues.POD_BARRIER:
                {
                    return EnqueuePipelineEvent(
                        new PodBarrierEvent { VtId = vtId, BundleSerial = bundleSerial });
                }

                case IsaOpcodeValues.VT_BARRIER:
                {
                    return EnqueuePipelineEvent(
                        new VtBarrierEvent { VtId = vtId, BundleSerial = bundleSerial });
                }

                case IsaOpcodeValues.STREAM_WAIT:
                    return ThrowUnsupportedEagerStreamControlOpcode(instr);

                default:
                    throw new InvalidOpcodeException($"Invalid opcode {FormatOpcode(opcode)} in SmtVt execution unit", FormatOpcode(opcode), -1, false);
            }
        }

        private static void CaptureSmtVtRetireWindowPublications(
            InstructionIR instr,
            ICanonicalCpuState state,
            ref CpuCore.RetireWindowBatch retireBatch,
            ulong bundleSerial,
            byte vtId)
        {
            ushort opcode = ResolveOpcode(instr);

            if (opcode == IsaOpcodeValues.STREAM_WAIT)
            {
                retireBatch.NoteSerializingBoundary();
                return;
            }

            Core.Pipeline.PipelineEvent pipelineEvent = opcode switch
            {
                IsaOpcodeValues.YIELD => new YieldEvent
                {
                    VtId = vtId,
                    BundleSerial = bundleSerial
                },
                IsaOpcodeValues.WFE => new WfeEvent
                {
                    VtId = vtId,
                    BundleSerial = bundleSerial
                },
                IsaOpcodeValues.SEV => new SevEvent
                {
                    VtId = vtId,
                    BundleSerial = bundleSerial
                },
                IsaOpcodeValues.POD_BARRIER => new PodBarrierEvent
                {
                    VtId = vtId,
                    BundleSerial = bundleSerial
                },
                IsaOpcodeValues.VT_BARRIER => new VtBarrierEvent
                {
                    VtId = vtId,
                    BundleSerial = bundleSerial
                },
                _ => throw new InvalidOpcodeException(
                    $"Invalid opcode {FormatOpcode(opcode)} in SmtVt execution unit",
                    FormatOpcode(opcode),
                    -1,
                    false)
            };

            retireBatch.CaptureRetireWindowPipelineEvent(
                pipelineEvent,
                ResolveRetireWindowPublicationSmtVtOrderGuarantee(opcode),
                ReadExecutionPc(state, vtId),
                vtId,
                RequiresRetireWindowPublicationSerializingBoundaryFollowThrough(instr));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Core.SystemEventOrderGuarantee ResolveRetireWindowPublicationSmtVtOrderGuarantee(
            ushort opcode)
        {
            return opcode switch
            {
                IsaOpcodeValues.WFE or
                IsaOpcodeValues.SEV
                    => Core.SystemEventOrderGuarantee.DrainMemory,
                IsaOpcodeValues.YIELD or
                IsaOpcodeValues.POD_BARRIER or
                IsaOpcodeValues.VT_BARRIER
                    => Core.SystemEventOrderGuarantee.None,
                _ => Core.SystemEventOrderGuarantee.None
            };
        }

    }
}

