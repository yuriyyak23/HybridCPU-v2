namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class DomainRuntimeSubstrateExtractionContract
{
    public const string OldDomainRuntimeContextPath =
        "Core/VMX/Substrate/Runtime/Services/DomainRuntimeContext.cs";

    public const string OldDomainRuntimeOperationPath =
        "Core/VMX/Substrate/Runtime/Services/DomainRuntimeOperation.cs";

    public const string OldDomainRuntimeAuthorityPath =
        "Core/VMX/Substrate/Runtime/Authority/DomainRuntimeAuthority.cs";

    public const string OldRootAuthorityDescriptorPath =
        "Core/VMX/Substrate/Runtime/Authority/RootAuthorityDescriptor.cs";

    public const string OldDomainLegalityServicePath =
        "Core/VMX/Substrate/Runtime/Legality/DomainLegalityService.cs";

    public const string OldDomainValidationResultPath =
        "Core/VMX/Substrate/Runtime/Validation/DomainValidationResult.cs";

    public const string OldDomainSchedulingAdmissionPath =
        "Core/VMX/Substrate/Runtime/Scheduling/DomainSchedulingAdmission.cs";

    public const string OldDomainBindingTablePath =
        "Core/VMX/Substrate/Runtime/Binding/DomainBindingTable.cs";

    public const string NewDomainRuntimeContextPath =
        "Core/Runtime/Domains/Services/DomainRuntimeContext.cs";

    public const string NewDomainRuntimeOperationPath =
        "Core/Runtime/Domains/Services/DomainRuntimeOperation.cs";

    public const string NewDomainRuntimeAuthorityPath =
        "Core/Runtime/Domains/Authority/DomainRuntimeAuthority.cs";

    public const string NewRootAuthorityDescriptorPath =
        "Core/Runtime/Domains/Authority/RootAuthorityDescriptor.cs";

    public const string NewDomainLegalityServicePath =
        "Core/Runtime/Domains/Legality/DomainLegalityService.cs";

    public const string NewDomainValidationResultPath =
        "Core/Runtime/Domains/Validation/DomainValidationResult.cs";

    public const string NewDomainSchedulingAdmissionPath =
        "Core/Runtime/Domains/Scheduling/DomainSchedulingAdmission.cs";

    public const string NewDomainBindingTablePath =
        "Core/Runtime/Domains/Binding/DomainBindingTable.cs";

    public const string RuntimeBoundaryAdmissionPath =
        "Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs";

    public const string FrozenMemoryTranslationControlPath =
        "Core/VMX/Compatibility/Frontend/Projection/Memory/MemoryTranslationControl.cs";

    public const string FrozenVmcsFieldProjectionPath =
        "Core/VMX/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs";

    public const string FrozenIotlbCompatibilityAliasPath =
        "Core/VMX/Compatibility/Adapters/IO/IommuVmxCompatibilityAliases.cs";

    public const string FrozenOpcodeVocabularyPath =
        "Core/Common/CPU_Core.Enums.cs";

    public static string[] RemovedDomainRuntimeSubstratePaths { get; } =
    {
        OldDomainRuntimeContextPath,
        OldDomainRuntimeOperationPath,
        OldDomainRuntimeAuthorityPath,
        OldRootAuthorityDescriptorPath,
        OldDomainLegalityServicePath,
        OldDomainValidationResultPath,
        OldDomainSchedulingAdmissionPath,
        OldDomainBindingTablePath,
    };

    public static string[] NeutralDomainRuntimePaths { get; } =
    {
        NewDomainRuntimeContextPath,
        NewDomainRuntimeOperationPath,
        NewDomainRuntimeAuthorityPath,
        NewRootAuthorityDescriptorPath,
        NewDomainLegalityServicePath,
        NewDomainValidationResultPath,
        NewDomainSchedulingAdmissionPath,
        NewDomainBindingTablePath,
    };

    public static string[] FrozenCompatibilityVocabularyPaths { get; } =
    {
        FrozenMemoryTranslationControlPath,
        FrozenVmcsFieldProjectionPath,
        FrozenIotlbCompatibilityAliasPath,
        FrozenOpcodeVocabularyPath,
    };

    public static string[] ForbiddenNeutralDomainRuntimeMarkers { get; } =
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
    };

    public static string[] RequiredNeutralDomainRuntimeMarkers { get; } =
    {
        "DomainRuntimeContext",
        "DomainRuntimeOperation",
        "DomainRuntimeAuthority",
        "RootAuthorityDescriptor",
        "DomainLegalityService",
        "DomainValidationResult",
        "DomainSchedulingAdmission",
        "DomainBindingTable",
    };

    public static string[] RequiredRuntimeAdmissionMarkers { get; } =
    {
        "DomainRuntimeAuthority",
        "RuntimeBoundaryAdmissionService",
        "CapabilityBoundaryRequirement",
        "EvidenceBoundaryRequirement",
        "FrontendAuthoritativeMutationDenied",
    };

    public static string[] RequiredFrozenCompatibilityMarkers { get; } =
    {
        "MemoryTranslationControl",
        "VmcsFieldProjectionSchema",
        "InvalidateVmxIotlbByVmid",
        "VMFUNC",
    };
}
