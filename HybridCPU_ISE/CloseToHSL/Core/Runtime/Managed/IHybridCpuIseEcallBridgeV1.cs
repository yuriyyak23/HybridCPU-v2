using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>Explicit production hook for trusted user ECALL handling at retirement.</summary>
public interface IHybridCpuIseEcallBridgeV1
{
    bool TryHandle(EcallEvent ecall, ICanonicalCpuState state, PrivilegeLevel privilege);
}
