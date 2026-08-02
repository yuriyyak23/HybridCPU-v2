namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class EventTrapDomainIdentityAuthorityRemovalContract
{
    public const string EventDescriptorPath =
        "Core/Runtime/Events/Injection/EventInjectionDescriptor.cs";

    public const string InterruptRemapPath =
        "Core/Runtime/Events/Injection/InterruptRemapPolicy.cs";

    public const string PostedEventQueuePath =
        "Core/Runtime/Events/Injection/PostedEventQueue.cs";

    public const string VirtualInterruptFabricPath =
        "Core/Runtime/Events/Injection/VirtualInterruptFabric.cs";

    public const string TrapRequestPath =
        "Core/Runtime/Events/Traps/TrapRequest.cs";

    public const string VirtualTimerPath =
        "Core/Runtime/Events/Traps/VirtualTimerState.cs";

    public const string VirtualTimerExpirationPath =
        "Core/Runtime/Events/Traps/VirtualTimerState.Expiration.partial.cs";

    public const string CompatibilityEventProjectionPath =
        "Core/VMX/Compatibility/Generated/VmcsProjection/VmcsV2Blocks.cs";

    public static string[] ExecutableEventTrapPaths { get; } =
    {
        EventDescriptorPath,
        InterruptRemapPath,
        PostedEventQueuePath,
        VirtualInterruptFabricPath,
        TrapRequestPath,
        VirtualTimerPath,
        VirtualTimerExpirationPath,
    };

    public static string[] ForbiddenExecutableIdentityMarkers { get; } =
    {
        "Vmid",
        "Vpid",
        "vmid",
        "vpid",
        "VMID",
        "VPID",
    };

    public static string[] ForbiddenCompatibilityEventHelperMarkers { get; } =
    {
        "ushort vmid",
        "ushort vpid",
        "Vmid",
        "Vpid",
        "TryQueue(",
        "TryDeliver(",
        "ConfigureInterruptRemap",
        "RemoveInterruptRemap",
        "ClearInterruptRemaps",
        "RestoreSnapshot",
    };

    public bool IsNeutralIdentity(
        EventInjectionDescriptor descriptor,
        TrapRequest request,
        ushort executionDomainTag,
        ushort addressSpaceTag) =>
        descriptor.ExecutionDomainTag == executionDomainTag &&
        descriptor.AddressSpaceTag == addressSpaceTag &&
        request.ExecutionDomainTag == executionDomainTag &&
        request.AddressSpaceTag == addressSpaceTag;
}
