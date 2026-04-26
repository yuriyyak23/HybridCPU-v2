using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace HybridCPU_ISE.Tests.Phase09;

[CollectionDefinition("Phase09 Memory Bank Routing Telemetry", DisableParallelization = true)]
public sealed class Phase09MemoryBankRoutingTelemetryCollection;

[Collection("Phase09 Memory Bank Routing Telemetry")]
public sealed class Phase09MemoryBankRoutingFallbackTelemetryTests
{
    [Fact]
    public void ResolveSchedulerVisibleBankId_WhenProcessorMemoryIsUnavailable_ReturnsExplicitUninitializedBankAndIncrementsTelemetry()
    {
        MemoryBankRouting.ResetTelemetryForTesting();

        try
        {
            int bankId = ProcessorMemoryScope.WithProcessorMemory(
                memory: null,
                action: () => MemoryBankRouting.ResolveSchedulerVisibleBankId(0x2000UL));

            Assert.Equal(MemoryBankRouting.UninitializedSchedulerVisibleBankId, bankId);
            Assert.Equal(1UL, MemoryBankRouting.SchedulerVisibleUninitializedUseCount);
        }
        finally
        {
            MemoryBankRouting.ResetTelemetryForTesting();
        }
    }

    [Fact]
    public void ResolveSchedulerVisibleBankId_WhenRuntimeGeometryIsAvailable_UsesRuntimeGeometryWithoutTelemetry()
    {
        MemoryBankRouting.ResetTelemetryForTesting();

        try
        {
            var memory = ProcessorMemoryScope.CreateMemorySubsystem(numBanks: 8, bankWidthBytes: 128);

            int bankId = ProcessorMemoryScope.WithProcessorMemory(
                memory,
                () => MemoryBankRouting.ResolveSchedulerVisibleBankId(0x180UL));

            Assert.Equal(3, bankId);
            Assert.Equal(0UL, MemoryBankRouting.SchedulerVisibleUninitializedUseCount);
        }
        finally
        {
            MemoryBankRouting.ResetTelemetryForTesting();
        }
    }

    [Fact]
    public void ResolveSchedulerVisibleBankId_WhenRuntimeGeometryIsIncomplete_ReturnsExplicitUninitializedBankAndIncrementsTelemetry()
    {
        MemoryBankRouting.ResetTelemetryForTesting();

        try
        {
            var memory = ProcessorMemoryScope.CreateMemorySubsystem(numBanks: 8, bankWidthBytes: 0);

            int bankId = ProcessorMemoryScope.WithProcessorMemory(
                memory,
                () => MemoryBankRouting.ResolveSchedulerVisibleBankId(0x1000UL));

            Assert.Equal(MemoryBankRouting.UninitializedSchedulerVisibleBankId, bankId);
            Assert.Equal(1UL, MemoryBankRouting.SchedulerVisibleUninitializedUseCount);
        }
        finally
        {
            MemoryBankRouting.ResetTelemetryForTesting();
        }
    }
}
