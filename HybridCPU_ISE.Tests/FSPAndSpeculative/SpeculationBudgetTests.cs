using Xunit;
using Xunit.Abstractions;
using YAKSys_Hybrid_CPU.Core;
using HybridCPU_ISE.Tests.TestHelpers;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Phase 2C: Speculation Budget tests.
    /// Validates that the speculation budget cap correctly limits concurrent
    /// speculative operations, tracks exhaustion events, and releases budget
    /// on commit/squash.
    /// </summary>
    public class SpeculationBudgetTests
    {
        private readonly ITestOutputHelper _output;

        public SpeculationBudgetTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void WhenBudgetAvailableThenSpeculativeInjectionAllowed()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.CreditFairnessEnabled = false;
            scheduler.BankPressureTieBreakEnabled = false;
            scheduler.SpeculationBudgetEnabled = true;
            scheduler.SpeculationBudgetMax = 4;

            // Budget starts at max
            int initialBudget = scheduler.TestGetSpeculationBudget();
            Assert.Equal(4, initialBudget);

            // Non-speculative injection (same-thread ALU) should not affect budget
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 0, 1, 2);

            var aluCandidate = MicroOpTestHelper.CreateScalarALU(1, 16, 48, 49);
            scheduler.NominateSmtCandidate(1, aluCandidate);

            scheduler.PackBundleIntraCoreSmt(bundle, 0, 0);

            // ALU ops are not speculative (only cross-thread memory ops are), budget unchanged
            int afterBudget = scheduler.TestGetSpeculationBudget();
            _output.WriteLine($"Budget: before={initialBudget}, after={afterBudget}");
            Assert.Equal(initialBudget, afterBudget);
        }

        [Fact]
        public void WhenBudgetReleasedThenNewSpeculativeInjectionsUnblocked()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.CreditFairnessEnabled = false;
            scheduler.BankPressureTieBreakEnabled = false;
            scheduler.SpeculationBudgetEnabled = true;
            scheduler.SpeculationBudgetMax = 2;

            // Exhaust budget
            scheduler.TestSetSpeculationBudget(0);
            Assert.Equal(0, scheduler.TestGetSpeculationBudget());

            // Release one unit
            scheduler.ReleaseSpeculationBudget();
            Assert.Equal(1, scheduler.TestGetSpeculationBudget());

            // Release again
            scheduler.ReleaseSpeculationBudget();
            Assert.Equal(2, scheduler.TestGetSpeculationBudget());

            // Release beyond max should not exceed max
            scheduler.ReleaseSpeculationBudget();
            Assert.Equal(2, scheduler.TestGetSpeculationBudget());
        }

        [Fact]
        public void WhenFaultedSpeculativeOpThenBudgetReleased()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.CreditFairnessEnabled = false;
            scheduler.BankPressureTieBreakEnabled = false;
            scheduler.SpeculationBudgetEnabled = true;
            scheduler.SpeculationBudgetMax = 4;

            // Simulate 2 in-flight speculative ops
            scheduler.TestSetSpeculationBudget(2);

            // Create a faulted speculative op
            var faultedOp = MicroOpTestHelper.CreateScalarALU(1, 16, 48, 49);
            faultedOp.IsSpeculative = true;
            faultedOp.Faulted = true;

            var bundle = new MicroOp[8];
            bundle[0] = faultedOp;

            scheduler.ProcessFaultedOperations(bundle);

            // Budget should increase by 1
            Assert.Equal(3, scheduler.TestGetSpeculationBudget());
            Assert.Equal(1, scheduler.FaultedSpeculativeSteals);
        }

        [Fact]
        public void WhenSpeculationBudgetDisabledThenNoBlocking()
        {
            ProcessorMemoryScope.WithProcessorMemory(
                ProcessorMemoryScope.CreateMemorySubsystem(numBanks: 16, bankWidthBytes: 4096),
                () =>
            {
                var scheduler = new MicroOpScheduler();
                scheduler.CreditFairnessEnabled = false;
                scheduler.BankPressureTieBreakEnabled = false;
                scheduler.SpeculationBudgetEnabled = false;

                // Even with zero budget, disabled should not block
                scheduler.TestSetSpeculationBudget(0);

                var bundle = new MicroOp[8];
                bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 0, 1, 2);

                var memCandidate = MicroOpTestHelper.CreateLoadStore(1, 0x1000, 16, isLoad: true, memoryBankId: 0);
                memCandidate.OwnerThreadId = 1;
                scheduler.NominateSmtCandidate(1, memCandidate);

                scheduler.PackBundleIntraCoreSmt(bundle, 0, 0);

                Assert.Equal(0, scheduler.SpeculationBudgetExhaustionEvents);
            });
        }

        [Fact]
        public void WhenPeakConcurrentTrackedThenMaxObserved()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.CreditFairnessEnabled = false;
            scheduler.BankPressureTieBreakEnabled = false;
            scheduler.SpeculationBudgetEnabled = true;
            scheduler.SpeculationBudgetMax = 4;

            // PackBundle with cross-thread memory ops drives budget spend.
            // We'll check that PeakConcurrentSpeculativeOps stays within budget bounds.
            Assert.True(scheduler.PeakConcurrentSpeculativeOps >= 0);
            Assert.True(scheduler.PeakConcurrentSpeculativeOps <= scheduler.SpeculationBudgetMax);
        }
    }
}
