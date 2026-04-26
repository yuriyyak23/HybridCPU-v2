using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private int NormalizePipelineStateVtId(int vtId) =>
                (uint)vtId < (uint)SmtWays ? vtId : ReadActiveVirtualThreadId();

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
