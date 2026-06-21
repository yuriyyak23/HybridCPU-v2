using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Arch
{
    /// <summary>
    /// Machine-readable VMX architecture seed for Phase 00.
    /// VMX8 entries are the frozen compatibility baseline; VMX-v2 drafts remain
    /// unfrozen until a future VMCSv2 revision is explicitly published here.
    /// </summary>
    public static class VmxSpecTable
    {
        public const string FrozenCompatibilityBaseline = "VMX8";
        public const bool VmxV2DraftsAreCompatibilityTargets = false;

        public static IReadOnlyList<VmxOpcodeSpec> Vmx8Opcodes { get; } =
        [
            new(
                Opcode: Processor.CPU_Core.IsaOpcodeValues.VMXON,
                Mnemonic: "VMXON",
                OperandCount: 1,
                Flags: InstructionFlags.Privileged,
                OperandForm: VmxOperandForm.ReservedIgnoredRegister,
                OperationKindName: "VmxOn",
                InternalOpKindName: "VmxOn",
                RegisterRoleMask: VmxRegisterRoleMask.None,
                CompatibilityNote: "VMX8 preserves one encoded operand as reserved/ignored; VMX-v2 may reinterpret rs1 only behind a future capability."),
            new(
                Opcode: Processor.CPU_Core.IsaOpcodeValues.VMXOFF,
                Mnemonic: "VMXOFF",
                OperandCount: 0,
                Flags: InstructionFlags.Privileged,
                OperandForm: VmxOperandForm.None,
                OperationKindName: "VmxOff",
                InternalOpKindName: "VmxOff",
                RegisterRoleMask: VmxRegisterRoleMask.None,
                CompatibilityNote: "VMX8 control transition; guest-context VMXOFF retires as a VM-exit through runtime state."),
            new(
                Opcode: Processor.CPU_Core.IsaOpcodeValues.VMLAUNCH,
                Mnemonic: "VMLAUNCH",
                OperandCount: 0,
                Flags: InstructionFlags.Privileged,
                OperandForm: VmxOperandForm.None,
                OperationKindName: "VmLaunch",
                InternalOpKindName: "VmLaunch",
                RegisterRoleMask: VmxRegisterRoleMask.None,
                CompatibilityNote: "VM-entry legality remains runtime-owned and active-VMCS dependent."),
            new(
                Opcode: Processor.CPU_Core.IsaOpcodeValues.VMRESUME,
                Mnemonic: "VMRESUME",
                OperandCount: 0,
                Flags: InstructionFlags.Privileged,
                OperandForm: VmxOperandForm.None,
                OperationKindName: "VmResume",
                InternalOpKindName: "VmResume",
                RegisterRoleMask: VmxRegisterRoleMask.None,
                CompatibilityNote: "VM-entry resume legality remains runtime-owned and launched-VMCS dependent."),
            new(
                Opcode: Processor.CPU_Core.IsaOpcodeValues.VMREAD,
                Mnemonic: "VMREAD",
                OperandCount: 2,
                Flags: InstructionFlags.Privileged,
                OperandForm: VmxOperandForm.FieldSelectorToRegister,
                OperationKindName: "VmRead",
                InternalOpKindName: "VmRead",
                RegisterRoleMask: VmxRegisterRoleMask.ReadsRs1 | VmxRegisterRoleMask.WritesRd,
                CompatibilityNote: "Rs1 is the VMCS field selector register; VMX8 does not publish an immediate selector ABI."),
            new(
                Opcode: Processor.CPU_Core.IsaOpcodeValues.VMWRITE,
                Mnemonic: "VMWRITE",
                OperandCount: 2,
                Flags: InstructionFlags.Privileged,
                OperandForm: VmxOperandForm.FieldSelectorAndValueRegisters,
                OperationKindName: "VmWrite",
                InternalOpKindName: "VmWrite",
                RegisterRoleMask: VmxRegisterRoleMask.ReadsRs1 | VmxRegisterRoleMask.ReadsRs2,
                CompatibilityNote: "Rs1 is the VMCS field selector register and Rs2 is the value register."),
            new(
                Opcode: Processor.CPU_Core.IsaOpcodeValues.VMCLEAR,
                Mnemonic: "VMCLEAR",
                OperandCount: 1,
                Flags: InstructionFlags.Privileged,
                OperandForm: VmxOperandForm.VmcsPointerRegister,
                OperationKindName: "VmClear",
                InternalOpKindName: "VmClear",
                RegisterRoleMask: VmxRegisterRoleMask.ReadsRs1,
                CompatibilityNote: "Rs1 carries a VMCS descriptor pointer key; VMX8 does not publish scalar memory side effects."),
            new(
                Opcode: Processor.CPU_Core.IsaOpcodeValues.VMPTRLD,
                Mnemonic: "VMPTRLD",
                OperandCount: 1,
                Flags: InstructionFlags.Privileged,
                OperandForm: VmxOperandForm.VmcsPointerRegister,
                OperationKindName: "VmPtrLd",
                InternalOpKindName: "VmPtrLd",
                RegisterRoleMask: VmxRegisterRoleMask.ReadsRs1,
                CompatibilityNote: "Rs1 carries a VMCS descriptor pointer key; descriptor materialization is runtime-owned.")
        ];

        public static IReadOnlyList<VmcsFieldSpec> Vmx8VmcsFields { get; } =
        [
            new("GuestPc", 0, VmcsFieldGroup.GuestState, VmcsVisibilityPolicy.GuestArchitecturalState, "VMX8 compatibility alias."),
            new("GuestSp", 1, VmcsFieldGroup.GuestState, VmcsVisibilityPolicy.GuestArchitecturalState, "VMX8 compatibility alias."),
            new("GuestFlags", 2, VmcsFieldGroup.GuestState, VmcsVisibilityPolicy.GuestArchitecturalState, "VMX8 compatibility alias."),
            new("GuestCr0", 3, VmcsFieldGroup.GuestState, VmcsVisibilityPolicy.GuestArchitecturalState, "VMX8 compatibility alias."),
            new("GuestCr3", 4, VmcsFieldGroup.GuestState, VmcsVisibilityPolicy.GuestArchitecturalState, "VMX8 compatibility alias."),
            new("GuestCr4", 5, VmcsFieldGroup.GuestState, VmcsVisibilityPolicy.GuestArchitecturalState, "VMX8 compatibility alias."),
            new("HostPc", 32, VmcsFieldGroup.HostState, VmcsVisibilityPolicy.HostArchitecturalState, "VMX8 compatibility alias."),
            new("HostSp", 33, VmcsFieldGroup.HostState, VmcsVisibilityPolicy.HostArchitecturalState, "VMX8 compatibility alias."),
            new("HostFlags", 34, VmcsFieldGroup.HostState, VmcsVisibilityPolicy.HostArchitecturalState, "VMX8 compatibility alias."),
            new("HostCr0", 35, VmcsFieldGroup.HostState, VmcsVisibilityPolicy.HostArchitecturalState, "VMX8 compatibility alias."),
            new("HostCr3", 36, VmcsFieldGroup.HostState, VmcsVisibilityPolicy.HostArchitecturalState, "VMX8 compatibility alias."),
            new("PinBasedControls", 64, VmcsFieldGroup.Controls, VmcsVisibilityPolicy.RootOwnedControl, "VMX8 compatibility alias."),
            new("ProcBasedControls", 65, VmcsFieldGroup.Controls, VmcsVisibilityPolicy.RootOwnedControl, "VMX8 compatibility alias."),
            new("ExitControls", 66, VmcsFieldGroup.Controls, VmcsVisibilityPolicy.RootOwnedControl, "VMX8 compatibility alias."),
            new("EntryControls", 67, VmcsFieldGroup.Controls, VmcsVisibilityPolicy.RootOwnedControl, "VMX8 compatibility alias."),
            new("EptPointer", 80, VmcsFieldGroup.NestedTranslation, VmcsVisibilityPolicy.RootOwnedControl, "VMX8 compatibility alias; not a complete NPT contract in Phase 00."),
            new("Vpid", 81, VmcsFieldGroup.NestedTranslation, VmcsVisibilityPolicy.RootOwnedControl, "VMX8 compatibility alias; not a complete VPID contract in Phase 00."),
            new("SecondaryProcControls", 82, VmcsFieldGroup.NestedTranslation, VmcsVisibilityPolicy.RootOwnedControl, "VMX8 compatibility alias."),
            new("Cr3TargetCount", 83, VmcsFieldGroup.NestedTranslation, VmcsVisibilityPolicy.RootOwnedControl, "VMX8 compatibility alias."),
            new("ExitReason", 96, VmcsFieldGroup.ExitInfo, VmcsVisibilityPolicy.GuestVisibleByPolicy, "VMX8 compatibility alias for legacy exit projection."),
            new("ExitQualification", 97, VmcsFieldGroup.ExitInfo, VmcsVisibilityPolicy.GuestVisibleByPolicy, "VMX8 compatibility alias for legacy exit projection."),
            new("GuestPhysicalAddress", 112, VmcsFieldGroup.NestedTranslationExitInfo, VmcsVisibilityPolicy.GuestVisibleByPolicy, "VMX8 compatibility alias; Phase 03 wires typed NPT qualification."),
            new("EptViolationQualification", 113, VmcsFieldGroup.NestedTranslationExitInfo, VmcsVisibilityPolicy.GuestVisibleByPolicy, "VMX8 compatibility alias; Phase 03 wires typed NPT qualification.")
        ];

        public static IReadOnlyList<VmxCsrSpec> Vmx8Csrs { get; } =
        [
            new("VmxEnable", 0x820, VmxCsrAccessPolicy.MachineReadWrite, "Legacy VMX enable alias."),
            new("VmxCaps", 0x821, VmxCsrAccessPolicy.ReadOnly, "Legacy VMX capability alias."),
            new("VmxControl", 0x822, VmxCsrAccessPolicy.MachineReadWrite, "Legacy VMX control alias."),
            new("VmxExitReason", 0x823, VmxCsrAccessPolicy.MachineReadWrite, "Legacy VMX exit/failure reason projection alias."),
            new("VmxExitQual", 0x824, VmxCsrAccessPolicy.MachineReadWrite, "Legacy VMX exit qualification projection alias.")
        ];

        public static IReadOnlyList<VmExitReasonSpec> Vmx8ExitReasons { get; } =
        [
            new("None", 0),
            new("ExternalInterrupt", 1),
            new("TripleFault", 2),
            new("InitSignal", 3),
            new("Hlt", 12),
            new("VmCall", 18),
            new("VmxOff", 26),
            new("InvalidGuestState", 33),
            new("EntryFailMsrLoading", 34),
            new("EntryFailMachineCheck", 41)
        ];

        public static IReadOnlyList<VmExitReasonSpec> VmxV2ExitReasons { get; } =
        [
            new("VmFunc", 59),
            new("InstructionIntercept", 60),
            new("CsrIntercept", 61),
            new("MemoryIntercept", 62),
            new("VmxOperationIntercept", 63),
            new("LaneOperationIntercept", 64),
            new("VirtualTimer", 65),
            new("VmxPreemptionTimerExpired", 66),
            new("VirtualInterrupt", 67),
            new("DmaDescriptorFault", 68),
            new("IommuFault", 69),
            new("DmaPermissionFault", 70),
            new("DmaAbort", 71),
            new("DmaReplay", 72),
            new("VectorException", 73),
            new("StreamDescriptorFault", 74),
            new("StreamReplayRequired", 75),
            new("Lane7TokenFault", 76),
            new("Lane7BackendUnavailable", 77),
            new("Lane7QuotaExceeded", 78),
            new("SecurityPolicyViolation", 79)
        ];

        public static VmxOpcodeSpec GetVmx8Opcode(ushort opcode)
        {
            foreach (VmxOpcodeSpec spec in Vmx8Opcodes)
            {
                if (spec.Opcode == opcode)
                {
                    return spec;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Opcode is not a frozen VMX8 opcode.");
        }
    }

    public readonly record struct VmxOpcodeSpec(
        ushort Opcode,
        string Mnemonic,
        byte OperandCount,
        InstructionFlags Flags,
        VmxOperandForm OperandForm,
        string OperationKindName,
        string InternalOpKindName,
        VmxRegisterRoleMask RegisterRoleMask,
        string CompatibilityNote)
    {
        public InstructionClass InstructionClass => InstructionClass.Vmx;
        public SerializationClass SerializationClass => SerializationClass.VmxSerial;
        public OpcodeCategory Category => OpcodeCategory.Privileged;
        public byte ExecutionLatency => 1;
        public byte MemoryBandwidth => 0;
        public SlotClassSpec SlotClass => SlotClassSpec.SystemSingleton;
        public byte PinnedLaneId => 7;
    }

    public readonly record struct VmcsFieldSpec(
        string Name,
        ushort Id,
        VmcsFieldGroup Group,
        VmcsVisibilityPolicy VisibilityPolicy,
        string CompatibilityNote);

    public readonly record struct VmxCsrSpec(
        string Name,
        ushort Address,
        VmxCsrAccessPolicy AccessPolicy,
        string CompatibilityNote);

    public readonly record struct VmExitReasonSpec(string Name, uint Value);

    public enum VmxOperandForm : byte
    {
        None = 0,
        ReservedIgnoredRegister = 1,
        FieldSelectorToRegister = 2,
        FieldSelectorAndValueRegisters = 3,
        VmcsPointerRegister = 4,
    }

    [Flags]
    public enum VmxRegisterRoleMask : byte
    {
        None = 0,
        ReadsRs1 = 1 << 0,
        ReadsRs2 = 1 << 1,
        WritesRd = 1 << 2,
    }

    public enum SlotClassSpec : byte
    {
        SystemSingleton = 7,
    }

    public enum VmcsFieldGroup : byte
    {
        GuestState = 0,
        HostState = 1,
        Controls = 2,
        NestedTranslation = 3,
        ExitInfo = 4,
        NestedTranslationExitInfo = 5,
    }

    public enum VmcsVisibilityPolicy : byte
    {
        GuestArchitecturalState = 0,
        HostArchitecturalState = 1,
        RootOwnedControl = 2,
        GuestVisibleByPolicy = 3,
        HostOwnedRuntimeEvidence = 4,
    }

    public enum VmxCsrAccessPolicy : byte
    {
        ReadOnly = 0,
        MachineReadWrite = 1,
    }
}
