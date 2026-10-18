using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Memory;
using HybridCPU_ISE.Tests.TestHelpers;

namespace HybridCPU_ISE.Tests.SafetyAndVerification
{
    /// <summary>
    /// Refactoring Pt. 3: Universal Scoreboard — MSHR / Outstanding Load/Store Tracking Tests.
    ///
    /// Validates:
    /// - ScoreboardEntryType-tagged per-VT entries (Free/Dma/OutstandingLoad/OutstandingStore)
    /// - Bank-level conflict detection (IsBankPendingForVT / IsBankPendingGlobal)
    /// - Outstanding memory count tracking (GetOutstandingMemoryCount)
    /// - LoadStoreMicroOp.MemoryBankId computation
    /// - FSP injection rejection on bank conflict (PackBundleIntraCoreSmt)
    /// - Typed allocation / clearing lifecycle (SetSmtScoreboardPendingTyped / ClearSmtScoreboardEntry)
    /// - Legacy DMA path backward compatibility
    /// - MshrScoreboardStalls counter accuracy
    /// </summary>
    public class UniversalScoreboardTests
    {
        private static MemorySubsystem CreateRuntimeMemorySubsystem(int numBanks, int bankWidthBytes)
        {
            Processor proc = default;
            return new MemorySubsystem(ref proc)
            {
                NumBanks = numBanks,
                BankWidthBytes = bankWidthBytes
            };
        }

        private static void WithProcessorMemory(MemorySubsystem? memory, Action action)
        {
            MemorySubsystem? savedMemory = Processor.Memory;
            try
            {
                Processor.Memory = memory;
                action();
            }
            finally
            {
                Processor.Memory = savedMemory;
            }
        }

        #region ScoreboardEntryType and Typed Allocation

        [Fact]
        public void SetSmtScoreboardPendingTyped_OutstandingLoad_ShouldAllocateSlot()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            int vtId = 0;
            int bankId = 3;

            // Act
            int slot = scheduler.SetSmtScoreboardPendingTyped(
                targetId: bankId, virtualThreadId: vtId, currentCycle: 0,
                entryType: ScoreboardEntryType.OutstandingLoad, bankId: bankId);

            // Assert
            Assert.True(slot >= 0, "Should allocate a valid slot index");
            Assert.True(scheduler.IsBankPendingForVT(bankId, vtId));
        }

        [Fact]
        public void SetSmtScoreboardPendingTyped_OutstandingStore_ShouldAllocateSlot()
        {
            var scheduler = new MicroOpScheduler();
            int vtId = 2;
            int bankId = 7;

            int slot = scheduler.SetSmtScoreboardPendingTyped(
                targetId: bankId, virtualThreadId: vtId, currentCycle: 0,
                entryType: ScoreboardEntryType.OutstandingStore, bankId: bankId);

            Assert.True(slot >= 0);
            Assert.True(scheduler.IsBankPendingForVT(bankId, vtId));
        }

        [Fact]
        public void SetSmtScoreboardPendingTyped_DmaEntry_ShouldNotTriggerBankPending()
        {
            // DMA entries use different type — should NOT be detected by IsBankPendingForVT
            var scheduler = new MicroOpScheduler();
            int vtId = 1;
            int bankId = 5;

            // Allocate as DMA (legacy path)
            scheduler.SetSmtScoreboardPendingTyped(
                targetId: bankId, virtualThreadId: vtId, currentCycle: 0,
                entryType: ScoreboardEntryType.Dma, bankId: bankId);

            // DMA entry should NOT show up as bank-pending for memory conflict detection
            Assert.False(scheduler.IsBankPendingForVT(bankId, vtId));
        }

        [Fact]
        public void SetSmtScoreboardPending_Legacy_ShouldAllocateAsDma()
        {
            // Legacy API should delegate to typed API with Dma type
            var scheduler = new MicroOpScheduler();
            int slot = scheduler.SetSmtScoreboardPending(targetId: 42, virtualThreadId: 0, currentCycle: 0);

            Assert.True(slot >= 0);
            // Legacy DMA entries should be queryable via original IsSmtScoreboardPending
            Assert.True(scheduler.IsSmtScoreboardPending(42, 0));
            // Should NOT trigger bank-level memory conflict
            Assert.False(scheduler.IsBankPendingForVT(42, 0));
        }

