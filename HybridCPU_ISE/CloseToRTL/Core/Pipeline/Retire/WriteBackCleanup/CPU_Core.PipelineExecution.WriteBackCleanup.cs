using System;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReleaseRetiredWriteBackLaneBookkeeping(byte laneIndex)
            {
                ReleaseScalarLaneBookkeeping(pipeWB.GetLane(laneIndex));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReleaseDeferredNonRetiringWriteBackLaneBookkeeping()
            {
                ReleaseScalarLaneBookkeeping(pipeWB.Lane6);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReleaseArchitecturallyInvisibleWriteBackLaneBookkeeping()
            {
                for (byte laneIndex = 0; laneIndex < 8; laneIndex++)
                {
                    if (laneIndex == 6)
                        continue;

                    ScalarWriteBackLaneState lane = pipeWB.GetLane(laneIndex);
                    if (!lane.IsOccupied ||
                        lane.MicroOp == null ||
                        lane.MicroOp.IsRetireVisible)
                    {
                        continue;
                    }

                    ReleaseScalarLaneBookkeeping(lane);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ClearDeferredNonRetiringWriteBackLanes()
            {
                ScalarWriteBackLaneState clearedLane = new();
                clearedLane.Clear(6);
                pipeWB.SetLane(6, clearedLane);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ClearArchitecturallyInvisibleWriteBackLanes()
            {
                for (byte laneIndex = 0; laneIndex < 8; laneIndex++)
                {
                    if (laneIndex == 6)
                        continue;

                    ScalarWriteBackLaneState lane = pipeWB.GetLane(laneIndex);
                    if (!lane.IsOccupied ||
                        lane.MicroOp == null ||
                        lane.MicroOp.IsRetireVisible)
                    {
                        continue;
                    }

                    ScalarWriteBackLaneState clearedLane = new();
                    clearedLane.Clear(laneIndex);
                    pipeWB.SetLane(laneIndex, clearedLane);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void HandleEmptyWriteBackRetireWindow(
                bool hasRetireWindowExceptionDecision,
                PipelineStage retireWindowExceptionStage,
                byte retireWindowExceptionLaneIndex,
                bool retireWindowShouldSuppressYoungerWork)
            {
                if (hasRetireWindowExceptionDecision)
                {
                    DeliverStageAwareRetireWindowFault(
                        retireWindowExceptionStage,
                        retireWindowExceptionLaneIndex,
                        retireWindowShouldSuppressYoungerWork);
                }

                ReleaseDeferredNonRetiringWriteBackLaneBookkeeping();
                ClearDeferredNonRetiringWriteBackLanes();
                ReleaseArchitecturallyInvisibleWriteBackLaneBookkeeping();
                ClearArchitecturallyInvisibleWriteBackLanes();

                byte remainingOccupiedMask = ResolveRetireEligibleWriteBackLanesExcludingFaulted(pipeWB);
                if (remainingOccupiedMask == 0)
                {
                    pipeWB.Clear();
                    forwardWB.Clear();
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ClearRetiredWriteBackLane(byte laneIndex)
            {
                if (!IsRetireAuthoritativeWriteBackLane(laneIndex))
                {
                    ReleaseScalarLaneBookkeeping(pipeWB.GetLane(laneIndex));
                    pipeWB.Clear();
                    forwardWB.Clear();
                    return;
                }

                ScalarWriteBackLaneState clearedLane = new();
                clearedLane.Clear(laneIndex);
                pipeWB.SetLane(laneIndex, clearedLane);

                byte remainingOccupiedMask = ResolveRetireEligibleWriteBackLanesExcludingFaulted(pipeWB);
                if (remainingOccupiedMask == 0)
                {
                    pipeWB.Clear();
                    forwardWB.Clear();
                    return;
                }

                pipeWB.Valid = true;
                pipeWB.MaterializedScalarLaneCount = CountOccupiedScalarWriteBackLanes(pipeWB);
                pipeWB.MaterializedPhysicalLaneCount = CountOccupiedPhysicalWriteBackLanes(pipeWB);
                pipeWB.ActiveLaneIndex = ResolveConservativeRetireEligibleLaneIndex(pipeWB, remainingOccupiedMask);
                forwardWB.Clear();
            }

            /// <summary>
            /// Clear all retired write-back lanes from the current packet-local retire set.
            /// If any occupied lanes remain (for example faulted lanes excluded from normal retire),
            /// the WB stage stays valid with the remaining lanes. Otherwise WB is fully cleared.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ClearRetiredWriteBackLanes(Span<byte> retireOrder, int retireLaneCount)
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

                ReleaseDeferredNonRetiringWriteBackLaneBookkeeping();
                ClearDeferredNonRetiringWriteBackLanes();
                ReleaseArchitecturallyInvisibleWriteBackLaneBookkeeping();
                ClearArchitecturallyInvisibleWriteBackLanes();

                byte remainingOccupiedMask = ResolveRetireEligibleWriteBackLanesExcludingFaulted(pipeWB);
                if (remainingOccupiedMask == 0)
                {
                    pipeWB.Clear();
                    forwardWB.Clear();
                    return;
                }

                // Faulted or otherwise non-retired lanes remain; keep WB valid.
                pipeWB.Valid = true;
                pipeWB.MaterializedScalarLaneCount = CountOccupiedScalarWriteBackLanes(pipeWB);
                pipeWB.MaterializedPhysicalLaneCount = CountOccupiedPhysicalWriteBackLanes(pipeWB);
                pipeWB.ActiveLaneIndex = ResolveConservativeRetireEligibleLaneIndex(pipeWB, remainingOccupiedMask);
                forwardWB.Clear();
            }
        }
    }
}
