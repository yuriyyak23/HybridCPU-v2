using YAKSys_Hybrid_CPU.Core.Nested;

namespace YAKSys_Hybrid_CPU.Core;

public enum NestedEnablementProofAuthorityViolation : byte
{
    None = 0,
    MissingRequiredCapabilityProof = 1,
    MissingRequiredGateProof = 2,
    NonAuthoritativeProof = 3,
}

public sealed partial class NestedEnablementProofAuthorityContract
{
    public NestedEnablementProofAuthorityViolation Evaluate(
        NestedEnablementProof proof)
    {
        if (!proof.HasRequiredCapabilities)
        {
            return NestedEnablementProofAuthorityViolation.MissingRequiredCapabilityProof;
        }

        if (!proof.HasRequiredGates)
        {
            return NestedEnablementProofAuthorityViolation.MissingRequiredGateProof;
        }

        return proof.IsAuthoritative
            ? NestedEnablementProofAuthorityViolation.None
            : NestedEnablementProofAuthorityViolation.NonAuthoritativeProof;
    }

    public bool IsSatisfied(NestedEnablementProof proof) =>
        Evaluate(proof) == NestedEnablementProofAuthorityViolation.None;
}
