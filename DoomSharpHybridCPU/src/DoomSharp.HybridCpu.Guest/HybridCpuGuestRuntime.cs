using System;
using DoomSharp.Core;

namespace DoomSharp.HybridCpu.Guest;

public static class HybridCpuGuestRuntime
{
    private static IHybridCpuGuestServices? _services;

    static HybridCpuGuestRuntime()
    {
        // The production loader executes this guest cctor. The object is an image-owned
        // interface receiver rooted by _services; actual services remain loader/kernel-owned.
        _services = new HybridCpuNativeServiceReceiver();
    }

    /// <summary>
    /// Called by the managed image bootstrap after code-manager, cctor and static-root registration.
    /// The registered service object is itself a static GC root for the process lifetime.
    /// </summary>
    public static void RegisterPlatform(IHybridCpuGuestServices services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    internal static IHybridCpuGuestServices RequirePlatform()
    {
        return _services ?? throw new DoomTerminationException("HybridCPU guest services were not registered.", 2);
    }
}
