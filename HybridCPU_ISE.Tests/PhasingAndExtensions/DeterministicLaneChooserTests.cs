using Xunit;
using YAKSys_Hybrid_CPU.Core;
using HybridCPU_ISE.Tests.TestHelpers;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Phase 04 — Deterministic Lane Chooser tests.
    /// Validates DeterministicLaneChooser (Tier 1 + Tier 2), per-class lane tracking,
    /// telemetry counters, integration with TryMaterializeLane, and determinism.
    /// </summary>
    public class DeterministicLaneChooserTests
    {
        #region SelectLowestFree (Tier 1)

        [Fact]
        public void SelectLowestFree_WhenMultipleBitsSet_ThenReturnsLowestSetBit()
        {
            // 0b0000_1010 → bit 1 is lowest set
            int lane = DeterministicLaneChooser.SelectLowestFree(0b0000_1010);

            Assert.Equal(1, lane);
        }

        [Fact]
        public void SelectLowestFree_WhenOnlyHighBitSet_ThenReturns7()
        {
            int lane = DeterministicLaneChooser.SelectLowestFree(0b1000_0000);

            Assert.Equal(7, lane);
        }

        [Fact]
        public void SelectLowestFree_WhenEmpty_ThenReturnsMinusOne()
        {
            int lane = DeterministicLaneChooser.SelectLowestFree(0b0000_0000);

            Assert.Equal(-1, lane);
        }

        [Fact]
        public void SelectLowestFree_WhenOnlyBit0Set_ThenReturns0()
        {
            int lane = DeterministicLaneChooser.SelectLowestFree(0b0000_0001);

            Assert.Equal(0, lane);
        }

        [Fact]
        public void SelectLowestFree_WhenAllBitsSet_ThenReturns0()
        {
            int lane = DeterministicLaneChooser.SelectLowestFree(0b1111_1111);

            Assert.Equal(0, lane);
        }

        #endregion

        #region SelectWithReplayHint (Tier 2)

        [Fact]
        public void SelectWithReplayHint_WhenReplayActiveAndPreviousLaneFree_ThenReusesPreviousLane()
        {
            // freeLanes = 0b0000_1010 (lanes 1, 3), replay active, previous lane = 3
            int lane = DeterministicLaneChooser.SelectWithReplayHint(0b0000_1010, replayActive: true, previousLane: 3);

            Assert.Equal(3, lane);
        }

        [Fact]
        public void SelectWithReplayHint_WhenReplayActiveAndPreviousLaneNotFree_ThenFallsBackToLowest()
        {
            // freeLanes = 0b0000_1010 (lanes 1, 3), replay active, previous lane = 4 (not in mask)
            int lane = DeterministicLaneChooser.SelectWithReplayHint(0b0000_1010, replayActive: true, previousLane: 4);

            Assert.Equal(1, lane);
        }

        [Fact]
        public void SelectWithReplayHint_WhenReplayNotActive_ThenFallsBackToLowest()
        {
            // freeLanes = 0b0000_1010 (lanes 1, 3), replay NOT active, previous lane = 3
            int lane = DeterministicLaneChooser.SelectWithReplayHint(0b0000_1010, replayActive: false, previousLane: 3);

            Assert.Equal(1, lane);
        }

        [Fact]
        public void SelectWithReplayHint_WhenNoPreviousLane_ThenFallsBackToLowest()
        {
            // freeLanes = 0b0000_1010 (lanes 1, 3), replay active, no previous lane
            int lane = DeterministicLaneChooser.SelectWithReplayHint(0b0000_1010, replayActive: true, previousLane: -1);

            Assert.Equal(1, lane);
        }

        [Fact]
        public void SelectWithReplayHint_WhenEmptyMask_ThenReturnsMinusOne()
        {
            int lane = DeterministicLaneChooser.SelectWithReplayHint(0b0000_0000, replayActive: true, previousLane: 3);

            Assert.Equal(-1, lane);
        }

        #endregion

        #region Integration — TryMaterializeLane with DeterministicLaneChooser

        [Fact]
        public void TryMaterializeLane_WhenClassFlexible_OutsideReplay_ThenLowestLane()
        {
            var scheduler = new MicroOpScheduler();
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            byte occupancy = 0x00;

            bool result = scheduler.TestTryMaterializeLane(
                candidate, occupancy, out int lane, out var reason);

            Assert.True(result);
            Assert.Equal(0, lane);
            Assert.Equal(TypedSlotRejectReason.None, reason);
        }

        [Fact]
        public void TryMaterializeLane_WhenClassFlexible_ReplayActiveWithMatchingPreviousLane_ThenReusesPreviousLane()
        {
            var scheduler = new MicroOpScheduler();

            // Set up active replay phase
            var replayPhase = new ReplayPhaseContext(
                isActive: true, epochId: 1, cachedPc: 0x1000, epochLength: 10,
                completedReplays: 0, validSlotCount: 8, stableDonorMask: 0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);
            scheduler.SetReplayPhaseContext(replayPhase);

            // Seed previous lane for AluClass = lane 2
            scheduler.TestSetPreviousPhaseLane(SlotClass.AluClass, 2);

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            byte occupancy = 0x00; // All lanes free

            bool result = scheduler.TestTryMaterializeLane(
                candidate, occupancy, out int lane, out var reason);

            Assert.True(result);
            Assert.Equal(2, lane); // Reuses previous lane via Tier 2
            Assert.Equal(TypedSlotRejectReason.None, reason);
        }

        [Fact]
        public void TryMaterializeLane_WhenClassFlexible_ReplayActiveWithNonMatchingPreviousLane_ThenFallsBackToLowest()
        {
            var scheduler = new MicroOpScheduler();

            var replayPhase = new ReplayPhaseContext(
                isActive: true, epochId: 1, cachedPc: 0x1000, epochLength: 10,
                completedReplays: 0, validSlotCount: 8, stableDonorMask: 0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);
            scheduler.SetReplayPhaseContext(replayPhase);

            // Seed previous lane for AluClass = lane 2, but lane 2 is occupied
            scheduler.TestSetPreviousPhaseLane(SlotClass.AluClass, 2);

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            byte occupancy = 0b_0000_0101; // Lanes 0, 2 occupied

            bool result = scheduler.TestTryMaterializeLane(
                candidate, occupancy, out int lane, out var reason);

            Assert.True(result);
            Assert.Equal(1, lane); // Fallback to lowest free ALU lane
            Assert.Equal(TypedSlotRejectReason.None, reason);
        }

        [Fact]
        public void TryMaterializeLane_WhenHardPinned_ThenLaneChooserNotUsed()
        {
            var scheduler = new MicroOpScheduler();
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            candidate.Placement = candidate.Placement with
            {
                PinningKind = SlotPinningKind.HardPinned,
                PinnedLaneId = 2
            };
            byte occupancy = 0x00;

            bool result = scheduler.TestTryMaterializeLane(
                candidate, occupancy, out int lane, out var reason);

            Assert.True(result);
            Assert.Equal(2, lane); // Uses pinned lane, not chooser
            Assert.Equal(TypedSlotRejectReason.None, reason);
        }

        #endregion

        #region Per-Class Lane Tracking

        [Fact]
        public void PreviousPhaseLane_WhenInitialized_ThenAllMinusOne()
        {
            var scheduler = new MicroOpScheduler();

            Assert.Equal(-1, scheduler.TestGetPreviousPhaseLane(SlotClass.AluClass));
            Assert.Equal(-1, scheduler.TestGetPreviousPhaseLane(SlotClass.LsuClass));
            Assert.Equal(-1, scheduler.TestGetPreviousPhaseLane(SlotClass.DmaStreamClass));
            Assert.Equal(-1, scheduler.TestGetPreviousPhaseLane(SlotClass.BranchControl));
            Assert.Equal(-1, scheduler.TestGetPreviousPhaseLane(SlotClass.SystemSingleton));
            Assert.Equal(-1, scheduler.TestGetPreviousPhaseLane(SlotClass.Unclassified));
        }

        [Fact]
        public void PreviousPhaseLane_WhenSet_ThenReturnsCorrectValue()
        {
            var scheduler = new MicroOpScheduler();

            scheduler.TestSetPreviousPhaseLane(SlotClass.AluClass, 3);
            scheduler.TestSetPreviousPhaseLane(SlotClass.LsuClass, 5);

            Assert.Equal(3, scheduler.TestGetPreviousPhaseLane(SlotClass.AluClass));
            Assert.Equal(5, scheduler.TestGetPreviousPhaseLane(SlotClass.LsuClass));
        }

        [Fact]
        public void PreviousPhaseLane_WhenReplayPhaseDeactivates_ThenResetsToMinusOne()
        {
            var scheduler = new MicroOpScheduler();

            // Activate replay phase
            var activePhase = new ReplayPhaseContext(
                isActive: true, epochId: 1, cachedPc: 0x1000, epochLength: 10,
                completedReplays: 0, validSlotCount: 8, stableDonorMask: 0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);
            scheduler.SetReplayPhaseContext(activePhase);

            // Set lane records
            scheduler.TestSetPreviousPhaseLane(SlotClass.AluClass, 2);
            scheduler.TestSetPreviousPhaseLane(SlotClass.LsuClass, 4);

            // Deactivate replay phase
            var inactivePhase = new ReplayPhaseContext(
                isActive: false, epochId: 0, cachedPc: 0, epochLength: 0,
                completedReplays: 0, validSlotCount: 0, stableDonorMask: 0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.InactivePhase);
            scheduler.SetReplayPhaseContext(inactivePhase);

            Assert.Equal(-1, scheduler.TestGetPreviousPhaseLane(SlotClass.AluClass));
            Assert.Equal(-1, scheduler.TestGetPreviousPhaseLane(SlotClass.LsuClass));
        }

        [Fact]
        public void PreviousPhaseLane_WhenEpochChanges_ThenResetsToMinusOne()
        {
            var scheduler = new MicroOpScheduler();

            // Activate replay phase epoch 1
            var epoch1 = new ReplayPhaseContext(
                isActive: true, epochId: 1, cachedPc: 0x1000, epochLength: 10,
                completedReplays: 0, validSlotCount: 8, stableDonorMask: 0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);
            scheduler.SetReplayPhaseContext(epoch1);

            scheduler.TestSetPreviousPhaseLane(SlotClass.AluClass, 3);

            // Transition to epoch 2
            var epoch2 = new ReplayPhaseContext(
                isActive: true, epochId: 2, cachedPc: 0x2000, epochLength: 10,
                completedReplays: 0, validSlotCount: 8, stableDonorMask: 0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);
            scheduler.SetReplayPhaseContext(epoch2);

            Assert.Equal(-1, scheduler.TestGetPreviousPhaseLane(SlotClass.AluClass));
        }

        #endregion

        #region Telemetry

        [Fact]
        public void LaneReuseHits_WhenTier2Fires_ThenIncrements()
        {
            var scheduler = new MicroOpScheduler();

            var replayPhase = new ReplayPhaseContext(
                isActive: true, epochId: 1, cachedPc: 0x1000, epochLength: 10,
                completedReplays: 0, validSlotCount: 8, stableDonorMask: 0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);
            scheduler.SetReplayPhaseContext(replayPhase);

            // Seed previous lane for AluClass = lane 2
            scheduler.TestSetPreviousPhaseLane(SlotClass.AluClass, 2);

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            byte occupancy = 0x00;

            scheduler.TestTryMaterializeLane(candidate, occupancy, out int lane, out _);

            Assert.Equal(2, lane);
            Assert.Equal(1, scheduler.LaneReuseHits);
            Assert.Equal(0, scheduler.LaneReuseMisses);
        }

        [Fact]
        public void LaneReuseMisses_WhenTier2AvailableButPreviousLaneOccupied_ThenIncrements()
        {
            var scheduler = new MicroOpScheduler();

            var replayPhase = new ReplayPhaseContext(
                isActive: true, epochId: 1, cachedPc: 0x1000, epochLength: 10,
                completedReplays: 0, validSlotCount: 8, stableDonorMask: 0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);
            scheduler.SetReplayPhaseContext(replayPhase);

            // Seed previous lane for AluClass = lane 2, but lane 2 is occupied
            scheduler.TestSetPreviousPhaseLane(SlotClass.AluClass, 2);

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            byte occupancy = 0b_0000_0100; // Lane 2 occupied

            scheduler.TestTryMaterializeLane(candidate, occupancy, out int lane, out _);

            Assert.Equal(0, lane); // Fallback to lowest
            Assert.Equal(0, scheduler.LaneReuseHits);
            Assert.Equal(1, scheduler.LaneReuseMisses);
        }

        [Fact]
        public void LaneReuseCounters_WhenReplayNotActive_ThenNotIncremented()
        {
            var scheduler = new MicroOpScheduler();

            // No replay phase set (default: inactive)
            scheduler.TestSetPreviousPhaseLane(SlotClass.AluClass, 2);

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            byte occupancy = 0x00;

            scheduler.TestTryMaterializeLane(candidate, occupancy, out _, out _);

            Assert.Equal(0, scheduler.LaneReuseHits);
            Assert.Equal(0, scheduler.LaneReuseMisses);
        }

        #endregion

        #region Determinism

        [Fact]
        public void SelectWithReplayHint_GivenSameInput1000Times_AlwaysSameOutput()
        {
            byte freeLanes = 0b0000_1010;
            bool replayActive = true;
            int previousLane = 3;

            int firstResult = DeterministicLaneChooser.SelectWithReplayHint(freeLanes, replayActive, previousLane);

            for (int i = 1; i < 1000; i++)
            {
                int result = DeterministicLaneChooser.SelectWithReplayHint(freeLanes, replayActive, previousLane);
                Assert.Equal(firstResult, result);
            }
        }

        [Fact]
        public void TryMaterializeLane_GivenIdenticalInputs_Deterministic()
        {
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            byte occupancy = 0b_0000_0011;

            int firstLane = -1;
            for (int trial = 0; trial < 100; trial++)
            {
                var scheduler = new MicroOpScheduler();

                var replayPhase = new ReplayPhaseContext(
                    isActive: true, epochId: 1, cachedPc: 0x1000, epochLength: 10,
                    completedReplays: 0, validSlotCount: 8, stableDonorMask: 0,
                    lastInvalidationReason: ReplayPhaseInvalidationReason.None);
                scheduler.SetReplayPhaseContext(replayPhase);

                scheduler.TestSetPreviousPhaseLane(SlotClass.AluClass, 3);

                bool ok = scheduler.TestTryMaterializeLane(candidate, occupancy, out int lane, out _);
                Assert.True(ok);

                if (trial == 0)
                    firstLane = lane;
                else
                    Assert.Equal(firstLane, lane);
            }
        }

        [Fact]
        public void TwoIndependentSchedulers_SameInputs_SameLaneAssignment()
        {
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            byte occupancy = 0b_0000_0001;

            var scheduler1 = new MicroOpScheduler();
            var scheduler2 = new MicroOpScheduler();

            var replayPhase = new ReplayPhaseContext(
                isActive: true, epochId: 5, cachedPc: 0x2000, epochLength: 20,
                completedReplays: 1, validSlotCount: 8, stableDonorMask: 0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);
            scheduler1.SetReplayPhaseContext(replayPhase);
            scheduler2.SetReplayPhaseContext(replayPhase);

            scheduler1.TestSetPreviousPhaseLane(SlotClass.AluClass, 2);
            scheduler2.TestSetPreviousPhaseLane(SlotClass.AluClass, 2);

            scheduler1.TestTryMaterializeLane(candidate, occupancy, out int lane1, out _);
            scheduler2.TestTryMaterializeLane(candidate, occupancy, out int lane2, out _);

            Assert.Equal(lane1, lane2);
        }

        #endregion

        #region Regression — Phase 03 Baseline Behavior

        [Fact]
        public void TryMaterializeLane_WhenReplayInactive_ThenBehavesLikePhase03Baseline()
        {
            var scheduler = new MicroOpScheduler();
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            // ALU lanes: 0-3. Lanes 0, 1 occupied → lowest free = 2
            byte occupancy = 0b_0000_0011;

            bool result = scheduler.TestTryMaterializeLane(
                candidate, occupancy, out int lane, out var reason);

            Assert.True(result);
            Assert.Equal(2, lane); // Same as Phase 03 TrailingZeroCount behavior
            Assert.Equal(TypedSlotRejectReason.None, reason);
        }

        [Fact]
        public void TryMaterializeLane_LsuClass_WhenReplayInactive_ThenBehavesLikePhase03Baseline()
        {
            var scheduler = new MicroOpScheduler();
            var candidate = MicroOpTestHelper.CreateLoad(1, destReg: 20, address: 0x1000);
            byte occupancy = 0x00;

            bool result = scheduler.TestTryMaterializeLane(
                candidate, occupancy, out int lane, out var reason);

            Assert.True(result);
            Assert.Equal(4, lane); // Lowest LSU lane (same as Phase 03)
            Assert.Equal(TypedSlotRejectReason.None, reason);
        }

        #endregion
    }
}
