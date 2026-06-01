using System;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators;

public sealed class Lane7AcceleratorAdmissionPolicy
{
    private readonly Lane7StateBlock _lane7;

    internal Lane7AcceleratorAdmissionPolicy(Lane7StateBlock lane7)
    {
        _lane7 = lane7 ?? throw new ArgumentNullException(nameof(lane7));
    }

    public bool TryValidateSubmit(
        AcceleratorCommandDescriptor descriptor,
        ushort executionDomainTag,
        ushort addressSpaceTag,
        out Lane7VirtualHandle handle,
        out Lane7Fault fault) =>
        _lane7.TryValidateGuestSubmit(
            descriptor,
            executionDomainTag,
            addressSpaceTag,
            out handle,
            out fault);

    public Lane7PressureSnapshot ObserveSubmitPollPressure(
        ushort ownerVirtualThreadId) =>
        _lane7.ObserveSubmitPollPressure(ownerVirtualThreadId);
}
