namespace YAKSys_Hybrid_CPU.Core;

public enum SecureComputeAuthorityBoundaryViolation : byte
{
    None = 0,
    VmxActivation = 1,
    VmxCapsAuthority = 2,
    VmcsStateStore = 3,
    ActiveVmcsPointerIdentity = 4,
}

public sealed partial class SecureComputeVmxAuthorityBoundaryContract
{
    public SecureComputeAuthorityBoundaryViolation Validate(
        bool vmxActivatesSecureCompute,
        bool vmxCapsGrantsSecureCompute,
        bool vmcsStoresSecureState,
        bool activeVmcsPointerIsDomainIdentity)
    {
        if (vmxActivatesSecureCompute)
        {
            return SecureComputeAuthorityBoundaryViolation.VmxActivation;
        }

        if (vmxCapsGrantsSecureCompute)
        {
            return SecureComputeAuthorityBoundaryViolation.VmxCapsAuthority;
        }

        if (vmcsStoresSecureState)
        {
            return SecureComputeAuthorityBoundaryViolation.VmcsStateStore;
        }

        if (activeVmcsPointerIsDomainIdentity)
        {
            return SecureComputeAuthorityBoundaryViolation.ActiveVmcsPointerIdentity;
        }

        return SecureComputeAuthorityBoundaryViolation.None;
    }
}
