using System.Reflection;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7bs checkpoint re-entry rejects forged accelerator-device values.</summary>
public sealed class Rf127bsAcceleratorDeviceIdCheckpointInvalidInputBehaviorTests
{
    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)7)]
    [InlineData(ushort.MaxValue)]
    public void ForgedZeroAndUndefinedHandlesAreOmittedBeforeEitherOwnerIndex(ushort raw)
    {
        Lane7VirtualHandle forged = new(7, 2, 0x7000_0000_0000_0001UL, (AcceleratorDeviceId)raw,
            Lane7VirtualCapability.QueryCaps, 1);
        var restored = new Lane7StateBlock();
        restored.RestoreCheckpoint(CreateCheckpoint(forged));

        Assert.Equal(0, restored.ActiveHandleCount);
        Assert.False(restored.TryFindVirtualHandle(7, 2, (AcceleratorDeviceId)raw, out _));
        Assert.False(restored.TryGetVirtualHandle(forged.Value, out _));
    }

    private static Lane7Checkpoint CreateCheckpoint(Lane7VirtualHandle handle)
    {
        ConstructorInfo constructor = typeof(Lane7Checkpoint).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic).Single();
        return (Lane7Checkpoint)constructor.Invoke([
            (ushort)7, (ushort)3, true, new List<Lane7VirtualHandle> { handle },
            new List<Lane7VirtualToken>(), 1UL, 0UL, 0UL]);
    }
}
