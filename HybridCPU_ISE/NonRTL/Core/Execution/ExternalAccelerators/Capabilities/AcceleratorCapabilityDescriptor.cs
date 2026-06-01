using System;
using System.Collections.Generic;
using System.Linq;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

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
        AcceleratorCompatibilityMode compatibilityMode = AcceleratorCompatibilityMode.MetadataOnly,
        AcceleratorDescriptorTaxonomyKey? taxonomyKey = null,
        AcceleratorDescriptorTaxonomyStatus? taxonomyStatus = null)
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

        AcceleratorDescriptorTaxonomyKey effectiveTaxonomyKey =
            taxonomyKey ?? AcceleratorDescriptorTaxonomyCatalog.CurrentMatMul.Key;
        if (!AcceleratorDescriptorTaxonomyCatalog.TryGetEntry(
                effectiveTaxonomyKey,
                out AcceleratorDescriptorTaxonomyEntry taxonomyEntry))
        {
            throw new ArgumentException(
                "Accelerator capability descriptor references an unknown Lane7 descriptor taxonomy entry.",
                nameof(taxonomyKey));
        }

        AcceleratorDescriptorTaxonomyStatus effectiveTaxonomyStatus =
            taxonomyStatus ?? taxonomyEntry.Status;
        if (effectiveTaxonomyStatus != taxonomyEntry.Status)
        {
            throw new ArgumentException(
                "Accelerator capability descriptor taxonomy status does not match the catalog entry.",
                nameof(taxonomyStatus));
        }

        AcceleratorId = acceleratorId;
        DisplayName = displayName;
        CapabilityVersion = capabilityVersion;
        Operations = operations.ToArray();
        ResourceModel = resourceModel;
        AdoptionMode = adoptionMode;
        CompatibilityMode = compatibilityMode;
        TaxonomyKey = effectiveTaxonomyKey;
        TaxonomyStatus = effectiveTaxonomyStatus;
        TaxonomyEntry = taxonomyEntry;
    }

    public string AcceleratorId { get; }

    public string DisplayName { get; }

    public uint CapabilityVersion { get; }

    public IReadOnlyList<AcceleratorOperationCapability> Operations { get; }

    public AcceleratorResourceModel ResourceModel { get; }

    public AcceleratorCapabilityAdoptionMode AdoptionMode { get; }

    public AcceleratorCompatibilityMode CompatibilityMode { get; }

    public AcceleratorDescriptorTaxonomyKey TaxonomyKey { get; }

    public AcceleratorDescriptorTaxonomyStatus TaxonomyStatus { get; }

    public AcceleratorDescriptorTaxonomyEntry TaxonomyEntry { get; }

    public bool GrantsDescriptorAcceptanceAuthority => false;

    public bool GrantsTokenAuthority => false;

    public bool GrantsExecutionAuthority => false;

    public bool GrantsCommitAuthority => false;

    public bool GrantsCompilerEmissionAuthority => false;

    public bool GrantsTopologyQueryAuthority => false;

    public bool GrantsQueueOpenAuthority => false;

    public bool GrantsQueueBindAuthority => false;

    public bool GrantsQueueLifecycleAuthority => false;

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
