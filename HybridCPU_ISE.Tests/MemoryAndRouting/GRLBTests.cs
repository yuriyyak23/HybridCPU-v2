using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Unit tests for Global Resource Lock Bitset (GRLB) - Phase 8.
    /// Tests verify that resource acquisition and release work correctly,
    /// and that resource conflicts are properly detected and handled.
    /// </summary>
    public class GRLBTests
    {
        #region Helper Methods

        /// <summary>
        /// Create a test CPU core instance
        /// </summary>
        private Processor.CPU_Core CreateTestCore()
        {
            return new Processor.CPU_Core(0);
        }

        /// <summary>
        /// Create a scalar ALU micro-operation with specified registers
        /// </summary>
        private ScalarALUMicroOp CreateALUOp(ushort dest, ushort src1, ushort src2)
        {
            var op = new ScalarALUMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Addition,
                DestRegID = dest,
                Src1RegID = src1,
                Src2RegID = src2,
                WritesRegister = true,
                UsesImmediate = false
            };
            op.InitializeMetadata();
            return op;
        }

        /// <summary>
        /// Create a load micro-operation
        /// </summary>
        private LoadMicroOp CreateLoadOp(ushort dest, ushort baseReg, ulong address)
        {
            var op = new LoadMicroOp
            {
                DestRegID = dest,
                BaseRegID = baseReg,
                Address = address,
                Size = 8,
                WritesRegister = true,
                OwnerThreadId = 0
            };
            op.InitializeMetadata();
            return op;
        }

        /// <summary>
        /// Create a store micro-operation
        /// </summary>
        private StoreMicroOp CreateStoreOp(ushort src, ushort baseReg, ulong address)
        {
            var op = new StoreMicroOp
            {
                SrcRegID = src,
                BaseRegID = baseReg,
                Address = address,
                Size = 8,
                OwnerThreadId = 0
            };
            op.InitializeMetadata();
            return op;
        }

        #endregion

        #region ResourceMaskBuilder Tests

        [Fact]
        public void ResourceMaskBuilder_ForRegisterRead_ShouldSetCorrectBit()
        {
            // Test various register IDs
            ulong mask0 = (ulong)ResourceMaskBuilder.ForRegisterRead(0);  // Group 0
            ulong mask4 = (ulong)ResourceMaskBuilder.ForRegisterRead(4);  // Group 1
            ulong mask8 = (ulong)ResourceMaskBuilder.ForRegisterRead(8);  // Group 2

            Assert.Equal(1UL << 0, mask0);  // Bit 0
            Assert.Equal(1UL << 1, mask4);  // Bit 1
            Assert.Equal(1UL << 2, mask8);  // Bit 2
        }

        [Fact]
        public void ResourceMaskBuilder_ForRegisterWrite_ShouldSetCorrectBit()
        {
            // Test various register IDs
            ulong mask0 = (ulong)ResourceMaskBuilder.ForRegisterWrite(0);  // Group 0
            ulong mask4 = (ulong)ResourceMaskBuilder.ForRegisterWrite(4);  // Group 1

            Assert.Equal(1UL << 16, mask0);  // Bit 16
            Assert.Equal(1UL << 17, mask4);  // Bit 17
        }

        [Fact]
        public void ResourceMaskBuilder_ForLoad_ShouldSetBit48()
        {
            ulong mask = (ulong)ResourceMaskBuilder.ForLoad();
            Assert.Equal(1UL << 48, mask);
        }

        [Fact]
        public void ResourceMaskBuilder_ForStore_ShouldSetBit49()
        {
            ulong mask = (ulong)ResourceMaskBuilder.ForStore();
            Assert.Equal(1UL << 49, mask);
        }

        [Fact]
        public void ResourceMaskBuilder_ForMemoryDomain_ShouldSetCorrectBit()
        {
            ulong mask0 = (ulong)ResourceMaskBuilder.ForMemoryDomain(0);
            ulong mask1 = (ulong)ResourceMaskBuilder.ForMemoryDomain(1);

            Assert.Equal(1UL << 32, mask0);  // Bit 32
            Assert.Equal(1UL << 33, mask1);  // Bit 33
        }

        [Fact]
        public void ResourceMaskBuilder_ForStreamEngine_ShouldSetCorrectBit()
        {
            ulong mask0 = (ulong)ResourceMaskBuilder.ForStreamEngine(0);
            ulong mask1 = (ulong)ResourceMaskBuilder.ForStreamEngine(1);

            Assert.Equal(1UL << 55, mask0);  // Bit 55
            Assert.Equal(1UL << 56, mask1);  // Bit 56
        }

        #endregion

        #region MicroOp ResourceMask Initialization Tests

        [Fact]
        public void ScalarALUMicroOp_InitializeMetadata_ShouldSetResourceMask()
        {
            var op = CreateALUOp(dest: 8, src1: 4, src2: 12);

            // Should have:
            // - Register read bits for R4 (group 1) and R12 (group 3)
            // - Register write bit for R8 (group 2)
            ulong expectedMask = (ulong)(ResourceMaskBuilder.ForRegisterRead(4) |
                                 ResourceMaskBuilder.ForRegisterRead(12) |
                                 ResourceMaskBuilder.ForRegisterWrite(8));

            Assert.Equal(expectedMask, (ulong)op.ResourceMask);
        }

        [Fact]
        public void LoadMicroOp_InitializeMetadata_ShouldSetResourceMask()
        {
            var op = CreateLoadOp(dest: 8, baseReg: 4, address: 0x1000);

            // Should have:
            // - Register read bit for base register R4
            // - Register write bit for dest register R8
            // - Load channel bit (48)
            // - Memory domain bit (32 for domain 0)
            ulong expectedMask = (ulong)(ResourceMaskBuilder.ForRegisterRead(4) |
                                 ResourceMaskBuilder.ForRegisterWrite(8) |
                                 ResourceMaskBuilder.ForLoad() |
                                 ResourceMaskBuilder.ForMemoryDomain(0));

            Assert.Equal(expectedMask, (ulong)op.ResourceMask);
        }

        [Fact]
        public void StoreMicroOp_InitializeMetadata_ShouldSetResourceMask()
        {
            var op = CreateStoreOp(src: 4, baseReg: 8, address: 0x2000);

            // Should have:
            // - Register read bits for src (R4) and base (R8)
            // - Store channel bit (49)
            // - Memory domain bit (32 for domain 0)
            ulong expectedMask = (ulong)(ResourceMaskBuilder.ForRegisterRead(4) |
                                 ResourceMaskBuilder.ForRegisterRead(8) |
                                 ResourceMaskBuilder.ForStore() |
                                 ResourceMaskBuilder.ForMemoryDomain(0));

            Assert.Equal(expectedMask, (ulong)op.ResourceMask);
        }

        #endregion

        #region CPU_Core AcquireResources / ReleaseResources Tests

        [Fact]
        public void AcquireResources_WithNoConflict_ShouldSucceed()
        {
            var core = CreateTestCore();
            ulong mask = 0x1; // Bit 0

            bool result = core.AcquireResources(mask);

            Assert.True(result);
            Assert.Equal(mask, core.GetGlobalResourceLocks());
        }

        [Fact]
        public void AcquireResources_WithConflict_ShouldFail()
        {
            var core = CreateTestCore();
            ulong mask1 = 3UL << 16; // Bits 16 and 17 (Register Writes)
            ulong mask2 = 2UL << 16; // Bit 17 (conflicts with mask1)

            // First acquisition should succeed
            bool result1 = core.AcquireResources(mask1);
            Assert.True(result1);

            // Second acquisition should fail (conflict on bit 17)
            bool result2 = core.AcquireResources(mask2);
            Assert.False(result2);

            // Global locks should still only have mask1
            Assert.Equal(mask1, core.GetGlobalResourceLocks());
        }

        [Fact]
        public void AcquireResources_NonOverlapping_ShouldBothSucceed()
        {
            var core = CreateTestCore();
            ulong mask1 = 0x1; // Bit 0
            ulong mask2 = 0x2; // Bit 1 (no overlap)

            bool result1 = core.AcquireResources(mask1);
            bool result2 = core.AcquireResources(mask2);

            Assert.True(result1);
            Assert.True(result2);
            Assert.Equal(mask1 | mask2, core.GetGlobalResourceLocks());
        }

        [Fact]
        public void ReleaseResources_ShouldClearBits()
        {
            var core = CreateTestCore();
            ulong mask1 = 0x3; // Bits 0 and 1
            ulong mask2 = 0x1; // Bit 0

            core.AcquireResources(mask1);
            Assert.Equal(mask1, core.GetGlobalResourceLocks());

            core.ReleaseResources(mask2);
            Assert.Equal(0x2UL, core.GetGlobalResourceLocks()); // Only bit 1 remains
        }

        [Fact]
        public void ReleaseResources_Idempotent_ShouldNotCauseIssues()
        {
            var core = CreateTestCore();
            ulong mask = 0x1;

            core.AcquireResources(mask);
            core.ReleaseResources(mask);
            core.ReleaseResources(mask); // Release again

            Assert.Equal(0UL, core.GetGlobalResourceLocks());
        }

        [Fact]
        public void ClearAllResourceLocks_ShouldResetToZero()
        {
            var core = CreateTestCore();
            core.AcquireResources(0xFFFF);

            core.ClearAllResourceLocks();

            Assert.Equal(0UL, core.GetGlobalResourceLocks());
        }

        [Fact]
        public void AreResourcesLocked_ShouldDetectLockedResources()
        {
            var core = CreateTestCore();
            ulong mask = 0x5; // Bits 0 and 2

            core.AcquireResources(mask);

            Assert.True(core.AreResourcesLocked(0x1));  // Bit 0 is locked
            Assert.False(core.AreResourcesLocked(0x2)); // Bit 1 is not locked
            Assert.True(core.AreResourcesLocked(0x4));  // Bit 2 is locked
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void TwoALUOps_WithSameDestReg_ShouldConflict()
        {
            var core = CreateTestCore();
            var op1 = CreateALUOp(dest: 8, src1: 0, src2: 4);  // Writes to R8
            var op2 = CreateALUOp(dest: 8, src1: 12, src2: 16); // Also writes to R8

            bool result1 = core.AcquireResources(op1.ResourceMask);
            bool result2 = core.AcquireResources(op2.ResourceMask);

            Assert.True(result1);
            Assert.False(result2); // Should fail due to write conflict on R8
        }

        [Fact]
        public void TwoALUOps_WithDifferentRegs_ShouldNotConflict()
        {
            var core = CreateTestCore();
            var op1 = CreateALUOp(dest: 8, src1: 0, src2: 4);   // R8 = R0 + R4
            var op2 = CreateALUOp(dest: 12, src1: 16, src2: 20); // R12 = R16 + R20

            bool result1 = core.AcquireResources(op1.ResourceMask);
            bool result2 = core.AcquireResources(op2.ResourceMask);

            Assert.True(result1);
            Assert.True(result2); // Should succeed - no register conflicts
        }

        [Fact]
        public void LoadAndStore_SameDomain_ShouldConflictOnMemoryDomain()
        {
            var core = CreateTestCore();
            var load = CreateLoadOp(dest: 8, baseReg: 4, address: 0x1000);
            var store = CreateStoreOp(src: 12, baseReg: 16, address: 0x2000);

            bool result1 = core.AcquireResources(load.ResourceMask);

            // Store should conflict because both use memory domain 0 (bit 32)
            // Even though they use different LSU channels (load: bit 48, store: bit 49),
            // they share the same memory domain resource
            bool result2 = core.AcquireResources(store.ResourceMask);

            Assert.True(result1);
            Assert.False(result2); // Should fail due to memory domain conflict
        }

        [Fact]
        public void TwoLoads_ShouldConflictOnLoadChannel()
        {
            var core = CreateTestCore();
            var load1 = CreateLoadOp(dest: 8, baseReg: 4, address: 0x1000);
            var load2 = CreateLoadOp(dest: 12, baseReg: 16, address: 0x2000);

            bool result1 = core.AcquireResources(load1.ResourceMask);
            bool result2 = core.AcquireResources(load2.ResourceMask);

            Assert.True(result1);
            Assert.False(result2); // Should fail - both need load channel (bit 48)
        }

        [Fact]
        public void StructuralStalls_ShouldIncrement()
        {
            var core = CreateTestCore();
            ulong initialStalls = core.StructuralStalls;

            core.IncrementStructuralStalls();
            core.IncrementStructuralStalls();

            Assert.Equal(initialStalls + 2, core.StructuralStalls);
        }

        #endregion

        #region Token-Based Resource Tracking Tests

        [Fact]
        public void AcquireResourcesWithToken_ShouldReturnToken()
        {
            var core = CreateTestCore();
            ulong mask = 0x1; // Bit 0

            bool result = core.AcquireResourcesWithToken(mask, out ulong token);

            Assert.True(result);
            Assert.NotEqual(0UL, token);
            Assert.Equal(mask, core.GetGlobalResourceLocks());
        }

        [Fact]
        public void AcquireResourcesWithToken_WithConflict_ShouldReturnZeroToken()
        {
            var core = CreateTestCore();
            ulong mask1 = 3UL << 16; // Bits 16 and 17
            ulong mask2 = 2UL << 16; // Bit 17 (conflicts with mask1)

            core.AcquireResourcesWithToken(mask1, out ulong token1);
            bool result = core.AcquireResourcesWithToken(mask2, out ulong token2);

            Assert.False(result);
            Assert.Equal(0UL, token2);
        }

        [Fact]
        public void ReleaseResourcesWithToken_WithValidToken_ShouldRelease()
        {
            var core = CreateTestCore();
            ulong mask = 0x3; // Bits 0 and 1

            core.AcquireResourcesWithToken(mask, out ulong token);
            Assert.Equal(mask, core.GetGlobalResourceLocks());

            core.ReleaseResourcesWithToken(mask, token);
            Assert.Equal(0UL, core.GetGlobalResourceLocks());
        }

        [Fact]
        public void ReleaseResourcesWithToken_WithInvalidToken_ShouldNotRelease()
        {
            var core = CreateTestCore();
            ulong mask = 3UL << 16; // Bits 16 and 17

            core.AcquireResourcesWithToken(mask, out ulong token);
            Assert.Equal(mask, core.GetGlobalResourceLocks());

            // Try to release with wrong token
            core.ReleaseResourcesWithToken(mask, token + 1);
            Assert.Equal(mask, core.GetGlobalResourceLocks()); // Should still be locked
        }

        [Fact]
        public void TokenTracking_ShouldPreventABAProblem()
        {
            var core = CreateTestCore();
            ulong mask1 = 1UL << 16; // Bit 16
            ulong mask2 = 1UL << 16; // Bit 16 (same bit)

            // First operation acquires resource
            core.AcquireResourcesWithToken(mask1, out ulong token1);
            Assert.Equal(mask1, core.GetGlobalResourceLocks());

            // First operation releases resource
            core.ReleaseResourcesWithToken(mask1, token1);
            Assert.Equal(0UL, core.GetGlobalResourceLocks());

            // Second operation acquires same resource (gets new token)
            core.AcquireResourcesWithToken(mask2, out ulong token2);
            Assert.NotEqual(token1, token2); // Tokens should be different
            Assert.Equal(mask2, core.GetGlobalResourceLocks());

            // First operation tries to release again (with old token) - should fail
            core.ReleaseResourcesWithToken(mask1, token1);
            Assert.Equal(mask2, core.GetGlobalResourceLocks()); // Should still be locked by second operation

            // Second operation releases properly
            core.ReleaseResourcesWithToken(mask2, token2);
            Assert.Equal(0UL, core.GetGlobalResourceLocks());
        }

        [Fact]
        public void MultipleResources_DifferentTokens_ShouldReleaseIndependently()
        {
            var core = CreateTestCore();
            ulong mask1 = 0x1; // Bit 0
            ulong mask2 = 0x2; // Bit 1

            core.AcquireResourcesWithToken(mask1, out ulong token1);
            core.AcquireResourcesWithToken(mask2, out ulong token2);
            Assert.Equal(mask1 | mask2, core.GetGlobalResourceLocks());

            // Release first resource
            core.ReleaseResourcesWithToken(mask1, token1);
            Assert.Equal(mask2, core.GetGlobalResourceLocks());

            // Release second resource
            core.ReleaseResourcesWithToken(mask2, token2);
            Assert.Equal(0UL, core.GetGlobalResourceLocks());
        }

        #endregion

        #region Resource Usage Statistics Tests

        [Fact]
        public void GetResourceUsageCount_ShouldTrackAcquisitions()
        {
            var core = CreateTestCore();
            ulong mask = 0x1; // Bit 0

            Assert.Equal(0UL, core.GetResourceUsageCount(0));

            core.AcquireResources(mask);
            Assert.Equal(1UL, core.GetResourceUsageCount(0));

            core.ReleaseResources(mask);
            core.AcquireResources(mask);
            Assert.Equal(2UL, core.GetResourceUsageCount(0));
        }

        [Fact]
        public void GetResourceContentionCount_ShouldTrackConflicts()
        {
            var core = CreateTestCore();
            ulong mask = 1UL << 16; // Bit 16 (Register Write)

            core.AcquireResources(mask);
            Assert.Equal(0UL, core.GetResourceContentionCount(16));

            // Try to acquire again - should cause contention
            core.AcquireResources(mask);
            Assert.Equal(1UL, core.GetResourceContentionCount(16));

            // Try again
            core.AcquireResources(mask);
            Assert.Equal(2UL, core.GetResourceContentionCount(16));
        }

        [Fact]
        public void GetAllResourceUsageCounts_ShouldReturnArray()
        {
            var core = CreateTestCore();
            ulong mask1 = 0x1; // Bit 0
            ulong mask2 = 0x2; // Bit 1

            core.AcquireResources(mask1);
            core.AcquireResources(mask2);

            ulong[] counts = core.GetAllResourceUsageCounts();

            // GRLB now supports 128 resources (Phase: Safety Tags & Certificates)
            Assert.Equal(128, counts.Length);
            Assert.Equal(1UL, counts[0]);
            Assert.Equal(1UL, counts[1]);
            Assert.Equal(0UL, counts[2]);
        }

        [Fact]
        public void ResetGRLBCounters_ShouldClearAllStatistics()
        {
            var core = CreateTestCore();
            ulong mask = 1UL << 16; // Bit 16

            core.AcquireResources(mask);
            core.AcquireResources(mask); // Conflict
            core.IncrementStructuralStalls();

            Assert.True(core.GetResourceUsageCount(16) > 0);
            Assert.True(core.GetResourceContentionCount(16) > 0);
            Assert.True(core.StructuralStalls > 0);

            core.ResetGRLBCounters();

            Assert.Equal(0UL, core.GetResourceUsageCount(16));
            Assert.Equal(0UL, core.GetResourceContentionCount(16));
            Assert.Equal(0UL, core.StructuralStalls);
        }

        #endregion
    }
}
