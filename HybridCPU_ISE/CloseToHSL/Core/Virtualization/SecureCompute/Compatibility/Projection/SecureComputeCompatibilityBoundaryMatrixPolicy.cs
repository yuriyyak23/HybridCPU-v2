using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace YAKSys_Hybrid_CPU.Core;

public enum SecureComputeCompatibilityFieldClass : byte
{
    OrdinaryReadOnlyProjection = 0,
    GuestPrivilegedControl = 1,
    HostAddressSpaceAlias = 2,
    HostExecutionAlias = 3,
    CompatibilityControl = 4,
    SecureMeasurementEvidenceDebugMigration = 5,
}

public enum SecureComputeCompatibilityMatrixDecision : byte
{
    AllowedReadOnlyProjection = 0,
    DeniedNoNeutralOwner = 1,
    DeniedNoReadOnlySource = 2,
    DeniedNoSecureVisibility = 3,
    DeniedNoMigrationClass = 4,
    DeniedNoConformanceProof = 5,
    DeniedGuestPrivilegedControlOwnerMissing = 6,
    DeniedHostAddressSpaceOwnerMissing = 7,
    DeniedHostExecutionOwnerMissing = 8,
    DeniedCompatibilityControlContractMissing = 9,
    DeniedSecureSensitiveField = 10,
    DeniedSchemaOwnerMismatch = 11,
    DeniedWriteMutation = 12,
    DeniedVmxCapsAuthority = 13,
    DeniedVmcsCheckpointAuthority = 14,
    DeniedBackendSuccess = 15,
    DeniedUnsupportedGuestPrivilegedControlField = 16,
}

public readonly record struct SecureComputeCompatibilityReadMatrixRequest(
    ulong FieldId,
    SecureComputeCompatibilityFieldClass FieldClass,
    SecureComputeProjectionOwnerKind SchemaOwner,
    SecureComputeProjectionOwnerKind ExpectedOwner,
    bool HasNeutralOwner,
    bool HasReadOnlySource,
    bool SecureVisibilityAllowed,
    bool MigrationClassified,
    bool ConformanceProven);

public readonly record struct SecureComputeCompatibilityMatrixResult(
    SecureComputeCompatibilityMatrixDecision Decision,
    bool ValueAvailable,
    bool BackendSuccessAuthorized,
    bool MutationAuthorized,
    string Reason)
{
    public bool IsAllowed =>
        Decision == SecureComputeCompatibilityMatrixDecision.AllowedReadOnlyProjection &&
        ValueAvailable &&
        !BackendSuccessAuthorized &&
        !MutationAuthorized;

    public static SecureComputeCompatibilityMatrixResult AllowedReadOnlyProjection { get; } =
        new(
            SecureComputeCompatibilityMatrixDecision.AllowedReadOnlyProjection,
            ValueAvailable: true,
            BackendSuccessAuthorized: false,
            MutationAuthorized: false,
            string.Empty);

    public static SecureComputeCompatibilityMatrixResult Denied(
        SecureComputeCompatibilityMatrixDecision decision,
        string reason) =>
        new(
            decision,
            ValueAvailable: false,
            BackendSuccessAuthorized: false,
            MutationAuthorized: false,
            reason);
}

public sealed partial class SecureComputeCompatibilityBoundaryMatrixPolicy
{
    public SecureComputeCompatibilityMatrixResult AdmitVmRead(
        SecureComputeCompatibilityReadMatrixRequest request)
    {
        if (request.SchemaOwner != request.ExpectedOwner)
        {
            return Deny(
                SecureComputeCompatibilityMatrixDecision.DeniedSchemaOwnerMismatch,
                "SecureCompute compatibility projection schema owner mismatch is denied.");
        }

        SecureComputeCompatibilityMatrixResult classDecision = DenyByFieldClass(
            request.FieldId,
            request.FieldClass);
        if (!classDecision.IsAllowed)
        {
            return classDecision;
        }

        if (!request.HasNeutralOwner)
        {
            return Deny(
                SecureComputeCompatibilityMatrixDecision.DeniedNoNeutralOwner,
                "SecureCompute VMREAD requires a neutral owner.");
        }

        if (!request.HasReadOnlySource)
        {
            return Deny(
                SecureComputeCompatibilityMatrixDecision.DeniedNoReadOnlySource,
                "SecureCompute VMREAD requires a read-only neutral value source.");
        }

        if (!request.SecureVisibilityAllowed)
        {
            return Deny(
                SecureComputeCompatibilityMatrixDecision.DeniedNoSecureVisibility,
                "SecureCompute VMREAD requires secure visibility policy.");
        }

        if (!request.MigrationClassified)
        {
            return Deny(
                SecureComputeCompatibilityMatrixDecision.DeniedNoMigrationClass,
                "SecureCompute VMREAD requires migration classification.");
        }

        if (!request.ConformanceProven)
        {
            return Deny(
                SecureComputeCompatibilityMatrixDecision.DeniedNoConformanceProof,
                "SecureCompute VMREAD requires conformance proof.");
        }

        return SecureComputeCompatibilityMatrixResult.AllowedReadOnlyProjection;
    }