        [Fact]
        public void SetSmtScoreboardPendingTyped_FullScoreboard_ShouldReturnMinusOne()
        {
            var scheduler = new MicroOpScheduler();
            int vtId = 0;

            // Fill all 8 slots
            for (int i = 0; i < 8; i++)
            {
                int slot = scheduler.SetSmtScoreboardPendingTyped(
                    targetId: i, virtualThreadId: vtId, currentCycle: 0,
                    entryType: ScoreboardEntryType.OutstandingLoad, bankId: i);
                Assert.True(slot >= 0);
            }

            // 9th allocation should fail
            int overflow = scheduler.SetSmtScoreboardPendingTyped(
                targetId: 99, virtualThreadId: vtId, currentCycle: 0,
                entryType: ScoreboardEntryType.OutstandingLoad, bankId: 15);
            Assert.Equal(-1, overflow);
        }

        [Fact]
        public void SetSmtScoreboardPendingTyped_InvalidVT_ShouldReturnMinusOne()
        {
            var scheduler = new MicroOpScheduler();

            int slot = scheduler.SetSmtScoreboardPendingTyped(
                targetId: 0, virtualThreadId: 5, currentCycle: 0,
                entryType: ScoreboardEntryType.OutstandingLoad, bankId: 0);

            Assert.Equal(-1, slot);
        }

        #endregion

        #region ClearSmtScoreboardEntry — Type-Aware Clearing

        [Fact]
        public void ClearSmtScoreboardEntry_ShouldReleaseSlotAndBankPending()
        {
            var scheduler = new MicroOpScheduler();
            int vtId = 1;
            int bankId = 4;

            int slot = scheduler.SetSmtScoreboardPendingTyped(
                targetId: bankId, virtualThreadId: vtId, currentCycle: 0,
                entryType: ScoreboardEntryType.OutstandingLoad, bankId: bankId);

            Assert.True(scheduler.IsBankPendingForVT(bankId, vtId));

            // Act: clear the entry
            scheduler.ClearSmtScoreboardEntry(vtId, slot);

            // Assert: bank should no longer be pending
            Assert.False(scheduler.IsBankPendingForVT(bankId, vtId));
        }

        [Fact]
        public void ClearSmtScoreboard_ShouldClearAllEntriesAcrossAllVTs()
        {
            var scheduler = new MicroOpScheduler();

            // Fill entries across multiple VTs
            for (int vt = 0; vt < 4; vt++)
            {
                scheduler.SetSmtScoreboardPendingTyped(
                    targetId: vt, virtualThreadId: vt, currentCycle: 0,
                    entryType: ScoreboardEntryType.OutstandingLoad, bankId: vt);
            }

            // Verify they exist
            for (int vt = 0; vt < 4; vt++)
                Assert.True(scheduler.IsBankPendingForVT(vt, vt));

            // Act
            scheduler.ClearSmtScoreboard();

            // Assert: all cleared
            for (int vt = 0; vt < 4; vt++)
                Assert.False(scheduler.IsBankPendingForVT(vt, vt));
        }

        #endregion

        #region IsBankPendingForVT — Per-VT Bank Isolation

        [Fact]
        public void IsBankPendingForVT_DifferentVTs_ShouldBeIsolated()
        {
            var scheduler = new MicroOpScheduler();
            int bankId = 5;

            // Only VT0 has outstanding load for bank 5
            scheduler.SetSmtScoreboardPendingTyped(
                targetId: bankId, virtualThreadId: 0, currentCycle: 0,
                entryType: ScoreboardEntryType.OutstandingLoad, bankId: bankId);

            Assert.True(scheduler.IsBankPendingForVT(bankId, 0));
            Assert.False(scheduler.IsBankPendingForVT(bankId, 1));
            Assert.False(scheduler.IsBankPendingForVT(bankId, 2));
            Assert.False(scheduler.IsBankPendingForVT(bankId, 3));
        }

        [Fact]
        public void IsBankPendingForVT_DifferentBanks_ShouldBeIsolated()
        {
            var scheduler = new MicroOpScheduler();
            int vtId = 2;

            // Outstanding load for bank 3 only
            scheduler.SetSmtScoreboardPendingTyped(
                targetId: 3, virtualThreadId: vtId, currentCycle: 0,
                entryType: ScoreboardEntryType.OutstandingLoad, bankId: 3);

            Assert.True(scheduler.IsBankPendingForVT(3, vtId));
            Assert.False(scheduler.IsBankPendingForVT(0, vtId));
            Assert.False(scheduler.IsBankPendingForVT(7, vtId));
            Assert.False(scheduler.IsBankPendingForVT(15, vtId));
        }

