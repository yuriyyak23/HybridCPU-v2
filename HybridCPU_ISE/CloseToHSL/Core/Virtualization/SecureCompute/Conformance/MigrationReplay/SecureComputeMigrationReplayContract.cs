namespace YAKSys_Hybrid_CPU.Core;

public enum SecureComputeMigrationReplayViolation : byte
{
    None = 0,
    EpochRollback = 1,
    VmcsProjectionAuthority = 2,
    CompatibilityMetadataAuthority = 3,
    PrivateMemoryWithoutSealedPayload = 4,
}

public sealed partial class SecureComputeMigrationReplayContract
{
    public SecureComputeMigrationReplayViolation Validate(
        bool epochRollback,
        bool vmcsProjectionAuthority,
        bool compatibilityMetadataAuthority,
        bool privateMemoryWithoutSealedPayload)
    {
        if (epochRollback)
        {
            return SecureComputeMigrationReplayViolation.EpochRollback;
        }

        if (vmcsProjectionAuthority)
        {
            return SecureComputeMigrationReplayViolation.VmcsProjectionAuthority;
        }

        if (compatibilityMetadataAuthority)
        {
            return SecureComputeMigrationReplayViolation.CompatibilityMetadataAuthority;
        }

        if (privateMemoryWithoutSealedPayload)
        {
            return SecureComputeMigrationReplayViolation.PrivateMemoryWithoutSealedPayload;
        }

        return SecureComputeMigrationReplayViolation.None;
    }
}
