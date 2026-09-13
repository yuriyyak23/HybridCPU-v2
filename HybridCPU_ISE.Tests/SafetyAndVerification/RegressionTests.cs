using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Execution;
using YAKSys_Hybrid_CPU.Memory;
using HybridCPU_ISE.Tests.TestHelpers;

namespace HybridCPU_ISE.Tests.SafetyAndVerification
{
    /// <summary>
    /// Q1 Review — Regression Tests (Non-Interference)
    ///
    /// Tests that verify features don't break existing functionality:
    /// - Pipelined FSP doesn't break single-cycle path
    /// - Domain isolation doesn't break non-isolated routing
    /// - Baseline mode toggle doesn't break FSP mode
    /// - New counters don't affect existing behavior
    /// - Backwards compatibility with existing configurations
    ///
    /// Target files: MicroOpScheduler.cs, NoC_XY_Router.cs, ProcessorConfig.cs,
    ///               StreamRegisterFile.cs, PerformanceReport.cs
    /// Namespace: HybridCPU_ISE.Tests.SafetyAndVerification
    /// </summary>
    public class RegressionTests
    {
        #region 6.1 Pipelined FSP — Non-Interference with Single-Cycle

        [Fact]
        public void WhenPipelinedFspDisabledThenSingleCyclePathWorks()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.PipelinedFspEnabled = false;

            // Act - Should use single-cycle path
            var vtOp = MicroOpTestHelper.CreateScalarALU(1, destReg: 9, src1Reg: 10, src2Reg: 11);
            scheduler.NominateSmtCandidate(1, vtOp);

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);

