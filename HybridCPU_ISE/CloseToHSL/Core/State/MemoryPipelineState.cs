namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// MEM/WB-facing latches and per-core memory bindings. Referenced memory
/// subsystem/controller, main memory and atomic unit retain their authorities.
/// </summary>
internal sealed class MemoryPipelineState
{
    internal Processor.CPU_Core.MemoryStage Memory;
    internal Processor.CPU_Core.WriteBackStage WriteBack;
    internal byte[]? ExplicitPacketImmediateReadBuffer;
    internal Processor.MainMemoryArea? MainMemory;
    internal YAKSys_Hybrid_CPU.Memory.MemorySubsystem? MemorySubsystem;
    internal bool MemorySubsystemCaptured;
    internal Memory.IAtomicMemoryUnit? AtomicMemoryUnit;
}
