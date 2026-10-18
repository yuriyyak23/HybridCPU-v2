using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using HybridCPU_ISE.Tests.TestHelpers;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Phase 08 – Telemetry and Reject Taxonomy tests.
    /// Validates disaggregated reject counters, per-class distribution,
    /// NOP tracking, template invalidation sub-counts, and derived metrics.
    /// </summary>
    public class TelemetryRejectTaxonomyTests
    {
        #region Helpers

        private static MicroOp CreateAluCandidate(int vtId = 1)
        {
            var op = MicroOpTestHelper.CreateScalarALU(vtId, destReg: 20, src1Reg: 21, src2Reg: 22);
            op.Placement = op.Placement with
            {
                RequiredSlotClass = SlotClass.AluClass,
                PinningKind = SlotPinningKind.ClassFlexible
            };
            return op;
        }

        private static MicroOp CreateLsuCandidate(int vtId = 1)
        {
            var op = MicroOpTestHelper.CreateLoad(vtId, destReg: 30, address: 0x1000);
            op.Placement = op.Placement with
            {
                RequiredSlotClass = SlotClass.LsuClass,
                PinningKind = SlotPinningKind.ClassFlexible
            };
            return op;
        }

        private static MicroOp CreateDmaStreamCandidate(int vtId = 1)
        {
            var op = MicroOpTestHelper.CreateScalarALU(vtId, destReg: 40, src1Reg: 41, src2Reg: 42);
            op.Placement = op.Placement with
            {
                RequiredSlotClass = SlotClass.DmaStreamClass,
                PinningKind = SlotPinningKind.ClassFlexible
            };
            return op;
        }

        private static MicroOp CreateBranchControlCandidate(int vtId = 1)
        {
            var op = MicroOpTestHelper.CreateScalarALU(vtId, destReg: 50, src1Reg: 51, src2Reg: 52);
            op.Placement = op.Placement with
            {
                RequiredSlotClass = SlotClass.BranchControl,
                PinningKind = SlotPinningKind.ClassFlexible
            };
            return op;
        }

        private static MicroOp CreateHardPinnedCandidate(int vtId = 1, byte pinnedLane = 0)
        {
            var op = MicroOpTestHelper.CreateScalarALU(vtId, destReg: 60, src1Reg: 61, src2Reg: 62);
            op.Placement = op.Placement with
            {
                RequiredSlotClass = SlotClass.AluClass,
                PinningKind = SlotPinningKind.HardPinned,
                PinnedLaneId = pinnedLane
            };
            return op;
        }

        #endregion

        #region Disaggregated Reject Counters

        [Fact]
        public void RecordTypedSlotReject_StaticClassOvercommit_IncrementsDisaggregatedCounter()
        {
            var scheduler = new MicroOpScheduler();

            scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.StaticClassOvercommit);

            Assert.Equal(1, scheduler.StaticClassOvercommitRejects);
            Assert.Equal(1, scheduler.ClassCapacityRejects);
            Assert.Equal(1, scheduler.NopDueToNoClassCapacity);
        }

        [Fact]
        public void RecordTypedSlotReject_DynamicClassExhaustion_IncrementsDisaggregatedCounter()
        {
            var scheduler = new MicroOpScheduler();

            scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.DynamicClassExhaustion);

            Assert.Equal(1, scheduler.DynamicClassExhaustionRejects);
            Assert.Equal(1, scheduler.ClassCapacityRejects);
            Assert.Equal(1, scheduler.NopDueToNoClassCapacity);
        }

        [Fact]
        public void RecordTypedSlotReject_PinnedLaneConflict_IncrementsDisaggregatedAndNopCounters()
        {
            var scheduler = new MicroOpScheduler();

            scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.PinnedLaneConflict);

            Assert.Equal(1, scheduler.PinnedLaneConflicts);
            Assert.Equal(1, scheduler.LaneConflictRejects);
            Assert.Equal(1, scheduler.NopDueToPinnedConstraint);
        }

        [Fact]
        public void RecordTypedSlotReject_LateBindingConflict_IncrementsDisaggregatedCounter()
        {
            var scheduler = new MicroOpScheduler();

            scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.LateBindingConflict);

            Assert.Equal(1, scheduler.LateBindingConflicts);
            Assert.Equal(1, scheduler.LaneConflictRejects);
            Assert.Equal(0, scheduler.NopDueToPinnedConstraint);
        }

        [Fact]
        public void RecordTypedSlotReject_ResourceConflict_IncrementsNopDueToResourceConflict()
        {
            var scheduler = new MicroOpScheduler();

            scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.ResourceConflict);

            Assert.Equal(1, scheduler.TypedSlotResourceConflictRejects);
            Assert.Equal(1, scheduler.NopDueToResourceConflict);
        }

        [Fact]
        public void RecordTypedSlotReject_ResourceConflictWithCandidate_IncrementsCertificateRejectByClass()
        {
            var scheduler = new MicroOpScheduler();
            var candidate = CreateLsuCandidate(vtId: 2);

            scheduler.TestRecordTypedSlotReject(
                TypedSlotRejectReason.ResourceConflict,
                candidate,
                CertificateRejectDetail.SharedResourceConflict);

            Assert.Equal(1, scheduler.CertificateRejectByLsuClass);
            Assert.Equal(1, scheduler.TestGetPerVtRejections(2));
            Assert.Equal(0, scheduler.CertificateRegGroupConflictVT2);
            Assert.Equal(0, scheduler.TestGetPerVtRegGroupConflicts(2));
        }

        [Fact]
        public void RecordTypedSlotReject_RegisterGroupConflict_IncrementsCertificateRejectByClassAndVt()
        {
            var scheduler = new MicroOpScheduler();
            var candidate = CreateBranchControlCandidate(vtId: 3);

            scheduler.TestRecordTypedSlotReject(
                TypedSlotRejectReason.ResourceConflict,
                candidate,
                CertificateRejectDetail.RegisterGroupConflict);

            Assert.Equal(1, scheduler.CertificateRejectByBranchControl);
            Assert.Equal(1, scheduler.TestGetPerVtRejections(3));
            Assert.Equal(1, scheduler.CertificateRegGroupConflictVT3);
            Assert.Equal(1, scheduler.TestGetPerVtRegGroupConflicts(3));
        }

        [Fact]
        public void RecordTypedSlotReject_SharedResourceConflict_DoesNotIncrementCertificateRegisterGroupBreakdown()
        {
            var scheduler = new MicroOpScheduler();
            var candidate = CreateDmaStreamCandidate(vtId: 1);

            scheduler.TestRecordTypedSlotReject(
                TypedSlotRejectReason.ResourceConflict,
                candidate,
                CertificateRejectDetail.SharedResourceConflict);

            Assert.Equal(1, scheduler.CertificateRejectByDmaStreamClass);
            Assert.Equal(0, scheduler.CertificateRegGroupConflictVT0);
            Assert.Equal(0, scheduler.CertificateRegGroupConflictVT1);
            Assert.Equal(0, scheduler.CertificateRegGroupConflictVT2);
            Assert.Equal(0, scheduler.CertificateRegGroupConflictVT3);
        }

        [Fact]
        public void RecordTypedSlotReject_DomainReject_IncrementsDomainCounter()
        {
            var scheduler = new MicroOpScheduler();

            scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.DomainReject);

            Assert.Equal(1, scheduler.TypedSlotDomainRejects);
        }

        [Fact]
        public void RecordTypedSlotReject_ScoreboardReject_IncrementsNopDueToDynamicState()
        {
            var scheduler = new MicroOpScheduler();

            scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.ScoreboardReject);

            Assert.Equal(1, scheduler.TypedSlotScoreboardRejects);
            Assert.Equal(1, scheduler.NopDueToDynamicState);
        }

        [Fact]
        public void RecordTypedSlotReject_BankPendingReject_IncrementsNopDueToDynamicState()
        {
            var scheduler = new MicroOpScheduler();

            scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.BankPendingReject);

            Assert.Equal(1, scheduler.TypedSlotBankPendingRejects);
            Assert.Equal(1, scheduler.NopDueToDynamicState);
        }

        [Fact]
        public void RecordBankPendingReject_KnownBank_IncrementsPerBankCounter()
        {
            var scheduler = new MicroOpScheduler();

            scheduler.TestRecordBankPendingReject(2);

            Assert.Equal(1, scheduler.TypedSlotBankPendingRejects);
            Assert.Equal(1, scheduler.BankPendingRejectBank2);
            Assert.Equal(1, scheduler.NopDueToDynamicState);
        }

        [Fact]
        public void RecordBankPendingReject_UnknownBank_DoesNotPollutePerBankCounters()
        {
            var scheduler = new MicroOpScheduler();

            scheduler.TestRecordBankPendingReject(-1);

            Assert.Equal(1, scheduler.TypedSlotBankPendingRejects);
            Assert.Equal(0, scheduler.BankPendingRejectBank0 + scheduler.BankPendingRejectBank1 + scheduler.BankPendingRejectBank2 + scheduler.BankPendingRejectBank3);
        }

        [Fact]
        public void RecordTypedSlotReject_SpeculationBudgetReject_IncrementsNopDueToDynamicState()
        {
            var scheduler = new MicroOpScheduler();

            scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.SpeculationBudgetReject);

            Assert.Equal(1, scheduler.TypedSlotSpeculationBudgetRejects);
            Assert.Equal(1, scheduler.NopDueToDynamicState);
        }

        [Fact]
        public void RecordTypedSlotReject_AssistQuotaReject_IncrementsDynamicStateCounters()
        {
            var scheduler = new MicroOpScheduler();

            scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.AssistQuotaReject);

            Assert.Equal(1, scheduler.TypedSlotAssistQuotaRejects);
            Assert.Equal(1, scheduler.NopDueToDynamicState);
        }

        [Fact]
        public void RecordTypedSlotReject_AssistBackpressureReject_IncrementsDynamicStateCounters()
        {
            var scheduler = new MicroOpScheduler();

            scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.AssistBackpressureReject);

            Assert.Equal(1, scheduler.TypedSlotAssistBackpressureRejects);
            Assert.Equal(1, scheduler.NopDueToDynamicState);
        }

        #endregion

        #region RecordTypedSlotInject — Per-Class Distribution

        [Fact]
        public void RecordTypedSlotInject_AluClassFlexible_IncrementsCorrectCounters()
        {
            var scheduler = new MicroOpScheduler();
            var candidate = CreateAluCandidate();

            scheduler.TestRecordTypedSlotInject(candidate, lane: 0);

            Assert.Equal(1, scheduler.TotalLaneBindings);
            Assert.Equal(1, scheduler.AluClassInjects);
            Assert.Equal(1, scheduler.ClassFlexibleInjects);
            Assert.Equal(0, scheduler.HardPinnedInjects);
            Assert.Equal(1, scheduler.NopAvoided);
        }

        [Fact]
        public void RecordTypedSlotInject_LsuClassFlexible_IncrementsLsuCounter()
        {
            var scheduler = new MicroOpScheduler();
            var candidate = CreateLsuCandidate();

            scheduler.TestRecordTypedSlotInject(candidate, lane: 4);

            Assert.Equal(1, scheduler.LsuClassInjects);
            Assert.Equal(1, scheduler.TotalLaneBindings);
        }

        [Fact]
        public void RecordTypedSlotInject_DmaStreamClass_IncrementsDmaStreamCounter()
        {
            var scheduler = new MicroOpScheduler();
            var candidate = CreateDmaStreamCandidate();

            scheduler.TestRecordTypedSlotInject(candidate, lane: 6);

            Assert.Equal(1, scheduler.DmaStreamClassInjects);
            Assert.Equal(1, scheduler.TotalLaneBindings);
        }

        [Fact]
        public void RecordTypedSlotInject_BranchControlClass_IncrementsBranchCounter()
        {
            var scheduler = new MicroOpScheduler();
            var candidate = CreateBranchControlCandidate();

            scheduler.TestRecordTypedSlotInject(candidate, lane: 7);

            Assert.Equal(1, scheduler.BranchControlInjects);
            Assert.Equal(1, scheduler.TotalLaneBindings);
        }

        [Fact]
        public void RecordTypedSlotInject_HardPinned_IncrementsHardPinnedNotFlexible()
        {
            var scheduler = new MicroOpScheduler();
            var candidate = CreateHardPinnedCandidate();

            scheduler.TestRecordTypedSlotInject(candidate, lane: 0);

            Assert.Equal(1, scheduler.HardPinnedInjects);
            Assert.Equal(0, scheduler.ClassFlexibleInjects);
            Assert.Equal(0, scheduler.NopAvoided);
            Assert.Equal(1, scheduler.TotalLaneBindings);
        }

        [Fact]
        public void RecordTypedSlotInject_MultipleClasses_TotalLaneBindingsEqualsSumOfPerClass()
        {
            var scheduler = new MicroOpScheduler();

            scheduler.TestRecordTypedSlotInject(CreateAluCandidate(), lane: 0);
            scheduler.TestRecordTypedSlotInject(CreateAluCandidate(), lane: 1);
            scheduler.TestRecordTypedSlotInject(CreateLsuCandidate(), lane: 4);
            scheduler.TestRecordTypedSlotInject(CreateDmaStreamCandidate(), lane: 6);
            scheduler.TestRecordTypedSlotInject(CreateBranchControlCandidate(), lane: 7);

            long perClassSum = scheduler.AluClassInjects + scheduler.LsuClassInjects
                             + scheduler.DmaStreamClassInjects + scheduler.BranchControlInjects;
            Assert.Equal(scheduler.TotalLaneBindings, perClassSum);
        }

        [Fact]
        public void RecordTypedSlotInject_HardPinnedPlusFlexibleEqualsTotal()
        {
            var scheduler = new MicroOpScheduler();

            scheduler.TestRecordTypedSlotInject(CreateAluCandidate(), lane: 0);
            scheduler.TestRecordTypedSlotInject(CreateHardPinnedCandidate(), lane: 1);
            scheduler.TestRecordTypedSlotInject(CreateAluCandidate(), lane: 2);

            Assert.Equal(scheduler.TotalLaneBindings, scheduler.HardPinnedInjects + scheduler.ClassFlexibleInjects);
        }

        #endregion

        #region Template Invalidation Sub-Counts

        [Fact]
        public void InvalidateClassTemplate_DomainBoundary_IncrementsSubCount()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.TestSetClassTemplateValid(true);

            scheduler.TestInvalidateClassTemplate(ReplayPhaseInvalidationReason.DomainBoundary);

            Assert.Equal(1, scheduler.ClassTemplateDomainInvalidations);
            Assert.Equal(1, scheduler.ClassTemplateInvalidations);
        }

        [Fact]
        public void InvalidateClassTemplate_ClassCapacityMismatch_IncrementsSubCount()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.TestSetClassTemplateValid(true);

            scheduler.TestInvalidateClassTemplate(ReplayPhaseInvalidationReason.ClassCapacityMismatch);

            Assert.Equal(1, scheduler.ClassTemplateCapacityMismatchInvalidations);
            Assert.Equal(1, scheduler.ClassTemplateInvalidations);
        }

        [Fact]
        public void InvalidateClassTemplate_OtherReason_DoesNotIncrementSubCounts()
        {
            var scheduler = new MicroOpScheduler();
            scheduler.TestSetClassTemplateValid(true);

            scheduler.TestInvalidateClassTemplate(ReplayPhaseInvalidationReason.ClassTemplateExpired);

            Assert.Equal(0, scheduler.ClassTemplateDomainInvalidations);
            Assert.Equal(0, scheduler.ClassTemplateCapacityMismatchInvalidations);
            Assert.Equal(1, scheduler.ClassTemplateInvalidations);
        }

        [Fact]
        public void InvalidateClassTemplate_WhenNotValid_DoesNotIncrementAnyCounter()
        {
            var scheduler = new MicroOpScheduler();
            // Template starts invalid

            scheduler.TestInvalidateClassTemplate(ReplayPhaseInvalidationReason.DomainBoundary);

            Assert.Equal(0, scheduler.ClassTemplateDomainInvalidations);
            Assert.Equal(0, scheduler.ClassTemplateInvalidations);
        }

        #endregion

        #region Derived Metrics — Boundary Cases

        [Fact]
        public void ClassCapacityRejectRate_WhenNoRejects_ReturnsZero()
        {
            var report = new PerformanceReport();

            Assert.Equal(0.0, report.ClassCapacityRejectRate);
        }

        [Fact]
        public void ClassCapacityRejectRate_WhenAllCapacityRejects_ReturnsOne()
        {
            var report = new PerformanceReport
            {
                StaticClassOvercommitRejects = 50,
                DynamicClassExhaustionRejects = 50,
                PinnedLaneConflicts = 0,
                LateBindingConflicts = 0
            };

            Assert.Equal(1.0, report.ClassCapacityRejectRate);
        }

        [Fact]
        public void LaneReuseRate_WhenNoBindings_ReturnsZero()
        {
            var report = new PerformanceReport();

            Assert.Equal(0.0, report.LaneReuseRate);
        }

        [Fact]
        public void LaneReuseRate_WhenAllReused_ReturnsOne()
        {
            var report = new PerformanceReport
            {
                LaneReuseHits = 100,
                TotalLaneBindings = 100
            };

            Assert.Equal(1.0, report.LaneReuseRate);
        }

        [Fact]
        public void ClassFlexibleRatio_WhenNoInjects_ReturnsZero()
        {
            var report = new PerformanceReport();

            Assert.Equal(0.0, report.ClassFlexibleRatio);
        }

        [Fact]
        public void ClassFlexibleRatio_WhenAllFlexible_ReturnsOne()
        {
            var report = new PerformanceReport
            {
                ClassFlexibleInjects = 100,
                HardPinnedInjects = 0
            };

            Assert.Equal(1.0, report.ClassFlexibleRatio);
        }

        [Fact]
        public void NopReductionRate_WhenNoNopDecisions_ReturnsZero()
        {
            var report = new PerformanceReport();

            Assert.Equal(0.0, report.NopReductionRate);
        }

        [Fact]
        public void NopReductionRate_WhenAllAvoided_ReturnsOne()
        {
            var report = new PerformanceReport
            {
                NopAvoided = 100,
                NopDueToPinnedConstraint = 0,
                NopDueToNoClassCapacity = 0
            };

            Assert.Equal(1.0, report.NopReductionRate);
        }

        #endregion

        #region PerformanceReport Includes Phase 08 Fields

        [Fact]
        public void PerformanceReport_HasPhase8TypedSlotTelemetry_DetectsPresence()
        {
            var report = new PerformanceReport
            {
                TotalLaneBindings = 10
            };

            Assert.True(report.HasPhase8TypedSlotTelemetry);
        }

        [Fact]
        public void PerformanceReport_HasPhase8TypedSlotTelemetry_WhenEmpty_ReturnsFalse()
        {
            var report = new PerformanceReport();

            Assert.False(report.HasPhase8TypedSlotTelemetry);
        }

        [Fact]
        public void PerformanceReport_GeneratePhase8Summary_WhenDataPresent_ReturnsNonEmpty()
        {
            var report = new PerformanceReport
            {
                ClassFlexibleInjects = 100,
                HardPinnedInjects = 25,
                TotalLaneBindings = 125,
                NopAvoided = 80,
                StaticClassOvercommitRejects = 5
            };

            string summary = report.GeneratePhase8TypedSlotSummary();

            Assert.Contains("Typed-Slot Statistics", summary);
            Assert.Contains("Class-Flexible Injects", summary);
        }

        #endregion

        #region SchedulerPhaseMetrics Wiring

        [Fact]
        public void GetPhaseMetrics_WiresPhase08Counters()
        {
            var scheduler = new MicroOpScheduler();

            scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.StaticClassOvercommit);
            scheduler.TestRecordTypedSlotInject(CreateAluCandidate(), lane: 0);

            var metrics = scheduler.GetPhaseMetrics();

            Assert.Equal(1, metrics.StaticClassOvercommitRejects);
            Assert.Equal(1, metrics.AluClassInjects);
            Assert.Equal(1, metrics.TotalLaneBindings);
            Assert.Equal(1, metrics.NopDueToNoClassCapacity);
            Assert.Equal(1, metrics.NopAvoided);
        }

        [Fact]
        public void GetPhaseMetrics_WiresBankPendingAndClusteringCounters()
        {
            var scheduler = new MicroOpScheduler();

            scheduler.TestRecordBankPendingReject(9);
            scheduler.TestRecordMemoryClusteringEvent();

            var metrics = scheduler.GetPhaseMetrics();

            Assert.Equal(1, metrics.BankPendingRejectBank9);
            Assert.Equal(1, metrics.MemoryClusteringEvents);
        }

        #endregion

        #region TypedSlotEnabled=false Regression

        [Fact]
        public void WhenTypedSlotDisabled_Phase08CountersRemainZero()
        {
            var scheduler = new MicroOpScheduler();
            Assert.False(scheduler.TypedSlotEnabled);

            // All Phase 08 counters should be 0 by default
            Assert.Equal(0, scheduler.StaticClassOvercommitRejects);
            Assert.Equal(0, scheduler.DynamicClassExhaustionRejects);
            Assert.Equal(0, scheduler.PinnedLaneConflicts);
            Assert.Equal(0, scheduler.LateBindingConflicts);
            Assert.Equal(0, scheduler.TypedSlotDomainRejects);
            Assert.Equal(0, scheduler.TypedSlotStandardPathAccepts);
            Assert.Equal(0, scheduler.TotalLaneBindings);
            Assert.Equal(0, scheduler.ClassTemplateDomainInvalidations);
            Assert.Equal(0, scheduler.ClassTemplateCapacityMismatchInvalidations);
            Assert.Equal(0, scheduler.NopDueToPinnedConstraint);
            Assert.Equal(0, scheduler.NopDueToNoClassCapacity);
            Assert.Equal(0, scheduler.NopDueToResourceConflict);
            Assert.Equal(0, scheduler.NopDueToDynamicState);
            Assert.Equal(0, scheduler.NopAvoided);
            Assert.Equal(0, scheduler.AluClassInjects);
            Assert.Equal(0, scheduler.LsuClassInjects);
            Assert.Equal(0, scheduler.DmaStreamClassInjects);
            Assert.Equal(0, scheduler.BranchControlInjects);
            Assert.Equal(0, scheduler.HardPinnedInjects);
            Assert.Equal(0, scheduler.ClassFlexibleInjects);
            Assert.Equal(0, scheduler.TypedSlotAssistQuotaRejects);
        }

        #endregion

        #region NOP Tracking Accumulation

        [Fact]
        public void NopCounters_AccumulateAcrossMultipleRejects()
        {
            var scheduler = new MicroOpScheduler();

            scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.StaticClassOvercommit);
            scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.DynamicClassExhaustion);
            scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.PinnedLaneConflict);
            scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.ResourceConflict);
            scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.ScoreboardReject);
            scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.AssistQuotaReject);
            scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.AssistBackpressureReject);

            Assert.Equal(2, scheduler.NopDueToNoClassCapacity);
            Assert.Equal(1, scheduler.NopDueToPinnedConstraint);
            Assert.Equal(1, scheduler.NopDueToResourceConflict);
            Assert.Equal(3, scheduler.NopDueToDynamicState);
        }

        #endregion
    }
}
