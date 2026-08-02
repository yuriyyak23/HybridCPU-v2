using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace YAKSys_Hybrid_CPU.Core
{
    public readonly partial record struct Lane7VirtualToken
    {
        public bool ExposesHostTokenHandle(AcceleratorTokenHandle hostHandle) =>
            false;
    }
}
