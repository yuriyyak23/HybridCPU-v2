using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7br defined AcceleratorDeviceId checkpoint-restore parity.</summary>
public sealed class Rf127brAcceleratorDeviceIdCheckpointValidInputContractTests
{
    [Fact]
    public void EveryDefinedEnumMemberRetainsItsOwnerKeyAndHandleAcrossCheckpointRestore()
    {
        var source = new Lane7StateBlock();
        source.ConfigureOwnership(executionDomainTag: 7, addressSpaceTag: 3);
        var allocated = new List<Lane7VirtualHandle>();

        foreach (AcceleratorDeviceId acceleratorId in Enum.GetValues<AcceleratorDeviceId>())
        {
            Assert.True(source.TryAllocateVirtualHandle(2, acceleratorId, Lane7VirtualCapability.QueryCaps,
                out Lane7VirtualHandle handle, out Lane7Fault fault));
            Assert.Equal(Lane7Fault.None, fault);
            allocated.Add(handle);
        }

        var restored = new Lane7StateBlock();
        restored.RestoreCheckpoint(source.CreateCheckpoint());

        Assert.Equal(allocated.Count, restored.ActiveHandleCount);
        foreach (Lane7VirtualHandle expected in allocated)
        {
            Assert.True(restored.TryFindVirtualHandle(7, 2, expected.AcceleratorId, out Lane7VirtualHandle actual));
            Assert.Equal(expected, actual);
        }
    }
}
