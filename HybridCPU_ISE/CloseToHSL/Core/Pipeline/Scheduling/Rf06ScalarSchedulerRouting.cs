using System;
using System.Collections.Generic;
using System.Linq;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// RF-06.3 routing seam for the first scalar family.
///
/// The adapter resolves and checks the immutable contract at the decode/materialization
/// boundary, then submits the already materialized carrier to the existing SMT scheduler.
/// It does not implement admission or lane choice itself: <see cref="MicroOpScheduler"/>
/// remains the only scheduler and its PackBundleIntraCoreSmt path remains authoritative.
/// </summary>
internal static class Rf06ScalarSchedulerRouting
{
    private const int SmtWayCount = 4;

    internal static Rf06ScalarRoutingResult Route(
        MicroOpScheduler scheduler,
        CanonicalDecodedInstruction canonical,
        SourceOperationProvenance provenance,
        int ownerContextId,
        IReadOnlyList<MicroOp?> workingBundle,
        int ownerVirtualThreadId,
        int localCoreId,
        byte eligibleVirtualThreadMask,
        ulong workingBundleSequence,
        OperationAttemptIssuer attemptIssuer)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(canonical);
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(workingBundle);
        ArgumentNullException.ThrowIfNull(attemptIssuer);

        if (canonical.SlotIndex != provenance.SourceSlotId)
            throw new ArgumentException("Source provenance slot must match canonical slot index.", nameof(provenance));

        if ((uint)provenance.SourceVirtualThreadId >= SmtWayCount)
            throw new ArgumentOutOfRangeException(nameof(provenance));

        ExecutionContract contract;
        CheckedScalarLegacyProjection projection;
        try
        {
            contract = Rf06ScalarLegacyProjection.CreateContract(canonical);
            projection = Rf06ScalarLegacyProjection.Project(canonical, contract);
        }
        catch (InvalidOperationException)
        {
            return Rf06ScalarRoutingResult.Rejected(
                Rf06ScalarRoutingRejectReason.NotScalarFamily,
                admission: null,
                carrier: null);
        }

        AdmissionRecord admission = AdmissionRecord.Create(
            provenance,
            contract,
            provenance.SourceVirtualThreadId,
            ownerContextId,
            contract.Placement.DomainTag);

