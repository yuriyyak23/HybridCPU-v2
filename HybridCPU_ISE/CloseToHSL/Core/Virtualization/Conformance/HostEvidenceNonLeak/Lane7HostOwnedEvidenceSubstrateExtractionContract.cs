namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class Lane7HostOwnedEvidenceSubstrateExtractionContract
{
    public const string Lane7StatePath =
        "Core/Runtime/Lanes/Lane7/Lane7StateBlock.cs";

    public const string Lane7CheckpointPath =
        "Core/Runtime/Lanes/Lane7/Lane7StateBlock.Checkpoint.partial.cs";

    public const string Lane7CheckpointEvidencePath =
        "Core/Runtime/Lanes/Lane7/Lane7Checkpoint.Evidence.partial.cs";

    public const string Lane7VirtualTokenEvidencePath =
        "Core/Runtime/Lanes/Lane7/Lane7VirtualToken.Evidence.partial.cs";

    public const string NeutralHostEvidenceStorePath =
        "Core/Runtime/Lanes/Lane7/HostOwnedEvidence/Lane7HostOwnedEvidenceStore.cs";

    public const string FrozenMemoryTranslationControlPath =
        "Core/VMX/Compatibility/Frontend/Projection/Memory/MemoryTranslationControl.cs";

    public const string FrozenVmcsFieldProjectionPath =
        "Core/VMX/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs";

    public const string FrozenIotlbCompatibilityAliasPath =
        "Core/VMX/Compatibility/Adapters/IO/IommuVmxCompatibilityAliases.cs";

    public static bool ExtractsLane7HostEvidenceToNeutralRuntimeNamespace => true;

    public static bool RequiresTokenBackendAndSchedulerEvidenceRebuildAfterRestore => true;

    public static bool KeepsVmxTranslationVmcsAndIotlbNamesFrozenCompatibilityOnly => true;

    public static string[] ForbiddenLane7StateCacheMarkers { get; } =
    {
        "_hostTokenByVirtualValue",
        "_virtualTokenByHostHandle",
        "_backendBindings",
        "_submitPollCount",
        "public ulong BackendBindingEpoch { get; private set; }",
        "public ulong PressureEpoch { get; private set; }",
        "public Lane7PressureSnapshot LastPressure { get; private set; }",
    };

    public static string[] RequiredLane7StateDelegationMarkers { get; } =
    {
        "public Lane7HostOwnedEvidenceStore HostEvidence",
        "BackendBindingEpoch => HostEvidence.BackendBindingEpoch",
        "PressureEpoch => HostEvidence.PressureEpoch",
        "LastPressure => HostEvidence.LastPressure",
        "PrepareHostEvidenceForRestore",
        "RebuildHostTokenAfterRestore",
        "RebuildBackendBindingAfterRestore",
    };

    public static string[] ForbiddenNeutralEvidenceStoreMarkers { get; } =
    {
        "Vmcs",
        "Vmx",
        "VMCS",
        "VMX",
        "MemoryTranslationControl",
        "VmcsField",
        "INVVPID",
        "VMFUNC",
        "VmxFunctionLeaf",
        "Serialize",
        "RestoreHostHandle",
        "ImportHost",
    };

    public static string[] RequiredNeutralEvidenceStoreMarkers { get; } =
    {
        "EvidenceVisibilityClass.NativeTokenEvidence",
        "EvidenceVisibilityClass.BackendBindingEvidence",
        "EvidenceVisibilityClass.SchedulerEvidence",
        "EvidenceRestorePolicy.RecomputeAfterRestore",
        "HostOwnedEvidenceBoundary",
        "_nativeTokenByVirtualToken.Clear()",
        "_backendBindings.Clear()",
        "ResetSchedulerEvidence",
        "RebuildTokenAfterRestore",
        "RebuildBackendAfterRestore",
        "TryAdvanceEpoch",
        "epoch == ulong.MaxValue",
    };

    public static string[] ForbiddenCheckpointEvidenceLeakMarkers { get; } =
    {
        "ExposesHostTokenHandle(hostHandle)",
        "hostHandle.Value",
        "_hostTokenByVirtualValue",
        "_virtualTokenByHostHandle",
        "_backendBindings",
    };

    public static string[] RequiredFrozenCompatibilityMarkers { get; } =
    {
        "MemoryTranslationControl",
        "VmcsFieldProjectionSchema",
        "InvalidateVmxIotlbByVmid",
    };

    public bool RejectsCheckpointNativeHandleProjection(
        Lane7Checkpoint checkpoint,
        Execution.ExternalAccelerators.Tokens.AcceleratorTokenHandle hostHandle) =>
        !checkpoint.ContainsNativeTokenHandle(hostHandle);
}
