using System;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Storage-only containment for stable core identity and explicit platform,
/// execution-mode and interrupt-dispatch bindings. Construction and explicit
/// platform replacement retain lifecycle authority.
/// </summary>
internal sealed class CoreBindingState
{
    internal uint CoreId;
    internal CpuCorePlatformContext PlatformContext;
    internal ProcessorMode ExecutionMode;
    internal Func<Processor.DeviceType, ushort, ulong, byte>? InterruptDispatcher;
}
