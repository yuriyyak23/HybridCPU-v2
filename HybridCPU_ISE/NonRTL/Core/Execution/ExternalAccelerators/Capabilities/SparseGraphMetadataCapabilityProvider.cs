using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;

public sealed class SparseGraphMetadataCapabilityProvider : IAcceleratorCapabilityProvider
{
    public const string AcceleratorId = "sparse.graph.metadata.v1";
    public const uint CapabilityVersion = 1;

    public IReadOnlyList<AcceleratorCapabilityDescriptor> GetCapabilities()
    {
        var shape = new AcceleratorShapeCapability(
            "sparse-graph-csr",
            minElements: 1,
            maxElements: 1_048_576,
            minRank: 1,
            maxRank: 2);

        var operation = new AcceleratorOperationCapability(
            "sparse-graph-contract",
            new[] { "metadata" },
            new[] { shape });

        return new[]
        {
            new AcceleratorCapabilityDescriptor(
                AcceleratorId,
                "Sparse/Graph taxonomy metadata",
                CapabilityVersion,
                new[] { operation },
                new AcceleratorResourceModel(
                    baseLatencyCycles: 0,
                    cyclesPerElement: 0,
                    maxQueueOccupancy: 0,
                    scratchBytes: 0,
                    memoryBandwidthBytesPerCycle: 0),
                taxonomyKey: AcceleratorDescriptorTaxonomyCatalog.SparseGraphMetadata.Key,
                taxonomyStatus: AcceleratorDescriptorTaxonomyStatus.MetadataOnly)
        };
    }
}
