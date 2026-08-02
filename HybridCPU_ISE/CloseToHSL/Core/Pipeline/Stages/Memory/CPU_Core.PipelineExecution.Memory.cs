using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using HybridCPU_ISE.CloseToHSL.Memory.Timing;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Pipeline;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public sealed partial class CPU_Core
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
                GetBoundMemorySubsystem()?.CycleController.RecordCommittedDataWriteBytes(accessSize);
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

            internal DmaStreamComputeCommitResult ApplyDmaStreamComputeRetireCommit(
                DmaStreamComputeToken token,
                DmaStreamComputeOwnerGuardDecision commitGuardDecision)
            {
                return ApplyRetiredDmaStreamComputeTokenCommit(token, commitGuardDecision);
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
                    if (!lane.IsOccupied)
                        continue;

                    bool canceled = false;
                    if (lane.PendingMemoryRequest != null)
                    {
                        cancelMemSub.CancelPendingRequest(lane.PendingMemoryRequest);
                        lane.PendingMemoryRequest = null;
                        canceled = true;
                    }

                    if (lane.PendingMemoryControllerRequestId.HasValue)
                    {
                        cancelMemSub.CycleController.TryCancel(
                            lane.PendingMemoryControllerRequestId.Value);
                        lane.PendingMemoryControllerRequestId = null;
                        canceled = true;
                    }

                    if (canceled)
                    {
                        pipeMEM.SetLane(laneIndex, lane);
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void CancelInFlightSingleLaneControllerRequests()
            {
                if (!pipeEX.Valid)
                    return;

                for (byte laneIndex = 0; laneIndex < 8; laneIndex++)
                {
                    ScalarExecuteLaneState lane = pipeEX.GetLane(laneIndex);
                    if (lane.IsOccupied && lane.MicroOp is Core.LoadMicroOp loadMicroOp)
                    {
                        loadMicroOp.CancelPendingControllerRequest();
                    }
                    else if (lane.IsOccupied && lane.MicroOp is Core.StoreMicroOp storeMicroOp)
                    {
                        storeMicroOp.CancelPendingControllerRequest();
                    }
                    else if (lane.IsOccupied && lane.MicroOp is Core.LoadSegmentMicroOp loadSegmentMicroOp)
                    {
                        loadSegmentMicroOp.CancelPendingControllerRequest();
                    }
                    else if (lane.IsOccupied && lane.MicroOp is Core.VectorTransferMicroOp vectorTransferMicroOp)
                    {
                        vectorTransferMicroOp.CancelPendingControllerRequest();
                    }
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

                    if (lane.ResultReady &&
                        lane.PendingMemoryRequest == null &&
                        !lane.PendingMemoryControllerRequestId.HasValue)
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
                        if (lane.IsLoad)
                        {
                            if (!lane.PendingMemoryControllerRequestId.HasValue)
                            {
                                MemoryAdmissionResult admission =
                                    memSub.CycleController.TryAcceptExplicitPacketScalarLoad(
                                        (ulong)this.CoreID,
                                        address,
                                        accessSize);
                                if (admission.Status == MemoryAdmissionStatus.Rejected)
                                {
                                    Core.PageFaultException rejectedRequestFault = new(
                                        admission.Reason ??
                                        "MemoryCycleController rejected an explicit-packet scalar load.",
                                        address,
                                        isWrite: false);
                                    Core.Execution.ExecutionOutcome rejectedOutcome =
                                        ProjectExplicitPacketCompletedMemoryRequestFailureOutcome(
                                            rejectedRequestFault);
                                    DeliverExplicitPacketCompletedMemoryRequestFailureOutcome(
                                        rejectedOutcome,
                                        rejectedRequestFault);
                                    throw new InvalidOperationException(
                                        "Stage-aware explicit-packet rejected-request fault delivery must terminate the current cycle.");
                                }

                                if (admission.Status == MemoryAdmissionStatus.Backpressured)
                                {
                                    lane.ResultReady = false;
                                    lane.MemoryAccessSize = accessSize;
                                    pipeMEM.SetLane(laneIndex, lane);
                                    continue;
                                }

                                lane.PendingMemoryControllerRequestId = admission.RequestId;
                                lane.ResultReady = false;
                                lane.MemoryAccessSize = accessSize;
                                pipeMEM.SetLane(laneIndex, lane);
                                continue;
                            }

                            if (!memSub.CycleController.TryTakeCompletion(
                                    lane.PendingMemoryControllerRequestId.Value,
                                    out MemoryCompletion? completion))
                            {
                                lane.ResultReady = false;
                                pipeMEM.SetLane(laneIndex, lane);
                                continue;
                            }

                            if (completion == null || !completion.Succeeded)
                            {
                                Core.PageFaultException completedControllerFault = new(
                                    "Explicit-packet MEM observed failed completed controller request. " +
                                    (completion?.FailureReason ??
                                     "MemoryCycleController did not provide a failure reason."),
                                    address,
                                    isWrite: false);
                                Core.Execution.ExecutionOutcome outcome =
                                    ProjectExplicitPacketCompletedMemoryRequestFailureOutcome(
                                        completedControllerFault);
                                DeliverExplicitPacketCompletedMemoryRequestFailureOutcome(
                                    outcome,
                                    completedControllerFault);
                                throw new InvalidOperationException(
                                    "Stage-aware explicit-packet controller-completion fault delivery must terminate the current cycle.");
                            }

                            lane.ResultValue = DecodeExplicitPacketLoadBuffer(
                                lane.OpCode,
                                completion.Data.ToArray(),
                                accessSize);
                            lane.PendingMemoryControllerRequestId = null;
                        }
                        else
                        {
                            if (!lane.PendingMemoryControllerRequestId.HasValue)
                            {
                                byte[] storeBytes =
                                    CreateExplicitPacketStoreBuffer(lane.MemoryData, accessSize);
                                MemoryAdmissionResult admission =
                                    memSub.CycleController.TryAcceptExplicitPacketScalarStore(
                                        (ulong)this.CoreID,
                                        address,
                                        accessSize,
                                        storeBytes);
                                if (admission.Status == MemoryAdmissionStatus.Rejected)
                                {
                                    Core.PageFaultException rejectedStoreFault = new(
                                        admission.Reason ??
                                        "MemoryCycleController rejected an explicit-packet scalar store.",
                                        address,
                                        isWrite: true);
                                    Core.Execution.ExecutionOutcome rejectedStoreOutcome =
                                        ProjectExplicitPacketCompletedMemoryRequestFailureOutcome(
                                            rejectedStoreFault);
                                    DeliverExplicitPacketCompletedMemoryRequestFailureOutcome(
                                        rejectedStoreOutcome,
                                        rejectedStoreFault);
                                    throw new InvalidOperationException(
                                        "Stage-aware explicit-packet rejected-store fault delivery must terminate the current cycle.");
                                }

                                if (admission.Status == MemoryAdmissionStatus.Backpressured)
                                {
                                    lane.ResultReady = false;
                                    lane.MemoryAccessSize = accessSize;
                                    pipeMEM.SetLane(laneIndex, lane);
                                    continue;
                                }

                                lane.PendingMemoryControllerRequestId = admission.RequestId;
                                lane.ResultReady = false;
                                lane.MemoryAccessSize = accessSize;
                                pipeMEM.SetLane(laneIndex, lane);
                                continue;
                            }

                            if (!memSub.CycleController.TryTakeCompletion(
                                    lane.PendingMemoryControllerRequestId.Value,
                                    out MemoryCompletion? storeCompletion))
                            {
                                lane.ResultReady = false;
                                pipeMEM.SetLane(laneIndex, lane);
                                continue;
                            }

                            if (storeCompletion == null || !storeCompletion.Succeeded)
                            {
                                Core.PageFaultException completedStoreFault = new(
                                    "Explicit-packet MEM observed failed completed controller store request. " +
                                    (storeCompletion?.FailureReason ??
                                     "MemoryCycleController did not provide a failure reason."),
                                    address,
                                    isWrite);
                                Core.Execution.ExecutionOutcome storeOutcome =
                                    ProjectExplicitPacketCompletedMemoryRequestFailureOutcome(
                                        completedStoreFault);
                                DeliverExplicitPacketCompletedMemoryRequestFailureOutcome(
                                    storeOutcome,
                                    completedStoreFault);
                                throw new InvalidOperationException(
                                    "Stage-aware explicit-packet controller-store fault delivery must terminate the current cycle.");
                            }

                            lane.DefersStoreCommitToWriteBack = true;
                            lane.PendingMemoryControllerRequestId = null;
                        }
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

            // RF-07.2t intentionally owns only a completed and explicitly
            // unsuccessful MEM token. It is not a second delivery mechanism:
            // the existing memory-stage helper below remains sole authority for
            // lane marking, winner precedence, flush and fault publication.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static Core.Execution.ExecutionOutcome
                ProjectExplicitPacketCompletedMemoryRequestFailureOutcome(
                    Core.PageFaultException exception)
            {
                Core.Execution.ExecutionOutcome outcome =
                    Core.Execution.Rf07LegacyOutcomeProjection.ProjectException(exception);
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.ArchitecturalFault ||
                    outcome.Diagnostic?.Code != Core.Execution.ExecutionDiagnosticCode.PageFault ||
                    outcome.Diagnostic.FaultAddress != exception.FaultAddress ||
                    outcome.Diagnostic.FaultIsWrite != exception.IsWrite ||
                    outcome.Result is not null ||
                    outcome.HasArchitecturalEffects)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "Explicit-packet completed-token failure requires an exact no-effect ArchitecturalFault/PageFault outcome.");
                }

                return outcome;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void DeliverExplicitPacketCompletedMemoryRequestFailureOutcome(
                Core.Execution.ExecutionOutcome outcome,
                Core.PageFaultException exception)
            {
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.ArchitecturalFault ||
                    outcome.Diagnostic?.Code != Core.Execution.ExecutionDiagnosticCode.PageFault ||
                    outcome.Diagnostic.FaultAddress != exception.FaultAddress ||
                    outcome.Diagnostic.FaultIsWrite != exception.IsWrite ||
                    outcome.Result is not null ||
                    outcome.HasArchitecturalEffects)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "Explicit-packet completed-token failure delivery requires its exact projected PageFault outcome.");
                }

                DeliverStageAwareMemoryPageFault(exception);
            }

            // RF-07.2aj owns only the explicit-packet MEM-stage non-fault
            // exception tail. PageFaultException remains on the pre-existing
            // stage-aware memory-fault path above; this helper cannot classify
            // an unknown exception as Retryable or a not-ready lane.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static Core.Execution.ExecutionOutcome
                ProjectExplicitPacketMemoryNonFaultExceptionOutcome(Exception exception)
            {
                Core.Execution.ExecutionOutcome outcome =
                    Core.Execution.Rf07LegacyOutcomeProjection.ProjectException(exception);
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.FatalInvariantViolation ||
                    outcome.Diagnostic is null ||
                    outcome.Result is not null ||
                    outcome.HasArchitecturalEffects)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "Explicit-packet memory non-fault projection requires a no-effect FatalInvariantViolation outcome.");
                }

                return outcome;
            }
        }
    }
}
