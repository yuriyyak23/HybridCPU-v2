using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Phase 01 – SlotClass Taxonomy unit tests.
    /// Validates enums, lane-map queries, aliased-lane invariants,
    /// and correct SlotClass/PinningKind metadata on every MicroOp subclass.
    /// </summary>
    public class SlotClassTaxonomyTests
    {
        #region SlotClassLaneMap exhaustive coverage

        [Theory]
        [InlineData(SlotClass.AluClass)]
        [InlineData(SlotClass.LsuClass)]
        [InlineData(SlotClass.DmaStreamClass)]
        [InlineData(SlotClass.BranchControl)]
        [InlineData(SlotClass.SystemSingleton)]
        [InlineData(SlotClass.Unclassified)]
        public void GetLaneMask_ReturnsNonZeroForEveryDefinedSlotClass(SlotClass sc)
        {
            byte mask = SlotClassLaneMap.GetLaneMask(sc);
            Assert.NotEqual(0, mask);
        }

        #endregion

        #region GetClassCapacity expected values

        [Theory]
        [InlineData(SlotClass.AluClass, 4)]
        [InlineData(SlotClass.LsuClass, 2)]
        [InlineData(SlotClass.DmaStreamClass, 1)]
        [InlineData(SlotClass.BranchControl, 1)]
        [InlineData(SlotClass.SystemSingleton, 1)]
        public void GetClassCapacity_ReturnsExpectedCount(SlotClass sc, int expected)
        {
            Assert.Equal(expected, SlotClassLaneMap.GetClassCapacity(sc));
        }

        #endregion

        #region Non-aliased lane masks do not overlap

        [Fact]
        public void LaneMasks_AluLsuDma_DoNotOverlap()
        {
            byte alu = SlotClassLaneMap.GetLaneMask(SlotClass.AluClass);
            byte lsu = SlotClassLaneMap.GetLaneMask(SlotClass.LsuClass);
            byte dma = SlotClassLaneMap.GetLaneMask(SlotClass.DmaStreamClass);

            Assert.Equal(0, alu & lsu);
            Assert.Equal(0, alu & dma);
            Assert.Equal(0, lsu & dma);
        }

        #endregion

        #region Aliased lanes (BranchControl / SystemSingleton)

        [Fact]
        public void GetAliasedClasses_BranchControl_ContainsSystemSingleton()
        {
            var aliased = SlotClassLaneMap.GetAliasedClasses(SlotClass.BranchControl);
            Assert.Equal(1, aliased.Length);
            Assert.Equal(SlotClass.SystemSingleton, aliased[0]);
        }

        [Fact]
        public void GetAliasedClasses_SystemSingleton_ContainsBranchControl()
        {
            var aliased = SlotClassLaneMap.GetAliasedClasses(SlotClass.SystemSingleton);
            Assert.Equal(1, aliased.Length);
            Assert.Equal(SlotClass.BranchControl, aliased[0]);
        }

        [Theory]
        [InlineData(SlotClass.AluClass)]
        [InlineData(SlotClass.LsuClass)]
        [InlineData(SlotClass.DmaStreamClass)]
        [InlineData(SlotClass.Unclassified)]
        public void GetAliasedClasses_NonAliased_ReturnsEmpty(SlotClass sc)
        {
            Assert.True(SlotClassLaneMap.GetAliasedClasses(sc).IsEmpty);
        }

        [Fact]
        public void HasAliasedLanes_TrueForBranchAndSystem()
        {
            Assert.True(SlotClassLaneMap.HasAliasedLanes(SlotClass.BranchControl));
            Assert.True(SlotClassLaneMap.HasAliasedLanes(SlotClass.SystemSingleton));
        }

        [Theory]
        [InlineData(SlotClass.AluClass)]
        [InlineData(SlotClass.LsuClass)]
        [InlineData(SlotClass.DmaStreamClass)]
        [InlineData(SlotClass.Unclassified)]
        public void HasAliasedLanes_FalseForNonAliased(SlotClass sc)
        {
            Assert.False(SlotClassLaneMap.HasAliasedLanes(sc));
        }

        #endregion

        #region MicroOp subclass placement is set correctly

        [Fact]
        public void ScalarALUMicroOp_HasAluClassFlexible()
        {
            var op = new ScalarALUMicroOp();
            Assert.Equal(SlotClass.AluClass, op.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.ClassFlexible, op.Placement.PinningKind);
        }

        [Fact]
        public void VectorALUMicroOp_HasAluClassFlexible()
        {
            var op = new VectorALUMicroOp();
            Assert.Equal(SlotClass.AluClass, op.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.ClassFlexible, op.Placement.PinningKind);
        }

        [Fact]
        public void LoadMicroOp_HasLsuClassFlexible()
        {
            var op = new LoadMicroOp();
            Assert.Equal(SlotClass.LsuClass, op.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.ClassFlexible, op.Placement.PinningKind);
        }

        [Fact]
        public void StoreMicroOp_HasLsuClassFlexible()
        {
            var op = new StoreMicroOp();
            Assert.Equal(SlotClass.LsuClass, op.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.ClassFlexible, op.Placement.PinningKind);
        }

        [Fact]
        public void BranchMicroOp_HasBranchControlHardPinnedLane7()
        {
            var op = new BranchMicroOp();
            Assert.Equal(SlotClass.BranchControl, op.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, op.Placement.PinningKind);
            Assert.Equal(7, op.Placement.PinnedLaneId);
        }

        [Fact]
        public void CsrReadWriteMicroOp_HasSystemSingletonHardPinnedLane7()
        {
            var op = new CsrReadWriteMicroOp();
            Assert.Equal(SlotClass.SystemSingleton, op.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, op.Placement.PinningKind);
            Assert.Equal(7, op.Placement.PinnedLaneId);
        }

        [Fact]
        public void HaltMicroOp_HasSystemSingletonHardPinnedLane7()
        {
            var op = new HaltMicroOp();
            Assert.Equal(SlotClass.SystemSingleton, op.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, op.Placement.PinningKind);
            Assert.Equal(7, op.Placement.PinnedLaneId);
        }

        [Fact]
        public void PortIOMicroOp_HasSystemSingletonHardPinnedLane7()
        {
            var op = new PortIOMicroOp();
            Assert.Equal(SlotClass.SystemSingleton, op.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, op.Placement.PinningKind);
            Assert.Equal(7, op.Placement.PinnedLaneId);
        }

        [Fact]
        public void MoveMicroOp_HasAluClassFlexible()
        {
            var op = new MoveMicroOp();
            Assert.Equal(SlotClass.AluClass, op.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.ClassFlexible, op.Placement.PinningKind);
        }

        [Fact]
        public void IncrDecrMicroOp_HasAluClassFlexible()
        {
            var op = new IncrDecrMicroOp();
            Assert.Equal(SlotClass.AluClass, op.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.ClassFlexible, op.Placement.PinningKind);
        }

        #endregion

        #region Unclassified ops explicitly expected

        [Fact]
        public void NopMicroOp_HasUnclassifiedFlexible()
        {
            var op = new NopMicroOp();
            Assert.Equal(SlotClass.Unclassified, op.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.ClassFlexible, op.Placement.PinningKind);
        }

        [Fact]
        public void GenericMicroOp_HasUnclassifiedHardPinned()
        {
            var op = new GenericMicroOp();
            Assert.Equal(SlotClass.Unclassified, op.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, op.Placement.PinningKind);
        }

        [Fact]
        public void SysEventMicroOp_HasSystemSingletonHardPinned()
        {
            var op = new SysEventMicroOp { EventKind = SystemEventKind.Fence };
            Assert.Equal(SlotClass.SystemSingleton, op.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, op.Placement.PinningKind);
        }

        [Fact]
        public void CustomAcceleratorMicroOp_HasUnclassifiedHardPinned()
        {
            var op = new CustomAcceleratorMicroOp();
            Assert.Equal(SlotClass.Unclassified, op.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.HardPinned, op.Placement.PinningKind);
        }

        #endregion

        #region VectorMicroOp base class taxonomy

        [Fact]
        public void VectorBinaryOpMicroOp_InheritsAluClassFlexible()
        {
            var op = new VectorBinaryOpMicroOp();
            Assert.Equal(SlotClass.AluClass, op.Placement.RequiredSlotClass);
            Assert.Equal(SlotPinningKind.ClassFlexible, op.Placement.PinningKind);
        }

        #endregion

        #region HardPinned ops have PinnedLaneId inside GetLaneMask

        [Theory]
        [InlineData(typeof(BranchMicroOp))]
        [InlineData(typeof(CsrReadWriteMicroOp))]
        [InlineData(typeof(HaltMicroOp))]
        [InlineData(typeof(PortIOMicroOp))]
        public void HardPinnedOps_PinnedLaneIdIsInsideLaneMask(Type microOpType)
        {
            var op = (MicroOp)Activator.CreateInstance(microOpType)!;
            Assert.Equal(SlotPinningKind.HardPinned, op.Placement.PinningKind);

            byte mask = SlotClassLaneMap.GetLaneMask(op.Placement.RequiredSlotClass);
            bool laneInMask = (mask & (1 << op.Placement.PinnedLaneId)) != 0;
            Assert.True(laneInMask,
                $"{microOpType.Name}: PinnedLaneId={op.Placement.PinnedLaneId} is outside GetLaneMask(0x{mask:X2})");
        }

        #endregion

        #region ClassFlexible ops have capacity > 1

        [Theory]
        [InlineData(typeof(ScalarALUMicroOp))]
        [InlineData(typeof(VectorALUMicroOp))]
        [InlineData(typeof(LoadMicroOp))]
        [InlineData(typeof(StoreMicroOp))]
        [InlineData(typeof(MoveMicroOp))]
        [InlineData(typeof(IncrDecrMicroOp))]
        public void ClassFlexibleOps_HaveCapacityGreaterThanOne(Type microOpType)
        {
            var op = (MicroOp)Activator.CreateInstance(microOpType)!;
            Assert.Equal(SlotPinningKind.ClassFlexible, op.Placement.PinningKind);

            int capacity = SlotClassLaneMap.GetClassCapacity(op.Placement.RequiredSlotClass);
            Assert.True(capacity > 1,
                $"{microOpType.Name}: Capacity={capacity} for SlotClass={op.Placement.RequiredSlotClass}, expected > 1");
        }

        #endregion
    }
}
