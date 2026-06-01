namespace HybridCPU_ISE.Tests.Phase09;

internal sealed class CapabilityProjectionPlacementServiceSubstrateExtractionContract
{
    public const string RemovedCapabilityGeneratedProjectionPath =
        "Core/VMX/Substrate/Capabilities/DescriptorSet/CapabilityDescriptorSetSchema.cs";

    public const string CapabilityGeneratedProjectionPath =
        "Core/VMX/Compatibility/Generated/CapabilityProjection/CapabilityDescriptorSetSchema.cs";

    public static string[] RemovedNeutralServiceSubstratePaths { get; } =
    {
        "Core/VMX/Substrate/Memory/Invalidation/TranslationInvalidationService.cs",
        "Core/VMX/Substrate/Lanes/Lane7/Lane7StateBlock.cs",
        "Core/VMX/Substrate/Lanes/VectorStream/VectorStreamSnapshot.Restore.partial.cs",
        "Core/VMX/Substrate/Memory/AddressSpaces/AddressSpaceId.cs",
        "Core/VMX/Substrate/Memory/AddressSpaces/AddressSpaceDescriptor.cs",
        "Core/VMX/Substrate/Memory/DirtyTracking/DirtyTrackingServiceDescriptor.cs",
        "Core/VMX/Substrate/Memory/Iommu/IommuDomainDescriptor.cs",
        "Core/VMX/Substrate/Memory/Translation/MemoryTranslationPolicy.cs",
        "Core/VMX/Substrate/Memory/Translation/NestedPageWalker.cs",
        "Core/VMX/Substrate/Memory/Translation/NestedPageWalker.Translate.partial.cs",
        "Core/VMX/Substrate/Memory/Translation/NestedTlbTag.cs",
        "Core/VMX/Substrate/Memory/Translation/NestedTranslationResult.cs",
        "Core/VMX/Substrate/Memory/Translation/TranslationViolationInfo.cs",
        "Core/VMX/Substrate/IO/Dma/DmaAuthorityService.cs",
        "Core/VMX/Substrate/IO/Dma/DmaDomainBinding.cs",
        "Core/VMX/Substrate/IO/Dma/DmaWindowDescriptor.cs",
        "Core/VMX/Substrate/IO/Iotlb/IotlbInvalidationService.cs",
        "Core/VMX/Substrate/Migration/Checkpoint/DomainCheckpointImage.cs",
        "Core/VMX/Substrate/Migration/Format/MigrationDescriptor.cs",
        "Core/VMX/Substrate/Migration/Restore/RestoreValidationService.cs",
        "Core/VMX/Substrate/Migration/Validation/MigrationValidationPolicy.cs",
        "Core/VMX/Substrate/Completion/Routing/CompletionRoutingService.cs",
        "Core/VMX/Substrate/Completion/Routing/LaneCompletionRouting.cs",
        "Core/VMX/Substrate/Events/Injection/EventDeliveryService.cs",
        "Core/VMX/Substrate/Events/Injection/EventInjectionDescriptor.cs",
        "Core/VMX/Substrate/Events/Injection/InterruptRemapPolicy.cs",
        "Core/VMX/Substrate/Events/Injection/PostedEventQueue.cs",
        "Core/VMX/Substrate/Events/Injection/PostedEventQueue.Snapshot.partial.cs",
        "Core/VMX/Substrate/Events/Injection/VirtualInterruptFabric.cs",
        "Core/VMX/Substrate/Events/Injection/VirtualInterruptFabric.Remap.partial.cs",
        "Core/VMX/Substrate/Lanes/Lane6/Lane6StateBlock.cs",
        "Core/VMX/Substrate/Lanes/Lane6/Lane6VirtualToken.Evidence.partial.cs",
        "Core/VMX/Substrate/Lanes/Lane6/Fences/FenceDomain.cs",
        "Core/VMX/Substrate/Lanes/Lane6/Queues/Lane6QueueNamespace.cs",
        "Core/VMX/Substrate/Lanes/Lane6/Tokens/Lane6TokenNamespace.cs",
    };

