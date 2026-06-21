using System;
using System.Collections.Generic;
using System.Numerics;
using YAKSys_Hybrid_CPU.Arch;

namespace HybridCPU.Compiler.Core.IR
{
    public static partial class HybridCpuSlotModel
    {
        /// <summary>
        /// Searches the legal physical slot placements for one candidate group without changing scheduler order.
        /// </summary>
        public static IrBundlePlacementSearchResult SearchAssignments(IReadOnlyList<IrIssueSlotMask> legalSlots)
        {
            return SearchAssignments(legalSlots, previousInstructionSlots: null);
        }

        /// <summary>
        /// Searches the legal physical slot placements for one candidate group with adjacent-bundle context.
        /// </summary>
        public static IrBundlePlacementSearchResult SearchAssignments(IReadOnlyList<IrIssueSlotMask> legalSlots, IReadOnlyList<int>? previousInstructionSlots)
        {
            return SearchAssignments(legalSlots, previousInstructionSlots, default);
        }

        internal static IrBundlePlacementSearchResult SearchAssignments(
            IReadOnlyList<IrIssueSlotMask> legalSlots,
            IReadOnlyList<int>? previousInstructionSlots,
            HybridCpuBackendPlacementTieBreakContext tieBreakContext)
        {
            ArgumentNullException.ThrowIfNull(legalSlots);

            IrSlotAssignmentAnalysis analysis = AnalyzeAssignment(legalSlots);
            if (!analysis.HasLegalAssignment || legalSlots.Count == 0)
            {
                return new IrBundlePlacementSearchResult(
                    analysis,
                    Array.Empty<IrBundlePlacementCandidate>(),
                    Array.Empty<int>(),
                    IrBundlePlacementQuality.Empty,
                    IrBundleTransitionQuality.Empty,
                    IrBundlePlacementSearchSummary.Empty);
            }

            List<IrBundlePlacementCandidate> evaluatedPlacements = EnumeratePlacementCandidates(legalSlots);
            if (evaluatedPlacements.Count == 0)
            {
                return new IrBundlePlacementSearchResult(
                    analysis,
                    Array.Empty<IrBundlePlacementCandidate>(),
                    Array.Empty<int>(),
                    IrBundlePlacementQuality.Empty,
                    IrBundleTransitionQuality.Empty,
                    IrBundlePlacementSearchSummary.Empty);
            }

            var paretoOptimalPlacements = new List<IrBundlePlacementCandidate>();
            IrBundlePlacementCandidate? bestCandidate = null;
            IrBundleTransitionQuality bestTransitionQuality = IrBundleTransitionQuality.Empty;

            foreach (IrBundlePlacementCandidate candidate in evaluatedPlacements)
            {
                AddToParetoFrontier(paretoOptimalPlacements, candidate);

                IrBundleTransitionQuality candidateTransitionQuality = IrBundleTransitionQuality.Create(previousInstructionSlots, candidate.InstructionSlots);
                if (bestCandidate is null || HybridCpuBundlePlacementPolicy.IsBetterPlacement(candidate.Quality, candidateTransitionQuality, candidate.InstructionSlots, bestCandidate.Quality, bestTransitionQuality, bestCandidate.InstructionSlots, tieBreakContext))
                {
                    bestCandidate = candidate;
                    bestTransitionQuality = candidateTransitionQuality;
                }
            }

            if (bestCandidate is null)
            {
                return new IrBundlePlacementSearchResult(
                    analysis,
                    Array.Empty<IrBundlePlacementCandidate>(),
                    Array.Empty<int>(),
                    IrBundlePlacementQuality.Empty,
                    IrBundleTransitionQuality.Empty,
                    IrBundlePlacementSearchSummary.Empty);
            }

            return new IrBundlePlacementSearchResult(
                analysis,
                paretoOptimalPlacements,
                bestCandidate.InstructionSlots,
                bestCandidate.Quality,
                bestTransitionQuality,
                new IrBundlePlacementSearchSummary(
                    evaluatedPlacements.Count,
                    paretoOptimalPlacements.Count,
                    evaluatedPlacements.Count - paretoOptimalPlacements.Count));
        }

        /// <summary>
        /// Searches legal placement pairs for two adjacent bundles without changing scheduler order.
        /// </summary>
        public static IrAdjacentBundlePlacementSearchResult SearchAdjacentBundleAssignments(
            IReadOnlyList<IrIssueSlotMask> firstBundleLegalSlots,
            IReadOnlyList<IrIssueSlotMask> secondBundleLegalSlots)
        {
            return SearchAdjacentBundleAssignments(firstBundleLegalSlots, secondBundleLegalSlots, previousInstructionSlots: null);
        }

