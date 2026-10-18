using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace YAKSys_Hybrid_CPU;

public partial struct Processor
{
    public sealed partial class CPU_Core
    {
        internal CpuInstructionTranslationResult TranslateScalarInstructionMemoryAddress(
            Core.LoadStoreMicroOp operation,
            CpuInstructionTranslationAccessKind accessKind,
            byte accessSize)
        {
            ArgumentNullException.ThrowIfNull(operation);
            if (accessKind is not (CpuInstructionTranslationAccessKind.ScalarLoad or
                CpuInstructionTranslationAccessKind.ScalarStore))
            {
                throw new ArgumentOutOfRangeException(nameof(accessKind));
            }

            CpuInstructionTranslationOperationIdentity identity;
            if (operation.PostStageBIssuedAttempt is Core.PostStageBIssuedAttempt issuedAttempt)
            {
                Core.Decoder.VliwOperationId operationId =
                    issuedAttempt.ScheduledOperation.OperationId;
                ulong eventId = Core.ArchitecturalCompletionEventIdentity.Create(
                    operationId.WorkingBundleSequence,
                    operationId.WorkingSlotIndex);
                identity = new CpuInstructionTranslationOperationIdentity(
                    operationId.OperationAttempt,
                    eventId,
                    CpuInstructionTranslationOwner.CreateMemoryOperationIdentity(
                        operationId.OperationAttempt,
                        eventId,
                        accessKind,
                        operation.MemoryAddress));
            }
            else
            {
                identity = CpuInstructionTranslationOwner.IssueFallbackMemoryIdentity(
                    accessKind,
                    operation.MemoryAddress);
            }

            var scope = new CpuInstructionTranslationScope(
                operation.Placement.DomainTag,
                operation.OwnerContextId,
                operation.VirtualThreadId);
            var request = new CpuInstructionTranslationRequest(
                scope,
                accessKind,
                operation.MemoryAddress,
                accessSize == 0 ? (ushort)8 : accessSize,
                identity);
            return CpuInstructionTranslationOwner.Translate(request);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CpuInstructionTranslationResult TranslateInstructionFetch(ulong virtualAddress)
        {
            CpuInstructionTranslationScope scope = CpuInstructionTranslationOwner.IsIdentity
                ? new CpuInstructionTranslationScope(0, 0, ReadActiveVirtualThreadId())
                : CpuInstructionTranslationOwner.ConfiguredScope;
            var request = new CpuInstructionTranslationRequest(
                scope,
                CpuInstructionTranslationAccessKind.InstructionFetch,
                virtualAddress,
                256,
                CpuInstructionTranslationOwner.IssueFetchIdentity(virtualAddress));
            return CpuInstructionTranslationOwner.Translate(request);
        }

        private void CommitCpuInstructionTranslationFault(
            in CpuInstructionTranslationFault fault)
        {
            CpuInstructionTranslationRequest request = fault.Request;
            bool secondStage =
                fault.FaultStage == CpuInstructionTranslationFaultStage.SecondStage;
            var candidate = new Core.ArchitecturalCompletionCandidate(
                request.Scope.DomainId,
                request.Scope.ContextId,
                request.Scope.VirtualThreadId,
                request.OperationIdentity.AttemptId,
                request.OperationIdentity.EventId,
                new Core.NeutralArchitecturalCompletionFacts(
                    Core.NeutralArchitecturalCompletionClass.TranslationFault,
                    Core.NeutralScalarFact.Present((ulong)(byte)fault.Reason),
                    Core.NeutralScalarFact.Present(fault.Qualification),
                    Core.NeutralAddressFact.Present(
                        secondStage ? fault.GuestPhysicalAddress : request.VirtualAddress,
                        secondStage
                            ? Core.NeutralFaultAddressSemantic.GuestPhysicalAddress
                            : Core.NeutralFaultAddressSemantic.VirtualAddress),
                    Core.NeutralAuxiliaryFact.Present(
                        fault.Auxiliary,
                        secondStage
                            ? Core.NeutralFaultAuxiliarySemantic.SecondStageTranslationViolation
                            : Core.NeutralFaultAuxiliarySemantic.TranslationFault)),
                Core.NeutralTranslationProvenance.Present(
                    request.OperationIdentity.MemoryOperationId,
                    fault.OwnerEpoch,
                    fault.MemoryDomainOwnerEpoch,
                    fault.AddressSpaceGeneration,
                    fault.AddressSpaceIdentity));

            Core.ArchitecturalCompletionCommitResult result =
                ArchitecturalCompletionCommitOwner.CommitAtCanonicalPreciseFaultBoundary(
                    secondStage
                        ? CanonicalCpuSecondStageTranslationFaultCompletionProducer
                        : CanonicalCpuTranslationFaultCompletionProducer,
                    candidate);
            if (result.Decision != Core.ArchitecturalCompletionCommitDecision.Committed)
            {
                throw new InvalidOperationException(
                    $"Mandatory CPU translation-fault completion commit was denied: {result.Decision}: {result.Reason}");
            }
        }

        private void DeliverCommittedCpuInstructionTranslationFault(
            in CpuInstructionTranslationFault fault)
        {
            if (fault.Reason == CpuInstructionTranslationFaultReason.SecondStageSourceStale)
            {
                PendingCpuFetchTranslationFault = null;
                FlushPipeline(Core.AssistInvalidationReason.Replay);
                throw new CpuInstructionTranslationSourceStaleException(fault);
            }
            CommitCpuInstructionTranslationFault(fault);
            PendingCpuFetchTranslationFault = null;
            FlushPipeline(Core.AssistInvalidationReason.Trap);
            throw new CpuInstructionTranslationFaultException(fault);
        }

        private void MarkActiveExecuteLaneCpuTranslationFault(
            in CpuInstructionTranslationFault fault)
        {
            if (pipeEX.ActiveLaneIndex >= 8)
                throw new InvalidOperationException("Execute-stage CPU translation fault requires a live lane.");

            ScalarExecuteLaneState lane = pipeEX.GetLane(pipeEX.ActiveLaneIndex);
            if (!lane.IsOccupied)
                throw new InvalidOperationException("Execute-stage CPU translation fault cannot attach to an empty lane.");
            lane.HasFault = true;
            lane.FaultAddress = fault.Request.VirtualAddress;
            lane.FaultIsWrite = fault.Request.AccessKind == CpuInstructionTranslationAccessKind.ScalarStore;
            lane.CpuTranslationFault = fault;
            pipeEX.SetLane(pipeEX.ActiveLaneIndex, lane);
        }

        private void MarkActiveMemoryLaneCpuTranslationFault(
            in CpuInstructionTranslationFault fault)
        {
            if (pipeMEM.ActiveLaneIndex >= 8)
                throw new InvalidOperationException("Memory-stage CPU translation fault requires a live lane.");

            ScalarMemoryLaneState lane = pipeMEM.GetLane(pipeMEM.ActiveLaneIndex);
            if (!lane.IsOccupied)
                throw new InvalidOperationException("Memory-stage CPU translation fault cannot attach to an empty lane.");
            lane.HasFault = true;
            lane.FaultAddress = fault.Request.VirtualAddress;
            lane.FaultIsWrite = fault.Request.AccessKind == CpuInstructionTranslationAccessKind.ScalarStore;
            lane.CpuTranslationFault = fault;
            pipeMEM.SetLane(pipeMEM.ActiveLaneIndex, lane);
        }

        private void DeliverStageAwareExecuteCpuTranslationFault(
            in CpuInstructionTranslationFault fault)
        {
            MarkActiveExecuteLaneCpuTranslationFault(fault);
            DeliverStageAwareCpuTranslationWinner();
        }

        private void DeliverStageAwareMemoryCpuTranslationFault(
            in CpuInstructionTranslationFault fault)
        {
            MarkActiveMemoryLaneCpuTranslationFault(fault);
            DeliverStageAwareCpuTranslationWinner();
        }

        private void DeliverStageAwareCpuTranslationWinner()
        {
            if (!TryResolveStageAwareExceptionWinnerMetadata(
                    pipeWB,
                    pipeMEM,
                    pipeEX,
                    out StageAwareExceptionWinnerMetadata winner))
            {
                throw new InvalidOperationException("CPU translation fault delivery requires a stage-aware winner.");
            }

            if (winner.CpuTranslationFault is CpuInstructionTranslationFault translationFault)
            {
                DeliverCommittedCpuInstructionTranslationFault(translationFault);
                return;
            }

            FlushPipeline(Core.AssistInvalidationReason.Trap);
            throw new Core.PageFaultException(winner.FaultAddress, winner.FaultIsWrite);
        }

        private bool TryDeliverPendingCpuFetchTranslationFault()
        {
            if (PendingCpuFetchTranslationFault is not CpuInstructionTranslationFault fault)
                return false;

            if (pipeID.Valid || pipeEX.Valid || pipeMEM.Valid || pipeWB.Valid)
                return true;

            DeliverCommittedCpuInstructionTranslationFault(fault);
            return true;
        }

        private void ClearPendingCpuFetchTranslationFault() =>
            PendingCpuFetchTranslationFault = null;
    }
}
