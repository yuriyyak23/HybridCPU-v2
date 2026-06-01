using System;

namespace HybridCPU_ISE.Tests.Phase09;

internal readonly record struct LegacyReverseImportRequest(
    bool OriginatesFromLegacyVmx,
    bool DescriptorOwnerIdentified,
    bool CapabilityPolicyAdded,
    bool EvidencePolicyAdded,
    bool RetireBoundaryDefined,
    bool ProjectionTestsAdded,
    bool ContainsAuthoritativeVmxState);

internal static class LegacyIommuDomainBindingReturnContract
{
    public const string LegacyOriginPath =
        "Legacy/VMX/Substrate/Memory/Iommu/IOMMU.DomainBinding.partial.cs";

    public const string GenericHostPath =
        "Memory/MMU/IOMMU.DomainBinding.cs";

    public const string VmxCompatibilityAliasPath =
        "Core/VMX/Compatibility/Adapters/IO/IommuVmxCompatibilityAliases.cs";

    public static LegacyReverseImportRequest RequiredCoreReturnProof { get; } =
        new(
            OriginatesFromLegacyVmx: true,
            DescriptorOwnerIdentified: true,
            CapabilityPolicyAdded: true,
            EvidencePolicyAdded: true,
            RetireBoundaryDefined: true,
            ProjectionTestsAdded: true,
            ContainsAuthoritativeVmxState: false);

    public static bool RejectsVmxShapedHostMechanics => true;
}

internal static class LegacyVmcsMemoryTranslationProjectionRemovalContract
{
    public const string LegacyOriginPath =
        "Legacy/VMX/Substrate/Memory/Translation/LegacyVmcsMemoryTranslationControlProjection.cs";

    public const string QuarantinedFrontendPath =
        "Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs";

    public const string GenericControlPath =
        "Core/VMX/Compatibility/Frontend/Projection/Memory/MemoryTranslationControl.cs";

    public static bool RemovedWithoutReplacement => true;

    public static bool RejectsReturnedVmcsTranslationAuthority => true;
}

internal static class LegacyShadowVmcsBlockRemovalContract
{
    public const string LegacyOriginPath =
        "Legacy/VMX/Compatibility/Generated/VmcsProjection/ShadowVmcsBlock.cs";

    public const string NestedProjectionServicePath =
        "Core/VMX/Compatibility/Generated/VmcsProjection/ShadowVmcsNestedProjectionService.cs";

    public const string VmcsDescriptorPath =
        "NonRTL/Core/System/Vmcs/V2/VmcsV2Descriptor.cs";

    public const string VmcsManagerPath =
        "Legacy/VMX/Substrate/Runtime/Binding/VmcsManager.cs";

    public const string CheckpointPath =
        "NonRTL/Core/System/Migration/VmxCheckpointImage.cs";

    public static bool RemovedWithoutReplacement => true;

    public static bool RejectsShadowVmcsAuthorityReturn => true;
}

internal static class LegacyVmxV1ExecutionAdapterSurfaceReturnContract
{
    public const string LegacyDispatcherOriginPath =
        "Legacy/VMX/Compatibility/Adapters/LegacyVmxV1/ExecutionDispatcherV4.Vmx.cs";

    public const string CoreDispatcherPath =
        "Core/Execution/ExecutionDispatcherV4.VmxCompatibility.cs";

    public const string LegacyPipelineOriginPath =
        "Legacy/VMX/Compatibility/Adapters/LegacyVmxV1/CPU_Core.PipelineExecution.Vmx.cs";

    public const string CorePipelinePath =
        "Core/Pipeline/Core/CPU_Core.PipelineExecution.VmxRetire.cs";

    public static LegacyReverseImportRequest RequiredCoreReturnProof { get; } = new(
        OriginatesFromLegacyVmx: true,
        DescriptorOwnerIdentified: true,
        CapabilityPolicyAdded: true,
        EvidencePolicyAdded: true,
        RetireBoundaryDefined: true,
        ProjectionTestsAdded: true,
        ContainsAuthoritativeVmxState: false);

