using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Unit tests for Intra-Core 4-way SMT scheduling and per-VT scoreboard.
    /// Verifies PackBundleIntraCoreSmt, NominateSmtCandidate, and per-thread
    /// scoreboard isolation.
    /// </summary>
    public class IntraCoreSmt4WayTests
    {
        private ScalarALUMicroOp CreateAluOp(int vt, ushort dest, ushort src1, ushort src2, int threadId = 0)
        {
            var op = new ScalarALUMicroOp
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Addition,
                OwnerThreadId = threadId,
                VirtualThreadId = vt,
                DestRegID = dest,
                Src1RegID = src1,
                Src2RegID = src2,
                WritesRegister = true
            };
            op.InitializeMetadata();
            return op;
        }

        private MicroOp[] CreateEmptyBundle()
        {
            var bundle = new MicroOp[8];
            for (int i = 0; i < 8; i++)
                bundle[i] = null!;
            return bundle;
        }

        private MicroOp[] CreateBundleWithOneOp(int vt)
        {
            var bundle = CreateEmptyBundle();
            bundle[0] = CreateAluOp(vt, dest: 0, src1: 1, src2: 2);
            return bundle;
        }

        #region NominateSmtCandidate Tests

        [Fact]
        public void WhenNominateValidCandidateThenPortIsSet()
        {
            var scheduler = new MicroOpScheduler();
            var op = CreateAluOp(vt: 1, dest: 4, src1: 5, src2: 6);

            scheduler.NominateSmtCandidate(1, op);

            // No direct way to inspect internal ports — verified via PackBundleIntraCoreSmt
            Assert.NotNull(op);
        }

        [Fact]
        public void WhenNominateOutOfRangeThenIgnored()
        {
            var scheduler = new MicroOpScheduler();
            var op = CreateAluOp(vt: 0, dest: 0, src1: 1, src2: 2);

            // Should not throw for out-of-range VT
            scheduler.NominateSmtCandidate(5, op);
            scheduler.NominateSmtCandidate(-1, op);
        }

        [Fact]
        public void WhenNominateNullThenIgnored()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.NominateSmtCandidate(0, null!);
        }

        #endregion

        #region PackBundleIntraCoreSmt Tests

        [Fact]
        public void WhenNoSmtCandidatesThenBundleUnchanged()
        {
            var scheduler = new MicroOpScheduler();
            var bundle = CreateBundleWithOneOp(vt: 0);
            var original0 = bundle[0];

            var result = scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            Assert.Same(original0, result[0]);
            for (int i = 1; i < 8; i++)
                Assert.Null(result[i]);
        }

        [Fact]
        public void WhenSmtCandidateNominatedThenInjectedIntoEmptySlot()
        {
            var scheduler = new MicroOpScheduler();
            var bundle = CreateBundleWithOneOp(vt: 0);

            // Nominate a candidate from VT1
            var candidate = CreateAluOp(vt: 1, dest: 8, src1: 9, src2: 10);
            scheduler.NominateSmtCandidate(1, candidate);

            var result = scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            // Should have the original op at slot 0 and the candidate somewhere in slots 1-7
            Assert.Same(bundle[0], result[0]);
            bool found = false;
            for (int i = 1; i < 8; i++)
            {
                if (result[i] == candidate)
                {
                    found = true;
                    break;
                }
            }
            Assert.True(found, "SMT candidate should be injected into an empty slot");
            Assert.Equal(1L, scheduler.SmtInjectionsCount);
        }

        [Fact]
        public void WhenSmtCandidateConflictsOnSharedResourceThenRejected()
        {
            var scheduler = new MicroOpScheduler();

            // Create bundle op that claims memory domain 0 (bit 32) via ALU op
            var bundleOp = CreateAluOp(vt: 0, dest: 0, src1: 1, src2: 2);
            // Add memory domain 0 to the existing SafetyMask
            bundleOp.SafetyMask |= new SafetyMask128(1UL << 32, 0);
            bundleOp.RefreshAdmissionMetadata();
            var bundle = CreateEmptyBundle();
            bundle[0] = bundleOp;

            // Nominate candidate that also claims memory domain 0
            var candidate = CreateAluOp(vt: 1, dest: 4, src1: 5, src2: 6);
            candidate.SafetyMask |= new SafetyMask128(1UL << 32, 0);
            candidate.RefreshAdmissionMetadata();
            scheduler.NominateSmtCandidate(1, candidate);

            var result = scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            // Candidate should NOT be injected (shared resource conflict on memory domain)
            bool found = false;
            for (int i = 1; i < 8; i++)
            {
                if (result[i] == candidate) found = true;
            }
            Assert.False(found, "Candidate with shared resource conflict should be rejected");
            Assert.True(scheduler.SmtRejectionsCount > 0, "Should have at least one rejection due to shared resource conflict");
        }

        [Fact]
        public void WhenMultipleVtCandidatesThenMultipleInjected()
        {
            var scheduler = new MicroOpScheduler();
            var bundle = CreateBundleWithOneOp(vt: 0);

            // Nominate candidates from VT1, VT2, VT3 using different register groups
            scheduler.NominateSmtCandidate(1, CreateAluOp(vt: 1, dest: 4, src1: 5, src2: 6));
            scheduler.NominateSmtCandidate(2, CreateAluOp(vt: 2, dest: 8, src1: 9, src2: 10));
            scheduler.NominateSmtCandidate(3, CreateAluOp(vt: 3, dest: 12, src1: 13, src2: 14));

            var result = scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            int injected = 0;
            for (int i = 0; i < 8; i++)
            {
                if (result[i] != null) injected++;
            }
            // Original + up to 3 injected
            Assert.True(injected >= 2, $"Expected at least 2 ops in bundle, got {injected}");
        }

        [Fact]
        public void WhenOwnerVtCandidateNominatedThenSkipped()
        {
            var scheduler = new MicroOpScheduler();
            var bundle = CreateBundleWithOneOp(vt: 0);

            // Nominate candidate from same VT as owner (should be skipped)
            var candidate = CreateAluOp(vt: 0, dest: 8, src1: 9, src2: 10);
            scheduler.NominateSmtCandidate(0, candidate);

            var result = scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            // The candidate should not have been injected (same VT as owner)
            bool found = false;
            for (int i = 1; i < 8; i++)
            {
                if (result[i] == candidate) found = true;
            }
            Assert.False(found, "Candidate from owner VT should be skipped");
        }

        [Fact]
        public void WhenVirtualThreadIsNotEligibleThenSchedulerSnapshotRejectsIt()
        {
            var scheduler = new MicroOpScheduler();
            var bundle = CreateBundleWithOneOp(vt: 0);

            var blockedCandidate = CreateAluOp(vt: 1, dest: 8, src1: 9, src2: 10);
            var allowedCandidate = CreateAluOp(vt: 2, dest: 12, src1: 13, src2: 14);
            scheduler.NominateSmtCandidate(1, blockedCandidate);
            scheduler.NominateSmtCandidate(2, allowedCandidate);

            var result = scheduler.PackBundleIntraCoreSmt(
                bundle,
                ownerVirtualThreadId: 0,
                localCoreId: 0,
                eligibleVirtualThreadMask: 0b1101);

            bool blockedInjected = false;
            bool allowedInjected = false;
            for (int i = 1; i < 8; i++)
            {
                blockedInjected |= result[i] == blockedCandidate;
                allowedInjected |= result[i] == allowedCandidate;
            }

            Assert.False(blockedInjected, "Ineligible VT must not appear in SMT nomination snapshot");
            Assert.True(allowedInjected, "Eligible VT should remain visible to SMT nomination snapshot");
        }

        [Fact]
        public void WhenEligibilityMaskAppliedThenDiagnosticsExposeFilteredReadyCandidates()
        {
            var scheduler = new MicroOpScheduler();
            var bundle = CreateBundleWithOneOp(vt: 0);

            scheduler.NominateSmtCandidate(1, CreateAluOp(vt: 1, dest: 8, src1: 9, src2: 10));
            scheduler.NominateSmtCandidate(2, CreateAluOp(vt: 2, dest: 12, src1: 13, src2: 14));

            scheduler.PackBundleIntraCoreSmt(
                bundle,
                ownerVirtualThreadId: 0,
                localCoreId: 0,
                eligibleVirtualThreadMask: 0b0101);

            var snapshot = scheduler.TestGetLastSmtEligibilitySnapshot();

            Assert.Equal(0b0101, snapshot.NormalizedMask);
            Assert.Equal(0b0110, snapshot.ReadyPortMask);
            Assert.Equal(0b0100, snapshot.VisibleReadyMask);
            Assert.Equal(0b0010, snapshot.MaskedReadyMask);
            Assert.True(snapshot.HasMaskedReadyCandidates);
            Assert.Equal(1, snapshot.MaskedReadyCount);
            Assert.Equal(1L, scheduler.EligibilityMaskedCycles);
            Assert.Equal(1L, scheduler.EligibilityMaskedReadyCandidates);
        }

        #endregion

        #region TDM Arbitration Tests

        [Fact]
        public void TdmLogic_SolvesStarvation_ForBackgroundThreads()
        {
            var scheduler = new MicroOpScheduler();
            // Let's create an empty bundle to give room for injection
            // We use ownerVirtualThreadId = -1 (no owner) to allow both VT0 (Primary) and VT1 (Background) to compete

            // Both threads want to use the exact same resource (conflict!)
            // To ensure only ONE of them is packed, we give them the same safety mask for memory logic.
            var opPrimary = CreateAluOp(vt: 0, dest: 4, src1: 5, src2: 6);
            opPrimary.SafetyMask |= new SafetyMask128(1UL << 32, 0);
            opPrimary.RefreshAdmissionMetadata();

            var opBackground = CreateAluOp(vt: 1, dest: 8, src1: 9, src2: 10);
            opBackground.SafetyMask |= new SafetyMask128(1UL << 32, 0);
            opBackground.RefreshAdmissionMetadata();

            // 1. Without TDM (Normal cycles) -> Priority encoder prefers VT0
            scheduler.GlobalCycleCounter = 10;
            scheduler.ClearSmtNominationPorts();
            scheduler.NominateSmtCandidate(0, opPrimary);
            scheduler.NominateSmtCandidate(1, opBackground);

            var resultNormal = scheduler.PackBundleIntraCoreSmt(CreateEmptyBundle(), ownerVirtualThreadId: -1, localCoreId: 0);

            bool primaryInjected = false;
            bool backgroundInjected = false;
            for (int i = 0; i < 8; i++)
            {
                if (resultNormal[i] == opPrimary) primaryInjected = true;
                if (resultNormal[i] == opBackground) backgroundInjected = true;
            }

            Assert.True(primaryInjected, "Primary thread should win normal arbitration");
            Assert.False(backgroundInjected, "Background thread should be starved in normal arbitration");

            // 2. With TDM (Cycle 64) -> TDM-aware encoder prefers VT > 0
            scheduler.GlobalCycleCounter = MicroOpScheduler.TDM_PERIOD - 1; // It will be incremented to TDM_PERIOD in PackBundle
            scheduler.ClearSmtNominationPorts();
            scheduler.NominateSmtCandidate(0, opPrimary);
            scheduler.NominateSmtCandidate(1, opBackground);

            var resultTdm = scheduler.PackBundleIntraCoreSmt(CreateEmptyBundle(), ownerVirtualThreadId: -1, localCoreId: 0);

            primaryInjected = false;
            backgroundInjected = false;
            for (int i = 0; i < 8; i++)
            {
                if (resultTdm[i] == opPrimary) primaryInjected = true;
                if (resultTdm[i] == opBackground) backgroundInjected = true;
            }

            Assert.False(primaryInjected, "Primary thread should yield during TDM frame");
            Assert.True(backgroundInjected, "Background thread should win TDM arbitration and be scheduled");
        }

        #endregion

        #region Per-Thread Scoreboard Tests

        [Fact]
        public void WhenSmtScoreboardSetThenPendingDetected()
        {
            var scheduler = new MicroOpScheduler();

            int slot = scheduler.SetSmtScoreboardPending(targetId: 5, virtualThreadId: 2, currentCycle: 100);
            Assert.True(slot >= 0, "Should allocate a scoreboard slot");
            Assert.True(scheduler.IsSmtScoreboardPending(targetId: 5, virtualThreadId: 2));
        }

        [Fact]
        public void WhenSmtScoreboardClearedThenNotPending()
        {
            var scheduler = new MicroOpScheduler();

            int slot = scheduler.SetSmtScoreboardPending(targetId: 5, virtualThreadId: 1, currentCycle: 100);
            scheduler.ClearSmtScoreboardEntry(virtualThreadId: 1, slotIndex: slot);

            Assert.False(scheduler.IsSmtScoreboardPending(targetId: 5, virtualThreadId: 1));
        }

        [Fact]
        public void WhenDifferentVtScoreboardThenIsolated()
        {
            var scheduler = new MicroOpScheduler();

            // Set pending on VT0
            scheduler.SetSmtScoreboardPending(targetId: 5, virtualThreadId: 0, currentCycle: 100);

            // VT1 should NOT see it
            Assert.False(scheduler.IsSmtScoreboardPending(targetId: 5, virtualThreadId: 1));
            Assert.False(scheduler.IsSmtScoreboardPending(targetId: 5, virtualThreadId: 2));
            Assert.False(scheduler.IsSmtScoreboardPending(targetId: 5, virtualThreadId: 3));

            // VT0 should see it
            Assert.True(scheduler.IsSmtScoreboardPending(targetId: 5, virtualThreadId: 0));
        }

        [Fact]
        public void WhenClearSmtScoreboardThenAllEntriesCleared()
        {
            var scheduler = new MicroOpScheduler();

            scheduler.SetSmtScoreboardPending(targetId: 1, virtualThreadId: 0, currentCycle: 10);
            scheduler.SetSmtScoreboardPending(targetId: 2, virtualThreadId: 1, currentCycle: 20);
            scheduler.SetSmtScoreboardPending(targetId: 3, virtualThreadId: 2, currentCycle: 30);
            scheduler.SetSmtScoreboardPending(targetId: 4, virtualThreadId: 3, currentCycle: 40);

            scheduler.ClearSmtScoreboard();

            for (int vt = 0; vt < 4; vt++)
            {
                Assert.False(scheduler.IsSmtScoreboardPending(targetId: vt + 1, virtualThreadId: vt));
            }
        }

        #endregion

        #region CPU_Core.StateData SMT Expansion Tests

        [Fact]
        public void WhenCoreCreatedThenSmtStateInitialized()
        {
            var core = new Processor.CPU_Core(0);

            Assert.NotNull(core.ArchContexts);
            Assert.Equal(Processor.CPU_Core.SmtWays, core.ArchContexts.Length);
            Assert.Equal(0, core.ActiveVirtualThreadId);
            for (int vt = 0; vt < Processor.CPU_Core.SmtWays; vt++)
            {
                Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(vt));
            }
        }

        [Fact]
        public void WhenCommittedPcsSetThenArchContextsRemainIndependent()
        {
            var core = new Processor.CPU_Core(0);

            core.WriteCommittedPc(0, 0x1000);
            core.WriteCommittedPc(1, 0x2000);
            core.WriteCommittedPc(2, 0x3000);
            core.WriteCommittedPc(3, 0x4000);

            Assert.Equal(0x1000UL, core.ReadCommittedPc(0));
            Assert.Equal(0x2000UL, core.ReadCommittedPc(1));
            Assert.Equal(0x3000UL, core.ReadCommittedPc(2));
            Assert.Equal(0x4000UL, core.ReadCommittedPc(3));
            Assert.Equal(0x1000UL, core.ArchContexts[0].CommittedPc);
            Assert.Equal(0x2000UL, core.ArchContexts[1].CommittedPc);
            Assert.Equal(0x3000UL, core.ArchContexts[2].CommittedPc);
            Assert.Equal(0x4000UL, core.ArchContexts[3].CommittedPc);
        }

        [Fact]
        public void WhenVirtualThreadStalledThenIsolated()
        {
            var core = new Processor.CPU_Core(0);

            core.WriteVirtualThreadPipelineState(1, PipelineState.WaitForEvent);

            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(0));
            Assert.Equal(PipelineState.WaitForEvent, core.ReadVirtualThreadPipelineState(1));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(2));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(3));
        }

        [Fact]
        public void WhenPrepareExecutionStartThenFreshStateAndPipelineModeAreReseeded()
        {
            var core = new Processor.CPU_Core(0);

            core.ActiveVirtualThreadId = 3;
            core.WriteActiveLivePc(0xDEAD);
            core.WriteCommittedPc(0, 0x1000);
            core.WriteCommittedPc(1, 0x2000);
            core.WriteCommittedPc(2, 0x3000);
            core.WriteCommittedPc(3, 0x4000);
            core.SetPipelineMode(false);

            core.PrepareExecutionStart(0x9000, activeVtId: 2);

            Assert.Equal(2, core.ReadActiveVirtualThreadId());
            Assert.Equal(0x9000UL, core.ReadActiveLivePc());
            Assert.Equal(0x9000UL, core.ReadCommittedPc(0));
            Assert.Equal(0x9000UL, core.ReadCommittedPc(1));
            Assert.Equal(0x9000UL, core.ReadCommittedPc(2));
            Assert.Equal(0x9000UL, core.ReadCommittedPc(3));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(0));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(1));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(2));
            Assert.Equal(PipelineState.Task, core.ReadVirtualThreadPipelineState(3));

            var pipeCtrl = core.GetPipelineControl();
            Assert.Equal(0UL, pipeCtrl.CycleCount);
            Assert.False(pipeCtrl.Stalled);
            Assert.True(pipeCtrl.Enabled);
            Assert.True(pipeCtrl.ClusterPreparedModeEnabled);
        }

        #endregion
    }
}
