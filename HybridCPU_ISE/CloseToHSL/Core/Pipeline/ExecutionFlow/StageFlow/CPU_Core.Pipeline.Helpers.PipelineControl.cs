using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public sealed partial class CPU_Core
        {
            /// <summary>
            /// Pipeline control and state
            /// </summary>
            public struct PipelineControl
            {
                public bool Enabled;
                public bool Stalled;
                public PipelineStallKind StallReason;
                private Core.TelemetryState? _telemetry;

                private Core.TelemetryState Telemetry =>
                    _telemetry ??= new Core.TelemetryState();

                internal PipelineControl(Core.TelemetryState telemetry)
                {
                    ArgumentNullException.ThrowIfNull(telemetry);
                    Enabled = false;
                    Stalled = false;
                    StallReason = PipelineStallKind.None;
                    ClusterPreparedModeEnabled = false;
                    _telemetry = telemetry;
                }

                internal PipelineControl CreateSnapshot()
                {
                    PipelineControl snapshot = this;
                    snapshot._telemetry = Telemetry.CreateSnapshot();
                    return snapshot;
                }

                public ulong CycleCount
                {
                    get => Telemetry.CycleCount;
                    set => Telemetry.CycleCount = value;
                }
                public ulong StallCycles
                {
                    get => Telemetry.StallCycles;
                    set => Telemetry.StallCycles = value;
                }
                public ulong InstructionsRetired
                {
                    get => Telemetry.InstructionsRetired;
                    set => Telemetry.InstructionsRetired = value;
                }
                public ulong Lane7ConditionalBranchExecuteCompletionCount
                {
                    get => Telemetry.Lane7ConditionalBranchExecuteCompletionCount;
                    set => Telemetry.Lane7ConditionalBranchExecuteCompletionCount = value;
                }
                public ulong Lane7ConditionalBranchRedirectCount
                {
                    get => Telemetry.Lane7ConditionalBranchRedirectCount;
                    set => Telemetry.Lane7ConditionalBranchRedirectCount = value;
                }
                public ulong BranchMispredicts
                {
                    get => Telemetry.BranchMispredicts;
                    set => Telemetry.BranchMispredicts = value;
                }
                public ulong DataHazards
                {
                    get => Telemetry.DataHazards;
                    set => Telemetry.DataHazards = value;
                }
                public ulong MemoryStalls
                {
                    get => Telemetry.MemoryStalls;
                    set => Telemetry.MemoryStalls = value;
                }
                public ulong ForwardingEvents
                {
                    get => Telemetry.ForwardingEvents;
                    set => Telemetry.ForwardingEvents = value;
                }
                public ulong ControlHazards
                {
                    get => Telemetry.ControlHazards;
                    set => Telemetry.ControlHazards = value;
                }
                public ulong WAWHazards
                {
                    get => Telemetry.WAWHazards;
                    set => Telemetry.WAWHazards = value;
                }
                public ulong LoadUseBubbles
                {
                    get => Telemetry.LoadUseBubbles;
                    set => Telemetry.LoadUseBubbles = value;
                }
                public ulong FrontendStalls
                {
                    get => Telemetry.FrontendStalls;
                    set => Telemetry.FrontendStalls = value;
                }
                public ulong DomainSquashCount
                {
                    get => Telemetry.DomainSquashCount;
                    set => Telemetry.DomainSquashCount = value;
                }
                public ulong EarlyDomainSquashCount
                {
                    get => Telemetry.EarlyDomainSquashCount;
                    set => Telemetry.EarlyDomainSquashCount = value;
                }
                public ulong MshrScoreboardStalls
                {
                    get => Telemetry.MshrScoreboardStalls;
                    set => Telemetry.MshrScoreboardStalls = value;
                }
                public ulong BankConflictStallCycles
                {
                    get => Telemetry.BankConflictStallCycles;
                    set => Telemetry.BankConflictStallCycles = value;
                }
                public ulong ExceptionYoungerSuppressCount
                {
                    get => Telemetry.ExceptionYoungerSuppressCount;
                    set => Telemetry.ExceptionYoungerSuppressCount = value;
                }
                public ulong MultiSlotDecodeAdvanceCount
                {
                    get => Telemetry.MultiSlotDecodeAdvanceCount;
                    set => Telemetry.MultiSlotDecodeAdvanceCount = value;
                }
                public ulong MultiLaneExecuteCount
                {
                    get => Telemetry.MultiLaneExecuteCount;
                    set => Telemetry.MultiLaneExecuteCount = value;
                }
                public ulong MemoryFaultCarrierCount
                {
                    get => Telemetry.MemoryFaultCarrierCount;
                    set => Telemetry.MemoryFaultCarrierCount = value;
                }
                public ulong AuxiliaryCoexistenceConflictCount
                {
                    get => Telemetry.AuxiliaryCoexistenceConflictCount;
                    set => Telemetry.AuxiliaryCoexistenceConflictCount = value;
                }
                public ulong[] ScalarIssueWidthHistogram
                {
                    get => Telemetry.ScalarIssueWidthHistogram;
                    set => Telemetry.ScalarIssueWidthHistogram = value;
                }
                public ulong ClusterProbeCount
                {
                    get => Telemetry.ClusterProbeCount;
                    set => Telemetry.ClusterProbeCount = value;
                }
                public ulong ClusterProbeRefinedWidthSum
                {
                    get => Telemetry.ClusterProbeRefinedWidthSum;
                    set => Telemetry.ClusterProbeRefinedWidthSum = value;
                }
                public ulong ClusterProbeNarrowFallbackCount
                {
                    get => Telemetry.ClusterProbeNarrowFallbackCount;
                    set => Telemetry.ClusterProbeNarrowFallbackCount = value;
                }
                public bool ClusterPreparedModeEnabled;
                public ulong ClusterPreparedExecutionChoiceCount
                {
                    get => Telemetry.ClusterPreparedExecutionChoiceCount;
                    set => Telemetry.ClusterPreparedExecutionChoiceCount = value;
                }
                public ulong DifferentialTraceCompareCount
                {
                    get => Telemetry.DifferentialTraceCompareCount;
                    set => Telemetry.DifferentialTraceCompareCount = value;
                }
                public ulong DifferentialTraceDiscrepancyCount
                {
                    get => Telemetry.DifferentialTraceDiscrepancyCount;
                    set => Telemetry.DifferentialTraceDiscrepancyCount = value;
                }
                public ulong ClusterModeFallbackCount
                {
                    get => Telemetry.ClusterModeFallbackCount;
                    set => Telemetry.ClusterModeFallbackCount = value;
                }
                public ulong DecoderPreparedScalarGroupCount
                {
                    get => Telemetry.DecoderPreparedScalarGroupCount;
                    set => Telemetry.DecoderPreparedScalarGroupCount = value;
                }
                public ulong DecoderPreparedFallbackCount
                {
                    get => Telemetry.DecoderPreparedFallbackCount;
                    set => Telemetry.DecoderPreparedFallbackCount = value;
                }
                public ulong DecodeFallbackCount
                {
                    get => Telemetry.DecodeFallbackCount;
                    set => Telemetry.DecodeFallbackCount = value;
                }
                public ulong DecodeFaultBundleCount
                {
                    get => Telemetry.DecodeFaultBundleCount;
                    set => Telemetry.DecodeFaultBundleCount = value;
                }
                public ulong CrossSlotRejectCount
                {
                    get => Telemetry.CrossSlotRejectCount;
                    set => Telemetry.CrossSlotRejectCount = value;
                }
                public ulong HazardRegisterDataCount
                {
                    get => Telemetry.HazardRegisterDataCount;
                    set => Telemetry.HazardRegisterDataCount = value;
                }
                public ulong HazardMemoryBankCount
                {
                    get => Telemetry.HazardMemoryBankCount;
                    set => Telemetry.HazardMemoryBankCount = value;
                }
                public ulong HazardControlFlowCount
                {
                    get => Telemetry.HazardControlFlowCount;
                    set => Telemetry.HazardControlFlowCount = value;
                }
                public ulong HazardSystemBarrierCount
                {
                    get => Telemetry.HazardSystemBarrierCount;
                    set => Telemetry.HazardSystemBarrierCount = value;
                }
                public ulong HazardPinnedLaneCount
                {
                    get => Telemetry.HazardPinnedLaneCount;
                    set => Telemetry.HazardPinnedLaneCount = value;
                }
                public ulong ScalarClusterEligibleButBlockedCount
                {
                    get => Telemetry.ScalarClusterEligibleButBlockedCount;
                    set => Telemetry.ScalarClusterEligibleButBlockedCount = value;
                }
                public ulong ReferenceFallbackDueToControlConflictCount
                {
                    get => Telemetry.ReferenceFallbackDueToControlConflictCount;
                    set => Telemetry.ReferenceFallbackDueToControlConflictCount = value;
                }
                public ulong ReferenceFallbackDueToMemoryConflictCount
                {
                    get => Telemetry.ReferenceFallbackDueToMemoryConflictCount;
                    set => Telemetry.ReferenceFallbackDueToMemoryConflictCount = value;
                }
                public ulong VTSpreadPerBundle
                {
                    get => Telemetry.VTSpreadPerBundle;
                    set => Telemetry.VTSpreadPerBundle = value;
                }
                public ulong BurstReadCycles
                {
                    get => Telemetry.BurstReadCycles;
                    set => Telemetry.BurstReadCycles = value;
                }
                public ulong BurstWriteCycles
                {
                    get => Telemetry.BurstWriteCycles;
                    set => Telemetry.BurstWriteCycles = value;
                }
                public ulong ComputeCycles
                {
                    get => Telemetry.ComputeCycles;
                    set => Telemetry.ComputeCycles = value;
                }
                public ulong OverlappedCycles
                {
                    get => Telemetry.OverlappedCycles;
                    set => Telemetry.OverlappedCycles = value;
                }
                public ulong WidePathGate3_ReferenceSequentialCount
                {
                    get => Telemetry.WidePathGate3_ReferenceSequentialCount;
                    set => Telemetry.WidePathGate3_ReferenceSequentialCount = value;
                }
                public ulong WidePathGate4_NarrowFallbackCount
                {
                    get => Telemetry.WidePathGate4_NarrowFallbackCount;
                    set => Telemetry.WidePathGate4_NarrowFallbackCount = value;
                }
                public ulong WidePathGate5_NotClusterCandidateCount
                {
                    get => Telemetry.WidePathGate5_NotClusterCandidateCount;
                    set => Telemetry.WidePathGate5_NotClusterCandidateCount = value;
                }
                public ulong WidePathGate6_PreparedMaskZeroCount
                {
                    get => Telemetry.WidePathGate6_PreparedMaskZeroCount;
                    set => Telemetry.WidePathGate6_PreparedMaskZeroCount = value;
                }
                public ulong WidePathSuccessCount
                {
                    get => Telemetry.WidePathSuccessCount;
                    set => Telemetry.WidePathSuccessCount = value;
                }
                public ulong RefinedMaskPromotionCount
                {
                    get => Telemetry.RefinedMaskPromotionCount;
                    set => Telemetry.RefinedMaskPromotionCount = value;
                }
                public ulong PartialWidthIssueCount
                {
                    get => Telemetry.PartialWidthIssueCount;
                    set => Telemetry.PartialWidthIssueCount = value;
                }
                public ulong IssuePacketPreparedLaneCountSum
                {
                    get => Telemetry.IssuePacketPreparedLaneCountSum;
                    set => Telemetry.IssuePacketPreparedLaneCountSum = value;
                }
                public ulong IssuePacketMaterializedLaneCountSum
                {
                    get => Telemetry.IssuePacketMaterializedLaneCountSum;
                    set => Telemetry.IssuePacketMaterializedLaneCountSum = value;
                }
                public ulong IssuePacketPreparedPhysicalLaneCountSum
                {
                    get => Telemetry.IssuePacketPreparedPhysicalLaneCountSum;
                    set => Telemetry.IssuePacketPreparedPhysicalLaneCountSum = value;
                }
                public ulong IssuePacketMaterializedPhysicalLaneCountSum
                {
                    get => Telemetry.IssuePacketMaterializedPhysicalLaneCountSum;
                    set => Telemetry.IssuePacketMaterializedPhysicalLaneCountSum = value;
                }
                public ulong IssuePacketWidthDropCount
                {
                    get => Telemetry.IssuePacketWidthDropCount;
                    set => Telemetry.IssuePacketWidthDropCount = value;
                }
                public ulong ReferenceSequentialFallbackCount
                {
                    get => Telemetry.ReferenceSequentialFallbackCount;
                    set => Telemetry.ReferenceSequentialFallbackCount = value;
                }
                public ulong FallbackSofteningPromotionCount
                {
                    get => Telemetry.FallbackSofteningPromotionCount;
                    set => Telemetry.FallbackSofteningPromotionCount = value;
                }
                public ulong NopElisionSkipCount
                {
                    get => Telemetry.NopElisionSkipCount;
                    set => Telemetry.NopElisionSkipCount = value;
                }
                public ulong ScalarLanesRetired
                {
                    get => Telemetry.ScalarLanesRetired;
                    set => Telemetry.ScalarLanesRetired = value;
                }
                public ulong NonScalarLanesRetired
                {
                    get => Telemetry.NonScalarLanesRetired;
                    set => Telemetry.NonScalarLanesRetired = value;
                }
                public ulong RetireCycleCount
                {
                    get => Telemetry.RetireCycleCount;
                    set => Telemetry.RetireCycleCount = value;
                }
                public ulong InvariantViolationCount
                {
                    get => Telemetry.InvariantViolationCount;
                    set => Telemetry.InvariantViolationCount = value;
                }
                public const double ReferenceSequentialFallbackRateBudgetThreshold = 0.05;

                public void Clear()
                {
                    Enabled = false;
                    Stalled = false;
                    StallReason = PipelineStallKind.None;
                    ClusterPreparedModeEnabled = true;
                    Telemetry.ClearPipeline();
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
