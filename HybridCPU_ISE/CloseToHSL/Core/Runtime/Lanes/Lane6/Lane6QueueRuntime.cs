using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core;

public sealed class Lane6QueueRuntime
{
    private readonly Dictionary<(ushort IoDomainTag, uint DomainId, uint DeviceId, ushort VtId), Lane6QueueBinding> _queues = new();
    private readonly Dictionary<(ushort IoDomainTag, uint DomainId, uint DeviceId), ulong> _fenceEpochs = new();
    private readonly int _queueCapacity;
    private ulong _queueEpoch;
    private ulong _fenceEpoch;
    private ulong _nextVirtualQueueId = 0x1_0000UL;
    private ulong _nextVirtualTokenId = 0x1_0000_0000UL;

    public Lane6QueueRuntime(int queueCapacity = 64)
        : this(queueCapacity, queueEpoch: 0, fenceEpoch: 0)
    {
    }

    internal Lane6QueueRuntime(int queueCapacity, ulong queueEpoch, ulong fenceEpoch)
    {
        _queueCapacity = queueCapacity < 0 ? 0 : queueCapacity;
        _queueEpoch = queueEpoch;
        _fenceEpoch = fenceEpoch;
        HostEvidence = new Lane6HostOwnedEvidenceStore();
    }

    public Lane6HostOwnedEvidenceStore HostEvidence { get; }

    public ulong QueueEpoch => _queueEpoch;

    public ulong FenceEpoch => _fenceEpoch;

    public int ActiveQueueCount => _queues.Count;

    public bool TryEnsureQueue(
        IommuDomainBinding binding,
        ushort ownerVirtualThreadId,
        ulong guestQueueId,
        out Lane6QueueBinding queue,
        out DmaFault fault)
    {
        queue = default;
        fault = DmaFault.None;

        if (!binding.IsValid)
        {
            fault = DmaFault.Abort(
                DmaFaultKind.QueueOwnershipFault,
                0,
                isWrite: false,
                "Lane6 queue runtime requires I/O-domain tag/domain/device ownership before queue binding.");
            return false;
        }

        var key = (binding.IoDomainTag, binding.DomainId, binding.DeviceId, ownerVirtualThreadId);
        if (_queues.TryGetValue(key, out queue))
        {
            return true;
        }

        if (_queues.Count >= _queueCapacity)
        {
            fault = DmaFault.Replay(
                DmaFaultKind.Lane6QueuePressure,
                0,
                isWrite: false,
                "Lane6 virtual queue pressure is transient and must replay, not abort the guest descriptor.");
            return false;
        }

        if (!TryAdvanceEpoch(ref _queueEpoch, out ulong queueEpoch))
        {
            fault = EpochExhaustedFault("Lane6 queue epoch");
            return false;
        }

        if (!TryAllocateIdentity(ref _nextVirtualQueueId, out ulong virtualQueueId))
        {
            fault = IdentityExhaustedFault("Lane6 virtual queue identity");
            return false;
        }

        queue = new Lane6QueueBinding(
            binding.IoDomainTag,
            ownerVirtualThreadId,
            binding.DomainId,
            binding.DomainTag,
            binding.DeviceId,
            guestQueueId == 0 ? BuildDefaultGuestQueueId(binding, ownerVirtualThreadId) : guestQueueId,
            virtualQueueId,
            queueEpoch);
        _queues[key] = queue;
        return true;
    }

    public bool TryObserveFence(
        IommuDomainBinding binding,
        ulong guestFenceId,
        out DmaFault fault)
    {
        _ = guestFenceId;
        fault = DmaFault.None;
        if (!binding.IsValid)
        {
            fault = DmaFault.Abort(
                DmaFaultKind.QueueOwnershipFault,
                0,
                isWrite: false,
                "Lane6 fence observation requires a valid I/O-domain binding.");
            return false;
        }

        if (!TryAdvanceEpoch(ref _fenceEpoch, out ulong epoch))
        {
            fault = EpochExhaustedFault("Lane6 fence epoch");
            return false;
        }

        _fenceEpochs[(binding.IoDomainTag, binding.DomainId, binding.DeviceId)] = epoch;
        return true;
    }