    public SecureComputeCompatibilityMatrixResult AdmitVmWrite(bool secureSensitiveField)
    {
        _ = secureSensitiveField;
        return Deny(
            SecureComputeCompatibilityMatrixDecision.DeniedWriteMutation,
            "SecureCompute compatibility VMWRITE is denied and has no backend mutation.");
    }

    public SecureComputeCompatibilityMatrixResult AdmitVmxCapsDescriptorMaterialization(
        bool attemptsSecureDescriptorMaterialization)
    {
        return attemptsSecureDescriptorMaterialization
            ? Deny(
                SecureComputeCompatibilityMatrixDecision.DeniedVmxCapsAuthority,
                "VmxCaps projection cannot materialize a SecureCompute descriptor.")
            : SecureComputeCompatibilityMatrixResult.AllowedReadOnlyProjection;
    }

    public SecureComputeCompatibilityMatrixResult AdmitVmcsCheckpointAuthority(
        bool containsSecureMetadata)
    {
        return containsSecureMetadata
            ? Deny(
                SecureComputeCompatibilityMatrixDecision.DeniedVmcsCheckpointAuthority,
                "VMCS projection metadata cannot be SecureCompute checkpoint authority.")
            : SecureComputeCompatibilityMatrixResult.AllowedReadOnlyProjection;
    }

    public SecureComputeCompatibilityMatrixResult AdmitProjectionCompletion(
        bool attemptsBackendSuccess)
    {
        return attemptsBackendSuccess
            ? Deny(
                SecureComputeCompatibilityMatrixDecision.DeniedBackendSuccess,
                "Compatibility projection cannot become SecureCompute backend success.")
            : SecureComputeCompatibilityMatrixResult.AllowedReadOnlyProjection;
    }

    private static SecureComputeCompatibilityMatrixResult DenyByFieldClass(
        ulong fieldId,
        SecureComputeCompatibilityFieldClass fieldClass) =>
        fieldClass switch
        {
            SecureComputeCompatibilityFieldClass.GuestPrivilegedControl
                when fieldId is not (
                    (ulong)VmcsField.GuestCr0 or
                    (ulong)VmcsField.GuestCr4) =>
                    Deny(
                        SecureComputeCompatibilityMatrixDecision.DeniedUnsupportedGuestPrivilegedControlField,
                        "Only GuestCr0 and GuestCr4 have a Phase 10 privileged execution-state projection contract."),
            SecureComputeCompatibilityFieldClass.HostAddressSpaceAlias =>
                Deny(
                    SecureComputeCompatibilityMatrixDecision.DeniedHostAddressSpaceOwnerMissing,
                    "Host address-space aliases require a neutral host-address-space owner."),
            SecureComputeCompatibilityFieldClass.HostExecutionAlias =>
                Deny(
                    SecureComputeCompatibilityMatrixDecision.DeniedHostExecutionOwnerMissing,
                    "Host execution aliases require a neutral host-execution owner."),
            SecureComputeCompatibilityFieldClass.CompatibilityControl =>
                Deny(
                    SecureComputeCompatibilityMatrixDecision.DeniedCompatibilityControlContractMissing,
                    "Compatibility-control fields require an explicit neutral value contract."),
            SecureComputeCompatibilityFieldClass.SecureMeasurementEvidenceDebugMigration =>
                Deny(
                    SecureComputeCompatibilityMatrixDecision.DeniedSecureSensitiveField,
                    "Secure measurement, evidence, debug and migration fields are denied by default."),
            _ => SecureComputeCompatibilityMatrixResult.AllowedReadOnlyProjection,
        };

    private static SecureComputeCompatibilityMatrixResult Deny(
        SecureComputeCompatibilityMatrixDecision decision,
        string reason) =>
        SecureComputeCompatibilityMatrixResult.Denied(decision, reason);
}
