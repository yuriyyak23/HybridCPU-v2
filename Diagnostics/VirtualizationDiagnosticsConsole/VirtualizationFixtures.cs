using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;
using YAKSys_Hybrid_CPU.Core.Vmx;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.VirtualizationDiagnostics;

internal static class VirtualizationFixtures
{
    public static VmxMicroOp CreateVmCall(int ownerContextId = 42, ulong domainTag = 0)
    {
        var vmx = new VmxMicroOp
        {
            OpCode = IsaOpcodeValues.VMCALL,
            OwnerThreadId = 0,
            VirtualThreadId = 0,
            OwnerContextId = ownerContextId,
            Rd = 0,
            Rs1 = 0,
            Rs2 = 0,
            Instruction = new InstructionIR
            {
                CanonicalOpcode = new IsaOpcode(IsaOpcodeValues.VMCALL),
                Class = InstructionClass.Vmx,
                SerializationClass = SerializationClass.VmxSerial,
                Rd = 0,
                Rs1 = 0,
                Rs2 = 0,
                Imm = 0,
            },
        };
        if (domainTag != 0)
        {
            vmx.Placement = new SlotPlacementMetadata
            {
                RequiredSlotClass = SlotClass.SystemSingleton,
                PinningKind = SlotPinningKind.HardPinned,
                PinnedLaneId = 7,
                DomainTag = domainTag,
            };
        }
        vmx.RefreshWriteMetadata();
        return vmx;
    }

    public static ReplayPhaseContext ReplayPhase(ulong epoch) =>
        new(true, epoch, 0x4000UL + epoch * 8, 1, 0, 0, 0, ReplayPhaseInvalidationReason.None);

    public static SmtBundleMetadata4Way Bundle(
        int ownerContextId = 42,
        ulong domainTag = 0,
        int operationCount = 0) =>
        new(0, ownerContextId, domainTag, domainTag, domainTag, operationCount);

    public static DomainRuntimeContext RuntimeContext() =>
        new(
            execution: new ExecutionDomainDescriptor(),
            memory: new MemoryDomainDescriptor(),
            io: new IoDomainDescriptor(),
            capabilities: new CapabilityDescriptorSet(0, 0, 0),
            secureCompute: null,
            domainTag: 7,
            addressSpaceTag: 9);

    public static RootAuthorityDescriptor RootAuthority() =>
        new(RootAuthorityClass.RuntimeRoot, 1, 0, true, false);

    public static EvidencePolicyDescriptor ProjectionEvidencePolicy(bool aliases = true) =>
        new(aliases, allowGuestArchitecturalState: true, allowMigrationSerializableState: false);

    public static PrivilegedExecutionStateDescriptor PrivilegedDescriptor() =>
        new(
            DomainTag: 7,
            AddressSpaceTag: 9,
            PolicyEpoch: new PrivilegedExecutionStateEpoch(11),
            Materialized: true,
            GuestCr0: new PrivilegedControlRegisterValue(PrivilegedControlRegisterKind.GuestCr0, 0x80000011UL),
            GuestCr4: new PrivilegedControlRegisterValue(PrivilegedControlRegisterKind.GuestCr4, 0x00000620UL),
            LegalityPolicy: new PrivilegedControlRegisterLegalityPolicy(
                GuestCr0AllowedMask: 0xFFFF_FFFFUL,
                GuestCr0RequiredMask: 0x1,
                GuestCr4AllowedMask: 0xFFFF_FFFFUL,
                GuestCr4RequiredMask: 0x20,
                Materialized: true),
            EvidenceClass: PrivilegedExecutionStateEvidenceClass.GuestVisibleReadOnlyProjection,
            MigrationClass: PrivilegedExecutionStateMigrationClass.RevalidatedAfterRestore);

    public static VmxCompatibilityVmReadAdmissionRequest VmReadRequest(
        VmcsField field,
        PrivilegedExecutionStateDescriptor? descriptor,
        bool conformanceProven = true,
        bool projectionEvidenceValidated = true) =>
        new(
            Context: RuntimeContext(),
            RootAuthority: RootAuthority(),
            EvidencePolicy: ProjectionEvidencePolicy(),
            Descriptor: null,
            FieldId: (ushort)field,
            DestinationRegister: 3,
            FieldSelectorRegister: 1,
            ReservedRegister: 0,
            DescriptorValidated: true,
            CapabilityValidated: true,
            SchedulingValidated: true,
            NoEmissionValidated: true,
            ProjectionEvidenceValidated: projectionEvidenceValidated,
            PrivilegedExecutionState: descriptor,
            CurrentPrivilegedExecutionStateEpoch: new PrivilegedExecutionStateEpoch(11),
            PrivilegedExecutionStateConformanceProven: conformanceProven);

    public static VmxCompatibilityVmCallTrapAdmissionRequest VmCallRequest(
        bool projectionEvidenceValidated = true,
        bool allowAliases = true,
        bool enableTrap = true)
    {
        var bitmap = new TrapPolicyBitmap();
        if (enableTrap)
            bitmap.EnableVmxOperation(VmxOperationKind.VmCall);
        return new(
            Context: RuntimeContext(),
            RootAuthority: RootAuthority(),
            EvidencePolicy: ProjectionEvidencePolicy(allowAliases),
            TrapPolicy: new TrapPolicyDescriptor().WithEnabledClasses(TrapPolicyClass.CompatibilityOperation),
            TrapBitmap: bitmap,
            VtId: 1,
            HypercallLeafRegister: 2,
            DescriptorRegister: 3,
            ExecutionDomainTag: 4,
            AddressSpaceTag: 5,
            DescriptorValidated: true,
            CapabilityValidated: true,
            SchedulingValidated: true,
            NoEmissionValidated: true,
            ProjectionEvidenceValidated: projectionEvidenceValidated,
            DomainValidated: true);
    }
}
