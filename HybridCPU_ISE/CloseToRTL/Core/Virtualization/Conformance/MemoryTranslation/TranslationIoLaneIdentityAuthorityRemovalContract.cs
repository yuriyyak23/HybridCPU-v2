using HybridCPU_ISE.CloseToRTL.Memory.MMU;
using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class TranslationIoLaneIdentityAuthorityRemovalContract
{
    public const string IommuDomainBindingPath =
        "Core/Runtime/IO/Dma/DmaDomainBinding.cs";

    public const string IotlbTagPath = "Memory/MMU/IotlbTag.cs";

    public const string DomainBoundIommuPath = "Memory/MMU/IOMMU.DomainBinding.cs";

    public const string IoHostBackendPath = "Memory/MMU/IoVirtualizationHostBackend.cs";

    public const string IoVirtualizationBlockPath =
        "Core/Runtime/Domains/Descriptors/IoDomain/IoVirtualizationBlock.cs";

    public const string IotlbInvalidationServicePath =
        "Core/Runtime/IO/Iotlb/IotlbInvalidationService.cs";

    public const string NestedTranslationResultPath =
        "Core/Runtime/Memory/Translation/NestedTranslationResult.cs";

    public const string CompatibilityTranslationControlPath =
        "Core/VMX/Compatibility/Frontend/Projection/Memory/MemoryTranslationControl.cs";

    public const string RemovedNestedProjectionComposerPath =
        "Core/VMX/Substrate/Nested/Projection/ComposedDomainProjectionComposer.cs";

    public const string Lane6StatePath =
        "Core/Runtime/Lanes/Lane6/Lane6StateBlock.cs";

    public const string Lane6QueueRuntimePath =
        "Core/Runtime/Lanes/Lane6/Lane6QueueRuntime.cs";

    public const string DmaValidationPath =
        "NonRTL/Core/Execution/DmaStreamCompute/VmxDmaDescriptorValidator.cs";

    public const string Lane7StatePath =
        "Core/Runtime/Lanes/Lane7/Lane7StateBlock.cs";

    public const string Lane7CheckpointPath =
        "Core/Runtime/Lanes/Lane7/Lane7StateBlock.Checkpoint.partial.cs";

    public const string LaneCompletionRoutingPath =
        "Core/Runtime/Completion/Routing/LaneCompletionRouting.cs";

    public const string CompatibilityIoAliasPath =
        "Core/VMX/Compatibility/Adapters/IO/IommuVmxCompatibilityAliases.cs";

    public static string[] IoIdentityPaths { get; } =
    {
        IommuDomainBindingPath,
        IotlbTagPath,
        DomainBoundIommuPath,
        IoHostBackendPath,
        IoVirtualizationBlockPath,
        IotlbInvalidationServicePath,
    };

    public static string[] LaneIdentityPaths { get; } =
    {
        Lane6StatePath,
        Lane6QueueRuntimePath,
        DmaValidationPath,
        Lane7StatePath,
        Lane7CheckpointPath,
        LaneCompletionRoutingPath,
    };

    public static string[] ForbiddenExecutableIdentityMarkers { get; } =
    {
        "Vmid",
        "Vpid",
        "VMID",
        "VPID",
    };

    public static string[] ForbiddenNestedResultMarkers { get; } =
    {
        "NptViolation",
        "NptMisconfiguration",
        "FromNptFault",
        "CausesVmExit",
    };

    public static string[] ForbiddenCompatibilityFactoryMarkers { get; } =
    {
        "CreateSecondStageControl",
        "CreateRuntimeProjection",
        "FromDomainControl",
        "ToCompatibilityControl",
    };

    public bool IsNeutralIotlbIdentity(
        IommuDomainBinding binding,
        IotlbTag tag) =>
        binding.IsValid &&
        tag.IoDomainTag == binding.IoDomainTag &&
        tag.DomainId == binding.DomainId &&
        tag.DeviceId == binding.DeviceId &&
        tag.DomainEpoch == binding.DomainEpoch;
}
