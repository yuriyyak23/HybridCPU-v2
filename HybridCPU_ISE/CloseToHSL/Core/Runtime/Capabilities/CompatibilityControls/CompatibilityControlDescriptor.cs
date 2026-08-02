namespace YAKSys_Hybrid_CPU.Core;

public enum CompatibilityControlMaterializationState : byte
{
    NotMaterialized = 0,
    ReadOnlyProjectionAvailable = 1,
}

[System.Flags]
public enum CompatibilityEventRoutingPolicy : ulong
{
    None = 0,
    RuntimeTrapPolicyRequired = 1UL << 0,
    NeutralTrapResultRequired = 1UL << 1,
    PublicationFenceRequired = 1UL << 2,
}

[System.Flags]
public enum CompatibilityExecutionPolicy : ulong
{
    None = 0,
    RuntimeBoundaryAdmissionRequired = 1UL << 0,
    ReadProjectionOnly = 1UL << 1,
    WritesDenied = 1UL << 2,
    BackendExecutionDenied = 1UL << 3,
    AuthoritativeMutationDenied = 1UL << 4,
}

[System.Flags]
public enum CompatibilityExitPublicationPolicy : ulong
{
    None = 0,
    NeutralCompletionSourceRequired = 1UL << 0,
    PublicationFenceRequired = 1UL << 1,
    RetirePublicationRequiresNeutralPermit = 1UL << 2,
    PublicationDeniedUntilRouteMaterialized = 1UL << 3,
}

[System.Flags]
public enum CompatibilityEntryAdmissionPolicy : ulong
{
    None = 0,
    RootAuthorityRequired = 1UL << 0,
    DescriptorValidationRequired = 1UL << 1,
    CapabilityValidationRequired = 1UL << 2,
    SchedulingValidationRequired = 1UL << 3,
    NoEmissionValidationRequired = 1UL << 4,
    ProjectionEvidenceRequired = 1UL << 5,
}

[System.Flags]
public enum CompatibilitySecondaryExecutionPolicy : ulong
{
    None = 0,
    NestedIntentRequiresNeutralOwner = 1UL << 0,
    SecondStageTranslationRequiresMemoryOwner = 1UL << 1,
    AddressSpaceTagRequiresMemoryOwner = 1UL << 2,
    ControlValueProjectionDenied = 1UL << 3,
}

