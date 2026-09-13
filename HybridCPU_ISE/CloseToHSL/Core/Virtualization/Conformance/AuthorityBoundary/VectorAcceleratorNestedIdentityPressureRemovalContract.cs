namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class VectorAcceleratorNestedIdentityPressureRemovalContract
{
    public const string RemovedVectorExceptionInfoPath =
        "NonRTL/Core/System/VmxVectorExceptionInfo.cs";

    public const string RemovedStreamDescriptorFaultInfoPath =
        "NonRTL/Core/System/VmxStreamDescriptorFaultInfo.cs";

    public const string NeutralVectorExceptionInfoPath =
        "NonRTL/Core/System/VectorStreamExceptionInfo.cs";

    public const string NeutralStreamDescriptorFaultInfoPath =
        "NonRTL/Core/System/VectorStreamDescriptorFaultInfo.cs";

    public const string VmcsBlocksPath =
        "Core/VMX/Compatibility/Generated/VmcsProjection/VmcsV2Blocks.cs";

    public const string VmcsDescriptorPath =
        "NonRTL/Core/System/Vmcs/V2/VmcsV2Descriptor.cs";

    public const string RemovedAcceleratorHandleMapPath =
        "NonRTL/Core/Execution/ExternalAccelerators/VmxAcceleratorHandleMap.cs";

    public const string RemovedAcceleratorTokenVirtualizerPath =
        "NonRTL/Core/Execution/ExternalAccelerators/VmxLane7TokenVirtualizer.cs";

    public const string RemovedAcceleratorCompletionRouterPath =
        "NonRTL/Core/Execution/ExternalAccelerators/VmxAcceleratorCompletionRouter.cs";

    public const string RemovedAcceleratorPolicyPath =
        "NonRTL/Core/Execution/ExternalAccelerators/VmxAcceleratorPolicy.cs";

    public const string NeutralAcceleratorHandleNamespacePath =
        "Core/Runtime/Lanes/Lane7/Accelerators/Lane7AcceleratorHandleNamespace.cs";

    public const string NeutralAcceleratorTokenNamespacePath =
        "Core/Runtime/Lanes/Lane7/Accelerators/Lane7AcceleratorTokenNamespace.cs";

    public const string NeutralAcceleratorCompletionRouterPath =
        "Core/Runtime/Lanes/Lane7/Accelerators/Lane7AcceleratorCompletionRouter.cs";

    public const string NeutralAcceleratorAdmissionPolicyPath =
        "Core/Runtime/Lanes/Lane7/Accelerators/Lane7AcceleratorAdmissionPolicy.cs";

    public const string Lane7StatePath =
        "Core/Runtime/Lanes/Lane7/Lane7StateBlock.cs";

    public const string ChildDomainIntentPath =
        "Core/VMX/Compatibility/Frontend/Projection/Nested/ChildDomainIntentDescriptor.cs";

    public const string NestedProjectionCheckpointOwnerPath =
        "Core/Runtime/Nested/NestedDomainProjectionCheckpointService.cs";

    public static string[] RemovedVectorIdentityPaths { get; } =
    {
        RemovedVectorExceptionInfoPath,
        RemovedStreamDescriptorFaultInfoPath,
    };

    public static string[] RemovedAcceleratorWrapperPaths { get; } =
    {
        RemovedAcceleratorHandleMapPath,
        RemovedAcceleratorTokenVirtualizerPath,
        RemovedAcceleratorCompletionRouterPath,
        RemovedAcceleratorPolicyPath,
    };

    public static string[] NeutralVectorIdentityPaths { get; } =
    {
        NeutralVectorExceptionInfoPath,
        NeutralStreamDescriptorFaultInfoPath,
    };

    public static string[] NeutralAcceleratorNamespacePaths { get; } =
    {
        NeutralAcceleratorHandleNamespacePath,
        NeutralAcceleratorTokenNamespacePath,
        NeutralAcceleratorCompletionRouterPath,
        NeutralAcceleratorAdmissionPolicyPath,
    };

    public static string[] ForbiddenVectorIdentityMarkers { get; } =
    {
        "VmxVectorExceptionInfo",
        "VmxStreamDescriptorFaultInfo",
        "Vmid",
        "Vpid",
        "vmid",
        "vpid",
        "CausesVmExit",
        "ReflectAsVmExit",
    };

    public static string[] ForbiddenAcceleratorNamespaceMarkers { get; } =
    {
        "VmxAcceleratorHandleMap",
        "VmxLane7TokenVirtualizer",
        "VmxAcceleratorCompletionRouter",
        "VmxAcceleratorPolicy",
        "ushort vmid",
        "vmid",
        "Vmid",
        "Vpid",
    };

    public static string[] ForbiddenNestedIntentMarkers { get; } =
    {
        "Vmcs12Pointer",
        "TryVmRead",
        "TryVmWrite",
        "VmcsField.Vpid",
        "VmcsField.",
        "ushort vmid",
        "ushort vpid",
    };

    public static string[] RequiredNeutralNestedIntentMarkers { get; } =
    {
        "ChildDomainIntentFieldIds.AddressSpaceTag",
        "SecondStageRootPointer",
        "SecondStageViolationQualification",
        "ChildIntentPointer",
    };

    public bool IsNeutralVectorExceptionIdentity(
        VectorStreamExceptionInfo info,
        ushort executionDomainTag,
        ushort addressSpaceTag) =>
        info.ExecutionDomainTag == executionDomainTag &&
        info.AddressSpaceTag == addressSpaceTag &&
        info.RequiresCompatibilityExitProjection;

    public bool IsNeutralStreamFaultIdentity(
        VectorStreamDescriptorFaultInfo info,
        ushort executionDomainTag,
        ushort addressSpaceTag) =>
        info.ExecutionDomainTag == executionDomainTag &&
        info.AddressSpaceTag == addressSpaceTag &&
        info.IsFaulted;

    public bool RejectsHostOwnedNestedRestore(
        NestedDomainProjectionCheckpointService service,
        NestedDomainProjectionCheckpointRequest request) =>
        !service.Validate(request).IsAllowed &&
        request.Checkpoint is not null &&
        request.Checkpoint.ContainsHostOwnedEvidence;
}