    public ulong GetFenceEpoch(IommuDomainBinding binding) =>
        _fenceEpochs.TryGetValue(
            (binding.IoDomainTag, binding.DomainId, binding.DeviceId),
            out ulong epoch)
            ? epoch
            : 0;

    public bool TryMapToken(
        Lane6QueueBinding queue,
        ulong guestTokenId,
        ulong guestFenceId,
        DmaStreamComputeTokenHandle hostHandle,
        out Lane6VirtualToken virtualToken,
        out DmaFault fault)
    {
        virtualToken = default;
        fault = DmaFault.None;

        if (!queue.IsValid || guestTokenId == 0 || hostHandle.IsDefault)
        {
            fault = DmaFault.Abort(
                DmaFaultKind.QueueOwnershipFault,
                0,
                isWrite: false,
                "Lane6 token binding requires a valid queue, guest token, and host-owned token handle.");
            return false;
        }

        if (!TryAllocateIdentity(ref _nextVirtualTokenId, out ulong virtualTokenId))
        {
            fault = IdentityExhaustedFault("Lane6 virtual token identity");
            return false;
        }

        ulong fenceEpoch = _fenceEpochs.TryGetValue(
            (queue.IoDomainTag, queue.DomainId, queue.DeviceId),
            out ulong observedFenceEpoch)
            ? observedFenceEpoch
            : 0;
        virtualToken = new Lane6VirtualToken(
            queue.IoDomainTag,
            queue.OwnerVirtualThreadId,
            queue.DomainId,
            queue.DomainTag,
            queue.DeviceId,
            guestTokenId,
            virtualTokenId,
            guestFenceId,
            queue.QueueEpoch,
            fenceEpoch);

        if (!HostEvidence.TryBind(virtualToken, hostHandle))
        {
            virtualToken = default;
            fault = DmaFault.Abort(
                DmaFaultKind.QueueOwnershipFault,
                0,
                isWrite: false,
                "Lane6 host evidence store denied native token binding.");
            return false;
        }

        return true;
    }

    public Lane6HostEvidenceRestoreResult PrepareHostEvidenceForRestore(
        EvidencePolicyDescriptor evidencePolicy) =>
        HostEvidence.PrepareForRestore(evidencePolicy);

    private static bool TryAdvanceEpoch(ref ulong epoch, out ulong advancedEpoch)
    {
        if (epoch == ulong.MaxValue)
        {
            advancedEpoch = 0;
            return false;
        }

        epoch++;
        advancedEpoch = epoch;
        return true;
    }

    private static bool TryAllocateIdentity(ref ulong nextIdentity, out ulong identity)
    {
        if (nextIdentity == 0 || nextIdentity == ulong.MaxValue)
        {
            identity = 0;
            return false;
        }

        identity = nextIdentity++;
        return true;
    }

    private static DmaFault EpochExhaustedFault(string identity) =>
        DmaFault.Abort(
            DmaFaultKind.EpochExhausted,
            0,
            isWrite: false,
            $"{identity} exhausted; runtime refuses wraparound and requires a new domain binding.");

    private static DmaFault IdentityExhaustedFault(string identity) =>
        DmaFault.Abort(
            DmaFaultKind.ResourceIdentityExhausted,
            0,
            isWrite: false,
            $"{identity} exhausted; runtime refuses identity reuse.");

    private static ulong BuildDefaultGuestQueueId(
        IommuDomainBinding binding,
        ushort ownerVirtualThreadId) =>
        ((ulong)binding.IoDomainTag << 48) |
        ((ulong)binding.DeviceId << 32) |
        ((ulong)ownerVirtualThreadId << 16) |
        binding.DomainId;
}
