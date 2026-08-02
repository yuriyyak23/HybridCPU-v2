using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;

public sealed class TopologyQueueMetadataCapabilityProvider : IAcceleratorCapabilityProvider
{
    public const string AcceleratorId = "topology.queue.metadata.v1";
    public const uint CapabilityVersion = 1;

    public IReadOnlyList<AcceleratorCapabilityDescriptor> GetCapabilities()
    {
        var shape = new AcceleratorShapeCapability(
            "topology-queue-map",
            minElements: 1,
            maxElements: 64,
            minRank: 1,
            maxRank: 2);

        var operation = new AcceleratorOperationCapability(
            "topology-queue-contract",
            new[] { "metadata" },
            new[] { shape });

        return new[]
        {
            new AcceleratorCapabilityDescriptor(
                AcceleratorId,
                "Topology/queue taxonomy metadata",
                CapabilityVersion,
                new[] { operation },
                new AcceleratorResourceModel(
                    baseLatencyCycles: 0,
                    cyclesPerElement: 0,
                    maxQueueOccupancy: 0,
                    scratchBytes: 0,
                    memoryBandwidthBytesPerCycle: 0),
                taxonomyKey: AcceleratorDescriptorTaxonomyCatalog.TopologyQueueMetadata.Key,
                taxonomyStatus: AcceleratorDescriptorTaxonomyStatus.MetadataOnly)
        };
    }
}
