namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Reference-owned per-core diagnostic telemetry. This state observes existing
/// owner events; it is not an execution, legality, timing, commit, rollback or
/// publication authority.
/// </summary>
internal sealed class TelemetryState
{
    internal ulong CycleCount;
    internal ulong StallCycles;
    internal ulong InstructionsRetired;
    internal ulong Lane7ConditionalBranchExecuteCompletionCount;
    internal ulong Lane7ConditionalBranchRedirectCount;
    internal ulong BranchMispredicts;
    internal ulong DataHazards;
    internal ulong MemoryStalls;
    internal ulong ForwardingEvents;
    internal ulong ControlHazards;
    internal ulong WAWHazards;
    internal ulong LoadUseBubbles;
    internal ulong FrontendStalls;
    internal ulong DomainSquashCount;
    internal ulong EarlyDomainSquashCount;
    internal ulong MshrScoreboardStalls;
    internal ulong BankConflictStallCycles;
    internal ulong ExceptionYoungerSuppressCount;
    internal ulong MultiSlotDecodeAdvanceCount;
    internal ulong MultiLaneExecuteCount;
    internal ulong MemoryFaultCarrierCount;
    internal ulong AuxiliaryCoexistenceConflictCount;
    internal ulong[] ScalarIssueWidthHistogram = new ulong[5];
    internal ulong ClusterProbeCount;
    internal ulong ClusterProbeRefinedWidthSum;
    internal ulong ClusterProbeNarrowFallbackCount;
    internal ulong ClusterPreparedExecutionChoiceCount;
    internal ulong DifferentialTraceCompareCount;
    internal ulong DifferentialTraceDiscrepancyCount;
    internal ulong ClusterModeFallbackCount;
    internal ulong DecoderPreparedScalarGroupCount;
    internal ulong DecoderPreparedFallbackCount;
    internal ulong DecodeFallbackCount;
    internal ulong DecodeFaultBundleCount;
    internal ulong CrossSlotRejectCount;
    internal ulong HazardRegisterDataCount;
    internal ulong HazardMemoryBankCount;
    internal ulong HazardControlFlowCount;
    internal ulong HazardSystemBarrierCount;
    internal ulong HazardPinnedLaneCount;
    internal ulong ScalarClusterEligibleButBlockedCount;
    internal ulong ReferenceFallbackDueToControlConflictCount;
    internal ulong ReferenceFallbackDueToMemoryConflictCount;
    internal ulong VTSpreadPerBundle;
    internal ulong BurstReadCycles;
    internal ulong BurstWriteCycles;
    internal ulong ComputeCycles;
    internal ulong OverlappedCycles;
    internal ulong WidePathGate3_ReferenceSequentialCount;
    internal ulong WidePathGate4_NarrowFallbackCount;
    internal ulong WidePathGate5_NotClusterCandidateCount;
    internal ulong WidePathGate6_PreparedMaskZeroCount;
    internal ulong WidePathSuccessCount;
    internal ulong RefinedMaskPromotionCount;
    internal ulong PartialWidthIssueCount;
    internal ulong IssuePacketPreparedLaneCountSum;
    internal ulong IssuePacketMaterializedLaneCountSum;
    internal ulong IssuePacketPreparedPhysicalLaneCountSum;
    internal ulong IssuePacketMaterializedPhysicalLaneCountSum;
    internal ulong IssuePacketWidthDropCount;
    internal ulong ReferenceSequentialFallbackCount;
    internal ulong FallbackSofteningPromotionCount;
    internal ulong NopElisionSkipCount;
    internal ulong ScalarLanesRetired;
    internal ulong NonScalarLanesRetired;
    internal ulong RetireCycleCount;
    internal ulong InvariantViolationCount;
    internal DifferentialTraceCapture? DifferentialTraceCapture;

    internal long AssistLaunchCount;
    internal long AssistCompletedCount;
    internal long AssistKilledCount;
    internal long AssistInvalidationCount;
    internal ulong TestReferenceRawFallbackCount;

    internal void ClearPipeline()
    {
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

    internal void ClearAssist()
    {
        AssistLaunchCount = 0;
        AssistCompletedCount = 0;
        AssistKilledCount = 0;
        AssistInvalidationCount = 0;
    }

    internal TelemetryState CreateSnapshot()
    {
        var snapshot = (TelemetryState)MemberwiseClone();
        snapshot.ScalarIssueWidthHistogram =
            (ulong[])ScalarIssueWidthHistogram.Clone();
        return snapshot;
    }
}
