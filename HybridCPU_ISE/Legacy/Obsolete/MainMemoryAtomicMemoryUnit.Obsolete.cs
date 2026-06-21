using System;

namespace YAKSys_Hybrid_CPU.Core.Memory
{
    public sealed partial class MainMemoryAtomicMemoryUnit
    {
        [Obsolete("Use MainMemoryAtomicMemoryUnit(Processor.MainMemoryArea mainMemory) with explicit binding. Implicit Processor.MainMemory fallback is disabled.")]
        public MainMemoryAtomicMemoryUnit()
        {
            throw new MainMemoryBindingUnavailableException(
                nameof(MainMemoryAtomicMemoryUnit),
                "parameterless construction");
        }
    }
}
