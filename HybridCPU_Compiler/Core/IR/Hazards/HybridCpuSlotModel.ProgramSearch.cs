using System;
using System.Collections.Generic;
using System.Numerics;
using YAKSys_Hybrid_CPU.Arch;

namespace HybridCPU.Compiler.Core.IR
{
    public static partial class HybridCpuSlotModel
    {
        /// <summary>
        /// Searches legal placement triplets for three adjacent bundles without changing scheduler order.
        /// </summary>
        public static IrAdjacentBundleTripletPlacementSearchResult SearchAdjacentBundleTripletAssignments(
            IReadOnlyList<IrIssueSlotMask> firstBundleLegalSlots,
            IReadOnlyList<IrIssueSlotMask> secondBundleLegalSlots,
            IReadOnlyList<IrIssueSlotMask> thirdBundleLegalSlots)
        {
            return SearchAdjacentBundleTripletAssignments(firstBundleLegalSlots, secondBundleLegalSlots, thirdBundleLegalSlots, previousInstructionSlots: null);
        }

        /// <summary>
        /// Searches legal placement triplets for three adjacent bundles with incoming previous-bundle context without changing scheduler order.
        /// </summary>
        public static IrAdjacentBundleTripletPlacementSearchResult SearchAdjacentBundleTripletAssignments(
            IReadOnlyList<IrIssueSlotMask> firstBundleLegalSlots,
            IReadOnlyList<IrIssueSlotMask> secondBundleLegalSlots,
            IReadOnlyList<IrIssueSlotMask> thirdBundleLegalSlots,
            IReadOnlyList<int>? previousInstructionSlots)
        {
            return SearchAdjacentBundleTripletAssignments(firstBundleLegalSlots, secondBundleLegalSlots, thirdBundleLegalSlots, previousInstructionSlots, default);
        }

