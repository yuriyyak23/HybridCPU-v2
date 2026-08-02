namespace YAKSys_Hybrid_CPU.Core;

public enum TrapPolicyAuthority : byte
{
    Runtime = 0,
    CompatibilityProjection = 1,
}

public sealed partial class TrapPolicyDescriptor
{
    public TrapPolicyDescriptor()
        : this(
            authority: TrapPolicyAuthority.Runtime,
            enabledClasses: TrapPolicyClass.None,
            requiresValidatedDomain: true,
            allowsCompatibilityProjection: true)
    {
    }

    public TrapPolicyDescriptor(
        TrapPolicyAuthority authority,
        TrapPolicyClass enabledClasses,
        bool requiresValidatedDomain,
        bool allowsCompatibilityProjection)
    {
        Authority = authority;
        EnabledClasses = enabledClasses;
        RequiresValidatedDomain = requiresValidatedDomain;
        AllowsCompatibilityProjection = allowsCompatibilityProjection;
    }

    public TrapPolicyAuthority Authority { get; }

    public TrapPolicyClass EnabledClasses { get; }

    public bool RequiresValidatedDomain { get; }

    public bool AllowsCompatibilityProjection { get; }

    public bool IsRuntimeAuthoritative =>
        Authority == TrapPolicyAuthority.Runtime;

    public bool HasTrapClasses =>
        EnabledClasses != TrapPolicyClass.None;

    public bool CanProjectToCompatibilityFrontend =>
        AllowsCompatibilityProjection &&
        IsRuntimeAuthoritative;

    public bool AllowsClass(TrapPolicyClass policyClass) =>
        policyClass != TrapPolicyClass.None &&
        (EnabledClasses & policyClass) == policyClass;

    public TrapPolicyDescriptor WithEnabledClasses(TrapPolicyClass enabledClasses) =>
        new(Authority, enabledClasses, RequiresValidatedDomain, AllowsCompatibilityProjection);
}
