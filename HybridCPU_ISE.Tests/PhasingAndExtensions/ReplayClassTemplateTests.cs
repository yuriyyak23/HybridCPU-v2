using Xunit;
using YAKSys_Hybrid_CPU.Core;
using HybridCPU_ISE.Tests.TestHelpers;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Phase 07 — Replay Class-Level Template Reuse tests.
    /// Validates ClassCapacityTemplate, template capture/match, domain scoping,
    /// decrement-aware fast path, LoopBuffer class-level donor capacity,
    /// and ReplayEngine class-template-aware counterexample types.
    /// </summary>
    public class ReplayClassTemplateTests
    {
        #region ClassCapacityTemplate Construction

        [Fact]
        public void ClassCapacityTemplate_WhenConstructedFromState_ThenCapturesFreeCapacity()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            // Default: ALU=4 total, LSU=2 total, DmaStream=1, Branch=1, System=1
            // All unoccupied → free = total

            var template = new ClassCapacityTemplate(state);

            Assert.Equal(4, template.AluFree);
            Assert.Equal(2, template.LsuFree);
            Assert.Equal(1, template.DmaStreamFree);
            Assert.Equal(1, template.BranchControlFree);
            Assert.Equal(1, template.SystemSingletonFree);
        }

        [Fact]
        public void ClassCapacityTemplate_WhenPartiallyOccupied_ThenCapturesRemainingFree()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            state.IncrementOccupancy(SlotClass.AluClass);
            state.IncrementOccupancy(SlotClass.AluClass);
            state.IncrementOccupancy(SlotClass.LsuClass);

            var template = new ClassCapacityTemplate(state);

            Assert.Equal(2, template.AluFree);     // 4 - 2
            Assert.Equal(1, template.LsuFree);      // 2 - 1
            Assert.Equal(1, template.DmaStreamFree);
            Assert.Equal(1, template.BranchControlFree);
            Assert.Equal(1, template.SystemSingletonFree);
        }

        [Fact]
        public void ClassCapacityTemplate_WhenOverCounted_ThenClampedToZero()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            // Over-occupy ALU beyond total
            state.IncrementOccupancy(SlotClass.AluClass);
            state.IncrementOccupancy(SlotClass.AluClass);
            state.IncrementOccupancy(SlotClass.AluClass);
            state.IncrementOccupancy(SlotClass.AluClass);
            state.IncrementOccupancy(SlotClass.AluClass); // 5th → free = -1

            var template = new ClassCapacityTemplate(state);

            Assert.Equal(0, template.AluFree); // clamped from -1
        }

        #endregion

        #region ClassCapacityTemplate Compatibility

        [Fact]
        public void IsCompatibleWith_WhenCurrentHasMoreFree_ThenReturnsTrue()
        {
            var captureState = new SlotClassCapacityState();
            captureState.InitializeFromLaneMap();
            captureState.IncrementOccupancy(SlotClass.AluClass); // ALU free = 3
            var template = new ClassCapacityTemplate(captureState);

            var currentState = new SlotClassCapacityState();
            currentState.InitializeFromLaneMap(); // ALU free = 4

            Assert.True(template.IsCompatibleWith(currentState));
        }

        [Fact]
        public void IsCompatibleWith_WhenCurrentHasEqualFree_ThenReturnsTrue()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            state.IncrementOccupancy(SlotClass.AluClass);
            var template = new ClassCapacityTemplate(state);

            Assert.True(template.IsCompatibleWith(state));
        }

        [Fact]
        public void IsCompatibleWith_WhenAnyClassHasLessFree_ThenReturnsFalse()
        {
            var captureState = new SlotClassCapacityState();
            captureState.InitializeFromLaneMap(); // ALU free = 4
            var template = new ClassCapacityTemplate(captureState);

            var currentState = new SlotClassCapacityState();
            currentState.InitializeFromLaneMap();
            currentState.IncrementOccupancy(SlotClass.AluClass);
            currentState.IncrementOccupancy(SlotClass.AluClass);
            currentState.IncrementOccupancy(SlotClass.AluClass);
            currentState.IncrementOccupancy(SlotClass.AluClass);
            // ALU free = 0, template expects 4

            Assert.False(template.IsCompatibleWith(currentState));
        }

        [Fact]
        public void IsCompatibleWith_WhenLsuClassHasLessFree_ThenReturnsFalse()
        {
            var captureState = new SlotClassCapacityState();
            captureState.InitializeFromLaneMap(); // LSU free = 2
            var template = new ClassCapacityTemplate(captureState);

            var currentState = new SlotClassCapacityState();
            currentState.InitializeFromLaneMap();
            currentState.IncrementOccupancy(SlotClass.LsuClass);
            currentState.IncrementOccupancy(SlotClass.LsuClass);
            // LSU free = 0, template expects 2

            Assert.False(template.IsCompatibleWith(currentState));
        }

        #endregion

        #region ClassCapacityTemplate Equality

        [Fact]
        public void ClassCapacityTemplate_WhenSameState_ThenEqual()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            state.IncrementOccupancy(SlotClass.AluClass);

            var template1 = new ClassCapacityTemplate(state);
            var template2 = new ClassCapacityTemplate(state);

            Assert.True(template1.Equals(template2));
            Assert.Equal(template1.GetHashCode(), template2.GetHashCode());
        }

        [Fact]
        public void ClassCapacityTemplate_WhenDifferentState_ThenNotEqual()
        {
            var state1 = new SlotClassCapacityState();
            state1.InitializeFromLaneMap();

            var state2 = new SlotClassCapacityState();
            state2.InitializeFromLaneMap();
            state2.IncrementOccupancy(SlotClass.AluClass);

            var template1 = new ClassCapacityTemplate(state1);
            var template2 = new ClassCapacityTemplate(state2);

            Assert.False(template1.Equals(template2));
        }

        [Fact]
        public void ClassCapacityTemplate_GetCapturedFree_ReturnsCorrectValues()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            state.IncrementOccupancy(SlotClass.AluClass);

            var template = new ClassCapacityTemplate(state);

            Assert.Equal(3, template.GetCapturedFree(SlotClass.AluClass));
            Assert.Equal(2, template.GetCapturedFree(SlotClass.LsuClass));
            Assert.Equal(1, template.GetCapturedFree(SlotClass.DmaStreamClass));
            Assert.Equal(0, template.GetCapturedFree(SlotClass.Unclassified));
        }

        #endregion

        #region TemplateBudget

        [Fact]
        public void TemplateBudget_WhenInitialized_ThenMatchesTemplate()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            state.IncrementOccupancy(SlotClass.AluClass);
            var template = new ClassCapacityTemplate(state);

            var budget = new TemplateBudget(template);

            Assert.Equal(3, budget.GetRemaining(SlotClass.AluClass));
            Assert.Equal(2, budget.GetRemaining(SlotClass.LsuClass));
            Assert.Equal(1, budget.GetRemaining(SlotClass.DmaStreamClass));
        }

        [Fact]
        public void TemplateBudget_WhenDecremented_ThenRemainingDecreases()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            var template = new ClassCapacityTemplate(state);
            var budget = new TemplateBudget(template);

            budget.Decrement(SlotClass.AluClass);
            Assert.Equal(3, budget.GetRemaining(SlotClass.AluClass));

            budget.Decrement(SlotClass.AluClass);
            Assert.Equal(2, budget.GetRemaining(SlotClass.AluClass));
        }

        [Fact]
        public void TemplateBudget_WhenDecrementedToZero_ThenRemainsAtZero()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            var template = new ClassCapacityTemplate(state);
            var budget = new TemplateBudget(template);

            // DmaStream has capacity 1
            budget.Decrement(SlotClass.DmaStreamClass);
            Assert.Equal(0, budget.GetRemaining(SlotClass.DmaStreamClass));

            // Decrementing again should stay at 0
            budget.Decrement(SlotClass.DmaStreamClass);
            Assert.Equal(0, budget.GetRemaining(SlotClass.DmaStreamClass));
        }

        #endregion

        #region ReplayPhaseKey Extension

        [Fact]
        public void ReplayPhaseKey_LegacyConstructor_ThenDefaultClassTemplateAndDomainScope()
        {
            var key = new ReplayPhaseKey(epochId: 1, cachedPc: 0x1000, stableDonorMask: 0xFF, validSlotCount: 8);

            Assert.Equal(default(ClassCapacityTemplate), key.ClassTemplate);
            Assert.Equal(0, key.DomainScopeId);
        }

        [Fact]
        public void ReplayPhaseKey_ExtendedConstructor_ThenClassTemplateAndDomainScopePreserved()
        {
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            var template = new ClassCapacityTemplate(state);

            var key = new ReplayPhaseKey(
                epochId: 1, cachedPc: 0x1000, stableDonorMask: 0xFF, validSlotCount: 8,
                classTemplate: template, domainScopeId: 42);

            Assert.Equal(template, key.ClassTemplate);
            Assert.Equal(42, key.DomainScopeId);
        }

        #endregion

        #region ReplayPhaseInvalidationReason Phase 07 Values

        [Fact]
        public void ReplayPhaseInvalidationReason_Phase07Values_ThenCorrectOrdinals()
        {
            Assert.Equal((byte)7, (byte)ReplayPhaseInvalidationReason.DomainBoundary);
            Assert.Equal((byte)8, (byte)ReplayPhaseInvalidationReason.ClassCapacityMismatch);
            Assert.Equal((byte)9, (byte)ReplayPhaseInvalidationReason.ClassTemplateExpired);
        }

        #endregion

        #region Template Capture — First Successful Injection

        [Fact]
        public void WhenTypedSlotEnabledAndReplayActive_AndFirstInjection_ThenClassTemplateCaptured()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.TypedSlotEnabled = true;

            // Activate replay phase
            var replayPhase = new ReplayPhaseContext(
                isActive: true, epochId: 1, cachedPc: 0x1000, epochLength: 100,
                completedReplays: 0, validSlotCount: 8, stableDonorMask: 0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);
            scheduler.SetReplayPhaseContext(replayPhase);

            Assert.False(scheduler.TestGetClassTemplateValid());

            // Inject a candidate
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, candidate);

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            // Template should be captured after first injection
            Assert.True(scheduler.TestGetClassTemplateValid());
            var template = scheduler.TestGetClassCapacityTemplate();
            // Template should reflect the capacity at capture time
            Assert.True(template.AluFree > 0 || template.LsuFree > 0);
        }

        [Fact]
        public void WhenTypedSlotDisabled_ThenClassTemplateNotCaptured()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.TypedSlotEnabled = false;

            var replayPhase = new ReplayPhaseContext(
                isActive: true, epochId: 1, cachedPc: 0x1000, epochLength: 100,
                completedReplays: 0, validSlotCount: 8, stableDonorMask: 0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);
            scheduler.SetReplayPhaseContext(replayPhase);

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, candidate);

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            Assert.False(scheduler.TestGetClassTemplateValid());
        }

        [Fact]
        public void WhenReplayNotActive_ThenClassTemplateNotCaptured()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.TypedSlotEnabled = true;

            // No replay phase active (default)
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, candidate);

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            Assert.False(scheduler.TestGetClassTemplateValid());
        }

        #endregion

        #region Template Match — Level 1 Class-Capacity

        [Fact]
        public void WhenClassTemplateValid_AndCompatible_ThenReuseHitsIncremented()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.TypedSlotEnabled = true;

            var replayPhase = new ReplayPhaseContext(
                isActive: true, epochId: 1, cachedPc: 0x1000, epochLength: 100,
                completedReplays: 1, validSlotCount: 8, stableDonorMask: 0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);
            scheduler.SetReplayPhaseContext(replayPhase);

            // Seed a valid class template
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            state.IncrementOccupancy(SlotClass.AluClass); // 1 ALU occupied
            var template = new ClassCapacityTemplate(state);
            scheduler.TestSetClassCapacityTemplate(template);
            scheduler.TestSetClassTemplateValid(true);
            scheduler.TestSetClassTemplateDomainId(0);

            long hitsBefore = scheduler.ClassTemplateReuseHits;

            // Run a cycle — class capacity should be compatible since bundle has same structure
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, candidate);

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            Assert.True(scheduler.ClassTemplateReuseHits > hitsBefore);
        }

        #endregion

        #region Domain Scoping — Invalidation on Boundary

        [Fact]
        public void WhenDomainIdChanges_ThenClassTemplateInvalidated()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.TypedSlotEnabled = true;

            var replayPhase = new ReplayPhaseContext(
                isActive: true, epochId: 1, cachedPc: 0x1000, epochLength: 100,
                completedReplays: 1, validSlotCount: 8, stableDonorMask: 0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);
            scheduler.SetReplayPhaseContext(replayPhase);

            // Seed template with domain 0
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            scheduler.TestSetClassCapacityTemplate(new ClassCapacityTemplate(state));
            scheduler.TestSetClassTemplateValid(true);
            scheduler.TestSetClassTemplateDomainId(0);

            long invalidationsBefore = scheduler.ClassTemplateInvalidations;

            // Run a cycle with different ownerVt (domain changes)
            var candidate = MicroOpTestHelper.CreateScalarALU(2, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(2, candidate);

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(1, destReg: 1, src1Reg: 2, src2Reg: 3);
            // ownerVt=1 but template captured with domainId=0 → domain mismatch
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 1, localCoreId: 0);

            Assert.True(scheduler.ClassTemplateInvalidations > invalidationsBefore);
        }

        [Fact]
        public void WhenReplayPhaseDeactivates_ThenClassTemplateInvalidated()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.TypedSlotEnabled = true;

            var replayPhase = new ReplayPhaseContext(
                isActive: true, epochId: 1, cachedPc: 0x1000, epochLength: 100,
                completedReplays: 1, validSlotCount: 8, stableDonorMask: 0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);
            scheduler.SetReplayPhaseContext(replayPhase);

            // Seed valid template
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            scheduler.TestSetClassCapacityTemplate(new ClassCapacityTemplate(state));
            scheduler.TestSetClassTemplateValid(true);
            scheduler.TestSetClassTemplateDomainId(0);

            Assert.True(scheduler.TestGetClassTemplateValid());

            // Deactivate replay phase
            var inactivePhase = new ReplayPhaseContext(
                isActive: false, epochId: 0, cachedPc: 0, epochLength: 0,
                completedReplays: 0, validSlotCount: 0, stableDonorMask: 0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.Completed);
            scheduler.SetReplayPhaseContext(inactivePhase);

            Assert.False(scheduler.TestGetClassTemplateValid());
        }

        [Fact]
        public void WhenReplayEpochChanges_ThenClassTemplateExpired()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.TypedSlotEnabled = true;

            // Set epoch 1
            var phase1 = new ReplayPhaseContext(
                isActive: true, epochId: 1, cachedPc: 0x1000, epochLength: 100,
                completedReplays: 1, validSlotCount: 8, stableDonorMask: 0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);
            scheduler.SetReplayPhaseContext(phase1);

            // Seed valid template
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            scheduler.TestSetClassCapacityTemplate(new ClassCapacityTemplate(state));
            scheduler.TestSetClassTemplateValid(true);
            scheduler.TestSetClassTemplateDomainId(0);

            // Change to epoch 2
            var phase2 = new ReplayPhaseContext(
                isActive: true, epochId: 2, cachedPc: 0x2000, epochLength: 50,
                completedReplays: 0, validSlotCount: 8, stableDonorMask: 0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);
            scheduler.SetReplayPhaseContext(phase2);

            Assert.False(scheduler.TestGetClassTemplateValid());
        }

        #endregion

        #region Template-Guided Fast Path

        [Fact]
        public void WhenClassTemplateHit_ThenFastPathAcceptsIncremented()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.TypedSlotEnabled = true;

            var replayPhase = new ReplayPhaseContext(
                isActive: true, epochId: 1, cachedPc: 0x1000, epochLength: 100,
                completedReplays: 1, validSlotCount: 8, stableDonorMask: 0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);
            scheduler.SetReplayPhaseContext(replayPhase);

            // Seed a template matching current capacity (all free)
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            state.IncrementOccupancy(SlotClass.AluClass); // 1 ALU occupied = 3 ALU free
            scheduler.TestSetClassCapacityTemplate(new ClassCapacityTemplate(state));
            scheduler.TestSetClassTemplateValid(true);
            scheduler.TestSetClassTemplateDomainId(0);

            long fastPathBefore = scheduler.TypedSlotFastPathAccepts;

            // Inject — should use fast path (template budget has remaining ALU capacity)
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, candidate);

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            Assert.True(scheduler.TypedSlotFastPathAccepts > fastPathBefore);
        }

        #endregion

        #region Two-Level Template Matching

        [Fact]
        public void WhenLevel1ClassMatch_ButLevel2SlotMismatch_ThenPartialReuse()
        {
            // Level 1 (class-capacity) match should still work even when
            // Level 2 (exact slot/donor mask) doesn't match.
            var scheduler = new MicroOpScheduler();
            scheduler.TypedSlotEnabled = true;

            // Active replay phase with specific donor mask
            var replayPhase = new ReplayPhaseContext(
                isActive: true, epochId: 1, cachedPc: 0x1000, epochLength: 100,
                completedReplays: 1, validSlotCount: 6,
                stableDonorMask: 0b_0000_0110, // slots 1,2 are donors
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);
            scheduler.SetReplayPhaseContext(replayPhase);

            // Seed class template — reflects capacity, not exact slots
            var state = new SlotClassCapacityState();
            state.InitializeFromLaneMap();
            state.IncrementOccupancy(SlotClass.AluClass);
            state.IncrementOccupancy(SlotClass.AluClass);
            scheduler.TestSetClassCapacityTemplate(new ClassCapacityTemplate(state));
            scheduler.TestSetClassTemplateValid(true);
            scheduler.TestSetClassTemplateDomainId(0);

            // Run injection — class-level match should succeed (Level 1)
            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, candidate);

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);
            bundle[3] = MicroOpTestHelper.CreateScalarALU(0, destReg: 4, src1Reg: 5, src2Reg: 6);
            var result = scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            // Class template reuse should have been attempted
            Assert.True(scheduler.ClassTemplateReuseHits > 0 || scheduler.ClassTemplateInvalidations > 0);
        }

        #endregion

        #region LoopBuffer Class-Level Donor Capacity

        [Fact]
        public void LoopBuffer_WhenActive_ThenClassDonorCapacityReflectsLaneMap()
        {
            var loopBuffer = new LoopBuffer();
            loopBuffer.Initialize();

            // IsReplayStableDonorSlot: true for null slots AND for non-VectorMicroOp slots.
            // So ScalarALU ops are also donors. To test per-class donor capacity correctly,
            // we need slots that are NOT donors — only VectorMicroOp ops are non-donors.
            // All null slots + non-vector occupied slots become donors.

            // Load a bundle: all slots null → all 8 are donors
            loopBuffer.BeginLoad(pc: 0x1000, totalIterations: 10);
            for (int i = 0; i < 8; i++)
                loopBuffer.StoreSlot(i, null);
            loopBuffer.CommitLoad();

            Assert.Equal(LoopBuffer.BufferState.Active, loopBuffer.State);

            // All null slots are donors → per lane map:
            // lanes 0-3 (ALU), lanes 4-5 (LSU), lane 6 (DMA), lane 7 (Branch+System aliased)
            Assert.Equal(4, loopBuffer.GetClassDonorCapacity(SlotClass.AluClass));
            Assert.Equal(2, loopBuffer.GetClassDonorCapacity(SlotClass.LsuClass));
            Assert.Equal(1, loopBuffer.GetClassDonorCapacity(SlotClass.DmaStreamClass));
            Assert.Equal(1, loopBuffer.GetClassDonorCapacity(SlotClass.BranchControl));
            Assert.Equal(1, loopBuffer.GetClassDonorCapacity(SlotClass.SystemSingleton));
        }

        [Fact]
        public void LoopBuffer_WhenEmpty_ThenClassDonorCapacityIsZero()
        {
            var loopBuffer = new LoopBuffer();
            loopBuffer.Initialize();

            Assert.Equal(0, loopBuffer.GetClassDonorCapacity(SlotClass.AluClass));
            Assert.Equal(0, loopBuffer.GetClassDonorCapacity(SlotClass.LsuClass));
        }

        [Fact]
        public void LoopBuffer_WhenInvalidated_ThenClassDonorCapacityResets()
        {
            var loopBuffer = new LoopBuffer();
            loopBuffer.Initialize();

            loopBuffer.BeginLoad(pc: 0x1000, totalIterations: 10);
            for (int i = 0; i < 8; i++)
                loopBuffer.StoreSlot(i, null); // all donors
            loopBuffer.CommitLoad();

            Assert.True(loopBuffer.GetClassDonorCapacity(SlotClass.AluClass) > 0);

            loopBuffer.Invalidate(ReplayPhaseInvalidationReason.Manual);

            Assert.Equal(0, loopBuffer.GetClassDonorCapacity(SlotClass.AluClass));
        }

        #endregion

        #region ReplayEngine Counterexample Types

        [Fact]
        public void OracleGapCategory_ClassCapacityDivergence_ThenExists()
        {
            var category = OracleGapCategory.ClassCapacityDivergence;
            Assert.Equal(6, (int)category);
        }

        [Fact]
        public void CounterexampleEvidenceSummary_WhenClassCapacityDivergence_ThenCorrectDominant()
        {
            var summary = new HybridCPU_ISE.Core.CounterexampleEvidenceSummary(
                totalCounterexamples: 5,
                donorRestrictionCount: 1,
                fairnessOrderingCount: 0,
                legalityConservatismCount: 0,
                domainIsolationCount: 0,
                speculationBudgetCount: 0,
                totalMissedSlots: 10,
                classCapacityDivergenceCount: 4);

            Assert.Equal(OracleGapCategory.ClassCapacityDivergence, summary.DominantCategory);
            Assert.Equal(4, summary.ClassCapacityDivergenceCount);
        }

        [Fact]
        public void CounterexampleEvidenceSummary_Describe_IncludesClassCapacity()
        {
            var summary = new HybridCPU_ISE.Core.CounterexampleEvidenceSummary(
                totalCounterexamples: 1,
                donorRestrictionCount: 0,
                fairnessOrderingCount: 0,
                legalityConservatismCount: 0,
                domainIsolationCount: 0,
                speculationBudgetCount: 0,
                totalMissedSlots: 2,
                classCapacityDivergenceCount: 1);

            string description = summary.Describe();
            Assert.Contains("classCapacity=1", description);
        }

        #endregion

        #region Existing Test Regression — TypedSlotEnabled=false

        [Fact]
        public void WhenTypedSlotDisabled_ThenNoPhase07CountersChanged()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.TypedSlotEnabled = false;

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, candidate);

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);
            scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0);

            Assert.Equal(0, scheduler.ClassTemplateReuseHits);
            Assert.Equal(0, scheduler.ClassTemplateInvalidations);
            Assert.Equal(0, scheduler.TypedSlotFastPathAccepts);
        }

        #endregion

        #region Template Stability Across Replay Iterations

        [Fact]
        public void WhenSameReplayEpoch_AndSameCapacity_ThenTemplateStaysValid()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.TypedSlotEnabled = true;

            var replayPhase = new ReplayPhaseContext(
                isActive: true, epochId: 1, cachedPc: 0x1000, epochLength: 100,
                completedReplays: 0, validSlotCount: 8, stableDonorMask: 0,
                lastInvalidationReason: ReplayPhaseInvalidationReason.None);
            scheduler.SetReplayPhaseContext(replayPhase);

            // First cycle — captures template
            var candidate1 = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            scheduler.NominateSmtCandidate(1, candidate1);
            var bundle1 = new MicroOp[8];
            bundle1[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);
            scheduler.PackBundleIntraCoreSmt(bundle1, ownerVirtualThreadId: 0, localCoreId: 0);

            Assert.True(scheduler.TestGetClassTemplateValid());

            // Second cycle — same epoch, same capacity
            var candidate2 = MicroOpTestHelper.CreateScalarALU(2, destReg: 30, src1Reg: 31, src2Reg: 32);
            scheduler.NominateSmtCandidate(2, candidate2);
            var bundle2 = new MicroOp[8];
            bundle2[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 5, src1Reg: 6, src2Reg: 7);
            scheduler.PackBundleIntraCoreSmt(bundle2, ownerVirtualThreadId: 0, localCoreId: 0);

            Assert.True(scheduler.TestGetClassTemplateValid());
        }

        #endregion
    }
}
