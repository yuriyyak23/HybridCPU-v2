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

        internal static ulong SignExtendByteFromScalar(ulong op1) =>
            unchecked((ulong)(long)(sbyte)(byte)op1);

        internal static ulong SignExtendHalfFromScalar(ulong op1) =>
            unchecked((ulong)(long)(short)(ushort)op1);

        internal static ulong ZeroExtendHalfFromScalar(ulong op1) =>
            unchecked((ushort)op1);

        internal static ulong ConditionalZeroIfEqualZero(ulong op1, ulong op2) =>
            op2 == 0 ? 0UL : op1;

        internal static ulong ConditionalZeroIfNotEqualZero(ulong op1, ulong op2) =>
            op2 != 0 ? 0UL : op1;

        internal static ulong CountLeadingZeros64(ulong op1) =>
            (ulong)System.Numerics.BitOperations.LeadingZeroCount(op1);

        internal static ulong CountTrailingZeros64(ulong op1) =>
            op1 == 0
                ? 64UL
                : (ulong)System.Numerics.BitOperations.TrailingZeroCount(op1);

        internal static ulong CountPopulation64(ulong op1) =>
            (ulong)System.Numerics.BitOperations.PopCount(op1);

        internal static ulong RotateLeft64(ulong op1, ulong op2)
        {
            int amount = (int)(op2 & ShiftAmountMask);
            return amount == 0
                ? op1
                : unchecked((op1 << amount) | (op1 >> (64 - amount)));
        }

        internal static ulong RotateRight64(ulong op1, ulong op2)
        {
            int amount = (int)(op2 & ShiftAmountMask);
            return amount == 0
                ? op1
                : unchecked((op1 >> amount) | (op1 << (64 - amount)));
        }

        internal static ulong AndNot64(ulong op1, ulong op2) =>
            op1 & ~op2;

        internal static ulong OrNot64(ulong op1, ulong op2) =>
            op1 | ~op2;

        internal static ulong Xnor64(ulong op1, ulong op2) =>
            ~(op1 ^ op2);

        internal static ulong BitSet64(ulong op1, ulong op2) =>
            op1 | (1UL << (int)(op2 & ShiftAmountMask));

        internal static ulong BitClear64(ulong op1, ulong op2) =>
            op1 & ~(1UL << (int)(op2 & ShiftAmountMask));

        internal static ulong BitInvert64(ulong op1, ulong op2) =>
            op1 ^ (1UL << (int)(op2 & ShiftAmountMask));

        internal static ulong BitExtract64(ulong op1, ulong op2) =>
            (op1 >> (int)(op2 & ShiftAmountMask)) & 1UL;

        internal static ulong MinSigned64(ulong op1, ulong op2) =>
            unchecked((long)op1) <= unchecked((long)op2) ? op1 : op2;

        internal static ulong MaxSigned64(ulong op1, ulong op2) =>
            unchecked((long)op1) >= unchecked((long)op2) ? op1 : op2;

        internal static ulong MinUnsigned64(ulong op1, ulong op2) =>
            op1 <= op2 ? op1 : op2;

        internal static ulong MaxUnsigned64(ulong op1, ulong op2) =>
            op1 >= op2 ? op1 : op2;

        internal static ulong ReverseBytes64(ulong op1) =>
            ((op1 & 0x0000_0000_0000_00FFUL) << 56)
            | ((op1 & 0x0000_0000_0000_FF00UL) << 40)
            | ((op1 & 0x0000_0000_00FF_0000UL) << 24)
            | ((op1 & 0x0000_0000_FF00_0000UL) << 8)
            | ((op1 & 0x0000_00FF_0000_0000UL) >> 8)
            | ((op1 & 0x0000_FF00_0000_0000UL) >> 24)
            | ((op1 & 0x00FF_0000_0000_0000UL) >> 40)
            | ((op1 & 0xFF00_0000_0000_0000UL) >> 56);

        internal static ulong ReverseBitsInEachByte64(ulong op1)
        {
            op1 = ((op1 & 0x5555_5555_5555_5555UL) << 1)
                | ((op1 >> 1) & 0x5555_5555_5555_5555UL);
            op1 = ((op1 & 0x3333_3333_3333_3333UL) << 2)
                | ((op1 >> 2) & 0x3333_3333_3333_3333UL);
            op1 = ((op1 & 0x0F0F_0F0F_0F0F_0F0FUL) << 4)
                | ((op1 >> 4) & 0x0F0F_0F0F_0F0F_0F0FUL);
            return op1;
        }

        internal static ulong ShiftLeftOneAdd(ulong op1, ulong op2) =>
            unchecked((op1 << 1) + op2);

        internal static ulong ShiftLeftTwoAdd(ulong op1, ulong op2) =>
            unchecked((op1 << 2) + op2);

        internal static ulong ShiftLeftThreeAdd(ulong op1, ulong op2) =>
            unchecked((op1 << 3) + op2);

        internal static ulong AddUnsignedWord(ulong op1, ulong op2) =>
            unchecked(ZeroExtendWordFromScalar(op1) + op2);

        internal static ulong ShiftLeftOneAddUnsignedWord(ulong op1, ulong op2) =>
            unchecked((ZeroExtendWordFromScalar(op1) << 1) + op2);

        internal static ulong ShiftLeftTwoAddUnsignedWord(ulong op1, ulong op2) =>
            unchecked((ZeroExtendWordFromScalar(op1) << 2) + op2);

        internal static ulong ShiftLeftThreeAddUnsignedWord(ulong op1, ulong op2) =>
            unchecked((ZeroExtendWordFromScalar(op1) << 3) + op2);

        internal static ulong ShiftLeftUnsignedWordImmediate(ulong op1, ulong immediate) =>
            unchecked(ZeroExtendWordFromScalar(op1) << (int)(immediate & ShiftAmountMask));

        internal static ulong CarryLessMultiplyLow64(ulong op1, ulong op2)
        {
            ulong result = 0UL;
            ulong multiplicand = op1;
            ulong multiplier = op2;

            for (int bit = 0; bit < 64 && multiplier != 0; bit++)
            {
                if ((multiplier & 1UL) != 0)
                {
                    result ^= multiplicand;
                }

                multiplier >>= 1;
                multiplicand <<= 1;
            }

            return result;
        }

        internal static ulong CarryLessMultiplyHigh64(ulong op1, ulong op2) =>
            CarryLessMultiplyWindow64(op1, op2, startBit: 64);

        internal static ulong CarryLessMultiplyReverse64(ulong op1, ulong op2) =>
            CarryLessMultiplyWindow64(op1, op2, startBit: 63);

        private static ulong CarryLessMultiplyWindow64(ulong op1, ulong op2, int startBit)
        {
            ulong result = 0UL;

            for (int multiplierBit = 0; multiplierBit < 64; multiplierBit++)
            {
                if (((op2 >> multiplierBit) & 1UL) == 0)
                {
                    continue;
                }

                for (int multiplicandBit = 0; multiplicandBit < 64; multiplicandBit++)
                {
                    if (((op1 >> multiplicandBit) & 1UL) == 0)
                    {
                        continue;
                    }

                    int resultBit = multiplierBit + multiplicandBit - startBit;
                    if ((uint)resultBit < 64U)
                    {
                        result ^= 1UL << resultBit;
                    }
                }
            }

            return result;
        }

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
                case Processor.CPU_Core.InstructionsEnum.ADD:
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
                case Processor.CPU_Core.InstructionsEnum.SUB:
                    return op1 - op2;
                case Processor.CPU_Core.InstructionsEnum.MUL:
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
                case Processor.CPU_Core.InstructionsEnum.DIV:
                    return op2 != 0 ? op1 / op2 : 0;


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
                case Processor.CPU_Core.InstructionsEnum.CZERO_EQZ:
                    return ConditionalZeroIfEqualZero(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.CZERO_NEZ:
                    return ConditionalZeroIfNotEqualZero(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.CLZ:
                    return CountLeadingZeros64(op1);
                case Processor.CPU_Core.InstructionsEnum.CTZ:
                    return CountTrailingZeros64(op1);
                case Processor.CPU_Core.InstructionsEnum.CPOP:
                    return CountPopulation64(op1);
                case Processor.CPU_Core.InstructionsEnum.SEXT_B:
                    return SignExtendByteFromScalar(op1);
                case Processor.CPU_Core.InstructionsEnum.SEXT_H:
                    return SignExtendHalfFromScalar(op1);
                case Processor.CPU_Core.InstructionsEnum.ZEXT_H:
                    return ZeroExtendHalfFromScalar(op1);
                case Processor.CPU_Core.InstructionsEnum.ROL:
                    return RotateLeft64(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.ROR:
                    return RotateRight64(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.ROLI:
                    return RotateLeft64(op1, immediate);
                case Processor.CPU_Core.InstructionsEnum.RORI:
                    return RotateRight64(op1, immediate);
                case Processor.CPU_Core.InstructionsEnum.BSET:
                    return BitSet64(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.BCLR:
                    return BitClear64(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.BINV:
                    return BitInvert64(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.BEXT:
                    return BitExtract64(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.BSETI:
                    return BitSet64(op1, immediate);
                case Processor.CPU_Core.InstructionsEnum.BCLRI:
                    return BitClear64(op1, immediate);
                case Processor.CPU_Core.InstructionsEnum.BINVI:
                    return BitInvert64(op1, immediate);
                case Processor.CPU_Core.InstructionsEnum.BEXTI:
                    return BitExtract64(op1, immediate);
                case Processor.CPU_Core.InstructionsEnum.ANDN:
                    return AndNot64(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.ORN:
                    return OrNot64(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.XNOR:
                    return Xnor64(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.MIN:
                    return MinSigned64(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.MAX:
                    return MaxSigned64(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.MINU:
                    return MinUnsigned64(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.MAXU:
                    return MaxUnsigned64(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.REV8:
                    return ReverseBytes64(op1);
                case Processor.CPU_Core.InstructionsEnum.BREV8:
                    return ReverseBitsInEachByte64(op1);
                case Processor.CPU_Core.InstructionsEnum.SH1ADD:
                    return ShiftLeftOneAdd(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.SH2ADD:
                    return ShiftLeftTwoAdd(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.SH3ADD:
                    return ShiftLeftThreeAdd(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.ADD_UW:
                    return AddUnsignedWord(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.SH1ADD_UW:
                    return ShiftLeftOneAddUnsignedWord(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.SH2ADD_UW:
                    return ShiftLeftTwoAddUnsignedWord(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.SH3ADD_UW:
                    return ShiftLeftThreeAddUnsignedWord(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.SLLI_UW:
                    return ShiftLeftUnsignedWordImmediate(op1, immediate);
                case Processor.CPU_Core.InstructionsEnum.CLMUL:
                    return CarryLessMultiplyLow64(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.CLMULH:
                    return CarryLessMultiplyHigh64(op1, op2);
                case Processor.CPU_Core.InstructionsEnum.CLMULR:
                    return CarryLessMultiplyReverse64(op1, op2);

                case (Processor.CPU_Core.InstructionsEnum)52:
                    return ~op1;

                case Processor.CPU_Core.InstructionsEnum.SLL:
                    return op1 << (int)(immediate & ShiftAmountMask);
                case Processor.CPU_Core.InstructionsEnum.SRL:
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



                default:
                    return 0;
            }
        }
    }
}
