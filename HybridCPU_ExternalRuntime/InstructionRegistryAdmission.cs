namespace HybridCPU.ExternalRuntime;

public sealed partial class HybridCpuExternalRuntime
{
    // Publish the existing machine registry before any provider instance can admit
    // CPU-backed work. CLR type initialization serializes concurrent constructors.
    static HybridCpuExternalRuntime() => YAKSys_Hybrid_CPU.Core.InstructionRegistry.Initialize();
}
