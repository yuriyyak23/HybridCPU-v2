namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Frozen read-only compatibility translation projection. It cannot produce
    /// memory-domain state, address-space identity, or invalidation epochs.
    /// </summary>
    public readonly record struct MemoryTranslationControl(
        bool NptEnabled,
        bool VpidEnabled,
        ulong GuestCr3,
        ulong NptRoot,
        ushort Vmid,
        ushort Vpid,
        ulong VmcsIdentity,
        ulong VmcsEpoch,
        byte DefaultMemoryType)
    {
        public const ulong EnableNptBit = 1UL << 1;
        public const ulong EnableVpidBit = 1UL << 5;
        public const byte WriteBackMemoryType = 6;

        public bool SecondStageTranslationEnabled => NptEnabled;

        public bool AddressSpaceTaggingEnabled => VpidEnabled;

        public ulong SecondStageRoot => NptRoot;

        public ushort AddressSpaceTag => Vpid;

        public ulong AddressSpaceRoot => GuestCr3;

        public ulong GuestAddressSpaceRoot => GuestCr3;

        public ulong CompatibilityProjectionIdentity => VmcsIdentity;

        public ulong AddressSpaceGeneration => VmcsEpoch;

        public bool HasCompatibilityProjectionIdentity => CompatibilityProjectionIdentity != 0;

        public bool IsReadOnlyCompatibilityProjection => true;

        public bool IsValid =>
            !SecondStageTranslationEnabled ||
            ((GuestAddressSpaceRoot & 0xFFFUL) == 0 &&
             (SecondStageRoot & 0xFFFUL) == 0 &&
             (!AddressSpaceTaggingEnabled || AddressSpaceTag != 0));

        public static MemoryTranslationControl Disabled { get; } =
            new(
                NptEnabled: false,
                VpidEnabled: false,
                GuestCr3: 0,
                NptRoot: 0,
                Vmid: 0,
                Vpid: 0,
                VmcsIdentity: 0,
                VmcsEpoch: 0,
                DefaultMemoryType: WriteBackMemoryType);

    }
}
