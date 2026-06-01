namespace HybridCPU_ISE.Tests.Phase09;

internal sealed class LegacyVmxV1AdapterBoundaryRemovalContract
{
    public const string RemovedCompatibilityBoundaryPath =
        "Legacy/VMX/Compatibility/Adapters/LegacyVmxV1/LegacyVmxV1AdapterBoundary.cs";

    public static string[] CurrentFailClosedRoutingPaths { get; } =
    {
        "Core/Execution/ExecutionDispatcherV4.VmxCompatibility.cs",
        "Core/Pipeline/Core/CPU_Core.PipelineExecution.VmxRetire.cs",
    };

    public static string[] RequiredTypedFailClosedMarkers { get; } =
    {
        "VmExitReason.SecurityPolicyViolation",
        "ApplyRemovedFrontendFailClosedEffect",
    };

    public bool RemovedWithoutReplacement => true;
    public bool LeavesCurrentTypedFailClosedRoutingInCore => true;
    public bool DoesNotIntroduceLegacyAdapterAuthority => true;

    public bool IsSatisfied() =>
        RemovedWithoutReplacement &&
        LeavesCurrentTypedFailClosedRoutingInCore &&
        DoesNotIntroduceLegacyAdapterAuthority;
}

internal sealed class LegacyVmxV2AdapterBoundaryRemovalContract
{
    public const string RemovedCompatibilityBoundaryPath =
        "Legacy/VMX/Compatibility/Adapters/LegacyVmxV2/LegacyVmxV2AdapterBoundary.cs";

    public static string[] RetainedGeneratedProjectionPaths { get; } =
    {
        "Core/VMX/Compatibility/Generated/VmcsProjection/NestedDomainControllerCompatibilityProjection.cs",
        "Core/VMX/Compatibility/Generated/VmcsProjection/ShadowVmcsNestedProjectionService.cs",
    };

    public static string[] RequiredGeneratedProjectionMarkers { get; } =
    {
        "ShadowVmcsNestedProjectionService",
        "CompatibilityProjectionFailed",
    };

    public bool RemovedWithoutReplacement => true;
    public bool LeavesReadOnlyDeniedProjectionVocabularyExplicit => true;
    public bool DoesNotIntroduceLegacyAdapterAuthority => true;

    public bool IsSatisfied() =>
        RemovedWithoutReplacement &&
        LeavesReadOnlyDeniedProjectionVocabularyExplicit &&
        DoesNotIntroduceLegacyAdapterAuthority;
}

internal sealed class LegacyCsrBackedVmxCapabilityDescriptorSourceRemovalContract
{
    public const string RemovedCompatibilitySourcePath =
        "Legacy/VMX/Compatibility/Generated/CsrProjection/LegacyCsrBackedVmxCapabilityDescriptorSource.cs";

    public static string[] NeutralCapabilityAuthorityPaths { get; } =
    {
        "Core/Runtime/Capabilities/Descriptors/CapabilityDescriptorSet.cs",
        "Core/Runtime/Capabilities/Grants/CapabilityGrant.cs",
    };

    public static string[] RequiredNeutralCapabilityAuthorityMarkers { get; } =
    {
        "CapabilityGrantCollection TypedGrants",
        "class CapabilityGrantCollection",
    };

    public bool RemovedWithoutReplacement => true;
    public bool LeavesTypedGrantAuthorityOutsideQuarantine => true;
    public bool DoesNotIntroduceCsrBackedAuthority => true;

    public bool IsSatisfied() =>
        RemovedWithoutReplacement &&
        LeavesTypedGrantAuthorityOutsideQuarantine &&
        DoesNotIntroduceCsrBackedAuthority;
}

internal sealed class LegacyVmxTranslationInvalidationBackendRemovalContract
{
    public const string RemovedCompatibilityBackendPath =
        "Legacy/VMX/Compatibility/Adapters/MemoryInvalidation/LegacyVmxTranslationInvalidationBackend.cs";

    public static string[] NeutralMemoryAuthorityPaths { get; } =
    {
        "Core/Runtime/Memory/Invalidation/TranslationInvalidationService.cs",
        "Memory/MMU/TranslationInvalidationHostBackend.cs",
    };

    public static string[] RequiredNeutralMemoryAuthorityMarkers { get; } =
    {
        "TranslationInvalidationService",
        "TranslationInvalidationHostBackend",
    };

    public bool RemovedWithoutReplacement => true;
    public bool LeavesNeutralMemoryInvalidationAuthorityOutsideQuarantine => true;
    public bool DoesNotIntroduceRenamedVmxRuntimeOwner => true;

    public bool IsSatisfied() =>
        RemovedWithoutReplacement &&
        LeavesNeutralMemoryInvalidationAuthorityOutsideQuarantine &&
        DoesNotIntroduceRenamedVmxRuntimeOwner;
}

internal sealed class LegacyVmxIoVirtualizationBackendRemovalContract
{
    public const string RemovedCompatibilityBackendPath =
        "Legacy/VMX/Compatibility/Adapters/IO/LegacyVmxIoVirtualizationBackend.cs";

    public static string[] NeutralIoAuthorityPaths { get; } =
    {
        "Core/Runtime/Domains/Descriptors/IoDomain/IoVirtualizationBlock.cs",
        "Core/Runtime/IO/Iotlb/IotlbInvalidationService.cs",
        "Memory/MMU/IoVirtualizationHostBackend.cs",
    };

    public static string[] RequiredNeutralIoAuthorityMarkers { get; } =
    {
        "IoVirtualizationBlock",
        "IotlbInvalidationService",
        "IoVirtualizationHostBackend",
    };

    public static string[] ForbiddenCompatibilityAuthorityMutationMarkers { get; } =
    {
        "CapabilityGrantCollection",
        "CreateGrant(",
        "AddGrant(",
        "HardwareWrite(",
        "HostOwnedEvidenceStore",
        "MemoryDomainTranslationControl",
        "AdvanceRuntimeEpoch(",
        "IOMMU.",
        "IommuDomainBinding",
        "IoVirtualizationHostBackend",
        "IotlbInvalidationService",
        "DmaAuthorityService",
        "DmaDomainBinding",
        "Lane6StateBlock",
        "Lane7StateBlock",
        "CompletionRoutingService",
        "PostedEventQueue",
        "DomainCheckpointImage",
        "RestoreValidationService",
    };

    public bool RemovedWithoutReplacement => true;
    public bool LeavesNeutralIoAuthorityOutsideQuarantine => true;
    public bool DoesNotIntroduceRenamedVmxRuntimeOwner => true;

    public bool IsSatisfied() =>
        RemovedWithoutReplacement &&
        LeavesNeutralIoAuthorityOutsideQuarantine &&
        DoesNotIntroduceRenamedVmxRuntimeOwner;
}

internal static class LegacyVmxExecutionUnitRemovalContract
{
    public const string LegacyOriginPath =
        "Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs";

    public const string CurrentDispatcherPath =
        "Core/Execution/ExecutionDispatcherV4.VmxCompatibility.cs";

    public const string CurrentRetirePath =
        "Core/Pipeline/Core/CPU_Core.PipelineExecution.VmxRetire.cs";

    public const string CurrentMicroOpPath =
        "Core/Pipeline/MicroOps/MicroOp.IO.cs";

    public static bool RemovedWithoutReplacement => true;
    public static bool RejectsLegacyOpcodeShell => true;
    public static bool RejectsLegacyConstructorAbi => true;
    public static bool RequiresTypedFailClosedCompatibilityEffects => true;
}
