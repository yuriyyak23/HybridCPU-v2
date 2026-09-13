using System;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Pipeline;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public sealed partial class CPU_Core
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
                Core.Execution.ExecutionOutcome outcome =
                    ProjectExplicitPacketPageFaultOutcome(pageFaultException);
                DeliverExplicitPacketPageFaultOutcome(outcome, pageFaultException);
            }

            // RF-07.2c owns only the direct PageFaultException route in the
            // explicit-packet execution contour. The existing lane fault carrier
            // and stage-aware delivery remain authoritative; no parallel attempt
            // identity or mutable attempt carrier is introduced here.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.Execution.ExecutionOutcome ProjectExplicitPacketPageFaultOutcome(
                Core.PageFaultException exception)
            {
                Core.Execution.ExecutionOutcome outcome =
                    Core.Execution.Rf07LegacyOutcomeProjection.ProjectException(exception);
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.ArchitecturalFault ||
                    outcome.Diagnostic is not
                    {
                        Code: Core.Execution.ExecutionDiagnosticCode.PageFault,
                        FaultAddress: ulong faultAddress,
                        FaultIsWrite: bool faultIsWrite,
                    } ||
                    faultAddress != exception.FaultAddress ||
                    faultIsWrite != exception.IsWrite)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "The explicit-packet page-fault adapter requires an exact ArchitecturalFault diagnostic.");
                }

                return outcome;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void DeliverExplicitPacketPageFaultOutcome(
                Core.Execution.ExecutionOutcome outcome,
                Core.PageFaultException exception)
            {
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.ArchitecturalFault ||
                    outcome.Diagnostic?.Code != Core.Execution.ExecutionDiagnosticCode.PageFault)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "Only a typed PageFault ArchitecturalFault may enter explicit-packet stage-aware delivery.");
                }

                DeliverStageAwareExecutePageFault(exception);
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
                Core.Execution.ExecutionOutcome outcome =
                    ProjectExplicitPacketAlignmentFaultOutcome(
                        memoryAlignmentException,
                        alignmentFault);
                DeliverExplicitPacketAlignmentFaultOutcome(outcome, alignmentFault);
            }

            // RF-07.2d owns only this explicit-packet alignment catch. The
            // pre-existing PageFaultException translation remains the sole
            // delivery carrier; no parallel identity or mutable attempt carrier
            // is introduced.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.Execution.ExecutionOutcome ProjectExplicitPacketAlignmentFaultOutcome(
                Core.Memory.MemoryAlignmentException exception,
                Core.PageFaultException translatedFault)
            {
                Core.Execution.ExecutionOutcome outcome =
                    Core.Execution.Rf07LegacyOutcomeProjection.ProjectException(
                        exception,
                        translatedFault.IsWrite);
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.ArchitecturalFault ||
                    outcome.Diagnostic is not
                    {
                        Code: Core.Execution.ExecutionDiagnosticCode.AlignmentFault,
                        FaultAddress: ulong faultAddress,
                        FaultIsWrite: bool faultIsWrite,
                    } ||
                    faultAddress != translatedFault.FaultAddress ||
                    faultIsWrite != translatedFault.IsWrite)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "The explicit-packet alignment-fault adapter requires an exact ArchitecturalFault diagnostic.");
                }

                return outcome;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void DeliverExplicitPacketAlignmentFaultOutcome(
                Core.Execution.ExecutionOutcome outcome,
                Core.PageFaultException translatedFault)
            {
                if (outcome.Kind != Core.Execution.ExecutionOutcomeKind.ArchitecturalFault ||
                    outcome.Diagnostic?.Code != Core.Execution.ExecutionDiagnosticCode.AlignmentFault)
                {
                    throw new Core.Execution.ExecutionOutcomeContractViolationException(
                        "Only a typed AlignmentFault ArchitecturalFault may enter explicit-packet stage-aware delivery.");
                }

                DeliverStageAwareExecutePageFault(translatedFault);
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
