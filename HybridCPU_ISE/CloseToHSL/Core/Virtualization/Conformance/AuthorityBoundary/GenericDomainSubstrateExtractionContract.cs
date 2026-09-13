namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class GenericDomainSubstrateExtractionContract
{
    public static string[] RemovedGenericDomainSubstratePaths { get; } =
    {
        "Core/VMX/Substrate/Descriptors/CompletionRoute/CompletionRouteDescriptor.cs",
        "Core/VMX/Substrate/Descriptors/EventQueue/EventQueueDescriptor.cs",
        "Core/VMX/Substrate/Descriptors/EvidencePolicy/EvidencePolicyDescriptor.cs",
        "Core/VMX/Substrate/Descriptors/ExecutionDomain/BundleLegalityDescriptor.cs",
        "Core/VMX/Substrate/Descriptors/ExecutionDomain/ExecutionDomainDescriptor.cs",
        "Core/VMX/Substrate/Descriptors/ExecutionDomain/ExecutionExtensionDescriptor.cs",
        "Core/VMX/Substrate/Descriptors/ExecutionDomain/SchedulingBudgetDescriptor.cs",
        "Core/VMX/Substrate/Descriptors/IoDomain/IoDomainDescriptor.cs",
        "Core/VMX/Substrate/Descriptors/IoDomain/IoVirtualizationBlock.cs",
        "Core/VMX/Substrate/Descriptors/Lane6Domain/Lane6DomainDescriptor.cs",
        "Core/VMX/Substrate/Descriptors/Lane7Accelerator/Lane7AcceleratorDescriptor.cs",
        "Core/VMX/Substrate/Descriptors/MemoryDomain/MemoryDomainDescriptor.cs",
        "Core/VMX/Substrate/Descriptors/Observability/ObservabilityDescriptor.cs",
        "Core/VMX/Substrate/Descriptors/TrapPolicy/TrapPolicyDescriptor.cs",
        "Core/VMX/Substrate/Domains/Execution/ExecutionDomainRuntime.cs",
        "Core/VMX/Substrate/Domains/IO/IoDomainRuntime.cs",
        "Core/VMX/Substrate/Domains/Lane6/Lane6DomainRuntime.cs",
        "Core/VMX/Substrate/Domains/Lane7/Lane7DomainRuntime.cs",
        "Core/VMX/Substrate/Domains/Memory/MemoryDomainRuntime.cs",
        "Core/VMX/Substrate/Domains/Nested/NestedDomainRuntime.cs",
        "Core/VMX/Substrate/Domains/VectorStream/VectorStreamDomainRuntime.cs",
        "Core/VMX/Substrate/Nested/Descriptors/NestedDomainDescriptor.cs",
        "Core/VMX/Substrate/Nested/Projection/INestedProjectionService.cs",
        "Core/VMX/Substrate/Nested/Projection/NestedProjectionService.cs",
        "Core/VMX/Substrate/Nested/Validation/NestedValidationResult.cs",
    };

    public static string[] NeutralRuntimePaths { get; } =
    {
        "Core/Runtime/Domains/Descriptors/CompletionRoute/CompletionRouteDescriptor.cs",
        "Core/Runtime/Domains/Descriptors/EventQueue/EventQueueDescriptor.cs",
        "Core/Runtime/Domains/Descriptors/EvidencePolicy/EvidencePolicyDescriptor.cs",
        "Core/Runtime/Domains/Descriptors/ExecutionDomain/BundleLegalityDescriptor.cs",
        "Core/Runtime/Domains/Descriptors/ExecutionDomain/ExecutionDomainDescriptor.cs",
        "Core/Runtime/Domains/Descriptors/ExecutionDomain/ExecutionExtensionDescriptor.cs",
        "Core/Runtime/Domains/Descriptors/ExecutionDomain/SchedulingBudgetDescriptor.cs",
        "Core/Runtime/Domains/Descriptors/IoDomain/IoDomainDescriptor.cs",
        "Core/Runtime/Domains/Descriptors/IoDomain/IoVirtualizationBlock.cs",
        "Core/Runtime/Domains/Descriptors/Lane6Domain/Lane6DomainDescriptor.cs",
        "Core/Runtime/Domains/Descriptors/Lane7Accelerator/Lane7AcceleratorDescriptor.cs",
        "Core/Runtime/Domains/Descriptors/MemoryDomain/MemoryDomainDescriptor.cs",
        "Core/Runtime/Domains/Descriptors/Observability/ObservabilityDescriptor.cs",
        "Core/Runtime/Domains/Descriptors/TrapPolicy/TrapPolicyDescriptor.cs",
        "Core/Runtime/Domains/Admission/Execution/ExecutionDomainRuntime.cs",
        "Core/Runtime/Domains/Admission/IO/IoDomainRuntime.cs",
        "Core/Runtime/Domains/Admission/Lane6/Lane6DomainRuntime.cs",
        "Core/Runtime/Domains/Admission/Lane7/Lane7DomainRuntime.cs",
        "Core/Runtime/Domains/Admission/Memory/MemoryDomainRuntime.cs",
        "Core/Runtime/Domains/Admission/Nested/NestedDomainRuntime.cs",
        "Core/Runtime/Domains/Admission/VectorStream/VectorStreamDomainRuntime.cs",
        "Core/Runtime/Nested/Descriptors/NestedDomainDescriptor.cs",
        "Core/Runtime/Nested/Projection/INestedProjectionService.cs",
        "Core/Runtime/Nested/Projection/NestedProjectionService.cs",
        "Core/Runtime/Nested/Validation/NestedValidationResult.cs",
    };

    public static string[] ForbiddenNeutralRuntimeMarkers { get; } =
    {
        "Vmcs",
        "Vmx",
        "VMCS",
        "VMX",
        "MemoryTranslationControl",
        "VmcsField",
        "INVVPID",
        "VMFUNC",
        "VmxRetireEffect",
        "IOMMU.VmxCompatibilityAliases",
        "CsrAddresses",
        "VmExit",
        "VmxCaps",
        "ShadowVmcs",
    };

    public static string[] RequiredNeutralRuntimeMarkers { get; } =
    {
        "ExecutionDomainDescriptor",
        "MemoryDomainDescriptor",
        "IoDomainDescriptor",
        "Lane6DomainDescriptor",
        "Lane7AcceleratorDescriptor",
        "NestedDomainDescriptor",
        "ExecutionDomainRuntime",
        "MemoryDomainRuntime",
        "IoDomainRuntime",
        "Lane6DomainRuntime",
        "Lane7DomainRuntime",
        "NestedDomainRuntime",
        "VectorStreamDomainRuntime",
        "NestedProjectionService",
        "NestedValidationResult",
    };

    public const string CapabilityDescriptorSetPath =
        "Core/Runtime/Capabilities/Descriptors/CapabilityDescriptorSet.cs";

    public const string CapabilityGeneratedSchemaPath =
        "Core/VMX/Compatibility/Generated/CapabilityProjection/CapabilityDescriptorSetSchema.cs";

    public static string[] FrozenCompatibilityProjectionPaths { get; } =
    {
        "Core/VMX/Compatibility/Frontend/Projection/Memory/MemoryTranslationControl.cs",
        "Core/VMX/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs",
        "Core/VMX/Compatibility/Adapters/IO/IommuVmxCompatibilityAliases.cs",
        "Core/VMX/Compatibility/Generated/CsrProjection/VmxCapsProjection.cs",
    };

    public static string[] RequiredFrozenCompatibilityMarkers { get; } =
    {
        "MemoryTranslationControl",
        "VmcsFieldProjectionSchema",
        "InvalidateVmxIotlbByVmid",
        "VmxCapsProjection",
    };
}
