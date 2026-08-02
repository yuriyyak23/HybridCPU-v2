using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7bn defined AcceleratorDeviceId Lane-7 valid-input contract.</summary>
public sealed class Rf127bnAcceleratorDeviceIdLane7ValidInputContractTests
{
    public static IEnumerable<object[]> DefinedIds() => Enum.GetValues<AcceleratorDeviceId>()
        .Select(id => new object[] { id });

    [Theory]
    [MemberData(nameof(DefinedIds))]
    public void EveryDefinedNonzeroEnumMemberPreservesLane7HandleKeyParity(AcceleratorDeviceId acceleratorId)
    {
        var lane7 = new Lane7StateBlock();
        lane7.ConfigureOwnership(executionDomainTag: 7, addressSpaceTag: 3);

        Assert.True(lane7.TryAllocateVirtualHandle(
            ownerVirtualThreadId: 2,
            acceleratorId: acceleratorId,
            capabilities: Lane7VirtualCapability.QueryCaps,
            out Lane7VirtualHandle allocated,
            out Lane7Fault fault));
        Assert.Equal(Lane7Fault.None, fault);
        Assert.Equal(acceleratorId, allocated.AcceleratorId);
        Assert.True(lane7.TryFindVirtualHandle(7, 2, acceleratorId, out Lane7VirtualHandle found));
        Assert.Equal(allocated, found);
    }

    [Fact]
    public void ParserCapabilityNarrowingDoesNotChangeDefinedEnumRepresentation()
    {
        AcceleratorDeviceId[] values = Enum.GetValues<AcceleratorDeviceId>();

        Assert.Equal(6, values.Length);
        Assert.All(values, value => Assert.NotEqual((AcceleratorDeviceId)0, value));
        Assert.Contains(AcceleratorDeviceId.ReferenceMatMul, values);
        Assert.Contains(AcceleratorDeviceId.TensorMetadata, values);
        Assert.Contains(AcceleratorDeviceId.SparseGraphMetadata, values);
    }
}
