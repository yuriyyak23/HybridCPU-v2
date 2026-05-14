using System;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Canonical static ALU computation helper.
    ///
    /// Purpose: single source of truth for all scalar integer / FP-light operations.
    /// Both <see cref="Pipeline.MicroOps.ScalarALUMicroOp"/> (MicroOp-path) and
    /// the pipeline execute-stage scalar path delegate here directly, eliminating
    /// the duplicated switch-case that previously existed in both places.
    /// </summary>
    public static class ScalarAluOps
    {
        /// <summary>
        /// Mask applied to shift-amount operands to keep them in the valid
        /// 64-bit range [0, 63] (6 bits).
        /// </summary>
        private const int ShiftAmountMask = 0x3F;
        private const int WordShiftAmountMask = 0x1F;

        internal static ulong SignExtendWord(uint word) =>
            unchecked((ulong)(long)(int)word);

        internal static ulong SignExtendWordFromScalar(ulong op1) =>
            SignExtendWord(unchecked((uint)op1));

        internal static ulong ZeroExtendWordFromScalar(ulong op1) =>
            unchecked((uint)op1);

        internal static ulong AddWordImmediate(ulong op1, long signedImmediate)
        {
            uint sum = unchecked((uint)op1 + (uint)signedImmediate);
            return SignExtendWord(sum);
        }

        internal static ulong AddWord(ulong op1, ulong op2)
        {
            uint sum = unchecked((uint)op1 + (uint)op2);
            return SignExtendWord(sum);
        }

        internal static ulong SubWord(ulong op1, ulong op2)
        {
            uint difference = unchecked((uint)op1 - (uint)op2);
            return SignExtendWord(difference);
        }

        internal static ulong MultiplyWord(ulong op1, ulong op2)
        {
            uint product = unchecked((uint)((ulong)(uint)op1 * (uint)op2));
            return SignExtendWord(product);
        }

        internal static ulong DivideWord(ulong op1, ulong op2)
        {
            int dividend = unchecked((int)(uint)op1);
            int divisor = unchecked((int)(uint)op2);

            if (divisor == 0)
            {
                return ulong.MaxValue;
            }

            if (dividend == int.MinValue && divisor == -1)
            {
                return SignExtendWord(unchecked((uint)int.MinValue));
            }

            int quotient = dividend / divisor;
            return unchecked((ulong)(long)quotient);
        }

        internal static ulong DivideUnsignedWord(ulong op1, ulong op2)
        {
            uint dividend = unchecked((uint)op1);
            uint divisor = unchecked((uint)op2);

            if (divisor == 0)
            {
                return ulong.MaxValue;
            }

            uint quotient = dividend / divisor;
            return SignExtendWord(quotient);
        }

        internal static ulong RemainderWord(ulong op1, ulong op2)
        {
            int dividend = unchecked((int)(uint)op1);
            int divisor = unchecked((int)(uint)op2);

            if (divisor == 0)
            {
                return SignExtendWord(unchecked((uint)dividend));
            }

            if (dividend == int.MinValue && divisor == -1)
            {
                return 0;
            }

            int remainder = dividend % divisor;
            return unchecked((ulong)(long)remainder);
        }

        internal static ulong RemainderUnsignedWord(ulong op1, ulong op2)
        {
            uint dividend = unchecked((uint)op1);
            uint divisor = unchecked((uint)op2);

            if (divisor == 0)
            {
                return SignExtendWord(dividend);
            }

            uint remainder = dividend % divisor;
            return SignExtendWord(remainder);
        }

        internal static ulong ShiftLeftWord(ulong op1, ulong op2)
        {
            uint shifted = unchecked((uint)op1 << (int)(op2 & WordShiftAmountMask));
            return SignExtendWord(shifted);
        }

        internal static ulong ShiftLeftWordImmediate(ulong op1, long signedImmediate)
        {
            uint shifted = unchecked((uint)op1 << (int)((ulong)signedImmediate & WordShiftAmountMask));
            return SignExtendWord(shifted);
        }

        internal static ulong ShiftRightLogicalWordImmediate(ulong op1, long signedImmediate)
        {
            uint shifted = (uint)op1 >> (int)((ulong)signedImmediate & WordShiftAmountMask);
            return SignExtendWord(shifted);
        }

        internal static ulong ShiftRightArithmeticWordImmediate(ulong op1, long signedImmediate)
        {
            int shifted = (int)(uint)op1 >> (int)((ulong)signedImmediate & WordShiftAmountMask);
            return unchecked((ulong)(long)shifted);
        }

        internal static ulong ShiftRightLogicalWord(ulong op1, ulong op2)
        {
            uint shifted = (uint)op1 >> (int)(op2 & WordShiftAmountMask);
            return SignExtendWord(shifted);
        }

        internal static ulong ShiftRightArithmeticWord(ulong op1, ulong op2)
        {
            int shifted = (int)(uint)op1 >> (int)(op2 & WordShiftAmountMask);
            return unchecked((ulong)(long)shifted);
        }

        /// <summary>
        /// Compute the result of a scalar ALU operation.
        /// </summary>
        /// <param name="opCode">Instruction opcode.</param>
        /// <param name="op1">First (source-1) register operand.</param>
        /// <param name="op2">Second operand or register-register source-2 operand.</param>
        /// <param name="immediate">Canonical runtime immediate payload.</param>
        /// <param name="instructionPc">Instruction PC for PC-relative forms such as AUIPC.</param>
        /// <returns>64-bit unsigned result.</returns>
        public static ulong Compute(
            uint opCode,
            ulong op1,
            ulong op2,
            ulong immediate,
            ulong instructionPc = 0)
        {
            long signedImmediate = unchecked((long)immediate);

            switch ((Processor.CPU_Core.InstructionsEnum)opCode)
            {
                case Processor.CPU_Core.InstructionsEnum.Addition:
                    return op1 + op2;
                case Processor.CPU_Core.InstructionsEnum.ADDW:
                    return AddWord(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.SUBW:
                    return SubWord(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.SLLW:
                    return ShiftLeftWord(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.SRLW:
                    return ShiftRightLogicalWord(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.SRAW:
                    return ShiftRightArithmeticWord(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.Subtraction:
                    return op1 - op2;
                case Processor.CPU_Core.InstructionsEnum.Multiplication:
                    return op1 * op2;
                case Processor.CPU_Core.InstructionsEnum.MULW:
                    return MultiplyWord(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.DIVW:
                    return DivideWord(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.DIVUW:
                    return DivideUnsignedWord(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.REMW:
                    return RemainderWord(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.REMUW:
                    return RemainderUnsignedWord(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.SEXT_W:
                    return SignExtendWordFromScalar(op1);
                case Processor.CPU_Core.InstructionsEnum.ZEXT_W:
                    return ZeroExtendWordFromScalar(op1);
                case Processor.CPU_Core.InstructionsEnum.Division:
                    return op2 != 0 ? op1 / op2 : 0;
                case Processor.CPU_Core.InstructionsEnum.Modulus:
                    return op2 != 0 ? op1 % op2 : 0;

                case (Processor.CPU_Core.InstructionsEnum)45:
                    return (ulong)Math.Sqrt((double)op1);

                case Processor.CPU_Core.InstructionsEnum.MULH:
                    return (ulong)(long)(((System.Int128)(long)op1 * (System.Int128)(long)op2) >> 64);
                case Processor.CPU_Core.InstructionsEnum.MULHU:
                    return (ulong)(((System.UInt128)op1 * (System.UInt128)op2) >> 64);
                case Processor.CPU_Core.InstructionsEnum.MULHSU:
                    return (ulong)(long)(((System.Int128)(long)op1 * (System.Int128)(ulong)op2) >> 64);

                case Processor.CPU_Core.InstructionsEnum.DIVU:
                    return op2 != 0 ? op1 / op2 : ulong.MaxValue;
                case Processor.CPU_Core.InstructionsEnum.REM:
                    return op2 != 0 ? (ulong)((long)op1 % (long)op2) : op1;
                case Processor.CPU_Core.InstructionsEnum.REMU:
                    return op2 != 0 ? op1 % op2 : op1;

                case Processor.CPU_Core.InstructionsEnum.AND:
                    return op1 & op2;
                case Processor.CPU_Core.InstructionsEnum.OR:
                    return op1 | op2;
                case Processor.CPU_Core.InstructionsEnum.XOR:
                    return op1 ^ op2;

                case (Processor.CPU_Core.InstructionsEnum)52:
                    return ~op1;

                case Processor.CPU_Core.InstructionsEnum.ShiftLeft:
                    return op1 << (int)(immediate & ShiftAmountMask);
                case Processor.CPU_Core.InstructionsEnum.ShiftRight:
                    return op1 >> (int)(immediate & ShiftAmountMask);
                case Processor.CPU_Core.InstructionsEnum.SRA:
                    return (ulong)((long)op1 >> (int)(op2 & ShiftAmountMask));

                case Processor.CPU_Core.InstructionsEnum.SLT:
                    return (long)op1 < (long)op2 ? 1UL : 0UL;
                case Processor.CPU_Core.InstructionsEnum.SLTU:
                    return op1 < op2 ? 1UL : 0UL;

                case Processor.CPU_Core.InstructionsEnum.ADDI:
                    return op1 + immediate;
                case Processor.CPU_Core.InstructionsEnum.ADDIW:
                    return AddWordImmediate(op1, signedImmediate);
                case Processor.CPU_Core.InstructionsEnum.SLLIW:
                    return ShiftLeftWordImmediate(op1, signedImmediate);
                case Processor.CPU_Core.InstructionsEnum.SRLIW:
                    return ShiftRightLogicalWordImmediate(op1, signedImmediate);
                case Processor.CPU_Core.InstructionsEnum.SRAIW:
                    return ShiftRightArithmeticWordImmediate(op1, signedImmediate);
                case Processor.CPU_Core.InstructionsEnum.ANDI:
                    return op1 & immediate;
                case Processor.CPU_Core.InstructionsEnum.ORI:
                    return op1 | immediate;
                case Processor.CPU_Core.InstructionsEnum.XORI:
                    return op1 ^ immediate;
                case Processor.CPU_Core.InstructionsEnum.SLLI:
                    return op1 << (int)(immediate & ShiftAmountMask);
                case Processor.CPU_Core.InstructionsEnum.SRLI:
                    return op1 >> (int)(immediate & ShiftAmountMask);
                case Processor.CPU_Core.InstructionsEnum.SRAI:
                    return (ulong)((long)op1 >> (int)(immediate & ShiftAmountMask));
                case Processor.CPU_Core.InstructionsEnum.SLTI:
                    return (long)op1 < signedImmediate ? 1UL : 0UL;
                case Processor.CPU_Core.InstructionsEnum.SLTIU:
                    return op1 < immediate ? 1UL : 0UL;
                case Processor.CPU_Core.InstructionsEnum.LUI:
                    return (ulong)(signedImmediate << 12);
                case Processor.CPU_Core.InstructionsEnum.AUIPC:
                    return instructionPc + (ulong)(signedImmediate << 12);

                case Processor.CPU_Core.InstructionsEnum.Move_Num:
                    return immediate;
                case Processor.CPU_Core.InstructionsEnum.Move:
                    return op1;

                default:
                    return 0;
            }
        }
    }
}
