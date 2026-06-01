using System;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            private static InvalidOperationException CreateUnsupportedOptionalScalarXsqrtContourException()
            {
                return new InvalidOperationException(
                    "Optional scalar XSQRT contour is unsupported on the authoritative ISE runtime surface. " +
                    "Helper emission and direct emulation must fail closed because canonical decode rejects raw opcode 45 " +
                    "without an authoritative scalar carrier/materializer follow-through.");
            }

            private static InvalidOperationException CreateUnsupportedOptionalScalarXfmacContourException()
            {
                return new InvalidOperationException(
                    "Optional scalar XFMAC contour is unsupported on the authoritative ISE runtime surface. " +
                    "Helper emission and direct emulation must fail closed because canonical decode rejects raw opcode 55 " +
                    "without an authoritative scalar carrier/materializer follow-through.");
            }

            public byte SquareRoot(ArchRegId accumulatorRegisterId, ArchRegId sourceRegisterId)
            {
                throw CreateUnsupportedOptionalScalarXsqrtContourException();
            }

            public byte FMAC(ArchRegId accumulatorRegisterId, ArchRegId firstOperandRegisterId, ArchRegId secondOperandRegisterId)
            {
                throw CreateUnsupportedOptionalScalarXfmacContourException();
            }
        }
    }
}
