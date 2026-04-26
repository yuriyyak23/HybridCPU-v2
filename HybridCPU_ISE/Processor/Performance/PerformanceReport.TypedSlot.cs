using HybridCPU_ISE.Core;
using System.Text;

namespace YAKSys_Hybrid_CPU
{
    public partial class PerformanceReport
    {
        #region Typed-Slot Statistics (Phase 08)

        /// <summary>Disaggregated: compiler bundle exceeds class capacity.</summary>
        public long StaticClassOvercommitRejects { get; set; }

        /// <summary>Disaggregated: intra-cycle typed-slot densification exhaustion.</summary>
        public long DynamicClassExhaustionRejects { get; set; }

        /// <summary>Disaggregated: hard-pinned lane occupied.</summary>
        public long PinnedLaneConflicts { get; set; }

        /// <summary>Disaggregated: all class lanes occupied.</summary>
        public long LateBindingConflicts { get; set; }

        /// <summary>Domain isolation gating reject.</summary>
        public long TypedSlotDomainRejects { get; set; }

        /// <summary>SMT owner-context guard-plane rejects hidden behind ResourceConflict.</summary>
        public long SmtOwnerContextGuardRejects { get; set; }

        /// <summary>SMT domain guard-plane rejects hidden behind ResourceConflict.</summary>
        public long SmtDomainGuardRejects { get; set; }

        /// <summary>SMT boundary guard-plane rejects.</summary>
        public long SmtBoundaryGuardRejects { get; set; }

        /// <summary>SMT shared-resource certificate rejects hidden behind ResourceConflict.</summary>
        public long SmtSharedResourceCertificateRejects { get; set; }

        /// <summary>SMT register-group certificate rejects hidden behind ResourceConflict.</summary>
        public long SmtRegisterGroupCertificateRejects { get; set; }

        /// <summary>Most recent SMT legality reject kind observed by typed-slot admission.</summary>
        public Core.RejectKind LastSmtLegalityRejectKind { get; set; }

        /// <summary>Most recent SMT legality authority source observed by typed-slot admission.</summary>
        public Core.LegalityAuthoritySource LastSmtLegalityAuthoritySource { get; set; }

        /// <summary>SMT legality rejections attributed to ALU-class candidates.</summary>
        public long SmtLegalityRejectByAluClass { get; set; }

        /// <summary>SMT legality rejections attributed to LSU-class candidates.</summary>
        public long SmtLegalityRejectByLsuClass { get; set; }

        /// <summary>SMT legality rejections attributed to DMA/Stream-class candidates.</summary>
        public long SmtLegalityRejectByDmaStreamClass { get; set; }

        /// <summary>SMT legality rejections attributed to Branch/Control candidates.</summary>
        public long SmtLegalityRejectByBranchControl { get; set; }

        /// <summary>SMT legality rejections attributed to SystemSingleton candidates.</summary>
        public long SmtLegalityRejectBySystemSingleton { get; set; }

        /// <summary>Fast-path accepts via template budget.</summary>
        public long TypedSlotFastPathAccepts { get; set; }

        /// <summary>Admission via full pipeline (complement to fast-path).</summary>
        public long TypedSlotStandardPathAccepts { get; set; }

        /// <summary>Level 1 class-template reuse hits.</summary>
        public long ClassTemplateReuseHits { get; set; }

        /// <summary>Class-template invalidations.</summary>
        public long ClassTemplateInvalidations { get; set; }

        /// <summary>Template invalidated by domain boundary.</summary>
        public long ClassTemplateDomainInvalidations { get; set; }

        /// <summary>Template invalidated by capacity mismatch.</summary>
        public long ClassTemplateCapacityMismatchInvalidations { get; set; }

        /// <summary>Tier 2 lane reuse hits.</summary>
        public long LaneReuseHits { get; set; }

        /// <summary>Tier 2 lane reuse misses (fallback to Tier 1).</summary>
        public long LaneReuseMisses { get; set; }

