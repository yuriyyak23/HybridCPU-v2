using HybridCPU_ISE.CloseToRTL.Memory.MMU;
using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute
{
    public readonly record struct VmxDmaDescriptorValidationEvidence(
        ushort IoDomainTag,
        ushort AddressSpaceTag,
        uint DomainId,
        ulong DomainTag,
        uint DeviceId,
        ulong DomainEpoch,
        ulong IotlbMappingEpoch,
        ulong QueueEpoch,
        ulong FenceEpoch,
        ulong ValidationHash)
    {
        public bool IsComplete =>
            IoDomainTag != 0 &&
            DomainId != 0 &&
            DomainTag != 0 &&
            DeviceId != 0 &&
            DomainEpoch != 0 &&
            IotlbMappingEpoch != 0 &&
            QueueEpoch != 0 &&
            ValidationHash != 0;
    }

    public sealed record VmxDmaDescriptorValidationResult
    {
        private VmxDmaDescriptorValidationResult(
            bool isValid,
            DmaStreamComputeDescriptor? materializedDescriptor,
            VmxDmaDescriptorValidationEvidence evidence,
            Lane6QueueBinding queue,
            DmaFault fault,
            string message)
        {
            IsValid = isValid;
            MaterializedDescriptor = materializedDescriptor;
            Evidence = evidence;
            Queue = queue;
            Fault = fault;
            Message = message;
        }

        public bool IsValid { get; }

        public DmaStreamComputeDescriptor? MaterializedDescriptor { get; }

        public VmxDmaDescriptorValidationEvidence Evidence { get; }

        public Lane6QueueBinding Queue { get; }

        public DmaFault Fault { get; }

        public string Message { get; }

        public bool IsAbort => Fault.Disposition == DmaFaultDisposition.Abort;

        public bool IsReplay => Fault.Disposition == DmaFaultDisposition.Replay;

        public static VmxDmaDescriptorValidationResult Valid(
            DmaStreamComputeDescriptor materializedDescriptor,
            VmxDmaDescriptorValidationEvidence evidence,
            Lane6QueueBinding queue) =>
            new(
                true,
                materializedDescriptor,
                evidence,
                queue,
                DmaFault.None,
                "VMX DMA descriptor accepted and materialized into host-owned Lane6 evidence.");

        public static VmxDmaDescriptorValidationResult Fail(DmaFault fault, string? message = null) =>
            new(
                false,
                null,
                default,
                default,
                fault,
                string.IsNullOrWhiteSpace(message) ? fault.Message : message);
    }

    public static class VmxDmaDescriptorValidator
    {
        public static VmxDmaDescriptorValidationResult ValidateAndMaterialize(
            DmaStreamComputeDescriptor guestDescriptor,
            IoVirtualizationBlock ioVirt,
            Lane6StateBlock lane6State,
            ushort ioDomainTag,
            ushort addressSpaceTag = 0,
            ulong guestQueueId = 0)
        {
            ArgumentNullException.ThrowIfNull(guestDescriptor);
            ArgumentNullException.ThrowIfNull(ioVirt);
            ArgumentNullException.ThrowIfNull(lane6State);

            if (ioDomainTag == 0)
            {
                return VmxDmaDescriptorValidationResult.Fail(
                    DmaFault.Abort(
                        DmaFaultKind.MissingDomainBinding,
                        guestDescriptor.DescriptorReference.DescriptorAddress,
                        isWrite: false,
                        "DMA compatibility descriptor validation requires a non-zero I/O-domain tag."));
            }

            if (!ioVirt.TryResolveDescriptorBinding(
                    ioDomainTag,
                    guestDescriptor.OwnerBinding,
                    out IommuDomainBinding binding))
            {
                return VmxDmaDescriptorValidationResult.Fail(
                    DmaFault.Abort(
                        DmaFaultKind.MissingDomainBinding,
                        guestDescriptor.DescriptorReference.DescriptorAddress,
                        isWrite: false,
                        "Guest DMA descriptor has no I/O-domain tag/domain/device IOMMU binding."));
            }

            if (!lane6State.TryEnsureQueue(
                    binding,
                    guestDescriptor.OwnerBinding.OwnerVirtualThreadId,
                    guestQueueId,
                    out Lane6QueueBinding queue,
                    out DmaFault queueFault))
            {
                return VmxDmaDescriptorValidationResult.Fail(queueFault);
            }

            if (binding.NonCoherent &&
                binding.RequiresFence &&
                !lane6State.HasObservedRequiredFence(binding))
            {
                return VmxDmaDescriptorValidationResult.Fail(
                    DmaFault.Abort(
                        DmaFaultKind.NonCoherentFenceRequired,
                        guestDescriptor.DescriptorReference.DescriptorAddress,
                        isWrite: false,
                        "Non-coherent DMA domains require an observed Lane6 fence before descriptor materialization."));
            }

            if (!TryTranslateRanges(
                    binding,
                    guestDescriptor.NormalizedReadMemoryRanges,
                    IOMMUAccessPermissions.Read,
                    out IReadOnlyList<DmaStreamComputeMemoryRange> materializedReadRanges,
                    out DmaFault readFault))
            {
                return VmxDmaDescriptorValidationResult.Fail(readFault);
            }

            if (!TryTranslateRanges(
                    binding,
                    guestDescriptor.NormalizedWriteMemoryRanges,
                    IOMMUAccessPermissions.Write,
                    out IReadOnlyList<DmaStreamComputeMemoryRange> materializedWriteRanges,
                    out DmaFault writeFault))
            {
                return VmxDmaDescriptorValidationResult.Fail(writeFault);
            }

            ulong fenceEpoch = lane6State.QueueRuntime.GetFenceEpoch(binding);
            VmxDmaDescriptorValidationEvidence evidence = new(
                binding.IoDomainTag,
                addressSpaceTag,
                binding.DomainId,
                binding.DomainTag,
                binding.DeviceId,
                binding.DomainEpoch,
                IOMMU.IotlbMappingEpoch,
                queue.QueueEpoch,
                fenceEpoch,
                ComputeValidationHash(guestDescriptor, binding, queue, fenceEpoch));

            DmaStreamComputeDescriptor materialized = guestDescriptor with
            {
                ReadMemoryRanges = materializedReadRanges,
                NormalizedReadMemoryRanges = materializedReadRanges,
                WriteMemoryRanges = materializedWriteRanges,
                NormalizedWriteMemoryRanges = materializedWriteRanges,
                VmxValidationEvidence = evidence
            };

            return VmxDmaDescriptorValidationResult.Valid(materialized, evidence, queue);
        }

        public static bool IsValidationCurrent(
            DmaStreamComputeDescriptor descriptor,
            IoVirtualizationBlock ioVirt,
            out DmaFault fault)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            ArgumentNullException.ThrowIfNull(ioVirt);
            fault = DmaFault.None;

            if (descriptor.VmxValidationEvidence is not { } evidence ||
                !evidence.IsComplete)
            {
                fault = DmaFault.Abort(
                    DmaFaultKind.ValidationEvidenceMissing,
                    descriptor.DescriptorReference.DescriptorAddress,
                    isWrite: false,
                    "VMX guest Lane6 execution requires descriptor validation evidence before MicroOp execution.");
                return false;
            }

            if (!ioVirt.TryResolveBinding(
                    evidence.IoDomainTag,
                    evidence.DomainTag,
                    evidence.DeviceId,
                    out IommuDomainBinding binding) ||
                binding.DomainId != evidence.DomainId ||
                binding.DomainEpoch != evidence.DomainEpoch ||
                IOMMU.IotlbMappingEpoch != evidence.IotlbMappingEpoch)
            {
                fault = DmaFault.Abort(
                    DmaFaultKind.ValidationEvidenceStale,
                    descriptor.DescriptorReference.DescriptorAddress,
                    isWrite: false,
                    "VMX guest Lane6 descriptor validation evidence is stale after IOMMU/domain/IOTLB epoch change.");
                return false;
            }

            return true;
        }

        private static bool TryTranslateRanges(
            IommuDomainBinding binding,
            IReadOnlyList<DmaStreamComputeMemoryRange> ranges,
            IOMMUAccessPermissions permissions,
            out IReadOnlyList<DmaStreamComputeMemoryRange> materializedRanges,
            out DmaFault fault)
        {
            fault = DmaFault.None;
            if (ranges is null || ranges.Count == 0)
            {
                materializedRanges = Array.Empty<DmaStreamComputeMemoryRange>();
                return true;
            }

            var materialized = new DmaStreamComputeMemoryRange[ranges.Count];
            for (int index = 0; index < ranges.Count; index++)
            {
                DmaStreamComputeMemoryRange range = ranges[index];
                if (range.Length == 0 ||
                    range.Address > ulong.MaxValue - (range.Length - 1))
                {
                    materializedRanges = Array.Empty<DmaStreamComputeMemoryRange>();
                    fault = DmaFault.Abort(
                        DmaFaultKind.DescriptorFault,
                        range.Address,
                        (permissions & IOMMUAccessPermissions.Write) != 0,
                        "VMX DMA descriptor contains an empty or overflowing memory range.");
                    return false;
                }

                if (!TryTranslateCompleteRange(binding, range, permissions, out ulong hostPhysicalAddress, out fault))
                {
                    materializedRanges = Array.Empty<DmaStreamComputeMemoryRange>();
                    return false;
                }

                materialized[index] = new DmaStreamComputeMemoryRange(hostPhysicalAddress, range.Length);
            }

            materializedRanges = materialized;
            return true;
        }

        private static bool TryTranslateCompleteRange(
            IommuDomainBinding binding,
            DmaStreamComputeMemoryRange range,
            IOMMUAccessPermissions permissions,
            out ulong firstHostPhysicalAddress,
            out DmaFault fault)
        {
            firstHostPhysicalAddress = 0;
            fault = DmaFault.None;
            ulong remaining = range.Length;
            ulong current = range.Address;
            ulong expectedHost = 0;
            bool first = true;

            while (remaining > 0)
            {
                ulong pageOffset = current & 0xFFFUL;
                ulong chunkSize = Math.Min(4096UL - pageOffset, remaining);
                if (!IOMMU.TryTranslateDma(
                        binding,
                        current,
                        chunkSize,
                        permissions,
                        out DmaTranslationResult translation))
                {
                    fault = translation.Fault;
                    return false;
                }

                if (first)
                {
                    firstHostPhysicalAddress = translation.HostPhysicalAddress;
                    expectedHost = translation.HostPhysicalAddress + chunkSize;
                    first = false;
                }
                else if (translation.HostPhysicalAddress != expectedHost)
                {
                    fault = DmaFault.Abort(
                        DmaFaultKind.IommuTranslationFault,
                        current,
                        (permissions & IOMMUAccessPermissions.Write) != 0,
                        "VMX DMA descriptor range spans non-contiguous host physical pages.");
                    return false;
                }
                else
                {
                    expectedHost += chunkSize;
                }

                current += chunkSize;
                remaining -= chunkSize;
            }

            return true;
        }

        private static ulong ComputeValidationHash(
            DmaStreamComputeDescriptor descriptor,
            IommuDomainBinding binding,
            Lane6QueueBinding queue,
            ulong fenceEpoch)
        {
            var hasher = new HardwareHash();
            hasher.Initialize();
            hasher.Compress(descriptor.DescriptorIdentityHash);
            hasher.Compress(descriptor.NormalizedFootprintHash);
            hasher.Compress(binding.IoDomainTag);
            hasher.Compress(binding.DomainId);
            hasher.Compress(binding.DomainTag);
            hasher.Compress(binding.DeviceId);
            hasher.Compress(binding.DomainEpoch);
            hasher.Compress(IOMMU.IotlbMappingEpoch);
            hasher.Compress(queue.VirtualQueueId);
            hasher.Compress(queue.QueueEpoch);
            hasher.Compress(fenceEpoch);
            ulong hash = hasher.Finalize();
            return hash == 0 ? 0xD5C0_5EEDUL : hash;
        }
    }
}