        [Fact]
        public void IsBankPendingForVT_InvalidVT_ShouldReturnFalse()
        {
            var scheduler = new MicroOpScheduler();

            scheduler.SetSmtScoreboardPendingTyped(
                targetId: 0, virtualThreadId: 0, currentCycle: 0,
                entryType: ScoreboardEntryType.OutstandingLoad, bankId: 0);

            Assert.False(scheduler.IsBankPendingForVT(0, 5));  // VT=5 out of range
            Assert.False(scheduler.IsBankPendingForVT(0, -1)); // negative VT
        }

        #endregion

        #region IsBankPendingGlobal — Cross-VT Query

        [Fact]
        public void IsBankPendingGlobal_ShouldDetectAcrossAnyVT()
        {
            var scheduler = new MicroOpScheduler();
            int bankId = 10;

            // Only VT3 has outstanding load for bank 10
            scheduler.SetSmtScoreboardPendingTyped(
                targetId: bankId, virtualThreadId: 3, currentCycle: 0,
                entryType: ScoreboardEntryType.OutstandingStore, bankId: bankId);

            Assert.True(scheduler.IsBankPendingGlobal(bankId));
        }

        [Fact]
        public void IsBankPendingGlobal_EmptyScoreboard_ShouldReturnFalse()
        {
            var scheduler = new MicroOpScheduler();
            Assert.False(scheduler.IsBankPendingGlobal(0));
            Assert.False(scheduler.IsBankPendingGlobal(15));
        }

        #endregion

        #region GetOutstandingMemoryCount

        [Fact]
        public void GetOutstandingMemoryCount_ShouldCountLoadAndStoreEntries()
        {
            var scheduler = new MicroOpScheduler();
            int vtId = 1;

            Assert.Equal(0, scheduler.GetOutstandingMemoryCount(vtId));

            // Add 2 loads and 1 store
            scheduler.SetSmtScoreboardPendingTyped(0, vtId, 0, ScoreboardEntryType.OutstandingLoad, 0);
            scheduler.SetSmtScoreboardPendingTyped(1, vtId, 0, ScoreboardEntryType.OutstandingLoad, 1);
            scheduler.SetSmtScoreboardPendingTyped(2, vtId, 0, ScoreboardEntryType.OutstandingStore, 2);

            Assert.Equal(3, scheduler.GetOutstandingMemoryCount(vtId));
        }

        [Fact]
        public void GetOutstandingMemoryCount_ShouldNotCountDmaEntries()
        {
            var scheduler = new MicroOpScheduler();
            int vtId = 0;

            // Add 1 DMA + 1 OutstandingLoad
            scheduler.SetSmtScoreboardPendingTyped(0, vtId, 0, ScoreboardEntryType.Dma, -1);
            scheduler.SetSmtScoreboardPendingTyped(1, vtId, 0, ScoreboardEntryType.OutstandingLoad, 3);

            // DMA should not be counted
            Assert.Equal(1, scheduler.GetOutstandingMemoryCount(vtId));
        }

        [Fact]
        public void GetOutstandingMemoryCount_InvalidVT_ShouldReturnZero()
        {
            var scheduler = new MicroOpScheduler();
            Assert.Equal(0, scheduler.GetOutstandingMemoryCount(5));
            Assert.Equal(0, scheduler.GetOutstandingMemoryCount(-1));
        }

        #endregion

        #region LoadStoreMicroOp.MemoryBankId — Address-Interleaved Bank Computation

        [Fact]
        public void LoadMicroOp_MemoryBankId_ShouldReturnExplicitUninitializedContour_WhenRuntimeGeometryIsUnavailable()
        {
            WithProcessorMemory(memory: null, () =>
            {
                var load = new LoadMicroOp { Address = 0 };
                Assert.Equal(MemoryBankRouting.UninitializedSchedulerVisibleBankId, load.MemoryBankId);
            });
        }

