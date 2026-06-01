using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            /// <summary>
            /// Pipeline control and state
            /// </summary>
            public struct PipelineControl
            {
                public bool Enabled;
                public bool Stalled;
                public PipelineStallKind StallReason;
                public ulong CycleCount;
                public ulong StallCycles;
                public ulong InstructionsRetired;
                public ulong Lane7ConditionalBranchExecuteCompletionCount;
                public ulong Lane7ConditionalBranchRedirectCount;
                public ulong BranchMispredicts;
                public ulong DataHazards;
                public ulong MemoryStalls;
                public ulong ForwardingEvents;
                public ulong ControlHazards;
                public ulong WAWHazards;
                public ulong LoadUseBubbles;
                public ulong FrontendStalls;
                public ulong DomainSquashCount;
                public ulong EarlyDomainSquashCount;
                public ulong MshrScoreboardStalls;
                public ulong BankConflictStallCycles;
                public ulong ExceptionYoungerSuppressCount;
                public ulong MultiSlotDecodeAdvanceCount;
                public ulong MultiLaneExecuteCount;
                public ulong MemoryFaultCarrierCount;
                public ulong AuxiliaryCoexistenceConflictCount;
                public ulong[] ScalarIssueWidthHistogram;
                public ulong ClusterProbeCount;
                public ulong ClusterProbeRefinedWidthSum;
                public ulong ClusterProbeNarrowFallbackCount;
                public bool ClusterPreparedModeEnabled;
                public ulong ClusterPreparedExecutionChoiceCount;
                public ulong DifferentialTraceCompareCount;
                public ulong DifferentialTraceDiscrepancyCount;
                public ulong ClusterModeFallbackCount;
                public ulong DecoderPreparedScalarGroupCount;
                public ulong DecoderPreparedFallbackCount;
                public ulong DecodeFallbackCount;
                public ulong DecodeFaultBundleCount;
                public ulong CrossSlotRejectCount;
                public ulong HazardRegisterDataCount;
                public ulong HazardMemoryBankCount;
                public ulong HazardControlFlowCount;
                public ulong HazardSystemBarrierCount;
                public ulong HazardPinnedLaneCount;
                public ulong ScalarClusterEligibleButBlockedCount;
                public ulong ReferenceFallbackDueToControlConflictCount;
                public ulong ReferenceFallbackDueToMemoryConflictCount;
                public ulong VTSpreadPerBundle;
                public ulong BurstReadCycles;
                public ulong BurstWriteCycles;
                public ulong ComputeCycles;
                public ulong OverlappedCycles;
                public ulong WidePathGate3_ReferenceSequentialCount;
                public ulong WidePathGate4_NarrowFallbackCount;
                public ulong WidePathGate5_NotClusterCandidateCount;
                public ulong WidePathGate6_PreparedMaskZeroCount;
                public ulong WidePathSuccessCount;
                public ulong RefinedMaskPromotionCount;
                public ulong PartialWidthIssueCount;
                public ulong IssuePacketPreparedLaneCountSum;
                public ulong IssuePacketMaterializedLaneCountSum;
                public ulong IssuePacketPreparedPhysicalLaneCountSum;
                public ulong IssuePacketMaterializedPhysicalLaneCountSum;
                public ulong IssuePacketWidthDropCount;
                public ulong ReferenceSequentialFallbackCount;
                public ulong FallbackSofteningPromotionCount;
                public ulong NopElisionSkipCount;
                public ulong ScalarLanesRetired;
                public ulong NonScalarLanesRetired;
                public ulong RetireCycleCount;
                public ulong InvariantViolationCount;
                public const double ReferenceSequentialFallbackRateBudgetThreshold = 0.05;

                public void Clear()
                {
                    Enabled = false;
                    Stalled = false;
                    StallReason = PipelineStallKind.None;
                    CycleCount = 0;
                    StallCycles = 0;
                    InstructionsRetired = 0;
                    Lane7ConditionalBranchExecuteCompletionCount = 0;
                    Lane7ConditionalBranchRedirectCount = 0;
                    BranchMispredicts = 0;
                    DataHazards = 0;
                    MemoryStalls = 0;
                    ForwardingEvents = 0;
                    ControlHazards = 0;
                    WAWHazards = 0;
                    LoadUseBubbles = 0;
                    FrontendStalls = 0;
                    DomainSquashCount = 0;
                    EarlyDomainSquashCount = 0;
                    MshrScoreboardStalls = 0;
                    BankConflictStallCycles = 0;
                    ExceptionYoungerSuppressCount = 0;
                    MultiSlotDecodeAdvanceCount = 0;
                    MultiLaneExecuteCount = 0;
                    MemoryFaultCarrierCount = 0;
                    AuxiliaryCoexistenceConflictCount = 0;
                    ScalarIssueWidthHistogram = new ulong[5];
                    ClusterProbeCount = 0;
                    ClusterProbeRefinedWidthSum = 0;
                    ClusterProbeNarrowFallbackCount = 0;
                    ClusterPreparedModeEnabled = true;
                    ClusterPreparedExecutionChoiceCount = 0;
                    DifferentialTraceCompareCount = 0;
                    DifferentialTraceDiscrepancyCount = 0;
                    ClusterModeFallbackCount = 0;
                    DecoderPreparedScalarGroupCount = 0;
                    DecoderPreparedFallbackCount = 0;
                    DecodeFallbackCount = 0;
                    DecodeFaultBundleCount = 0;
                    CrossSlotRejectCount = 0;
                    HazardRegisterDataCount = 0;
                    HazardMemoryBankCount = 0;
                    HazardControlFlowCount = 0;
                    HazardSystemBarrierCount = 0;
                    HazardPinnedLaneCount = 0;
                    ScalarClusterEligibleButBlockedCount = 0;
                    ReferenceFallbackDueToControlConflictCount = 0;
                    ReferenceFallbackDueToMemoryConflictCount = 0;
                    VTSpreadPerBundle = 0;
                    BurstReadCycles = 0;
                    BurstWriteCycles = 0;
                    ComputeCycles = 0;
                    OverlappedCycles = 0;
                    WidePathGate3_ReferenceSequentialCount = 0;
                    WidePathGate4_NarrowFallbackCount = 0;
                    WidePathGate5_NotClusterCandidateCount = 0;
                    WidePathGate6_PreparedMaskZeroCount = 0;
                    WidePathSuccessCount = 0;
                    RefinedMaskPromotionCount = 0;
                    PartialWidthIssueCount = 0;
                    IssuePacketPreparedLaneCountSum = 0;
                    IssuePacketMaterializedLaneCountSum = 0;
                    IssuePacketPreparedPhysicalLaneCountSum = 0;
                    IssuePacketMaterializedPhysicalLaneCountSum = 0;
                    IssuePacketWidthDropCount = 0;
                    ReferenceSequentialFallbackCount = 0;
                    FallbackSofteningPromotionCount = 0;
                    NopElisionSkipCount = 0;
                    ScalarLanesRetired = 0;
                    NonScalarLanesRetired = 0;
                    RetireCycleCount = 0;
                    InvariantViolationCount = 0;
                }

                /// <summary>
                /// Total bundles that reached the widened-runtime decision point and therefore
                /// contribute to the reference-fallback budget denominator.
                /// </summary>
                public ulong GetClusterPreparedOpportunityCount()
                {
                    return WidePathSuccessCount + ClusterModeFallbackCount;
                }

                /// <summary>
                /// Fraction of widened-runtime opportunities that still fell back to the
                /// reference sequential contour.
                /// </summary>
                public double GetReferenceSequentialFallbackRate()
                {
                    ulong opportunityCount = GetClusterPreparedOpportunityCount();
                    if (opportunityCount == 0)
                        return 0.0;

                    return (double)ReferenceSequentialFallbackCount / (double)opportunityCount;
                }

                /// <summary>
                /// Budget policy for widened-runtime regressions. Explicit reference-mode selection
                /// is tracked separately and does not count as a budget violation.
                /// </summary>
                public bool ExceedsReferenceSequentialFallbackRateBudget()
                {
                    ulong opportunityCount = GetClusterPreparedOpportunityCount();
                    return opportunityCount != 0 &&
                           GetReferenceSequentialFallbackRate() > ReferenceSequentialFallbackRateBudgetThreshold;
                }

                /// <summary>
                /// Calculate Instructions Per Cycle (IPC) metric
                /// </summary>
                public double GetIPC()
                {
                    if (CycleCount == 0) return 0.0;
                    return (double)InstructionsRetired / (double)CycleCount;
                }

                /// <summary>
                /// Calculate pipeline efficiency (ratio of useful cycles)
                /// </summary>
                public double GetEfficiency()
                {
                    if (CycleCount == 0) return 0.0;
                    return (double)(CycleCount - StallCycles) / (double)CycleCount;
                }

                /// <summary>
                /// Stage 7 Phase E: effective issue width for the currently live retire-authoritative subset.
                /// </summary>
                public double GetEffectiveIssueWidth()
                {
                    ulong activeCycles = CycleCount - StallCycles;
                    if (activeCycles == 0) return 0.0;
                    return (double)InstructionsRetired / (double)activeCycles;
                }

                /// <summary>
                /// Post-phase-05: scalar-only IPC (lanes 0..3 + early-exit control-flow).
                /// Secondary metric — use <see cref="GetIPC"/> for total.
                /// </summary>
                public double GetScalarIPC()
                {
                    if (CycleCount == 0) return 0.0;
                    return (double)ScalarLanesRetired / (double)CycleCount;
                }

                /// <summary>
                /// Post-phase-05: average retired lane count per retire-active cycle.
                /// Reflects true heterogeneous retired width for the live lane0..5 window.
                /// </summary>
                public double GetAverageRetiredWidth()
                {
                    if (RetireCycleCount == 0) return 0.0;
                    return (double)InstructionsRetired / (double)RetireCycleCount;
                }
            }
        }
    }
}
