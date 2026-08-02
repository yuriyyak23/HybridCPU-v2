using System;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public sealed partial class CPU_Core
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private int NormalizePipelineStateVtId(int vtId)
            {
                if ((uint)vtId >= (uint)SmtWays)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(vtId),
                        vtId,
                        $"Pipeline state carries an invalid virtual-thread id; expected [0, {SmtWays - 1}].");
                }

                return vtId;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private int GetCurrentDecodeThreadId() =>
                NormalizePipelineStateVtId(pipeID.MicroOp?.OwnerThreadId ?? ReadActiveVirtualThreadId());

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ulong ResolveCurrentScalarMicroOpExecutionPc()
            {
                if (pipeEX.Valid)
                    return pipeEX.PC;
                if (pipeID.Valid)
                    return pipeID.PC;
                if (pipeIF.Valid)
                    return pipeIF.PC;

                return ReadActiveLivePc();
            }
        }
    }
}