    public static string[] NeutralRuntimeServicePaths { get; } =
    {
        "Core/Runtime/Memory/Invalidation/TranslationInvalidationService.cs",
        "Core/Runtime/Lanes/Lane7/Lane7StateBlock.cs",
        "Core/Runtime/Lanes/VectorStream/SaveRestore/VectorStreamSnapshot.Restore.partial.cs",
        "Core/Runtime/Memory/AddressSpaces/AddressSpaceId.cs",
        "Core/Runtime/Memory/AddressSpaces/AddressSpaceDescriptor.cs",
        "Core/Runtime/Memory/DirtyTracking/DirtyTrackingServiceDescriptor.cs",
        "Core/Runtime/Memory/Iommu/IommuDomainDescriptor.cs",
        "Core/Runtime/Memory/Translation/MemoryTranslationPolicy.cs",
        "Core/Runtime/Memory/Translation/NestedPageWalker.cs",
        "Core/Runtime/Memory/Translation/NestedPageWalker.Translate.partial.cs",
        "Core/Runtime/Memory/Translation/NestedTlbTag.cs",
        "Core/Runtime/Memory/Translation/NestedTranslationResult.cs",
        "Core/Runtime/Memory/Translation/TranslationViolationInfo.cs",
        "Core/Runtime/IO/Dma/DmaAuthorityService.cs",
        "Core/Runtime/IO/Dma/DmaDomainBinding.cs",
        "Core/Runtime/IO/Dma/DmaWindowDescriptor.cs",
        "Core/Runtime/IO/Iotlb/IotlbInvalidationService.cs",
        "Core/Runtime/Migration/Checkpoint/DomainCheckpointImage.cs",
        "Core/Runtime/Migration/Format/MigrationDescriptor.cs",
        "Core/Runtime/Migration/Restore/RestoreValidationService.cs",
        "Core/Runtime/Migration/Validation/MigrationValidationPolicy.cs",
        "Core/Runtime/Completion/Routing/CompletionRoutingService.cs",
        "Core/Runtime/Completion/Routing/LaneCompletionRouting.cs",
        "Core/Runtime/Events/Injection/EventDeliveryService.cs",
        "Core/Runtime/Events/Injection/EventInjectionDescriptor.cs",
        "Core/Runtime/Events/Injection/InterruptRemapPolicy.cs",
        "Core/Runtime/Events/Injection/PostedEventQueue.cs",
        "Core/Runtime/Events/Injection/PostedEventQueue.Snapshot.partial.cs",
        "Core/Runtime/Events/Injection/VirtualInterruptFabric.cs",
        "Core/Runtime/Events/Injection/VirtualInterruptFabric.Remap.partial.cs",
        "Core/Runtime/Lanes/Lane6/Lane6StateBlock.cs",
        "Core/Runtime/Lanes/Lane6/Lane6VirtualToken.Evidence.partial.cs",
        "Core/Runtime/Lanes/Lane6/Fences/FenceDomain.cs",
        "Core/Runtime/Lanes/Lane6/Queues/Lane6QueueNamespace.cs",
        "Core/Runtime/Lanes/Lane6/Tokens/Lane6TokenNamespace.cs",
    };

    public static string[] ForbiddenNeutralRuntimeMarkers { get; } =
    {
        "Vmcs",
        "Vmx",
        "VMCS",
        "VMX",
        "VmExit",
        "VmxExit",
        "VmxFunction",
        "CsrAddresses",
        "MemoryTranslationControl",
        "LegacyVmx",
        "INVVPID",
        "VMFUNC",
        "ShadowVmcs",
    };

    public static string[] FrozenCompatibilityQuarantinePaths { get; } =
    {
        "Core/VMX/Compatibility/Frontend/Projection/Memory/MemoryTranslationControl.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Completion/CompletionProjectionService.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Events/TrapDecision.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Lanes/Lane7CheckpointVmcsEvidenceProjection.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Lanes/VectorStreamSnapshotVmcsEvidenceProjection.cs",
    };

