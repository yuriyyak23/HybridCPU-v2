using YAKSys_Hybrid_CPU.Core.Nested;

namespace YAKSys_Hybrid_CPU.Core;

public enum NestedProofAuthoritySourceViolation : byte
{
    None = 0,
    MissingDescriptorBackedProof = 1,
    MissingEvidencePolicyProof = 2,
    CompatibilityAliasUsedAsAuthority = 3,
    MissingRuntimeValidation = 4,
}

public sealed partial class NestedProofAuthoritySourceContract
{
    public NestedProofAuthoritySourceViolation Evaluate(
        NestedProofAuthoritySource source)
    {
        if (!source.DescriptorBacked)
        {
            return NestedProofAuthoritySourceViolation.MissingDescriptorBackedProof;
        }

        if (!source.EvidencePolicyBound)
        {
            return NestedProofAuthoritySourceViolation.MissingEvidencePolicyProof;
        }

        if (source.GateEvidenceClass == EvidenceVisibilityClass.CompatibilityAlias)
        {
            return NestedProofAuthoritySourceViolation.CompatibilityAliasUsedAsAuthority;
        }

        return source.RuntimeValidated
            ? NestedProofAuthoritySourceViolation.None
            : NestedProofAuthoritySourceViolation.MissingRuntimeValidation;
    }

    public bool IsSatisfied(NestedProofAuthoritySource source) =>
        Evaluate(source) == NestedProofAuthoritySourceViolation.None;
}
