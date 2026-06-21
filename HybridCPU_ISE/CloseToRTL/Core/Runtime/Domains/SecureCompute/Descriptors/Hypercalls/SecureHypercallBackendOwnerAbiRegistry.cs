namespace YAKSys_Hybrid_CPU.Core;

public static class SecureHypercallBackendOwnerAbiRegistry
{
    public const string RegistrySource =
        "ADR-SC-HYP-BACKEND-OWNER";

    public const string AllocationRevision =
        "ADR-SC-HYP-BACKEND-OWNER-2026-06-17";

    public const ushort TransportOpcodeValue =
        CompatAbiFreezeContract.FrozenCallOpcode;

    public const ulong DecodedLeafValue =
        0x5343_4859_5042UL;

    public const ulong ServiceIdValue =
        0x5343_5356_4345UL;

    public const ulong OwnerIdValue =
        0x5343_4F57_4E52UL;

    public const ulong OwnerEpochValue = 13UL;

    public const ushort ContractVersionMajor = 1;

    public const ushort ContractVersionMinor = 0;

    public const ulong RequiredGrantLocalIdValue =
        0x5343_4752_4E54UL;

    public const ulong RequiredGrantProvenanceHashValue =
        0x5343_4144_5250UL;

    public const ulong OwnerPolicyDigestValue =
        0x5343_504F_4C59UL;

    public const ulong OwnerProofDigestValue =
        0x5343_5052_4F46UL;

    public static SecureHypercallTransportOpcode TransportOpcode =>
        new(TransportOpcodeValue);

    public static SecureHypercallDecodedLeaf DecodedLeaf =>
        new(DecodedLeafValue);

    public static SecureComputeServiceId ServiceId =>
        new(ServiceIdValue);

    public static SecureBackendOwnerId OwnerId =>
        new(OwnerIdValue);

    public static SecureRevocationEpoch OwnerEpoch =>
        new(OwnerEpochValue);

    public static SecureHypercallContractVersion ContractVersion =>
        new(ContractVersionMajor, ContractVersionMinor);

    public static SecureGrantHandle RequiredGrant =>
        new(
            SecureGrantHandleKind.HypercallPolicy,
            RequiredGrantLocalIdValue,
            RequiredGrantProvenanceHashValue,
            OwnerEpochValue);

    public static SecureHypercallBackendContractDescriptor ProductionContract =>
        new(
            DecodedLeaf,
            ServiceId,
            OwnerId,
            OwnerEpoch,
            ContractVersion,
            RequiredGrant,
            SecureHypercallReplayPolicy.DenyReplay,
            SecureHypercallCancellationPolicy.DenyBeforeExecution,
            SecureHypercallRequestMigrationClass.NonMigratableInFlight,
            SecureHypercallResultMigrationClass.NoResultBeforeExecution);

    public static SecureBackendOwnerDescriptor CreateOwnerDescriptor(
        SecureBackendOwnerSource source =
            SecureBackendOwnerSource.NeutralRuntimeService,
        SecureRevocationEpoch? epoch = null,
        bool grantProofValidated = true,
        bool evidenceProofValidated = true,
        bool completionFenceValidated = true,
        bool retireFenceValidated = true,
        bool negativeTestsPresent = true) =>
        new(
            OwnerIdValue,
            source,
            OwnerPolicyDigestValue,
            OwnerProofDigestValue,
            epoch ?? OwnerEpoch,
            Materialized: true,
            grantProofValidated,
            evidenceProofValidated,
            completionFenceValidated,
            retireFenceValidated,
            negativeTestsPresent);
}
