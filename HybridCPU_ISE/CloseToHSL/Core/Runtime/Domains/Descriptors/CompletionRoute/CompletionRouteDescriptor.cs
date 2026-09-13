namespace YAKSys_Hybrid_CPU.Core;

public enum CompletionRouteAuthority : byte
{
    Runtime = 0,
    DomainDescriptor = 1,
    CompatibilityProjection = 2,
}

public sealed partial class CompletionRouteDescriptor
{
    public CompletionRouteDescriptor()
        : this(
            authority: CompletionRouteAuthority.Runtime,
            enabledSourceMask: 0,
            requiresPostedEventQueue: true,
            allowsCompatibilityProjection: true)
    {
    }

    public CompletionRouteDescriptor(
        CompletionRouteAuthority authority,
        ulong enabledSourceMask,
        bool requiresPostedEventQueue,
        bool allowsCompatibilityProjection)
    {
        Authority = authority;
        EnabledSourceMask = enabledSourceMask;
        RequiresPostedEventQueue = requiresPostedEventQueue;
        AllowsCompatibilityProjection = allowsCompatibilityProjection;
    }

    public CompletionRouteAuthority Authority { get; }

    public ulong EnabledSourceMask { get; }

    public bool RequiresPostedEventQueue { get; }

    public bool AllowsCompatibilityProjection { get; }

    public bool IsRuntimeAuthoritative =>
        Authority == CompletionRouteAuthority.Runtime;

    public bool HasEnabledRoutes =>
        EnabledSourceMask != 0;

    public bool CanProjectToCompatibilityFrontend =>
        AllowsCompatibilityProjection &&
        IsRuntimeAuthoritative;

    public bool AllowsSource(LaneCompletionSourceKind sourceKind)
    {
        byte bit = (byte)sourceKind;
        return bit is not 0 and < 64 &&
               (EnabledSourceMask & (1UL << bit)) != 0;
    }

    public CompletionRouteDescriptor WithEnabledSourceMask(ulong enabledSourceMask) =>
        new(Authority, enabledSourceMask, RequiresPostedEventQueue, AllowsCompatibilityProjection);
}
