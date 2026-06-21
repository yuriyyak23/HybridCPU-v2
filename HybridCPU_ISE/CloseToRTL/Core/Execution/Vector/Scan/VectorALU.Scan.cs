using HybridCPU_ISE.Arch;
using System;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Execution
{
    internal static partial class VectorALU
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyScanSumElement(
            uint op,
            DataTypeEnum dataType,
            ReadOnlySpan<byte> sourceElement,
            Span<byte> destinationElement,
            ref long signedAccumulator,
            ref ulong unsignedAccumulator,
            ref Processor.CPU_Core core)
        {
            if (op != (uint)Processor.CPU_Core.InstructionsEnum.VSCAN_SUM)
            {
                throw new InvalidOperationException(
                    $"VectorALU.ApplyScanSumElement rejected opcode 0x{op:X}; Phase 05A opens only VSCAN.SUM.");
            }

            MarkVectorDirty(ref core);

            if (DataTypeUtils.IsSignedInteger(dataType))
            {
                long value = ElementCodec.LoadI(sourceElement, 0, dataType);
                signedAccumulator = unchecked(signedAccumulator + value);
                ElementCodec.StoreI(destinationElement, 0, dataType, signedAccumulator);
                return;
            }

            if (DataTypeUtils.IsUnsignedInteger(dataType))
            {
                ulong value = ElementCodec.LoadU(sourceElement, 0, dataType);
                unsignedAccumulator = unchecked(unsignedAccumulator + value);
                ElementCodec.StoreU(destinationElement, 0, dataType, unsignedAccumulator);
                return;
            }

            throw ExecutionFaultContract.CreateUnsupportedVectorElementTypeException(
                $"VectorALU.ApplyScanSumElement rejected unsupported VSCAN.SUM DataType {dataType}. " +
                "Phase 05A publishes only integer prefix-sum semantics.");
        }
    }
}