    public static string[] RequiredCompatibilityQuarantineMarkers { get; } =
    {
        "MemoryTranslationControl",
        "VmxCompletionProjection",
        "VmxOperation",
        "Vmcs.V2.VmcsV2HostEvidenceKind",
        "Vmcs.V2.VmcsV2HostEvidenceKind",
    };

    public static string[] RemovedProjectFolderMarkers { get; } =
    {
        "Core\\VMX\\Substrate\\Capabilities\\DescriptorSet\\",
        "Core\\VMX\\Substrate\\Capabilities\\Grants\\",
        "Core\\VMX\\Substrate\\Capabilities\\Negotiation\\",
        "Core\\VMX\\Substrate\\Capabilities\\Publication\\",
        "Core\\VMX\\Substrate\\Completion\\Routing\\",
        "Core\\VMX\\Substrate\\Events\\Injection\\",
        "Core\\VMX\\Substrate\\IO\\Dma\\",
        "Core\\VMX\\Substrate\\IO\\Iotlb\\",
        "Core\\VMX\\Substrate\\Lanes\\Lane6\\",
        "Core\\VMX\\Substrate\\Memory\\AddressSpaces\\",
        "Core\\VMX\\Substrate\\Memory\\DirtyTracking\\",
        "Core\\VMX\\Substrate\\Memory\\Iommu\\",
        "Core\\VMX\\Substrate\\Migration\\",
    };

    public bool MovesGeneratedCapabilityProjectionOutOfSubstrate => true;

    public bool KeepsVmxShapedNamesCompatibilityOnly => true;

    public bool DoesNotCreateRenamedRuntimeOwner => true;

    public bool IsSatisfied() =>
        MovesGeneratedCapabilityProjectionOutOfSubstrate &&
        KeepsVmxShapedNamesCompatibilityOnly &&
        DoesNotCreateRenamedRuntimeOwner;
}

internal sealed class CoreVmxSubstrateResidualExtractionContract
{
    public static string[] RemovedNeutralResidualSubstratePaths { get; } =
    {
        "Core/VMX/Substrate/Memory/Translation/MemoryTranslationControl.cs",
        "Core/VMX/Substrate/Memory/Invalidation/TranslationInvalidationService.cs",
        "Core/VMX/Substrate/Lanes/VectorStream/VectorStreamSnapshot.Restore.partial.cs",
        "Core/VMX/Substrate/Lanes/Lane7/Lane7StateBlock.cs",
        "Core/VMX/Substrate/Lanes/Lane7/Lane7Checkpoint.Evidence.partial.cs",
        "Core/VMX/Substrate/Nested/Descriptors/ChildDomainIntentDescriptor.cs",
        "Core/VMX/Substrate/Nested/Policies/NestedDomainController.cs",
        "Core/VMX/Substrate/Evidence/HostOwned/HostOwnedEvidenceBoundary.cs",
        "Core/VMX/Substrate/Evidence/GuestVisible/GuestVisibleEvidenceProjection.cs",
        "Core/VMX/Substrate/Evidence/DebugTrace/DebugTraceExportPolicy.cs",
        "Core/VMX/Substrate/Lanes/VectorStream/State/VectorStreamState.cs",
        "Core/VMX/Substrate/Lanes/VectorStream/State/VectorStreamExecutionExtensionDescriptor.cs",
        "Core/VMX/Substrate/Lanes/VectorStream/SaveRestore/VectorStreamSaveRestoreProjection.cs",
        "Core/VMX/Substrate/Lanes/Lane7/Tokens/Lane7TokenNamespace.cs",
        "Core/VMX/Substrate/Lanes/Lane7/Handles/Lane7HandleNamespace.cs",
        "Core/VMX/Substrate/Lanes/Lane7/Handles/Lane7BackendBindingPolicy.cs",
        "Core/VMX/Substrate/Lanes/Lane7/Completion/Lane7CompletionPolicy.cs",
        "Core/VMX/Substrate/Lanes/Lane7/Lane7VirtualToken.Evidence.partial.cs",
        "Core/VMX/Substrate/Lanes/Lane7/Lane7StateBlock.Checkpoint.partial.cs",
        "Core/VMX/Substrate/Events/Traps/VirtualTimerState.cs",
        "Core/VMX/Substrate/Events/Traps/VirtualTimerState.Expiration.partial.cs",
        "Core/VMX/Substrate/Events/Traps/SchedulingBudgetTimer.Snapshot.partial.cs",
        "Core/VMX/Substrate/Events/Traps/DomainTrapRecord.cs",
        "Core/VMX/Substrate/Nested/Policies/NestedEvidencePolicy.cs",
        "Core/VMX/Substrate/Nested/CapabilityFilter/NestedCapabilityFilter.cs",
        "Core/VMX/Substrate/Nested/MemoryComposition/NestedMemoryDomainComposer.cs",
        "Core/VMX/Substrate/Nested/MemoryComposition/NestedMemoryCompositionService.cs",
    };

