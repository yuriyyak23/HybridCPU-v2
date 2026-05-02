using Xunit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcMatMulCapabilityTests
{
    [Fact]
    public void L7SdcMatMulCapability_QueryAdvertisesMatMulWithoutAuthority()
    {
        var provider = new MatMulCapabilityProvider();
        var registry = new AcceleratorCapabilityRegistry();
        registry.RegisterProvider(provider);

        AcceleratorCapabilityQueryResult query =
            registry.Query(MatMulCapabilityProvider.AcceleratorId);

        Assert.True(query.IsMetadataAvailable, query.RejectReason);
        Assert.Equal("MatMul fixture metadata", query.Descriptor!.DisplayName);
        Assert.False(query.GrantsDecodeAuthority);
        Assert.False(query.GrantsCommandSubmissionAuthority);
        Assert.False(query.GrantsExecutionAuthority);
        Assert.False(query.GrantsCommitAuthority);
        Assert.True(query.Descriptor.TryGetOperation("matmul", out AcceleratorOperationCapability? operation));
        Assert.True(operation!.SupportsDatatype("f32"));
        Assert.True(operation.SupportsDatatype("f64"));
        Assert.True(operation.SupportsDatatype("int32"));
        Assert.False(operation.SupportsDatatype("uint32"));
        Assert.True(operation.SupportsShape("matrix-2d", elementCount: 4, rank: 2));
        Assert.False(operation.SupportsShape(
            "matrix-2d",
            MatMulDescriptorValidator.MaxOutputElements + 1,
            rank: 2));
    }
}
