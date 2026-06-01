using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Pipeline;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            // The live register-producing widened subset currently extends through lanes 4..5,
            // so decode-side hazard tracking must follow the same authoritative producer window.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool ScanExecuteHazardAcrossLiveRegisterProducerLane(ScalarExecuteLaneState lane, int consumerThreadId)
            {
                if (!lane.IsOccupied || !lane.WritesRegister || lane.DestRegID == 0)
                {
                    return false;
                }

                if (NormalizePipelineStateVtId(lane.OwnerThreadId) != consumerThreadId)
                {
                    return false;
                }

                if (PendingDecodeWindowReadsRegister(consumerThreadId, lane.DestRegID))
                {
                    if (!lane.ResultReady)
                    {
                        pipeCtrl.DataHazards++;
                        pipeCtrl.StallReason = PipelineStallKind.DataHazard;
                        return true;
                    }

                    if (lane.IsMemoryOp && lane.IsLoad)
                    {
                        pipeCtrl.LoadUseBubbles++;
                        pipeCtrl.StallReason = PipelineStallKind.DataHazard;
                        return true;
                    }
                }

                if (PendingDecodeWindowWritesRegister(consumerThreadId, lane.DestRegID) && !lane.ResultReady)
                {
                    pipeCtrl.WAWHazards++;
                    pipeCtrl.StallReason = PipelineStallKind.DataHazard;
                    return true;
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool ScanExecuteHazardsAcrossLiveRegisterProducerLanes()
            {
                int consumerThreadId = GetCurrentDecodeThreadId();
                return ScanExecuteHazardAcrossLiveRegisterProducerLane(pipeEX.Lane0, consumerThreadId)
                    || ScanExecuteHazardAcrossLiveRegisterProducerLane(pipeEX.Lane1, consumerThreadId)
                    || ScanExecuteHazardAcrossLiveRegisterProducerLane(pipeEX.Lane2, consumerThreadId)
                    || ScanExecuteHazardAcrossLiveRegisterProducerLane(pipeEX.Lane3, consumerThreadId)
                    || ScanExecuteHazardAcrossLiveRegisterProducerLane(pipeEX.Lane4, consumerThreadId)
                    || ScanExecuteHazardAcrossLiveRegisterProducerLane(pipeEX.Lane5, consumerThreadId)
                    || ScanExecuteHazardAcrossLiveRegisterProducerLane(pipeEX.Lane7, consumerThreadId);
            }

            // MEM-side RAW/WAW tracking uses the same live register-producing subset as EX/WB,
            // including lane 7 when the branch carrier ever exposes a register write.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool ScanMemoryHazardAcrossLiveRegisterProducerLane(ScalarMemoryLaneState lane, int consumerThreadId)
            {
                if (!lane.IsOccupied || !lane.WritesRegister || lane.DestRegID == 0)
                {
                    return false;
                }

                if (NormalizePipelineStateVtId(lane.OwnerThreadId) != consumerThreadId)
                {
                    return false;
                }

                // WAW: only stall when MEM result is not yet ready.
                // When ResultReady, WB will drain MEM this cycle.
                if (PendingDecodeWindowWritesRegister(consumerThreadId, lane.DestRegID) && !lane.ResultReady)
                {
                    pipeCtrl.WAWHazards++;
                    pipeCtrl.StallReason = PipelineStallKind.DataHazard;
                    return true;
                }

                if (PendingDecodeWindowReadsRegister(consumerThreadId, lane.DestRegID) && !lane.ResultReady)
                {
                    pipeCtrl.DataHazards++;
                    pipeCtrl.StallReason = PipelineStallKind.DataHazard;
                    return true;
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool ScanMemoryHazardsAcrossLiveRegisterProducerLanes()
            {
                int consumerThreadId = GetCurrentDecodeThreadId();
                return ScanMemoryHazardAcrossLiveRegisterProducerLane(pipeMEM.Lane0, consumerThreadId)
                    || ScanMemoryHazardAcrossLiveRegisterProducerLane(pipeMEM.Lane1, consumerThreadId)
                    || ScanMemoryHazardAcrossLiveRegisterProducerLane(pipeMEM.Lane2, consumerThreadId)
                    || ScanMemoryHazardAcrossLiveRegisterProducerLane(pipeMEM.Lane3, consumerThreadId)
                    || ScanMemoryHazardAcrossLiveRegisterProducerLane(pipeMEM.Lane4, consumerThreadId)
                    || ScanMemoryHazardAcrossLiveRegisterProducerLane(pipeMEM.Lane5, consumerThreadId)
                    || ScanMemoryHazardAcrossLiveRegisterProducerLane(pipeMEM.Lane7, consumerThreadId);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool RegisterSetContains(
                System.Collections.Generic.IReadOnlyList<int>? registerSet,
                ushort registerId)
            {
                if (registerId == 0 || registerSet == null)
                    return false;

                for (int i = 0; i < registerSet.Count; i++)
                {
                    if (registerSet[i] == registerId)
                        return true;
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool DecodeStageReadsRegister(ushort registerId)
            {
                return pipeID.Valid &&
                       RegisterSetContains(pipeID.MicroOp?.ReadRegisters, registerId);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool DecodeStageWritesRegister(ushort registerId)
            {
                return pipeID.WritesRegister &&
                       RegisterSetContains(pipeID.MicroOp?.WriteRegisters, registerId);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool IssuePacketReadsRegister(int consumerThreadId, ushort registerId)
            {
                if (!pipeIDAdmissionDecisionDraft.UsesIssuePacketAsExecutionSource || registerId == 0)
                    return false;

                Core.BundleIssuePacket issuePacket = pipeIDAdmissionHandoff.IssuePacket;
                byte executableNonScalarPhysicalLaneMask =
                    ResolveExecutableNonScalarPhysicalLaneMask(
                        issuePacket,
                        pipeIDAdmissionHandoff.DependencySummary);

                for (byte laneIndex = 0; laneIndex < 8; laneIndex++)
                {
                    if (!ShouldMaterializeIssuePacketLane(issuePacket, laneIndex, executableNonScalarPhysicalLaneMask))
                        continue;

                    Core.IssuePacketLane lane = issuePacket.GetPhysicalLane(laneIndex);
                    if (NormalizePipelineStateVtId(lane.OwnerThreadId) != consumerThreadId)
                        continue;

                    if (RegisterSetContains(lane.MicroOp?.ReadRegisters, registerId))
                        return true;
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool IssuePacketWritesRegister(int consumerThreadId, ushort registerId)
            {
                if (!pipeIDAdmissionDecisionDraft.UsesIssuePacketAsExecutionSource || registerId == 0)
                    return false;

                Core.BundleIssuePacket issuePacket = pipeIDAdmissionHandoff.IssuePacket;
                byte executableNonScalarPhysicalLaneMask =
                    ResolveExecutableNonScalarPhysicalLaneMask(
                        issuePacket,
                        pipeIDAdmissionHandoff.DependencySummary);

                for (byte laneIndex = 0; laneIndex < 8; laneIndex++)
                {
                    if (!ShouldMaterializeIssuePacketLane(issuePacket, laneIndex, executableNonScalarPhysicalLaneMask))
                        continue;

                    Core.IssuePacketLane lane = issuePacket.GetPhysicalLane(laneIndex);
                    if (NormalizePipelineStateVtId(lane.OwnerThreadId) != consumerThreadId)
                        continue;

                    if (RegisterSetContains(lane.MicroOp?.WriteRegisters, registerId))
                        return true;
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool PendingDecodeWindowReadsRegister(int consumerThreadId, ushort registerId)
            {
                if (pipeIDAdmissionDecisionDraft.UsesIssuePacketAsExecutionSource)
                    return IssuePacketReadsRegister(consumerThreadId, registerId);

                return GetCurrentDecodeThreadId() == consumerThreadId && DecodeStageReadsRegister(registerId);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool PendingDecodeWindowWritesRegister(int consumerThreadId, ushort registerId)
            {
                if (pipeIDAdmissionDecisionDraft.UsesIssuePacketAsExecutionSource)
                    return IssuePacketWritesRegister(consumerThreadId, registerId);

                return GetCurrentDecodeThreadId() == consumerThreadId && DecodeStageWritesRegister(registerId);
            }
        }
    }
}