        [Fact]
        public void StoreMicroOp_MemoryBankId_ShouldReturnExplicitUninitializedContour_WhenRuntimeGeometryIsUnavailable()
        {
            WithProcessorMemory(memory: null, () =>
            {
                var store = new StoreMicroOp { Address = 4096 * 7 };
                Assert.Equal(MemoryBankRouting.UninitializedSchedulerVisibleBankId, store.MemoryBankId);
            });
        }

        [Fact]
        public void LoadMicroOp_MemoryAddress_ShouldReturnAddress()
        {
            var load = new LoadMicroOp { Address = 0xDEADBEEF };
            Assert.Equal(0xDEADBEEFUL, load.MemoryAddress);
        }

        [Fact]
        public void StoreMicroOp_MemoryAddress_ShouldReturnAddress()
        {
            var store = new StoreMicroOp { Address = 0xCAFEBABE };
            Assert.Equal(0xCAFEBABEUL, store.MemoryAddress);
        }

        [Theory]
        [InlineData(0UL, 0)]
        [InlineData(4095UL, 0)]       // same page → bank 0
        [InlineData(4096UL, 1)]       // next page → bank 1
        [InlineData(8192UL, 2)]       // bank 2
        [InlineData(65536UL, 0)]      // 4096*16 → wraps to bank 0
        [InlineData(65536UL + 4096, 1)] // wraps + 1 → bank 1
        public void MemoryBankId_Theory_ShouldMatchExpected(ulong address, int expectedBank)
        {
            WithProcessorMemory(CreateRuntimeMemorySubsystem(numBanks: 16, bankWidthBytes: 4096), () =>
            {
                var load = new LoadMicroOp { Address = address };
                Assert.Equal(expectedBank, load.MemoryBankId);
            });
        }

        [Fact]
        public void LoadMicroOp_MemoryBankId_ShouldFollowRuntimeMemoryGeometry()
        {
            MemorySubsystem runtimeMemory = CreateRuntimeMemorySubsystem(numBanks: 16, bankWidthBytes: 128);

            WithProcessorMemory(runtimeMemory, () =>
            {
                var load = new LoadMicroOp { Address = 128 };
                Assert.Equal(1, load.MemoryBankId);

                load = new LoadMicroOp { Address = (128UL * 15UL) + 64UL };
                Assert.Equal(15, load.MemoryBankId);

                load = new LoadMicroOp { Address = (128UL * 16UL) + 7UL };
                Assert.Equal(0, load.MemoryBankId);
            });
        }

        #endregion

        #region PackBundleIntraCoreSmt — MSHR Bank Conflict Rejection

        [Fact]
        public void PackBundleIntraCoreSmt_LoadWithBankConflict_ShouldRejectInjection()
        {
            WithProcessorMemory(CreateRuntimeMemorySubsystem(numBanks: 16, bankWidthBytes: 4096), () =>
            {
            // Arrange
            var scheduler = new MicroOpScheduler();

            // Pre-populate scoreboard: VT1 has outstanding load on bank 0
            scheduler.SetSmtScoreboardPendingTyped(
                targetId: 0, virtualThreadId: 1, currentCycle: 0,
                entryType: ScoreboardEntryType.OutstandingLoad, bankId: 0);

            // Create a load from VT1 targeting bank 0 (address 0 → bank 0)
            var candidateLoad = MicroOpTestHelper.CreateLoad(
                virtualThreadId: 1, destReg: 20, address: 0, domainTag: 0);

            // Nominate VT1 candidate
            scheduler.NominateSmtCandidate(1, candidateLoad);

            // Create an empty bundle owned by VT0
            var bundle = new MicroOp[8];

            // Act
            var result = scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            // Assert: candidate should be rejected due to bank conflict
            bool injected = false;
            for (int i = 0; i < 8; i++)
            {
                if (result[i] != null)
                    injected = true;
            }
            Assert.False(injected, "Load targeting a bank with outstanding MSHR should be rejected");
            Assert.True(scheduler.MshrScoreboardStalls > 0, "MshrScoreboardStalls counter should increment");
            });
        }

