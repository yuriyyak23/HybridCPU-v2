using System;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using YAKSys_Hybrid_CPU.Core.Decoder;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public sealed partial class CPU_Core
        {
            private readonly Core.DomainHypercallRetireOwner _domainHypercallRetireOwner;
            private ulong _nextDomainHypercallRetireWindowIdentity;
            private ulong _domainHypercallRetireOrderEpoch;

            internal Core.DomainHypercallRetireOwner ExactHypercallRetireOwner =>
                _domainHypercallRetireOwner;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsExactHypercallPendingCanonicalRetire(Core.MicroOp microOp) =>
                microOp is Core.VmxMicroOp vmx &&
                vmx.ExactHypercallCompletionPublication is Core.DomainHypercallCompletionPublicationResult publication &&
                publication.IsPublished;

            private void IssueExactHypercallRetireGrant(
                in ScalarWriteBackLaneState lane,
                byte laneIndex,
                byte retireOrderIndex,
                ulong retireWindowIdentity,
                ulong orderEpoch)
            {
                if (lane.MicroOp is not Core.VmxMicroOp carrier ||
                    carrier.ExactHypercallCompletionPublication is not Core.DomainHypercallCompletionPublicationResult publication ||
                    !publication.IsPublished)
                    return;
                if (publication.Owner is null || publication.RestoreOwner is null ||
                    lane.PostStageBIssuedAttempt is null ||
                    lane.PostStageBIssuedAttempt.ScheduledOperation.PhysicalLane != laneIndex)
                    throw new InvalidOperationException("Canonical E6 issuance requires completion owner, restore owner, and live post-Stage-B identity.");

                VliwOperationId operationId =
                    lane.PostStageBIssuedAttempt.ScheduledOperation.OperationId;
                AdmissionRecord admission =
                    lane.PostStageBIssuedAttempt.ScheduledOperation.Admission;
                var eligibility = new Core.DomainHypercallRetireEligibility(
                    carrier,
                    lane.VirtualThreadId,
                    lane.DomainTag,
                    admission.SourceProvenance.SourceSlotIndex,
                    operationId.WorkingSlotIndex,
                    operationId.WorkingBundleSequence,
                    operationId.OperationAttempt,
                    laneIndex,
                    retireOrderIndex,
                    retireWindowIdentity,
                    orderEpoch,
                    IsCanonicalHead: retireOrderIndex == 0,
                    IsSquashed: false,
                    HasWinningException: lane.HasFault);
                Core.DomainHypercallRetireResult result = _domainHypercallRetireOwner.Issue(
                    publication.Owner,
                    publication,
                    publication.RestoreOwner,
                    eligibility);
                if (!result.IsIssued || result.E6 is null)
                    throw new InvalidOperationException($"Canonical E6 issuance denied: {result.Decision}: {result.Reason}");
                carrier.AttachExactHypercallRetireGrant(result.E6, retireWindowIdentity, orderEpoch);
            }

            private void ConsumeExactHypercallRetireGrant(Core.VmxMicroOp carrier)
            {
                Core.DomainHypercallCompletionPublicationResult publication =
                    carrier.ExactHypercallCompletionPublication ??
                    throw new InvalidOperationException("E6 carrier lost its E5 publication.");
                if (!_domainHypercallRetireOwner.ConsumeAtPreciseRetire(
                        carrier.ExactHypercallRetireGrant,
                        publication.RestoreOwner!,
                        carrier.ExactHypercallRetireWindowIdentity,
                        carrier.ExactHypercallRetireOrderEpoch))
                    throw new InvalidOperationException("Canonical precise retire rejected a stale, duplicate, or foreign E6.");
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Core.VmxRetireEffect? MaterializeLaneVmxEffect(Core.MicroOp microOp)
            {
                if (microOp is Core.VmxMicroOp vmxMicroOp)
                {
                    if (vmxMicroOp.HasVmReadScalarResultReceipt)
                        return null;
                    return vmxMicroOp.CreateRetireEffect();
                }

                return null;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Core.VmxRetireOutcome ApplyRetiredVmxEffect(
                in Core.VmxRetireEffect vmxEffect,
                int virtualThreadId)
            {
                int normalizedVtId = NormalizePipelineStateVtId(virtualThreadId);
                Core.VmxRetireOutcome outcome = ApplyRemovedFrontendFailClosedEffect(vmxEffect);

                RetireVmxOutcomeRecords(outcome, normalizedVtId);
                ApplyRetiredVmxPipelineStateOwnership(normalizedVtId, vmxEffect, outcome);

                if (outcome.RedirectTargetPc.HasValue)
                {
                    if (outcome.FlushesPipeline &&
                        ReadActiveVirtualThreadId() == normalizedVtId)
                    {
                        ApplyPipelineControlFlowRedirect(
                            outcome.RedirectTargetPc.Value,
                            Core.AssistInvalidationReason.VmTransition);
                    }
                }

                if (outcome.FlushesPipeline &&
                    (!outcome.RedirectTargetPc.HasValue ||
                     ReadActiveVirtualThreadId() != normalizedVtId))
                {
                    PublishReplayInvalidationForOffPipeVmxBoundary();
                    InvalidateAssistRuntime(Core.AssistInvalidationReason.VmTransition);
                }

                return outcome;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Core.VmxRetireOutcome ApplyRemovedFrontendFailClosedEffect(
                in Core.VmxRetireEffect vmxEffect)
            {
                if (!vmxEffect.IsValid)
                {
                    return Core.VmxRetireOutcome.NoOp();
                }

                return Core.VmxRetireOutcome.Fault(
                    vmxEffect.FailureReason == Core.VmExitReason.None
                        ? Core.VmExitReason.SecurityPolicyViolation
                        : vmxEffect.FailureReason);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void PublishReplayInvalidationForOffPipeVmxBoundary()
            {
                AdvanceReplayCodeGenerationEpoch();
                if (!_loopBuffer.CurrentReplayPhase.IsActive)
                {
                    return;
                }

                _loopBuffer.Invalidate(Core.ReplayPhaseInvalidationReason.SerializingEvent);
                _fspScheduler?.SetReplayPhaseContext(
                    _loopBuffer.CurrentReplayPhase,
                    invalidateAssistOnDeactivate: false);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RetireVmxOutcomeRecords(
                in Core.VmxRetireOutcome outcome,
                int virtualThreadId)
            {
                Span<RetireRecord> retireRecords = stackalloc RetireRecord[3];
                int retireRecordCount = 0;

                if (outcome.HasRegisterWriteback)
                {
                    if (outcome.RegisterDestination == 0)
                    {
                        throw new InvalidOperationException(
                            "VMX retire outcome requested an architectural writeback to x0.");
                    }

                    if (outcome.RestoredStackPointer.HasValue &&
                        outcome.RegisterDestination == 2)
                    {
                        throw new InvalidOperationException(
                            "VMX retire outcome published both an explicit writeback and a restored stack pointer for x2.");
                    }

                    retireRecords[retireRecordCount++] = RetireRecord.RegisterWrite(
                        virtualThreadId,
                        outcome.RegisterDestination,
                        outcome.RegisterWritebackValue);
                }

                if (outcome.RestoredStackPointer.HasValue)
                {
                    retireRecords[retireRecordCount++] = RetireRecord.RegisterWrite(
                        virtualThreadId,
                        2,
                        outcome.RestoredStackPointer.Value);
                }

                if (outcome.RedirectTargetPc.HasValue)
                {
                    retireRecords[retireRecordCount++] = RetireRecord.PcWrite(
                        virtualThreadId,
                        outcome.RedirectTargetPc.Value);
                }

                if (retireRecordCount != 0)
                {
                    RetireCoordinator.Retire(retireRecords[..retireRecordCount]);
                }
            }

            internal Core.VmxRetireOutcome ApplyRetiredVmxEffectForTesting(
                in Core.VmxRetireEffect vmxEffect,
                int virtualThreadId)
            {
                return ApplyRetiredVmxEffect(vmxEffect, virtualThreadId);
            }
        }
    }
}
