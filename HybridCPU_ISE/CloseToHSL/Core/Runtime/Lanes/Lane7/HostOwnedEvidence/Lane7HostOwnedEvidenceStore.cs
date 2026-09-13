using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace YAKSys_Hybrid_CPU.Core;

public enum Lane7HostEvidenceRestoreDecision : byte
{
    RebuildRequired = 0,
    Rebuilt = 1,
    RestoreRejected = 2,
}

public readonly record struct Lane7HostEvidenceRestoreResult(
    Lane7HostEvidenceRestoreDecision Decision,
    string Reason)
{
    public bool RequiresRebuild => Decision == Lane7HostEvidenceRestoreDecision.RebuildRequired;

    public static Lane7HostEvidenceRestoreResult RebuildRequired { get; } =
        new(
            Lane7HostEvidenceRestoreDecision.RebuildRequired,
            "Lane7 host-owned token, backend binding, and scheduler evidence was cleared for restore and must be rebuilt from host runtime state.");

    public static Lane7HostEvidenceRestoreResult Rebuilt { get; } =
        new(
            Lane7HostEvidenceRestoreDecision.Rebuilt,
            "Lane7 host-owned evidence was rebuilt from fresh host runtime state.");

    public static Lane7HostEvidenceRestoreResult Rejected(string reason) =>
        new(Lane7HostEvidenceRestoreDecision.RestoreRejected, reason);
}

public sealed class Lane7HostOwnedEvidenceStore
{
    private readonly Dictionary<ulong, Lane7BackendBinding> _backendBindings = new();
    private readonly Dictionary<ulong, AcceleratorTokenHandle> _nativeTokenByVirtualToken = new();
    private readonly Dictionary<ulong, ulong> _virtualTokenByNativeHandle = new();
    private int _submitPollCount;
    private Lane7PressureSnapshot _lastPressure;
    private ulong _backendBindingEpoch;
    private ulong _pressureEpoch;

    public int ActiveBackendBindingCount => _backendBindings.Count;

    public int ActiveTokenBindingCount => _nativeTokenByVirtualToken.Count;

    public ulong BackendBindingEpoch => _backendBindingEpoch;

    public ulong PressureEpoch => _pressureEpoch;

    public Lane7PressureSnapshot LastPressure => _lastPressure;

    public bool TryBindToken(
        Lane7VirtualToken virtualToken,
        AcceleratorTokenHandle hostHandle)
    {
        if (!virtualToken.IsValid || !hostHandle.IsValid)
        {
            return false;
        }

        if (_nativeTokenByVirtualToken.Remove(virtualToken.VirtualTokenId, out AcceleratorTokenHandle previousHandle))
        {
            _virtualTokenByNativeHandle.Remove(previousHandle.Value);
        }

        _nativeTokenByVirtualToken[virtualToken.VirtualTokenId] = hostHandle;
        _virtualTokenByNativeHandle[hostHandle.Value] = virtualToken.VirtualTokenId;
        return true;
    }

    public bool TryResolveHostToken(
        ulong virtualTokenId,
        out AcceleratorTokenHandle hostHandle) =>
        _nativeTokenByVirtualToken.TryGetValue(virtualTokenId, out hostHandle);

    public bool TryResolveVirtualTokenForHost(
        AcceleratorTokenHandle hostHandle,
        IReadOnlyDictionary<ulong, Lane7VirtualToken> virtualTokens,
        out Lane7VirtualToken virtualToken)
    {
        if (hostHandle.IsValid &&
            _virtualTokenByNativeHandle.TryGetValue(hostHandle.Value, out ulong virtualTokenId))
        {
            return virtualTokens.TryGetValue(virtualTokenId, out virtualToken);
        }

        virtualToken = default;
        return false;
    }

    public bool ReleaseToken(ulong virtualTokenId)
    {
        if (!_nativeTokenByVirtualToken.Remove(virtualTokenId, out AcceleratorTokenHandle hostHandle))
        {
            return false;
        }

        _virtualTokenByNativeHandle.Remove(hostHandle.Value);
        return true;
    }

    public void ClearTokenBindings()
    {
        _nativeTokenByVirtualToken.Clear();
        _virtualTokenByNativeHandle.Clear();
    }

    public bool TryBindBackend(
        ushort executionDomainTag,
        ushort ownerVirtualThreadId,
        ulong virtualHandle,
        ulong backendGeneration,
        bool available,
        bool requiresRebind,
        out Lane7BackendBinding binding)
    {
        binding = default;
        if (executionDomainTag == 0 || virtualHandle == 0)
        {
            return false;
        }

        if (!TryAdvanceBackendBindingEpoch(out ulong bindingEpoch))
        {
            return false;
        }

        binding = new Lane7BackendBinding(
            executionDomainTag,
            ownerVirtualThreadId,
            virtualHandle,
            bindingEpoch,
            backendGeneration == 0 ? bindingEpoch : backendGeneration,
            available,
            requiresRebind);
        _backendBindings[virtualHandle] = binding;
        return true;
    }

    public bool TryResolveBackendBinding(
        ulong virtualHandle,
        out Lane7BackendBinding binding) =>
        _backendBindings.TryGetValue(virtualHandle, out binding);

    public bool RemoveBackendBinding(ulong virtualHandle)
    {
        bool removed = _backendBindings.Remove(virtualHandle);
        if (removed)
        {
            TryAdvanceBackendBindingEpoch(out _);
        }

        return removed;
    }

