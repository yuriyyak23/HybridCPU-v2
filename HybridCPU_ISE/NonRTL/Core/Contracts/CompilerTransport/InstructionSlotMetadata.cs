using System;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Registers;
using CoreSlotMetadata = YAKSys_Hybrid_CPU.Core.SlotMetadata;

namespace HybridCPU_ISE.Arch
{
    /// <summary>
    /// Non-architectural per-instruction slot metadata carried alongside the VLIW payload.
    /// </summary>
    public readonly record struct InstructionSlotMetadata(
        VtId VirtualThreadId,
        CoreSlotMetadata SlotMetadata)
    {
        public static InstructionSlotMetadata Default { get; } = new(VtId.Create(0), CoreSlotMetadata.Default);

        public DmaStreamComputeDescriptor? DmaStreamComputeDescriptor { get; init; }

        public DmaStreamComputeDescriptorReference? DmaStreamComputeDescriptorReference =>
            DmaStreamComputeDescriptor?.DescriptorReference;

        public AcceleratorCommandDescriptor? AcceleratorCommandDescriptor { get; init; }

        public AcceleratorDescriptorReference? AcceleratorCommandDescriptorReference =>
            AcceleratorCommandDescriptor?.DescriptorReference;

        public InstructionSlotMetadata WithAcceleratorDescriptor(
            AcceleratorCommandDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            return this with { AcceleratorCommandDescriptor = descriptor };
        }
    }
}