    public static string[] NeutralRuntimeResidualPaths { get; } =
    {
        "Core/Runtime/Memory/Translation/MemoryDomainTranslationControl.cs",
        "Core/Runtime/Memory/Invalidation/TranslationInvalidationService.cs",
        "Core/Runtime/Lanes/VectorStream/SaveRestore/VectorStreamSnapshot.Restore.partial.cs",
        "Core/Runtime/Lanes/Lane7/Lane7StateBlock.cs",
        "Core/Runtime/Lanes/Lane7/Lane7Checkpoint.Evidence.partial.cs",
        "Core/Runtime/Evidence/HostOwned/HostOwnedEvidenceBoundary.cs",
        "Core/Runtime/Evidence/GuestVisible/GuestVisibleEvidenceProjection.cs",
        "Core/Runtime/Evidence/DebugTrace/DebugTraceExportPolicy.cs",
        "Core/Runtime/Lanes/VectorStream/State/VectorStreamState.cs",
        "Core/Runtime/Lanes/VectorStream/State/VectorStreamExecutionExtensionDescriptor.cs",
        "Core/Runtime/Lanes/VectorStream/SaveRestore/VectorStreamSaveRestoreProjection.cs",
        "Core/Runtime/Lanes/Lane7/Tokens/Lane7TokenNamespace.cs",
        "Core/Runtime/Lanes/Lane7/Handles/Lane7HandleNamespace.cs",
        "Core/Runtime/Lanes/Lane7/Handles/Lane7BackendBindingPolicy.cs",
        "Core/Runtime/Lanes/Lane7/Completion/Lane7CompletionPolicy.cs",
        "Core/Runtime/Lanes/Lane7/Lane7VirtualToken.Evidence.partial.cs",
        "Core/Runtime/Lanes/Lane7/Lane7StateBlock.Checkpoint.partial.cs",
        "Core/Runtime/Events/Traps/VirtualTimerState.cs",
        "Core/Runtime/Events/Traps/VirtualTimerState.Expiration.partial.cs",
        "Core/Runtime/Events/Traps/SchedulingBudgetTimer.Snapshot.partial.cs",
        "Core/Runtime/Events/Traps/DomainTrapRecord.cs",
        "Core/Runtime/Nested/Policies/NestedEvidencePolicy.cs",
        "Core/Runtime/Nested/CapabilityFilter/NestedCapabilityFilter.cs",
        "Core/Runtime/Nested/MemoryComposition/NestedMemoryDomainComposer.cs",
        "Core/Runtime/Nested/MemoryComposition/NestedMemoryCompositionService.cs",
    };

    public static string[] ForbiddenNeutralRuntimeMarkers { get; } =
    {
        "Vmcs",
        "VMCS",
        "Vmx",
        "VMX",
        "VmExit",
        "VmxFunctionLeaf",
        "MemoryTranslationControl",
        "VmcsField",
        "INVVPID",
        "VMFUNC",
        "CsrAddresses",
        "ShadowVmcs",
    };

