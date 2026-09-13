namespace YAKSys_Hybrid_CPU.Core;

public enum SecureDomainOperationClass : byte
{
    Ordinary = 0,
    EnterSecureDomain = 1,
    TouchSecureMemory = 2,
    CreateEvidence = 3,
    PublishCompletion = 4,
    PublishRetireSideEffect = 5,
    SecureIo = 6,
    SecureHypercall = 7,
    SecureMigration = 8,
    NestedSecureDomain = 9,
    CompatibilityProjection = 10,
}

public enum SecureDomainAdmissionDecision : byte
{
    AllowedNoEffect = 0,
    AllowedSecureOperation = 1,
    DeniedMissingDescriptor = 2,
    DeniedDisabledDescriptor = 3,
    DeniedUnmaterializedDescriptor = 4,
    DeniedMissingMeasurement = 5,
    DeniedMissingMemoryPolicy = 6,
    DeniedMissingEvidencePolicy = 7,
    DeniedMissingMigrationPolicy = 8,
    DeniedMissingIoPolicy = 9,
    DeniedMissingHypercallPolicy = 10,
    DeniedOrdinaryOverDenyGuard = 11,
    DeniedMemoryDomainBindingMismatch = 12,
}

public readonly record struct SecureDomainAdmissionResult(
    SecureDomainAdmissionDecision Decision,
    string Reason)
{
    public bool IsAllowed =>
        Decision is SecureDomainAdmissionDecision.AllowedNoEffect
            or SecureDomainAdmissionDecision.AllowedSecureOperation;

    public static SecureDomainAdmissionResult AllowedNoEffect { get; } =
        new(SecureDomainAdmissionDecision.AllowedNoEffect, string.Empty);

    public static SecureDomainAdmissionResult AllowedSecureOperation { get; } =
        new(SecureDomainAdmissionDecision.AllowedSecureOperation, string.Empty);

    public static SecureDomainAdmissionResult Denied(
        SecureDomainAdmissionDecision decision,
        string reason) =>
        new(decision, reason);
}

public sealed partial class SecureDomainAdmissionPolicy
{
    public SecureDomainAdmissionResult Admit(
        SecureComputeDomainDescriptor? descriptor,
        SecureDomainOperationClass operationClass,
        DomainMeasurementDescriptor? measurement,
        SecureMemoryDomainDescriptor? memory)
    {
        if (operationClass == SecureDomainOperationClass.Ordinary)
        {
            return SecureDomainAdmissionResult.AllowedNoEffect;
        }

        if (descriptor is null)
        {
            return SecureDomainAdmissionResult.Denied(
                SecureDomainAdmissionDecision.DeniedMissingDescriptor,
                "Secure descriptor is required only for secure-domain operation classes.");
        }

        if (!descriptor.IsEnabled)
        {
            return SecureDomainAdmissionResult.Denied(
                SecureDomainAdmissionDecision.DeniedDisabledDescriptor,
                "Secure descriptor is disabled.");
        }

        if (!descriptor.IsMaterialized)
        {
            return SecureDomainAdmissionResult.Denied(
                SecureDomainAdmissionDecision.DeniedUnmaterializedDescriptor,
                "Secure descriptor must be materialized before secure-domain enforcement is active.");
        }

        if (descriptor.RequiresMeasurement)
        {
            SecureMeasurementAdmissionResult measurementAdmission =
                new SecureMeasurementAdmissionPolicy().AdmitMeasurement(
                    descriptor,
                    measurement,
                    memory);

            if (!measurementAdmission.IsAllowed)
            {
                return SecureDomainAdmissionResult.Denied(
                    SecureDomainAdmissionDecision.DeniedMissingMeasurement,
                    measurementAdmission.Reason);
            }
        }

        if (descriptor.RequiresSecureMemoryPolicy &&
            (memory?.IsMaterialized != true || !memory.HasPrivateMemory))
        {
            return SecureDomainAdmissionResult.Denied(
                SecureDomainAdmissionDecision.DeniedMissingMemoryPolicy,
                "Secure domain requires a materialized secure memory descriptor with private memory policy.");
        }

        if (memory is { IsMaterialized: true } &&
            memory.DomainTag != descriptor.DomainTag)
        {
            return SecureDomainAdmissionResult.Denied(
                SecureDomainAdmissionDecision.DeniedMemoryDomainBindingMismatch,
                "Secure memory descriptor domain tag must match the secure domain descriptor.");
        }

        if (operationClass == SecureDomainOperationClass.SecureIo &&
            !descriptor.IoPolicy.NeutralIoOwnerMaterialized)
        {
            return SecureDomainAdmissionResult.Denied(
                SecureDomainAdmissionDecision.DeniedMissingIoPolicy,
                "Secure I/O operation requires a neutral secure I/O owner policy.");
        }

        if (operationClass == SecureDomainOperationClass.SecureHypercall &&
            !descriptor.HypercallPolicy.HasPolicy)
        {
            return SecureDomainAdmissionResult.Denied(
                SecureDomainAdmissionDecision.DeniedMissingHypercallPolicy,
                "Secure hypercall operation requires a secure hypercall policy.");
        }

        return SecureDomainAdmissionResult.AllowedSecureOperation;
    }
}
