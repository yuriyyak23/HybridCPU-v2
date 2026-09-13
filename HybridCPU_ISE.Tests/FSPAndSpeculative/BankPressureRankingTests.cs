using Xunit;
using Xunit.Abstractions;
using YAKSys_Hybrid_CPU.Core;
using HybridCPU_ISE.Tests.TestHelpers;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Phase 2B: Bank-Pressure-Aware FSP tests.
    /// Validates that bank-pressure scoring reorders candidates
    /// to prefer less-congested memory banks.
    /// </summary>
    public class BankPressureRankingTests
    {
        private readonly ITestOutputHelper _output;

        public BankPressureRankingTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void WhenNoBankPressureThenNoAvoidanceEvents()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.CreditFairnessEnabled = false;
            scheduler.BankPressureTieBreakEnabled = true;
            scheduler.SpeculationBudgetEnabled = false;

            // Both candidates target non-congested banks
            var opVt1 = MicroOpTestHelper.CreateScalarALU(1, 20, 48, 49);
            var opVt2 = MicroOpTestHelper.CreateScalarALU(2, 24, 50, 51);

            scheduler.NominateSmtCandidate(1, opVt1);
            scheduler.NominateSmtCandidate(2, opVt2);

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 0, 1, 2);

            scheduler.PackBundleIntraCoreSmt(bundle, 0, 0);

            // Non-memory ops have zero pressure, no reordering should happen
            // (avoidance count should be 0 since equal pressure doesn't trigger swap)
            _output.WriteLine($"Avoidance: {scheduler.BankPressureAvoidanceCount}");
            Assert.Equal(0, scheduler.BankPressureAvoidanceCount);
        }

        [Fact]
        public void WhenMultipleCyclesWithPressureThenAvoidanceCountAccumulates()
        {
            ProcessorMemoryScope.WithProcessorMemory(
                ProcessorMemoryScope.CreateMemorySubsystem(numBanks: 16, bankWidthBytes: 4096),
                () =>
            {
                var scheduler = new MicroOpScheduler();
                scheduler.CreditFairnessEnabled = false;
                scheduler.BankPressureTieBreakEnabled = true;
                scheduler.SpeculationBudgetEnabled = false;

                int cycles = 50;

                for (int c = 0; c < cycles; c++)
                {
                    // Re-create pressure each cycle (persistent scoreboard entries)
                    if (c == 0)
                    {
                        scheduler.SetSmtScoreboardPendingTyped(100, 0, 0, ScoreboardEntryType.OutstandingLoad, 3);
                    }

                    var opVt1 = MicroOpTestHelper.CreateLoadStore(1, 0x3000, 20, isLoad: true, memoryBankId: 3);
                    var opVt2 = MicroOpTestHelper.CreateScalarALU(2, 24, 50, 51);

                    scheduler.NominateSmtCandidate(1, opVt1);
                    scheduler.NominateSmtCandidate(2, opVt2);

                    var bundle = new MicroOp[8];
                    for (int i = 0; i < 6; i++)
                        bundle[i] = MicroOpTestHelper.CreateScalarALU(0, (ushort)(i * 4), (ushort)(i * 4 + 1), (ushort)(i * 4 + 2));

                    scheduler.PackBundleIntraCoreSmt(bundle, 0, 0);
                }

                _output.WriteLine($"Avoidance count over {cycles} cycles: {scheduler.BankPressureAvoidanceCount}");
                // Some reordering should have happened when VT2 (non-memory, pressure=0) was preferred over VT1 (bank 3, pressure>0)
                Assert.True(scheduler.BankPressureAvoidanceCount > 0, "Pressure avoidance should accumulate over cycles");
            });
        }
    }
}