    public static string[] RequiredNeutralRuntimeMarkers { get; } =
    {
        "MemoryDomainTranslationControl",
        "TranslationInvalidationService",
        "VectorStreamSnapshot",
        "Lane7StateBlock",
        "ContainsNativeTokenHandle",
        "HostOwnedEvidenceBoundary",
        "GuestVisibleEvidenceProjection",
        "DebugTraceExportPolicy",
        "VectorStreamExecutionExtensionDescriptor",
        "VectorStreamSaveRestoreProjection",
        "Lane7TokenNamespace",
        "Lane7HandleNamespace",
        "Lane7CompletionPolicy",
        "VirtualTimerState",
        "DomainTrapRecord",
        "NestedCapabilityFilter",
        "NestedEvidencePolicy",
        "NestedMemoryDomainComposer",
        "NestedMemoryCompositionService",
    };

    public static string[] FrozenCompatibilityQuarantinePaths { get; } =
    {
        "Core/VMX/Compatibility/Frontend/Projection/Memory/MemoryTranslationControl.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Completion/CompletionProjectionService.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Completion/CompletionRecordCompatibilityProjection.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Events/TrapDecision.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Events/TrapPolicyService.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Lanes/VectorStreamSnapshotVmcsEvidenceProjection.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Lanes/Lane7CheckpointVmcsEvidenceProjection.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Nested/NestedCompletionMapper.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Nested/NestedExitMapper.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Nested/ChildDomainIntentDescriptor.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Nested/NestedExitMapper.MemoryComposition.partial.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Nested/NestedDomainController.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Nested/NestedInterceptTranslator.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Nested/NestedInterceptTranslator.Translate.partial.cs",
        "Core/VMX/Compatibility/Frontend/Retire/DomainEpochTracker.cs",
        "Core/VMX/Compatibility/Frontend/Retire/RetireEvidenceBoundary.cs",
    };

    public static string[] RequiredFrozenCompatibilityMarkers { get; } =
    {
        "MemoryTranslationControl",
        "VmxCompletionProjection",
        "FromCompatibilityExit",
        "VmxOperation",
        "VmExitReason.SecurityPolicyViolation",
        "Vmcs.V2.VmcsV2HostEvidenceKind",
        "Vmcs.V2.VmcsV2HostEvidenceKind",
        "NestedDomainDescriptor",
        "VmExitReason",
        "VmcsV2BlockDirectory",
        "VmExitReason",
        "VmxV2InstructionCaps.NestedVmx",
        "TrapPolicyClass.CompatibilityOperation",
        "VmExitReason.SecurityPolicyViolation",
        "VmxEventKind",
        "EvidencePolicyDescriptor",
    };

    public bool MovesNeutralResidualsToRuntime => true;

    public bool KeepsRemainingVmxSubstrateAsFrozenCompatibilityQuarantine => true;

    public bool DoesNotCreateRenamedVmxRuntimeOwner => true;

    public bool IsSatisfied() =>
        MovesNeutralResidualsToRuntime &&
        KeepsRemainingVmxSubstrateAsFrozenCompatibilityQuarantine &&
        DoesNotCreateRenamedVmxRuntimeOwner;
}

internal sealed class FinalFrozenAliasQuarantineContract
{
    public static string[] RemovedSubstrateAndHostAliasPaths { get; } =
    {
        "Core/VMX/Substrate/Memory/Translation/MemoryTranslationControl.cs",
        "Core/VMX/Substrate/Memory/Invalidation/TranslationInvalidationService.cs",
        "Core/VMX/Substrate/Lanes/Lane7/Lane7StateBlock.cs",
        "Core/VMX/Substrate/Lanes/Lane7/Lane7Checkpoint.Evidence.partial.cs",
        "Core/VMX/Substrate/Lanes/VectorStream/VectorStreamSnapshot.Restore.partial.cs",
        "Core/VMX/Substrate/Nested/Descriptors/ChildDomainIntentDescriptor.cs",
        "Core/VMX/Substrate/Nested/Policies/NestedDomainController.cs",
        "Memory/MMU/IOMMU.VmxCompatibilityAliases.cs",
        "Legacy/VMX/Compatibility/Adapters/IO/LegacyVmxIoVirtualizationBackend.cs",
        "Legacy/VMX/Compatibility/Adapters/MemoryInvalidation/LegacyVmxTranslationInvalidationBackend.cs",
    };

