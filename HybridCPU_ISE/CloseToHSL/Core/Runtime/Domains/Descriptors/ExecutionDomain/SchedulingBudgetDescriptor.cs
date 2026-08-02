namespace YAKSys_Hybrid_CPU.Core;

public enum SchedulingBudgetAuthority : byte
{
    Runtime = 0,
    CompatibilityProjection = 1,
}

public sealed partial class SchedulingBudgetDescriptor
{
    public SchedulingBudgetDescriptor()
        : this(
            authority: SchedulingBudgetAuthority.Runtime,
            maxOperationsPerEpoch: 0,
            requiresSystemSingletonLane: true,
            pinnedLaneId: VirtualizationLaneBindingPolicy.Lane7Id)
    {
    }

    public SchedulingBudgetDescriptor(
        SchedulingBudgetAuthority authority,
        ulong maxOperationsPerEpoch,
        bool requiresSystemSingletonLane,
        byte pinnedLaneId)
    {
        Authority = authority;
        MaxOperationsPerEpoch = maxOperationsPerEpoch;
        RequiresSystemSingletonLane = requiresSystemSingletonLane;
        PinnedLaneId = pinnedLaneId;
    }

    public SchedulingBudgetAuthority Authority { get; }

    public ulong MaxOperationsPerEpoch { get; }

    public bool RequiresSystemSingletonLane { get; }

    public byte PinnedLaneId { get; }

    public bool IsRuntimeAuthoritative =>
        Authority == SchedulingBudgetAuthority.Runtime;

    public bool IsUnbounded => MaxOperationsPerEpoch == 0;

    public bool HasFiniteBudget => MaxOperationsPerEpoch != 0;

    public bool AcceptsLane(byte laneId) =>
        !RequiresSystemSingletonLane ||
        laneId == PinnedLaneId;

    public SchedulingBudgetDescriptor WithMaxOperationsPerEpoch(ulong maxOperationsPerEpoch) =>
        new(Authority, maxOperationsPerEpoch, RequiresSystemSingletonLane, PinnedLaneId);
}