        [Fact]
        public void PackBundleIntraCoreSmt_ScalarALU_ShouldBypassBankCheck()
        {
            // ALU ops are not LoadStoreMicroOp — should skip MSHR check entirely
            var scheduler = new MicroOpScheduler();

            // Lock all banks for VT2 (but ALU ops should not care)
            for (int b = 0; b < 16; b++)
            {
                scheduler.SetSmtScoreboardPendingTyped(
                    targetId: b, virtualThreadId: 2, currentCycle: 0,
                    entryType: ScoreboardEntryType.OutstandingLoad, bankId: b);
            }

            var candidateALU = MicroOpTestHelper.CreateScalarALU(
                virtualThreadId: 2, destReg: 20, src1Reg: 21, src2Reg: 22);

            scheduler.NominateSmtCandidate(2, candidateALU);

            var bundle = new MicroOp[8];

            var result = scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            bool injected = false;
            for (int i = 0; i < 8; i++)
            {
                if (result[i] != null)
                    injected = true;
            }
            Assert.True(injected, "Scalar ALU ops should bypass MSHR bank conflict checks");
        }

        [Fact]
        public void PackBundleIntraCoreSmt_StoreWithBankConflict_ShouldRejectInjection()
        {
            WithProcessorMemory(CreateRuntimeMemorySubsystem(numBanks: 16, bankWidthBytes: 4096), () =>
            {
            var scheduler = new MicroOpScheduler();

            // VT3 has outstanding store on bank 7
            scheduler.SetSmtScoreboardPendingTyped(
                targetId: 7, virtualThreadId: 3, currentCycle: 0,
                entryType: ScoreboardEntryType.OutstandingStore, bankId: 7);

            // Create a store from VT3 targeting bank 7 (4096*7 → bank 7)
            var candidateStore = MicroOpTestHelper.CreateStore(
                virtualThreadId: 3, srcReg: 10, address: 4096 * 7, domainTag: 0);
            Assert.True(candidateStore.AdmissionMetadata.IsMemoryOp);
            Assert.Equal(7, candidateStore.MemoryBankId);

            var writeRange = Assert.Single(candidateStore.AdmissionMetadata.WriteMemoryRanges);
            Assert.Equal(4096UL * 7UL, writeRange.Address);
            Assert.Equal((ulong)sizeof(ulong), writeRange.Length);

            scheduler.NominateSmtCandidate(3, candidateStore);

            var bundle = new MicroOp[8];

            var result = scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            bool injected = false;
            for (int i = 0; i < 8; i++)
            {
                if (result[i] != null)
                    injected = true;
            }
            Assert.False(injected, "Store targeting a bank with outstanding MSHR should be rejected");
            Assert.True(scheduler.MshrScoreboardStalls > 0, "MshrScoreboardStalls counter should increment");
            });
        }

        #endregion

        #region Scoreboard Lifecycle — Allocate / Forward / Clear

        [Fact]
        public void ScoreboardLifecycle_AllocateThenClear_ShouldRestoreAvailability()
        {
            // Simulates the full pipeline lifecycle: EX registers → WB clears
            var scheduler = new MicroOpScheduler();
            int vtId = 0;
            int bankId = 3;

            // Step 1: EX stage — register outstanding load
            int slot = scheduler.SetSmtScoreboardPendingTyped(
                targetId: bankId, virtualThreadId: vtId, currentCycle: 100,
                entryType: ScoreboardEntryType.OutstandingLoad, bankId: bankId);
            Assert.True(slot >= 0);
            Assert.True(scheduler.IsBankPendingForVT(bankId, vtId));
            Assert.Equal(1, scheduler.GetOutstandingMemoryCount(vtId));

            // Step 2: WB stage — clear the entry using forwarded slot index
            scheduler.ClearSmtScoreboardEntry(vtId, slot);
            Assert.False(scheduler.IsBankPendingForVT(bankId, vtId));
            Assert.Equal(0, scheduler.GetOutstandingMemoryCount(vtId));
        }

        [Fact]
        public void ScoreboardLifecycle_MultipleBanks_IndependentTracking()
        {
            var scheduler = new MicroOpScheduler();
            int vtId = 2;

            int slot0 = scheduler.SetSmtScoreboardPendingTyped(
                0, vtId, 0, ScoreboardEntryType.OutstandingLoad, 0);
            int slot5 = scheduler.SetSmtScoreboardPendingTyped(
                5, vtId, 0, ScoreboardEntryType.OutstandingLoad, 5);
            int slot10 = scheduler.SetSmtScoreboardPendingTyped(
                10, vtId, 0, ScoreboardEntryType.OutstandingStore, 10);

            Assert.Equal(3, scheduler.GetOutstandingMemoryCount(vtId));
            Assert.True(scheduler.IsBankPendingForVT(0, vtId));
            Assert.True(scheduler.IsBankPendingForVT(5, vtId));
            Assert.True(scheduler.IsBankPendingForVT(10, vtId));

            // Clear bank 5 only
            scheduler.ClearSmtScoreboardEntry(vtId, slot5);
            Assert.True(scheduler.IsBankPendingForVT(0, vtId));
            Assert.False(scheduler.IsBankPendingForVT(5, vtId));
            Assert.True(scheduler.IsBankPendingForVT(10, vtId));
            Assert.Equal(2, scheduler.GetOutstandingMemoryCount(vtId));
        }