public readonly record struct CompatibilityControlReadOnlyView(
    bool IsMaterialized,
    CompatibilityEventRoutingPolicy EventRoutingPolicy,
    CompatibilityExecutionPolicy ExecutionPolicy,
    CompatibilityExitPublicationPolicy ExitPublicationPolicy,
    CompatibilityEntryAdmissionPolicy EntryAdmissionPolicy,
    CompatibilitySecondaryExecutionPolicy SecondaryExecutionPolicy)
{
    public static CompatibilityControlReadOnlyView FailClosedProjectionOnly { get; } =
        Create(
            CompatibilityEventRoutingPolicy.RuntimeTrapPolicyRequired |
                CompatibilityEventRoutingPolicy.NeutralTrapResultRequired |
                CompatibilityEventRoutingPolicy.PublicationFenceRequired,
            CompatibilityExecutionPolicy.RuntimeBoundaryAdmissionRequired |
                CompatibilityExecutionPolicy.ReadProjectionOnly |
                CompatibilityExecutionPolicy.WritesDenied |
                CompatibilityExecutionPolicy.BackendExecutionDenied |
                CompatibilityExecutionPolicy.AuthoritativeMutationDenied,
            CompatibilityExitPublicationPolicy.NeutralCompletionSourceRequired |
                CompatibilityExitPublicationPolicy.PublicationFenceRequired |
                CompatibilityExitPublicationPolicy.RetirePublicationRequiresNeutralPermit |
                CompatibilityExitPublicationPolicy.PublicationDeniedUntilRouteMaterialized,
            CompatibilityEntryAdmissionPolicy.RootAuthorityRequired |
                CompatibilityEntryAdmissionPolicy.DescriptorValidationRequired |
                CompatibilityEntryAdmissionPolicy.CapabilityValidationRequired |
                CompatibilityEntryAdmissionPolicy.SchedulingValidationRequired |
                CompatibilityEntryAdmissionPolicy.NoEmissionValidationRequired |
                CompatibilityEntryAdmissionPolicy.ProjectionEvidenceRequired,
            CompatibilitySecondaryExecutionPolicy.NestedIntentRequiresNeutralOwner |
                CompatibilitySecondaryExecutionPolicy.SecondStageTranslationRequiresMemoryOwner |
                CompatibilitySecondaryExecutionPolicy.AddressSpaceTagRequiresMemoryOwner |
                CompatibilitySecondaryExecutionPolicy.ControlValueProjectionDenied);

    public static CompatibilityControlReadOnlyView Create(
        CompatibilityEventRoutingPolicy eventRoutingPolicy,
        CompatibilityExecutionPolicy executionPolicy,
        CompatibilityExitPublicationPolicy exitPublicationPolicy,
        CompatibilityEntryAdmissionPolicy entryAdmissionPolicy,
        CompatibilitySecondaryExecutionPolicy secondaryExecutionPolicy) =>
        new(
            IsMaterialized: true,
            eventRoutingPolicy,
            executionPolicy,
            exitPublicationPolicy,
            entryAdmissionPolicy,
            secondaryExecutionPolicy);

    public bool IsSemanticallyComplete =>
        IsMaterialized &&
        EventRoutingPolicy != CompatibilityEventRoutingPolicy.None &&
        ExecutionPolicy != CompatibilityExecutionPolicy.None &&
        ExitPublicationPolicy != CompatibilityExitPublicationPolicy.None &&
        EntryAdmissionPolicy != CompatibilityEntryAdmissionPolicy.None &&
        SecondaryExecutionPolicy != CompatibilitySecondaryExecutionPolicy.None;

    public bool RequiresRuntimeBoundaryAdmission =>
        (ExecutionPolicy & CompatibilityExecutionPolicy.RuntimeBoundaryAdmissionRequired) != 0;

    public bool DeniesWrites =>
        (ExecutionPolicy & CompatibilityExecutionPolicy.WritesDenied) != 0;

    public bool DeniesBackendExecution =>
        (ExecutionPolicy & CompatibilityExecutionPolicy.BackendExecutionDenied) != 0;

    public bool DeniesAuthoritativeMutation =>
        (ExecutionPolicy & CompatibilityExecutionPolicy.AuthoritativeMutationDenied) != 0;

    public bool RequiresNeutralPublicationFence =>
        (ExitPublicationPolicy & CompatibilityExitPublicationPolicy.PublicationFenceRequired) != 0;

    public bool KeepsControlValuesUnprojected =>
        (SecondaryExecutionPolicy & CompatibilitySecondaryExecutionPolicy.ControlValueProjectionDenied) != 0;
}

public sealed partial class CompatibilityControlDescriptor
{
    public CompatibilityControlDescriptor()
        : this(
            CompatibilityControlMaterializationState.NotMaterialized,
            default)
    {
    }

    public CompatibilityControlDescriptor(
        CompatibilityControlMaterializationState materializationState,
        CompatibilityControlReadOnlyView readOnlyView)
    {
        MaterializationState = materializationState;
        ReadOnlyView = readOnlyView;
    }

    public static CompatibilityControlDescriptor NotMaterialized { get; } =
        new(
            CompatibilityControlMaterializationState.NotMaterialized,
            default);

    public static CompatibilityControlDescriptor FailClosedProjectionOnly { get; } =
        FromNeutralSemantics(CompatibilityControlReadOnlyView.FailClosedProjectionOnly);

    public CompatibilityControlMaterializationState MaterializationState { get; }

    public CompatibilityControlReadOnlyView ReadOnlyView { get; }

    public bool IsRuntimeAuthoritativeControlOwner => true;

    public bool HasMaterializedReadOnlyProjection =>
        MaterializationState == CompatibilityControlMaterializationState.ReadOnlyProjectionAvailable &&
        ReadOnlyView.IsSemanticallyComplete;

    public static CompatibilityControlDescriptor FromNeutralSemantics(
        CompatibilityControlReadOnlyView readOnlyView) =>
        new(
            readOnlyView.IsSemanticallyComplete
                ? CompatibilityControlMaterializationState.ReadOnlyProjectionAvailable
                : CompatibilityControlMaterializationState.NotMaterialized,
            readOnlyView);

    public bool TryCreateReadOnlyControlView(
        out CompatibilityControlReadOnlyView view,
        out string reason)
    {
        if (!HasMaterializedReadOnlyProjection)
        {
            view = default;
            reason = "Neutral compatibility control owner has no materialized read-only control view.";
            return false;
        }

        view = ReadOnlyView;
        reason = "Neutral compatibility control owner exposed a materialized read-only control view.";
        return true;
    }
}
