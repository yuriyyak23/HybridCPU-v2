#if TESTING
using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Diagnostics;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// TEST-ONLY partial extension for MicroOpScheduler.
    ///
    /// Purpose: Enables performance testing without requiring real multi-threaded execution.
    /// This code is ONLY for testing and does NOT modify ISA, encoding, or production behavior.
    ///
    /// Design rationale:
    /// - Tests need to simulate background thread nomination without complex threading
    /// - Production code remains unchanged (main MicroOpScheduler.cs)
    /// - TestMode flag gates all test-specific behavior
    /// - No changes to instruction encoding, VLIW slots, or ISA semantics
    ///
    /// Created: 2026-03-02
    /// For: Performance test infrastructure (testPerfPlan.md Iteration 2)
    /// </summary>
    public partial class MicroOpScheduler
    {
        #region Test-Only Fields

        /// <summary>
        /// TEST-ONLY: Enable test mode for synthetic nomination.
        /// When true, scheduler-side inter-core nomination helpers rebuild a synthetic
        /// visible nomination snapshot from <see cref="TestNominationQueues"/> instead of
        /// reading the live inter-core nomination ports directly.
        /// Default: false (production behavior unchanged).
        /// </summary>
        internal bool TestMode { get; set; }

        /// <summary>
        /// TEST-ONLY: Synthetic nomination queues for each virtual thread [0..3].
        /// Used only when TestMode == true. Scheduler-side inter-core helpers expose the
        /// head of each queue as a synthetic visible nomination source without requiring
        /// threaded producers or live core-port writes.
        /// </summary>
        internal Queue<MicroOp>[] TestNominationQueues { get; private set; }

        /// <summary>
        /// TEST-ONLY: Detailed rejection reason for last failed injection.
        /// Helps tests understand why a candidate was rejected.
        /// </summary>
        internal string LastRejectionReason { get; private set; } = "";

        /// <summary>
        /// TEST-ONLY: Counter for rejections due to resource conflicts.
        /// Incremented when CanInject() returns false due to register/resource overlap.
        /// </summary>
        internal long TestRejectedDueToResourceConflict { get; private set; }

        /// <summary>
        /// TEST-ONLY: Counter for rejections due to memory conflicts.
        /// Incremented when CanInject() returns false due to memory domain overlap.
        /// </summary>
        internal long TestRejectedDueToMemoryConflict { get; private set; }

        /// <summary>
        /// TEST-ONLY: Counter for rejections due to safety mask conflicts.
        /// Incremented when the runtime legality checker rejects due to mask overlap.
        /// </summary>
        internal long TestRejectedDueToSafetyMask { get; private set; }

        /// <summary>
        /// TEST-ONLY: Counter for rejections due to full bundle (no stealable slots).
        /// </summary>
        internal long TestRejectedDueToFullBundle { get; private set; }

        #endregion

        #region Test-Only Methods

        /// <summary>
        /// TEST-ONLY: Initialize test mode infrastructure.
        /// Call this before using TestMode in tests.
        /// </summary>
        internal void InitializeTestMode()
        {
            TestMode = true;
            TestNominationQueues = new Queue<MicroOp>[4];
            for (int i = 0; i < 4; i++)
            {
                TestNominationQueues[i] = new Queue<MicroOp>();
            }
            ResetTestCounters();
        }

        /// <summary>
        /// TEST-ONLY: Enqueue a micro-op for a specific virtual thread.
        /// The head element of each queue becomes visible through the synthetic
        /// inter-core nomination snapshot used by scheduler test mode.
        /// </summary>
        /// <param name="vtId">Virtual thread ID (0-3)</param>
        /// <param name="op">Micro-op to nominate</param>
        internal void TestEnqueueMicroOp(int vtId, MicroOp op)
        {
            if (!TestMode)
            {
                throw new InvalidOperationException(
                    "TestEnqueueMicroOp can only be called when TestMode is enabled. " +
                    "Call InitializeTestMode() first.");
            }

            if (vtId < 0 || vtId >= 4)
            {
                throw new ArgumentOutOfRangeException(nameof(vtId),
                    "Virtual thread ID must be 0-3 for 4-way SMT.");
            }

            if (op == null)
            {
                throw new ArgumentNullException(nameof(op),
                    "Cannot enqueue null micro-op.");
            }

            TestNominationQueues[vtId].Enqueue(op);
        }

        /// <summary>
        /// TEST-ONLY: Record rejection reason for diagnostics.
        /// </summary>
        private void TestRecordRejection(string reason, RejectionType type)
        {
            if (!TestMode)
                return;

            LastRejectionReason = reason;

            switch (type)
            {
                case RejectionType.ResourceConflict:
                    TestRejectedDueToResourceConflict++;
                    break;
                case RejectionType.MemoryConflict:
                    TestRejectedDueToMemoryConflict++;
                    break;
                case RejectionType.SafetyMask:
                    TestRejectedDueToSafetyMask++;
                    break;
                case RejectionType.FullBundle:
                    TestRejectedDueToFullBundle++;
                    break;
            }
        }

        /// <summary>
        /// TEST-ONLY: Reset all test-specific counters.
        /// </summary>
        internal void ResetTestCounters()
        {
            TestRejectedDueToResourceConflict = 0;
            TestRejectedDueToMemoryConflict = 0;
            TestRejectedDueToSafetyMask = 0;
            TestRejectedDueToFullBundle = 0;
            LastRejectionReason = "";
        }

        /// <summary>
        /// TEST-ONLY: Get count of pending nominations across all VTs.
        /// </summary>
        internal int TestGetPendingNominationCount()
        {
            if (!TestMode || TestNominationQueues == null)
                return 0;

            int total = 0;
            for (int i = 0; i < 4; i++)
            {
                total += TestNominationQueues[i].Count;
            }
            return total;
        }

        /// <summary>
        /// TEST-ONLY: Clear all nomination queues.
        /// </summary>
        internal void TestClearNominationQueues()
        {
            if (!TestMode || TestNominationQueues == null)
                return;

            for (int i = 0; i < 4; i++)
            {
                TestNominationQueues[i].Clear();
            }
        }

        /// <summary>
        /// TEST-ONLY: Get the current fairness credit value for a virtual thread.
        /// </summary>
        internal int TestGetFairnessCredit(int vtId)
        {
            if ((uint)vtId >= 4) return 0;
            return _fairnessCredits[vtId];
        }

        /// <summary>
        /// TEST-ONLY: Set the fairness credit for a virtual thread (for seeding tests).
        /// </summary>
        internal void TestSetFairnessCredit(int vtId, int credit)
        {
            if ((uint)vtId >= 4) return;
            _fairnessCredits[vtId] = credit;
        }

        /// <summary>
        /// TEST-ONLY: Get per-VT injection count for fairness distribution analysis.
        /// </summary>
        internal long TestGetPerVtInjections(int vtId)
        {
            if ((uint)vtId >= 4) return 0;
            return _perVtInjections[vtId];
        }

        /// <summary>
        /// TEST-ONLY: Get per-VT rejection count for Phase 3 telemetry assertions.
        /// </summary>
        internal long TestGetPerVtRejections(int vtId)
        {
            return vtId switch
            {
                0 => RejectionsVT0,
                1 => RejectionsVT1,
                2 => RejectionsVT2,
                3 => RejectionsVT3,
                _ => 0
            };
        }

        /// <summary>
        /// TEST-ONLY: Get per-VT register-group conflict count for Phase 3 telemetry assertions.
        /// </summary>
        internal long TestGetPerVtRegGroupConflicts(int vtId)
        {
            return vtId switch
            {
                0 => RegGroupConflictsVT0,
                1 => RegGroupConflictsVT1,
                2 => RegGroupConflictsVT2,
                3 => RegGroupConflictsVT3,
                _ => 0
            };
        }

        /// <summary>
        /// TEST-ONLY: Get the current speculation budget level.
        /// </summary>
        internal int TestGetSpeculationBudget()
        {
            return _speculationBudget;
        }

        /// <summary>
        /// TEST-ONLY: Set the speculation budget level (for seeding tests).
        /// </summary>
        internal void TestSetSpeculationBudget(int budget)
        {
            _speculationBudget = budget;
        }

        /// <summary>
        /// TEST-ONLY: Seed Phase 1 replay/certificate metrics so runtime trace emission can exercise realistic evidence paths.
        /// </summary>
        internal void TestSetPhaseMetrics(SchedulerPhaseMetrics metrics)
        {
            ReplayAwareCycles = metrics.ReplayAwareCycles;
            PhaseCertificateReadyHits = metrics.PhaseCertificateReadyHits;
            PhaseCertificateReadyMisses = metrics.PhaseCertificateReadyMisses;
            EstimatedPhaseCertificateChecksSaved = metrics.EstimatedChecksSaved;
            PhaseCertificateInvalidations = metrics.PhaseCertificateInvalidations;
            PhaseCertificateMutationInvalidations = metrics.PhaseCertificateMutationInvalidations;
            PhaseCertificatePhaseMismatchInvalidations = metrics.PhaseCertificatePhaseMismatchInvalidations;
            LastPhaseCertificateInvalidationReason = metrics.LastCertificateInvalidationReason;
            DeterminismReferenceOpportunitySlots = metrics.DeterminismReferenceOpportunitySlots;
            DeterminismReplayEligibleSlots = metrics.DeterminismReplayEligibleSlots;
            DeterminismMaskedSlots = metrics.DeterminismMaskedSlots;
            DeterminismEstimatedLostSlots = metrics.DeterminismEstimatedLostSlots;
            DeterminismConstrainedCycles = metrics.DeterminismConstrainedCycles;
            DomainIsolationProbeAttempts = metrics.DomainIsolationProbeAttempts;
            DomainIsolationBlockedAttempts = metrics.DomainIsolationBlockedAttempts;
            DomainIsolationCrossDomainBlocks = metrics.DomainIsolationCrossDomainBlocks;
            DomainIsolationKernelToUserBlocks = metrics.DomainIsolationKernelToUserBlocks;
            EligibilityMaskedCycles = metrics.EligibilityMaskedCycles;
            EligibilityMaskedReadyCandidates = metrics.EligibilityMaskedReadyCandidates;
            _lastSmtEligibilitySnapshot = new SmtEligibilitySnapshot(
                metrics.LastEligibilityRequestedMask,
                metrics.LastEligibilityNormalizedMask,
                metrics.LastEligibilityReadyPortMask,
                metrics.LastEligibilityVisibleReadyMask,
                metrics.LastEligibilityMaskedReadyMask);
            ClassTemplateReuseHits = metrics.ClassTemplateReuseHits;
            ClassTemplateInvalidations = metrics.ClassTemplateInvalidations;
            TypedSlotFastPathAccepts = metrics.TypedSlotFastPathAccepts;
            TypedSlotStandardPathAccepts = metrics.TypedSlotStandardPathAccepts;
            TotalLaneBindings = metrics.TotalLaneBindings;
            LaneReuseHits = metrics.LaneReuseHits;
            LaneReuseMisses = metrics.LaneReuseMisses;
            AluClassInjects = metrics.AluClassInjects;
            LsuClassInjects = metrics.LsuClassInjects;
            DmaStreamClassInjects = metrics.DmaStreamClassInjects;
            BranchControlInjects = metrics.BranchControlInjects;
            HardPinnedInjects = metrics.HardPinnedInjects;
            ClassFlexibleInjects = metrics.ClassFlexibleInjects;
            NopAvoided = metrics.NopAvoided;
            NopDueToPinnedConstraint = metrics.NopDueToPinnedConstraint;
            NopDueToNoClassCapacity = metrics.NopDueToNoClassCapacity;
            NopDueToResourceConflict = metrics.NopDueToResourceConflict;
            NopDueToDynamicState = metrics.NopDueToDynamicState;
            StaticClassOvercommitRejects = metrics.StaticClassOvercommitRejects;
            DynamicClassExhaustionRejects = metrics.DynamicClassExhaustionRejects;
            PinnedLaneConflicts = metrics.PinnedLaneConflicts;
            LateBindingConflicts = metrics.LateBindingConflicts;
            TypedSlotDomainRejects = metrics.TypedSlotDomainRejects;
            SmtOwnerContextGuardRejects = metrics.SmtOwnerContextGuardRejects;
            SmtDomainGuardRejects = metrics.SmtDomainGuardRejects;
            SmtBoundaryGuardRejects = metrics.SmtBoundaryGuardRejects;
            SmtSharedResourceCertificateRejects = metrics.SmtSharedResourceCertificateRejects;
            SmtRegisterGroupCertificateRejects = metrics.SmtRegisterGroupCertificateRejects;
            _lastSmtLegalityRejectKind = metrics.LastSmtLegalityRejectKind;
            _lastSmtLegalityAuthoritySource = metrics.LastSmtLegalityAuthoritySource;
            SmtLegalityRejectByAluClass = metrics.SmtLegalityRejectByAluClass;
            SmtLegalityRejectByLsuClass = metrics.SmtLegalityRejectByLsuClass;
            SmtLegalityRejectByDmaStreamClass = metrics.SmtLegalityRejectByDmaStreamClass;
            SmtLegalityRejectByBranchControl = metrics.SmtLegalityRejectByBranchControl;
            SmtLegalityRejectBySystemSingleton = metrics.SmtLegalityRejectBySystemSingleton;
            ClassTemplateDomainInvalidations = metrics.ClassTemplateDomainInvalidations;
            ClassTemplateCapacityMismatchInvalidations = metrics.ClassTemplateCapacityMismatchInvalidations;
            CertificateRejectByAluClass = metrics.CertificateRejectByAluClass;
            CertificateRejectByLsuClass = metrics.CertificateRejectByLsuClass;
            CertificateRejectByDmaStreamClass = metrics.CertificateRejectByDmaStreamClass;
            CertificateRejectByBranchControl = metrics.CertificateRejectByBranchControl;
            CertificateRejectBySystemSingleton = metrics.CertificateRejectBySystemSingleton;
            RejectionsVT0 = metrics.RejectionsVT0;
            RejectionsVT1 = metrics.RejectionsVT1;
            RejectionsVT2 = metrics.RejectionsVT2;
            RejectionsVT3 = metrics.RejectionsVT3;
            RegGroupConflictsVT0 = metrics.RegGroupConflictsVT0 != 0 ? metrics.RegGroupConflictsVT0 : metrics.CertificateRegGroupConflictVT0;
            RegGroupConflictsVT1 = metrics.RegGroupConflictsVT1 != 0 ? metrics.RegGroupConflictsVT1 : metrics.CertificateRegGroupConflictVT1;
            RegGroupConflictsVT2 = metrics.RegGroupConflictsVT2 != 0 ? metrics.RegGroupConflictsVT2 : metrics.CertificateRegGroupConflictVT2;
            RegGroupConflictsVT3 = metrics.RegGroupConflictsVT3 != 0 ? metrics.RegGroupConflictsVT3 : metrics.CertificateRegGroupConflictVT3;
            BankPendingRejectBank0 = metrics.BankPendingRejectBank0;
            BankPendingRejectBank1 = metrics.BankPendingRejectBank1;
            BankPendingRejectBank2 = metrics.BankPendingRejectBank2;
            BankPendingRejectBank3 = metrics.BankPendingRejectBank3;
            BankPendingRejectBank4 = metrics.BankPendingRejectBank4;
            BankPendingRejectBank5 = metrics.BankPendingRejectBank5;
            BankPendingRejectBank6 = metrics.BankPendingRejectBank6;
            BankPendingRejectBank7 = metrics.BankPendingRejectBank7;
            BankPendingRejectBank8 = metrics.BankPendingRejectBank8;
            BankPendingRejectBank9 = metrics.BankPendingRejectBank9;
            BankPendingRejectBank10 = metrics.BankPendingRejectBank10;
            BankPendingRejectBank11 = metrics.BankPendingRejectBank11;
            BankPendingRejectBank12 = metrics.BankPendingRejectBank12;
            BankPendingRejectBank13 = metrics.BankPendingRejectBank13;
            BankPendingRejectBank14 = metrics.BankPendingRejectBank14;
            BankPendingRejectBank15 = metrics.BankPendingRejectBank15;
            MemoryClusteringEvents = metrics.MemoryClusteringEvents;
            TypedSlotHardwareBudgetRejects = metrics.TypedSlotHardwareBudgetRejects;
            TypedSlotAssistQuotaRejects = metrics.TypedSlotAssistQuotaRejects;
            TypedSlotAssistBackpressureRejects = metrics.TypedSlotAssistBackpressureRejects;
            AssistNominationCount = metrics.AssistNominations;
            AssistInjectionsCount = metrics.AssistInjections;
            AssistRejects = metrics.AssistRejects;
            AssistBoundaryRejects = metrics.AssistBoundaryRejects;
            AssistInvalidations = metrics.AssistInvalidations;
            AssistInterCoreNominations = metrics.AssistInterCoreNominations;
            AssistInterCoreInjections = metrics.AssistInterCoreInjections;
            AssistInterCoreRejects = metrics.AssistInterCoreRejects;
            AssistInterCoreDomainRejects = metrics.AssistInterCoreDomainRejects;
            AssistInterCorePodLocalInjections = metrics.AssistInterCorePodLocalInjections;
            AssistInterCoreCrossPodInjections = metrics.AssistInterCoreCrossPodInjections;
            AssistInterCorePodLocalRejects = metrics.AssistInterCorePodLocalRejects;
            AssistInterCoreCrossPodRejects = metrics.AssistInterCoreCrossPodRejects;
            AssistInterCorePodLocalDomainRejects = metrics.AssistInterCorePodLocalDomainRejects;
            AssistInterCoreCrossPodDomainRejects = metrics.AssistInterCoreCrossPodDomainRejects;
            AssistInterCoreSameVtVectorInjects = metrics.AssistInterCoreSameVtVectorInjects;
            AssistInterCoreDonorVtVectorInjects = metrics.AssistInterCoreDonorVtVectorInjects;
            AssistInterCoreSameVtVectorWritebackInjects = metrics.AssistInterCoreSameVtVectorWritebackInjects;
            AssistInterCoreDonorVtVectorWritebackInjects = metrics.AssistInterCoreDonorVtVectorWritebackInjects;
                AssistInterCoreLane6DefaultStoreDonorPrefetchInjects = metrics.AssistInterCoreLane6DefaultStoreDonorPrefetchInjects;
            AssistInterCoreLane6HotLoadDonorPrefetchInjects = metrics.AssistInterCoreLane6HotLoadDonorPrefetchInjects;
            AssistInterCoreLane6HotStoreDonorPrefetchInjects = metrics.AssistInterCoreLane6HotStoreDonorPrefetchInjects;
            AssistInterCoreLane6DonorPrefetchInjects = metrics.AssistInterCoreLane6DonorPrefetchInjects;
            AssistInterCoreLane6ColdStoreLdsaInjects = metrics.AssistInterCoreLane6ColdStoreLdsaInjects;
            AssistInterCoreLane6LdsaInjects = metrics.AssistInterCoreLane6LdsaInjects;
            AssistQuotaRejects = metrics.AssistQuotaRejects;
            AssistQuotaIssueRejects = metrics.AssistQuotaIssueRejects;
            AssistQuotaLineRejects = metrics.AssistQuotaLineRejects;
            AssistQuotaLinesReserved = metrics.AssistQuotaLinesReserved;
            AssistBackpressureRejects = metrics.AssistBackpressureRejects;
            AssistBackpressureOuterCapRejects = metrics.AssistBackpressureOuterCapRejects;
            AssistBackpressureMshrRejects = metrics.AssistBackpressureMshrRejects;
            AssistBackpressureDmaSrfRejects = metrics.AssistBackpressureDmaSrfRejects;
            AssistDonorPrefetchInjects = metrics.AssistDonorPrefetchInjects;
            AssistLdsaInjects = metrics.AssistLdsaInjects;
            AssistVdsaInjects = metrics.AssistVdsaInjects;
            AssistSameVtInjects = metrics.AssistSameVtInjects;
            AssistDonorVtInjects = metrics.AssistDonorVtInjects;
            LastAssistInvalidationReason = metrics.LastAssistInvalidationReason;
            LastAssistOwnershipSignature = metrics.LastAssistOwnershipSignature;

            _loopPhaseTrackers.Clear();
            _loopPhaseEntryCounts.Clear();
            if (metrics.LoopPhaseProfiles is not null)
            {
                foreach (LoopPhaseClassProfile profile in metrics.LoopPhaseProfiles)
                {
                    _loopPhaseEntryCounts[profile.LoopPcAddress] = profile.IterationsSampled;
                    _loopPhaseTrackers[profile.LoopPcAddress] = new LoopPhaseTracker(profile.LoopPcAddress, profile.IterationsSampled);
                }
            }
        }

        /// <summary>
        /// TEST-ONLY: Get the current exported loop-phase profiles.
        /// </summary>
        internal IReadOnlyList<LoopPhaseClassProfile> TestGetLoopPhaseProfiles()
        {
            return BuildLoopPhaseProfiles() ?? Array.Empty<LoopPhaseClassProfile>();
        }

        /// <summary>
        /// TEST-ONLY: Read the currently published replay-phase context.
        /// </summary>
        internal ReplayPhaseContext TestGetReplayPhaseContext()
        {
            return _currentReplayPhase;
        }

        #endregion

        #region Test-Only Phase 03 Accessors

        /// <summary>
        /// TEST-ONLY: Expose TryClassAdmission for direct unit testing.
        /// </summary>
        internal bool TestTryClassAdmission(
            MicroOp candidate,
            BundleResourceCertificate4Way cert4Way,
            MicroOp[]? workingBundle,
            int ownerVirtualThreadId,
            int currentPassInjections,
            out TypedSlotRejectReason rejectReason,
            bool templateHitThisCycle = false,
            TemplateBudget templateBudget = default)
        {
            ClassTemplateAdmissionState templateState = templateHitThisCycle
                ? ClassTemplateAdmissionState.Create(templateBudget)
                : default;
            SmtBundleMetadata4Way bundleMetadata = SmtBundleMetadata4Way.Empty(ownerVirtualThreadId);
            BoundaryGuardState boundaryGuard = BoundaryGuardState.Open(_serializingEpochCounter);
            foreach (MicroOp op in workingBundle ?? Array.Empty<MicroOp>())
            {
                bundleMetadata = bundleMetadata.WithOperation(op);
                boundaryGuard = boundaryGuard.WithOperation(op);
            }

            return TryClassAdmission(
                candidate,
                ref cert4Way,
                bundleMetadata,
                boundaryGuard,
                ref templateState,
                ownerVirtualThreadId,
                currentPassInjections,
                out rejectReason);
        }

        /// <summary>
        /// TEST-ONLY: Expose TryMaterializeLane for direct unit testing.
        /// </summary>
        internal bool TestTryMaterializeLane(
            MicroOp candidate,
            byte bundleOccupancy,
            out int selectedLane,
            out TypedSlotRejectReason rejectReason)
        {
            return TryMaterializeLane(candidate, bundleOccupancy, out selectedLane, out rejectReason);
        }

        /// <summary>
        /// TEST-ONLY: Expose ComputeBundleOccupancy for unit testing.
        /// </summary>
        internal static byte TestComputeBundleOccupancy(MicroOp[] bundle)
        {
            return ComputeBundleOccupancy(bundle);
        }

        /// <summary>
        /// TEST-ONLY: Set internal class-capacity state (for seeding tests).
        /// </summary>
        internal void TestSetClassCapacity(SlotClassCapacityState state)
        {
            _classCapacity = state;
        }

        #endregion

        #region Test-Only Phase 04 Accessors

        /// <summary>
        /// TEST-ONLY: Get the previous-lane record for a class (for Tier 2 testing).
        /// </summary>
        internal int TestGetPreviousPhaseLane(SlotClass slotClass)
        {
            return GetPreviousPhaseLane(slotClass);
        }

        /// <summary>
        /// TEST-ONLY: Set the previous-lane record for a class (for seeding Tier 2 tests).
        /// </summary>
        internal void TestSetPreviousPhaseLane(SlotClass slotClass, int lane)
        {
            RecordPhaseLane(slotClass, lane);
        }

        /// <summary>
        /// TEST-ONLY: Reset all per-class previous-lane records (for test isolation).
        /// </summary>
        internal void TestResetPreviousPhaseLanes()
        {
            ResetPreviousPhaseLanes();
        }

        #endregion

        #region Test-Only Phase 06 Accessors

        /// <summary>
        /// TEST-ONLY: Get the last <see cref="CertificateRejectDetail"/> captured
        /// by the diagnostics path in <see cref="TryClassAdmission"/>.
        /// </summary>
        internal CertificateRejectDetail TestGetLastCertificateRejectDetail()
        {
            return _lastCertificateRejectDetail;
        }

        /// <summary>
        /// TEST-ONLY: Get the last <see cref="TypedSlotRejectClassification"/> computed
        /// by <see cref="RecordTypedSlotReject(TypedSlotRejectReason, MicroOp)"/>.
        /// </summary>
        internal TypedSlotRejectClassification TestGetLastRejectClassification()
        {
            return _lastRejectClassification;
        }

        /// <summary>
        /// TEST-ONLY: Get a readonly snapshot of the FSP pipeline register placement per VT.
        /// Placement is rebuilt from an explicit SMT nomination snapshot rather than from
        /// shadow placement metadata in <c>_fspPipelineReg</c>.
        /// </summary>
        internal (bool Valid, SlotPlacementMetadata Placement) TestGetFspPipelinePlacement(int vt)
        {
            if ((uint)vt >= SMT_WAYS)
                return default;

            ref var reg = ref _fspPipelineReg[vt];
            if (!reg.Valid)
            {
                return default;
            }

            SmtNominationSnapshot nominationSnapshot = CaptureSmtNominationSnapshot(AllEligibleVirtualThreadMask);
            if (!nominationSnapshot.TryGetCandidate(reg.VirtualThreadId, out MicroOp candidate))
            {
                return (true, SlotPlacementMetadata.Default with
                {
                    RequiredSlotClass = SlotClass.Unclassified,
                    PinningKind = SlotPinningKind.ClassFlexible
                });
            }

            return (true, candidate.Placement);
        }

        /// <summary>
        /// TEST-ONLY: Returns the most recent eligibility snapshot captured while
        /// building an SMT nomination state from an explicit runnable VT mask.
        /// </summary>
        internal SmtEligibilitySnapshot TestGetLastSmtEligibilitySnapshot()
        {
            return _lastSmtEligibilitySnapshot;
        }

        #endregion

        #region Test-Only Phase 07 Accessors

        /// <summary>
        /// TEST-ONLY: Get the current class-capacity template.
        /// </summary>
        internal ClassCapacityTemplate TestGetClassCapacityTemplate()
        {
            return _classCapacityTemplate;
        }

        /// <summary>
        /// TEST-ONLY: Set the class-capacity template (for seeding tests).
        /// </summary>
        internal void TestSetClassCapacityTemplate(ClassCapacityTemplate template)
        {
            _classCapacityTemplate = template;
        }

        /// <summary>
        /// TEST-ONLY: Get whether the class-capacity template is valid.
        /// </summary>
        internal bool TestGetClassTemplateValid()
        {
            return _classTemplateValid;
        }

        /// <summary>
        /// TEST-ONLY: Set whether the class-capacity template is valid (for seeding tests).
        /// </summary>
        internal void TestSetClassTemplateValid(bool valid)
        {
            _classTemplateValid = valid;
        }

        /// <summary>
        /// TEST-ONLY: Get the domain scope ID associated with the class template.
        /// </summary>
        internal int TestGetClassTemplateDomainId()
        {
            return _classTemplateDomainId;
        }

        /// <summary>
        /// TEST-ONLY: Set the domain scope ID for the class template (for seeding tests).
        /// </summary>
        internal void TestSetClassTemplateDomainId(int domainId)
        {
            _classTemplateDomainId = domainId;
        }

        /// <summary>
        /// TEST-ONLY: Build explicit class-template admission state for the current scheduler state.
        /// Returns whether the class-template fast path is active for <paramref name="currentDomainId"/>.
        /// </summary>
        internal bool TestTryPrepareClassTemplateAdmissionState(int currentDomainId, out TemplateBudget templateBudget)
        {
            ClassTemplateAdmissionState templateState = PrepareClassTemplateAdmissionState(currentDomainId);
            templateBudget = templateState.TemplateBudgetSnapshot;
            return templateState.TemplateHit;
        }

        #endregion

        #region Test-Only Phase 08 Accessors

        /// <summary>
        /// TEST-ONLY: Invoke RecordTypedSlotInject for unit testing.
        /// </summary>
        internal void TestRecordTypedSlotInject(MicroOp candidate, int lane)
        {
            RecordTypedSlotInject(candidate, lane);
        }

        /// <summary>
        /// TEST-ONLY: Invoke RecordTypedSlotReject (base overload) for unit testing.
        /// </summary>
        internal void TestRecordTypedSlotReject(TypedSlotRejectReason reason)
        {
            if (reason == TypedSlotRejectReason.BankPendingReject)
            {
                RecordBankPendingReject(-1);
                return;
            }

            if (reason == TypedSlotRejectReason.HardwareBudgetReject)
            {
                TypedSlotHardwareBudgetRejects++;
                SmtRejectionsCount++;
                NopDueToDynamicState++;
                return;
            }

            RecordTypedSlotReject(reason);
        }

        /// <summary>
        /// TEST-ONLY: Record a bank-pending reject with a specific bank ID.
        /// </summary>
        internal void TestRecordBankPendingReject(int memoryBankId)
        {
            RecordBankPendingReject(memoryBankId);
        }

        /// <summary>
        /// TEST-ONLY: Record a decode-side memory clustering event.
        /// </summary>
        internal void TestRecordMemoryClusteringEvent()
        {
            RecordMemoryClusteringEvent();
        }

        /// <summary>
        /// TEST-ONLY: Invoke metadata-aware reject accounting with a seeded certificate detail.
        /// </summary>
        internal void TestRecordTypedSlotReject(
            TypedSlotRejectReason reason,
            MicroOp candidate,
            CertificateRejectDetail certificateRejectDetail)
        {
            _lastCertificateRejectDetail = certificateRejectDetail;
            RecordTypedSlotReject(reason, candidate);
        }

        /// <summary>
        /// TEST-ONLY: Invoke InvalidateClassTemplate for unit testing.
        /// </summary>
        internal void TestInvalidateClassTemplate(ReplayPhaseInvalidationReason reason)
        {
            InvalidateClassTemplate(reason);
        }

        /// <summary>
        /// TEST-ONLY: Check whether a valid candidate has been nominated to the given
        /// inter-core FSP port (indexed by core ID, 0–15).
        /// Rebuilt from an explicit inter-core nomination snapshot.
        /// </summary>
        /// <param name="coreId">Core nomination port to query (0–15).</param>
        /// <returns><c>true</c> if the port has a valid candidate.</returns>
        internal bool HasNominatedCandidate(int coreId)
        {
            if ((uint)coreId >= NUM_PORTS) return false;
            return CaptureInterCoreNominationSnapshot().IsReadyCandidate(coreId);
        }

        #endregion

        #region Test-Only Enums

        /// <summary>
        /// TEST-ONLY: Rejection reason categories for diagnostics.
        /// </summary>
        private enum RejectionType
        {
            ResourceConflict,
            MemoryConflict,
            SafetyMask,
            FullBundle
        }

        #endregion
    }
}
#endif
