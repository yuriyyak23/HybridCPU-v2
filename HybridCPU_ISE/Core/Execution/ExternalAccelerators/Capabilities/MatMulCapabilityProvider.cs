using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;

public sealed class MatMulCapabilityProvider : IAcceleratorCapabilityProvider
{
    public const string AcceleratorId = "matmul.fixture.v1";
    public const uint CapabilityVersion = 1;

    public IReadOnlyList<AcceleratorCapabilityDescriptor> GetCapabilities()
    {
        var shape = new AcceleratorShapeCapability(
            "matrix-2d",
            minElements: 1,
            maxElements: MatMulDescriptorValidator.MaxOutputElements,
            minRank: 2,
            maxRank: 2);

        var operation = new AcceleratorOperationCapability(
            "matmul",
            new[] { "f32", "f64", "int32" },
            new[] { shape });

        return new[]
        {
            new AcceleratorCapabilityDescriptor(
                AcceleratorId,
                "MatMul fixture metadata",
                CapabilityVersion,
                new[] { operation },
                MatMulResourceModel.Default.ToCapabilityResourceModel())
        };
    }

    public static bool Matches(AcceleratorCommandDescriptor descriptor)
    {
        return descriptor.AcceleratorClass == AcceleratorClassId.Matrix &&
               descriptor.AcceleratorId == AcceleratorDeviceId.ReferenceMatMul &&
               descriptor.Operation == AcceleratorOperationKind.MatMul &&
               descriptor.CapabilityVersion == CapabilityVersion;
    }
}