        internal static IrAdjacentBundleTripletPlacementSearchResult SearchAdjacentBundleTripletAssignments(
            IReadOnlyList<IrIssueSlotMask> firstBundleLegalSlots,
            IReadOnlyList<IrIssueSlotMask> secondBundleLegalSlots,
            IReadOnlyList<IrIssueSlotMask> thirdBundleLegalSlots,
            IReadOnlyList<int>? previousInstructionSlots,
            HybridCpuBackendPlacementTieBreakContext tieBreakContext)
        {
            ArgumentNullException.ThrowIfNull(firstBundleLegalSlots);
            ArgumentNullException.ThrowIfNull(secondBundleLegalSlots);
            ArgumentNullException.ThrowIfNull(thirdBundleLegalSlots);

            IrSlotAssignmentAnalysis firstAnalysis = AnalyzeAssignment(firstBundleLegalSlots);
            IrSlotAssignmentAnalysis secondAnalysis = AnalyzeAssignment(secondBundleLegalSlots);
            IrSlotAssignmentAnalysis thirdAnalysis = AnalyzeAssignment(thirdBundleLegalSlots);
            if (!firstAnalysis.HasLegalAssignment
                || !secondAnalysis.HasLegalAssignment
                || !thirdAnalysis.HasLegalAssignment
                || firstBundleLegalSlots.Count == 0
                || secondBundleLegalSlots.Count == 0
                || thirdBundleLegalSlots.Count == 0)
            {
                return new IrAdjacentBundleTripletPlacementSearchResult(
                    firstAnalysis,
                    secondAnalysis,
                    thirdAnalysis,
                    Array.Empty<IrAdjacentBundleTripletPlacementCandidate>(),
                    bestPlacementTriplet: null,
                    IrAdjacentBundleTripletPlacementSearchSummary.Empty);
            }

            List<IrBundlePlacementCandidate> firstBundleCandidates = EnumeratePlacementCandidates(firstBundleLegalSlots);
            List<IrBundlePlacementCandidate> secondBundleCandidates = EnumeratePlacementCandidates(secondBundleLegalSlots);
            List<IrBundlePlacementCandidate> thirdBundleCandidates = EnumeratePlacementCandidates(thirdBundleLegalSlots);
            if (firstBundleCandidates.Count == 0 || secondBundleCandidates.Count == 0 || thirdBundleCandidates.Count == 0)
            {
                return new IrAdjacentBundleTripletPlacementSearchResult(
                    firstAnalysis,
                    secondAnalysis,
                    thirdAnalysis,
                    Array.Empty<IrAdjacentBundleTripletPlacementCandidate>(),
                    bestPlacementTriplet: null,
                    IrAdjacentBundleTripletPlacementSearchSummary.Empty);
            }

            var paretoOptimalPlacementTriplets = new List<IrAdjacentBundleTripletPlacementCandidate>();
            IrAdjacentBundleTripletPlacementCandidate? bestPlacementTriplet = null;
            int evaluatedPlacementTripletCount = 0;
            foreach (IrBundlePlacementCandidate firstBundleCandidate in firstBundleCandidates)
            {
                foreach (IrBundlePlacementCandidate secondBundleCandidate in secondBundleCandidates)
                {
                    foreach (IrBundlePlacementCandidate thirdBundleCandidate in thirdBundleCandidates)
                    {
                        evaluatedPlacementTripletCount++;
                        IrBundleTransitionQuality incomingTransitionQuality = IrBundleTransitionQuality.Create(previousInstructionSlots, firstBundleCandidate.InstructionSlots);
                        IrBundleTransitionQuality firstTransitionQuality = IrBundleTransitionQuality.Create(firstBundleCandidate.InstructionSlots, secondBundleCandidate.InstructionSlots);
                        IrBundleTransitionQuality secondTransitionQuality = IrBundleTransitionQuality.Create(secondBundleCandidate.InstructionSlots, thirdBundleCandidate.InstructionSlots);
                        IrAdjacentBundleTripletPlacementQuality tripletQuality = IrAdjacentBundleTripletPlacementQuality.Create(
                            firstBundleCandidate.Quality,
                            secondBundleCandidate.Quality,
                            thirdBundleCandidate.Quality,
                            incomingTransitionQuality,
                            firstTransitionQuality,
                            secondTransitionQuality);
                        var tripletCandidate = new IrAdjacentBundleTripletPlacementCandidate(
                            firstBundleCandidate.InstructionSlots,
                            secondBundleCandidate.InstructionSlots,
                            thirdBundleCandidate.InstructionSlots,
                            firstBundleCandidate.Quality,
                            secondBundleCandidate.Quality,
                            thirdBundleCandidate.Quality,
                            incomingTransitionQuality,
                            firstTransitionQuality,
                            secondTransitionQuality,
                            tripletQuality);

                        AddToParetoFrontier(paretoOptimalPlacementTriplets, tripletCandidate);
                        if (bestPlacementTriplet is null || HybridCpuAdjacentBundleTripletPlacementPolicy.IsBetterPlacement(tripletCandidate, bestPlacementTriplet, tieBreakContext))
                        {
                            bestPlacementTriplet = tripletCandidate;
                        }
                    }
                }
            }

            return new IrAdjacentBundleTripletPlacementSearchResult(
                firstAnalysis,
                secondAnalysis,
                thirdAnalysis,
                paretoOptimalPlacementTriplets,
                bestPlacementTriplet,
                new IrAdjacentBundleTripletPlacementSearchSummary(
                    evaluatedPlacementTripletCount,
                    paretoOptimalPlacementTriplets.Count,
                    evaluatedPlacementTripletCount - paretoOptimalPlacementTriplets.Count));
        }

        /// <summary>
        /// Searches legal placements for a whole basic-block bundle sequence without changing scheduler order.
        /// </summary>
        public static IrBasicBlockPlacementSearchResult SearchBasicBlockAssignments(IReadOnlyList<IReadOnlyList<IrIssueSlotMask>> bundleLegalSlots)
        {
            return SearchBasicBlockAssignments(bundleLegalSlots, previousInstructionSlots: null);
        }

