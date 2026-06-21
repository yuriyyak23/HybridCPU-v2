using System;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.TestHelpers;

internal static class ProcessorMemoryScope
{
    internal static void WithProcessorMemory(MemorySubsystem? memory, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        MemorySubsystem? savedMemory = Processor.Memory;
        try
        {
            Processor.Memory = memory;
            action();
        }
        finally
        {
            Processor.Memory = savedMemory;
        }
    }

    internal static T WithProcessorMemory<T>(MemorySubsystem? memory, Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        MemorySubsystem? savedMemory = Processor.Memory;
        try
        {
            Processor.Memory = memory;
            return action();
        }
        finally
        {
            Processor.Memory = savedMemory;
        }
    }

    internal static MemorySubsystem CreateMemorySubsystem(int numBanks, int bankWidthBytes)
    {
        Processor proc = default;
        return new MemorySubsystem(ref proc)
        {
            NumBanks = numBanks,
            BankWidthBytes = bankWidthBytes
        };
    }
}
