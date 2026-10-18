using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcCapabilityRegistryTests
{
    [Fact]
    public void L7SdcCapabilityRegistry_RegisterProvider_StoresMetadataOnly()
    {
        AcceleratorCapabilityRegistry registry = CreateRegistry();

        Assert.Equal(1, registry.Count);
        Assert.True(registry.TryGetDescriptor("matmul.fixture.v1", out AcceleratorCapabilityDescriptor? descriptor));
        Assert.Equal("MatMul fixture metadata", descriptor!.DisplayName);
        Assert.Equal(1u, descriptor.CapabilityVersion);
        Assert.Equal(AcceleratorCapabilityAdoptionMode.MetadataOnly, descriptor.AdoptionMode);
        Assert.Equal(AcceleratorCompatibilityMode.MetadataOnly, descriptor.CompatibilityMode);

        Assert.True(descriptor.TryGetOperation("matmul", out AcceleratorOperationCapability? operation));
        Assert.True(operation!.SupportsDatatype("f32"));
        Assert.True(operation.SupportsDatatype("int32"));
        Assert.False(operation.SupportsDatatype("UINT32"));
        Assert.False(operation.SupportsDatatype("bf16"));
        Assert.True(operation.SupportsShape("matrix-2d", elementCount: 64, rank: 2));
        Assert.False(operation.SupportsShape("matrix-2d", elementCount: 0, rank: 2));
        Assert.False(operation.SupportsShape("vector-1d", elementCount: 64, rank: 1));

        Assert.Equal(266UL, descriptor.ResourceModel.EstimateLatencyCycles(64));
        Assert.Equal(2u, descriptor.ResourceModel.EstimateQueueOccupancy(8));
    }

    [Fact]
    public void L7SdcCapabilityRegistry_QuerySuccess_IsEvidenceOnly()
    {
        AcceleratorCapabilityRegistry registry = CreateRegistry();

        AcceleratorCapabilityQueryResult result = registry.Query("matmul.fixture.v1");

        Assert.True(result.IsMetadataAvailable);
        Assert.NotNull(result.Descriptor);
        Assert.False(result.GrantsDecodeAuthority);
        Assert.False(result.GrantsCommandSubmissionAuthority);
        Assert.False(result.GrantsExecutionAuthority);
        Assert.False(result.GrantsCommitAuthority);
    }

    [Fact]
    public void L7SdcCapabilityRegistry_UnknownAcceleratorId_Rejects()
    {
        AcceleratorCapabilityRegistry registry = CreateRegistry();

        AcceleratorCapabilityQueryResult result = registry.Query("fft.fixture.v1");

        Assert.True(result.IsRejected);
        Assert.Null(result.Descriptor);
        Assert.Contains("Unknown accelerator id", result.RejectReason);
    }

    [Fact]
    public void L7SdcCapabilityRegistry_UnknownAdoptionOrCompatibilityMode_Rejects()
    {
        AcceleratorCapabilityRegistry registry = CreateRegistry();

        AcceleratorCapabilityQueryResult adoptionReject = registry.Query(
            "matmul.fixture.v1",
            (AcceleratorCapabilityAdoptionMode)99,
            AcceleratorCompatibilityMode.MetadataOnly);
        Assert.True(adoptionReject.IsRejected);
        Assert.Contains("adoption mode", adoptionReject.RejectReason);

        AcceleratorCapabilityQueryResult compatibilityReject = registry.Query(
            "matmul.fixture.v1",
            AcceleratorCapabilityAdoptionMode.MetadataOnly,
            (AcceleratorCompatibilityMode)99);
        Assert.True(compatibilityReject.IsRejected);
        Assert.Contains("compatibility mode", compatibilityReject.RejectReason);
    }

    [Fact]
    public void L7SdcCapabilityRegistry_RegisterDescriptor_WithUnknownModes_Rejects()
    {
        var registry = new AcceleratorCapabilityRegistry();

        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            registry.RegisterDescriptor(CreateDescriptor(
                adoptionMode: (AcceleratorCapabilityAdoptionMode)99)));

        Assert.Contains("adoption mode", ex.Message);
    }

    [Fact]
    public void L7SdcCapabilityRegistry_DuplicateAcceleratorId_Rejects()
    {
        var registry = new AcceleratorCapabilityRegistry();
        registry.RegisterDescriptor(CreateDescriptor());

        Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterDescriptor(CreateDescriptor()));
    }

    internal static AcceleratorCapabilityRegistry CreateRegistry()
    {
        var registry = new AcceleratorCapabilityRegistry();
        registry.RegisterProvider(new MatMulCapabilityProvider());
        return registry;
    }

    internal static AcceleratorCapabilityDescriptor CreateDescriptor(
        AcceleratorCapabilityAdoptionMode adoptionMode =
            AcceleratorCapabilityAdoptionMode.MetadataOnly,
        AcceleratorCompatibilityMode compatibilityMode =
            AcceleratorCompatibilityMode.MetadataOnly)
    {
        var shape = new AcceleratorShapeCapability(
            "matrix-2d",
            minElements: 1,
            maxElements: 4096,
            minRank: 2,
            maxRank: 2);

        var operation = new AcceleratorOperationCapability(
            "matmul",
            new[] { "f32", "uint32" },
            new[] { shape });

        var resources = new AcceleratorResourceModel(
            baseLatencyCycles: 10,
            cyclesPerElement: 4,
            maxQueueOccupancy: 2,
            scratchBytes: 4096,
            memoryBandwidthBytesPerCycle: 64);

        return new AcceleratorCapabilityDescriptor(
            "matmul.fixture.v1",
            "MatMul fixture metadata",
            capabilityVersion: 1,
            operations: new[] { operation },
            resourceModel: resources,
            adoptionMode,
            compatibilityMode);
    }

    private sealed class StaticCapabilityProvider : IAcceleratorCapabilityProvider
    {
        private readonly IReadOnlyList<AcceleratorCapabilityDescriptor> _descriptors;

        public StaticCapabilityProvider(params AcceleratorCapabilityDescriptor[] descriptors)
        {
            _descriptors = descriptors.ToArray();
        }

        public IReadOnlyList<AcceleratorCapabilityDescriptor> GetCapabilities() => _descriptors;
    }
}
