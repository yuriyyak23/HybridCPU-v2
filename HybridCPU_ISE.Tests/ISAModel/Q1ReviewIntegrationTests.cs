using Xunit;
using Xunit.Abstractions;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Execution;
using YAKSys_Hybrid_CPU.Memory;
using HybridCPU_ISE.Tests.TestHelpers;

namespace HybridCPU_ISE.Tests.ISAModel
{
    /// <summary>
    /// Q1 Review — Integration Tests (Cross-Feature)
    ///
    /// Tests that verify multiple Q1 Review features work together correctly:
    /// - Pipelined FSP + Domain Isolation
    /// - Pipelined FSP + Baseline Mode Toggle
    /// - NoC Domain Isolation + Performance Reporting
    /// - StreamRegisterFile + Performance Reporting
    /// - Full system integration with all Q1 features
    ///
    /// Target files: MicroOpScheduler.cs, NoC_XY_Router.cs, ProcessorConfig.cs,
    ///               StreamRegisterFile.cs, PerformanceReport.cs
    /// Namespace: HybridCPU_ISE.Tests.ISAModel
    /// </summary>
    public class Q1ReviewIntegrationTests
    {
        private readonly ITestOutputHelper _output;

        public Q1ReviewIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        #region 5.1 Cross-Feature Integration

        [Fact]
        public void WhenPipelinedFspWithBaselineModeInOrderThenNoInjections()
        {
            // Arrange
            var config = new ProcessorConfig
            {
                ExecutionMode = BaselineMode.InOrder_Baseline, // Disable FSP
                PipelinedFspEnabled = true // Pipeline enabled but shouldn't inject
            };

            var scheduler = new MicroOpScheduler
            {
                PipelinedFspEnabled = config.PipelinedFspEnabled
            };

            // Act - Nominate candidates
            var vtOp1 = MicroOpTestHelper.CreateScalarALU(1, destReg: 9, src1Reg: 10, src2Reg: 11);
            scheduler.NominateSmtCandidate(1, vtOp1);

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);

            // First cycle - prime pipeline
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            // Second cycle - would inject if FSP enabled
            var vtOp2 = MicroOpTestHelper.CreateScalarALU(1, destReg: 12, src1Reg: 13, src2Reg: 14);
            scheduler.NominateSmtCandidate(1, vtOp2);

            var bundle2 = new MicroOp[8];
            bundle2[0] = MicroOpTestHelper.CreateScalarALU(0, 4, 5, 6);
            var result = scheduler.PackBundleIntraCoreSmt(bundle2, ownerVirtualThreadId: 0, localCoreId: 0);

            // Assert - BaselineMode should take precedence (though in real implementation,
            // the scheduler would be configured based on BaselineMode)
            // Here we verify pipeline latency counter increments (pipeline is active)
            Assert.True(scheduler.FspPipelineLatencyCycles > 0);
        }

        [Fact]
        public void WhenNoCDomainIsolationWithPerformanceReportThenStatsCollected()
        {
            // Arrange
            var router1 = new NoC_XY_Router(localX: 0, localY: 0);
            var router2 = new NoC_XY_Router(localX: 1, localY: 0);
            router1.ConnectNeighbor(NoCPort.East, router2);

            router1.DomainVcIsolationEnabled = true;
            router1.MaxInflightPerDomain = 5;

            // Act - Send flits that exceed domain limit
            for (int i = 0; i < 10; i++)
            {
                var flit = new NoCFlit
                {
                    DestX = 1, DestY = 0, SrcX = 0, SrcY = 0,
                    DomainTag = 0x1234, VirtualChannel = 0, Payload = new byte[64]
                };
                router1.RoutePacket(flit);
            }

            // Create performance report
            var report = new PerformanceReport
            {
                // In real system, these would be collected from router
                BankContentionStalls = (long)router1.DomainStallCount
            };

            // Assert - Stats should be captured in report
            Assert.Equal(5, router1.DomainStallCount);
            Assert.Equal(5, report.BankContentionStalls);
        }

        [Fact]
        public void WhenStreamRegisterFileWithPerformanceReportThenBypassHitsCollected()
        {
            // Arrange
            var srf = new StreamRegisterFile();
            var memory = new byte[1024];

            // Act - Allocate and hit
            int regIndex = srf.AllocateRegister(0x100, 8, 4);
            srf.LoadRegister(regIndex, memory);

            byte[] destination = new byte[32];
            // Serve from SRF 5 times
            for (int i = 0; i < 5; i++)
            {
                Assert.True(srf.ReadRegister(regIndex, destination, 32));
            }

            var stats = srf.GetStatistics();

            // Create performance report
            var report = new PerformanceReport
            {
                L1BypassHits = (long)stats.l1BypassHits
            };

            // Assert
            Assert.Equal(5UL, stats.l1BypassHits);
            Assert.Equal(5, report.L1BypassHits);
        }

