using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7bo Lane-7 accelerator-device invalid-input behavior.</summary>
public sealed class Rf127boAcceleratorDeviceIdLane7InvalidInputBehaviorTests
{
    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)7)]
    [InlineData(ushort.MaxValue)]
    public void ZeroAndUndefinedValuesRejectBeforeHandleOwnerMutation(ushort raw)
    {
        var lane7 = new Lane7StateBlock();
        lane7.ConfigureOwnership(executionDomainTag: 7, addressSpaceTag: 3);

        Assert.False(lane7.TryAllocateVirtualHandle(
            ownerVirtualThreadId: 2,
            acceleratorId: (AcceleratorDeviceId)raw,
            capabilities: Lane7VirtualCapability.QueryCaps,
            out Lane7VirtualHandle handle,
            out Lane7Fault fault));
        Assert.Equal(default, handle);
        Assert.Equal(Lane7FaultKind.CapabilityDenied, fault.Kind);
        Assert.Equal(0, lane7.ActiveHandleCount);
        Assert.False(lane7.TryFindVirtualHandle(7, 2, (AcceleratorDeviceId)raw, out _));
    }
}
