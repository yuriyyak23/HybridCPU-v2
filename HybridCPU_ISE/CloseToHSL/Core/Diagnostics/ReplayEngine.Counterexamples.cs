using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Core
{
    /// <summary>
    /// Aggregated counterexample evidence summary for Phase 5 refinement guidance.
    /// </summary>
    public readonly struct CounterexampleEvidenceSummary
    {
        public CounterexampleEvidenceSummary(
            int totalCounterexamples,
            int donorRestrictionCount,
            int fairnessOrderingCount,
            int legalityConservatismCount,
            int domainIsolationCount,
            int speculationBudgetCount,
            long totalMissedSlots,
            int classCapacityDivergenceCount = 0)
        {
            TotalCounterexamples = totalCounterexamples;
            DonorRestrictionCount = donorRestrictionCount;
            FairnessOrderingCount = fairnessOrderingCount;
            LegalityConservatismCount = legalityConservatismCount;
            DomainIsolationCount = domainIsolationCount;
            SpeculationBudgetCount = speculationBudgetCount;
            TotalMissedSlots = totalMissedSlots;
            ClassCapacityDivergenceCount = classCapacityDivergenceCount;
        }

        /// <summary>Total number of counterexamples captured.</summary>
        public int TotalCounterexamples { get; }

        /// <summary>Counterexamples attributed to donor/replay mask restrictions.</summary>
        public int DonorRestrictionCount { get; }

        /// <summary>Counterexamples attributed to fairness ordering.</summary>
        public int FairnessOrderingCount { get; }

        /// <summary>Counterexamples attributed to legality conservatism.</summary>
        public int LegalityConservatismCount { get; }

        /// <summary>Counterexamples attributed to domain isolation.</summary>
        public int DomainIsolationCount { get; }

        /// <summary>Counterexamples attributed to speculation budget exhaustion.</summary>
        public int SpeculationBudgetCount { get; }

        /// <summary>Total missed slots across all counterexamples.</summary>
        public long TotalMissedSlots { get; }

        /// <summary>Phase 07: Counterexamples attributed to class-capacity template divergence.</summary>
        public int ClassCapacityDivergenceCount { get; }

        /// <summary>Dominant category: the reason bucket with the most counterexamples.</summary>
        public OracleGapCategory DominantCategory
        {
            get
            {
                if (TotalCounterexamples == 0)
                    return OracleGapCategory.None;

                int max = DonorRestrictionCount;
                var cat = OracleGapCategory.DonorRestriction;

                if (FairnessOrderingCount > max) { max = FairnessOrderingCount; cat = OracleGapCategory.FairnessOrdering; }
                if (LegalityConservatismCount > max) { max = LegalityConservatismCount; cat = OracleGapCategory.LegalityConservatism; }
                if (DomainIsolationCount > max) { max = DomainIsolationCount; cat = OracleGapCategory.DomainIsolation; }
                if (SpeculationBudgetCount > max) { max = SpeculationBudgetCount; cat = OracleGapCategory.SpeculationBudget; }
                if (ClassCapacityDivergenceCount > max) { cat = OracleGapCategory.ClassCapacityDivergence; }

                return cat;
            }
        }

        public string Describe()
        {
            if (TotalCounterexamples == 0)
                return "No counterexamples captured.";

            return $"{TotalCounterexamples} counterexample(s), {TotalMissedSlots} total missed slot(s). " +
                   $"Dominant: {DominantCategory}. " +
                   $"Breakdown: donor={DonorRestrictionCount}, fairness={FairnessOrderingCount}, " +
                   $"legality={LegalityConservatismCount}, domain={DomainIsolationCount}, " +
                   $"specBudget={SpeculationBudgetCount}, classCapacity={ClassCapacityDivergenceCount}.";
        }
    }

    /// <summary>
    /// Phase 5: Counterexample extraction and refinement guidance helpers.
    ///
    /// This partial class adds methods for extracting structured counterexample
    /// evidence from oracle gap records. The evidence is diagnostic-only and does
    /// not auto-mutate production legality rules.
    /// </summary>
    public partial class ReplayEngine
    {
        /// <summary>
        /// Extract a counterexample evidence summary from oracle gap counterexamples.
        /// </summary>
        public static CounterexampleEvidenceSummary ExtractCounterexampleEvidence(
            IReadOnlyList<OracleCounterexample> counterexamples)
        {
            ArgumentNullException.ThrowIfNull(counterexamples);

            if (counterexamples.Count == 0)
            {
                return new CounterexampleEvidenceSummary(0, 0, 0, 0, 0, 0, 0);
            }

            int donor = 0, fairness = 0, legality = 0, domain = 0, specBudget = 0, classCapacity = 0;
            long totalMissed = 0;

            foreach (var cx in counterexamples)
            {
                totalMissed += cx.MissedSlots;
                switch (cx.Category)
                {
                    case OracleGapCategory.DonorRestriction: donor++; break;
                    case OracleGapCategory.FairnessOrdering: fairness++; break;
                    case OracleGapCategory.LegalityConservatism: legality++; break;
                    case OracleGapCategory.DomainIsolation: domain++; break;
                    case OracleGapCategory.SpeculationBudget: specBudget++; break;
                    case OracleGapCategory.ClassCapacityDivergence: classCapacity++; break;
                }
            }

            return new CounterexampleEvidenceSummary(
                counterexamples.Count,
                donor, fairness, legality, domain, specBudget,
                totalMissed, classCapacity);
        }

        /// <summary>
        /// Generate refinement guidance strings from counterexample evidence.
        /// These are human-readable hints, not automatic rule mutations.
        /// </summary>
        public static IReadOnlyList<string> GenerateRefinementGuidance(
            CounterexampleEvidenceSummary evidence)
        {
            var guidance = new List<string>();

            if (evidence.TotalCounterexamples == 0)
            {
                guidance.Add("No refinement guidance needed — no counterexamples detected.");
                return guidance;
            }

            if (evidence.DonorRestrictionCount > 0)
            {
                guidance.Add(
                    $"DONOR MASK: {evidence.DonorRestrictionCount} counterexample(s) show " +
                    "replay-stable donor mask is over-restrictive. " +
                    "Consider widening stable donor mask during long replay epochs.");
            }

            if (evidence.FairnessOrderingCount > 0)
            {
                guidance.Add(
                    $"FAIRNESS: {evidence.FairnessOrderingCount} counterexample(s) show " +
                    "credit-based ordering skipped ready candidates. " +
                    "Consider adjusting FAIRNESS_CREDIT_CAP or tie-break heuristic.");
            }

            if (evidence.LegalityConservatismCount > 0)
            {
                guidance.Add(
                    $"LEGALITY: {evidence.LegalityConservatismCount} counterexample(s) show " +
                    "safety mask or certificate conservatism rejected oracle-legal candidates. " +
                    "Consider auditing RAR-aware hazard detection for false positives.");
            }

            if (evidence.DomainIsolationCount > 0)
            {
                guidance.Add(
                    $"DOMAIN: {evidence.DomainIsolationCount} counterexample(s) show " +
                    "domain isolation enforcement blocked otherwise-legal injections. " +
                    "Review kernel-to-user enforcement scope if workload is single-domain.");
            }

            if (evidence.SpeculationBudgetCount > 0)
            {
                guidance.Add(
                    $"SPEC BUDGET: {evidence.SpeculationBudgetCount} counterexample(s) show " +
                    "speculation budget exhaustion prevented valid injections. " +
                    "Consider increasing SpeculationBudgetMax for workloads with high memory ILP.");
            }

            if (evidence.ClassCapacityDivergenceCount > 0)
            {
                guidance.Add(
                    $"CLASS CAPACITY: {evidence.ClassCapacityDivergenceCount} counterexample(s) show " +
                    "class-capacity template divergence between runs. " +
                    "Review class-level replay template stability and domain scoping correctness.");
            }

            return guidance;
        }
    }
}
