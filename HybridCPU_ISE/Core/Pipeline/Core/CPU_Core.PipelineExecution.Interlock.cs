using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Pipeline;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool AreAllExplicitExecuteLanesReady(in ExecuteStage executeStage)
            {
                for (byte laneIndex = 0; laneIndex < 8; laneIndex++)
                {
                    ScalarExecuteLaneState lane = executeStage.GetLane(laneIndex);
                    if (lane.IsOccupied && !lane.ResultReady)
                        return false;
                }

                return executeStage.MaterializedPhysicalLaneCount > 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool AreAllExplicitMemoryLanesReady(in MemoryStage memoryStage)
            {
                for (byte laneIndex = 0; laneIndex < 8; laneIndex++)
                {
                    ScalarMemoryLaneState lane = memoryStage.GetLane(laneIndex);
                    if (lane.IsOccupied && !lane.ResultReady)
                        return false;
                }

                return memoryStage.MaterializedPhysicalLaneCount > 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static PipelineCycleStallDecision ReportInterlockInvariantViolation() =>
                PipelineCycleStallDecision.InvariantViolation();

            /// <summary>
            /// Check for pipeline hazards that require stalling.
            /// Detects RAW, WAW, load-use, and in-flight memory/vector interlocks.
            /// </summary>
            /// <returns>True if pipeline should stall</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private PipelineCycleStallDecision ResolvePipelineHazardStallDecision()
            {
                if (pipeEX.Valid && pipeEX.MaterializedPhysicalLaneCount == 0)
                {
                    return ReportInterlockInvariantViolation();
                }

                if (pipeMEM.Valid && pipeMEM.MaterializedPhysicalLaneCount == 0)
                {
                    return ReportInterlockInvariantViolation();
                }

                if (pipeID.Valid)
                {
                    if (pipeEX.Valid && ScanExecuteHazardsAcrossLiveRegisterProducerLanes())
                    {
                        return PipelineCycleStallDecision.ForKind(pipeCtrl.StallReason);
                    }

                    if (pipeMEM.Valid && ScanMemoryHazardsAcrossLiveRegisterProducerLanes())
                    {
                        return PipelineCycleStallDecision.ForKind(pipeCtrl.StallReason);
                    }
                }

                if (pipeEX.Valid && pipeEX.UsesExplicitPacketLanes && !AreAllExplicitExecuteLanesReady(pipeEX))
                {
                    return PipelineCycleStallDecision.MemoryWait(
                        countMemoryStall: true,
                        countMshrScoreboardStall: false,
                        countBankConflictStall: false);
                }

                if (pipeEX.Valid && !pipeEX.UsesExplicitPacketLanes && pipeEX.IsMemoryOp && !pipeEX.ResultReady)
                {
                    return PipelineCycleStallDecision.MemoryWait(
                        countMemoryStall: true,
                        countMshrScoreboardStall: false,
                        countBankConflictStall: false);
                }

                if (pipeEX.Valid && pipeEX.IsVectorOp && !pipeEX.VectorComplete)
                {
                    return PipelineCycleStallDecision.MemoryWait(
                        countMemoryStall: true,
                        countMshrScoreboardStall: false,
                        countBankConflictStall: false);
                }

                if (pipeMEM.Valid && pipeMEM.UsesExplicitPacketLanes && !AreAllExplicitMemoryLanesReady(pipeMEM))
                {
                    return PipelineCycleStallDecision.MemoryWait(
                        countMemoryStall: true,
                        countMshrScoreboardStall: false,
                        countBankConflictStall: false);
                }

                if (pipeMEM.Valid && !pipeMEM.UsesExplicitPacketLanes && !pipeMEM.ResultReady)
                {
                    return PipelineCycleStallDecision.MemoryWait(
                        countMemoryStall: true,
                        countMshrScoreboardStall: false,
                        countBankConflictStall: false);
                }

                return PipelineCycleStallDecision.None();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RefreshInFlightExplicitMemoryProgress()
            {
                if (!pipeMEM.Valid || !pipeMEM.UsesExplicitPacketLanes)
                    return;

                ExecuteExplicitPacketMemoryWork();
            }
        }
    }
}
