namespace YAKSys_Hybrid_CPU.Core;

public enum SecureComputeVmcsProjectionFenceDecision : byte
{
    ProjectionAllowed = 0,
    DeniedSecureStateStore = 1,
    DeniedCheckpointAuthority = 2,
}

public sealed partial class SecureComputeVmcsProjectionFence
{
    public SecureComputeVmcsProjectionFenceDecision Validate(
        bool storesSecureState,
        bool usedAsCheckpointAuthority)
    {
        if (storesSecureState)
        {
            return SecureComputeVmcsProjectionFenceDecision.DeniedSecureStateStore;
        }

        if (usedAsCheckpointAuthority)
        {
            return SecureComputeVmcsProjectionFenceDecision.DeniedCheckpointAuthority;
        }

        return SecureComputeVmcsProjectionFenceDecision.ProjectionAllowed;
    }
}