            var result = scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            // Assert - Should inject immediately (single-cycle)
            Assert.Equal(1, (int)scheduler.SmtInjectionsCount);
            Assert.Equal(0, scheduler.FspPipelineLatencyCycles); // No pipeline latency
        }

        [Fact]
        public void WhenPipelinedFspToggledThenBehaviorSwitches()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();

            // Act - Start with single-cycle
            scheduler.PipelinedFspEnabled = false;

            var vtOp1 = MicroOpTestHelper.CreateScalarALU(1, destReg: 9, src1Reg: 10, src2Reg: 11);
            scheduler.NominateSmtCandidate(1, vtOp1);

            var bundle1 = new MicroOp[8];
            bundle1[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);
            scheduler.PackBundleIntraCoreSmt(bundle1, ownerVirtualThreadId: 0, localCoreId: 0);

            long injectionsAfterSingleCycle = scheduler.SmtInjectionsCount;

            // Act - Switch to pipelined
            scheduler.PipelinedFspEnabled = true;

            var vtOp2 = MicroOpTestHelper.CreateScalarALU(1, destReg: 12, src1Reg: 13, src2Reg: 14);
            scheduler.NominateSmtCandidate(1, vtOp2);

            var bundle2 = new MicroOp[8];
            bundle2[0] = MicroOpTestHelper.CreateScalarALU(0, 4, 5, 6);
            scheduler.PackBundleIntraCoreSmt(bundle2, ownerVirtualThreadId: 0, localCoreId: 0);

            // Assert - Single-cycle injection happened, pipelined mode now active
            Assert.True(injectionsAfterSingleCycle > 0);
            Assert.True(scheduler.FspPipelineLatencyCycles > 0); // Pipeline is now active
        }

        [Fact]
        public void WhenPipelinedFspDisabledThenNoLatencyCyclesCounted()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.PipelinedFspEnabled = false;

            // Act - Run multiple cycles
            for (int i = 0; i < 10; i++)
            {
                var vtOp = MicroOpTestHelper.CreateScalarALU(1, destReg: (ushort)(9 + i), src1Reg: (ushort)(10 + i), src2Reg: (ushort)(11 + i));
                scheduler.NominateSmtCandidate(1, vtOp);

                var bundle = new MicroOp[8];
                bundle[0] = MicroOpTestHelper.CreateScalarALU(0, (ushort)(i + 1), (ushort)(i + 2), (ushort)(i + 3));
                scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);
            }

            // Assert - No pipeline latency
            Assert.Equal(0, scheduler.FspPipelineLatencyCycles);
        }

        [Fact]
        public void WhenNullBundleWithPipelinedFspThenNoError()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.PipelinedFspEnabled = true;

            // Act
            var result = scheduler.PackBundleIntraCoreSmt(null!, ownerVirtualThreadId: 0, localCoreId: 0);

            // Assert - Should handle null gracefully
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        #endregion

        #region 6.2 NoC Domain Isolation — Non-Interference with Default Routing

        [Fact]
        public void WhenDomainIsolationDisabledThenNormalRoutingWorks()
        {
            // Arrange
            var router1 = new NoC_XY_Router(localX: 0, localY: 0);
            var router2 = new NoC_XY_Router(localX: 1, localY: 0);
            router1.ConnectNeighbor(NoCPort.East, router2);

            router1.DomainVcIsolationEnabled = false;

            // Act - Send flits
            for (int i = 0; i < 10; i++)
            {
                var flit = new NoCFlit
                {
                    DestX = 1,
                    DestY = 0,
                    SrcX = 0,
                    SrcY = 0,
                    DomainTag = 0x1234,
                    VirtualChannel = 0,
                    Payload = new byte[64]
                };
                router1.RoutePacket(flit);
            }

            // Assert - All flits should be routed
            Assert.Equal(10, router1.FlitsRouted);
            Assert.Equal(0, router1.DomainStallCount);
        }

        [Fact]
        public void WhenMaxInflightPerDomainZeroThenNoLimitEnforced()
        {
            // Arrange
            var router1 = new NoC_XY_Router(localX: 0, localY: 0);
            var router2 = new NoC_XY_Router(localX: 1, localY: 0);
            router1.ConnectNeighbor(NoCPort.East, router2);

            router1.DomainVcIsolationEnabled = true;
            router1.MaxInflightPerDomain = 0; // Zero disables limit

            // Act - Send many flits
            for (int i = 0; i < 50; i++)
            {
                var flit = new NoCFlit
                {
                    DestX = 1,
                    DestY = 0,
                    SrcX = 0,
                    SrcY = 0,
                    DomainTag = 0x1234,
                    VirtualChannel = 0,
                    Payload = new byte[64]
                };
                router1.RoutePacket(flit);
            }

            // Assert - All should be routed (no limit)
            Assert.Equal(50, router1.FlitsRouted);
            Assert.Equal(0, router1.DomainStallCount);
        }

        [Fact]
        public void WhenDomainTagZeroThenExemptFromIsolation()
        {
            // Arrange
            var router1 = new NoC_XY_Router(localX: 0, localY: 0);
            var router2 = new NoC_XY_Router(localX: 1, localY: 0);
            router1.ConnectNeighbor(NoCPort.East, router2);

            router1.DomainVcIsolationEnabled = true;
            router1.MaxInflightPerDomain = 5;

            // Act - Send many flits with DomainTag = 0
            for (int i = 0; i < 50; i++)
            {
                var flit = new NoCFlit
                {
                    DestX = 1,
                    DestY = 0,
                    SrcX = 0,
                    SrcY = 0,
                    DomainTag = 0,
                    VirtualChannel = 0,
                    Payload = new byte[64]
                };
                router1.RoutePacket(flit);
            }

            // Assert - All should be routed (DomainTag 0 is exempt)
            Assert.Equal(50, router1.FlitsRouted);
            Assert.Equal(0, router1.DomainStallCount);
        }

        [Fact]
        public void WhenReleaseDomainInflightWithIsolationDisabledThenNoError()
        {
            // Arrange
            var router = new NoC_XY_Router(localX: 0, localY: 0);
            router.DomainVcIsolationEnabled = false;

            // Act - Should be safe no-op
            router.ReleaseDomainInflight(0x1234);

            // Assert - No crash
            Assert.False(router.DomainVcIsolationEnabled);
        }

        #endregion

        #region 6.3 Baseline Mode Toggle — Non-Interference

        [Fact]
        public void WhenFspEnabledThenVliwStealEnabledIsTrue()
        {
            // Arrange
            var config = new ProcessorConfig();

            // Act
            config.ExecutionMode = BaselineMode.FSP_Enabled;

            // Assert
            Assert.True(config.VliwStealEnabled);
            Assert.Equal(BaselineMode.FSP_Enabled, config.ExecutionMode);
        }

        [Fact]
        public void WhenInOrderBaselineThenVliwStealEnabledIsFalse()
        {
            // Arrange
            var config = new ProcessorConfig();

            // Act
            config.ExecutionMode = BaselineMode.InOrder_Baseline;

            // Assert
            Assert.False(config.VliwStealEnabled);
            Assert.Equal(BaselineMode.InOrder_Baseline, config.ExecutionMode);
        }

        [Fact]
        public void WhenVliwStealToggledThenExecutionModeFollows()
        {
            // Arrange
            var config = new ProcessorConfig();

            // Act & Assert - Toggle VliwStealEnabled
            config.VliwStealEnabled = false;
            Assert.Equal(BaselineMode.InOrder_Baseline, config.ExecutionMode);

            config.VliwStealEnabled = true;
            Assert.Equal(BaselineMode.FSP_Enabled, config.ExecutionMode);
        }

        [Fact]
        public void WhenBaselineModeSetThenPipelinedFspUnaffected()
        {
            // Arrange
            var config = new ProcessorConfig();
            config.PipelinedFspEnabled = true;

            // Act - Change baseline mode
            config.ExecutionMode = BaselineMode.InOrder_Baseline;

            // Assert - PipelinedFspEnabled should remain unchanged
            Assert.True(config.PipelinedFspEnabled);
            Assert.Equal(BaselineMode.InOrder_Baseline, config.ExecutionMode);
        }

        #endregion

        #region 6.4 StreamRegisterFile — Non-Interference

        [Fact]
        public void WhenNoBypassHitsThenCounterStaysZero()
        {
            // Arrange
            var srf = new StreamRegisterFile();

            // Act - Only allocate (no hits)
            srf.AllocateRegister(0x100, 8, 4);
            srf.AllocateRegister(0x200, 8, 4);
            srf.AllocateRegister(0x300, 8, 4);

            var stats = srf.GetStatistics();

            // Assert - No bypass hits
            Assert.Equal(0UL, stats.l1BypassHits);
            Assert.Equal(3UL, stats.misses);
        }

        [Fact]
        public void WhenGetStatisticsThenOldCountersUnaffected()
        {
            // Arrange
            var srf = new StreamRegisterFile();
            var memory = new byte[1024];

            // Act - Generate hits and misses
            int reg1 = srf.AllocateRegister(0x100, 8, 4);
            srf.LoadRegister(reg1, memory);
            srf.AllocateRegister(0x100, 8, 4); // Hit
            srf.ReadRegister(reg1, new byte[32], 32);

            int reg2 = srf.AllocateRegister(0x200, 8, 4); // Miss

            var stats = srf.GetStatistics();

            // Assert - Old counters still work
            Assert.Equal(1UL, stats.hits);
            Assert.Equal(2UL, stats.misses);
            Assert.Equal(1.0 / 3.0, stats.hitRate, 5); // 1 hit, 2 misses -> 1/3 hit rate
            // New counter also present
            Assert.Equal(1UL, stats.l1BypassHits);
        }

        #endregion

        #region 6.5 PerformanceReport — Non-Interference

        [Fact]
        public void WhenNewCountersZeroThenSummaryStillValid()
        {
            // Arrange
            var report = new PerformanceReport
            {
                TotalInstructions = 1000,
                TotalCycles = 500,
                BankContentionStalls = 0,
                FspPipelineLatencyCycles = 0,
                L1BypassHits = 0
            };

            // Act
            string summary = report.GenerateSummary();

            // Assert - Summary should still be generated
            Assert.NotEmpty(summary);
            Assert.Contains("IPC", summary);
            Assert.DoesNotContain("Stream Register File", summary); // Section omitted when L1BypassHits = 0
        }

        [Fact]
        public void WhenNewCountersNonZeroThenOldCountersUnaffected()
        {
            // Arrange
            var report = new PerformanceReport
            {
                TotalInstructions = 1000,
                TotalCycles = 500,
                BankContentionStalls = 100,
                FspPipelineLatencyCycles = 200,
                L1BypassHits = 300
            };

            // Act
            double ipc = report.IPC;

            // Assert - IPC calculation still works
            Assert.Equal(2.0, ipc);
        }

        [Fact]
        public void WhenExportToCSVThenAllCountersPresent()
        {
            // Arrange
            var report = new PerformanceReport
            {
                TotalInstructions = 1000,
                TotalBursts = 50,
                BankContentionStalls = 10,
                FspPipelineLatencyCycles = 20,
                L1BypassHits = 30
            };

            // Act
            string csv = report.ExportToCSV();

            // Assert - Both old and new counters in CSV
            Assert.Contains("Pipeline,TotalInstructions,1000", csv);
            Assert.Contains("Memory,TotalBursts,50", csv);
            Assert.Contains("Scheduler,BankContentionStalls,10", csv);
            Assert.Contains("Scheduler,FspPipelineLatencyCycles,20", csv);
            Assert.Contains("StreamRegFile,L1BypassHits,30", csv);
        }

        #endregion

        #region 6.6 Factory Methods — Backwards Compatibility

        [Fact]
        public void WhenDefaultFactoryThenNewFieldsHaveDefaults()
        {
            // Arrange & Act
            var config = ProcessorConfig.Default();

            // Assert - New fields have proper defaults
            Assert.Equal(BaselineMode.FSP_Enabled, config.ExecutionMode);
            Assert.True(config.VliwStealEnabled);
            Assert.False(config.PipelinedFspEnabled);
            // Old fields still present
            Assert.Equal(8, config.NumMemoryBanks);
        }

        [Fact]
        public void WhenHighPerformanceFPGAFactoryThenNewFieldsHaveDefaults()
        {
            // Arrange & Act
            var config = ProcessorConfig.HighPerformanceFPGA();

            // Assert - New fields have proper defaults
            Assert.Equal(BaselineMode.FSP_Enabled, config.ExecutionMode);
            Assert.False(config.PipelinedFspEnabled);
            // Old fields customized
            Assert.Equal(16, config.NumMemoryBanks);
        }

        [Fact]
        public void WhenTestingFactoryThenNewFieldsHaveDefaults()
        {
            // Arrange & Act
            var config = ProcessorConfig.Testing();

            // Assert - New fields have proper defaults
            Assert.Equal(BaselineMode.FSP_Enabled, config.ExecutionMode);
            Assert.False(config.PipelinedFspEnabled);
            // Old fields customized
            Assert.Equal(1, config.NumMemoryBanks);
        }

        #endregion
    }
}
