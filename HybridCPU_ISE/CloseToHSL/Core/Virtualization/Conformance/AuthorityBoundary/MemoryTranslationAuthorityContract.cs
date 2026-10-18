namespace YAKSys_Hybrid_CPU.Core;

public enum MemoryTranslationAuthorityViolation : byte
{
    None = 0,
    GuestCr3UsedAsAuthority = 1,
    VmcsIdentityUsedAsAuthority = 2,
    MissingGenericAddressSpaceRoot = 3,
    CompatibilityIdentityInAuthorityView = 4,
    MissingDomainControl = 5,
    DomainControlMismatch = 6,
}

public readonly record struct MemoryTranslationAuthorityRequest(
    bool UsesGuestCr3AsAuthority,
    bool UsesVmcsIdentityAsAuthority,
    bool HasGenericAddressSpaceRoot,
    bool AuthorityViewIncludesCompatibilityIdentity);

public sealed partial class MemoryTranslationAuthorityContract
{
    public MemoryTranslationAuthorityViolation Evaluate(
        MemoryTranslationAuthorityRequest request)
    {
        if (request.UsesGuestCr3AsAuthority)
        {
            return MemoryTranslationAuthorityViolation.GuestCr3UsedAsAuthority;
        }

        if (request.UsesVmcsIdentityAsAuthority)
        {
            return MemoryTranslationAuthorityViolation.VmcsIdentityUsedAsAuthority;
        }

        if (!request.HasGenericAddressSpaceRoot)
        {
            return MemoryTranslationAuthorityViolation.MissingGenericAddressSpaceRoot;
        }

        return request.AuthorityViewIncludesCompatibilityIdentity
            ? MemoryTranslationAuthorityViolation.CompatibilityIdentityInAuthorityView
            : MemoryTranslationAuthorityViolation.None;
    }

    public bool IsSatisfied(MemoryTranslationAuthorityRequest request) =>
        Evaluate(request) == MemoryTranslationAuthorityViolation.None;

    public bool IsSatisfied(MemoryTranslationControl control)
        => false;

    public MemoryTranslationAuthorityViolation EvaluateDomainControl(
        MemoryTranslationControl control)
        => MemoryTranslationAuthorityViolation.MissingDomainControl;
}