        /// <summary>
        /// Searches legal placement pairs for two adjacent bundles with incoming previous-bundle context without changing scheduler order.
        /// </summary>
        public static IrAdjacentBundlePlacementSearchResult SearchAdjacentBundleAssignments(
            IReadOnlyList<IrIssueSlotMask> firstBundleLegalSlots,
            IReadOnlyList<IrIssueSlotMask> secondBundleLegalSlots,
            IReadOnlyList<int>? previousInstructionSlots)
        {
            return SearchAdjacentBundleAssignments(firstBundleLegalSlots, secondBundleLegalSlots, previousInstructionSlots, default);
        }

        internal static IrAdjacentBundlePlacementSearchResult SearchAdjacentBundleAssignments(
            IReadOnlyList<IrIssueSlotMask> firstBundleLegalSlots,
            IReadOnlyList<IrIssueSlotMask> secondBundleLegalSlots,
            IReadOnlyList<int>? previousInstructionSlots,
            HybridCpuBackendPlacementTieBreakContext tieBreakContext)
        {
            ArgumentNullException.ThrowIfNull(firstBundleLegalSlots);
            ArgumentNullException.ThrowIfNull(secondBundleLegalSlots);

            IrSlotAssignmentAnalysis firstAnalysis = AnalyzeAssignment(firstBundleLegalSlots);
            IrSlotAssignmentAnalysis secondAnalysis = AnalyzeAssignment(secondBundleLegalSlots);
            if (!firstAnalysis.HasLegalAssignment || !secondAnalysis.HasLegalAssignment || firstBundleLegalSlots.Count == 0 || secondBundleLegalSlots.Count == 0)
            {
                return new IrAdjacentBundlePlacementSearchResult(
                    firstAnalysis,
                    secondAnalysis,
                    Array.Empty<IrAdjacentBundlePlacementCandidate>(),
                    bestPlacementPair: null,
                    IrAdjacentBundlePlacementSearchSummary.Empty);
            }

            List<IrBundlePlacementCandidate> firstBundleCandidates = EnumeratePlacementCandidates(firstBundleLegalSlots);
            List<IrBundlePlacementCandidate> secondBundleCandidates = EnumeratePlacementCandidates(secondBundleLegalSlots);
            if (firstBundleCandidates.Count == 0 || secondBundleCandidates.Count == 0)
            {
                return new IrAdjacentBundlePlacementSearchResult(
                    firstAnalysis,
                    secondAnalysis,
                    Array.Empty<IrAdjacentBundlePlacementCandidate>(),
                    bestPlacementPair: null,
                    IrAdjacentBundlePlacementSearchSummary.Empty);
            }

            var paretoOptimalPlacementPairs = new List<IrAdjacentBundlePlacementCandidate>();
            IrAdjacentBundlePlacementCandidate? bestPlacementPair = null;
            int evaluatedPlacementPairCount = 0;
            foreach (IrBundlePlacementCandidate firstBundleCandidate in firstBundleCandidates)
            {
                foreach (IrBundlePlacementCandidate secondBundleCandidate in secondBundleCandidates)
                {
                    evaluatedPlacementPairCount++;
                    IrBundleTransitionQuality incomingTransitionQuality = IrBundleTransitionQuality.Create(previousInstructionSlots, firstBundleCandidate.InstructionSlots);
                    IrBundleTransitionQuality transitionQuality = IrBundleTransitionQuality.Create(firstBundleCandidate.InstructionSlots, secondBundleCandidate.InstructionSlots);
                    IrAdjacentBundlePlacementQuality pairQuality = IrAdjacentBundlePlacementQuality.Create(
                        firstBundleCandidate.Quality,
                        secondBundleCandidate.Quality,
                        incomingTransitionQuality,
                        transitionQuality);
                    var pairCandidate = new IrAdjacentBundlePlacementCandidate(
                        firstBundleCandidate.InstructionSlots,
                        secondBundleCandidate.InstructionSlots,
                        firstBundleCandidate.Quality,
                        secondBundleCandidate.Quality,
                        incomingTransitionQuality,
                        transitionQuality,
                        pairQuality);

                    AddToParetoFrontier(paretoOptimalPlacementPairs, pairCandidate);
                    if (bestPlacementPair is null || HybridCpuAdjacentBundlePlacementPolicy.IsBetterPlacement(pairCandidate, bestPlacementPair, tieBreakContext))
                    {
                        bestPlacementPair = pairCandidate;
                    }
                }
            }

            return new IrAdjacentBundlePlacementSearchResult(
                firstAnalysis,
                secondAnalysis,
                paretoOptimalPlacementPairs,
                bestPlacementPair,
                new IrAdjacentBundlePlacementSearchSummary(
                    evaluatedPlacementPairCount,
                    paretoOptimalPlacementPairs.Count,
                    evaluatedPlacementPairCount - paretoOptimalPlacementPairs.Count));
        }
    }
}