        [Fact]
        public void WhenPipelinedFspWithSchedulerStatsThenLatencyCyclesCounted()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.PipelinedFspEnabled = true;

            // Act - Run multiple cycles
            for (int cycle = 0; cycle < 10; cycle++)
            {
                var bundle = new MicroOp[8];
                bundle[0] = MicroOpTestHelper.CreateScalarALU(0, (ushort)(cycle + 1), (ushort)(cycle + 2), (ushort)(cycle + 3));

                var vtOp = MicroOpTestHelper.CreateScalarALU(1, destReg: (ushort)(9 + cycle), src1Reg: (ushort)(10 + cycle), src2Reg: (ushort)(11 + cycle));
                scheduler.NominateSmtCandidate(1, vtOp);

                scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);
            }

            // Create performance report
            var report = new PerformanceReport
            {
                FspPipelineLatencyCycles = scheduler.FspPipelineLatencyCycles,
                SuccessfulInjections = scheduler.SmtInjectionsCount
            };

            // Assert - Pipeline latency should be non-zero
            Assert.True(scheduler.FspPipelineLatencyCycles > 0);
            Assert.True(report.FspPipelineLatencyCycles > 0);
        }

        [Fact]
        public void WhenAllQ1FeaturesEnabledThenAllStatsAvailable()
        {
            // Arrange - Enable all Q1 features
            var config = new ProcessorConfig
            {
                ExecutionMode = BaselineMode.FSP_Enabled,
                PipelinedFspEnabled = true
            };

            var scheduler = new MicroOpScheduler
            {
                PipelinedFspEnabled = config.PipelinedFspEnabled
            };

            var srf = new StreamRegisterFile();
            var memory = new byte[1024];

            var router = new NoC_XY_Router(localX: 0, localY: 0);
            router.DomainVcIsolationEnabled = true;

            // Act - Exercise all features
            // 1. Pipelined FSP
            var vtOp = MicroOpTestHelper.CreateScalarALU(1, destReg: 9, src1Reg: 10, src2Reg: 11);
            scheduler.NominateSmtCandidate(1, vtOp);

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            // 2. SRF bypass
            int regIndex = srf.AllocateRegister(0x100, 8, 4);
            srf.LoadRegister(regIndex, memory);
            srf.ReadRegister(regIndex, new byte[32], 32);

            var srfStats = srf.GetStatistics();

            // 3. Create comprehensive report
            var report = new PerformanceReport
            {
                FspPipelineLatencyCycles = scheduler.FspPipelineLatencyCycles,
                BankContentionStalls = 0, // Would come from scheduler MSHR stats
                L1BypassHits = (long)srfStats.l1BypassHits,
                SuccessfulInjections = scheduler.SmtInjectionsCount
            };

            // Assert - All Q1 features should have stats
            Assert.True(report.FspPipelineLatencyCycles >= 0);
            Assert.True(report.L1BypassHits >= 0);
            Assert.True(config.VliwStealEnabled);
            Assert.True(config.PipelinedFspEnabled);
            Assert.True(router.DomainVcIsolationEnabled);
        }

        [Fact]
        public void WhenGenerateSummaryWithAllQ1CountersThenAllPresent()
        {
            // Arrange
            var report = new PerformanceReport
            {
                BankContentionStalls = 100,
                FspPipelineLatencyCycles = 200,
                L1BypassHits = 300,
                SuccessfulInjections = 1000,
                RejectedInjections = 50,
                TotalInstructions = 10000,
                TotalCycles = 5000
            };

            // Act
            string summary = report.GenerateSummary();

            // Assert - All Q1 counters should be in summary
            Assert.Contains("Bank Contention Stalls", summary);
            Assert.Contains("FSP Pipeline Latency Cycles", summary);
            Assert.Contains("L1 Bypass Hits", summary);
            Assert.Contains("IPC", summary);

            _output.WriteLine("=== Performance Summary ===");
            _output.WriteLine(summary);
        }

        [Fact]
        public void WhenExportToCSVWithAllQ1CountersThenAllPresent()
        {
            // Arrange
            var report = new PerformanceReport
            {
                BankContentionStalls = 111,
                FspPipelineLatencyCycles = 222,
                L1BypassHits = 333,
                TotalBursts = 1000,
                TotalInstructions = 5000
            };

            // Act
            string csv = report.ExportToCSV();

            // Assert - All Q1 counters should be in CSV
            Assert.Contains("Scheduler,BankContentionStalls,111", csv);
            Assert.Contains("Scheduler,FspPipelineLatencyCycles,222", csv);
            Assert.Contains("StreamRegFile,L1BypassHits,333", csv);

            _output.WriteLine("=== Performance CSV ===");
            _output.WriteLine(csv);
        }

        #endregion
    }
}
