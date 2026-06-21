using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Phase 02 – Class-Capacity Vacancy Model tests.
    /// Validates SlotClassCapacityState struct, ComputeFromBundle helper,
    /// aliased-lane capacity invariants, and scheduler integration.
    /// </summary>
    public class SlotClassCapacityTests
    {
        #region InitializeFromLaneMap

        [Fact]
        public void InitializeFromLaneMap_SetsTotalsFromSlotClassLaneMap()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();

            Assert.Equal(SlotClassLaneMap.GetClassCapacity(SlotClass.AluClass), state.AluTotal);
            Assert.Equal(SlotClassLaneMap.GetClassCapacity(SlotClass.LsuClass), state.LsuTotal);
            Assert.Equal(SlotClassLaneMap.GetClassCapacity(SlotClass.DmaStreamClass), state.DmaStreamTotal);
            Assert.Equal(SlotClassLaneMap.GetClassCapacity(SlotClass.BranchControl), state.BranchControlTotal);
            Assert.Equal(SlotClassLaneMap.GetClassCapacity(SlotClass.SystemSingleton), state.SystemSingletonTotal);
        }

        [Fact]
        public void InitializeFromLaneMap_ResetsOccupancyToZero()
        {
            var state = new SlotClassCapacityState();
            state.AluOccupied = 3;
            state.InitializeFromLaneMap();

            Assert.Equal(0, state.AluOccupied);
            Assert.Equal(0, state.LsuOccupied);
            Assert.Equal(0, state.DmaStreamOccupied);
            Assert.Equal(0, state.BranchControlOccupied);
            Assert.Equal(0, state.SystemSingletonOccupied);
        }

        #endregion

        #region ResetOccupancy

        [Fact]
        public void ResetOccupancy_ClearsAllOccupiedButPreservesTotals()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            state.AluOccupied = 2;
            state.LsuOccupied = 1;

            state.ResetOccupancy();

            Assert.Equal(0, state.AluOccupied);
            Assert.Equal(0, state.LsuOccupied);
            Assert.Equal(4, state.AluTotal);
            Assert.Equal(2, state.LsuTotal);
        }

        #endregion

        #region ComputeFromBundle

        [Fact]
        public void ComputeFromBundle_EmptyBundle_AllOccupiedZero()
        {
            var bundle = new MicroOp?[8];

            var state = SlotClassCapacity.ComputeFromBundle(bundle);

            Assert.Equal(0, state.AluOccupied);
            Assert.Equal(0, state.LsuOccupied);
            Assert.Equal(0, state.DmaStreamOccupied);
            Assert.Equal(0, state.BranchControlOccupied);
            Assert.Equal(0, state.SystemSingletonOccupied);
        }

        [Fact]
        public void ComputeFromBundle_AllAluOps_AluOccupiedMatchesCount()
        {
            var bundle = new MicroOp?[8];
            for (int i = 0; i < 4; i++)
                bundle[i] = new ScalarALUMicroOp();

            var state = SlotClassCapacity.ComputeFromBundle(bundle);

            Assert.Equal(4, state.AluOccupied);
            Assert.Equal(state.AluTotal, state.AluOccupied);
            Assert.False(state.HasFreeCapacity(SlotClass.AluClass));
        }

        [Fact]
        public void ComputeFromBundle_MixedBundle_CorrectPerClassCounts()
        {
            var bundle = new MicroOp?[8];
            bundle[0] = new ScalarALUMicroOp();   // ALU
            bundle[1] = new ScalarALUMicroOp();   // ALU
            bundle[2] = new LoadMicroOp();         // LSU
            bundle[3] = new StoreMicroOp();        // LSU
            bundle[4] = new BranchMicroOp();  // BranchControl
            // slots 5-7 empty

            var state = SlotClassCapacity.ComputeFromBundle(bundle);

            Assert.Equal(2, state.AluOccupied);
            Assert.Equal(2, state.LsuOccupied);
            Assert.Equal(1, state.BranchControlOccupied);
            Assert.Equal(0, state.DmaStreamOccupied);
            Assert.Equal(0, state.SystemSingletonOccupied);
        }

        #endregion

        #region GetFreeCapacity

        [Theory]
        [InlineData(SlotClass.AluClass, 4)]
        [InlineData(SlotClass.LsuClass, 2)]
        [InlineData(SlotClass.DmaStreamClass, 1)]
        [InlineData(SlotClass.BranchControl, 1)]
        [InlineData(SlotClass.SystemSingleton, 1)]
        public void GetFreeCapacity_EmptyBundle_EqualsTotal(SlotClass sc, int expectedTotal)
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();

            Assert.Equal(expectedTotal, state.GetFreeCapacity(sc));
        }

        [Fact]
        public void GetFreeCapacity_Unclassified_AlwaysReturnsZero()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();

            Assert.Equal(0, state.GetFreeCapacity(SlotClass.Unclassified));
        }

        #endregion

        #region HasFreeCapacity boundary

        [Fact]
        public void HasFreeCapacity_EmptyBundle_TrueForAllNonUnclassified()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();

            Assert.True(state.HasFreeCapacity(SlotClass.AluClass));
            Assert.True(state.HasFreeCapacity(SlotClass.LsuClass));
            Assert.True(state.HasFreeCapacity(SlotClass.DmaStreamClass));
            Assert.True(state.HasFreeCapacity(SlotClass.BranchControl));
            Assert.True(state.HasFreeCapacity(SlotClass.SystemSingleton));
        }

        [Fact]
        public void HasFreeCapacity_FullAlu_ReturnsFalse()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            state.AluOccupied = state.AluTotal;

            Assert.False(state.HasFreeCapacity(SlotClass.AluClass));
        }

        [Fact]
        public void HasFreeCapacity_PartialAlu_ReturnsTrue()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            state.AluOccupied = (byte)(state.AluTotal - 1);

            Assert.True(state.HasFreeCapacity(SlotClass.AluClass));
        }

        #endregion

        #region Aliased-class capacity

        [Fact]
        public void HasFreeCapacity_BranchOccupied_SystemSingletonShowsNoFreeCapacity()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            state.BranchControlOccupied = 1;

            Assert.False(state.HasFreeCapacity(SlotClass.SystemSingleton));
        }

        [Fact]
        public void HasFreeCapacity_SystemSingletonOccupied_BranchControlShowsNoFreeCapacity()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            state.SystemSingletonOccupied = 1;

            Assert.False(state.HasFreeCapacity(SlotClass.BranchControl));
        }

        [Fact]
        public void HasFreeCapacity_NeitherAliasOccupied_BothShowFreeCapacity()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();

            Assert.True(state.HasFreeCapacity(SlotClass.BranchControl));
            Assert.True(state.HasFreeCapacity(SlotClass.SystemSingleton));
        }

        [Fact]
        public void HasFreeCapacity_BranchOccupied_AluUnaffected()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            state.BranchControlOccupied = 1;

            Assert.True(state.HasFreeCapacity(SlotClass.AluClass));
        }

        #endregion

        #region IncrementOccupancy

        [Fact]
        public void IncrementOccupancy_IncreasesCorrectCounter()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();

            state.IncrementOccupancy(SlotClass.AluClass);
            state.IncrementOccupancy(SlotClass.AluClass);
            state.IncrementOccupancy(SlotClass.LsuClass);

            Assert.Equal(2, state.AluOccupied);
            Assert.Equal(1, state.LsuOccupied);
            Assert.Equal(0, state.DmaStreamOccupied);
        }

        [Fact]
        public void IncrementOccupancy_Unclassified_DoesNotChangeAnyCounter()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();

            state.IncrementOccupancy(SlotClass.Unclassified);

            Assert.Equal(0, state.AluOccupied);
            Assert.Equal(0, state.LsuOccupied);
            Assert.Equal(0, state.DmaStreamOccupied);
            Assert.Equal(0, state.BranchControlOccupied);
            Assert.Equal(0, state.SystemSingletonOccupied);
        }

        #endregion

        #region Occupancy sum invariant

        [Fact]
        public void AfterNPlacements_SumOccupiedEqualsN()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();

            state.IncrementOccupancy(SlotClass.AluClass);
            state.IncrementOccupancy(SlotClass.LsuClass);
            state.IncrementOccupancy(SlotClass.BranchControl);

            int sum = state.AluOccupied + state.LsuOccupied + state.DmaStreamOccupied
                    + state.BranchControlOccupied + state.SystemSingletonOccupied;
            Assert.Equal(3, sum);
        }

        #endregion

        #region Scheduler integration

        [Fact]
        public void MicroOpScheduler_HasClassCapacityFor_ReflectsCapacityState()
        {
            var scheduler = new MicroOpScheduler();

            // Fresh scheduler: all classes should have capacity
            var aluOp = new ScalarALUMicroOp();
            Assert.True(scheduler.HasClassCapacityFor(aluOp));

            var lsuOp = new LoadMicroOp();
            Assert.True(scheduler.HasClassCapacityFor(lsuOp));
        }

        [Fact]
        public void MicroOpScheduler_GetClassCapacitySnapshot_ReturnsInitializedState()
        {
            var scheduler = new MicroOpScheduler();
            var snapshot = scheduler.GetClassCapacitySnapshot();

            Assert.Equal(4, snapshot.AluTotal);
            Assert.Equal(2, snapshot.LsuTotal);
            Assert.Equal(1, snapshot.DmaStreamTotal);
            Assert.Equal(0, snapshot.AluOccupied);
        }

        [Fact]
        public void MicroOpScheduler_PackBundle_ComputesClassCapacity()
        {
            var scheduler = new MicroOpScheduler();

            var bundle = new MicroOp[8];
            bundle[0] = new ScalarALUMicroOp();
            bundle[1] = new ScalarALUMicroOp();
            var loadOp = new LoadMicroOp();
            loadOp.InitializeMetadata();
            bundle[2] = loadOp;
            // slots 3-7 are null/NOP

            // Pack with steal enabled (stealMask=0 so no actual stealing occurs)
            // but class-capacity computation still runs
            scheduler.PackBundle(bundle, 0, true, 0x00);

            var snapshot = scheduler.GetClassCapacitySnapshot();
            Assert.Equal(2, snapshot.AluOccupied);
            Assert.Equal(1, snapshot.LsuOccupied);
        }

        [Fact]
        public void MicroOpScheduler_PackBundleIntraCoreSmt_ComputesClassCapacity()
        {
            var scheduler = new MicroOpScheduler();

            var bundle = new MicroOp[8];
            bundle[0] = new ScalarALUMicroOp();
            var branchOp = new BranchMicroOp();
            branchOp.InitializeMetadata();
            bundle[1] = branchOp;
            // slots 2-7 empty

            scheduler.PackBundleIntraCoreSmt(bundle, 0, 0);

            var snapshot = scheduler.GetClassCapacitySnapshot();
            Assert.Equal(1, snapshot.AluOccupied);
            Assert.Equal(1, snapshot.BranchControlOccupied);
        }

        #endregion

        #region ComputeFromBundle with NopMicroOp

        [Fact]
        public void ComputeFromBundle_NopOps_CountedAsUnclassified()
        {
            var bundle = new MicroOp?[8];
            bundle[0] = new NopMicroOp();
            bundle[1] = new NopMicroOp();

            var state = SlotClassCapacity.ComputeFromBundle(bundle);

            // NopMicroOp has RequiredSlotClass = Unclassified → not counted in any typed class
            Assert.Equal(0, state.AluOccupied);
            Assert.Equal(0, state.LsuOccupied);
        }

        #endregion
    }
}
