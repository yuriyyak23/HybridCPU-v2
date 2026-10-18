namespace YAKSys_Hybrid_CPU.Core
{
    public enum VectorConfigOperationKind : byte
    {
        None = 0,
        Vsetvl = 1,
        Vsetvli = 2,
        Vsetivli = 3
    }

    /// <summary>
    /// Deferred retire payload for vector-config lane-7 carriers.
    /// Keeps VL/VTYPE architectural publication and optional rd writeback coupled on the
    /// authoritative WB path instead of mutating VectorConfig during Execute().
    /// </summary>
    public readonly struct VectorConfigRetireEffect
    {
        private VectorConfigRetireEffect(
            VectorConfigOperationKind operation,
            ulong actualVectorLength,
            ulong vtype,
            byte tailAgnostic,
            byte maskAgnostic,
            bool hasRegisterWriteback,
            ushort destinationRegister)
        {
            Operation = operation;
            ActualVectorLength = actualVectorLength;
            VType = vtype;
            TailAgnostic = tailAgnostic;
            MaskAgnostic = maskAgnostic;
            HasRegisterWriteback = hasRegisterWriteback;
            DestinationRegister = destinationRegister;
        }

        public VectorConfigOperationKind Operation { get; }

        public ulong ActualVectorLength { get; }

        public ulong VType { get; }

        public byte TailAgnostic { get; }

        public byte MaskAgnostic { get; }

        public bool HasRegisterWriteback { get; }

        public ushort DestinationRegister { get; }

        public bool IsValid => Operation != VectorConfigOperationKind.None;

        public static VectorConfigRetireEffect Create(
            VectorConfigOperationKind operation,
            ulong actualVectorLength,
            ulong vtype,
            byte tailAgnostic,
            byte maskAgnostic,
            bool hasRegisterWriteback,
            ushort destinationRegister) =>
            new(
                operation,
                actualVectorLength,
                vtype,
                tailAgnostic,
                maskAgnostic,
                hasRegisterWriteback,
                destinationRegister);
    }
}
