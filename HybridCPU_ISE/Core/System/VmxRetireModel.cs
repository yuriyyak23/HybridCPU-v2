namespace YAKSys_Hybrid_CPU.Core
{
    public enum VmxOperationKind : byte
    {
        None = 0,
        VmxOn = 1,
        VmxOff = 2,
        VmLaunch = 3,
        VmResume = 4,
        VmRead = 5,
        VmWrite = 6,
        VmClear = 7,
        VmPtrLd = 8,
    }

    public readonly struct VmxRetireEffect
    {
        private VmxRetireEffect(
            VmxOperationKind operation,
            bool isFaulted,
            VmExitReason failureReason,
            bool exitGuestContextOnRetire,
            bool hasRegisterDestination,
            ushort registerDestination,
            VmcsField vmcsField,
            long vmcsValue,
            ulong vmcsPointer)
        {
            Operation = operation;
            IsFaulted = isFaulted;
            FailureReason = failureReason;
            ExitGuestContextOnRetire = exitGuestContextOnRetire;
            HasRegisterDestination = hasRegisterDestination;
            RegisterDestination = registerDestination;
            VmcsField = vmcsField;
            VmcsValue = vmcsValue;
            VmcsPointer = vmcsPointer;
        }

        public VmxOperationKind Operation { get; }

        public bool IsFaulted { get; }

        public VmExitReason FailureReason { get; }

        public bool ExitGuestContextOnRetire { get; }

        public bool HasRegisterDestination { get; }

        public ushort RegisterDestination { get; }

        public VmcsField VmcsField { get; }

        public long VmcsValue { get; }

        public ulong VmcsPointer { get; }

        public bool IsValid => Operation != VmxOperationKind.None;

        public static VmxRetireEffect Fault(
            VmxOperationKind operation,
            VmExitReason failureReason = VmExitReason.None) =>
            new(
                operation,
                isFaulted: true,
                failureReason,
                exitGuestContextOnRetire: false,
                hasRegisterDestination: false,
                registerDestination: 0,
                vmcsField: default,
                vmcsValue: 0,
                vmcsPointer: 0);

        public static VmxRetireEffect Control(
            VmxOperationKind operation,
            bool exitGuestContextOnRetire = false) =>
            new(
                operation,
                isFaulted: false,
                VmExitReason.None,
                exitGuestContextOnRetire,
                hasRegisterDestination: false,
                registerDestination: 0,
                vmcsField: default,
                vmcsValue: 0,
                vmcsPointer: 0);

        public static VmxRetireEffect VmcsRead(
            VmcsField field,
            ushort destinationRegister,
            bool hasRegisterDestination) =>
            new(
                VmxOperationKind.VmRead,
                isFaulted: false,
                VmExitReason.None,
                exitGuestContextOnRetire: false,
                hasRegisterDestination,
                destinationRegister,
                field,
                vmcsValue: 0,
                vmcsPointer: 0);

        public static VmxRetireEffect VmcsWrite(
            VmcsField field,
            long value) =>
            new(
                VmxOperationKind.VmWrite,
                isFaulted: false,
                VmExitReason.None,
                exitGuestContextOnRetire: false,
                hasRegisterDestination: false,
                registerDestination: 0,
                field,
                value,
                vmcsPointer: 0);

        public static VmxRetireEffect VmcsPointerEffect(
            VmxOperationKind operation,
            ulong vmcsPointer) =>
            new(
                operation,
                isFaulted: false,
                VmExitReason.None,
                exitGuestContextOnRetire: false,
                hasRegisterDestination: false,
                registerDestination: 0,
                vmcsField: default,
                vmcsValue: 0,
                vmcsPointer);
    }

    public readonly record struct VmxRetireOutcome(
        bool Faulted,
        VmExitReason FailureReason,
        bool HasRegisterWriteback,
        ushort RegisterDestination,
        ulong RegisterWritebackValue,
        ulong? RedirectTargetPc,
        ulong? RestoredStackPointer,
        bool FlushesPipeline)
    {
        public bool RedirectsControlFlow => RedirectTargetPc.HasValue;

        public static VmxRetireOutcome Fault(VmExitReason failureReason = VmExitReason.None) =>
            new(
                Faulted: true,
                FailureReason: failureReason,
                HasRegisterWriteback: false,
                RegisterDestination: 0,
                RegisterWritebackValue: 0,
                RedirectTargetPc: null,
                RestoredStackPointer: null,
                FlushesPipeline: false);

        public static VmxRetireOutcome NoOp() =>
            new(
                Faulted: false,
                FailureReason: VmExitReason.None,
                HasRegisterWriteback: false,
                RegisterDestination: 0,
                RegisterWritebackValue: 0,
                RedirectTargetPc: null,
                RestoredStackPointer: null,
                FlushesPipeline: false);
    }
}
