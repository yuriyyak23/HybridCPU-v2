using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using YAKSys_Hybrid_CPU.Core.Vmx;

namespace YAKSys_Hybrid_CPU.Core
{
    public readonly record struct VmxInstructionPayload(
        VmxOperandForm OperandForm,
        VmxInvalidationScope InvalidationScope,
        VmxFunctionLeaf FunctionLeaf,
        VmxRootDescriptorReference RootDescriptor,
        VmxExitQualification ExitQualification)
    {
        public static VmxInstructionPayload Empty { get; } =
            new(
                VmxOperandForm.None,
                VmxInvalidationScope.None,
                VmxFunctionLeaf.None,
                VmxRootDescriptorReference.CompatibilityDefault,
                VmxExitQualification.None);

        public static VmxInstructionPayload FromDecodedRegisters(
            ushort opcode,
            byte rd,
            byte rs1,
            byte rs2)
        {
            return opcode switch
            {
                IsaOpcodeValues.VMXON => Empty with
                {
                    OperandForm = VmxOperandForm.ReservedIgnoredRegister,
                    RootDescriptor = VmxRootDescriptorReference.FromOperand(rs1)
                },
                IsaOpcodeValues.VMREAD => Empty with { OperandForm = VmxOperandForm.FieldSelectorToRegister },
                IsaOpcodeValues.VMWRITE => Empty with { OperandForm = VmxOperandForm.FieldSelectorAndValueRegisters },
                IsaOpcodeValues.VMCLEAR or IsaOpcodeValues.VMPTRLD => Empty with { OperandForm = VmxOperandForm.VmcsPointerRegister },
                IsaOpcodeValues.VMPTRST => Empty with { OperandForm = VmxOperandForm.CurrentVmcsPointerToRegister },
                IsaOpcodeValues.VMCALL => Empty with
                {
                    OperandForm = VmxOperandForm.HypercallLeafAndDescriptor,
                    ExitQualification = new VmxExitQualification(rs1, VmxInvalidationScope.None, rs2)
                },
                IsaOpcodeValues.INVEPT or IsaOpcodeValues.INVVPID => Empty with
                {
                    OperandForm = VmxOperandForm.InvalidationScopeAndDescriptor,
                    InvalidationScope = DecodeInvalidationScope(rs1),
                    ExitQualification = new VmxExitQualification(0, DecodeInvalidationScope(rs1), rs2)
                },
                IsaOpcodeValues.VMFUNC => Empty with
                {
                    OperandForm = VmxOperandForm.FunctionLeafAndDescriptor,
                    FunctionLeaf = DecodeFunctionLeaf(rs1),
                    ExitQualification = new VmxExitQualification(rs1, VmxInvalidationScope.None, rs2)
                },
                IsaOpcodeValues.VMSAVEX or IsaOpcodeValues.VMRESTX => Empty with
                {
                    OperandForm = VmxOperandForm.ExtendedStateMaskAndDescriptor,
                    ExitQualification = new VmxExitQualification(rs1, VmxInvalidationScope.None, rs2)
                },
                _ => Empty,
            };
        }

        public static VmxInvalidationScope DecodeInvalidationScope(ulong value) =>
            value switch
            {
                1 => VmxInvalidationScope.SingleContext,
                2 => VmxInvalidationScope.AllContexts,
                3 => VmxInvalidationScope.SingleAddress,
                _ => VmxInvalidationScope.None,
            };

        public static VmxFunctionLeaf DecodeFunctionLeaf(ulong value) =>
            value switch
            {
                (ushort)VmxFunctionLeaf.CapabilityQuery => VmxFunctionLeaf.CapabilityQuery,
                (ushort)VmxFunctionLeaf.Lane7QueryCaps => VmxFunctionLeaf.Lane7QueryCaps,
                (ushort)VmxFunctionLeaf.Lane7Submit => VmxFunctionLeaf.Lane7Submit,
                _ => VmxFunctionLeaf.None,
            };
    }

    public static class VmxV2InstructionCaps
    {
        public const ulong VmPtrSt = 1UL << 0;
        public const ulong VmCall = 1UL << 1;
        public const ulong Invept = 1UL << 2;
        public const ulong Invvpid = 1UL << 3;
        public const ulong VmFunc = 1UL << 4;
        public const ulong RootDescriptorOperand = 1UL << 5;
        public const ulong VmSaveX = 1UL << 6;
        public const ulong VmRestX = 1UL << 7;
        public const ulong NestedVmx = 1UL << 8;
    }

    public static class VmxV2ControlBits
    {
        public const ulong VmFuncCapabilityQuery = 1UL << 0;
        public const ulong VmFuncLane7CapabilityQuery = 1UL << 1;
    }
}
