namespace YAKSys_Hybrid_CPU.Core;

public enum SecureComputeVmWriteDecision : byte
{
    DeniedSecureStateMutation = 0,
    DeniedProjectionOnlyField = 1,
}

public sealed partial class SecureComputeVmWriteDenyPolicy
{
    public SecureComputeVmWriteDecision Deny(bool secureSensitiveField) =>
        secureSensitiveField
            ? SecureComputeVmWriteDecision.DeniedSecureStateMutation
            : SecureComputeVmWriteDecision.DeniedProjectionOnlyField;
}
