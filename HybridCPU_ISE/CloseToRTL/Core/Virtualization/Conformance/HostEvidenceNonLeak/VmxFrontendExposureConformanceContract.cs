namespace YAKSys_Hybrid_CPU.Core;

public enum VmxFrontendExposureViolation : byte
{
    None = 0,
    HostEvidenceVisible = 1,
    NativeTokenVisible = 2,
    BackendBindingVisible = 3,
    SchedulerEvidenceVisible = 4,
    MigrationOnlyStateVisible = 5,
    LanePrivateStateVisible = 6,
    NonGeneratedProjection = 7,
    MissingAccessPolicy = 8,
}

public readonly record struct VmxFrontendExposureRequest(
    EvidenceVisibilityClass EvidenceClass,
    MigrationPayloadClass PayloadClass,
    bool IsGuestVisible,
    bool ExposesNativeToken,
    bool ExposesBackendHandle,
    bool ExposesSchedulerEvidence,
    bool ExposesMigrationOnlyState,
    bool ExposesLanePrivateState,
    bool IsGeneratedProjection,
    bool HasAccessPolicy);

public sealed partial class VmxFrontendExposureConformanceContract
{
    public VmxFrontendExposureViolation Evaluate(
        VmxFrontendExposureRequest request)
    {
        if (!request.IsGeneratedProjection)
        {
            return VmxFrontendExposureViolation.NonGeneratedProjection;
        }

        if (!request.HasAccessPolicy)
        {
            return VmxFrontendExposureViolation.MissingAccessPolicy;
        }

        if (!request.IsGuestVisible)
        {
            return VmxFrontendExposureViolation.None;
        }

        if (IsHostOwnedEvidence(request.EvidenceClass) ||
            request.PayloadClass == MigrationPayloadClass.HostOwnedRuntimeEvidence)
        {
            return VmxFrontendExposureViolation.HostEvidenceVisible;
        }

        if (request.ExposesNativeToken ||
            request.EvidenceClass == EvidenceVisibilityClass.NativeTokenEvidence ||
            request.PayloadClass == MigrationPayloadClass.NativeTokenEvidence)
        {
            return VmxFrontendExposureViolation.NativeTokenVisible;
        }

        if (request.ExposesBackendHandle ||
            request.EvidenceClass == EvidenceVisibilityClass.BackendBindingEvidence ||
            request.PayloadClass == MigrationPayloadClass.BackendBindingEvidence)
        {
            return VmxFrontendExposureViolation.BackendBindingVisible;
        }

        if (request.ExposesSchedulerEvidence ||
            request.EvidenceClass == EvidenceVisibilityClass.SchedulerEvidence ||
            request.PayloadClass == MigrationPayloadClass.SchedulerEvidence)
        {
            return VmxFrontendExposureViolation.SchedulerEvidenceVisible;
        }

        if (request.ExposesMigrationOnlyState ||
            request.PayloadClass == MigrationPayloadClass.CompatibilityProjectionMetadata)
        {
            return VmxFrontendExposureViolation.MigrationOnlyStateVisible;
        }

        return request.ExposesLanePrivateState
            ? VmxFrontendExposureViolation.LanePrivateStateVisible
            : VmxFrontendExposureViolation.None;
    }

    public bool IsSatisfied(VmxFrontendExposureRequest request) =>
        Evaluate(request) == VmxFrontendExposureViolation.None;

    private static bool IsHostOwnedEvidence(EvidenceVisibilityClass evidenceClass) =>
        evidenceClass is EvidenceVisibilityClass.HostOwnedRuntimeEvidence
            or EvidenceVisibilityClass.SchedulerEvidence
            or EvidenceVisibilityClass.BackendBindingEvidence
            or EvidenceVisibilityClass.NativeTokenEvidence;
}
