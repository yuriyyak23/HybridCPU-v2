using System;
using System.Collections.Generic;
using System.Linq;
using HybridCPU.Compiler.Core.IR.Telemetry;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Materializes physical bundles from an existing local schedule without changing scheduler policy.
    /// </summary>
    public sealed class HybridCpuBundleFormer
    {
        private const string EmptySlotReason = "No scheduled instruction occupied this slot in the current cycle, so the bundler left an internal NOP.";
        private const int MaxWholeProgramGlobalPlacementBundles = 96;
        private const int MaxBlockLookaheadBundles = 64;

        private readonly HybridCpuInstructionLegalityChecker _legalityChecker = new();
        private readonly bool _useClassFirstBinding;

        /// <summary>
        /// Telemetry profile reader supplying advisory Stage 6 placement signals.
        /// </summary>
        public TelemetryProfileReader? ProfileReader { get; set; }

        /// <summary>
        /// Enables bounded certificate-aware placement tie-breaks.
        /// Default: <c>false</c>.
        /// </summary>
        public bool UseCertificateAwareCoalescingTieBreaks { get; set; }

        /// <summary>
        /// Virtual thread ID for the current program being materialized.
        /// Default: <c>-1</c> (unknown).
        /// </summary>
        public int VirtualThreadId { get; set; } = -1;

        /// <summary>
        /// Marks the current bundle formation path as coordinator-special.
        /// Default: <c>false</c>.
        /// </summary>
        public bool TreatAsCoordinatorPath { get; set; }

        /// <summary>
        /// Initializes a new instance with the legacy slot-search path.
        /// </summary>
        public HybridCpuBundleFormer() : this(useClassFirstBinding: false) { }

        /// <summary>
        /// Initializes a new instance with an optional class-first binding path.
        /// </summary>
        /// <param name="useClassFirstBinding">
        /// When <see langword="true"/>, the direct <c>MaterializeBundle</c> path attempts
        /// deterministic late-lane binding before falling back to exhaustive search.
        /// </param>
        public HybridCpuBundleFormer(bool useClassFirstBinding)
        {
            _useClassFirstBinding = useClassFirstBinding;
        }

        /// <summary>
        /// Materializes bundles for a scheduled IR program.
        /// </summary>
        public IrProgramBundlingResult BundleProgram(IrProgramSchedule programSchedule)
        {
            ArgumentNullException.ThrowIfNull(programSchedule);

            if (ShouldAttemptWholeProgramGlobalPlacement(programSchedule)
                && TryBundleProgramGlobally(programSchedule, out IrProgramBundlingResult? programResult))
            {
                return programResult!;
            }

            var blockResults = new List<IrBasicBlockBundlingResult>(programSchedule.BlockSchedules.Count);
            foreach (IrBasicBlockSchedule blockSchedule in programSchedule.BlockSchedules)
            {
                blockResults.Add(BundleBlock(blockSchedule));
            }

            return new IrProgramBundlingResult(programSchedule, blockResults);
        }

        /// <summary>
        /// Materializes bundles for one scheduled basic block.
        /// </summary>
        public IrBasicBlockBundlingResult BundleBlock(IrBasicBlockSchedule blockSchedule)
        {
            ArgumentNullException.ThrowIfNull(blockSchedule);

            bool allowLookaheadSearch = ShouldAttemptBlockLookahead(blockSchedule);
            var bundles = new List<IrMaterializedBundle>(blockSchedule.CycleGroups.Count);
            IReadOnlyList<int>? previousInstructionSlots = null;
            for (int cycleGroupIndex = 0; cycleGroupIndex < blockSchedule.CycleGroups.Count;)
            {
                IrScheduleCycleGroup cycleGroup = blockSchedule.CycleGroups[cycleGroupIndex];
                if (allowLookaheadSearch
                    && cycleGroupIndex + 1 < blockSchedule.CycleGroups.Count
                    && TryMaterializeBlockGlobalLookahead(
                        blockSchedule,
                        cycleGroupIndex,
                        previousInstructionSlots,
                        out IrMaterializedBundle? blockGlobalBundle))
                {
                    bundles.Add(blockGlobalBundle!);
                    previousInstructionSlots = blockGlobalBundle!.SlotAssignment.InstructionSlots;
                    cycleGroupIndex++;
                    continue;
                }

                if (allowLookaheadSearch
                    && cycleGroupIndex + 2 < blockSchedule.CycleGroups.Count
                    && TryMaterializeAdjacentBundleTripletLookahead(
                        blockSchedule,
                        cycleGroup,
                        blockSchedule.CycleGroups[cycleGroupIndex + 1],
                        blockSchedule.CycleGroups[cycleGroupIndex + 2],
                        previousInstructionSlots,
                        out IrMaterializedBundle? tripletLeadBundle))
                {
                    bundles.Add(tripletLeadBundle!);
                    previousInstructionSlots = tripletLeadBundle!.SlotAssignment.InstructionSlots;
                    cycleGroupIndex++;
                    continue;
                }

                if (allowLookaheadSearch
                    && cycleGroupIndex + 1 < blockSchedule.CycleGroups.Count
                    && TryMaterializeAdjacentBundlePair(
                        blockSchedule,
                        cycleGroup,
                        blockSchedule.CycleGroups[cycleGroupIndex + 1],
                        previousInstructionSlots,
                        out IrMaterializedBundle? firstBundle,
                        out IrMaterializedBundle? secondBundle))
                {
                    bundles.Add(firstBundle!);
                    bundles.Add(secondBundle!);
                    previousInstructionSlots = secondBundle!.SlotAssignment.InstructionSlots;
                    cycleGroupIndex += 2;
                    continue;
                }

                IrMaterializedBundle bundle = MaterializeBundle(blockSchedule, cycleGroup, previousInstructionSlots);
                bundles.Add(bundle);
                previousInstructionSlots = bundle.SlotAssignment.InstructionSlots;
                cycleGroupIndex++;
            }

            return new IrBasicBlockBundlingResult(blockSchedule, bundles);
        }

        private static bool ShouldAttemptWholeProgramGlobalPlacement(IrProgramSchedule programSchedule)
        {
            return CountProgramCycleGroups(programSchedule) <= MaxWholeProgramGlobalPlacementBundles;
        }

        private static bool ShouldAttemptBlockLookahead(IrBasicBlockSchedule blockSchedule)
        {
            return blockSchedule.CycleGroups.Count <= MaxBlockLookaheadBundles;
        }

        private static int CountProgramCycleGroups(IrProgramSchedule programSchedule)
        {
            int totalCycleGroups = 0;
            for (int blockIndex = 0; blockIndex < programSchedule.BlockSchedules.Count; blockIndex++)
            {
                totalCycleGroups += programSchedule.BlockSchedules[blockIndex].CycleGroups.Count;
            }

            return totalCycleGroups;
        }

        private bool TryBundleProgramGlobally(IrProgramSchedule programSchedule, out IrProgramBundlingResult? programResult)
        {
            ArgumentNullException.ThrowIfNull(programSchedule);

            var programLegalSlots = new IReadOnlyList<IReadOnlyList<IrIssueSlotMask>>[programSchedule.BlockSchedules.Count];
            var legalityAnalyses = new IrCandidateBundleAnalysis[programSchedule.BlockSchedules.Count][];
            var localSearchResults = new IrBundlePlacementSearchResult[programSchedule.BlockSchedules.Count][];
            HybridCpuBackendPlacementTieBreakContext tieBreakContext = CreatePlacementTieBreakContext();
            for (int blockIndex = 0; blockIndex < programSchedule.BlockSchedules.Count; blockIndex++)
            {
                IrBasicBlockSchedule blockSchedule = programSchedule.BlockSchedules[blockIndex];
                var blockLegalSlots = new IReadOnlyList<IrIssueSlotMask>[blockSchedule.CycleGroups.Count];
                var blockLegalityAnalyses = new IrCandidateBundleAnalysis[blockSchedule.CycleGroups.Count];
                var blockLocalSearchResults = new IrBundlePlacementSearchResult[blockSchedule.CycleGroups.Count];
                for (int bundleIndex = 0; bundleIndex < blockSchedule.CycleGroups.Count; bundleIndex++)
                {
                    IrScheduleCycleGroup cycleGroup = blockSchedule.CycleGroups[bundleIndex];
                    blockLegalityAnalyses[bundleIndex] = AnalyzeLegality(blockSchedule, cycleGroup);
                    IReadOnlyList<IrIssueSlotMask> bundleLegalSlots = GetLegalSlots(cycleGroup.Instructions);
                    blockLegalSlots[bundleIndex] = bundleLegalSlots;
                    blockLocalSearchResults[bundleIndex] = HybridCpuSlotModel.SearchAssignments(bundleLegalSlots, previousInstructionSlots: null, tieBreakContext);
                }

                programLegalSlots[blockIndex] = blockLegalSlots;
                legalityAnalyses[blockIndex] = blockLegalityAnalyses;
                localSearchResults[blockIndex] = blockLocalSearchResults;
            }

            IrProgramPlacementSearchResult programSearch = HybridCpuSlotModel.SearchProgramAssignments(programLegalSlots, previousInstructionSlots: null, tieBreakContext);
            if (!programSearch.HasLegalAssignment || programSearch.BestPlacement is null)
            {
                programResult = null;
                return false;
            }

            var blockResults = new List<IrBasicBlockBundlingResult>(programSchedule.BlockSchedules.Count);
            for (int blockIndex = 0; blockIndex < programSchedule.BlockSchedules.Count; blockIndex++)
            {
                IrBasicBlockSchedule blockSchedule = programSchedule.BlockSchedules[blockIndex];
                IrBasicBlockPlacementCandidate blockPlacement = programSearch.BestPlacement.BlockPlacements[blockIndex];
                var bundles = new List<IrMaterializedBundle>(blockSchedule.CycleGroups.Count);
                for (int bundleIndex = 0; bundleIndex < blockSchedule.CycleGroups.Count; bundleIndex++)
                {
                    IrBundleTransitionQuality transitionQuality = bundleIndex == 0
                        ? blockPlacement.IncomingTransitionQuality
                        : blockPlacement.CrossBundleTransitionQualities[bundleIndex - 1];
                    IrMaterializedSlotAssignment slotAssignment = CreateSlotAssignment(
                        programSearch.BlockAnalyses[blockIndex][bundleIndex],
                        blockPlacement.BundleInstructionSlots[bundleIndex],
                        blockPlacement.BundlePlacementQualities[bundleIndex],
                        localSearchResults[blockIndex][bundleIndex].Summary,
                        transitionQuality);
                    bundles.Add(MaterializeBundle(blockSchedule.CycleGroups[bundleIndex], legalityAnalyses[blockIndex][bundleIndex], slotAssignment));
                }

                blockResults.Add(new IrBasicBlockBundlingResult(blockSchedule, bundles));
            }

            programResult = new IrProgramBundlingResult(programSchedule, blockResults);
            return true;
        }

        private bool TryMaterializeBlockGlobalLookahead(
            IrBasicBlockSchedule blockSchedule,
            int cycleGroupIndex,
            IReadOnlyList<int>? previousInstructionSlots,
            out IrMaterializedBundle? firstBundle)
        {
            ArgumentNullException.ThrowIfNull(blockSchedule);

            int remainingBundleCount = blockSchedule.CycleGroups.Count - cycleGroupIndex;
            var remainingCycleGroups = new IrScheduleCycleGroup[remainingBundleCount];
            var legalityAnalyses = new IrCandidateBundleAnalysis[remainingBundleCount];
            var bundleLegalSlots = new IReadOnlyList<IrIssueSlotMask>[remainingBundleCount];
            var localSearchResults = new IrBundlePlacementSearchResult[remainingBundleCount];
            HybridCpuBackendPlacementTieBreakContext tieBreakContext = CreatePlacementTieBreakContext();
            for (int remainingIndex = 0; remainingIndex < remainingBundleCount; remainingIndex++)
            {
                IrScheduleCycleGroup currentCycleGroup = blockSchedule.CycleGroups[cycleGroupIndex + remainingIndex];
                remainingCycleGroups[remainingIndex] = currentCycleGroup;
                legalityAnalyses[remainingIndex] = AnalyzeLegality(blockSchedule, currentCycleGroup);
                IReadOnlyList<IrIssueSlotMask> currentLegalSlots = GetLegalSlots(currentCycleGroup.Instructions);
                bundleLegalSlots[remainingIndex] = currentLegalSlots;
                IrBundlePlacementSearchResult localSearch = HybridCpuSlotModel.SearchAssignments(currentLegalSlots, previousInstructionSlots: null, tieBreakContext);
                localSearchResults[remainingIndex] = localSearch;
            }

            IrGlobalBasicBlockPlacementSearchResult blockSearch = HybridCpuSlotModel.SearchGlobalBasicBlockAssignments(bundleLegalSlots, previousInstructionSlots, tieBreakContext);
            if (!blockSearch.HasLegalAssignment || blockSearch.BestPlacement is null)
            {
                firstBundle = null;
                return false;
            }

            IrBasicBlockPlacementCandidate bestPlacement = blockSearch.BestPlacement;
            firstBundle = MaterializeBundle(
                remainingCycleGroups[0],
                legalityAnalyses[0],
                CreateSlotAssignment(
                    blockSearch.BundleAnalyses[0],
                    bestPlacement.BundleInstructionSlots[0],
                    bestPlacement.BundlePlacementQualities[0],
                    localSearchResults[0].Summary,
                    bestPlacement.IncomingTransitionQuality));

            return true;
        }

        private bool TryMaterializeAdjacentBundleTripletLookahead(
            IrBasicBlockSchedule blockSchedule,
            IrScheduleCycleGroup firstCycleGroup,
            IrScheduleCycleGroup secondCycleGroup,
            IrScheduleCycleGroup thirdCycleGroup,
            IReadOnlyList<int>? previousInstructionSlots,
            out IrMaterializedBundle? firstBundle)
        {
            ArgumentNullException.ThrowIfNull(blockSchedule);
            ArgumentNullException.ThrowIfNull(firstCycleGroup);
            ArgumentNullException.ThrowIfNull(secondCycleGroup);
            ArgumentNullException.ThrowIfNull(thirdCycleGroup);

            IrCandidateBundleAnalysis firstLegalityAnalysis = AnalyzeLegality(blockSchedule, firstCycleGroup);
            _ = AnalyzeLegality(blockSchedule, secondCycleGroup);
            _ = AnalyzeLegality(blockSchedule, thirdCycleGroup);

            IReadOnlyList<IrIssueSlotMask> firstLegalSlots = GetLegalSlots(firstCycleGroup.Instructions);
            IReadOnlyList<IrIssueSlotMask> secondLegalSlots = GetLegalSlots(secondCycleGroup.Instructions);
            IReadOnlyList<IrIssueSlotMask> thirdLegalSlots = GetLegalSlots(thirdCycleGroup.Instructions);
            HybridCpuBackendPlacementTieBreakContext tieBreakContext = CreatePlacementTieBreakContext();
            IrAdjacentBundleTripletPlacementSearchResult tripletSearch = HybridCpuSlotModel.SearchAdjacentBundleTripletAssignments(
                firstLegalSlots,
                secondLegalSlots,
                thirdLegalSlots,
                previousInstructionSlots,
                tieBreakContext);

            if (!tripletSearch.HasLegalAssignment || tripletSearch.BestPlacementTriplet is null)
            {
                firstBundle = null;
                return false;
            }

            IrAdjacentBundleTripletPlacementCandidate bestPlacementTriplet = tripletSearch.BestPlacementTriplet;
            IrBundlePlacementSearchResult firstBundleSearch = HybridCpuSlotModel.SearchAssignments(firstLegalSlots, previousInstructionSlots, tieBreakContext);
            firstBundle = MaterializeBundle(
                firstCycleGroup,
                firstLegalityAnalysis,
                CreateSlotAssignment(
                    tripletSearch.FirstBundleAnalysis,
                    bestPlacementTriplet.FirstInstructionSlots,
                    bestPlacementTriplet.FirstPlacementQuality,
                    firstBundleSearch.Summary,
                    bestPlacementTriplet.IncomingTransitionQuality));

            return true;
        }

        private bool TryMaterializeAdjacentBundlePair(
            IrBasicBlockSchedule blockSchedule,
            IrScheduleCycleGroup firstCycleGroup,
            IrScheduleCycleGroup secondCycleGroup,
            IReadOnlyList<int>? previousInstructionSlots,
            out IrMaterializedBundle? firstBundle,
            out IrMaterializedBundle? secondBundle)
        {
            ArgumentNullException.ThrowIfNull(blockSchedule);
            ArgumentNullException.ThrowIfNull(firstCycleGroup);
            ArgumentNullException.ThrowIfNull(secondCycleGroup);

            IrCandidateBundleAnalysis firstLegalityAnalysis = AnalyzeLegality(blockSchedule, firstCycleGroup);
            IrCandidateBundleAnalysis secondLegalityAnalysis = AnalyzeLegality(blockSchedule, secondCycleGroup);
            IReadOnlyList<IrIssueSlotMask> firstLegalSlots = GetLegalSlots(firstCycleGroup.Instructions);
            IReadOnlyList<IrIssueSlotMask> secondLegalSlots = GetLegalSlots(secondCycleGroup.Instructions);
            HybridCpuBackendPlacementTieBreakContext tieBreakContext = CreatePlacementTieBreakContext();
            IrAdjacentBundlePlacementSearchResult pairSearch = HybridCpuSlotModel.SearchAdjacentBundleAssignments(
                firstLegalSlots,
                secondLegalSlots,
                previousInstructionSlots,
                tieBreakContext);

            if (!pairSearch.HasLegalAssignment || pairSearch.BestPlacementPair is null)
            {
                firstBundle = null;
                secondBundle = null;
                return false;
            }

            IrAdjacentBundlePlacementCandidate bestPlacementPair = pairSearch.BestPlacementPair;
            IrBundlePlacementSearchResult firstBundleSearch = HybridCpuSlotModel.SearchAssignments(firstLegalSlots, previousInstructionSlots, tieBreakContext);
            IrBundlePlacementSearchResult secondBundleSearch = HybridCpuSlotModel.SearchAssignments(secondLegalSlots, bestPlacementPair.FirstInstructionSlots, tieBreakContext);

            firstBundle = MaterializeBundle(
                firstCycleGroup,
                firstLegalityAnalysis,
                CreateSlotAssignment(
                    pairSearch.FirstBundleAnalysis,
                    bestPlacementPair.FirstInstructionSlots,
                    bestPlacementPair.FirstPlacementQuality,
                    firstBundleSearch.Summary,
                    bestPlacementPair.IncomingTransitionQuality));

            secondBundle = MaterializeBundle(
                secondCycleGroup,
                secondLegalityAnalysis,
                CreateSlotAssignment(
                    pairSearch.SecondBundleAnalysis,
                    bestPlacementPair.SecondInstructionSlots,
                    bestPlacementPair.SecondPlacementQuality,
                    secondBundleSearch.Summary,
                    bestPlacementPair.TransitionQuality));

            return true;
        }

        private IrMaterializedBundle MaterializeBundle(
            IrBasicBlockSchedule blockSchedule,
            IrScheduleCycleGroup cycleGroup,
            IReadOnlyList<int>? previousInstructionSlots)
        {
            IrCandidateBundleAnalysis legalityAnalysis = AnalyzeLegality(blockSchedule, cycleGroup);

            // Phase 04: class-first deterministic binding path
            if (_useClassFirstBinding
                && legalityAnalysis.ClassCapacityResult?.IsWithinCapacity == true)
            {
                IrLateLaneBindingResult bindingResult = HybridCpuLateLaneBinder.BindLanes(
                    cycleGroup.Instructions,
                    legalityAnalysis.ClassCapacityResult);

                if (bindingResult.BindingSuccess)
                {
                    IrMaterializedSlotAssignment slotAssignment = CreateClassFirstSlotAssignment(
                        legalityAnalysis.SlotAnalysis,
                        bindingResult,
                        previousInstructionSlots);

                    return MaterializeBundleWithBindingMetadata(
                        cycleGroup, legalityAnalysis, slotAssignment, bindingResult);
                }
                // Binding failed — fall through to legacy exhaustive search
            }

            IrBundlePlacementSearchResult searchResult = HybridCpuSlotModel.SearchAssignments(GetLegalSlots(cycleGroup.Instructions), previousInstructionSlots, CreatePlacementTieBreakContext());
            if (!searchResult.HasLegalAssignment)
            {
                throw new InvalidOperationException($"Cannot materialize physical slots for block {blockSchedule.BlockId} at cycle {cycleGroup.Cycle} because the candidate group has no legal slot assignment.");
            }

            return MaterializeBundle(cycleGroup, legalityAnalysis, searchResult.MaterializeBestAssignment());
        }

        private IrCandidateBundleAnalysis AnalyzeLegality(IrBasicBlockSchedule blockSchedule, IrScheduleCycleGroup cycleGroup)
        {
            IrCandidateBundleAnalysis legalityAnalysis = _legalityChecker.AnalyzeCandidateBundle(cycleGroup.Instructions);
            if (!legalityAnalysis.IsLegal)
            {
                throw new InvalidOperationException($"Cannot materialize an illegal cycle group for block {blockSchedule.BlockId} at cycle {cycleGroup.Cycle}.");
            }

            return legalityAnalysis;
        }

        private static IrMaterializedSlotAssignment CreateSlotAssignment(
            IrSlotAssignmentAnalysis analysis,
            IReadOnlyList<int> instructionSlots,
            IrBundlePlacementQuality quality,
            IrBundlePlacementSearchSummary searchSummary,
            IrBundleTransitionQuality transitionQuality)
        {
            return new IrMaterializedSlotAssignment(
                analysis,
                instructionSlots.ToArray(),
                quality,
                searchSummary,
                transitionQuality);
        }

        private static IrMaterializedBundle MaterializeBundle(
            IrScheduleCycleGroup cycleGroup,
            IrCandidateBundleAnalysis legalityAnalysis,
            IrMaterializedSlotAssignment slotAssignment)
        {

            var slots = new IrMaterializedBundleSlot[HybridCpuSlotModel.SlotCount];
            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                slots[slotIndex] = new IrMaterializedBundleSlot(
                    slotIndex,
                    Instruction: null,
                    OrderInCycle: null,
                    InstructionLegalSlots: IrIssueSlotMask.None,
                    EmptyReason: EmptySlotReason);
            }

            for (int orderInCycle = 0; orderInCycle < cycleGroup.Instructions.Count; orderInCycle++)
            {
                IrInstruction instruction = cycleGroup.Instructions[orderInCycle];
                int assignedSlot = slotAssignment.InstructionSlots[orderInCycle];
                slots[assignedSlot] = new IrMaterializedBundleSlot(
                    assignedSlot,
                    instruction,
                    orderInCycle,
                    instruction.Annotation.LegalSlots);
            }

            return new IrMaterializedBundle(cycleGroup.Cycle, cycleGroup, legalityAnalysis, slotAssignment, slots);
        }

        private static IrMaterializedSlotAssignment CreateClassFirstSlotAssignment(
            IrSlotAssignmentAnalysis analysis,
            IrLateLaneBindingResult bindingResult,
            IReadOnlyList<int>? previousInstructionSlots)
        {
            IrBundlePlacementQuality quality = IrBundlePlacementQuality.Create(
                bindingResult.AssignedLanes, HybridCpuSlotModel.SlotCount);

            var searchSummary = new IrBundlePlacementSearchSummary(
                EvaluatedPlacementCount: 1,
                ParetoOptimalPlacementCount: 1,
                DominatedPlacementCount: 0);

            IrBundleTransitionQuality transitionQuality = previousInstructionSlots is not null
                ? IrBundleTransitionQuality.Create(previousInstructionSlots, bindingResult.AssignedLanes)
                : IrBundleTransitionQuality.Empty;

            return new IrMaterializedSlotAssignment(
                analysis,
                bindingResult.AssignedLanes.ToArray(),
                quality,
                searchSummary,
                transitionQuality);
        }

        private static IrMaterializedBundle MaterializeBundleWithBindingMetadata(
            IrScheduleCycleGroup cycleGroup,
            IrCandidateBundleAnalysis legalityAnalysis,
            IrMaterializedSlotAssignment slotAssignment,
            IrLateLaneBindingResult bindingResult)
        {
            var slots = new IrMaterializedBundleSlot[HybridCpuSlotModel.SlotCount];
            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                slots[slotIndex] = new IrMaterializedBundleSlot(
                    slotIndex,
                    Instruction: null,
                    OrderInCycle: null,
                    InstructionLegalSlots: IrIssueSlotMask.None,
                    EmptyReason: EmptySlotReason);
            }

            for (int orderInCycle = 0; orderInCycle < cycleGroup.Instructions.Count; orderInCycle++)
            {
                IrInstruction instruction = cycleGroup.Instructions[orderInCycle];
                int assignedSlot = slotAssignment.InstructionSlots[orderInCycle];
                slots[assignedSlot] = new IrMaterializedBundleSlot(
                    assignedSlot,
                    instruction,
                    orderInCycle,
                    instruction.Annotation.LegalSlots,
                    EmptyReason: null,
                    BindingKind: bindingResult.BindingKinds[orderInCycle],
                    AssignedClass: instruction.Annotation.RequiredSlotClass);
            }

            return new IrMaterializedBundle(cycleGroup.Cycle, cycleGroup, legalityAnalysis, slotAssignment, slots);
        }

        private static IReadOnlyList<IrIssueSlotMask> GetLegalSlots(IReadOnlyList<IrInstruction> instructions)
        {
            var legalSlots = new List<IrIssueSlotMask>(instructions.Count);
            foreach (IrInstruction instruction in instructions)
            {
                legalSlots.Add(instruction.Annotation.LegalSlots);
            }

            return legalSlots;
        }

        private HybridCpuBackendPlacementTieBreakContext CreatePlacementTieBreakContext()
        {
            double registerGroupPressure = 0.0;
            if (UseCertificateAwareCoalescingTieBreaks && ProfileReader is { HasProfile: true })
            {
                registerGroupPressure = ProfileReader.GetCertificateRegisterGroupPressureForBackendShaping(VirtualThreadId, TreatAsCoordinatorPath);
            }

            return new HybridCpuBackendPlacementTieBreakContext(
                UseCertificateAwareCoalescingTieBreaks,
                TreatAsCoordinatorPath,
                VirtualThreadId,
                registerGroupPressure);
        }
    }
}
