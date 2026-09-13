using YAKSys_Hybrid_CPU.Core.Memory;

namespace YAKSys_Hybrid_CPU.Core;

internal readonly record struct NeutralCompletionSecondStageProjection(
    NeutralCompletionCompatibilityProjectionDecision Decision,
    ulong GuestPhysicalAddress,
    ulong EptViolationQualification,
    string Reason)
{
    internal bool IsProjected =>
        Decision == NeutralCompletionCompatibilityProjectionDecision.Projected;
}

/// <summary>
/// Exact compatibility-only projection of one committed neutral CPU
/// second-stage translation-fault tuple. This owner cannot create completion
/// state and does not consume VMCS, nested or IOMMU authority.
/// </summary>
internal sealed class NeutralCompletionSecondStageProjectionOwner
{
    private const ulong ReadAccessBit = 1UL << 0;
    private const ulong WriteAccessBit = 1UL << 1;
    private const ulong ExecuteAccessBit = 1UL << 2;
    private const ulong GuestLinearAddressValidBit = 1UL << 7;
    private const ulong MisconfigurationBit = 1UL << 9;
    private readonly ulong _producerOwnerIdentity;
    private readonly ulong _producerOwnerEpoch;

    internal NeutralCompletionSecondStageProjectionOwner(
        ArchitecturalCompletionCommitOwner.ProducerRegistration producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        if (producer.Policy.OwnerName != "CanonicalCpuSecondStageTranslationFaultProducer" ||
            producer.Policy.CompletionClass != NeutralArchitecturalCompletionClass.TranslationFault ||
            !producer.Policy.RequiresReason || !producer.Policy.AllowsQualification ||
            producer.Policy.AllowedAddressSemantic != NeutralFaultAddressSemantic.GuestPhysicalAddress ||
            producer.Policy.AllowedAuxiliarySemantic != NeutralFaultAuxiliarySemantic.SecondStageTranslationViolation ||
            !producer.Policy.RequiresTranslationProvenance ||
            !producer.Policy.RequiresSecondStageProvenance)
        {
            throw new ArgumentException(
                "Second-stage compatibility projection requires the exact neutral CPU second-stage producer policy.",
                nameof(producer));
        }

        _producerOwnerIdentity = producer.OwnerIdentity;
        _producerOwnerEpoch = producer.OwnerEpoch;
    }

    internal NeutralCompletionSecondStageProjection Project(
        in NeutralCompletionObservationSnapshot snapshot)
    {
        if (snapshot.ProducerOwnerIdentity != _producerOwnerIdentity ||
            snapshot.ProducerOwnerEpoch != _producerOwnerEpoch)
            return Deny(NeutralCompletionCompatibilityProjectionDecision.DeniedProducer,
                "Snapshot was not issued by the exact CPU second-stage producer.");
        if (snapshot.CompletionClass != NeutralArchitecturalCompletionClass.TranslationFault ||
            snapshot.Facts.CompletionClass != NeutralArchitecturalCompletionClass.TranslationFault)
            return Deny(NeutralCompletionCompatibilityProjectionDecision.DeniedCompletionClass,
                "Only the exact neutral translation-fault completion class is admitted.");
        if (!snapshot.TranslationProvenance.HasSecondStageIdentity)
            return Deny(NeutralCompletionCompatibilityProjectionDecision.DeniedAuxiliarySemantic,
                "Exact CPU second-stage provenance is required.");
        if (!snapshot.Facts.FaultAddress.IsPresent)
            return Deny(NeutralCompletionCompatibilityProjectionDecision.DeniedReasonAbsent,
                "GuestPhysicalAddress requires an explicitly present address fact.");
        if (snapshot.Facts.FaultAddress.Semantic != NeutralFaultAddressSemantic.GuestPhysicalAddress)
            return Deny(NeutralCompletionCompatibilityProjectionDecision.DeniedReasonSemantic,
                "The address fact is not a neutral guest-physical address.");
        if (!snapshot.Facts.Reason.IsPresent || !snapshot.Facts.Qualification.IsPresent)
            return Deny(NeutralCompletionCompatibilityProjectionDecision.DeniedQualificationAbsent,
                "Reason-bound qualification is absent.");
        if (!snapshot.Facts.FaultAuxiliary.IsPresent ||
            snapshot.Facts.FaultAuxiliary.Semantic != NeutralFaultAuxiliarySemantic.SecondStageTranslationViolation)
            return Deny(NeutralCompletionCompatibilityProjectionDecision.DeniedAuxiliarySemantic,
                "Second-stage violation auxiliary is absent or semantically incompatible.");

        if (!CpuSecondStageNeutralAuxiliary.TryDecode(
                snapshot.Facts.FaultAuxiliary.Value,
                out CpuSecondStageFaultKind kind,
                out CpuInstructionTranslationAccessKind accessKind,
                out ushort accessSize,
                out byte pageWalkLevel) ||
            kind == CpuSecondStageFaultKind.SourceStale)
            return Deny(NeutralCompletionCompatibilityProjectionDecision.DeniedAuxiliarySemantic,
                "Neutral second-stage auxiliary is not an accepted exact fault tuple.");

        CpuInstructionTranslationFaultReason reason =
            unchecked((CpuInstructionTranslationFaultReason)snapshot.Facts.Reason.Value);
        CpuInstructionTranslationFaultReason expectedReason = kind == CpuSecondStageFaultKind.Misconfiguration
            ? CpuInstructionTranslationFaultReason.SecondStageMisconfiguration
            : CpuInstructionTranslationFaultReason.SecondStageViolation;
        if (snapshot.Facts.Reason.Value > byte.MaxValue || reason != expectedReason)
            return Deny(NeutralCompletionCompatibilityProjectionDecision.DeniedReasonSemantic,
                "Neutral reason does not match the exact second-stage fault kind.");

        ulong expectedNeutralQualification =
            ((ulong)(byte)reason << 56) |
            ((ulong)(byte)accessKind << 48) |
            ((ulong)accessSize << 32);
        if (snapshot.Facts.Qualification.Value != expectedNeutralQualification)
            return Deny(NeutralCompletionCompatibilityProjectionDecision.DeniedQualificationSemantic,
                "Neutral qualification does not match the reason/access/size auxiliary tuple.");

        ulong compatibilityQualification = GuestLinearAddressValidBit |
            (((ulong)pageWalkLevel & 0xFUL) << 12) |
            (accessKind switch
            {
                CpuInstructionTranslationAccessKind.InstructionFetch => ExecuteAccessBit,
                CpuInstructionTranslationAccessKind.ScalarLoad => ReadAccessBit,
                CpuInstructionTranslationAccessKind.ScalarStore => WriteAccessBit,
                _ => 0,
            });
        if (kind == CpuSecondStageFaultKind.Misconfiguration)
            compatibilityQualification |= MisconfigurationBit;

        return new(
            NeutralCompletionCompatibilityProjectionDecision.Projected,
            snapshot.Facts.FaultAddress.Value,
            compatibilityQualification,
            "Exact committed CPU second-stage facts mapped to the frozen compatibility read tuple.");
    }

    private static NeutralCompletionSecondStageProjection Deny(
        NeutralCompletionCompatibilityProjectionDecision decision,
        string reason) => new(decision, 0, 0, reason);
}