        [Fact]
        public void ScoreboardLifecycle_ClearAfterSquash_ShouldStillRelease()
        {
            // Even squashed ops must release their scoreboard entry
            // (deterministic release via forwarded slot index — no dependency on op success)
            var scheduler = new MicroOpScheduler();
            int vtId = 1;
            int bankId = 8;

            int slot = scheduler.SetSmtScoreboardPendingTyped(
                bankId, vtId, 0, ScoreboardEntryType.OutstandingLoad, bankId);
            Assert.True(scheduler.IsBankPendingForVT(bankId, vtId));

            // Simulate squash: just clear the entry (no Commit needed)
            scheduler.ClearSmtScoreboardEntry(vtId, slot);
            Assert.False(scheduler.IsBankPendingForVT(bankId, vtId));
        }

        #endregion

        #region Backward Compatibility — Legacy DMA Scoreboard

        [Fact]
        public void LegacySetSmtScoreboardPending_ShouldWorkWithIsSmtScoreboardPending()
        {
            var scheduler = new MicroOpScheduler();

            // Use legacy API
            int slot = scheduler.SetSmtScoreboardPending(targetId: 77, virtualThreadId: 3, currentCycle: 0);
            Assert.True(slot >= 0);
            Assert.True(scheduler.IsSmtScoreboardPending(77, 3));

            // Clear via legacy-compatible path
            scheduler.ClearSmtScoreboardEntry(3, slot);
            Assert.False(scheduler.IsSmtScoreboardPending(77, 3));
        }

        [Fact]
        public void LegacyAndTyped_CanCoexistInSameVT()
        {
            var scheduler = new MicroOpScheduler();
            int vtId = 0;

            // Mix legacy DMA and new typed entries in the same VT
            int dmaSlot = scheduler.SetSmtScoreboardPending(100, vtId, 0);
            int loadSlot = scheduler.SetSmtScoreboardPendingTyped(
                5, vtId, 0, ScoreboardEntryType.OutstandingLoad, 5);

            Assert.True(scheduler.IsSmtScoreboardPending(100, vtId));
            Assert.True(scheduler.IsBankPendingForVT(5, vtId));
            Assert.Equal(1, scheduler.GetOutstandingMemoryCount(vtId)); // only Load counts

            // Clear DMA
            scheduler.ClearSmtScoreboardEntry(vtId, dmaSlot);
            Assert.False(scheduler.IsSmtScoreboardPending(100, vtId));
            Assert.True(scheduler.IsBankPendingForVT(5, vtId)); // Load still pending

            // Clear Load
            scheduler.ClearSmtScoreboardEntry(vtId, loadSlot);
            Assert.False(scheduler.IsBankPendingForVT(5, vtId));
        }

        #endregion

        #region MshrScoreboardStalls Counter

        [Fact]
        public void MshrScoreboardStalls_ShouldIncrementOnBankConflictRejection()
        {
            WithProcessorMemory(CreateRuntimeMemorySubsystem(numBanks: 16, bankWidthBytes: 4096), () =>
            {
            var scheduler = new MicroOpScheduler();

            // Lock bank 0 for VT1
            scheduler.SetSmtScoreboardPendingTyped(
                0, 1, 0, ScoreboardEntryType.OutstandingLoad, 0);

            // Nominate a load from VT1 targeting bank 0
            var load = MicroOpTestHelper.CreateLoad(virtualThreadId: 1, destReg: 5, address: 0);
            scheduler.NominateSmtCandidate(1, load);

            long stallsBefore = scheduler.MshrScoreboardStalls;

            // Pack with owner VT0 — should reject VT1's load
            scheduler.PackBundleIntraCoreSmt(new MicroOp[8], ownerVirtualThreadId: 0, localCoreId: 0);

            Assert.Equal(stallsBefore + 1, scheduler.MshrScoreboardStalls);
            });
        }