        /// <summary>
        /// Searches legal placements for a whole basic-block bundle sequence with incoming previous-bundle context without changing scheduler order.
        /// </summary>
        public static IrBasicBlockPlacementSearchResult SearchBasicBlockAssignments(
            IReadOnlyList<IReadOnlyList<IrIssueSlotMask>> bundleLegalSlots,
            IReadOnlyList<int>? previousInstructionSlots)
        {
            ArgumentNullException.ThrowIfNull(bundleLegalSlots);

            if (bundleLegalSlots.Count == 0)
            {
                return new IrBasicBlockPlacementSearchResult(
                    Array.Empty<IrSlotAssignmentAnalysis>(),
                    Array.Empty<IrBasicBlockPlacementCandidate>(),
                    bestPlacement: null,
                    IrBasicBlockPlacementSearchSummary.Empty);
            }

            var bundleAnalyses = new IrSlotAssignmentAnalysis[bundleLegalSlots.Count];
            var bundleCandidates = new List<IrBundlePlacementCandidate>[bundleLegalSlots.Count];
            for (int bundleIndex = 0; bundleIndex < bundleLegalSlots.Count; bundleIndex++)
            {
                IReadOnlyList<IrIssueSlotMask> currentBundleLegalSlots = bundleLegalSlots[bundleIndex];
                ArgumentNullException.ThrowIfNull(currentBundleLegalSlots);

                IrSlotAssignmentAnalysis analysis = AnalyzeAssignment(currentBundleLegalSlots);
                bundleAnalyses[bundleIndex] = analysis;
                if (!analysis.HasLegalAssignment || currentBundleLegalSlots.Count == 0)
                {
                    return new IrBasicBlockPlacementSearchResult(
                        bundleAnalyses,
                        Array.Empty<IrBasicBlockPlacementCandidate>(),
                        bestPlacement: null,
                        IrBasicBlockPlacementSearchSummary.Empty);
                }

                List<IrBundlePlacementCandidate> evaluatedCandidates = EnumeratePlacementCandidates(currentBundleLegalSlots);
                if (evaluatedCandidates.Count == 0)
                {
                    return new IrBasicBlockPlacementSearchResult(
                        bundleAnalyses,
                        Array.Empty<IrBasicBlockPlacementCandidate>(),
                        bestPlacement: null,
                        IrBasicBlockPlacementSearchSummary.Empty);
                }

                bundleCandidates[bundleIndex] = evaluatedCandidates;
            }

            var paretoOptimalPlacements = new List<IrBasicBlockPlacementCandidate>();
            IrBasicBlockPlacementCandidate? bestPlacement = null;
            int evaluatedPlacementCount = 0;
            CollectBasicBlockPlacementCandidates(
                bundleCandidates,
                previousInstructionSlots,
                bundleIndex: 0,
                new List<IrBundlePlacementCandidate>(bundleCandidates.Length),
                paretoOptimalPlacements,
                ref bestPlacement,
                ref evaluatedPlacementCount);

            return new IrBasicBlockPlacementSearchResult(
                bundleAnalyses,
                paretoOptimalPlacements,
                bestPlacement,
                new IrBasicBlockPlacementSearchSummary(
                    evaluatedPlacementCount,
                    paretoOptimalPlacements.Count,
                    evaluatedPlacementCount - paretoOptimalPlacements.Count));
        }

        /// <summary>
        /// Searches scalable global placements for a whole basic-block bundle sequence without changing scheduler order.
        /// </summary>
        public static IrGlobalBasicBlockPlacementSearchResult SearchGlobalBasicBlockAssignments(IReadOnlyList<IReadOnlyList<IrIssueSlotMask>> bundleLegalSlots)
        {
            return SearchGlobalBasicBlockAssignments(bundleLegalSlots, previousInstructionSlots: null);
        }

        /// <summary>
        /// Searches scalable global placements for a whole basic-block bundle sequence with incoming previous-bundle context without changing scheduler order.
        /// </summary>
        public static IrGlobalBasicBlockPlacementSearchResult SearchGlobalBasicBlockAssignments(
            IReadOnlyList<IReadOnlyList<IrIssueSlotMask>> bundleLegalSlots,
            IReadOnlyList<int>? previousInstructionSlots)
        {
            return SearchGlobalBasicBlockAssignments(bundleLegalSlots, previousInstructionSlots, default);
        }

