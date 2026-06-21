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
            private ScalarExceptionOrderingDecision ResolveDecodeExceptionOrderingDecision(
                in Core.DecodedBundleSlotDescriptor slotDescriptor,
                ulong faultingPc)
            {
                ulong domainTag = slotDescriptor.GetRuntimeAdmissionDomainTag();
                int virtualThreadId = slotDescriptor.GetRuntimeExecutionVirtualThreadId();

                if (domainTag == 0 || CsrMemDomainCert == 0)
                    return ScalarExceptionOrderingDecision.None();

                if ((domainTag & CsrMemDomainCert) != 0)
                    return ScalarExceptionOrderingDecision.None();

                if (slotDescriptor.GetRuntimeExecutionIsFspInjected())
                {
                    return ScalarExceptionOrderingDecision.SilentSpeculativeDomainSquash(
                        virtualThreadId,
                        faultingPc,
                        domainTag,
                        CsrMemDomainCert);
                }

                return ScalarExceptionOrderingDecision.PreciseDomainFault(
                    virtualThreadId,
                    faultingPc,
                    domainTag,
                    CsrMemDomainCert);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.NopMicroOp CreateSilentSpeculativeSquashReplacement(uint opCode)
            {
                return new Core.NopMicroOp
                {
                    IsStealable = false,
                    OpCode = opCode
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.DomainFaultException CreatePreciseDecodeException(ScalarExceptionOrderingDecision decision)
            {
                if (!decision.IsPreciseArchitecturalFault)
                    throw new InvalidOperationException("Precise decode exception requested for a non-precise exception decision.");

                return new Core.DomainFaultException(
                    vtId: decision.VirtualThreadId,
                    pc: decision.FaultingPC,
                    opTag: decision.OperationDomainTag,
                    cert: decision.ActiveCert);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void DeliverStageAwareExecutePageFault(Core.PageFaultException pageFaultException)
            {
                MarkActiveExecuteLanePageFault(pageFaultException);

                if (!TryResolveStageAwareExceptionWinnerMetadata(
                    pipeWB,
                    pipeMEM,
                    pipeEX,
                    out StageAwareExceptionWinnerMetadata winnerMetadata))
                {
                    throw new InvalidOperationException("Stage-aware execute page fault delivery requires at least one materialized fault carrier.");
                }

                if (CanDeliverOlderStageFault(winnerMetadata.WinnerStage))
                {
                    if (ShouldSuppressYoungerWorkForExceptionWinner(
                        winnerMetadata.WinnerStage, pipeWB, pipeMEM, pipeEX))
                    {
                        pipeCtrl.ExceptionYoungerSuppressCount++;
                    }

                    Core.PageFaultException olderStageFault = new(
                        winnerMetadata.FaultAddress,
                        winnerMetadata.FaultIsWrite);

                    FlushPipeline(Core.AssistInvalidationReason.Trap);
                    throw olderStageFault;
                }

                if (winnerMetadata.WinnerLaneIndex >= 8)
                {
                    throw new InvalidOperationException("Stage-aware exception ordering selected an invalid live execute lane index.");
                }

                pipeEX.ActiveLaneIndex = winnerMetadata.WinnerLaneIndex;

                FlushPipeline(Core.AssistInvalidationReason.Trap);
                throw pageFaultException;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RethrowExplicitPacketExecutePageFault(Core.PageFaultException pageFaultException)
            {
                DeliverStageAwareExecutePageFault(pageFaultException);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RethrowExplicitPacketExecuteAlignmentFault(
                Core.Memory.MemoryAlignmentException memoryAlignmentException,
                Core.MicroOp? microOp)
            {
                Core.PageFaultException alignmentFault = new(
                    memoryAlignmentException.Message,
                    memoryAlignmentException,
                    memoryAlignmentException.Address,
                    isWrite: !IsAtomicReadOnlyAlignmentFaultCarrier(microOp));
                DeliverStageAwareExecutePageFault(alignmentFault);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void MarkActiveExecuteLanePageFault(Core.PageFaultException pageFaultException)
            {
                if (pipeEX.ActiveLaneIndex >= 8)
                    throw new InvalidOperationException("Execute-stage page fault requires a valid active materialized lane index.");

                ScalarExecuteLaneState lane = pipeEX.GetLane(pipeEX.ActiveLaneIndex);
                if (!lane.IsOccupied)
                    throw new InvalidOperationException("Execute-stage page fault requires an occupied materialized lane.");

                lane.HasFault = true;
                lane.FaultAddress = pageFaultException.FaultAddress;
                lane.FaultIsWrite = pageFaultException.IsWrite;
                pipeEX.SetLane(pipeEX.ActiveLaneIndex, lane);
            }

            /// <summary>
            /// Stage 6 Phase C: mark the active MEM-stage materialized lane as faulted.
            /// The fault carrier propagates to WB where <see cref="TryResolveExceptionDeliveryDecisionForRetireWindow"/>
            /// picks it up through the stage-aware exception ordering policy.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void MarkActiveMemoryLanePageFault(Core.PageFaultException pageFaultException)
            {
                if (pipeMEM.ActiveLaneIndex >= 8)
                    throw new InvalidOperationException("Memory-stage page fault requires a valid active materialized lane index.");

                ScalarMemoryLaneState lane = pipeMEM.GetLane(pipeMEM.ActiveLaneIndex);
                if (!lane.IsOccupied)
                    throw new InvalidOperationException("Memory-stage page fault requires an occupied materialized lane.");

                lane.HasFault = true;
                lane.FaultAddress = pageFaultException.FaultAddress;
                lane.FaultIsWrite = pageFaultException.IsWrite;
                pipeMEM.SetLane(pipeMEM.ActiveLaneIndex, lane);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void DeliverStageAwareMemoryPageFault(Core.PageFaultException pageFaultException)
            {
                MarkActiveMemoryLanePageFault(pageFaultException);

                if (!TryResolveStageAwareExceptionWinnerMetadata(
                    pipeWB,
                    pipeMEM,
                    pipeEX,
                    out StageAwareExceptionWinnerMetadata winnerMetadata))
                {
                    throw new InvalidOperationException("Stage-aware memory page fault delivery requires at least one materialized fault carrier.");
                }

                if (winnerMetadata.WinnerStage == PipelineStage.WriteBack)
                {
                    if (ShouldSuppressYoungerWorkForExceptionWinner(
                        winnerMetadata.WinnerStage, pipeWB, pipeMEM, pipeEX))
                    {
                        pipeCtrl.ExceptionYoungerSuppressCount++;
                    }

                    Core.PageFaultException olderStageFault = new(
                        winnerMetadata.FaultAddress,
                        winnerMetadata.FaultIsWrite);

                    FlushPipeline(Core.AssistInvalidationReason.Trap);
                    throw olderStageFault;
                }

                if (winnerMetadata.WinnerStage != PipelineStage.Memory)
                {
                    throw new InvalidOperationException("Stage-aware memory page fault delivery selected an invalid winner stage.");
                }

                pipeMEM.ActiveLaneIndex = winnerMetadata.WinnerLaneIndex;

                FlushPipeline(Core.AssistInvalidationReason.Trap);
                throw pageFaultException;
            }
        }
    }
}
