namespace YAKSys_Hybrid_CPU.Core;

public enum SecureComputeNoEmissionViolation : byte
{
    None = 0,
    NewInstructionEncoding = 1,
    NewOperandFormat = 2,
    CapabilityAwareLoadStoreFetch = 3,
    VmxSecureModeEmission = 4,
}

public sealed partial class SecureComputeNoEmissionContract
{
    public SecureComputeNoEmissionViolation Validate(
        bool emitsNewInstructionEncoding,
        bool emitsNewOperandFormat,
        bool emitsCapabilityAwareLoadStoreFetch,
        bool emitsVmxSecureMode)
    {
        if (emitsNewInstructionEncoding)
        {
            return SecureComputeNoEmissionViolation.NewInstructionEncoding;
        }

        if (emitsNewOperandFormat)
        {
            return SecureComputeNoEmissionViolation.NewOperandFormat;
        }

        if (emitsCapabilityAwareLoadStoreFetch)
        {
            return SecureComputeNoEmissionViolation.CapabilityAwareLoadStoreFetch;
        }

        if (emitsVmxSecureMode)
        {
            return SecureComputeNoEmissionViolation.VmxSecureModeEmission;
        }

        return SecureComputeNoEmissionViolation.None;
    }
}
