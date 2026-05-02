using System;
using System.Collections.Generic;
using System.Linq;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;

public enum AcceleratorCapabilityAdoptionMode
{
    MetadataOnly = 0
}

public enum AcceleratorCompatibilityMode
{
    MetadataOnly = 0
}

/// <summary>
/// External accelerator capability metadata. This descriptor is not decode,
/// submit, execution, or commit authority.
/// </summary>
public sealed class AcceleratorCapabilityDescriptor
{
    public AcceleratorCapabilityDescriptor(
        string acceleratorId,
        string displayName,
        uint capabilityVersion,
        IReadOnlyList<AcceleratorOperationCapability> operations,
        AcceleratorResourceModel resourceModel,
        AcceleratorCapabilityAdoptionMode adoptionMode = AcceleratorCapabilityAdoptionMode.MetadataOnly,
        AcceleratorCompatibilityMode compatibilityMode = AcceleratorCompatibilityMode.MetadataOnly)
    {
        if (string.IsNullOrWhiteSpace(acceleratorId))
        {
            throw new ArgumentException("Accelerator id is required.", nameof(acceleratorId));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(resourceModel);

        AcceleratorId = acceleratorId;
        DisplayName = displayName;
        CapabilityVersion = capabilityVersion;
        Operations = operations.ToArray();
        ResourceModel = resourceModel;
        AdoptionMode = adoptionMode;
        CompatibilityMode = compatibilityMode;
    }

    public string AcceleratorId { get; }

    public string DisplayName { get; }

    public uint CapabilityVersion { get; }

    public IReadOnlyList<AcceleratorOperationCapability> Operations { get; }

    public AcceleratorResourceModel ResourceModel { get; }

    public AcceleratorCapabilityAdoptionMode AdoptionMode { get; }

    public AcceleratorCompatibilityMode CompatibilityMode { get; }

    public bool TryGetOperation(
        string operationKind,
        out AcceleratorOperationCapability? operation)
    {
        operation = null;
        if (string.IsNullOrWhiteSpace(operationKind))
        {
            return false;
        }

        operation = Operations.FirstOrDefault(candidate =>
            string.Equals(
                candidate.OperationKind,
                operationKind,
                StringComparison.OrdinalIgnoreCase));

        return operation is not null;
    }
}