        #endregion

        #region Edge Cases and Stress

        [Fact]
        public void IsBankPendingForVT_AllBanksLocked_ShouldDetectAll()
        {
            var scheduler = new MicroOpScheduler();
            int vtId = 0;

            // Lock banks 0–7 (max 8 slots per VT)
            for (int b = 0; b < 8; b++)
            {
                scheduler.SetSmtScoreboardPendingTyped(
                    b, vtId, 0, ScoreboardEntryType.OutstandingLoad, b);
            }

            for (int b = 0; b < 8; b++)
                Assert.True(scheduler.IsBankPendingForVT(b, vtId));

            // Banks 8–15 should NOT be pending (no slots allocated for them)
            for (int b = 8; b < 16; b++)
                Assert.False(scheduler.IsBankPendingForVT(b, vtId));
        }

        [Fact]
        public void ConcurrentVTs_IndependentScoreboards()
        {
            var scheduler = new MicroOpScheduler();

            // Each VT locks a different bank
            for (int vt = 0; vt < 4; vt++)
            {
                scheduler.SetSmtScoreboardPendingTyped(
                    vt, vt, 0, ScoreboardEntryType.OutstandingLoad, vt);
            }

            // Cross-check: each VT only blocks its own bank
            for (int vt = 0; vt < 4; vt++)
            {
                Assert.True(scheduler.IsBankPendingForVT(vt, vt));
                for (int otherVt = 0; otherVt < 4; otherVt++)
                {
                    if (otherVt != vt)
                        Assert.False(scheduler.IsBankPendingForVT(vt, otherVt));
                }
            }
        }

        [Fact]
        public void MultiplePendingOnSameBank_SingleClearReleasesOne()
        {
            var scheduler = new MicroOpScheduler();
            int vtId = 0;
            int bankId = 3;

            // Two outstanding loads on same bank for same VT
            int slot1 = scheduler.SetSmtScoreboardPendingTyped(
                bankId, vtId, 0, ScoreboardEntryType.OutstandingLoad, bankId);
            int slot2 = scheduler.SetSmtScoreboardPendingTyped(
                bankId + 100, vtId, 0, ScoreboardEntryType.OutstandingLoad, bankId);

            Assert.True(scheduler.IsBankPendingForVT(bankId, vtId));

            // Clear one — bank should still be pending (second entry remains)
            scheduler.ClearSmtScoreboardEntry(vtId, slot1);
            Assert.True(scheduler.IsBankPendingForVT(bankId, vtId));

            // Clear second — now bank should be free
            scheduler.ClearSmtScoreboardEntry(vtId, slot2);
            Assert.False(scheduler.IsBankPendingForVT(bankId, vtId));
        }

        #endregion

        #region ISA Guard — Verify No ISA/Binary Format Changes

        [Fact]
        public void ISAGuard_ScoreboardEntryType_IsEnumNotStoredInInstruction()
        {
            // ScoreboardEntryType is a microarchitectural enum — verify it's byte-sized
            // and does NOT appear in any instruction encoding structure.
            Assert.Equal(sizeof(byte), sizeof(ScoreboardEntryType));
            Assert.Equal((byte)0, (byte)ScoreboardEntryType.Free);
            Assert.Equal((byte)1, (byte)ScoreboardEntryType.Dma);
            Assert.Equal((byte)2, (byte)ScoreboardEntryType.OutstandingLoad);
            Assert.Equal((byte)3, (byte)ScoreboardEntryType.OutstandingStore);
        }

        [Fact]
        public void ISAGuard_MemoryBankId_IsComputedNotEncoded()
        {
            // MemoryBankId is dynamically computed from the address — it is NOT a field
            // stored in the VLIW instruction binary encoding.
            WithProcessorMemory(CreateRuntimeMemorySubsystem(numBanks: 16, bankWidthBytes: 4096), () =>
            {
                var load1 = new LoadMicroOp { Address = 4096 * 3 };
                var load2 = new LoadMicroOp { Address = 4096 * 3 + 100 };

                Assert.Equal(load1.MemoryBankId, load2.MemoryBankId);
                Assert.Equal(3, load1.MemoryBankId);
            });
        }

        #endregion
    }
}

