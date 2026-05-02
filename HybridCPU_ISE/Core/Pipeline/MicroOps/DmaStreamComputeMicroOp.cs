using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Canonical lane6 typed-slot carrier for descriptor-backed memory-memory compute.
    /// Current contour exposes guard-bound scheduling plus normalized footprint evidence only;
    /// execution remains fail-closed.
    /// </summary>
    public sealed class DmaStreamComputeMicroOp : MicroOp
    {
        public DmaStreamComputeMicroOp(DmaStreamComputeDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            EnsureOwnerDomainGuardAccepted(descriptor);
            EnsureMandatoryFootprints(descriptor);

            Descriptor = descriptor;
            DescriptorReference = descriptor.DescriptorReference;
            DescriptorIdentityHash = descriptor.DescriptorIdentityHash;
            CertificateInputHash = descriptor.CertificateInputHash;
            NormalizedFootprintHash = descriptor.NormalizedFootprintHash;
            Operation = descriptor.Operation;
            ElementType = descriptor.ElementType;
            Shape = descriptor.Shape;
            RangeEncoding = descriptor.RangeEncoding;
            PartialCompletionPolicy = descriptor.PartialCompletionPolicy;
            OwnerBinding = descriptor.OwnerBinding;
            ReplayEvidence = DmaStreamComputeReplayEvidence.CreateForDescriptor(descriptor);

            IsMemoryOp = true;
            HasSideEffects = true;
            Class = MicroOpClass.Dma;
            InstructionClass = Arch.InstructionClass.Memory;
            SerializationClass = Arch.SerializationClass.MemoryOrdered;
            WritesRegister = false;

            OwnerThreadId = descriptor.OwnerBinding.OwnerVirtualThreadId;
            VirtualThreadId = descriptor.OwnerBinding.OwnerVirtualThreadId;
            OwnerContextId = ConvertOwnerContextId(descriptor.OwnerBinding.OwnerContextId);

            SetClassFlexiblePlacement(SlotClass.DmaStreamClass);
            Placement = Placement with { DomainTag = descriptor.OwnerBinding.OwnerDomainTag };

            ReadRegisters = Array.Empty<int>();
            WriteRegisters = Array.Empty<int>();
            ReadMemoryRanges = ConvertRanges(descriptor.NormalizedReadMemoryRanges);
            WriteMemoryRanges = ConvertRanges(descriptor.NormalizedWriteMemoryRanges);

            ResourceMask = BuildResourceMask(descriptor.OwnerBinding.OwnerDomainTag);
            PublishExplicitStructuralSafetyMask();
            RefreshAdmissionMetadata(this);
        }

        public DmaStreamComputeDescriptor Descriptor { get; }

        public DmaStreamComputeDescriptorReference DescriptorReference { get; }

        public ulong DescriptorIdentityHash { get; }

        public ulong CertificateInputHash { get; }

        public ulong NormalizedFootprintHash { get; }

        public DmaStreamComputeOperationKind Operation { get; }

        public DmaStreamComputeElementType ElementType { get; }

        public DmaStreamComputeShapeKind Shape { get; }

        public DmaStreamComputeRangeEncoding RangeEncoding { get; }

        public DmaStreamComputePartialCompletionPolicy PartialCompletionPolicy { get; }

        public DmaStreamComputeOwnerBinding OwnerBinding { get; }

        public DmaStreamComputeReplayEvidence ReplayEvidence { get; }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.SelfPublishes;

        public DmaStreamComputeReplayEvidence ExportReplayEvidence(
            DmaStreamComputeTokenLifecycleEvidence tokenLifecycleEvidence = default,
            DmaStreamComputeLanePlacementEvidence lanePlacementEvidence = default) =>
            DmaStreamComputeReplayEvidence.CreateForDescriptor(
                Descriptor,
                tokenLifecycleEvidence: tokenLifecycleEvidence,
                lanePlacementEvidence: lanePlacementEvidence);

        public override bool Execute(ref Processor.CPU_Core core)
        {
            throw new InvalidOperationException(
                "DmaStreamComputeMicroOp execution is disabled and must fail closed. " +
                "The lane6 typed-slot surface preserves descriptor and footprint evidence only; " +
                "DmaStreamComputeRuntime is an explicit runtime helper, not MicroOp.Execute, and no StreamEngine or DMAController fallback is implied.");
        }

        public override string GetDescription() =>
            $"DmaStreamCompute: Op={Operation}, Type={ElementType}, Shape={Shape}, " +
            $"Descriptor=0x{DescriptorIdentityHash:X16}, Footprint=0x{NormalizedFootprintHash:X16}";

        private static void EnsureMandatoryFootprints(DmaStreamComputeDescriptor descriptor)
        {
            if (descriptor.ReadMemoryRanges is null ||
                descriptor.ReadMemoryRanges.Count == 0 ||
                descriptor.NormalizedReadMemoryRanges is null ||
                descriptor.NormalizedReadMemoryRanges.Count == 0)
            {
                throw new InvalidOperationException(
                    "DmaStreamComputeMicroOp requires non-empty ReadMemoryRanges before typed-slot admission.");
            }

            if (IsMemoryWritingOperation(descriptor.Operation) &&
                (descriptor.WriteMemoryRanges is null ||
                 descriptor.WriteMemoryRanges.Count == 0 ||
                 descriptor.NormalizedWriteMemoryRanges is null ||
                 descriptor.NormalizedWriteMemoryRanges.Count == 0))
            {
                throw new InvalidOperationException(
                    "DmaStreamComputeMicroOp requires non-empty WriteMemoryRanges for memory-writing operations before typed-slot admission.");
            }
        }

        private static void EnsureOwnerDomainGuardAccepted(DmaStreamComputeDescriptor descriptor)
        {
            if (!descriptor.OwnerGuardDecision.IsAllowed)
            {
                throw new InvalidOperationException(
                    "DmaStreamComputeMicroOp requires an accepted owner/domain guard decision before typed-slot admission.");
            }

            if (descriptor.OwnerGuardDecision.DescriptorOwnerBinding is null ||
                !descriptor.OwnerGuardDecision.DescriptorOwnerBinding.Equals(descriptor.OwnerBinding))
            {
                throw new InvalidOperationException(
                    "DmaStreamComputeMicroOp owner/domain guard decision does not match the descriptor owner binding.");
            }
        }

        private static bool IsMemoryWritingOperation(DmaStreamComputeOperationKind operation) =>
            operation is DmaStreamComputeOperationKind.Copy
                or DmaStreamComputeOperationKind.Add
                or DmaStreamComputeOperationKind.Mul
                or DmaStreamComputeOperationKind.Fma
                or DmaStreamComputeOperationKind.Reduce;

        private static IReadOnlyList<(ulong Address, ulong Length)> ConvertRanges(
            IReadOnlyList<DmaStreamComputeMemoryRange> ranges)
        {
            if (ranges is null || ranges.Count == 0)
            {
                return Array.Empty<(ulong Address, ulong Length)>();
            }

            var converted = new (ulong Address, ulong Length)[ranges.Count];
            for (int i = 0; i < ranges.Count; i++)
            {
                converted[i] = (ranges[i].Address, ranges[i].Length);
            }

            return converted;
        }

        private static ResourceBitset BuildResourceMask(ulong ownerDomainTag)
        {
            int resourceDomainBucket = (int)(ownerDomainTag & 0xFUL);

            return ResourceMaskBuilder.ForDMAChannel(0)
                   | ResourceMaskBuilder.ForStreamEngine(0)
                   | ResourceMaskBuilder.ForLoad()
                   | ResourceMaskBuilder.ForStore()
                   | ResourceMaskBuilder.ForMemoryDomain(resourceDomainBucket);
        }

        private static int ConvertOwnerContextId(uint ownerContextId)
        {
            if (ownerContextId > int.MaxValue)
            {
                throw new InvalidOperationException(
                    "DmaStreamCompute owner context id exceeds the current runtime owner-context field width.");
            }

            return (int)ownerContextId;
        }
    }
}
