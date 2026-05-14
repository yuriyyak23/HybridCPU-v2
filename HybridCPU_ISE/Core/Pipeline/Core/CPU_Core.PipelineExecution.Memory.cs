using System;
using System.IO;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Pipeline;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte NormalizeScalarMemoryAccessSize(byte memoryAccessSize) =>
                memoryAccessSize == 0 ? (byte)8 : memoryAccessSize;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private byte[] ReadExplicitPacketLoadIntoReusableBuffer(ulong address, byte accessSize)
            {
                if (_explicitPacketImmediateReadBuffer == null || _explicitPacketImmediateReadBuffer.Length < accessSize)
                {
                    _explicitPacketImmediateReadBuffer = new byte[accessSize];
                }

                _explicitPacketImmediateReadBuffer = ReadBoundMainMemory(
                    address,
                    _explicitPacketImmediateReadBuffer,
                    accessSize,
                    "Explicit-packet synchronous fallback load");

                return _explicitPacketImmediateReadBuffer;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool IsScalarMemoryAccessInBounds(ulong address, byte accessSize)
            {
                ulong normalizedAccessSize = NormalizeScalarMemoryAccessSize(accessSize);
                ulong mainMemoryLength = GetBoundMainMemoryLength();
                return normalizedAccessSize <= mainMemoryLength &&
                    address <= mainMemoryLength - normalizedAccessSize;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryApplySingleLaneMemoryStageDomainSquash()
            {
                if (pipeEX.DomainTag == 0 || CsrMemDomainCert == 0)
                    return false;

                if ((pipeEX.DomainTag & CsrMemDomainCert) != 0)
                    return false;

                pipeMEM.WritesRegister = false;
                pipeMEM.ResultValue = 0;
                pipeMEM.ResultReady = true;
                pipeCtrl.DomainSquashCount++;
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PublishSingleLaneMemoryStageResult()
            {
                if (!pipeEX.IsMemoryOp)
                {
                    pipeMEM.ResultValue = pipeEX.ResultValue;
                    pipeMEM.ResultReady = true;
                    PublishExecuteCompletionContourCertificate(
                        pipeMEM.GetLane(pipeMEM.ActiveLaneIndex).MicroOp != null
                            ? Core.PipelineContourOwner.SingleLaneMicroOpExecution
                            : Core.PipelineContourOwner.ReferenceExecution,
                        Core.PipelineContourVisibilityStage.Memory,
                        pipeMEM.PC,
                        (byte)(1 << pipeMEM.ActiveLaneIndex));
                    return;
                }

                if (pipeEX.GeneratedAtomicEffect.HasValue)
                {
                    pipeMEM.ResultValue = 0;
                    pipeMEM.ResultReady = true;
                    PublishExecuteCompletionContourCertificate(
                        pipeMEM.GetLane(pipeMEM.ActiveLaneIndex).MicroOp != null
                            ? Core.PipelineContourOwner.SingleLaneMicroOpExecution
                            : Core.PipelineContourOwner.ReferenceExecution,
                        Core.PipelineContourVisibilityStage.Memory,
                        pipeMEM.PC,
                        (byte)(1 << pipeMEM.ActiveLaneIndex));
                    return;
                }

                if (pipeEX.IsLoad)
                {
                    ScalarExecuteLaneState executeLane = pipeEX.GetLane(pipeEX.ActiveLaneIndex);
                    ulong address = pipeEX.MemoryAddress;
                    byte loadAccessSize = NormalizeScalarMemoryAccessSize(executeLane.MemoryAccessSize);
                    if (IsScalarMemoryAccessInBounds(address, loadAccessSize))
                    {
                        byte[] buffer = new byte[loadAccessSize];
                        ReadBoundMainMemory(
                            address,
                            buffer,
                            loadAccessSize,
                            "PipelineStage_Memory() synchronous single-lane load");
                        pipeMEM.ResultValue = Core.LoadStoreMicroOp.DecodeLoadValue(
                            executeLane.OpCode,
                            buffer,
                            loadAccessSize,
                            "PipelineStage_Memory() synchronous single-lane load");
                    }
                    else
                    {
                        Core.PageFaultException loadFault = new(address, isWrite: false);
                        pipeCtrl.MemoryFaultCarrierCount++;
                        DeliverStageAwareMemoryPageFault(loadFault);
                    }

                    pipeMEM.ResultReady = true;
                    PublishExecuteCompletionContourCertificate(
                        pipeMEM.GetLane(pipeMEM.ActiveLaneIndex).MicroOp != null
                            ? Core.PipelineContourOwner.SingleLaneMicroOpExecution
                            : Core.PipelineContourOwner.ReferenceExecution,
                        Core.PipelineContourVisibilityStage.Memory,
                        pipeMEM.PC,
                        (byte)(1 << pipeMEM.ActiveLaneIndex));
                    return;
                }

                ulong storeAddress = pipeEX.MemoryAddress;
                ulong storeData = pipeEX.MemoryData;
                byte accessSize = NormalizeScalarMemoryAccessSize(
                    pipeEX.GetLane(pipeEX.ActiveLaneIndex).MemoryAccessSize);
                if (IsScalarMemoryAccessInBounds(storeAddress, accessSize))
                {
                    ScalarMemoryLaneState lane = pipeMEM.GetLane(pipeMEM.ActiveLaneIndex);
                    lane.MemoryAccessSize = accessSize;
                    lane.DefersStoreCommitToWriteBack = true;
                    pipeMEM.SetLane(pipeMEM.ActiveLaneIndex, lane);
                }
                else
                {
                    Core.PageFaultException storeFault = new(storeAddress, isWrite: true);
                    pipeCtrl.MemoryFaultCarrierCount++;
                    DeliverStageAwareMemoryPageFault(storeFault);
                }

                pipeMEM.ResultValue = storeData;
                pipeMEM.ResultReady = true;
                PublishExecuteCompletionContourCertificate(
                    pipeMEM.GetLane(pipeMEM.ActiveLaneIndex).MicroOp != null
                        ? Core.PipelineContourOwner.SingleLaneMicroOpExecution
                        : Core.PipelineContourOwner.ReferenceExecution,
                    Core.PipelineContourVisibilityStage.Memory,
                    pipeMEM.PC,
                    (byte)(1 << pipeMEM.ActiveLaneIndex));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PublishMemoryStageForwarding()
            {
                if (!pipeMEM.WritesRegister || !pipeMEM.ResultReady)
                    return;

                forwardMEM.Valid = true;
                forwardMEM.DestRegID = pipeMEM.DestRegID;
                forwardMEM.ForwardedValue = pipeMEM.ResultValue;
                pipeCtrl.ForwardingEvents++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplyRetiredScalarStoreCommit(in ScalarWriteBackLaneState lane)
            {
                if (!lane.DefersStoreCommitToWriteBack || !lane.IsMemoryOp || lane.IsLoad)
                    return;

                ApplyRetiredScalarStoreCommit(
                    lane.MemoryAddress,
                    lane.MemoryData,
                    lane.MemoryAccessSize,
                    $"Retired store lane {lane.LaneIndex}");
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplyRetiredScalarStoreCommit(
                ulong memoryAddress,
                ulong memoryData,
                byte memoryAccessSize,
                string carrierDescription)
            {
                byte accessSize = NormalizeScalarMemoryAccessSize(memoryAccessSize);
                if (!IsScalarMemoryAccessInBounds(memoryAddress, accessSize))
                {
                    throw new InvalidOperationException(
                        $"{carrierDescription} carries out-of-range memory commit.");
                }

                WriteBoundMainMemory(
                    memoryAddress,
                    CreateExplicitPacketStoreBuffer(memoryData, accessSize),
                    carrierDescription);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private DmaStreamComputeCommitResult ApplyRetiredDmaStreamComputeTokenCommit(
                DmaStreamComputeToken token,
                DmaStreamComputeOwnerGuardDecision commitGuardDecision)
            {
                ArgumentNullException.ThrowIfNull(token);

                DmaStreamComputeCommitResult result =
                    token.Commit(GetBoundMainMemory(), commitGuardDecision);

                if (result.RequiresRetireExceptionPublication)
                {
                    throw result.CreateRetireException();
                }

                return result;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte[] CreateExplicitPacketStoreBuffer(ulong data, byte accessSize)
            {
                return accessSize switch
                {
                    1 => new[] { (byte)data },
                    2 => BitConverter.GetBytes((ushort)data),
                    4 => BitConverter.GetBytes((uint)data),
                    _ => BitConverter.GetBytes(data)
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static ulong DecodeExplicitPacketLoadBuffer(uint opcode, byte[] buffer, byte accessSize) =>
                Core.LoadStoreMicroOp.DecodeLoadValue(
                    opcode,
                    buffer,
                    accessSize,
                    "Explicit packet load decode");

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void CancelInFlightExplicitMemoryRequests()
            {
                var cancelMemSub = GetBoundMemorySubsystem();
                if (cancelMemSub == null || !pipeMEM.Valid || !pipeMEM.UsesExplicitPacketLanes)
                    return;

                for (byte laneIndex = 4; laneIndex < 6; laneIndex++)
                {
                    ScalarMemoryLaneState lane = pipeMEM.GetLane(laneIndex);
                    if (!lane.IsOccupied || lane.PendingMemoryRequest == null)
                        continue;

                    cancelMemSub.CancelPendingRequest(lane.PendingMemoryRequest);
                    lane.PendingMemoryRequest = null;
                    pipeMEM.SetLane(laneIndex, lane);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ExecuteExplicitPacketMemoryWork()
            {
                byte originalActiveLaneIndex = pipeMEM.ActiveLaneIndex;
                byte completedLaneMask = 0;
                ulong completedContourPc = 0;

                for (byte laneIndex = 4; laneIndex < 8; laneIndex++)
                {
                    ScalarMemoryLaneState lane = pipeMEM.GetLane(laneIndex);
                    if (!lane.IsOccupied || !lane.IsMemoryOp)
                        continue;

                    if (lane.ResultReady && lane.PendingMemoryRequest == null)
                        continue;

                    pipeMEM.ActiveLaneIndex = laneIndex;

                    ulong address = lane.MemoryAddress;
                    byte accessSize = NormalizeScalarMemoryAccessSize(lane.MemoryAccessSize);
                    bool isWrite = !lane.IsLoad;

                    var memSub = GetBoundMemorySubsystem();

                    ulong memoryLength =
                        (laneIndex < 6 && memSub != null)
                            ? (ulong)GetBoundMainMemory().Length
                            : GetBoundMainMemoryLength();

                    if ((ulong)accessSize > memoryLength || address > memoryLength - (ulong)accessSize)
                    {
                        Core.PageFaultException memoryFault = new(address, isWrite);
                        pipeCtrl.MemoryFaultCarrierCount++;
                        DeliverStageAwareMemoryPageFault(memoryFault);
                        throw new InvalidOperationException("Memory-stage page fault delivery must terminate the current cycle.");
                    }

                    if (lane.GeneratedAtomicEffect.HasValue)
                    {
                        lane.MemoryAccessSize = accessSize;
                        lane.ResultValue = 0;
                        lane.ResultReady = true;
                        pipeMEM.SetLane(laneIndex, lane);
                        completedLaneMask |= (byte)(1 << laneIndex);
                        completedContourPc = lane.PC;
                        continue;
                    }

                    if (laneIndex < 6 && memSub != null)
                    {
                        if (lane.PendingMemoryRequest == null)
                        {
                            if (lane.IsLoad)
                            {
                                if (lane.PendingReadBuffer == null || lane.PendingReadBuffer.Length < accessSize)
                                {
                                    lane.PendingReadBuffer = new byte[accessSize];
                                }

                                lane.PendingMemoryRequest = memSub.EnqueueRead(
                                    (ulong)this.CoreID,
                                    address,
                                    accessSize,
                                    lane.PendingReadBuffer);
                            }
                            else
                            {
                                lane.PendingMemoryRequest = memSub.EnqueueWrite(
                                    (ulong)this.CoreID,
                                    address,
                                    accessSize,
                                    CreateExplicitPacketStoreBuffer(lane.MemoryData, accessSize),
                                    deferPhysicalWriteUntilRetire: true);
                            }

                            lane.ResultReady = false;
                            lane.MemoryAccessSize = accessSize;
                            pipeMEM.SetLane(laneIndex, lane);
                            continue;
                        }

                        if (!lane.PendingMemoryRequest.IsComplete)
                        {
                            lane.ResultReady = false;
                            pipeMEM.SetLane(laneIndex, lane);
                            continue;
                        }

                        lane.PendingMemoryRequest.ThrowIfFailed(
                            "CPU_Core.PipelineExecution explicit-packet memory lane");

                        if (lane.IsLoad)
                        {
                            lane.ResultValue = DecodeExplicitPacketLoadBuffer(
                                lane.OpCode,
                                lane.PendingMemoryRequest.GetBuffer(),
                                accessSize);
                        }
                        else
                        {
                            lane.DefersStoreCommitToWriteBack = true;
                        }

                        lane.PendingMemoryRequest = null;
                    }
                    else
                    {
                        if (lane.IsLoad)
                        {
                            byte[] readBuffer = ReadExplicitPacketLoadIntoReusableBuffer(address, accessSize);
                            lane.ResultValue = DecodeExplicitPacketLoadBuffer(
                                lane.OpCode,
                                readBuffer,
                                accessSize);
                        }
                        else
                        {
                            lane.DefersStoreCommitToWriteBack = true;
                        }
                    }

                    if (!lane.IsLoad)
                    {
                        lane.ResultValue = lane.MemoryData;
                    }

                    lane.MemoryAccessSize = accessSize;
                    lane.ResultReady = true;
                    pipeMEM.SetLane(laneIndex, lane);
                    completedLaneMask |= (byte)(1 << laneIndex);
                    completedContourPc = lane.PC;
                }

                pipeMEM.ActiveLaneIndex = originalActiveLaneIndex;
                PublishExecuteCompletionContourCertificate(
                    Core.PipelineContourOwner.ExplicitPacketExecution,
                    Core.PipelineContourVisibilityStage.Memory,
                    completedContourPc,
                    completedLaneMask);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryPrepareExplicitPacketExecuteMemoryCarrierLane(
                byte laneIndex,
                ref ScalarExecuteLaneState lane,
                ref int executedPhysicalLaneCount)
            {
                if (laneIndex < 6 && lane.MicroOp is Core.LoadMicroOp loadOp)
                {
                    lane.IsMemoryOp = true;
                    lane.IsLoad = true;
                    lane.MemoryAccessSize = loadOp.Size;
                    lane.MemoryAddress = loadOp.Address;
                    lane.MemoryData = 0;
                    lane.ResultValue = 0;
                    lane.ResultReady = true;
                    pipeEX.SetLane(laneIndex, lane);
                    executedPhysicalLaneCount++;

                    RecordExecuteLaneTraceEvent(laneIndex, lane);
                    return true;
                }

                if (laneIndex < 6 && lane.MicroOp is Core.StoreMicroOp storeOp)
                {
                    int consumerThreadId = NormalizePipelineStateVtId(lane.OwnerThreadId);
                    lane.IsMemoryOp = true;
                    lane.IsLoad = false;
                    lane.MemoryAccessSize = storeOp.Size;
                    lane.MemoryAddress = storeOp.Address;
                    lane.MemoryData = GetRegisterValueWithForwarding(consumerThreadId, storeOp.SrcRegID);
                    lane.ResultValue = lane.MemoryData;
                    lane.ResultReady = true;
                    pipeEX.SetLane(laneIndex, lane);
                    executedPhysicalLaneCount++;

                    RecordExecuteLaneTraceEvent(laneIndex, lane);
                    return true;
                }

                if (laneIndex < 6 &&
                    lane.MicroOp is Core.AtomicMicroOp atomicOp &&
                    lane.GeneratedAtomicEffect.HasValue)
                {
                    lane.IsMemoryOp = true;
                    lane.IsLoad = false;
                    lane.MemoryAccessSize = atomicOp.Size;
                    lane.MemoryAddress = atomicOp.Address;
                    lane.MemoryData = lane.GeneratedAtomicEffect.Value.SourceValue;
                    lane.ResultValue = 0;
                    lane.ResultReady = true;
                    pipeEX.SetLane(laneIndex, lane);
                    executedPhysicalLaneCount++;

                    RecordExecuteLaneTraceEvent(laneIndex, lane);
                    return true;
                }

                return false;
            }
        }
    }
}
