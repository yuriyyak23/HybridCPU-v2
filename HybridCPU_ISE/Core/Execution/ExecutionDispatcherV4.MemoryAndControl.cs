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
        // ── Memory execution unit ─────────────────────────────────────────────

        /// <summary>
        /// Memory execution unit.  When a <see cref="MemoryUnit"/> is attached,
        /// typed load/store operations (LB–LD, SB–SD) are fully executed with
        /// correct sign/zero extension and alignment checking (Phase 07).
        /// When no memory unit is available (legacy or EA-only mode), the method
        /// validates the opcode and returns the effective address for the LSU
        /// pipeline stage to complete.
        /// </summary>
        private ExecutionResult ExecuteMemory(InstructionIR instr, ICanonicalCpuState state, byte vtId)
        {
            ushort opcode = ResolveOpcode(instr);

            // Validate the opcode is a known ISA v4 memory opcode
            bool known = opcode is
                IsaOpcodeValues.LB  or IsaOpcodeValues.LBU or
                IsaOpcodeValues.LH  or IsaOpcodeValues.LHU or
                IsaOpcodeValues.LW  or IsaOpcodeValues.LWU or IsaOpcodeValues.LD  or
                IsaOpcodeValues.SB  or IsaOpcodeValues.SH  or
                IsaOpcodeValues.SW  or IsaOpcodeValues.SD  or
                IsaOpcodeValues.VGATHER or IsaOpcodeValues.VSCATTER or
                IsaOpcodeValues.MTILE_LOAD or IsaOpcodeValues.MTILE_STORE;

            if (!known)
                throw new InvalidOpcodeException($"Invalid opcode {FormatOpcode(opcode)} in Memory execution unit", FormatOpcode(opcode), -1, false);

            // EA-04: typed scalar eager execute resolves access facts only.
            bool isTypedScalar = opcode is
                IsaOpcodeValues.LB  or IsaOpcodeValues.LBU or
                IsaOpcodeValues.LH  or IsaOpcodeValues.LHU or
                IsaOpcodeValues.LW  or IsaOpcodeValues.LWU or IsaOpcodeValues.LD  or
                IsaOpcodeValues.SB  or IsaOpcodeValues.SH  or
                IsaOpcodeValues.SW  or IsaOpcodeValues.SD;

            if (isTypedScalar && _memoryUnit != null)
            {
                (ulong rs1Value, ulong rs2Value) = ResolveMemoryOperandValues(instr, state, vtId);
                ResolvedMemoryAccess resolved = _memoryUnit.ResolveArchitecturalAccess(
                    instr,
                    rs1Value,
                    rs2Value);
                return ExecutionResult.Ok(resolved.EffectiveAddress);
            }

            // EA-only mode: return effective address; LSU completes the access
            ulong eaFallback = (ulong)((long)ReadExecutionRegister(state, vtId, instr.Rs1) + instr.Imm);
            return ExecutionResult.Ok(eaFallback);
        }

        private void CaptureMemoryRetireWindowPublications(
            InstructionIR instr,
            ICanonicalCpuState state,
            ref CpuCore.RetireWindowBatch retireBatch,
            byte vtId)
        {
            ushort opcode = ResolveOpcode(instr);

            bool supportsRetireWindowPublication = opcode is
                IsaOpcodeValues.Load or
                IsaOpcodeValues.Store or
                IsaOpcodeValues.LB  or IsaOpcodeValues.LBU or
                IsaOpcodeValues.LH  or IsaOpcodeValues.LHU or
                IsaOpcodeValues.LW  or IsaOpcodeValues.LWU or IsaOpcodeValues.LD  or
                IsaOpcodeValues.SB  or IsaOpcodeValues.SH  or
                IsaOpcodeValues.SW  or IsaOpcodeValues.SD;

            if (!supportsRetireWindowPublication)
            {
                ThrowUnsupportedRetireWindowPublicationMemoryOpcode(instr);
                return;
            }

            if (_memoryUnit is null)
            {
                ThrowMissingMemoryUnitForRetireWindowPublication(instr);
                return;
            }

            (ulong rs1Value, ulong rs2Value) = ResolveMemoryOperandValues(instr, state, vtId);
            ResolvedMemoryAccess resolved = _memoryUnit.ResolveArchitecturalAccess(
                instr,
                rs1Value,
                rs2Value);

            if (resolved.HasStoreCommit)
            {
                retireBatch.CaptureRetireWindowScalarMemoryStore(
                    resolved.EffectiveAddress,
                    resolved.StoreData,
                    resolved.AccessSize);
                return;
            }

            if (!resolved.HasRegisterWrite)
            {
                return;
            }

            retireBatch.AppendRetireRecord(
                RetireRecord.RegisterWrite(
                    vtId,
                    resolved.RegisterDestination,
                    resolved.RegisterWriteValue));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (ulong Rs1Value, ulong Rs2Value) ResolveMemoryOperandValues(
            InstructionIR instr,
            ICanonicalCpuState state,
            byte vtId)
        {
            ulong rs1Value = instr.HasAbsoluteAddressing
                ? 0UL
                : ReadExecutionRegister(state, vtId, instr.Rs1);
            ulong rs2Value = RequiresMemoryStoreSourceValue(instr)
                ? ReadExecutionRegister(state, vtId, instr.Rs2)
                : 0UL;
            return (rs1Value, rs2Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RequiresMemoryStoreSourceValue(InstructionIR instr) =>
            ResolveOpcode(instr) is
                IsaOpcodeValues.SB or
                IsaOpcodeValues.SH or
                IsaOpcodeValues.SW or
                IsaOpcodeValues.SD or
                IsaOpcodeValues.Store;

        // ── ControlFlow execution unit ────────────────────────────────────────

        private static ExecutionResult ResolveControlFlowResult(InstructionIR instr, ICanonicalCpuState state, byte vtId)
        {
            ushort opcode = ResolveOpcode(instr);
            ulong pc  = ReadExecutionPc(state, vtId);
            ulong rs1 = ReadExecutionRegister(state, vtId, instr.Rs1);
            ulong rs2 = ReadExecutionRegister(state, vtId, instr.Rs2);
            long  imm = instr.Imm;

            switch (opcode)
            {
                // Unconditional jumps (JAL / JALR)
                case IsaOpcodeValues.JAL:
                {
                    ulong retAddr = pc + 4;
                    return ExecutionResult.Redirect(newPc: pc + (ulong)imm, rdValue: retAddr);
                }
                case IsaOpcodeValues.JALR:
                {
                    ulong retAddr = pc + 4;
                    ulong target  = (rs1 + (ulong)imm) & ~1UL;
                    return ExecutionResult.Redirect(newPc: target, rdValue: retAddr);
                }

                // Conditional branches
                case IsaOpcodeValues.BEQ  when rs1 == rs2:
                case IsaOpcodeValues.BNE  when rs1 != rs2:
                case IsaOpcodeValues.BLT  when (long)rs1 <  (long)rs2:
                case IsaOpcodeValues.BGE  when (long)rs1 >= (long)rs2:
                case IsaOpcodeValues.BLTU when rs1 <  rs2:
                case IsaOpcodeValues.BGEU when rs1 >= rs2:
                    return ExecutionResult.Redirect(newPc: pc + (ulong)imm);

                // Branch not taken — fall through
                case IsaOpcodeValues.BEQ:
                case IsaOpcodeValues.BNE:
                case IsaOpcodeValues.BLT:
                case IsaOpcodeValues.BGE:
                case IsaOpcodeValues.BLTU:
                case IsaOpcodeValues.BGEU:
                    return ExecutionResult.Ok();

                default:
                    throw new InvalidOpcodeException($"Invalid opcode {FormatOpcode(opcode)} in ControlFlow execution unit", FormatOpcode(opcode), -1, false);
            }
        }

        private static ExecutionResult ExecuteControlFlow(InstructionIR instr, ICanonicalCpuState state, byte vtId)
        {
            ushort opcode = ResolveOpcode(instr);
            ExecutionResult result = ResolveControlFlowResult(instr, state, vtId);
            if (opcode is IsaOpcodeValues.JAL or IsaOpcodeValues.JALR && instr.Rd != 0)
            {
                state.WriteRegister(vtId, instr.Rd, result.Value);
            }

            return result;
        }

        private static void CaptureControlFlowRetireWindowPublications(
            InstructionIR instr,
            ICanonicalCpuState state,
            ref CpuCore.RetireWindowBatch retireBatch,
            byte vtId)
        {
            ushort opcode = ResolveOpcode(instr);
            ExecutionResult result = ResolveControlFlowResult(instr, state, vtId);

            if (opcode is IsaOpcodeValues.JAL or IsaOpcodeValues.JALR &&
                instr.Rd != 0)
            {
                retireBatch.AppendRetireRecord(
                    RetireRecord.RegisterWrite(
                        vtId,
                        instr.Rd,
                        result.Value));
            }

            if (result.PcRedirected)
            {
                retireBatch.AppendRetireRecord(
                    RetireRecord.PcWrite(
                        vtId,
                        result.NewPc));
            }
        }

        // ── Atomic execution unit ─────────────────────────────────────────────

        private ExecutionResult ExecuteAtomic(InstructionIR instr, ICanonicalCpuState state, byte vtId)
        {
            throw new InvalidOperationException(
                $"Atomic opcode {FormatOpcode(ResolveOpcode(instr))} reached ExecutionDispatcherV4 eager execute surface without an authoritative retire-window publication follow-through. " +
                "Direct callers must use CaptureRetireWindowPublications(...) or the mainline retire path.");
        }

        // ── System execution unit ─────────────────────────────────────────────
    }
}

