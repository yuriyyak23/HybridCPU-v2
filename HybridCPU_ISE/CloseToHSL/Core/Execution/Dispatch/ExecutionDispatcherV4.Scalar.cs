using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
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
            long imm = instr.Imm;

            return opcode switch
            {
                // ── Reg-reg arithmetic ───────────────────────────────────────
                IsaOpcodeValues.ADD => rs1 + rs2,
                IsaOpcodeValues.ADDW => ScalarAluOps.AddWord(rs1, rs2),
                IsaOpcodeValues.SUBW => ScalarAluOps.SubWord(rs1, rs2),
                IsaOpcodeValues.SLLW => ScalarAluOps.ShiftLeftWord(rs1, rs2),
                IsaOpcodeValues.SRLW => ScalarAluOps.ShiftRightLogicalWord(rs1, rs2),
                IsaOpcodeValues.SRAW => ScalarAluOps.ShiftRightArithmeticWord(rs1, rs2),
                IsaOpcodeValues.SUB => rs1 - rs2,
                IsaOpcodeValues.MUL => rs1 * rs2,
                IsaOpcodeValues.MULW => ScalarAluOps.MultiplyWord(rs1, rs2),
                IsaOpcodeValues.DIVW => ScalarAluOps.DivideWord(rs1, rs2),
                IsaOpcodeValues.DIVUW => ScalarAluOps.DivideUnsignedWord(rs1, rs2),
                IsaOpcodeValues.REMW => ScalarAluOps.RemainderWord(rs1, rs2),
                IsaOpcodeValues.REMUW => ScalarAluOps.RemainderUnsignedWord(rs1, rs2),
                IsaOpcodeValues.SEXT_W => ScalarAluOps.SignExtendWordFromScalar(rs1),
                IsaOpcodeValues.ZEXT_W => ScalarAluOps.ZeroExtendWordFromScalar(rs1),
                IsaOpcodeValues.DIV => rs2 != 0 ? rs1 / rs2 : unchecked((ulong)-1L),

                // ── ISA v4 M-extension: high-half multiply ────────────────────
                IsaOpcodeValues.MULH => (ulong)(long)(((Int128)(long)rs1 * (Int128)(long)rs2) >> 64),
                IsaOpcodeValues.MULHU => (ulong)(((UInt128)rs1 * (UInt128)rs2) >> 64),
                // MULHSU: rs1 signed, rs2 unsigned — zero-extend rs2 into Int128
                IsaOpcodeValues.MULHSU => (ulong)(long)(((Int128)(long)rs1 * (Int128)(ulong)rs2) >> 64),

                // ── ISA v4 M-extension: unsigned divide and remainder ──────────
                // ISA spec: DIVU returns 2^XLEN-1 on div-by-zero;
                //           REM  returns dividend on div-by-zero;
                //           REMU returns dividend on div-by-zero.
                IsaOpcodeValues.DIVU => rs2 != 0 ? rs1 / rs2 : ulong.MaxValue,
                IsaOpcodeValues.REM => rs2 != 0 ? (ulong)((long)rs1 % (long)rs2) : rs1,
                IsaOpcodeValues.REMU => rs2 != 0 ? rs1 % rs2 : rs1,

                // ── Logical ───────────────────────────────────────────────────
                IsaOpcodeValues.AND => rs1 & rs2,
                IsaOpcodeValues.OR => rs1 | rs2,
                IsaOpcodeValues.XOR => rs1 ^ rs2,
                IsaOpcodeValues.CZERO_EQZ => ScalarAluOps.ConditionalZeroIfEqualZero(rs1, rs2),
                IsaOpcodeValues.CZERO_NEZ => ScalarAluOps.ConditionalZeroIfNotEqualZero(rs1, rs2),
                IsaOpcodeValues.CLZ => ScalarAluOps.CountLeadingZeros64(rs1),
                IsaOpcodeValues.CTZ => ScalarAluOps.CountTrailingZeros64(rs1),
                IsaOpcodeValues.CPOP => ScalarAluOps.CountPopulation64(rs1),
                IsaOpcodeValues.SEXT_B => ScalarAluOps.SignExtendByteFromScalar(rs1),
                IsaOpcodeValues.SEXT_H => ScalarAluOps.SignExtendHalfFromScalar(rs1),
                IsaOpcodeValues.ZEXT_H => ScalarAluOps.ZeroExtendHalfFromScalar(rs1),
                IsaOpcodeValues.ROL => ScalarAluOps.RotateLeft64(rs1, rs2),
                IsaOpcodeValues.ROR => ScalarAluOps.RotateRight64(rs1, rs2),
                IsaOpcodeValues.ROLI => ScalarAluOps.RotateLeft64(rs1, (ulong)imm),
                IsaOpcodeValues.RORI => ScalarAluOps.RotateRight64(rs1, (ulong)imm),
                IsaOpcodeValues.BSET => ScalarAluOps.BitSet64(rs1, rs2),
                IsaOpcodeValues.BCLR => ScalarAluOps.BitClear64(rs1, rs2),
                IsaOpcodeValues.BINV => ScalarAluOps.BitInvert64(rs1, rs2),
                IsaOpcodeValues.BEXT => ScalarAluOps.BitExtract64(rs1, rs2),
                IsaOpcodeValues.BSETI => ScalarAluOps.BitSet64(rs1, (ulong)imm),
                IsaOpcodeValues.BCLRI => ScalarAluOps.BitClear64(rs1, (ulong)imm),
                IsaOpcodeValues.BINVI => ScalarAluOps.BitInvert64(rs1, (ulong)imm),
                IsaOpcodeValues.BEXTI => ScalarAluOps.BitExtract64(rs1, (ulong)imm),
                IsaOpcodeValues.ANDN => ScalarAluOps.AndNot64(rs1, rs2),
                IsaOpcodeValues.ORN => ScalarAluOps.OrNot64(rs1, rs2),
                IsaOpcodeValues.XNOR => ScalarAluOps.Xnor64(rs1, rs2),
                IsaOpcodeValues.MIN => ScalarAluOps.MinSigned64(rs1, rs2),
                IsaOpcodeValues.MAX => ScalarAluOps.MaxSigned64(rs1, rs2),
                IsaOpcodeValues.MINU => ScalarAluOps.MinUnsigned64(rs1, rs2),
                IsaOpcodeValues.MAXU => ScalarAluOps.MaxUnsigned64(rs1, rs2),
                IsaOpcodeValues.REV8 => ScalarAluOps.ReverseBytes64(rs1),
                IsaOpcodeValues.BREV8 => ScalarAluOps.ReverseBitsInEachByte64(rs1),
                IsaOpcodeValues.SH1ADD => ScalarAluOps.ShiftLeftOneAdd(rs1, rs2),
                IsaOpcodeValues.SH2ADD => ScalarAluOps.ShiftLeftTwoAdd(rs1, rs2),
                IsaOpcodeValues.SH3ADD => ScalarAluOps.ShiftLeftThreeAdd(rs1, rs2),
                IsaOpcodeValues.ADD_UW => ScalarAluOps.AddUnsignedWord(rs1, rs2),
                IsaOpcodeValues.SH1ADD_UW => ScalarAluOps.ShiftLeftOneAddUnsignedWord(rs1, rs2),
                IsaOpcodeValues.SH2ADD_UW => ScalarAluOps.ShiftLeftTwoAddUnsignedWord(rs1, rs2),
                IsaOpcodeValues.SH3ADD_UW => ScalarAluOps.ShiftLeftThreeAddUnsignedWord(rs1, rs2),
                IsaOpcodeValues.SLLI_UW => ScalarAluOps.ShiftLeftUnsignedWordImmediate(rs1, (ulong)imm),
                IsaOpcodeValues.CLMUL => ScalarAluOps.CarryLessMultiplyLow64(rs1, rs2),
                IsaOpcodeValues.CLMULH => ScalarAluOps.CarryLessMultiplyHigh64(rs1, rs2),
                IsaOpcodeValues.CLMULR => ScalarAluOps.CarryLessMultiplyReverse64(rs1, rs2),

                // ── Shifts (register-register; immediate-based shifts use Imm) ─
                IsaOpcodeValues.SLL => rs1 << (int)(rs2 & 0x3F),
                IsaOpcodeValues.SRL => rs1 >> (int)(rs2 & 0x3F),
                IsaOpcodeValues.SRA => (ulong)((long)rs1 >> (int)(rs2 & 0x3F)),

                // ── Immediate ALU ─────────────────────────────────────────────
                IsaOpcodeValues.ADDI => rs1 + (ulong)imm,
                IsaOpcodeValues.ADDIW => ScalarAluOps.AddWordImmediate(rs1, imm),
                IsaOpcodeValues.SLLIW => ScalarAluOps.ShiftLeftWordImmediate(rs1, imm),
                IsaOpcodeValues.SRLIW => ScalarAluOps.ShiftRightLogicalWordImmediate(rs1, imm),
                IsaOpcodeValues.SRAIW => ScalarAluOps.ShiftRightArithmeticWordImmediate(rs1, imm),
                IsaOpcodeValues.ANDI => rs1 & (ulong)imm,
                IsaOpcodeValues.ORI => rs1 | (ulong)imm,
                IsaOpcodeValues.XORI => rs1 ^ (ulong)imm,
                IsaOpcodeValues.SLLI => rs1 << (int)((ulong)imm & 0x3F),
                IsaOpcodeValues.SRLI => rs1 >> (int)((ulong)imm & 0x3F),
                IsaOpcodeValues.SRAI => (ulong)((long)rs1 >> (int)((ulong)imm & 0x3F)),

                // ── Compare / set ─────────────────────────────────────────────
                IsaOpcodeValues.SLT => (long)rs1 < (long)rs2 ? 1UL : 0UL,
                IsaOpcodeValues.SLTU => rs1 < rs2 ? 1UL : 0UL,
                IsaOpcodeValues.SLTI => (long)rs1 < imm ? 1UL : 0UL,
                IsaOpcodeValues.SLTIU => rs1 < (ulong)imm ? 1UL : 0UL,

                // ── Upper immediate ───────────────────────────────────────────
                IsaOpcodeValues.LUI => (ulong)(imm << 12),
                IsaOpcodeValues.AUIPC => ReadExecutionPc(state, vtId) + (ulong)(imm << 12),

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
                vtId,
                instr.AcquireOrdering,
                instr.ReleaseOrdering);
            retireBatch.CaptureRetireWindowAtomicEffect(effect);
        }

    }
}
