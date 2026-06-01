namespace YAKSys_Hybrid_CPU.Core;

public enum SecureMeasurementAdmissionDecision : byte
{
    AllowedMeasurement = 0,
    DeniedMeasurementNotRequired = 1,
    DeniedMissingMeasurement = 2,
    DeniedPendingMeasurement = 3,
    DeniedStaleMeasurement = 4,
    DeniedRevokedMeasurement = 5,
    DeniedUnmaterializedMeasurement = 6,
    DeniedPolicyDigestMismatch = 7,
    DeniedMemoryDigestMismatch = 8,
    DeniedDebugClassMismatch = 9,
    DeniedEvidenceVisibility = 10,
    DeniedPublicationFence = 11,
}

public readonly record struct SecureMeasurementAdmissionResult(
    SecureMeasurementAdmissionDecision Decision,
    string Reason)
{
    public bool IsAllowed =>
        Decision == SecureMeasurementAdmissionDecision.AllowedMeasurement;

    public static SecureMeasurementAdmissionResult AllowedMeasurement { get; } =
        new(SecureMeasurementAdmissionDecision.AllowedMeasurement, string.Empty);

    public static SecureMeasurementAdmissionResult Denied(
        SecureMeasurementAdmissionDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class SecureMeasurementAdmissionPolicy
{
    public SecureMeasurementAdmissionResult AdmitMeasurement(
        SecureComputeDomainDescriptor descriptor,
        DomainMeasurementDescriptor? measurement,
        SecureMemoryDomainDescriptor? memory) =>
        AdmitMeasurement(descriptor, measurement, memory, requireMeasurement: descriptor.RequiresMeasurement);

    public SecureMeasurementAdmissionResult AdmitAttestationPublication(
        SecureComputeDomainDescriptor descriptor,
        DomainMeasurementDescriptor? measurement,
        SecureMemoryDomainDescriptor? memory,
        EvidencePolicyDescriptor? baseEvidencePolicy,
        SecureCompletionPublicationFence? publicationFence)
    {
        SecureMeasurementAdmissionResult measurementAdmission =
            AdmitMeasurement(descriptor, measurement, memory, requireMeasurement: true);
        if (!measurementAdmission.IsAllowed)
        {
            return measurementAdmission;
        }

        DomainMeasurementDescriptor materialized = measurement!;
        SecureEvidenceVisibilityClass evidenceClass = materialized.AttestationEvidenceClass;
        if (descriptor.EvidenceVisibilityPolicy.MustQuarantine(evidenceClass) ||
            evidenceClass == SecureEvidenceVisibilityClass.RecomputedAfterRestore)
        {
            return Deny(
                SecureMeasurementAdmissionDecision.DeniedEvidenceVisibility,
                "Attestation evidence is host-owned, denied or recomputed-only and cannot be published to guest-visible state.");
        }

        if (publicationFence?.CanPublishCompletion != true)
        {
            return Deny(
                SecureMeasurementAdmissionDecision.DeniedPublicationFence,
                "Attestation publication requires a completion publication fence.");
        }

        if (baseEvidencePolicy is null)
        {
            return Deny(
                SecureMeasurementAdmissionDecision.DeniedEvidenceVisibility,
                "Attestation publication requires a neutral evidence policy.");
        }

        return evidenceClass switch
        {
            SecureEvidenceVisibilityClass.GuestVisible
                when descriptor.EvidenceVisibilityPolicy.CanExposeToGuest(baseEvidencePolicy, evidenceClass) =>
                    SecureMeasurementAdmissionResult.AllowedMeasurement,

            SecureEvidenceVisibilityClass.CompatibilityAlias
                when descriptor.CompatibilityProjectionPolicy.AllowReadOnlyAliases &&
                     descriptor.EvidenceVisibilityPolicy.CanExposeToGuest(baseEvidencePolicy, evidenceClass) =>
                    SecureMeasurementAdmissionResult.AllowedMeasurement,

            SecureEvidenceVisibilityClass.MigrationSerializable
                when descriptor.EvidenceVisibilityPolicy.CanSerializeAcrossMigration(baseEvidencePolicy, evidenceClass) =>
                    SecureMeasurementAdmissionResult.AllowedMeasurement,

            SecureEvidenceVisibilityClass.DebugOnly
                when descriptor.DebugPolicy.AllowsDebug &&
                     materialized.DebugClass == SecureMeasurementDebugClass.MeasuredDebug &&
                     descriptor.EvidenceVisibilityPolicy.CanExposeToGuest(baseEvidencePolicy, evidenceClass) =>
                    SecureMeasurementAdmissionResult.AllowedMeasurement,

            _ => Deny(
                SecureMeasurementAdmissionDecision.DeniedEvidenceVisibility,
                "Attestation evidence visibility is not allowed by secure and neutral evidence policies."),
        };
    }

    private static SecureMeasurementAdmissionResult AdmitMeasurement(
        SecureComputeDomainDescriptor descriptor,
        DomainMeasurementDescriptor? measurement,
        SecureMemoryDomainDescriptor? memory,
        bool requireMeasurement)
    {
        if (!requireMeasurement)
        {
            return SecureMeasurementAdmissionResult.AllowedMeasurement;
        }

        if (measurement is null ||
            measurement.State == SecureMeasurementState.Missing)
        {
            return Deny(
                SecureMeasurementAdmissionDecision.DeniedMissingMeasurement,
                "Secure domain requires a materialized measurement descriptor.");
        }

        if (measurement.State == SecureMeasurementState.Pending)
        {
            return Deny(
                SecureMeasurementAdmissionDecision.DeniedPendingMeasurement,
                "Secure domain measurement is still pending materialization.");
        }

        if (measurement.State == SecureMeasurementState.Stale)
        {
            return Deny(
                SecureMeasurementAdmissionDecision.DeniedStaleMeasurement,
                "Secure domain measurement epoch is stale.");
        }

        if (measurement.State == SecureMeasurementState.Revoked)
        {
            return Deny(
                SecureMeasurementAdmissionDecision.DeniedRevokedMeasurement,
                "Secure domain measurement has been revoked.");
        }

        if (!measurement.IsMaterialized ||
            !measurement.BindsDomain(descriptor.DomainTag))
        {
            return Deny(
                SecureMeasurementAdmissionDecision.DeniedUnmaterializedMeasurement,
                "Secure domain measurement must be materialized and bound to the secure domain.");
        }

        if (!measurement.BindsPolicyDigest(DomainMeasurementDescriptor.ComputePolicyDigest(descriptor)))
        {
            return Deny(
                SecureMeasurementAdmissionDecision.DeniedPolicyDigestMismatch,
                "Secure domain measurement must bind to the secure domain policy digest.");
        }

        if (memory is { IsMaterialized: true })
        {
            if (!measurement.IsCurrentFor(memory.PolicyEpoch))
            {
                return Deny(
                    SecureMeasurementAdmissionDecision.DeniedStaleMeasurement,
                    "Secure domain measurement epoch must match the secure memory policy epoch.");
            }

            if (memory.HasMeasuredMemory &&
                !measurement.BindsMemoryDigest(DomainMeasurementDescriptor.ComputeMemoryDigest(memory)))
            {
                return Deny(
                    SecureMeasurementAdmissionDecision.DeniedMemoryDigestMismatch,
                    "Secure domain measurement must bind to measured secure memory regions.");
            }
        }

        if (descriptor.DebugPolicy.ChangesMeasurementClass &&
            measurement.DebugClass != SecureMeasurementDebugClass.MeasuredDebug)
        {
            return Deny(
                SecureMeasurementAdmissionDecision.DeniedDebugClassMismatch,
                "Secure debug policy changes the measurement class and requires measured-debug evidence.");
        }

        if (!descriptor.DebugPolicy.AllowsDebug &&
            measurement.DebugClass == SecureMeasurementDebugClass.MeasuredDebug)
        {
            return Deny(
                SecureMeasurementAdmissionDecision.DeniedDebugClassMismatch,
                "Measured-debug evidence is denied unless secure debug policy explicitly allows debug.");
        }

        return SecureMeasurementAdmissionResult.AllowedMeasurement;
    }

    private static SecureMeasurementAdmissionResult Deny(
        SecureMeasurementAdmissionDecision decision,
        string reason) =>
        SecureMeasurementAdmissionResult.Denied(decision, reason);
}
