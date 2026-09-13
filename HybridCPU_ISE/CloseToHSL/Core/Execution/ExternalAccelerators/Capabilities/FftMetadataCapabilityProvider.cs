using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;

public sealed class FftMetadataCapabilityProvider : IAcceleratorCapabilityProvider
{
    public const string AcceleratorId = "fft.metadata.v1";
    public const uint CapabilityVersion = 1;

    public IReadOnlyList<AcceleratorCapabilityDescriptor> GetCapabilities()
    {
        var shape = new AcceleratorShapeCapability(
            "fft-1d",
            minElements: 2,
            maxElements: 1_048_576,
            minRank: 1,
            maxRank: 1);

        var operation = new AcceleratorOperationCapability(
            "fft-contract",
            new[] { "metadata" },
            new[] { shape });

        return new[]
        {
            new AcceleratorCapabilityDescriptor(
                AcceleratorId,
                "FFT taxonomy metadata",
                CapabilityVersion,
                new[] { operation },
                new AcceleratorResourceModel(
                    baseLatencyCycles: 0,
                    cyclesPerElement: 0,
                    maxQueueOccupancy: 0,
                    scratchBytes: 0,
                    memoryBandwidthBytesPerCycle: 0),
                taxonomyKey: AcceleratorDescriptorTaxonomyCatalog.FftMetadata.Key,
                taxonomyStatus: AcceleratorDescriptorTaxonomyStatus.MetadataOnly)
        };
    }
}
