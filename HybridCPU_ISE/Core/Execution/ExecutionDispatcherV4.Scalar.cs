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
        // ── ScalarAlu execution unit ─────────────────────────────────────────

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowUnsupportedRetireWindowPublicationMemoryOpcode(InstructionIR instr) =>
            throw new InvalidOperationException(
                $"Memory opcode {FormatOpcode(ResolveOpcode(instr))} does not expose an authoritative path through retire-window publication here; pipeline execution remains the supported path.");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ThrowMissingMemoryUnitForRetireWindowPublication(InstructionIR instr) =>
            throw new InvalidOperationException(
                $"Memory opcode {FormatOpcode(ResolveOpcode(instr))} requires a wired MemoryUnit for an authoritative path through retire-window publication; pipeline execution remains the supported path.");

        private static ulong ResolveScalarAluValue(InstructionIR instr, ICanonicalCpuState state, byte vtId)
        {
            ushort opcode = ResolveOpcode(instr);
            ulong rs1 = ReadExecutionRegister(state, vtId, instr.Rs1);
            ulong rs2 = ReadExecutionRegister(state, vtId, instr.Rs2);
            long  imm = instr.Imm;

            return opcode switch
            {
                // ── Reg-reg arithmetic ───────────────────────────────────────
                IsaOpcodeValues.Addition       => rs1 + rs2,
                IsaOpcodeValues.Subtraction    => rs1 - rs2,
                IsaOpcodeValues.Multiplication => rs1 * rs2,
                IsaOpcodeValues.Division       => rs2 != 0 ? rs1 / rs2 : unchecked((ulong)-1L),
                IsaOpcodeValues.Modulus        => rs2 != 0 ? rs1 % rs2 : rs1,

                // ── ISA v4 M-extension: high-half multiply ────────────────────
                IsaOpcodeValues.MULH   => (ulong)(long)(((Int128)(long)rs1 * (Int128)(long)rs2) >> 64),
                IsaOpcodeValues.MULHU  => (ulong)(((UInt128)rs1 * (UInt128)rs2) >> 64),
                // MULHSU: rs1 signed, rs2 unsigned — zero-extend rs2 into Int128
                IsaOpcodeValues.MULHSU => (ulong)(long)(((Int128)(long)rs1 * (Int128)(ulong)rs2) >> 64),

                // ── ISA v4 M-extension: unsigned divide and remainder ──────────
                // ISA spec: DIVU returns 2^XLEN-1 on div-by-zero;
                //           REM  returns dividend on div-by-zero;
                //           REMU returns dividend on div-by-zero.
                IsaOpcodeValues.DIVU => rs2 != 0 ? rs1 / rs2 : ulong.MaxValue,
                IsaOpcodeValues.REM  => rs2 != 0 ? (ulong)((long)rs1 % (long)rs2) : rs1,
                IsaOpcodeValues.REMU => rs2 != 0 ? rs1 % rs2 : rs1,

                // ── Logical ───────────────────────────────────────────────────
                IsaOpcodeValues.AND => rs1 & rs2,
                IsaOpcodeValues.OR  => rs1 | rs2,
                IsaOpcodeValues.XOR => rs1 ^ rs2,

                // ── Shifts (register-register; immediate-based shifts use Imm) ─
                IsaOpcodeValues.ShiftLeft  => rs1 << (int)(rs2 & 0x3F),
                IsaOpcodeValues.ShiftRight => rs1 >> (int)(rs2 & 0x3F),

                // ── Immediate ALU ─────────────────────────────────────────────
                IsaOpcodeValues.ADDI  => rs1 + (ulong)imm,
                IsaOpcodeValues.ANDI  => rs1 & (ulong)imm,
                IsaOpcodeValues.ORI   => rs1 | (ulong)imm,
                IsaOpcodeValues.XORI  => rs1 ^ (ulong)imm,
                IsaOpcodeValues.SLLI  => rs1 << (int)((ulong)imm & 0x3F),
                IsaOpcodeValues.SRLI  => rs1 >> (int)((ulong)imm & 0x3F),
                IsaOpcodeValues.SRAI  => (ulong)((long)rs1 >> (int)((ulong)imm & 0x3F)),

                // ── Compare / set ─────────────────────────────────────────────
                IsaOpcodeValues.SLT   => (long)rs1 < (long)rs2 ? 1UL : 0UL,
                IsaOpcodeValues.SLTU  => rs1 < rs2 ? 1UL : 0UL,
                IsaOpcodeValues.SLTI  => (long)rs1 < imm ? 1UL : 0UL,
                IsaOpcodeValues.SLTIU => rs1 < (ulong)imm ? 1UL : 0UL,

                // ── Upper immediate ───────────────────────────────────────────
                IsaOpcodeValues.LUI   => (ulong)(imm << 12),
                IsaOpcodeValues.AUIPC => ReadExecutionPc(state, vtId) + (ulong)(imm << 12),

                // ── Move ─────────────────────────────────────────────────────
                IsaOpcodeValues.Move_Num => (ulong)imm,
                IsaOpcodeValues.Move     => rs1,

                _ => throw new InvalidOpcodeException($"Invalid opcode {FormatOpcode(opcode)} in ScalarAlu execution unit", FormatOpcode(opcode), -1, false)
            };
        }

        private static ExecutionResult ExecuteScalarAlu(InstructionIR instr, ICanonicalCpuState state, byte vtId)
        {
            ulong result = ResolveScalarAluValue(instr, state, vtId);
            if (instr.Rd != 0)
                state.WriteRegister(vtId, instr.Rd, result);
            return ExecutionResult.Ok(result);
        }

        private static void CaptureScalarAluRetireWindowPublications(
            InstructionIR instr,
            ICanonicalCpuState state,
            ref CpuCore.RetireWindowBatch retireBatch,
            byte vtId)
        {
            if (instr.Rd == 0)
            {
                return;
            }

            retireBatch.AppendRetireRecord(
                RetireRecord.RegisterWrite(
                    vtId,
                    instr.Rd,
                    ResolveScalarAluValue(instr, state, vtId)));
        }

        private void CaptureAtomicRetireWindowPublications(
            InstructionIR instr,
            ICanonicalCpuState state,
            ref CpuCore.RetireWindowBatch retireBatch,
            byte vtId)
        {
            ushort opcode = ResolveOpcode(instr);
            AtomicRetireEffect effect = _atomicMemoryUnit.ResolveRetireEffect(
                opcode,
                instr.Rd,
                ReadExecutionRegister(state, vtId, instr.Rs1),
                ReadExecutionRegister(state, vtId, instr.Rs2),
                state.GetCoreID(),
                vtId);
            retireBatch.CaptureRetireWindowAtomicEffect(effect);
        }

    }
}

