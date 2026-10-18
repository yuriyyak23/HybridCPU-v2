using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

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
        VmPtrSt = 9,
        VmCall = 10,
        Invept = 11,
        Invvpid = 12,
        VmFunc = 13,
        VmSaveX = 14,
        VmRestX = 15,
        InterceptExit = 16,
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
            ulong vmcsPointer,
            VmxInvalidationScope invalidationScope = VmxInvalidationScope.None,
            ulong descriptorOperand = 0,
            VmxFunctionLeaf functionLeaf = VmxFunctionLeaf.None,
            VectorStreamSaveMask extendedStateMask = VectorStreamSaveMask.None,
            VmxExitQualification exitQualification = default,
            VmxCompletionKind completionKind = VmxCompletionKind.Success,
            VmxRootDescriptorReference rootDescriptor = default,
            VmExitReason exitReason = VmExitReason.None,
            TrapDecision interceptDecision = default,
            VmFailCode failCode = VmFailCode.None,
            VmAbortCode abortCode = VmAbortCode.None)
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
            InvalidationScope = invalidationScope;
            DescriptorOperand = descriptorOperand;
            FunctionLeaf = functionLeaf;
            ExtendedStateMask = extendedStateMask;
            ExitQualification = exitQualification;
            CompletionKind = completionKind;
            FailCode = failCode;
            AbortCode = abortCode;
            ExitReason = exitReason;
            InterceptDecision = interceptDecision;
            RootDescriptor = rootDescriptor.IsValid
                ? rootDescriptor
                : VmxRootDescriptorReference.CompatibilityDefault;
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

        public VmxInvalidationScope InvalidationScope { get; }

        public ulong DescriptorOperand { get; }

        public VmxFunctionLeaf FunctionLeaf { get; }

        public VectorStreamSaveMask ExtendedStateMask { get; }

        public VmxExitQualification ExitQualification { get; }

        public VmxCompletionKind CompletionKind { get; }

        public VmFailCode FailCode { get; }

        public VmAbortCode AbortCode { get; }

        public VmxRootDescriptorReference RootDescriptor { get; }

        public VmExitReason ExitReason { get; }

        public TrapDecision InterceptDecision { get; }

        public bool IsValid => Operation != VmxOperationKind.None;

        public static VmxRetireEffect Fault(
            VmxOperationKind operation,
            VmExitReason failureReason = VmExitReason.None,
            VmFailCode failCode = VmFailCode.None) =>
            new(
                operation,
                isFaulted: true,
                failureReason,
                exitGuestContextOnRetire: false,
                hasRegisterDestination: false,
                registerDestination: 0,
                vmcsField: default,
                vmcsValue: 0,
                vmcsPointer: 0,
                completionKind: VmxCompletionKind.VmFailInvalid,
                failCode: failCode);

        public static VmxRetireEffect Abort(
            VmxOperationKind operation,
            VmAbortCode abortCode) =>
            new(
                operation,
                isFaulted: true,
                VmExitReason.None,
                exitGuestContextOnRetire: false,
                hasRegisterDestination: false,
                registerDestination: 0,
                vmcsField: default,
                vmcsValue: 0,
                vmcsPointer: 0,
                completionKind: VmxCompletionKind.VmAbort,
                abortCode: abortCode);

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

        public static VmxRetireEffect VmxOnRootDescriptor(VmxRootDescriptorReference rootDescriptor) =>
            new(
                VmxOperationKind.VmxOn,
                isFaulted: false,
                VmExitReason.None,
                exitGuestContextOnRetire: false,
                hasRegisterDestination: false,
                registerDestination: 0,
                vmcsField: default,
                vmcsValue: 0,
                vmcsPointer: 0,
                rootDescriptor: rootDescriptor);

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

        public static VmxRetireEffect VmPtrSt(
            ushort destinationRegister,
            bool hasRegisterDestination) =>
            new(
                VmxOperationKind.VmPtrSt,
                isFaulted: false,
                VmExitReason.None,
                exitGuestContextOnRetire: false,
                hasRegisterDestination,
                destinationRegister,
                vmcsField: default,
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

        public static VmxRetireEffect VmCall(
            ushort leaf,
            ulong descriptor,
            bool exitGuestContextOnRetire) =>
            new(
                VmxOperationKind.VmCall,
                isFaulted: false,
                VmExitReason.None,
                exitGuestContextOnRetire,
                hasRegisterDestination: false,
                registerDestination: 0,
                vmcsField: default,
                vmcsValue: 0,
                vmcsPointer: 0,
                descriptorOperand: descriptor,
                exitQualification: new VmxExitQualification(leaf, VmxInvalidationScope.None, descriptor),
                completionKind: exitGuestContextOnRetire ? VmxCompletionKind.VmExit : VmxCompletionKind.Success);

        public static VmxRetireEffect Invalidation(
            VmxOperationKind operation,
            VmxInvalidationScope scope,
            ulong descriptor) =>
            new(
                operation,
                isFaulted: false,
                VmExitReason.None,
                exitGuestContextOnRetire: false,
                hasRegisterDestination: false,
                registerDestination: 0,
                vmcsField: default,
                vmcsValue: 0,
                vmcsPointer: 0,
                invalidationScope: scope,
                descriptorOperand: descriptor,
                exitQualification: new VmxExitQualification(0, scope, descriptor));

        public static VmxRetireEffect VmFunc(
            VmxFunctionLeaf leaf,
            ulong descriptor,
            ushort destinationRegister,
            bool hasRegisterDestination,
            bool exitGuestContextOnRetire,
            VmExitReason exitReason = VmExitReason.VmFunc) =>
            new(
                VmxOperationKind.VmFunc,
                isFaulted: false,
                VmExitReason.None,
                exitGuestContextOnRetire,
                hasRegisterDestination,
                destinationRegister,
                vmcsField: default,
                vmcsValue: 0,
                vmcsPointer: 0,
                descriptorOperand: descriptor,
                functionLeaf: leaf,
                exitQualification: new VmxExitQualification((ushort)leaf, VmxInvalidationScope.None, descriptor),
                completionKind: exitGuestContextOnRetire ? VmxCompletionKind.VmExit : VmxCompletionKind.Success,
                exitReason: exitGuestContextOnRetire ? exitReason : VmExitReason.None);

        public static VmxRetireEffect ExtendedState(
            VmxOperationKind operation,
            VectorStreamSaveMask mask,
            ulong descriptor) =>
            new(
                operation,
                isFaulted: false,
                VmExitReason.None,
                exitGuestContextOnRetire: false,
                hasRegisterDestination: false,
                registerDestination: 0,
                vmcsField: default,
                vmcsValue: 0,
                vmcsPointer: 0,
                descriptorOperand: descriptor,
                extendedStateMask: mask,
                exitQualification: new VmxExitQualification((ushort)mask, VmxInvalidationScope.None, descriptor));

        public static VmxRetireEffect InterceptExit(
            TrapDecision decision,
            TrapCompletionPublicationFenceResult publicationFence) =>
            publicationFence.RetirePublicationAllowed
                ? new(
                    VmxOperationKind.InterceptExit,
                    isFaulted: false,
                    VmExitReason.None,
                    exitGuestContextOnRetire: true,
                    hasRegisterDestination: false,
                    registerDestination: 0,
                    vmcsField: default,
                    vmcsValue: 0,
                    vmcsPointer: 0,
                    exitQualification: decision.ExitQualification,
                    completionKind: VmxCompletionKind.VmExit,
                    exitReason: decision.ExitReason,
                    interceptDecision: decision)
                : Fault(
                    VmxOperationKind.InterceptExit,
                    VmExitReason.SecurityPolicyViolation);
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
