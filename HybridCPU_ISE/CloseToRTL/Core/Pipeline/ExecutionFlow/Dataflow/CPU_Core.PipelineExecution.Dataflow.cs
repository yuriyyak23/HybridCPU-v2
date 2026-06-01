using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Pipeline;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            private struct LaneAwareForwardingMatch
            {
                public bool ResultReady;
                public ushort DestRegID;
                public ulong ResultValue;
                public PipelineStage SourceStage;
                public byte SourceLaneIndex;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsForwardingRegisterMatch(
                bool isOccupied,
                bool writesRegister,
                ushort destRegId,
                int producerThreadId,
                int consumerThreadId,
                ushort regID)
            {
                return isOccupied &&
                    writesRegister &&
                    destRegId != 0 &&
                    destRegId == regID &&
                    producerThreadId == consumerThreadId;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsHiddenForwardingRegisterMatch(
                bool isOccupied,
                Core.MicroOp microOp,
                ushort primaryDestRegId,
                int producerThreadId,
                int consumerThreadId,
                ushort regID)
            {
                if (!isOccupied || microOp == null || producerThreadId != consumerThreadId || regID == 0)
                    return false;

                var writeRegisters = microOp.WriteRegisters;
                for (int i = 0; i < writeRegisters.Count; i++)
                {
                    int writeReg = writeRegisters[i];
                    if (writeReg <= 0 || writeReg > ushort.MaxValue)
                        continue;

                    ushort writeRegId = (ushort)writeReg;
                    if (writeRegId == primaryDestRegId)
                        continue;

                    if (writeRegId == regID)
                        return true;
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool TryResolveLaneAwareProducerMatch(
                ScalarExecuteLaneState lane,
                int consumerThreadId,
                ushort regID,
                out LaneAwareForwardingMatch match)
            {
                if (IsForwardingRegisterMatch(
                    lane.IsOccupied,
                    lane.WritesRegister,
                    lane.DestRegID,
                    lane.OwnerThreadId,
                    consumerThreadId,
                    regID))
                {
                    match = new LaneAwareForwardingMatch
                    {
                        ResultReady = lane.ResultReady,
                        DestRegID = lane.DestRegID,
                        ResultValue = lane.ResultValue,
                        SourceStage = PipelineStage.Execute,
                        SourceLaneIndex = lane.LaneIndex
                    };
                    return true;
                }

                match = default;
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool TryResolveLaneAwareProducerMatch(
                ScalarMemoryLaneState lane,
                int consumerThreadId,
                ushort regID,
                out LaneAwareForwardingMatch match)
            {
                if (IsForwardingRegisterMatch(
                    lane.IsOccupied,
                    lane.WritesRegister,
                    lane.DestRegID,
                    lane.OwnerThreadId,
                    consumerThreadId,
                    regID))
                {
                    match = new LaneAwareForwardingMatch
                    {
                        ResultReady = lane.ResultReady,
                        DestRegID = lane.DestRegID,
                        ResultValue = lane.ResultValue,
                        SourceStage = PipelineStage.Memory,
                        SourceLaneIndex = lane.LaneIndex
                    };
                    return true;
                }

                match = default;
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool TryResolveLaneAwareProducerMatch(
                ScalarWriteBackLaneState lane,
                int consumerThreadId,
                ushort regID,
                out LaneAwareForwardingMatch match)
            {
                if (IsForwardingRegisterMatch(
                    lane.IsOccupied,
                    lane.WritesRegister,
                    lane.DestRegID,
                    lane.OwnerThreadId,
                    consumerThreadId,
                    regID))
                {
                    match = new LaneAwareForwardingMatch
                    {
                        ResultReady = true,
                        DestRegID = lane.DestRegID,
                        ResultValue = lane.ResultValue,
                        SourceStage = PipelineStage.WriteBack,
                        SourceLaneIndex = lane.LaneIndex
                    };
                    return true;
                }

                match = default;
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool HasHiddenLaneAwareProducerMatch(
                ScalarExecuteLaneState lane,
                int consumerThreadId,
                ushort regID)
            {
                return IsHiddenForwardingRegisterMatch(
                    lane.IsOccupied,
                    lane.MicroOp,
                    lane.DestRegID,
                    lane.OwnerThreadId,
                    consumerThreadId,
                    regID);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool HasHiddenLaneAwareProducerMatch(
                ScalarMemoryLaneState lane,
                int consumerThreadId,
                ushort regID)
            {
                return IsHiddenForwardingRegisterMatch(
                    lane.IsOccupied,
                    lane.MicroOp,
                    lane.DestRegID,
                    lane.OwnerThreadId,
                    consumerThreadId,
                    regID);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool HasHiddenLaneAwareProducerMatch(
                ScalarWriteBackLaneState lane,
                int consumerThreadId,
                ushort regID)
            {
                return IsHiddenForwardingRegisterMatch(
                    lane.IsOccupied,
                    lane.MicroOp,
                    lane.DestRegID,
                    lane.OwnerThreadId,
                    consumerThreadId,
                    regID);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool HasHiddenLaneAwareProducerMatch(int consumerThreadId, ushort regID)
            {
                if (regID == 0)
                    return false;

                return HasHiddenLaneAwareProducerMatch(pipeEX.Lane0, consumerThreadId, regID)
                    || HasHiddenLaneAwareProducerMatch(pipeEX.Lane1, consumerThreadId, regID)
                    || HasHiddenLaneAwareProducerMatch(pipeEX.Lane2, consumerThreadId, regID)
                    || HasHiddenLaneAwareProducerMatch(pipeEX.Lane3, consumerThreadId, regID)
                    || HasHiddenLaneAwareProducerMatch(pipeEX.Lane4, consumerThreadId, regID)
                    || HasHiddenLaneAwareProducerMatch(pipeEX.Lane5, consumerThreadId, regID)
                    || HasHiddenLaneAwareProducerMatch(pipeEX.Lane7, consumerThreadId, regID)
                    || HasHiddenLaneAwareProducerMatch(pipeMEM.Lane0, consumerThreadId, regID)
                    || HasHiddenLaneAwareProducerMatch(pipeMEM.Lane1, consumerThreadId, regID)
                    || HasHiddenLaneAwareProducerMatch(pipeMEM.Lane2, consumerThreadId, regID)
                    || HasHiddenLaneAwareProducerMatch(pipeMEM.Lane3, consumerThreadId, regID)
                    || HasHiddenLaneAwareProducerMatch(pipeMEM.Lane4, consumerThreadId, regID)
                    || HasHiddenLaneAwareProducerMatch(pipeMEM.Lane5, consumerThreadId, regID)
                    || HasHiddenLaneAwareProducerMatch(pipeMEM.Lane7, consumerThreadId, regID)
                    || HasHiddenLaneAwareProducerMatch(pipeWB.Lane0, consumerThreadId, regID)
                    || HasHiddenLaneAwareProducerMatch(pipeWB.Lane1, consumerThreadId, regID)
                    || HasHiddenLaneAwareProducerMatch(pipeWB.Lane2, consumerThreadId, regID)
                    || HasHiddenLaneAwareProducerMatch(pipeWB.Lane3, consumerThreadId, regID)
                    || HasHiddenLaneAwareProducerMatch(pipeWB.Lane4, consumerThreadId, regID)
                    || HasHiddenLaneAwareProducerMatch(pipeWB.Lane5, consumerThreadId, regID)
                    || HasHiddenLaneAwareProducerMatch(pipeWB.Lane7, consumerThreadId, regID);
            }

            // Lane-aware forwarding must observe the same live register-producing subset so
            // widened LSU loads on lanes 4..5 and any future link-producing lane-7 branch do not
            // disappear back into scalar-only lookup.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryResolveLaneAwareProducerMatch(int consumerThreadId, ushort regID, out LaneAwareForwardingMatch match)
            {
                if (regID == 0)
                {
                    match = default;
                    return false;
                }

                return TryResolveLaneAwareProducerMatch(pipeEX.Lane0, consumerThreadId, regID, out match)
                    || TryResolveLaneAwareProducerMatch(pipeEX.Lane1, consumerThreadId, regID, out match)
                    || TryResolveLaneAwareProducerMatch(pipeEX.Lane2, consumerThreadId, regID, out match)
                    || TryResolveLaneAwareProducerMatch(pipeEX.Lane3, consumerThreadId, regID, out match)
                    || TryResolveLaneAwareProducerMatch(pipeEX.Lane4, consumerThreadId, regID, out match)
                    || TryResolveLaneAwareProducerMatch(pipeEX.Lane5, consumerThreadId, regID, out match)
                    || TryResolveLaneAwareProducerMatch(pipeEX.Lane7, consumerThreadId, regID, out match)
                    || TryResolveLaneAwareProducerMatch(pipeMEM.Lane0, consumerThreadId, regID, out match)
                    || TryResolveLaneAwareProducerMatch(pipeMEM.Lane1, consumerThreadId, regID, out match)
                    || TryResolveLaneAwareProducerMatch(pipeMEM.Lane2, consumerThreadId, regID, out match)
                    || TryResolveLaneAwareProducerMatch(pipeMEM.Lane3, consumerThreadId, regID, out match)
                    || TryResolveLaneAwareProducerMatch(pipeMEM.Lane4, consumerThreadId, regID, out match)
                    || TryResolveLaneAwareProducerMatch(pipeMEM.Lane5, consumerThreadId, regID, out match)
                    || TryResolveLaneAwareProducerMatch(pipeMEM.Lane7, consumerThreadId, regID, out match)
                    || TryResolveLaneAwareProducerMatch(pipeWB.Lane0, consumerThreadId, regID, out match)
                    || TryResolveLaneAwareProducerMatch(pipeWB.Lane1, consumerThreadId, regID, out match)
                    || TryResolveLaneAwareProducerMatch(pipeWB.Lane2, consumerThreadId, regID, out match)
                    || TryResolveLaneAwareProducerMatch(pipeWB.Lane3, consumerThreadId, regID, out match)
                    || TryResolveLaneAwareProducerMatch(pipeWB.Lane4, consumerThreadId, regID, out match)
                    || TryResolveLaneAwareProducerMatch(pipeWB.Lane5, consumerThreadId, regID, out match)
                    || TryResolveLaneAwareProducerMatch(pipeWB.Lane7, consumerThreadId, regID, out match);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool IsLaneAwareForwardingReady(int consumerThreadId, ushort regID)
            {
                if (regID == 0)
                {
                    return true;
                }

                if (TryResolveLaneAwareProducerMatch(consumerThreadId, regID, out LaneAwareForwardingMatch match))
                {
                    return match.ResultReady;
                }

                long currentCycle = (long)pipeCtrl.CycleCount;

                if (forwardEX.Valid && forwardEX.DestRegID == regID)
                {
                    return forwardEX.IsAvailable(currentCycle);
                }

                if (forwardMEM.Valid && forwardMEM.DestRegID == regID)
                {
                    return forwardMEM.IsAvailable(currentCycle);
                }

                if (forwardWB.Valid && forwardWB.DestRegID == regID)
                {
                    return forwardWB.IsAvailable(currentCycle);
                }

                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private ulong GetRegisterValueWithForwarding(int consumerThreadId, ushort regID)
            {
                if (regID == 0 || regID >= YAKSys_Hybrid_CPU.Core.Registers.RenameMap.ArchRegs)
                    return 0;

                if (TryResolveLaneAwareProducerMatch(consumerThreadId, regID, out LaneAwareForwardingMatch laneAwareMatch))
                {
                    if (laneAwareMatch.ResultReady)
                    {
                        pipeCtrl.ForwardingEvents++;
                        return laneAwareMatch.ResultValue;
                    }

                    return ReadArch(consumerThreadId, regID);
                }

                long currentCycle = (long)pipeCtrl.CycleCount;

                if (forwardEX.Valid && forwardEX.DestRegID == regID && forwardEX.IsAvailable(currentCycle))
                {
                    pipeCtrl.ForwardingEvents++;
                    return forwardEX.ForwardedValue;
                }

                if (forwardMEM.Valid && forwardMEM.DestRegID == regID && forwardMEM.IsAvailable(currentCycle))
                {
                    pipeCtrl.ForwardingEvents++;
                    return forwardMEM.ForwardedValue;
                }

                if (forwardWB.Valid && forwardWB.DestRegID == regID && forwardWB.IsAvailable(currentCycle))
                {
                    pipeCtrl.ForwardingEvents++;
                    return forwardWB.ForwardedValue;
                }

                return ReadArch(consumerThreadId, regID);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool CheckForwardingReadiness(ushort reg2ID, ushort reg3ID)
            {
                int consumerThreadId = GetCurrentDecodeThreadId();
                return IsLaneAwareForwardingReady(consumerThreadId, reg2ID)
                    && IsLaneAwareForwardingReady(consumerThreadId, reg3ID);
            }
        }
    }
}
