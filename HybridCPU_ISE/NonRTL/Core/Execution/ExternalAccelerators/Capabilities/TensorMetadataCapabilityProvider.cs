using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;

public sealed class TensorMetadataCapabilityProvider : IAcceleratorCapabilityProvider
{
    public const string AcceleratorId = "tensor.metadata.v1";
    public const uint CapabilityVersion = 1;

    public IReadOnlyList<AcceleratorCapabilityDescriptor> GetCapabilities()
    {
        var shape = new AcceleratorShapeCapability(
            "tensor-nd",
            minElements: 1,
            maxElements: 1_048_576,
            minRank: 1,
            maxRank: 8);

        var operation = new AcceleratorOperationCapability(
            "tensor-contract",
            new[] { "f32", "f64", "int32" },
            new[] { shape });

        return new[]
        {
            new AcceleratorCapabilityDescriptor(
                AcceleratorId,
                "Tensor taxonomy metadata",
                CapabilityVersion,
                new[] { operation },
                new AcceleratorResourceModel(
                    baseLatencyCycles: 0,
                    cyclesPerElement: 0,
                    maxQueueOccupancy: 0,
                    scratchBytes: 0,
                    memoryBandwidthBytesPerCycle: 0),
                taxonomyKey: AcceleratorDescriptorTaxonomyCatalog.TensorMetadata.Key,
                taxonomyStatus: AcceleratorDescriptorTaxonomyStatus.MetadataOnly)
        };
    }
}