        internal static IrGlobalBasicBlockPlacementSearchResult SearchGlobalBasicBlockAssignments(
            IReadOnlyList<IReadOnlyList<IrIssueSlotMask>> bundleLegalSlots,
            IReadOnlyList<int>? previousInstructionSlots,
            HybridCpuBackendPlacementTieBreakContext tieBreakContext)
        {
            ArgumentNullException.ThrowIfNull(bundleLegalSlots);

            if (bundleLegalSlots.Count == 0)
            {
                return new IrGlobalBasicBlockPlacementSearchResult(
                    Array.Empty<IrSlotAssignmentAnalysis>(),
                    bestPlacement: null,
                    IrGlobalBasicBlockPlacementSearchSummary.Empty);
            }

            if (!TryCollectBundlePlacementCandidates(bundleLegalSlots, out IrSlotAssignmentAnalysis[] bundleAnalyses, out List<IrBundlePlacementCandidate>[] bundleCandidates))
            {
                return new IrGlobalBasicBlockPlacementSearchResult(
                    bundleAnalyses,
                    bestPlacement: null,
                    IrGlobalBasicBlockPlacementSearchSummary.Empty);
            }

            int evaluatedTransitionCount = 0;
            int retainedStateCount = 0;
            List<IrBasicBlockPlacementCandidate> retainedStates = CreateInitialGlobalStates(bundleCandidates[0], previousInstructionSlots, ref evaluatedTransitionCount, ref retainedStateCount);
            for (int bundleIndex = 1; bundleIndex < bundleCandidates.Length; bundleIndex++)
            {
                var nextStates = new IrBasicBlockPlacementCandidate[bundleCandidates[bundleIndex].Count];
                for (int candidateIndex = 0; candidateIndex < bundleCandidates[bundleIndex].Count; candidateIndex++)
                {
                    IrBundlePlacementCandidate currentBundleCandidate = bundleCandidates[bundleIndex][candidateIndex];
                    for (int stateIndex = 0; stateIndex < retainedStates.Count; stateIndex++)
                    {
                        evaluatedTransitionCount++;
                        IrBasicBlockPlacementCandidate extendedPlacement = AppendBasicBlockPlacementCandidate(retainedStates[stateIndex], currentBundleCandidate);
                        if (nextStates[candidateIndex] is null || HybridCpuBasicBlockPlacementPolicy.IsBetterPlacement(extendedPlacement, nextStates[candidateIndex], tieBreakContext))
                        {
                            nextStates[candidateIndex] = extendedPlacement;
                        }
                    }
                }

                retainedStates = new List<IrBasicBlockPlacementCandidate>(nextStates.Length);
                foreach (IrBasicBlockPlacementCandidate state in nextStates)
                {
                    retainedStates.Add(state);
                }

                retainedStateCount += retainedStates.Count;
            }

            IrBasicBlockPlacementCandidate? bestPlacement = null;
            foreach (IrBasicBlockPlacementCandidate retainedState in retainedStates)
            {
                if (bestPlacement is null || HybridCpuBasicBlockPlacementPolicy.IsBetterPlacement(retainedState, bestPlacement, tieBreakContext))
                {
                    bestPlacement = retainedState;
                }
            }

            return new IrGlobalBasicBlockPlacementSearchResult(
                bundleAnalyses,
                bestPlacement,
                new IrGlobalBasicBlockPlacementSearchSummary(
                    bundleCandidates.Length,
                    evaluatedTransitionCount,
                    retainedStateCount));
        }

        /// <summary>
        /// Searches scalable global placements for a whole program without changing scheduler order.
        /// </summary>
        public static IrProgramPlacementSearchResult SearchProgramAssignments(IReadOnlyList<IReadOnlyList<IReadOnlyList<IrIssueSlotMask>>> programLegalSlots)
        {
            return SearchProgramAssignments(programLegalSlots, previousInstructionSlots: null);
        }

