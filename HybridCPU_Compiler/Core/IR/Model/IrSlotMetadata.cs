using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace HybridCPU.Compiler.Core.IR
{
    /// <summary>
    /// Compiler-side typed-slot/admission descriptor derived from canonical opcode facts.
    /// </summary>
    /// <remarks>
    /// This descriptor mirrors existing compiler/runtime vocabulary; it does not make
    /// compiler metadata authoritative over runtime legality.
    /// </remarks>
    public readonly record struct IrTypedSlotAdmissionDescriptor(
        IrResourceClass ResourceClass,
        SlotClass RequiredSlotClass,
        IrSlotBindingKind BindingKind,
        IrIssueSlotMask LegalSlots)
    {
        public static IrTypedSlotAdmissionDescriptor FromExecutionProfile(IrOpcodeExecutionProfile profile) =>
            new(
                profile.ResourceClass,
                profile.DerivedSlotClass,
                profile.DerivedBindingKind,
                profile.LegalSlots);
    }

    /// <summary>
    /// Compiler-side slot metadata detached from ISA operands and decode payload.
    /// </summary>
    /// <remarks>
    /// <paramref name="VirtualThreadId"/> is core ownership metadata.
    /// <paramref name="StealabilityHint"/> is an advisory runtime hint propagated from
    /// encoded metadata; it does not affect structural admissibility or lane binding.
    /// <paramref name="AdmissionDescriptor"/> carries the canonical typed-slot/admission
    /// descriptor once IR construction has derived it from opcode semantics.
    /// </remarks>
    public readonly record struct IrSlotMetadata(
        byte VirtualThreadId,
        bool StealabilityHint = false,
        IrTypedSlotAdmissionDescriptor? AdmissionDescriptor = null,
        DmaStreamComputeDescriptor? DmaStreamComputeDescriptor = null,
        AcceleratorCommandDescriptor? AcceleratorCommandDescriptor = null)
    {
        public static IrSlotMetadata DefaultForVirtualThread(byte virtualThreadId) =>
            new(virtualThreadId);

        public static IrSlotMetadata FromInstructionMetadata(InstructionSlotMetadata metadata) =>
            new(
                metadata.VirtualThreadId.Value,
                metadata.SlotMetadata.StealabilityPolicy == StealabilityPolicy.Stealable,
                DmaStreamComputeDescriptor: metadata.DmaStreamComputeDescriptor,
                AcceleratorCommandDescriptor: metadata.AcceleratorCommandDescriptor);

        public IrSlotMetadata WithAdmissionDescriptor(IrOpcodeExecutionProfile profile) =>
            this with { AdmissionDescriptor = IrTypedSlotAdmissionDescriptor.FromExecutionProfile(profile) };

        public IrSlotMetadata WithAcceleratorDescriptor(AcceleratorCommandDescriptor descriptor)
        {
            System.ArgumentNullException.ThrowIfNull(descriptor);
            return this with { AcceleratorCommandDescriptor = descriptor };
        }
    }
}
