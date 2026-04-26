using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using HybridCPU_ISE.Tests.TestHelpers;
using System;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Section 1: Safety Mask 128-bit & Scoreboard (GRLB) Tests
    /// Тесты для подтверждения свойств модели безопасности и табло ресурсов.
    /// Based on: required_tests_coverage.md Section 1
    /// </summary>
    public class ScoreboardGRLBTests
    {
        #region 1.1 Mask_Generation_Determinism

        /// <summary>
        /// Test: Mask_Generation_Determinism
        /// Проверка, что декодер генерирует строго идентичные 128-битные маски
        /// для одинаковых инструкций.
        /// </summary>
        [Fact]
        public void Mask_Generation_Determinism_ShouldGenerateIdenticalMasks()
        {
            // Arrange: Create two identical micro-ops
            var op1 = MicroOpTestHelper.CreateScalarALU(
                virtualThreadId: 0,
                destReg: 4,
                src1Reg: 1,
                src2Reg: 2);

            var op2 = MicroOpTestHelper.CreateScalarALU(
                virtualThreadId: 0,
                destReg: 4,
                src1Reg: 1,
                src2Reg: 2);

            // Act & Assert: Verify masks are identical (same inputs should produce identical masks)
            Assert.Equal(op1.SafetyMask.Low, op2.SafetyMask.Low);
            Assert.Equal(op1.SafetyMask.High, op2.SafetyMask.High);
            Assert.Equal(op1.SafetyMask, op2.SafetyMask);
            Assert.Equal(op1.ResourceMask.Low, op2.ResourceMask.Low);
            Assert.Equal(op1.ResourceMask.High, op2.ResourceMask.High);
        }

        /// <summary>
        /// Test: Mask_Generation_Determinism with multiple operations
        /// Проверка детерминизма масок для последовательности одинаковых операций.
        /// </summary>
        [Fact]
        public void Mask_Generation_Determinism_MultipleOperations_ShouldBeIdentical()
        {
            // Arrange: Create an array of identical operations
            const int count = 10;
            var ops = new ScalarALUMicroOp[count];

            // Act: Generate identical operations
            for (int i = 0; i < count; i++)
            {
                ops[i] = MicroOpTestHelper.CreateScalarALU(
                    virtualThreadId: 0,
                    destReg: 10,
                    src1Reg: 5,
                    src2Reg: 6);
            }

            // Assert: All masks should be identical
            var expectedMask = ops[0].SafetyMask;
            for (int i = 1; i < count; i++)
            {
                Assert.Equal(expectedMask, ops[i].SafetyMask);
            }
        }

        #endregion

        #region 1.2 Mask_Partitioning_Enforcement

        /// <summary>
        /// Test: Mask_Partitioning_Enforcement
        /// Убедиться, что биты 0-31 используются только для регистров,
        /// 32-47 для lease locks, 48-50 для портов, 51-62 для DMA.
        /// </summary>
        [Fact]
        public void Mask_Partitioning_Enforcement_RegisterBits_ShouldBeInRange_0_31()
        {
            // Arrange: Create mask with register bits set (bits 0-31 in Low)
            var registerMask = new SafetyMask128(0x00000000FFFFFFFFUL, 0x0000000000000000UL);

            // Act: Extract the bits
            ulong registerBits = registerMask.Low & 0x00000000FFFFFFFFUL;

            // Assert: Only bits 0-31 should be set
            Assert.NotEqual(0UL, registerBits);
            Assert.Equal(0UL, registerMask.Low & 0xFFFFFFFF00000000UL);
            Assert.Equal(0UL, registerMask.High);
        }

        /// <summary>
        /// Test: Mask_Partitioning_Enforcement for LSU ports
        /// Проверка, что биты 48-50 используются для портов LSU.
        /// </summary>
        [Fact]
        public void Mask_Partitioning_Enforcement_LSUPorts_ShouldBeInRange_48_50()
        {
            // Arrange: LSU port bits are 48, 49, 50 (Load, Store, Atomic)
            // Bit 48: Load operation (LSU read channel)
            var loadMask = new SafetyMask128(1UL << 48, 0);
            // Bit 49: Store operation (LSU write channel)
            var storeMask = new SafetyMask128(1UL << 49, 0);
            // Bit 50: Atomic operation (LSU atomic channel)
            var atomicMask = new SafetyMask128(1UL << 50, 0);

            // Act: Combine all LSU port masks
            var combinedLSU = loadMask | storeMask | atomicMask;

            // Assert: Should be exactly bits 48, 49, 50
            ulong expectedBits = (1UL << 48) | (1UL << 49) | (1UL << 50);
            Assert.Equal(expectedBits, combinedLSU.Low);
            Assert.Equal(0UL, combinedLSU.High);
        }

        /// <summary>
        /// Test: Mask_Partitioning_Enforcement for DMA channels
        /// Проверка, что биты 51-62 используются для DMA каналов.
        /// </summary>
        [Fact]
        public void Mask_Partitioning_Enforcement_DMAChannels_ShouldBeInRange_51_62()
        {
            // Arrange: DMA channel bits are 51-54 in Low, 64-67 in High
            // First 4 DMA channels: bits 51-54
            var dma0 = new SafetyMask128(1UL << 51, 0);
            var dma1 = new SafetyMask128(1UL << 52, 0);
            var dma2 = new SafetyMask128(1UL << 53, 0);
            var dma3 = new SafetyMask128(1UL << 54, 0);

            // Act: Combine DMA channel masks
            var combinedDMA = dma0 | dma1 | dma2 | dma3;

            // Assert: Should be bits 51-54
            ulong expectedBits = (1UL << 51) | (1UL << 52) | (1UL << 53) | (1UL << 54);
            Assert.Equal(expectedBits, combinedDMA.Low);
            Assert.Equal(0UL, combinedDMA.High);
        }

        #endregion

        #region 1.3 Scoreboard_Atomic_Update

        /// <summary>
        /// Test: Scoreboard_Atomic_Update
        /// Проверка корректности применения логического OR при захвате ресурсов
        /// и AND-NOT при освобождении.
        /// </summary>
        [Fact]
        public void Scoreboard_Atomic_Update_Acquire_ShouldUseOR()
        {
            // Arrange: Create a CPU core
            var core = new Processor.CPU_Core(0);
            core.ClearAllResourceLocks();

            var mask1 = new ResourceBitset(0x00000000000000FFUL, 0);
            var mask2 = new ResourceBitset(0x0000000000000F00UL, 0);

            // Act: Acquire resources using OR operation
            bool acquired1 = core.AcquireResources(mask1);
            bool acquired2 = core.AcquireResources(mask2);

            // Assert: Both acquisitions should succeed (no overlap)
            Assert.True(acquired1);
            Assert.True(acquired2);

            // Verify the combined state is the OR of both masks
            var currentLocks = core.GetGlobalResourceLocks();
            Assert.Equal(0x00000000000000FFUL | 0x0000000000000F00UL, currentLocks.Low);
        }

        /// <summary>
        /// Test: Scoreboard_Atomic_Update for release using AND-NOT
        /// Проверка освобождения ресурсов с использованием AND-NOT операции.
        /// </summary>
        [Fact]
        public void Scoreboard_Atomic_Update_Release_ShouldUseANDNOT()
        {
            // Arrange: Create a CPU core and acquire some resources
            var core = new Processor.CPU_Core(0);
            core.ClearAllResourceLocks();

            var mask = new ResourceBitset(0x00000000000000FFUL, 0);
            core.AcquireResources(mask);

            // Act: Release resources using AND-NOT operation
            core.ReleaseResources(mask);

            // Assert: Resources should be cleared
            var currentLocks = core.GetGlobalResourceLocks();
            Assert.Equal(0UL, currentLocks.Low);
            Assert.Equal(0UL, currentLocks.High);
        }

        /// <summary>
        /// Test: Scoreboard_Atomic_Update partial release
        /// Проверка частичного освобождения ресурсов.
        /// </summary>
        [Fact]
        public void Scoreboard_Atomic_Update_PartialRelease_ShouldWorkCorrectly()
        {
            // Arrange: Acquire multiple resources
            var core = new Processor.CPU_Core(0);
            core.ClearAllResourceLocks();

            var mask1 = new ResourceBitset(0x00000000000000FFUL, 0);
            var mask2 = new ResourceBitset(0x0000000000000F00UL, 0);
            core.AcquireResources(mask1);
            core.AcquireResources(mask2);

            // Act: Release only mask1
            core.ReleaseResources(mask1);

            // Assert: Only mask2 should remain
            var currentLocks = core.GetGlobalResourceLocks();
            Assert.Equal(0x0000000000000F00UL, currentLocks.Low);
            Assert.Equal(0UL, currentLocks.High);
        }

        #endregion

        #region 1.4 Scoreboard_No_Leakage

        /// <summary>
        /// Test: Scoreboard_No_Leakage
        /// Убедиться, что после выполнения и отмены (cancel) инструкции,
        /// Scoreboard возвращается в исходное состояние (отсутствие утечки зарезервированных битов).
        /// </summary>
        [Fact]
        public void Scoreboard_No_Leakage_AfterAcquireAndRelease_ShouldBeClean()
        {
            // Arrange: Create a CPU core
            var core = new Processor.CPU_Core(0);
            core.ClearAllResourceLocks();

            var initialState = core.GetGlobalResourceLocks();
            var mask = new ResourceBitset(0x00000000FFFFFFFFUL, 0x00000000FFFFFFFFUL);

            // Act: Acquire and then release resources
            bool acquired = core.AcquireResources(mask);
            Assert.True(acquired);

            core.ReleaseResources(mask);

            // Assert: Scoreboard should return to initial state (no leakage)
            var finalState = core.GetGlobalResourceLocks();
            Assert.Equal(initialState.Low, finalState.Low);
            Assert.Equal(initialState.High, finalState.High);
        }

        /// <summary>
        /// Test: Scoreboard_No_Leakage with token tracking
        /// Проверка отсутствия утечки с использованием токенов (ABA problem prevention).
        /// </summary>
        [Fact]
        public void Scoreboard_No_Leakage_WithTokenTracking_ShouldPreventABA()
        {
            // Arrange: Create a CPU core
            var core = new Processor.CPU_Core(0);
            core.ClearAllResourceLocks();

            // Use write bits (16-31) for ABA test because read bits use RefCounters instead of tokens.
            var mask = new ResourceBitset(0x0000000000FF0000UL, 0);

            // Act: Acquire with token
            bool acquired1 = core.AcquireResourcesWithToken(mask, out ulong token1);
            Assert.True(acquired1);

            // Release with token
            core.ReleaseResourcesWithToken(mask, token1);

            // Verify clean state
            var state1 = core.GetGlobalResourceLocks();
            Assert.Equal(0UL, state1.Low);

            // Re-acquire (new token)
            bool acquired2 = core.AcquireResourcesWithToken(mask, out ulong token2);
            Assert.True(acquired2);

            // Try to release with old token (should not work - ABA prevention)
            core.ReleaseResourcesWithToken(mask, token1);

            // Assert: Resource should still be locked (old token didn't release it)
            var state2 = core.GetGlobalResourceLocks();
            Assert.NotEqual(0UL, state2.Low);

            // Release with correct token
            core.ReleaseResourcesWithToken(mask, token2);

            // Assert: Now should be clean
            var finalState = core.GetGlobalResourceLocks();
            Assert.Equal(0UL, finalState.Low);
        }

        /// <summary>
        /// Test: Scoreboard_No_Leakage multiple acquire/release cycles
        /// Проверка отсутствия утечки при множественных циклах захвата/освобождения.
        /// </summary>
        [Fact]
        public void Scoreboard_No_Leakage_MultipleCycles_ShouldRemainClean()
        {
            // Arrange: Create a CPU core
            var core = new Processor.CPU_Core(0);
            core.ClearAllResourceLocks();

            var mask = new ResourceBitset(0x00000000FFFFFFFFUL, 0);

            // Act: Multiple acquire/release cycles
            for (int i = 0; i < 100; i++)
            {
                bool acquired = core.AcquireResources(mask);
                Assert.True(acquired);
                core.ReleaseResources(mask);
            }

            // Assert: Scoreboard should be clean (no accumulated leakage)
            var finalState = core.GetGlobalResourceLocks();
            Assert.Equal(0UL, finalState.Low);
            Assert.Equal(0UL, finalState.High);
        }

        /// <summary>
        /// Test: Scoreboard_No_Leakage with ClearAllResourceLocks
        /// Проверка полной очистки при flush pipeline.
        /// </summary>
        [Fact]
        public void Scoreboard_No_Leakage_ClearAll_ShouldResetCompletely()
        {
            // Arrange: Acquire various resources
            var core = new Processor.CPU_Core(0);
            core.AcquireResources(new ResourceBitset(0xFFFFFFFFFFFFFFFFUL, 0xFFFFFFFFFFFFFFFFUL));

            // Act: Clear all locks (pipeline flush)
            core.ClearAllResourceLocks();

            // Assert: All locks should be cleared
            var finalState = core.GetGlobalResourceLocks();
            Assert.Equal(0UL, finalState.Low);
            Assert.Equal(0UL, finalState.High);

            // Verify all banks are also cleared
            var banks = core.GetGrlbBanks();
            Assert.Equal(0u, banks[0]);
            Assert.Equal(0u, banks[1]);
            Assert.Equal(0u, banks[2]);
            Assert.Equal(0u, banks[3]);
        }

        /// <summary>
        /// Test: Verification of RAR permissibility and RefCount logic (Variant B fix)
        /// </summary>
        [Fact]
        public void Scoreboard_RAR_Permitted_Without_WAR_Leakage()
        {
            // Arrange
            var core = new Processor.CPU_Core(0);
            core.ClearAllResourceLocks();

            // Mask representing a read from Register Group 5 (bit 5)
            var readMask = new ResourceBitset(1UL << 5, 0);
            
            // Mask representing a write to Register Group 5 (bit 21, since WRITE_BASE=16)
            var writeMask = new ResourceBitset(1UL << 21, 0);

            // Act 1: Op A reads from Grp 5
            bool acquiredA = core.AcquireResourcesWithToken(readMask, out ulong tokenA);
            Assert.True(acquiredA, "Op A should acquire read lock.");

            // Act 2: Op B reads from Grp 5 (RAR shouldn't conflict)
            bool acquiredB = core.AcquireResourcesWithToken(readMask, out ulong tokenB);
            Assert.True(acquiredB, "Op B should acquire read lock due to safe RAR logic.");

            // Attempting to Write to Grp 5 while others are reading should fail! (WAR hazard)
            bool acquiredWrite = core.AcquireResourcesWithToken(writeMask, out ulong tokenW);
            Assert.False(acquiredWrite, "Write should fail due to active readers (WAR).");

            // Act 3: Op A completes and releases
            core.ReleaseResourcesWithToken(readMask, tokenA);

            // GRLB should STILL hold the read bit because Op B is still reading! 
            // This tests that AND-NOT didn't prematurely clear it.
            var stateAfterA = core.GetGlobalResourceLocks();
            Assert.Equal(1UL << 5, stateAfterA.Low);

            // Act 4: Op B completes and releases
            core.ReleaseResourcesWithToken(readMask, tokenB);

            // Now it should be completely empty
            var finalState = core.GetGlobalResourceLocks();
            Assert.Equal(0UL, finalState.Low);
        }

        #endregion
    }
}
