using Xunit;
using Xunit.Abstractions;
using YAKSys_Hybrid_CPU.Core;
using HybridCPU_ISE.Tests.TestHelpers;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Phase 2A: Credit-Based Deterministic Fairness tests.
    /// Validates that credit accumulation, balanced injection distribution,
    /// and starvation prevention work correctly in the scheduler.
    /// </summary>
    public class CreditFairnessTests
    {
        private readonly ITestOutputHelper _output;

        public CreditFairnessTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void WhenCreditFairnessEnabledThenAllVtsReceiveInjections()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.CreditFairnessEnabled = true;
            scheduler.BankPressureTieBreakEnabled = false;
            scheduler.SpeculationBudgetEnabled = false;

            int ownerVt = 0;
            int cycles = 200;

            for (int c = 0; c < cycles; c++)
            {
                var bundle = new MicroOp[8];
                bundle[0] = MicroOpTestHelper.CreateScalarALU(ownerVt, (ushort)(c % 4), 60, 61);

                for (int vt = 1; vt < 4; vt++)
                {
                    var candidate = MicroOpTestHelper.CreateScalarALU(
                        vt, (ushort)(16 + vt * 4 + (c % 4)), (ushort)(48 + vt), (ushort)(52 + vt));
                    scheduler.NominateSmtCandidate(vt, candidate);
                }

                scheduler.PackBundleIntraCoreSmt(bundle, ownerVt, 0);
            }

            long vt1 = scheduler.TestGetPerVtInjections(1);
            long vt2 = scheduler.TestGetPerVtInjections(2);
            long vt3 = scheduler.TestGetPerVtInjections(3);

            _output.WriteLine($"VT1={vt1}, VT2={vt2}, VT3={vt3}");

            Assert.True(vt1 > 0, "VT1 should receive injections");
            Assert.True(vt2 > 0, "VT2 should receive injections");
            Assert.True(vt3 > 0, "VT3 should receive injections");
        }

        [Fact]
        public void WhenVtSkippedThenCreditAccumulates()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.CreditFairnessEnabled = true;
            scheduler.BankPressureTieBreakEnabled = false;
            scheduler.SpeculationBudgetEnabled = false;

            // Nominate VT2 but fill all slots so it can't be injected (only 1 empty slot, VT1 gets it due to lower vtId tie-break initially)
            // First run 1 cycle with only VT2 having a candidate but the bundle is full
            var fullBundle = new MicroOp[8];
            for (int i = 0; i < 8; i++)
                fullBundle[i] = MicroOpTestHelper.CreateScalarALU(0, (ushort)(i * 4), (ushort)(i * 4 + 1), (ushort)(i * 4 + 2));

            var candidateVt2 = MicroOpTestHelper.CreateScalarALU(2, 32, 48, 49);
            scheduler.NominateSmtCandidate(2, candidateVt2);

            scheduler.PackBundleIntraCoreSmt(fullBundle, 0, 0);

            // VT2 had a valid candidate but couldn't inject -> credit should increase
            int credit = scheduler.TestGetFairnessCredit(2);
            Assert.True(credit > 0, $"VT2 credit should accumulate when skipped, got {credit}");
        }

        [Fact]
        public void WhenCreditFairnessDisabledThenFixedPriorityUsed()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.CreditFairnessEnabled = false;
            scheduler.BankPressureTieBreakEnabled = false;
            scheduler.SpeculationBudgetEnabled = false;

            int ownerVt = 0;
            int cycles = 100;

            for (int c = 0; c < cycles; c++)
            {
                // Only 1 empty slot per cycle -> VT with fixed-priority advantage wins
                var bundle = new MicroOp[8];
                for (int i = 0; i < 7; i++)
                    bundle[i] = MicroOpTestHelper.CreateScalarALU(ownerVt, (ushort)(i * 4), (ushort)(i * 4 + 1), (ushort)(i * 4 + 2));

                for (int vt = 1; vt < 4; vt++)
                {
                    var candidate = MicroOpTestHelper.CreateScalarALU(
                        vt, (ushort)(16 + vt * 4), (ushort)(48 + vt), (ushort)(52 + vt));
                    scheduler.NominateSmtCandidate(vt, candidate);
                }

                scheduler.PackBundleIntraCoreSmt(bundle, ownerVt, 0);
            }

            long vt1 = scheduler.TestGetPerVtInjections(1);
            long vt3 = scheduler.TestGetPerVtInjections(3);

            _output.WriteLine($"VT1={vt1}, VT3={vt3}");

            // With fixed priority, VT1 should always win (it scans first)
            Assert.True(vt1 > vt3, "VT1 should dominate with fixed priority");
        }

        [Fact]
        public void WhenCreditsSeededThenHighCreditVtGoesFirst()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.CreditFairnessEnabled = true;
            scheduler.BankPressureTieBreakEnabled = false;
            scheduler.SpeculationBudgetEnabled = false;

            // Seed VT3 with high credit so it should go first
            scheduler.TestSetFairnessCredit(3, 10);
            scheduler.TestSetFairnessCredit(1, 0);
            scheduler.TestSetFairnessCredit(2, 0);

            // One empty slot -> the VT with highest credit should win
            var bundle = new MicroOp[8];
            for (int i = 0; i < 7; i++)
                bundle[i] = MicroOpTestHelper.CreateScalarALU(0, (ushort)(i * 4), (ushort)(i * 4 + 1), (ushort)(i * 4 + 2));

            for (int vt = 1; vt < 4; vt++)
            {
                var candidate = MicroOpTestHelper.CreateScalarALU(
                    vt, (ushort)(16 + vt * 4), (ushort)(48 + vt), (ushort)(52 + vt));
                scheduler.NominateSmtCandidate(vt, candidate);
            }

            scheduler.PackBundleIntraCoreSmt(bundle, 0, 0);

            long vt3 = scheduler.TestGetPerVtInjections(3);
            Assert.Equal(1, vt3);
        }

        [Fact]
        public void WhenStarvationOccursThenStarvationEventCounted()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.CreditFairnessEnabled = true;
            scheduler.BankPressureTieBreakEnabled = false;
            scheduler.SpeculationBudgetEnabled = false;

            // Pre-seed VT2 credit just below cap
            scheduler.TestSetFairnessCredit(2, 15);

            // Full bundle -> VT2 can't inject but has valid candidate -> credit will hit cap
            var fullBundle = new MicroOp[8];
            for (int i = 0; i < 8; i++)
                fullBundle[i] = MicroOpTestHelper.CreateScalarALU(0, (ushort)(i * 4), (ushort)(i * 4 + 1), (ushort)(i * 4 + 2));

            var candidateVt2 = MicroOpTestHelper.CreateScalarALU(2, 32, 48, 49);
            scheduler.NominateSmtCandidate(2, candidateVt2);

            long starvationBefore = scheduler.FairnessStarvationEvents;
            scheduler.PackBundleIntraCoreSmt(fullBundle, 0, 0);
            long starvationAfter = scheduler.FairnessStarvationEvents;

            Assert.True(starvationAfter > starvationBefore, "Starvation event should be recorded when credit hits cap");
        }

        [Fact]
        public void WhenCreditFairnessEnabledThenInjectionDistributionIsBalanced()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.CreditFairnessEnabled = true;
            scheduler.BankPressureTieBreakEnabled = false;
            scheduler.SpeculationBudgetEnabled = false;

            int ownerVt = 0;
            int cycles = 600;

            for (int c = 0; c < cycles; c++)
            {
                // 3 empty slots per cycle -> all 3 background VTs can inject
                var bundle = new MicroOp[8];
                for (int i = 0; i < 5; i++)
                    bundle[i] = MicroOpTestHelper.CreateScalarALU(ownerVt, (ushort)(i * 4), (ushort)(i * 4 + 1), (ushort)(i * 4 + 2));

                for (int vt = 1; vt < 4; vt++)
                {
                    var candidate = MicroOpTestHelper.CreateScalarALU(
                        vt, (ushort)(16 + vt * 4 + (c % 4)), (ushort)(48 + vt), (ushort)(52 + vt));
                    scheduler.NominateSmtCandidate(vt, candidate);
                }

                scheduler.PackBundleIntraCoreSmt(bundle, ownerVt, 0);
            }

            long vt1 = scheduler.TestGetPerVtInjections(1);
            long vt2 = scheduler.TestGetPerVtInjections(2);
            long vt3 = scheduler.TestGetPerVtInjections(3);

            _output.WriteLine($"VT1={vt1}, VT2={vt2}, VT3={vt3}");

            long max = System.Math.Max(vt1, System.Math.Max(vt2, vt3));
            long min = System.Math.Min(vt1, System.Math.Min(vt2, vt3));

            // With credit fairness, distribution should be within 20% of each other
            Assert.True(min * 100 / max >= 80,
                $"Injection distribution too skewed: min={min}, max={max}, ratio={min * 100 / max}%");
        }
    }
}
