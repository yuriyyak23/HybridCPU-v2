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
                case Processor.CPU_Core.InstructionsEnum.Subtraction:
                    return op1 - op2;
                case Processor.CPU_Core.InstructionsEnum.Multiplication:
                    return op1 * op2;
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

                case Processor.CPU_Core.InstructionsEnum.SLT:
                    return (long)op1 < (long)op2 ? 1UL : 0UL;
                case Processor.CPU_Core.InstructionsEnum.SLTU:
                    return op1 < op2 ? 1UL : 0UL;

                case Processor.CPU_Core.InstructionsEnum.ADDI:
                    return op1 + immediate;
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
