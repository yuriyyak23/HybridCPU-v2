namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class VmxCompletionTrapNestedRetireQuarantineContract
{
    public static string[] RemovedCompatibilitySubstratePaths { get; } =
    {
        "Core/VMX/Substrate/Completion/Projection/CompletionProjectionService.cs",
        "Core/VMX/Substrate/Completion/Records/CompletionRecord.cs",
        "Core/VMX/Substrate/Events/Intercepts/TrapDecision.cs",
        "Core/VMX/Substrate/Events/Intercepts/TrapPolicyBitmap.cs",
        "Core/VMX/Substrate/Events/Intercepts/TrapPolicyService.cs",
        "Core/VMX/Substrate/Events/Traps/DomainTrapRecord.cs",
        "Core/VMX/Substrate/Events/Traps/SchedulingBudgetTimer.cs",
        "Core/VMX/Substrate/Nested/CompletionMapping/NestedCompletionMapper.cs",
        "Core/VMX/Substrate/Nested/CompletionMapping/NestedExitMapper.cs",
        "Core/VMX/Substrate/Nested/MemoryComposition/NestedExitMapper.MemoryComposition.partial.cs",
        "Core/VMX/Substrate/Nested/TrapTranslation/NestedInterceptTranslator.cs",
        "Core/VMX/Substrate/Nested/TrapTranslation/NestedInterceptTranslator.Translate.partial.cs",
        "Core/VMX/Substrate/Runtime/RetireEvidence/DomainEpochTracker.cs",
        "Core/VMX/Substrate/Runtime/RetireEvidence/RetireEvidenceBoundary.cs",
    };

    public static string[] NeutralAuthorityPaths { get; } =
    {
        "Core/Runtime/Completion/Records/CompletionRecord.cs",
        "Core/Runtime/Completion/Routing/CompletionRoutingService.cs",
        "Core/Runtime/Domains/Descriptors/CompletionRoute/CompletionRouteDescriptor.cs",
        "Core/Runtime/Domains/Descriptors/TrapPolicy/TrapPolicyDescriptor.cs",
        "Core/Runtime/Events/Traps/DomainTrapRecord.cs",
        "Core/Runtime/Events/Traps/NeutralTrapResult.cs",
        "Core/Runtime/Events/Traps/SchedulingBudgetTimer.cs",
        "Core/Runtime/Events/Traps/TrapPolicyBitmap.cs",
        "Core/Runtime/Events/Traps/TrapRequest.cs",
        "Core/Runtime/Nested/Descriptors/NestedDomainDescriptor.cs",
        "Core/Runtime/Domains/Descriptors/EvidencePolicy/EvidencePolicyDescriptor.cs",
        "Core/Runtime/Domains/Descriptors/Observability/ObservabilityDescriptor.cs",
        "Core/Runtime/Evidence/HostOwned/HostOwnedEvidenceBoundary.cs",
    };

    public static string[] CompatibilityQuarantinePaths { get; } =
    {
        "Core/VMX/Compatibility/Frontend/Projection/Completion/CompletionProjectionService.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Completion/CompletionRecordCompatibilityProjection.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Events/TrapDecision.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Events/TrapPolicyService.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Events/VmxTrapProjectionMapper.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Nested/NestedCompletionMapper.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Nested/NestedExitMapper.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Nested/NestedExitMapper.MemoryComposition.partial.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Nested/NestedInterceptTranslator.cs",
        "Core/VMX/Compatibility/Frontend/Projection/Nested/NestedInterceptTranslator.Translate.partial.cs",
        "Core/VMX/Compatibility/Frontend/Retire/DomainEpochTracker.cs",
        "Core/VMX/Compatibility/Frontend/Retire/RetireEvidenceBoundary.cs",
        "Core/VMX/Compatibility/Generated/VmcsProjection/VmcsV2Blocks.cs",
        "NonRTL/Core/System/Vmcs/V2/VmcsV2Descriptor.cs",
    };

    public static string[] ForbiddenNeutralAuthorityMarkers { get; } =
    {
        "Vmcs",
        "VMCS",
        "Vmx",
        "VMX",
        "VmExitReason",
        "VmxExitQualification",
        "TrapDecision",
        "FromCompatibilityExit",
        "IsCompatibilityProjectionSource",
        "RecordNestedTranslationExit",
        "RecordInterceptExit",
        "RecordQualifiedExit",
    };

    public static string[] RequiredNeutralAuthorityMarkers { get; } =
    {
        "CompletionRouteDescriptor",
        "TrapPolicyDescriptor",
        "DomainTrapRecordAuthority.Runtime",
        "NeutralTrapResult",
        "TrapRequest",
        "NestedDomainDescriptor",
        "EvidencePolicyDescriptor",
        "ObservabilityDescriptor",
        "HostOwnedEvidenceBoundary",
        "IsRuntimeAuthoritative",
    };

    public static string[] ForbiddenVmcsProjectionHelperMarkers { get; } =
    {
        "public void RecordNestedTranslationExit(",
        "public void RecordNestedTranslationFault(",
        "public void RecordInterceptExit(",
        "public void RecordQualifiedExit(",
        "TryRouteCompletion(",
        "ConfigureRoute(",
        "DisableRoute(",
        "LaneCompletionRouter Router",
        "TrapPolicyBitmap Bitmap",
        "SchedulingBudgetTimer Timer",
    };

    public static string[] RequiredVmcsProjectionReadOnlyMarkers { get; } =
    {
        "IsReadOnlyCompatibilityProjection",
        "VMCSv2 scalar projection cache was removed",
    };

    public bool RemovesSubstrateCompatibilityAuthority => true;

    public bool KeepsVmxNamesAsCompatibilityProjectionOnly => true;

    public bool RequiresNeutralRuntimeAuthority => true;

    public bool DoesNotCreateRenamedRuntimeOwner => true;

    public bool IsSatisfied() =>
        RemovesSubstrateCompatibilityAuthority &&
        KeepsVmxNamesAsCompatibilityProjectionOnly &&
        RequiresNeutralRuntimeAuthority &&
        DoesNotCreateRenamedRuntimeOwner;
}
