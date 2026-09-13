namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class CapabilityRuntimeSubstrateExtractionContract
{
    public static string[] RemovedCapabilitySubstratePaths { get; } =
    {
        "Core/VMX/Substrate/Descriptors/Capability/CapabilityDescriptorSet.cs",
        "Core/VMX/Substrate/Capabilities/Grants/CapabilityGrant.cs",
        "Core/VMX/Substrate/Capabilities/Negotiation/CapabilityNegotiationService.cs",
        "Core/VMX/Substrate/Capabilities/Publication/CapabilityPublicationPolicy.cs",
    };

    public static string[] NeutralCapabilityRuntimePaths { get; } =
    {
        "Core/Runtime/Capabilities/Descriptors/CapabilityDescriptorSet.cs",
        "Core/Runtime/Capabilities/Grants/CapabilityGrant.cs",
        "Core/Runtime/Capabilities/Negotiation/CapabilityNegotiationService.cs",
        "Core/Runtime/Capabilities/Publication/CapabilityPublicationPolicy.cs",
    };

    public const string CompatibilityCapabilityProjectionPath =
        "Core/VMX/Compatibility/Frontend/Projection/CapabilityCompatibilityProjection.cs";

    public static string[] ForbiddenNeutralCapabilityRuntimeMarkers { get; } =
    {
        "Vmcs",
        "VMCS",
        "Vmx",
        "VMX",
        "CapabilityDescriptorSetSchema",
        "FromCompatibilityMasks(",
        "KnownVmxV2CompatibilityMask",
        "VmxCompatibility",
        "VmxCompatibilityBits",
        "VmxV2InstructionCaps",
        "PublishedVmxCaps",
        "PublishedCapabilityWord",
    };

    public static string[] RequiredNeutralCapabilityRuntimeMarkers { get; } =
    {
        "CapabilityGrantCollection",
        "CapabilityDescriptorSet",
        "CapabilityNegotiationService",
        "CapabilityPublicationPolicy",
        "TypedGrants",
    };

    public static string[] RequiredCompatibilityProjectionMarkers { get; } =
    {
        "CapabilityGrantCollection.FromCompatibilityMasks",
        "CapabilityDescriptorSetSchema.VmxCompatibility",
        "CapabilityDescriptorSetSchema.VmxCompatibilityBits",
        "CreateInternalGrant",
    };

    public static string[] RemovedProjectFolderMarkers { get; } =
    {
        "Core\\VMX\\Substrate\\Capabilities\\Grants\\",
        "Core\\VMX\\Substrate\\Capabilities\\Negotiation\\",
        "Core\\VMX\\Substrate\\Capabilities\\Publication\\",
        "Core\\VMX\\Substrate\\Descriptors\\Capability\\",
    };

    public bool MovesTypedCapabilityRuntimeOutOfVmxSubstrate => true;

    public bool KeepsMaskIngressCompatibilityProjectionOnly => true;

    public bool DoesNotCreateVmxCapabilityOwner => true;

    public bool IsSatisfied() =>
        MovesTypedCapabilityRuntimeOutOfVmxSubstrate &&
        KeepsMaskIngressCompatibilityProjectionOnly &&
        DoesNotCreateVmxCapabilityOwner;
}
