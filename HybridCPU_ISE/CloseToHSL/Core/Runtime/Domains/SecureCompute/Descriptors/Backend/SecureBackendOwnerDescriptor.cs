namespace YAKSys_Hybrid_CPU.Core;

public enum SecureBackendOwnerSource : byte
{
    None = 0,
    NeutralRuntimeService = 1,
    NeutralDeviceModel = 2,
    NeutralMigrationService = 3,
    CompatibilityProjection = 4,
    VmxFrontend = 5,
    VmcsProjection = 6,
    VmxCapsProjection = 7,
    ShadowVmcsProjection = 8,
}

public enum SecureBackendRfcAdrState : byte
{
    Missing = 0,
    Draft = 1,
    Approved = 2,
}

public readonly record struct SecureBackendOwnerDescriptor(
    ulong OwnerId,
    SecureBackendOwnerSource Source,
    ulong PolicyDigest,
    ulong ProofDigest,
    SecureRevocationEpoch Epoch,
    bool Materialized,
    bool GrantProofValidated,
    bool EvidenceProofValidated,
    bool CompletionFenceValidated,
    bool RetireFenceValidated,
    bool NegativeTestsPresent)
{
    public static SecureBackendOwnerDescriptor None { get; } = new(
        OwnerId: 0,
        Source: SecureBackendOwnerSource.None,
        PolicyDigest: 0,
        ProofDigest: 0,
        Epoch: SecureRevocationEpoch.Unmaterialized,
        Materialized: false,
        GrantProofValidated: false,
        EvidenceProofValidated: false,
        CompletionFenceValidated: false,
        RetireFenceValidated: false,
        NegativeTestsPresent: false);

    public bool HasIdentity =>
        OwnerId != 0;

    public bool IsNeutralSource =>
        Source is SecureBackendOwnerSource.NeutralRuntimeService
            or SecureBackendOwnerSource.NeutralDeviceModel
            or SecureBackendOwnerSource.NeutralMigrationService;

    public bool HasProofChain =>
        PolicyDigest != 0 &&
        ProofDigest != 0 &&
        GrantProofValidated &&
        EvidenceProofValidated &&
        CompletionFenceValidated &&
        RetireFenceValidated;
}
