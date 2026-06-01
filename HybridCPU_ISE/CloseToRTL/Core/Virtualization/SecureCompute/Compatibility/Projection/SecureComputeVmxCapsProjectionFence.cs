namespace YAKSys_Hybrid_CPU.Core;

public enum SecureComputeVmxCapsProjectionFenceDecision : byte
{
    ProjectionAllowed = 0,
    DeniedAuthorityGrant = 1,
    DeniedActivationBit = 2,
    DeniedWriteMutation = 3,
}

public sealed partial class SecureComputeVmxCapsProjectionFence
{
    public SecureComputeVmxCapsProjectionFenceDecision Validate(
        bool attemptsAuthorityGrant,
        bool attemptsActivation,
        bool attemptsWriteMutation)
    {
        if (attemptsAuthorityGrant)
        {
            return SecureComputeVmxCapsProjectionFenceDecision.DeniedAuthorityGrant;
        }

        if (attemptsActivation)
        {
            return SecureComputeVmxCapsProjectionFenceDecision.DeniedActivationBit;
        }

        if (attemptsWriteMutation)
        {
            return SecureComputeVmxCapsProjectionFenceDecision.DeniedWriteMutation;
        }

        return SecureComputeVmxCapsProjectionFenceDecision.ProjectionAllowed;
    }
}
