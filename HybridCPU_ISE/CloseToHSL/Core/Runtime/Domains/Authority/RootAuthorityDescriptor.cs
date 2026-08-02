namespace YAKSys_Hybrid_CPU.Core;

public enum RootAuthorityClass : byte
{
    RuntimeRoot = 0,
    CompatibilityFrontend = 1,
}

public sealed partial class RootAuthorityDescriptor
{
    public RootAuthorityDescriptor()
        : this(
            authorityClass: RootAuthorityClass.RuntimeRoot,
            authorityEpoch: 0,
            grantedCapabilityMask: 0,
            allowCompatibilityFrontendActivation: false,
            allowAuthoritativeStateMutation: false)
    {
    }

    public RootAuthorityDescriptor(
        RootAuthorityClass authorityClass,
        ulong authorityEpoch,
        ulong grantedCapabilityMask,
        bool allowCompatibilityFrontendActivation,
        bool allowAuthoritativeStateMutation)
    {
        AuthorityClass = authorityClass;
        AuthorityEpoch = authorityEpoch;
        GrantedCapabilityMask = grantedCapabilityMask;
        AllowCompatibilityFrontendActivation = allowCompatibilityFrontendActivation;
        AllowAuthoritativeStateMutation = allowAuthoritativeStateMutation;
    }

    public static RootAuthorityDescriptor FailClosed { get; } = new();

    public RootAuthorityClass AuthorityClass { get; }

    public ulong AuthorityEpoch { get; }

    public ulong GrantedCapabilityMask { get; }

    public bool AllowCompatibilityFrontendActivation { get; }

    public bool AllowAuthoritativeStateMutation { get; }

    public bool IsRuntimeRoot => AuthorityClass == RootAuthorityClass.RuntimeRoot;

    public bool IsCompatibilityFrontendOnly =>
        AuthorityClass == RootAuthorityClass.CompatibilityFrontend;

    public bool HasCapability(ulong capabilityMask) =>
        capabilityMask != 0 &&
        (GrantedCapabilityMask & capabilityMask) == capabilityMask;

    public bool CanActivateCompatibilityFrontend =>
        IsRuntimeRoot &&
        AllowCompatibilityFrontendActivation;

    public bool CanMutateAuthoritativeState(DomainRuntimeOperation operation) =>
        IsRuntimeRoot &&
        AllowAuthoritativeStateMutation &&
        operation.CanMutateAuthoritativeState;

    public RootAuthorityDescriptor WithGrantedCapabilities(ulong capabilityMask) =>
        new(
            AuthorityClass,
            AuthorityEpoch,
            capabilityMask,
            AllowCompatibilityFrontendActivation,
            AllowAuthoritativeStateMutation);
}