        /// <summary>
        /// Searches scalable global placements for a whole program with incoming previous-bundle context without changing scheduler order.
        /// </summary>
        public static IrProgramPlacementSearchResult SearchProgramAssignments(
            IReadOnlyList<IReadOnlyList<IReadOnlyList<IrIssueSlotMask>>> programLegalSlots,
            IReadOnlyList<int>? previousInstructionSlots)
        {
            return SearchProgramAssignments(programLegalSlots, previousInstructionSlots, default);
        }

        internal static IrProgramPlacementSearchResult SearchProgramAssignments(
            IReadOnlyList<IReadOnlyList<IReadOnlyList<IrIssueSlotMask>>> programLegalSlots,
            IReadOnlyList<int>? previousInstructionSlots,
            HybridCpuBackendPlacementTieBreakContext tieBreakContext)
        {
            ArgumentNullException.ThrowIfNull(programLegalSlots);

            if (programLegalSlots.Count == 0)
            {
                return new IrProgramPlacementSearchResult(
                    Array.Empty<IReadOnlyList<IrSlotAssignmentAnalysis>>(),
                    bestPlacement: null,
                    IrProgramPlacementSearchSummary.Empty);
            }

            var blockBundleCounts = new int[programLegalSlots.Count];
            var flattenedBundleLegalSlots = new List<IReadOnlyList<IrIssueSlotMask>>();
            for (int blockIndex = 0; blockIndex < programLegalSlots.Count; blockIndex++)
            {
                IReadOnlyList<IReadOnlyList<IrIssueSlotMask>> blockLegalSlots = programLegalSlots[blockIndex];
                ArgumentNullException.ThrowIfNull(blockLegalSlots);
                blockBundleCounts[blockIndex] = blockLegalSlots.Count;
                foreach (IReadOnlyList<IrIssueSlotMask> bundleLegalSlot in blockLegalSlots)
                {
                    flattenedBundleLegalSlots.Add(bundleLegalSlot);
                }
            }

            IrGlobalBasicBlockPlacementSearchResult globalBlockSearch = SearchGlobalBasicBlockAssignments(flattenedBundleLegalSlots, previousInstructionSlots, tieBreakContext);
            IReadOnlyList<IReadOnlyList<IrSlotAssignmentAnalysis>> blockAnalyses = SplitBlockAnalyses(globalBlockSearch.BundleAnalyses, blockBundleCounts);
            if (!globalBlockSearch.HasLegalAssignment || globalBlockSearch.BestPlacement is null)
            {
                return new IrProgramPlacementSearchResult(
                    blockAnalyses,
                    bestPlacement: null,
                    new IrProgramPlacementSearchSummary(
                        programLegalSlots.Count,
                        flattenedBundleLegalSlots.Count,
                        globalBlockSearch.Summary.EvaluatedTransitionCount,
                        globalBlockSearch.Summary.RetainedStateCount));
            }

            var blockPlacements = new IrBasicBlockPlacementCandidate[blockBundleCounts.Length];
            int bundleStartIndex = 0;
            for (int blockIndex = 0; blockIndex < blockBundleCounts.Length; blockIndex++)
            {
                blockPlacements[blockIndex] = SliceBlockPlacementCandidate(globalBlockSearch.BestPlacement, bundleStartIndex, blockBundleCounts[blockIndex]);
                bundleStartIndex += blockBundleCounts[blockIndex];
            }

            return new IrProgramPlacementSearchResult(
                blockAnalyses,
                new IrProgramPlacementCandidate(blockPlacements, globalBlockSearch.BestPlacement.Quality),
                new IrProgramPlacementSearchSummary(
                    programLegalSlots.Count,
                    flattenedBundleLegalSlots.Count,
                    globalBlockSearch.Summary.EvaluatedTransitionCount,
                    globalBlockSearch.Summary.RetainedStateCount));
        }
    }
}