        /// <summary>Total successful late lane bindings.</summary>
        public long TotalLaneBindings { get; set; }

        /// <summary>NOPs caused by hard-pinned lane unavailability.</summary>
        public long NopDueToPinnedConstraint { get; set; }

        /// <summary>NOPs caused by class-capacity exhaustion.</summary>
        public long NopDueToNoClassCapacity { get; set; }

        /// <summary>NOPs caused by SafetyMask conflict.</summary>
        public long NopDueToResourceConflict { get; set; }

        /// <summary>NOPs caused by dynamic runtime state.</summary>
        public long NopDueToDynamicState { get; set; }

        /// <summary>NOPs avoided thanks to class-flexible placement.</summary>
        public long NopAvoided { get; set; }

        /// <summary>Successful injections into ALU-class lanes.</summary>
        public long AluClassInjects { get; set; }

        /// <summary>Successful injections into LSU-class lanes.</summary>
        public long LsuClassInjects { get; set; }

        /// <summary>Successful injections into DMA/Stream-class lanes.</summary>
        public long DmaStreamClassInjects { get; set; }

        /// <summary>Injections into Branch/Control lanes.</summary>
        public long BranchControlInjects { get; set; }

        /// <summary>Injections of hard-pinned ops.</summary>
        public long HardPinnedInjects { get; set; }

        /// <summary>Injections of class-flexible ops.</summary>
        public long ClassFlexibleInjects { get; set; }

        /// <summary>
        /// Fraction of rejections that were class-capacity-related.
        /// </summary>
        public double ClassCapacityRejectRate
        {
            get
            {
                long total = StaticClassOvercommitRejects + DynamicClassExhaustionRejects + PinnedLaneConflicts + LateBindingConflicts;
                return total > 0
                    ? (double)(StaticClassOvercommitRejects + DynamicClassExhaustionRejects) / total
                    : 0.0;
            }
        }

        /// <summary>
        /// Fraction of lane bindings that reused replay hint.
        /// </summary>
        public double LaneReuseRate =>
            TotalLaneBindings > 0
                ? (double)LaneReuseHits / TotalLaneBindings
                : 0.0;

        /// <summary>
        /// Ratio of class-flexible to hard-pinned injects.
        /// </summary>
        public double ClassFlexibleRatio =>
            (ClassFlexibleInjects + HardPinnedInjects) > 0
                ? (double)ClassFlexibleInjects / (ClassFlexibleInjects + HardPinnedInjects)
                : 0.0;

        /// <summary>
        /// Estimated NOP reduction from typed-slot flexibility.
        /// </summary>
        public double NopReductionRate =>
            (NopAvoided + NopDueToPinnedConstraint + NopDueToNoClassCapacity) > 0
                ? (double)NopAvoided / (NopAvoided + NopDueToPinnedConstraint + NopDueToNoClassCapacity)
                : 0.0;

        /// <summary>
        /// Whether Phase 08 typed-slot telemetry is present.
        /// </summary>
        public bool HasPhase8TypedSlotTelemetry =>
            TotalLaneBindings > 0 ||
            StaticClassOvercommitRejects > 0 ||
            DynamicClassExhaustionRejects > 0 ||
            PinnedLaneConflicts > 0 ||
            LateBindingConflicts > 0 ||
            SmtOwnerContextGuardRejects > 0 ||
            SmtDomainGuardRejects > 0 ||
            SmtBoundaryGuardRejects > 0 ||
            SmtSharedResourceCertificateRejects > 0 ||
            SmtRegisterGroupCertificateRejects > 0 ||
            SmtLegalityRejectByAluClass > 0 ||
            SmtLegalityRejectByLsuClass > 0 ||
            SmtLegalityRejectByDmaStreamClass > 0 ||
            SmtLegalityRejectByBranchControl > 0 ||
            SmtLegalityRejectBySystemSingleton > 0 ||
            NopAvoided > 0;

        #endregion
    }
}
