namespace YAKSys_Hybrid_CPU.Core;

public readonly record struct ExecutionDomainReadOnlyStateView(
    ulong GuestPc,
    ulong GuestSp,
    ulong GuestFlags,
    bool HasMaterializedGuestPc,
    bool HasMaterializedGuestSp,
    bool HasMaterializedGuestFlags)
{
    public ulong StateEpoch { get; init; }

    public static ExecutionDomainReadOnlyStateView Unmaterialized { get; } =
        new(
            GuestPc: 0,
            GuestSp: 0,
            GuestFlags: 0,
            HasMaterializedGuestPc: false,
            HasMaterializedGuestSp: false,
            HasMaterializedGuestFlags: false)
        {
            StateEpoch = 0,
        };

    public static ExecutionDomainReadOnlyStateView FromGuestPcSpFlags(
        ulong guestPc,
        ulong guestSp,
        ulong guestFlags,
        ulong stateEpoch = 0) =>
        new(
            guestPc,
            guestSp,
            guestFlags,
            HasMaterializedGuestPc: true,
            HasMaterializedGuestSp: true,
            HasMaterializedGuestFlags: true)
        {
            StateEpoch = stateEpoch,
        };

    public bool HasAnyMaterializedGuestArchitecturalState =>
        HasMaterializedGuestPc ||
        HasMaterializedGuestSp ||
        HasMaterializedGuestFlags;

    public bool HasCompleteGuestPcSpFlags =>
        HasMaterializedGuestPc &&
        HasMaterializedGuestSp &&
        HasMaterializedGuestFlags;

    public bool IsMaterialized => HasAnyMaterializedGuestArchitecturalState;
}
