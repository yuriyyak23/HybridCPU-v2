using System;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Pipeline;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int TruncateRetireOrderBeforeWriteBackFaultWinner(
                in WriteBackStage writeBackStage,
                Span<byte> retireOrder,
                int retireLaneCount,
                PipelineStage winnerStage,
                byte winnerLaneIndex)
            {
                if (winnerStage != PipelineStage.WriteBack || !IsRetireAuthoritativeWriteBackLane(winnerLaneIndex))
                    return retireLaneCount;

                int truncatedCount = 0;
                for (int i = 0; i < retireLaneCount; i++)
                {
                    byte laneIndex = retireOrder[i];
                    if (CompareWriteBackLaneOrder(
                            writeBackStage,
                            laneIndex,
                            winnerLaneIndex) >= 0)
                    {
                        continue;
                    }

                    retireOrder[truncatedCount++] = laneIndex;
                }

                return truncatedCount;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte ResolveSuppressedYoungerWriteBackPeerMask(
                in WriteBackStage writeBackStage,
                byte winnerLaneIndex)
            {
                if (!IsRetireAuthoritativeWriteBackLane(winnerLaneIndex))
                    return 0;

                byte occupiedMask = GetOccupiedRetireEligibleWriteBackLaneMask(writeBackStage);
                byte suppressedMask = 0;

                for (byte laneIndex = 0; laneIndex < 8; laneIndex++)
                {
                    if (!IsRetireAuthoritativeWriteBackLane(laneIndex))
                        continue;

                    if ((occupiedMask & (1 << laneIndex)) == 0)
                        continue;

                    if (CompareWriteBackLaneOrder(
                            writeBackStage,
                            winnerLaneIndex,
                            laneIndex) >= 0)
                    {
                        continue;
                    }

                    suppressedMask |= (byte)(1 << laneIndex);
                }

                return suppressedMask;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ClearRetiredWriteBackLaneStateBeforeFaultDelivery(
                Span<byte> retireOrder,
                int retireLaneCount)
            {
                for (int i = 0; i < retireLaneCount; i++)
                {
                    byte laneIndex = retireOrder[i];
                    if (!IsRetireAuthoritativeWriteBackLane(laneIndex))
                        continue;

                    ScalarWriteBackLaneState clearedLane = new();
                    clearedLane.Clear(laneIndex);
                    pipeWB.SetLane(laneIndex, clearedLane);
                }

                forwardWB.Clear();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void DeliverStageAwareRetireWindowFault(
                PipelineStage winnerStage,
                byte winnerLaneIndex,
                bool shouldSuppressYoungerWork)
            {
                if (!TryResolveStageAwareExceptionWinnerMetadata(
                    pipeWB,
                    pipeMEM,
                    pipeEX,
                    out StageAwareExceptionWinnerMetadata winnerMetadata))
                {
                    throw new InvalidOperationException("WB retire-window fault delivery requires an authoritative stage-aware winner.");
                }

                byte suppressedYoungerStageMask = ResolveExceptionWinnerSuppressedLaneMask(
                    winnerStage,
                    pipeMEM,
                    pipeEX);
                byte suppressedYoungerWriteBackPeerMask = winnerStage == PipelineStage.WriteBack
                    ? ResolveSuppressedYoungerWriteBackPeerMask(pipeWB, winnerLaneIndex)
                    : (byte)0;

                if (shouldSuppressYoungerWork ||
                    suppressedYoungerStageMask != 0 ||
                    suppressedYoungerWriteBackPeerMask != 0)
                {
                    pipeCtrl.ExceptionYoungerSuppressCount++;
                }

                FlushPipeline(Core.AssistInvalidationReason.Trap);
                throw new Core.PageFaultException(
                    winnerMetadata.FaultAddress,
                    winnerMetadata.FaultIsWrite);
            }
        }
    }
}
