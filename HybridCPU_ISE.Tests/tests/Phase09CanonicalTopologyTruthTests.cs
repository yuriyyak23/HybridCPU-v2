using System;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.tests
{
    public sealed class Phase09CanonicalTopologyTruthTests
    {
        [Fact]
        public void T9_08d_CanonicalTypedTopology_UsesFixedW8LaneMap()
        {
            Assert.Equal(8, BundleMetadata.BundleSlotCount);
            Assert.Equal((byte)0b_0000_1111, SlotClassLaneMap.GetLaneMask(SlotClass.AluClass));
            Assert.Equal((byte)0b_0011_0000, SlotClassLaneMap.GetLaneMask(SlotClass.LsuClass));
            Assert.Equal((byte)0b_0100_0000, SlotClassLaneMap.GetLaneMask(SlotClass.DmaStreamClass));
            Assert.Equal((byte)0b_1000_0000, SlotClassLaneMap.GetLaneMask(SlotClass.BranchControl));
            Assert.Equal((byte)0b_1000_0000, SlotClassLaneMap.GetLaneMask(SlotClass.SystemSingleton));
        }

        [Fact]
        public void T9_08e_CanonicalTypedTopology_AdvertisesExpectedClassCapacities()
        {
            Assert.Equal(4, SlotClassLaneMap.GetClassCapacity(SlotClass.AluClass));
            Assert.Equal(2, SlotClassLaneMap.GetClassCapacity(SlotClass.LsuClass));
            Assert.Equal(1, SlotClassLaneMap.GetClassCapacity(SlotClass.DmaStreamClass));
            Assert.Equal(1, SlotClassLaneMap.GetClassCapacity(SlotClass.BranchControl));
            Assert.Equal(1, SlotClassLaneMap.GetClassCapacity(SlotClass.SystemSingleton));
        }

        [Fact]
        public void T9_08f_CanonicalTypedTopology_KeepsLane7AliasExplicit()
        {
            SlotClass[] branchAliases = SlotClassLaneMap.GetAliasedClasses(SlotClass.BranchControl).ToArray();
            SlotClass[] systemAliases = SlotClassLaneMap.GetAliasedClasses(SlotClass.SystemSingleton).ToArray();

            Assert.True(SlotClassLaneMap.HasAliasedLanes(SlotClass.BranchControl));
            Assert.True(SlotClassLaneMap.HasAliasedLanes(SlotClass.SystemSingleton));
            Assert.Equal(new[] { SlotClass.SystemSingleton }, branchAliases);
            Assert.Equal(new[] { SlotClass.BranchControl }, systemAliases);
        }
    }
}
