using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;

namespace YAKSys_Hybrid_CPU.Core
{
    public readonly partial record struct Lane6VirtualToken
    {
        public bool ExposesHostTokenHandle(DmaStreamComputeTokenHandle hostHandle) =>
            !hostHandle.IsDefault &&
            (VirtualTokenId == hostHandle.TokenId ||
             GuestTokenId == hostHandle.TokenId);
    }
}
