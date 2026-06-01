using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core
{
    public readonly record struct Lane6QueueBinding(
        ushort IoDomainTag,
        ushort OwnerVirtualThreadId,
        uint DomainId,
        ulong DomainTag,
        uint DeviceId,
        ulong GuestQueueId,
        ulong VirtualQueueId,
        ulong QueueEpoch)
    {
        public bool IsValid =>
            IoDomainTag != 0 &&
            DomainId != 0 &&
            DomainTag != 0 &&
            DeviceId != 0 &&
            VirtualQueueId != 0 &&
            QueueEpoch != 0;
    }

    public readonly partial record struct Lane6VirtualToken(
        ushort IoDomainTag,
        ushort OwnerVirtualThreadId,
        uint DomainId,
        ulong DomainTag,
        uint DeviceId,
        ulong GuestTokenId,
        ulong VirtualTokenId,
        ulong GuestFenceId,
        ulong QueueEpoch,
        ulong FenceEpoch)
    {
        public bool IsValid =>
            IoDomainTag != 0 &&
            DomainId != 0 &&
            DomainTag != 0 &&
            DeviceId != 0 &&
            GuestTokenId != 0 &&
            VirtualTokenId != 0;

    }

    public sealed class Lane6StateBlock
    {
        public Lane6StateBlock()
            : this(queueCapacity: 64)
        {
        }

        public Lane6StateBlock(int queueCapacity)
        {
            QueueRuntime = new Lane6QueueRuntime(queueCapacity);
        }

        public Lane6QueueRuntime QueueRuntime { get; }

        public Lane6HostOwnedEvidenceStore HostEvidence => QueueRuntime.HostEvidence;

        public ulong QueueEpoch => QueueRuntime.QueueEpoch;

        public ulong FenceEpoch => QueueRuntime.FenceEpoch;

        public int ActiveQueueCount => QueueRuntime.ActiveQueueCount;

        public bool TryEnsureQueue(
            IommuDomainBinding binding,
            ushort ownerVirtualThreadId,
            ulong guestQueueId,
            out Lane6QueueBinding queue,
            out DmaFault fault) =>
            QueueRuntime.TryEnsureQueue(
                binding,
                ownerVirtualThreadId,
                guestQueueId,
                out queue,
                out fault);

        public bool TryObserveFence(
            IommuDomainBinding binding,
            ulong guestFenceId,
            out DmaFault fault) =>
            QueueRuntime.TryObserveFence(binding, guestFenceId, out fault);

        public bool HasObservedRequiredFence(IommuDomainBinding binding) =>
            QueueRuntime.GetFenceEpoch(binding) != 0;

        public bool TryMapToken(
            Lane6QueueBinding queue,
            ulong guestTokenId,
            ulong guestFenceId,
            DmaStreamComputeTokenHandle hostHandle,
            out Lane6VirtualToken virtualToken,
            out DmaFault fault) =>
            QueueRuntime.TryMapToken(
                queue,
                guestTokenId,
                guestFenceId,
                hostHandle,
                out virtualToken,
                out fault);

        public bool TryResolveHostToken(
            Lane6VirtualToken virtualToken,
            out DmaStreamComputeTokenHandle hostHandle) =>
            HostEvidence.TryResolve(virtualToken, out hostHandle);

        public Lane6HostEvidenceRestoreResult PrepareHostEvidenceForRestore(
            EvidencePolicyDescriptor evidencePolicy) =>
            QueueRuntime.PrepareHostEvidenceForRestore(evidencePolicy);

        public Lane6HostEvidenceRestoreResult RebuildHostEvidenceAfterRestore(
            Lane6VirtualToken virtualToken,
            DmaStreamComputeTokenHandle hostHandle) =>
            HostEvidence.RebuildAfterRestore(virtualToken, hostHandle);
    }
}
