using Xunit;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.EvaluationAndMetrics
{
    public class StreamRegisterFileBypassTests
    {
        [Fact]
        public void WhenSrfCreatedThenL1BypassHitsIsZero()
        {
            var srf = new StreamRegisterFile();
            var stats = srf.GetStatistics();

            Assert.Equal(0UL, stats.l1BypassHits);
        }

        [Fact]
        public void WhenSrfCreatedThenIngressWarmTelemetryStartsAtZero()
        {
            var srf = new StreamRegisterFile();
            var telemetry = srf.GetIngressWarmTelemetry();

            Assert.Equal(0UL, telemetry.ForegroundWarmAttempts);
            Assert.Equal(0UL, telemetry.ForegroundWarmSuccesses);
            Assert.Equal(0UL, telemetry.ForegroundBypassHits);
            Assert.Equal(0UL, telemetry.AssistWarmAttempts);
            Assert.Equal(0UL, telemetry.AssistWarmSuccesses);
            Assert.Equal(0UL, telemetry.AssistBypassHits);
            Assert.Equal(0UL, telemetry.TranslationRejects);
            Assert.Equal(0UL, telemetry.BackendRejects);
        }

        [Fact]
        public void WhenAllocateHitsValidRegisterThenBypassHitsStayZeroUntilRead()
        {
            var srf = new StreamRegisterFile();
            var memory = new byte[1024];

            int regIndex = srf.AllocateRegister(sourceAddr: 0x100, elementSize: 8, elementCount: 4);
            Assert.NotEqual(-1, regIndex);
            Assert.True(srf.LoadRegister(regIndex, memory));

            int regIndex2 = srf.AllocateRegister(sourceAddr: 0x100, elementSize: 8, elementCount: 4);
            var stats = srf.GetStatistics();

            Assert.Equal(regIndex, regIndex2);
            Assert.Equal(1UL, stats.hits);
            Assert.Equal(1UL, stats.misses);
            Assert.Equal(0UL, stats.l1BypassHits);
        }

        [Fact]
        public void WhenReadRegisterServesValidDataThenL1BypassHitsIncrements()
        {
            var srf = new StreamRegisterFile();
            var memory = new byte[1024];

            int regIndex = srf.AllocateRegister(sourceAddr: 0x100, elementSize: 8, elementCount: 4);
            Assert.True(srf.LoadRegister(regIndex, memory));

            byte[] destination = new byte[32];
            Assert.True(srf.ReadRegister(regIndex, destination, 32));

            var stats = srf.GetStatistics();
            Assert.Equal(1UL, stats.l1BypassHits);
        }

        [Fact]
        public void WhenForegroundOwnedReadHitsValidRegisterThenBypassTelemetryAttributesForegroundConsume()
        {
            var srf = new StreamRegisterFile();
            var memory = new byte[1024];

            int regIndex = srf.AllocateRegister(sourceAddr: 0x180, elementSize: 8, elementCount: 4);
            Assert.True(srf.LoadRegister(regIndex, memory));

            byte[] destination = new byte[32];
            Assert.True(srf.ReadRegister(regIndex, destination, 32));

            var telemetry = srf.GetIngressWarmTelemetry();
            Assert.Equal(1UL, telemetry.ForegroundBypassHits);
            Assert.Equal(0UL, telemetry.AssistBypassHits);
        }

        [Fact]
        public void WhenAssistOwnedReadHitsValidRegisterThenBypassTelemetryAttributesAssistConsume()
        {
            var srf = new StreamRegisterFile();
            var memory = new byte[1024];

            Assert.True(srf.TryAllocateAssistRegister(
                sourceAddr: 0x280,
                elementSize: 8,
                elementCount: 4,
                AssistStreamRegisterPartitionPolicy.Default,
                out int regIndex,
                out AssistStreamRegisterRejectKind rejectKind));
            Assert.Equal(AssistStreamRegisterRejectKind.None, rejectKind);
            Assert.True(srf.LoadRegister(regIndex, memory));

            byte[] destination = new byte[32];
            Assert.True(srf.ReadRegister(regIndex, destination, 32));

            var telemetry = srf.GetIngressWarmTelemetry();
            Assert.Equal(0UL, telemetry.ForegroundBypassHits);
            Assert.Equal(1UL, telemetry.AssistBypassHits);
        }

        [Fact]
        public void WhenTryReadPrefetchedChunkHitsThenL1BypassHitsIncrements()
        {
            var srf = new StreamRegisterFile();
            var memory = new byte[1024];

            int regIndex = srf.AllocateRegister(sourceAddr: 0x100, elementSize: 4, elementCount: 4);
            Assert.True(srf.LoadRegister(regIndex, memory));

            byte[] destination = new byte[16];
            Assert.True(srf.TryReadPrefetchedChunk(0x100, 4, 4, destination));

            var stats = srf.GetStatistics();
            Assert.Equal(1UL, stats.l1BypassHits);
        }

        [Fact]
        public void WhenMultipleReadsThenL1BypassHitsAccumulates()
        {
            var srf = new StreamRegisterFile();
            var memory = new byte[1024];

            int regIndex = srf.AllocateRegister(sourceAddr: 0x100, elementSize: 8, elementCount: 4);
            Assert.True(srf.LoadRegister(regIndex, memory));

            byte[] destination = new byte[32];
            for (int i = 0; i < 5; i++)
            {
                Assert.True(srf.ReadRegister(regIndex, destination, 32));
            }

            var stats = srf.GetStatistics();
            Assert.Equal(5UL, stats.l1BypassHits);
        }

        [Fact]
        public void WhenReadOnLoadingStateThenNotCountedAsBypass()
        {
            var srf = new StreamRegisterFile();

            int regIndex = srf.AllocateRegister(sourceAddr: 0x100, elementSize: 8, elementCount: 4);
            srf.MarkLoading(regIndex);

            byte[] destination = new byte[32];
            Assert.False(srf.ReadRegister(regIndex, destination, 32));

            var stats = srf.GetStatistics();
            Assert.Equal(0UL, stats.l1BypassHits);
        }

        [Fact]
        public void WhenReadWithDifferentElementSizeThenNotCountedAsBypass()
        {
            var srf = new StreamRegisterFile();
            var memory = new byte[1024];

            int regIndex = srf.AllocateRegister(sourceAddr: 0x100, elementSize: 8, elementCount: 4);
            Assert.True(srf.LoadRegister(regIndex, memory));

            byte[] destination = new byte[32];
            Assert.False(srf.TryReadPrefetchedChunk(0x100, 4, 8, destination));

            var stats = srf.GetStatistics();
            Assert.Equal(0UL, stats.l1BypassHits);
        }

        [Fact]
        public void WhenGetStatisticsThenTupleHasFiveElements()
        {
            var srf = new StreamRegisterFile();
            var stats = srf.GetStatistics();

            Assert.IsType<(ulong, ulong, ulong, double, ulong)>(stats);
            Assert.Equal(0UL, stats.hits);
            Assert.Equal(0UL, stats.misses);
            Assert.Equal(0UL, stats.evictions);
            Assert.Equal(0.0, stats.hitRate);
            Assert.Equal(0UL, stats.l1BypassHits);
        }

        [Fact]
        public void WhenNoAccessesThenHitRateIsZero()
        {
            var srf = new StreamRegisterFile();
            var stats = srf.GetStatistics();

            Assert.Equal(0.0, stats.hitRate);
        }

        [Fact]
        public void WhenAllHitsThenHitRateIsOne()
        {
            var srf = new StreamRegisterFile();
            var memory = new byte[1024];

            int regIndex = srf.AllocateRegister(sourceAddr: 0x100, elementSize: 8, elementCount: 4);
            Assert.True(srf.LoadRegister(regIndex, memory));

            for (int i = 0; i < 10; i++)
            {
                srf.AllocateRegister(sourceAddr: 0x100, elementSize: 8, elementCount: 4);
            }

            var stats = srf.GetStatistics();
            Assert.Equal(10UL, stats.hits);
            Assert.Equal(1UL, stats.misses);
            Assert.True(stats.hitRate > 0.9 && stats.hitRate < 1.0);
        }

        [Fact]
        public void WhenMixedThenHitRateIsCorrectRatio()
        {
            var srf = new StreamRegisterFile();
            var memory = new byte[1024];

            int reg1 = srf.AllocateRegister(sourceAddr: 0x100, elementSize: 8, elementCount: 4);
            Assert.True(srf.LoadRegister(reg1, memory));
            int reg2 = srf.AllocateRegister(sourceAddr: 0x200, elementSize: 8, elementCount: 4);
            Assert.True(srf.LoadRegister(reg2, memory));
            int reg3 = srf.AllocateRegister(sourceAddr: 0x300, elementSize: 8, elementCount: 4);
            Assert.True(srf.LoadRegister(reg3, memory));

            for (int i = 0; i < 7; i++)
            {
                srf.AllocateRegister(sourceAddr: 0x100, elementSize: 8, elementCount: 4);
            }

            var stats = srf.GetStatistics();
            Assert.Equal(7UL, stats.hits);
            Assert.Equal(3UL, stats.misses);
            Assert.Equal(0.7, stats.hitRate, precision: 5);
        }
    }
}
