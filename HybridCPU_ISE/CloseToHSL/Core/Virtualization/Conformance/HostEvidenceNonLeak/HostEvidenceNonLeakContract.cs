namespace YAKSys_Hybrid_CPU.Core;

public enum HostEvidenceNonLeakViolation : byte
{
    None = 0,
    EvidenceEnvelopeLeak = 1,
    CompletionEnvelopeLeak = 2,
    DescriptorEnvelopeLeak = 3,
    GuestVisibleProjectionLeak = 4,
}

public sealed partial class HostEvidenceNonLeakContract
{
    public bool IsHostOwnedEvidenceClass(EvidenceVisibilityClass visibilityClass) =>
        visibilityClass is EvidenceVisibilityClass.HostOwnedRuntimeEvidence
            or EvidenceVisibilityClass.SchedulerEvidence
            or EvidenceVisibilityClass.BackendBindingEvidence
            or EvidenceVisibilityClass.NativeTokenEvidence;

    public HostEvidenceNonLeakViolation ValidateGuestVisibility(
        EvidenceVisibilityClass visibilityClass,
        bool isHostOwned,
        bool isGuestVisible)
    {
        if (!isGuestVisible)
        {
            return HostEvidenceNonLeakViolation.None;
        }

        return isHostOwned || IsHostOwnedEvidenceClass(visibilityClass)
            ? HostEvidenceNonLeakViolation.GuestVisibleProjectionLeak
            : HostEvidenceNonLeakViolation.None;
    }

    public HostEvidenceNonLeakViolation ValidateEvidenceEnvelope(
        EvidenceSidebandEnvelope envelope,
        EvidencePolicyDescriptor evidencePolicy)
    {
        if (envelope is null)
        {
            return HostEvidenceNonLeakViolation.None;
        }

        return envelope.RequiresHostHandling && envelope.CanExposeToGuest(evidencePolicy)
            ? HostEvidenceNonLeakViolation.EvidenceEnvelopeLeak
            : HostEvidenceNonLeakViolation.None;
    }

    public HostEvidenceNonLeakViolation ValidateCompletionEnvelope(
        CompletionSidebandEnvelope envelope,
        EvidencePolicyDescriptor evidencePolicy)
    {
        if (envelope is null)
        {
            return HostEvidenceNonLeakViolation.None;
        }

        return envelope.RequiresHostHandling && envelope.CanExposeToGuest(evidencePolicy)
            ? HostEvidenceNonLeakViolation.CompletionEnvelopeLeak
            : HostEvidenceNonLeakViolation.None;
    }

    public HostEvidenceNonLeakViolation ValidateDescriptorEnvelope(
        DescriptorSidebandEnvelope envelope,
        EvidencePolicyDescriptor evidencePolicy)
    {
        if (envelope is null)
        {
            return HostEvidenceNonLeakViolation.None;
        }

        return envelope.RequiresHostHandling && envelope.CanExposeToGuest(evidencePolicy)
            ? HostEvidenceNonLeakViolation.DescriptorEnvelopeLeak
            : HostEvidenceNonLeakViolation.None;
    }
}