    public static string[] NeutralAuthorityPaths { get; } =
    {
        "Core/Runtime/Memory/Translation/MemoryDomainTranslationControl.cs",
        "Core/Runtime/Memory/Invalidation/TranslationInvalidationService.cs",
        "Core/Runtime/Lanes/Lane7/Lane7StateBlock.cs",
        "Core/Runtime/Lanes/Lane7/Lane7StateBlock.Checkpoint.partial.cs",
        "Core/Runtime/Lanes/Lane7/Lane7Checkpoint.Evidence.partial.cs",
        "Core/Runtime/Lanes/VectorStream/SaveRestore/VectorStreamSnapshot.Restore.partial.cs",
        "Core/Runtime/Lanes/VectorStream/SaveRestore/VectorStreamSaveRestoreProjection.cs",
    };

    public static string[] CompatibilityProjectionPaths { get; } =
    {
        "Core/VMX/Compatibility/Frontend/Projection/Memory/MemoryTranslationControl.cs",
        "Core/VMX/Compatibility/Adapters/IO/IommuVmxCompatibilityAliases.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Lanes/Lane7CheckpointVmcsEvidenceProjection.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Lanes/VectorStreamSnapshotVmcsEvidenceProjection.cs",
        "Core/VMX/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs",
        "Core/VMX/Compatibility/Generated/VmcsProjection/VmcsFieldAliasProjection.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Nested/ChildDomainIntentDescriptor.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Nested/NestedDomainController.cs",
    };

    public static string[] DeniedCompatibilityAdapterPaths { get; } =
    {
    };

    public static string[] ForbiddenNeutralAuthorityMarkers { get; } =
    {
        "Vmcs",
        "VMCS",
        "Vmx",
        "VMX",
        "VmExit",
        "VmxFunctionLeaf",
        "VMFUNC",
        "INVVPID",
        "Npt",
        "Vpid",
        "LegacyVmx",
        "MemoryTranslationControl",
    };

    public static string[] ForbiddenTranslationProjectionAuthorityMarkers { get; } =
    {
        "DomainControl",
        "AuthorityView",
        "ToAddressSpaceId(",
        "AdvanceRuntimeEpoch(",
        "TryAdvanceRuntimeEpoch",
    };

    public static string[] ForbiddenIotlbAliasExecutionMarkers { get; } =
    {
        "BindIoDomain(",
        "UnbindIoDomain(",
        "TryGetIoDomainBinding(",
        "TryTranslateDma(",
        "CountIotlbEntries(",
        "InvalidateIotlbAll(",
        "InvalidateIotlbByIoDomainTag(",
        "InvalidateIotlbByIoDomain(",
        "InvalidateIotlbByDevice(",
        "InvalidateIotlbByEpoch(",
        "ApplyTranslationInvalidation(",
    };

    public static string[] ForbiddenLane7RuntimeCompatibilityMarkers { get; } =
    {
        "VmFunc",
        "VMFUNC",
        "VmExit",
        "VmxFunctionLeaf",
        "EnabledVmFuncLeaves",
        "_enabledFastLeaves",
    };

    public bool KeepsCompatibilityAliasesReadOnlyDenied => true;

    public bool KeepsAuthorityInNeutralRuntimeDescriptors => true;

    public bool DoesNotIntroduceRenamedVmxRuntimeOwner => true;

    public bool IsSatisfied() =>
        KeepsCompatibilityAliasesReadOnlyDenied &&
        KeepsAuthorityInNeutralRuntimeDescriptors &&
        DoesNotIntroduceRenamedVmxRuntimeOwner;
}
