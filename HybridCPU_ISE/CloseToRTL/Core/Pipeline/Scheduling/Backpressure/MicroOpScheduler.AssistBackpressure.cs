using System;
using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core
{
    public partial class MicroOpScheduler
    {
        private AssistBackpressureSnapshot CaptureAssistBackpressureSnapshot()
        {
            return new AssistBackpressureSnapshot(
                sharedOuterCapCredits: (byte)Math.Min(
                    _remainingHardwareMemoryIssueBudget,
                    _remainingHardwareLoadIssueBudget),
                consumedSharedReadBudgetByBank: _consumedHardwareLoadBudgetByBank,
                sharedReadBudgetAtLeastOneMask: _hardwareOccupancySnapshot.ReadBankBudgetAtLeastOneMask,
                sharedReadBudgetAtLeastTwoMask: _hardwareOccupancySnapshot.ReadBankBudgetAtLeastTwoMask,
                projectedOutstandingCountVt0: _projectedOutstandingMemoryCountByVt[0],
                projectedOutstandingCountVt1: _projectedOutstandingMemoryCountByVt[1],
                projectedOutstandingCountVt2: _projectedOutstandingMemoryCountByVt[2],
                projectedOutstandingCountVt3: _projectedOutstandingMemoryCountByVt[3],
                projectedOutstandingCapacityVt0: _projectedOutstandingMemoryCapacityByVt[0],
                projectedOutstandingCapacityVt1: _projectedOutstandingMemoryCapacityByVt[1],
                projectedOutstandingCapacityVt2: _projectedOutstandingMemoryCapacityByVt[2],
                projectedOutstandingCapacityVt3: _projectedOutstandingMemoryCapacityByVt[3]);
        }

        private bool TryReserveAssistBackpressure(
            AssistMicroOp assistMicroOp,
            ref AssistBackpressureState assistBackpressureState,
            MemorySubsystem? memSub = null)
        {
            bool dmaSrfAvailable = assistMicroOp.CarrierKind != AssistCarrierKind.Lane6Dma ||
                CanAllocateAssistDmaSrf(
                    assistMicroOp,
                    assistBackpressureState.DmaSrfPartitionPolicy,
                    memSub);

            if (assistBackpressureState.TryReserve(
                assistMicroOp,
                dmaSrfAvailable,
                out AssistBackpressureRejectKind rejectKind))
            {
                return true;
            }

            AssistBackpressureRejects++;
            switch (rejectKind)
            {
                case AssistBackpressureRejectKind.SharedOuterCap:
                    AssistBackpressureOuterCapRejects++;
                    break;
                case AssistBackpressureRejectKind.OutstandingMemory:
                    AssistBackpressureMshrRejects++;
                    break;
                case AssistBackpressureRejectKind.DmaStreamRegisterFile:
                    AssistBackpressureDmaSrfRejects++;
                    break;
            }

            RecordAssistReject(assistMicroOp, TypedSlotRejectReason.AssistBackpressureReject);
            return false;
        }

        private static bool CanAllocateAssistDmaSrf(
            AssistMicroOp assistMicroOp,
            AssistStreamRegisterPartitionPolicy partitionPolicy,
            MemorySubsystem? memSub = null)
        {
            StreamRegisterFile? streamRegisterFile = memSub?.StreamRegisters;
            if (streamRegisterFile == null)
            {
                return true;
            }

            return streamRegisterFile.CanAllocateAssistRegister(
                assistMicroOp.BaseAddress,
                assistMicroOp.ElementSize,
                partitionPolicy,
                out _);
        }
    }
}
