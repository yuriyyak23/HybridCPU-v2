using YAKSys_Hybrid_CPU.Core.Memory;

namespace YAKSys_Hybrid_CPU.Core;

internal enum NeutralCompletionCompatibilityProjectionDecision : byte
{
    Projected = 0,
    DeniedProducer = 1,
    DeniedCompletionClass = 2,
    DeniedReasonAbsent = 3,
    DeniedQualificationAbsent = 4,
    DeniedReasonSemantic = 5,
    DeniedQualificationSemantic = 6,
    DeniedAuxiliarySemantic = 7,
}

internal readonly record struct NeutralCompletionReasonQualificationProjection(
    NeutralCompletionCompatibilityProjectionDecision Decision,
    VmExitReason ExitReason,
    ulong ExitQualification,
    string Reason)
{
    internal bool IsProjected =>
        Decision == NeutralCompletionCompatibilityProjectionDecision.Projected;
}

/// <summary>
/// Exact compatibility-only mapping for committed neutral completion facts.
/// The mapper owns no completion state and cannot create or mutate runtime
/// authority. Unlisted producer/reason/qualification tuples are denied.
/// </summary>
internal sealed class NeutralCompletionReasonQualificationProjectionOwner
{
    private const ulong QualificationReservedMask = 0x0000_0000_FFFF_FFFFUL;
    private readonly ulong _producerOwnerIdentity;
    private readonly ulong _producerOwnerEpoch;

    internal NeutralCompletionReasonQualificationProjectionOwner(
        ArchitecturalCompletionCommitOwner.ProducerRegistration producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        if (producer.Policy.CompletionClass != NeutralArchitecturalCompletionClass.TranslationFault ||
            !producer.Policy.RequiresReason || !producer.Policy.AllowsQualification ||
            producer.Policy.AllowedAddressSemantic != NeutralFaultAddressSemantic.VirtualAddress ||
            producer.Policy.AllowedAuxiliarySemantic != NeutralFaultAuxiliarySemantic.TranslationFault)
        {
            throw new ArgumentException(
                "Completion compatibility mapping requires the exact neutral CPU translation-fault producer policy.",
                nameof(producer));
        }

        _producerOwnerIdentity = producer.OwnerIdentity;
        _producerOwnerEpoch = producer.OwnerEpoch;
    }

    internal NeutralCompletionReasonQualificationProjection Project(
        in NeutralCompletionObservationSnapshot snapshot)
    {
        if (snapshot.ProducerOwnerIdentity != _producerOwnerIdentity ||
            snapshot.ProducerOwnerEpoch != _producerOwnerEpoch)
            return Deny(NeutralCompletionCompatibilityProjectionDecision.DeniedProducer,
                "Completion snapshot was not issued by the exact accepted CPU translation-fault producer.");
        if (snapshot.CompletionClass != NeutralArchitecturalCompletionClass.TranslationFault ||
            snapshot.Facts.CompletionClass != NeutralArchitecturalCompletionClass.TranslationFault)
            return Deny(NeutralCompletionCompatibilityProjectionDecision.DeniedCompletionClass,
                "Only the exact neutral CPU translation-fault completion class is mapped.");
        if (!snapshot.Facts.Reason.IsPresent)
            return Deny(NeutralCompletionCompatibilityProjectionDecision.DeniedReasonAbsent,
                "ExitReason mapping requires a present neutral reason fact.");
        if (!snapshot.Facts.Qualification.IsPresent)
            return Deny(NeutralCompletionCompatibilityProjectionDecision.DeniedQualificationAbsent,
                "ExitQualification mapping requires a present reason-bound neutral qualification fact.");
        if (!snapshot.Facts.FaultAuxiliary.IsPresent ||
            snapshot.Facts.FaultAuxiliary.Semantic != NeutralFaultAuxiliarySemantic.TranslationFault ||
            snapshot.Facts.FaultAuxiliary.Value == 0)
            return Deny(NeutralCompletionCompatibilityProjectionDecision.DeniedAuxiliarySemantic,
                "CPU translation compatibility mapping requires its neutral non-zero owner-epoch auxiliary fact.");

        CpuInstructionTranslationFaultReason reason =
            unchecked((CpuInstructionTranslationFaultReason)(byte)snapshot.Facts.Reason.Value);
        if (snapshot.Facts.Reason.Value > byte.MaxValue || reason is not (
                CpuInstructionTranslationFaultReason.AccessDenied or
                CpuInstructionTranslationFaultReason.OwnerScopeMismatch))
        {
            return Deny(NeutralCompletionCompatibilityProjectionDecision.DeniedReasonSemantic,
                "The neutral translation reason has no owner-approved compatibility ExitReason mapping.");
        }

        ulong qualification = snapshot.Facts.Qualification.Value;
        byte encodedReason = unchecked((byte)(qualification >> 56));
        byte encodedAccess = unchecked((byte)(qualification >> 48));
        ushort encodedSize = unchecked((ushort)(qualification >> 32));
        if (encodedReason != (byte)reason ||
            encodedAccess is < (byte)CpuInstructionTranslationAccessKind.InstructionFetch or
                > (byte)CpuInstructionTranslationAccessKind.ScalarStore ||
            encodedSize == 0 || (qualification & QualificationReservedMask) != 0)
        {
            return Deny(NeutralCompletionCompatibilityProjectionDecision.DeniedQualificationSemantic,
                "Neutral qualification is not the exact reason/access/size encoding accepted by the mapping owner.");
        }

        return new(
            NeutralCompletionCompatibilityProjectionDecision.Projected,
            VmExitReason.SecurityPolicyViolation,
            qualification,
            "Exact CPU access-policy translation fault projected to the frozen compatibility reason and its reason-bound qualification.");
    }

    private static NeutralCompletionReasonQualificationProjection Deny(
        NeutralCompletionCompatibilityProjectionDecision decision,
        string reason) => new(decision, VmExitReason.None, 0, reason);
}
