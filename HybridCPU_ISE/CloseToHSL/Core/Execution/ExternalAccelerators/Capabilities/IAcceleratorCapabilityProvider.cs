using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;

/// <summary>
/// Provides external accelerator capability metadata only.
/// </summary>
public interface IAcceleratorCapabilityProvider
{
    IReadOnlyList<AcceleratorCapabilityDescriptor> GetCapabilities();
}