    public static bool RejectsVmcsManagerAuthority => true;

    public static bool RejectsRawVmcsFieldAuthority => true;

    public static bool RejectsIommuAuthority => true;
}

internal static class LegacyVmcsManagerVmxPublicationAuthorityRemovalContract
{
    private static readonly string[] RemovedSurfaceMarkerTable =
    {
        "RecordNestedTranslationExit",
        "TryResolveIntercept",
        "RecordInterceptExit",
        "RecordQualifiedVmExit",
        "TryQueueVirtualEvent",
        "TryDeliverVirtualEvent",
        "SnapshotDebugTraceCounters",
        "ResetDebugTraceCounters",
        "RecordVmxInvalidationForObservability",
        "RecordVmxFailForObservability",
        "RecordVmxAbortForObservability",
        "VmxEventKind.",
    };

    public const string RemovedManagerPath =
        "Legacy/VMX/Substrate/Runtime/Binding/VmcsManager.cs";

    public const string LegacyInterfacePath =
        "NonRTL/Core/System/IVmcsManager.cs";

    public static bool SliceRemovedWithoutReplacement => true;

    public static bool SupersededByFullManagerRemoval => true;

    public static bool RejectsVmExitAndInterceptPublication => true;

    public static bool RejectsStandaloneVirtualEventFrontendSurface => true;

    public static bool RejectsVmxObservabilitySurface => true;

    public static ReadOnlySpan<string> RemovedSurfaceMarkers => RemovedSurfaceMarkerTable;
}

internal static class LegacyVmcsManagerRemovalContract
{
    private static readonly string[] ProductionPathTable =
    {
        "Core/State/CPU_Core.StateData.cs",
        "Core/State/CPU_Core.MainMemoryBinding.cs",
        "Core/Pipeline/Core/CPU_Core.PipelineExecution.Memory.cs",
        "Core/Pipeline/Core/CPU_Core.PipelineExecution.Retire.cs",
        "Core/Pipeline/MicroOps/DmaStreamComputeMicroOp.cs",
        "Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs",
        "Memory/MMU/IOMMU.cs",
        "Core/VMX/Compatibility/Adapters/IO/IommuVmxCompatibilityAliases.cs",
        "NonRTL/Core/Execution/ExternalAccelerators/Commit/AcceleratorCommitModel.cs",
    };

    private static readonly string[] ForbiddenProductionOwnerMarkerTable =
    {
        "VmcsManager",
        "IVmcsManager",
        "core.Vmcs",
        "ActiveV2Descriptor",
        "HasActiveVmcs",
        "TryRouteVmxLaneCompletion",
        "TryMarkVmxDirtyRange",
        "TryMarkVectorStreamDirty",
        "VmxDirtyLogManager",
        "IVmxDirtyWriteSink",
        "VmcsV2Descriptor",
        "VmxDmaDescriptorValidator",
        "Lane7StateBlock",
        "LastVmxCompletionRouting",
    };

    public const string LegacyOriginPath =
        "Legacy/VMX/Substrate/Runtime/Binding/VmcsManager.cs";

    public const string LegacyInterfacePath =
        "NonRTL/Core/System/IVmcsManager.cs";

    public const string DirtyLogProjectionTypesPath =
        "NonRTL/Core/System/VmxDirtyLogProjectionTypes.cs";

    public const string Lane6Path =
        "Core/Pipeline/MicroOps/DmaStreamComputeMicroOp.cs";

    public const string Lane7Path =
        "Core/Pipeline/MicroOps/SystemDeviceCommandMicroOp.cs";

    public static bool RemovedWithoutReplacement => true;

    public static bool LiveCompatibilityLanePathsFailClosed => true;

    public static bool NativeLaneRuntimesRemainIndependent => true;

    public static bool RemovesVmcsOwnedDirtyTracking => true;

    public static ReadOnlySpan<string> ProductionPaths => ProductionPathTable;

    public static ReadOnlySpan<string> ForbiddenProductionOwnerMarkers =>
        ForbiddenProductionOwnerMarkerTable;
}
