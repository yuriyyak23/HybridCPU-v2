using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class Lane7Checkpoint
{
    public bool ContainsNativeTokenHandle(AcceleratorTokenHandle hostHandle) => false;
}