        return Route(
            scheduler,
            admission,
            projection.Carrier,
            workingBundle,
            ownerVirtualThreadId,
            localCoreId,
            eligibleVirtualThreadMask,
            workingBundleSequence,
            provenance.SourceSlotId,
            attemptIssuer);
    }

    /// <summary>
    /// Routes a substitute carrier under an already frozen scalar contract.
    /// The scheduler is deliberately unaware of the carrier's concrete type and
    /// applies the same Stage A/Stage B path to any carrier with equal frozen facts.
    /// </summary>
    internal static Rf06ScalarRoutingResult Route(
        MicroOpScheduler scheduler,
        AdmissionRecord admission,
        MicroOp carrier,
        IReadOnlyList<MicroOp?> workingBundle,
        int ownerVirtualThreadId,
        int localCoreId,
        byte eligibleVirtualThreadMask,
        ulong workingBundleSequence,
        int workingSlotIndex,
        OperationAttemptIssuer attemptIssuer)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(carrier);
        ArgumentNullException.ThrowIfNull(workingBundle);
        ArgumentNullException.ThrowIfNull(attemptIssuer);

        if (workingBundle.Count != BundleMetadata.BundleSlotCount)
            throw new ArgumentException("The existing scheduler path requires an eight-lane working bundle.", nameof(workingBundle));

        if ((uint)ownerVirtualThreadId >= SmtWayCount)
            throw new ArgumentOutOfRangeException(nameof(ownerVirtualThreadId));

        if ((uint)workingSlotIndex >= BundleMetadata.BundleSlotCount)
            throw new ArgumentOutOfRangeException(nameof(workingSlotIndex));

        if (!scheduler.TypedSlotEnabled)
        {
            return Rf06ScalarRoutingResult.Rejected(
                Rf06ScalarRoutingRejectReason.TypedSchedulerPathDisabled,
                admission,
                carrier);
        }

        if ((uint)admission.VirtualThreadId >= SmtWayCount ||
            admission.VirtualThreadId == ownerVirtualThreadId ||
            (eligibleVirtualThreadMask & (1 << admission.VirtualThreadId)) == 0)
        {
            return Rf06ScalarRoutingResult.Rejected(
                Rf06ScalarRoutingRejectReason.StageAEligibility,
                admission,
                carrier);
        }

        PrepareCarrier(admission, carrier);
        if (!HasEquivalentCarrierFacts(admission.ExecutionContract, carrier))
        {
            return Rf06ScalarRoutingResult.Rejected(
                Rf06ScalarRoutingRejectReason.CarrierContractMismatch,
                admission,
                carrier);
        }

        long stageARejectsBefore = GetStageARejectCount(scheduler);
        long stageBRejectsBefore = scheduler.PinnedLaneConflicts + scheduler.InvalidPinnedLaneRejects + scheduler.LateBindingConflicts;

        // This is the existing scheduler path. Nomination is only a transport step;
        // PackBundleIntraCoreSmt owns owner/domain, legality, capacity, pressure and lane choice.
        scheduler.NominateSmtCandidate(admission.VirtualThreadId, carrier);
        MicroOp[] packed = scheduler.PackBundleIntraCoreSmt(
            workingBundle,
            ownerVirtualThreadId,
            localCoreId,
            eligibleVirtualThreadMask);

        int selectedLane = Array.IndexOf(packed, carrier);
        if (selectedLane < 0)
        {
            Rf06ScalarRoutingRejectReason reason =
                scheduler.PinnedLaneConflicts + scheduler.InvalidPinnedLaneRejects + scheduler.LateBindingConflicts > stageBRejectsBefore
                    ? Rf06ScalarRoutingRejectReason.StageBNoLane
                    : GetStageARejectCount(scheduler) > stageARejectsBefore
                        ? Rf06ScalarRoutingRejectReason.StageAReject
                        : Rf06ScalarRoutingRejectReason.StageANotSelected;

            return Rf06ScalarRoutingResult.Rejected(reason, admission, carrier);
        }

        // The operation identity is intentionally issued after the existing scheduler
        // has committed a physical lane. Reusing this route for replay issues a fresh ID.
        ScheduledOperation scheduled = ScheduledOperation.CreateAfterStageB(
            admission,
            workingBundleSequence,
            workingSlotIndex,
            selectedLane,
            attemptIssuer);
        return Rf06ScalarRoutingResult.Scheduled(admission, carrier, scheduled);
    }

    private static void PrepareCarrier(AdmissionRecord admission, MicroOp carrier)
    {
        carrier.VirtualThreadId = admission.VirtualThreadId;
        carrier.OwnerThreadId = admission.VirtualThreadId;
        carrier.OwnerContextId = admission.OwnerContextId;
        carrier.RefreshAdmissionMetadata();
    }

    private static bool HasEquivalentCarrierFacts(ExecutionContract contract, MicroOp carrier)
    {
        return contract.GeneratedBinding.Opcode == carrier.OpCode &&
               carrier.InstructionClass == contract.InstructionClass &&
               carrier.SerializationClass == contract.SerializationClass &&
               HasEquivalentPlacement(carrier.Placement, contract.Placement) &&
               contract.StaticMemoryPlan is null &&
               carrier.IsMemoryOp == (contract.Memory.Kind != MemoryCapabilityKind.NoMemory) &&
               carrier.WritesRegister == (contract.StaticEffectContract == "RegisterWrite") &&
               carrier.IsStealable == contract.IsStealable &&
               carrier.IsRetireVisible == contract.IsRetireVisible &&
               carrier.ReadRegisters.SequenceEqual(contract.ReadRegisters) &&
               carrier.WriteRegisters.SequenceEqual(contract.WriteRegisters) &&
               carrier.ResourceMask == contract.ResourceMask;
    }

    private static bool HasEquivalentPlacement(
        SlotPlacementMetadata carrier,
        ExecutionPlacement contract) =>
        carrier.RequiredSlotClass == contract.RequiredSlotClass &&
        carrier.PinningKind == contract.PinningKind &&
        carrier.PinnedLaneId == contract.PinnedLaneId &&
        carrier.DomainTag == contract.DomainTag;

    private static long GetStageARejectCount(MicroOpScheduler scheduler) =>
        scheduler.StaticClassOvercommitRejects +
        scheduler.DynamicClassExhaustionRejects +
        scheduler.TypedSlotResourceConflictRejects +
        scheduler.TypedSlotScoreboardRejects +
        scheduler.TypedSlotBankPendingRejects +
        scheduler.TypedSlotHardwareBudgetRejects +
        scheduler.TypedSlotSpeculationBudgetRejects +
        scheduler.TypedSlotDomainRejects +
        scheduler.TypedSlotAssistQuotaRejects +
        scheduler.TypedSlotAssistBackpressureRejects;

}

internal enum Rf06ScalarRoutingRejectReason : byte
{
    None = 0,
    NotScalarFamily = 1,
    TypedSchedulerPathDisabled = 2,
    StageAEligibility = 3,
    CarrierContractMismatch = 4,
    StageAReject = 5,
    StageANotSelected = 6,
    StageBNoLane = 7,
}

internal sealed class Rf06ScalarRoutingResult
{
    private Rf06ScalarRoutingResult(
        Rf06ScalarRoutingRejectReason rejectReason,
        AdmissionRecord? admission,
        MicroOp? carrier,
        ScheduledOperation? scheduled)
    {
        RejectReason = rejectReason;
        Admission = admission;
        Carrier = carrier;
        ScheduledOperation = scheduled;
    }

    internal Rf06ScalarRoutingRejectReason RejectReason { get; }
    internal AdmissionRecord? Admission { get; }
    internal MicroOp? Carrier { get; }
    internal ScheduledOperation? ScheduledOperation { get; }
    internal bool IsScheduled => ScheduledOperation is not null;

    internal static Rf06ScalarRoutingResult Rejected(
        Rf06ScalarRoutingRejectReason reason,
        AdmissionRecord? admission,
        MicroOp? carrier) =>
        new(reason, admission, carrier, scheduled: null);

    internal static Rf06ScalarRoutingResult Scheduled(
        AdmissionRecord admission,
        MicroOp carrier,
        ScheduledOperation scheduled) =>
        new(Rf06ScalarRoutingRejectReason.None, admission, carrier, scheduled);
}
