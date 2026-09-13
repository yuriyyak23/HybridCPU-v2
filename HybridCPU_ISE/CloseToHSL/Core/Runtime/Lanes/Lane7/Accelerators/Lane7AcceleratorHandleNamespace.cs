using System;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators;

public sealed class Lane7AcceleratorHandleNamespace
{
    private readonly Lane7StateBlock _lane7;

    internal Lane7AcceleratorHandleNamespace(Lane7StateBlock lane7)
    {
        _lane7 = lane7 ?? throw new ArgumentNullException(nameof(lane7));
    }

    public Lane7VirtualHandle Allocate(
        ushort ownerVirtualThreadId,
        AcceleratorDeviceId acceleratorId,
        Lane7VirtualCapability capabilities) =>
        _lane7.AllocateVirtualHandle(ownerVirtualThreadId, acceleratorId, capabilities);

    public bool TryAllocate(
        ushort ownerVirtualThreadId,
        AcceleratorDeviceId acceleratorId,
        Lane7VirtualCapability capabilities,
        out Lane7VirtualHandle handle,
        out Lane7Fault fault) =>
        _lane7.TryAllocateVirtualHandle(
            ownerVirtualThreadId,
            acceleratorId,
            capabilities,
            out handle,
            out fault);

    public bool TryResolve(
        ulong virtualHandle,
        out Lane7VirtualHandle handle) =>
        _lane7.TryGetVirtualHandle(virtualHandle, out handle);

    public bool TryResolveForDescriptor(
        ushort executionDomainTag,
        AcceleratorCommandDescriptor descriptor,
        out Lane7VirtualHandle handle)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return _lane7.TryFindVirtualHandle(
            executionDomainTag,
            descriptor.OwnerBinding.OwnerVirtualThreadId,
            descriptor.AcceleratorId,
            out handle);
    }

    public bool Release(ulong virtualHandle) =>
        _lane7.TryReleaseVirtualHandle(virtualHandle);
}
