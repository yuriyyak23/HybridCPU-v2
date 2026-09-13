using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests;

internal sealed class NestedDomainProjectionCheckpointOwnerContract
{
    public const string NeutralOwnerPath =
        "Core/Runtime/Nested/NestedDomainProjectionCheckpointService.cs";

    public const string DescriptorPath =
        "Core/Runtime/Nested/Descriptors/NestedDomainDescriptor.cs";

    public const string DomainCheckpointImagePath =
        "Core/Runtime/Migration/Checkpoint/DomainCheckpointImage.cs";

    public const string CompatibilityProjectionPath =
        "Core/VMX/Compatibility/Generated/VmcsProjection/ShadowVmcsNestedProjectionService.cs";

    public static string[] ForbiddenNeutralOwnerMarkers { get; } =
    {
        "Vmcs",
        "Vmx",
        "VMCS",
        "VMX",
        "Shadow",
        "ReadFieldValue",
        "WriteFieldValue",
        "ActiveV2Descriptor",
        "DmaStreamComputeTokenHandle",
    };

    public static string[] RequiredNeutralOwnerMarkers { get; } =
    {
        "NestedProjectionService",
        "RestoreValidationService",
        "DomainCheckpointImage",
        "MigrationValidationPolicy",
        "EvidenceRestorePolicy",
        "ExpectedCheckpointEpoch",
    };

    public static string[] RequiredCompatibilityDenyMarkers { get; } =
    {
        "CompatibilityProjectionFailed",
        "removed without replacement",
        "cannot bypass the neutral nested projection/checkpoint service",
    };

    public bool RejectsHostOwnedCheckpoint(
        NestedDomainProjectionCheckpointService service,
        NestedDomainProjectionCheckpointRequest request) =>
        !service.Validate(request).IsAllowed &&
        request.Checkpoint is not null &&
        request.Checkpoint.ContainsHostOwnedEvidence;
}
