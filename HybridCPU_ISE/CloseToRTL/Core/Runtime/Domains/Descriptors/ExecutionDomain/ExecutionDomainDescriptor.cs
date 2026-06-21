namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class ExecutionDomainDescriptor
{
    public ExecutionDomainDescriptor()
        : this(
            domainTag: 0,
            bundleLegality: null,
            schedulingBudget: null,
            extension: null,
            compatibilityProjectionEnabled: true,
            readOnlyState: null)
    {
    }

    public ExecutionDomainDescriptor(
        ulong domainTag,
        BundleLegalityDescriptor? bundleLegality,
        SchedulingBudgetDescriptor? schedulingBudget,
        ExecutionExtensionDescriptor? extension,
        bool compatibilityProjectionEnabled,
        ExecutionDomainReadOnlyStateView? readOnlyState = null)
    {
        DomainTag = domainTag;
        BundleLegality = bundleLegality;
        SchedulingBudget = schedulingBudget;
        Extension = extension;
        CompatibilityProjectionEnabled = compatibilityProjectionEnabled;
        ReadOnlyState = readOnlyState ?? ExecutionDomainReadOnlyStateView.Unmaterialized;
    }

    public ulong DomainTag { get; }

    public BundleLegalityDescriptor? BundleLegality { get; }

    public SchedulingBudgetDescriptor? SchedulingBudget { get; }

    public ExecutionExtensionDescriptor? Extension { get; }

    public bool CompatibilityProjectionEnabled { get; }

    public ExecutionDomainReadOnlyStateView ReadOnlyState { get; }

    public bool IsAuthoritativeExecutionStateOwner => true;

    public bool HasBundleLegality => BundleLegality is not null;

    public bool HasSchedulingBudget => SchedulingBudget is not null;

    public bool HasExecutionExtension => Extension is not null;

    public bool HasMaterializedGuestArchitecturalState =>
        ReadOnlyState.HasAnyMaterializedGuestArchitecturalState;

    public bool TryCreateReadOnlyStateView(
        out ExecutionDomainReadOnlyStateView view,
        out string reason)
    {
        if (!HasMaterializedGuestArchitecturalState)
        {
            view = default;
            reason = "Execution domain descriptor has no materialized read-only guest architectural state view.";
            return false;
        }

        view = ReadOnlyState;
        reason = "Execution domain descriptor exposed a read-only guest architectural state view.";
        return true;
    }

    public ExecutionDomainDescriptor WithReadOnlyState(
        ExecutionDomainReadOnlyStateView readOnlyState) =>
        new(DomainTag, BundleLegality, SchedulingBudget, Extension, CompatibilityProjectionEnabled, readOnlyState);

    public ExecutionDomainDescriptor WithCompatibilityProjection(bool enabled) =>
        new(DomainTag, BundleLegality, SchedulingBudget, Extension, enabled, ReadOnlyState);
}