    public bool DiscardBackendBindings()
    {
        _backendBindings.Clear();
        return TryAdvanceBackendBindingEpoch(out _);
    }

    public void ClearBackendBindings() => _backendBindings.Clear();

    public Lane7PressureSnapshot ObserveSubmitPollPressure(
        ushort executionDomainTag,
        ushort addressSpaceTag,
        ushort ownerVirtualThreadId,
        int inflightTokens,
        Lane7QuotaPolicy quotaPolicy)
    {
        _submitPollCount++;
        bool quotaExceeded = _submitPollCount > quotaPolicy.MaxSubmitPollPerWindow;
        return ObserveSchedulerPressure(
            executionDomainTag,
            addressSpaceTag,
            ownerVirtualThreadId,
            inflightTokens,
            quotaPolicy,
            quotaExceeded);
    }

    public Lane7PressureSnapshot ObserveSchedulerPressure(
        ushort executionDomainTag,
        ushort addressSpaceTag,
        ushort ownerVirtualThreadId,
        int inflightTokens,
        Lane7QuotaPolicy quotaPolicy,
        bool quotaExceeded)
    {
        bool epochAdvanced = TryAdvancePressureEpoch(out ulong pressureEpoch);
        bool failClosedPressure = !epochAdvanced || quotaExceeded;
        _lastPressure = new Lane7PressureSnapshot(
            executionDomainTag,
            addressSpaceTag,
            ownerVirtualThreadId,
            inflightTokens,
            quotaPolicy.MaxInflightTokens,
            _submitPollCount,
            quotaPolicy.MaxSubmitPollPerWindow,
            failClosedPressure,
            failClosedPressure || _submitPollCount >= quotaPolicy.MaxSubmitPollPerWindow,
            pressureEpoch);
        return _lastPressure;
    }

    public void ResetSchedulerWindow()
    {
        _submitPollCount = 0;
    }

    public void ResetSchedulerEvidence()
    {
        _submitPollCount = 0;
        _pressureEpoch = 0;
        _lastPressure = default;
    }

    public Lane7HostEvidenceRestoreResult PrepareForRestore(
        EvidencePolicyDescriptor evidencePolicy)
    {
        var boundary = new HostOwnedEvidenceBoundary();
        if (!RequiresRecompute(boundary, evidencePolicy, EvidenceVisibilityClass.NativeTokenEvidence) ||
            !RequiresRecompute(boundary, evidencePolicy, EvidenceVisibilityClass.BackendBindingEvidence) ||
            !RequiresRecompute(boundary, evidencePolicy, EvidenceVisibilityClass.SchedulerEvidence))
        {
            return Lane7HostEvidenceRestoreResult.Rejected(
                "Lane7 host-owned evidence cannot be imported as restored state.");
        }

        ClearTokenBindings();
        ClearBackendBindings();
        ResetSchedulerEvidence();
        return Lane7HostEvidenceRestoreResult.RebuildRequired;
    }

    public Lane7HostEvidenceRestoreResult RebuildTokenAfterRestore(
        Lane7VirtualToken virtualToken,
        AcceleratorTokenHandle hostHandle) =>
        TryBindToken(virtualToken, hostHandle)
            ? Lane7HostEvidenceRestoreResult.Rebuilt
            : Lane7HostEvidenceRestoreResult.Rejected(
                "Lane7 host evidence rebuild requires a valid virtual token and fresh host token handle.");

    public Lane7HostEvidenceRestoreResult RebuildBackendAfterRestore(
        ushort executionDomainTag,
        ushort ownerVirtualThreadId,
        ulong virtualHandle,
        ulong backendGeneration,
        out Lane7BackendBinding binding) =>
        TryBindBackend(
            executionDomainTag,
            ownerVirtualThreadId,
            virtualHandle,
            backendGeneration,
            available: true,
            requiresRebind: false,
            out binding)
            ? Lane7HostEvidenceRestoreResult.Rebuilt
            : Lane7HostEvidenceRestoreResult.Rejected(
                "Lane7 backend evidence rebuild requires a valid virtual handle and an unexhausted binding epoch.");

    private static bool RequiresRecompute(
        HostOwnedEvidenceBoundary boundary,
        EvidencePolicyDescriptor evidencePolicy,
        EvidenceVisibilityClass evidenceClass) =>
        boundary.ValidateRestore(
            evidencePolicy,
            evidenceClass,
            EvidenceRestorePolicy.RecomputeAfterRestore).Decision ==
        HostOwnedEvidenceBoundaryDecision.RestoreRequiresRecompute;

    private bool TryAdvanceBackendBindingEpoch(out ulong advancedEpoch) =>
        TryAdvanceEpoch(ref _backendBindingEpoch, out advancedEpoch);

    private bool TryAdvancePressureEpoch(out ulong advancedEpoch) =>
        TryAdvanceEpoch(ref _pressureEpoch, out advancedEpoch);

    private static bool TryAdvanceEpoch(ref ulong epoch, out ulong advancedEpoch)
    {
        if (epoch == ulong.MaxValue)
        {
            advancedEpoch = epoch;
            return false;
        }

        epoch++;
        if (epoch == 0)
        {
            epoch = 1;
        }

        advancedEpoch = epoch;
        return true;
    }
}
