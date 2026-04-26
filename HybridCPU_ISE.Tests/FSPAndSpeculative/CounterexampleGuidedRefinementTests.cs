using HybridCPU_ISE.Core;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests
{
    public class CounterexampleGuidedRefinementTests
    {
        [Fact]
        public void WhenCounterexamplesEmittedThenRecordsAreStructured()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.EnableShadowOracle = true;

            // Restrictive donor mask to force oracle gap and counterexamples
            scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
                isActive: true,
                epochId: 70,
                cachedPc: 0xB000,
                epochLength: 10,
                completedReplays: 2,
                validSlotCount: 1,
                stableDonorMask: 0x01,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None));

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);

            scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, 8, 9, 10));
            scheduler.NominateSmtCandidate(2, MicroOpTestHelper.CreateScalarALU(2, 12, 13, 14));

            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            var counterexamples = scheduler.GetCounterexamples();

            // If there is a gap, counterexamples should be emitted
            OracleGapSummary summary = scheduler.GetOracleGapSummary();
            if (summary.TotalGap > 0)
            {
                Assert.NotEmpty(counterexamples);
                foreach (var cx in counterexamples)
                {
                    Assert.True(cx.MissedSlots > 0, "Each counterexample must have at least 1 missed slot.");
                    Assert.NotEqual(OracleGapCategory.None, cx.Category);
                    Assert.False(string.IsNullOrWhiteSpace(cx.Description));
                    Assert.False(string.IsNullOrWhiteSpace(cx.Describe()));
                }
            }
        }

        [Fact]
        public void WhenCounterexamplesExtractedThenEvidenceSummaryIsCorrect()
        {
            var counterexamples = new[]
            {
                new OracleCounterexample(1, OracleGapCategory.DonorRestriction, 2, "Donor mask restricted 2 slots"),
                new OracleCounterexample(2, OracleGapCategory.DonorRestriction, 1, "Donor mask restricted 1 slot"),
                new OracleCounterexample(3, OracleGapCategory.FairnessOrdering, 1, "Fairness skipped 1 candidate")
            };

            CounterexampleEvidenceSummary evidence = ReplayEngine.ExtractCounterexampleEvidence(counterexamples);

            Assert.Equal(3, evidence.TotalCounterexamples);
            Assert.Equal(2, evidence.DonorRestrictionCount);
            Assert.Equal(1, evidence.FairnessOrderingCount);
            Assert.Equal(0, evidence.LegalityConservatismCount);
            Assert.Equal(0, evidence.DomainIsolationCount);
            Assert.Equal(0, evidence.SpeculationBudgetCount);
            Assert.Equal(4, evidence.TotalMissedSlots);
            Assert.Equal(OracleGapCategory.DonorRestriction, evidence.DominantCategory);
        }

        [Fact]
        public void WhenRefinementGuidanceGeneratedThenNoProductionRuleMutation()
        {
            var counterexamples = new[]
            {
                new OracleCounterexample(1, OracleGapCategory.DonorRestriction, 2, "Donor mask restricted 2 slots"),
                new OracleCounterexample(2, OracleGapCategory.SpeculationBudget, 1, "Budget exhausted")
            };

            var evidence = ReplayEngine.ExtractCounterexampleEvidence(counterexamples);
            var guidance = ReplayEngine.GenerateRefinementGuidance(evidence);

            // Guidance should be non-empty and human-readable
            Assert.NotEmpty(guidance);

            // Guidance must not contain auto-mutation language
            foreach (var hint in guidance)
            {
                Assert.DoesNotContain("AUTO-APPLY", hint);
                Assert.DoesNotContain("MUTATING", hint);
                Assert.False(string.IsNullOrWhiteSpace(hint));
            }
        }

        [Fact]
        public void WhenNoCounterexamplesThenEvidenceSummaryIsEmpty()
        {
            var evidence = ReplayEngine.ExtractCounterexampleEvidence(
                System.Array.Empty<OracleCounterexample>());

            Assert.Equal(0, evidence.TotalCounterexamples);
            Assert.Equal(0, evidence.TotalMissedSlots);
            Assert.Equal(OracleGapCategory.None, evidence.DominantCategory);
        }

        [Fact]
        public void WhenNoCounterexamplesThenGuidanceSaysNoRefinementNeeded()
        {
            var evidence = ReplayEngine.ExtractCounterexampleEvidence(
                System.Array.Empty<OracleCounterexample>());
            var guidance = ReplayEngine.GenerateRefinementGuidance(evidence);

            Assert.Single(guidance);
            Assert.Contains("No refinement guidance needed", guidance[0]);
        }

        [Fact]
        public void WhenDivergencePatternsLocalizedThenCounterexampleDescriptionsAreSpecific()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.EnableShadowOracle = true;

            // Run multiple cycles with donor restriction
            for (int i = 0; i < 3; i++)
            {
                scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
                    isActive: true,
                    epochId: (ulong)(80 + i),
                    cachedPc: (ulong)(0xC000 + i * 0x100),
                    epochLength: 8,
                    completedReplays: 2,
                    validSlotCount: 1,
                    stableDonorMask: 0x01,
                    lastInvalidationReason: ReplayPhaseInvalidationReason.None));

                var bundle = new MicroOp[8];
                bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);

                scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, (ushort)(8 + i * 4), (ushort)(9 + i * 4), (ushort)(10 + i * 4)));
                scheduler.NominateSmtCandidate(2, MicroOpTestHelper.CreateScalarALU(2, (ushort)(20 + i * 4), (ushort)(21 + i * 4), (ushort)(22 + i * 4)));

                scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);
            }

            var counterexamples = scheduler.GetCounterexamples();
            var evidence = ReplayEngine.ExtractCounterexampleEvidence(counterexamples);

            // Each counterexample with donor restriction should mention the mask
            foreach (var cx in counterexamples)
            {
                if (cx.Category == OracleGapCategory.DonorRestriction)
                {
                    Assert.Contains("donor mask", cx.Description, System.StringComparison.OrdinalIgnoreCase);
                }
            }

            string description = evidence.Describe();
            Assert.False(string.IsNullOrWhiteSpace(description));
        }

        [Fact]
        public void WhenCounterexampleBufferExceedsLimitThenBufferIsBounded()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.EnableShadowOracle = true;

            // Run many cycles to try to overflow the counterexample buffer (limit = 64)
            for (int i = 0; i < 100; i++)
            {
                scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
                    isActive: true,
                    epochId: (ulong)(100 + i),
                    cachedPc: (ulong)(0xD000 + i * 0x100),
                    epochLength: 8,
                    completedReplays: 2,
                    validSlotCount: 1,
                    stableDonorMask: 0x01,
                    lastInvalidationReason: ReplayPhaseInvalidationReason.None));

                var bundle = new MicroOp[8];
                bundle[0] = MicroOpTestHelper.CreateScalarALU(0, 1, 2, 3);

                // Use modular register IDs to stay within ushort range (0-31 safe for mask builders)
                ushort baseReg = (ushort)((i % 6) * 4 + 4);
                scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateScalarALU(1, (ushort)(baseReg), (ushort)(baseReg + 1), (ushort)(baseReg + 2)));
                scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);
            }

            var counterexamples = scheduler.GetCounterexamples();

            Assert.True(counterexamples.Count <= 64,
                "Counterexample buffer must be bounded to prevent unbounded growth.");
        }
    }
}
