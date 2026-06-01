using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace YAKSys_Hybrid_CPU.Core
{
    [Flags]
    public enum Lane7VirtualCapability : ulong
    {
        None = 0,
        QueryCaps = 1UL << 0,
        Submit = 1UL << 1,
        PollStatus = 1UL << 2,
        Wait = 1UL << 3,
        Cancel = 1UL << 4,
        Fence = 1UL << 5,
        MatMul = 1UL << 16,
    }

    public enum Lane7FaultKind : byte
    {
        None = 0,
        VirtualizationDisabled = 1,
        OwnershipMismatch = 2,
        InvalidVirtualHandle = 3,
        InvalidVirtualToken = 4,
        CapabilityDenied = 5,
        QuotaExceeded = 6,
        FirstUseBindingRequired = 7,
        BackendUnavailable = 8,
        BackendRebindRequired = 9,
        SecuritySensitiveOperation = 10,
        CompletionRouteDenied = 11,
    }

    public enum Lane7FaultDisposition : byte
    {
        None = 0,
        Abort = 1,
        Replay = 2,
        RuntimeFallback = 3,
    }

    public readonly record struct Lane7Fault(
        Lane7FaultKind Kind,
        Lane7FaultDisposition Disposition,
        string Message)
    {
        public bool IsFaulted => Kind != Lane7FaultKind.None;

        public static Lane7Fault None { get; } =
            new(Lane7FaultKind.None, Lane7FaultDisposition.None, string.Empty);

        public static Lane7Fault Abort(
            Lane7FaultKind kind,
            string message) =>
            new(kind, Lane7FaultDisposition.Abort, message);

        public static Lane7Fault Replay(
            Lane7FaultKind kind,
            string message) =>
            new(kind, Lane7FaultDisposition.Replay, message);

        public static Lane7Fault Fallback(
            Lane7FaultKind kind,
            string message) =>
            new(kind, Lane7FaultDisposition.RuntimeFallback, message);
    }

    public readonly record struct Lane7QuotaPolicy(
        ushort MaxVirtualHandles,
        ushort MaxInflightTokens,
        ushort MaxSubmitPollPerWindow)
    {
        public static Lane7QuotaPolicy Default { get; } =
            new(MaxVirtualHandles: 64, MaxInflightTokens: 64, MaxSubmitPollPerWindow: 8);

        public Lane7QuotaPolicy Normalize() =>
            new(
                MaxVirtualHandles == 0 ? (ushort)1 : MaxVirtualHandles,
                MaxInflightTokens == 0 ? (ushort)1 : MaxInflightTokens,
                MaxSubmitPollPerWindow == 0 ? (ushort)1 : MaxSubmitPollPerWindow);
    }

    public readonly record struct Lane7VirtualHandle(
        ushort ExecutionDomainTag,
        ushort OwnerVirtualThreadId,
        ulong Value,
        AcceleratorDeviceId AcceleratorId,
        Lane7VirtualCapability Capabilities,
        ulong HandleEpoch)
    {
        public bool IsValid =>
            ExecutionDomainTag != 0 &&
            Value != 0 &&
            Capabilities != Lane7VirtualCapability.None &&
            HandleEpoch != 0;
    }

    public readonly partial record struct Lane7VirtualToken(
        ushort ExecutionDomainTag,
        ushort AddressSpaceTag,
        ushort OwnerVirtualThreadId,
        ulong VirtualHandle,
        ulong VirtualTokenId,
        ulong TokenEpoch,
        AcceleratorTokenStatusWord Status,
        ulong CompletionEpoch)
    {
        public bool IsValid =>
            ExecutionDomainTag != 0 &&
            VirtualHandle != 0 &&
            VirtualTokenId != 0 &&
            TokenEpoch != 0;

    }

    public readonly record struct Lane7BackendBinding(
        ushort ExecutionDomainTag,
        ushort OwnerVirtualThreadId,
        ulong VirtualHandle,
        ulong BindingEpoch,
        ulong BackendGeneration,
        bool Available,
        bool RequiresRebind)
    {
        public bool IsUsable =>
            ExecutionDomainTag != 0 &&
            VirtualHandle != 0 &&
            BindingEpoch != 0 &&
            Available &&
            !RequiresRebind;
    }

    public readonly record struct Lane7PressureSnapshot(
        ushort ExecutionDomainTag,
        ushort AddressSpaceTag,
        ushort OwnerVirtualThreadId,
        int InflightTokens,
        int MaxInflightTokens,
        int SubmitPollCount,
        int MaxSubmitPollPerWindow,
        bool QuotaExceeded,
        bool SchedulerPreemptionRecommended,
        ulong PressureEpoch);

    public sealed partial class Lane7Checkpoint
    {
        internal Lane7Checkpoint(
            ushort executionDomainTag,
            ushort addressSpaceTag,
            bool virtualizationEnabled,
            IReadOnlyList<Lane7VirtualHandle> handles,
            IReadOnlyList<Lane7VirtualToken> tokens,
            ulong ownershipEpoch,
            ulong tokenEpoch,
            ulong completionEpoch)
        {
            ExecutionDomainTag = executionDomainTag;
            AddressSpaceTag = addressSpaceTag;
            VirtualizationEnabled = virtualizationEnabled;
            VirtualHandles = handles;
            VirtualTokens = tokens;
            OwnershipEpoch = ownershipEpoch;
            TokenEpoch = tokenEpoch;
            CompletionEpoch = completionEpoch;
        }

        public ushort ExecutionDomainTag { get; }

        public ushort AddressSpaceTag { get; }

        public bool VirtualizationEnabled { get; }

        public IReadOnlyList<Lane7VirtualHandle> VirtualHandles { get; }

        public IReadOnlyList<Lane7VirtualToken> VirtualTokens { get; }

        public ulong OwnershipEpoch { get; }

        public ulong TokenEpoch { get; }

        public ulong CompletionEpoch { get; }

    }

    public sealed partial class Lane7StateBlock
    {
        private readonly Dictionary<ulong, Lane7VirtualHandle> _handlesByValue = new();
        private readonly Dictionary<(ushort ExecutionDomainTag, ushort OwnerVirtualThreadId, AcceleratorDeviceId AcceleratorId), ulong> _handleByOwner = new();
        private readonly Dictionary<ulong, Lane7VirtualToken> _tokensByVirtualValue = new();

        private ulong _nextVirtualHandle = 0x7000_0000_0000_0001UL;
        private ulong _nextVirtualToken = 0x7100_0000_0000_0001UL;

        public Lane7StateBlock()
        {
            HostEvidence = new Lane7HostOwnedEvidenceStore();
            HandleNamespace = new Lane7AcceleratorHandleNamespace(this);
            TokenNamespace = new Lane7AcceleratorTokenNamespace(this);
            CompletionRouter = new Lane7AcceleratorCompletionRouter(this);
            AdmissionPolicy = new Lane7AcceleratorAdmissionPolicy(this);
        }

        public bool VirtualizationEnabled { get; private set; }

        public ushort ExecutionDomainTag { get; private set; }

        public ushort AddressSpaceTag { get; private set; }

        public Lane7QuotaPolicy QuotaPolicy { get; private set; } =
            Lane7QuotaPolicy.Default;

        public ulong OwnershipEpoch { get; private set; }

        public ulong PolicyEpoch { get; private set; }

        public ulong TokenEpoch { get; private set; }

        public ulong CompletionEpoch { get; private set; }

        public ulong BackendBindingEpoch => HostEvidence.BackendBindingEpoch;

        public ulong PressureEpoch => HostEvidence.PressureEpoch;

        public Lane7Fault LastFault { get; private set; } = Lane7Fault.None;

        public Lane7PressureSnapshot LastPressure => HostEvidence.LastPressure;

        public Lane7HostOwnedEvidenceStore HostEvidence { get; }

        public Lane7AcceleratorHandleNamespace HandleNamespace { get; }

        public Lane7AcceleratorTokenNamespace TokenNamespace { get; }

        public Lane7AcceleratorCompletionRouter CompletionRouter { get; }

        public Lane7AcceleratorAdmissionPolicy AdmissionPolicy { get; }

        public int ActiveHandleCount => _handlesByValue.Count;

        public int ActiveVirtualTokenCount => _tokensByVirtualValue.Count;

        public ulong ConfigureOwnership(
            ushort executionDomainTag,
            ushort addressSpaceTag,
            bool enabled = true,
            Lane7QuotaPolicy? quotaPolicy = null)
        {
            if (enabled && executionDomainTag == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(executionDomainTag),
                    "Lane7 domain ownership requires a nonzero execution-domain tag.");
            }

            ExecutionDomainTag = enabled ? executionDomainTag : (ushort)0;
            AddressSpaceTag = enabled ? addressSpaceTag : (ushort)0;
            VirtualizationEnabled = enabled;
            QuotaPolicy = (quotaPolicy ?? Lane7QuotaPolicy.Default).Normalize();
            HostEvidence.ResetSchedulerWindow();
            return AdvanceOwnershipEpoch();
        }

        public Lane7VirtualHandle AllocateVirtualHandle(
            ushort ownerVirtualThreadId,
            AcceleratorDeviceId acceleratorId,
            Lane7VirtualCapability capabilities)
        {
            if (!TryAllocateVirtualHandle(
                    ownerVirtualThreadId,
                    acceleratorId,
                    capabilities,
                    out Lane7VirtualHandle handle,
                    out Lane7Fault fault))
            {
                throw new InvalidOperationException(fault.Message);
            }

            return handle;
        }

        public bool TryAllocateVirtualHandle(
            ushort ownerVirtualThreadId,
            AcceleratorDeviceId acceleratorId,
            Lane7VirtualCapability capabilities,
            out Lane7VirtualHandle handle,
            out Lane7Fault fault)
        {
            handle = default;
            if (!ValidateEnabled(out fault))
            {
                return false;
            }

            if (acceleratorId == 0 ||
                capabilities == Lane7VirtualCapability.None)
            {
                fault = RecordFault(Lane7Fault.Abort(
                    Lane7FaultKind.CapabilityDenied,
                    "Lane7 virtual accelerator handle allocation requires filtered virtual capabilities."));
                return false;
            }

            if (_handlesByValue.Count >= QuotaPolicy.MaxVirtualHandles)
            {
                fault = RecordFault(Lane7Fault.Replay(
                    Lane7FaultKind.QuotaExceeded,
                    "Lane7 virtual accelerator handle quota is exhausted."));
                ObservePressure(ownerVirtualThreadId, quotaExceeded: true);
                return false;
            }

            ulong value = AllocateVirtualHandleValue();
            handle = new Lane7VirtualHandle(
                ExecutionDomainTag,
                ownerVirtualThreadId,
                value,
                acceleratorId,
                capabilities,
                AdvanceOwnershipEpoch());
            _handlesByValue[value] = handle;
            _handleByOwner[(ExecutionDomainTag, ownerVirtualThreadId, acceleratorId)] = value;
            fault = Lane7Fault.None;
            return true;
        }

        public bool TryReleaseVirtualHandle(ulong virtualHandle)
        {
            if (!_handlesByValue.Remove(virtualHandle, out Lane7VirtualHandle handle))
            {
                return false;
            }

            _handleByOwner.Remove((handle.ExecutionDomainTag, handle.OwnerVirtualThreadId, handle.AcceleratorId));
            HostEvidence.RemoveBackendBinding(virtualHandle);
            List<ulong> tokenIds = new();
            foreach (KeyValuePair<ulong, Lane7VirtualToken> entry in _tokensByVirtualValue)
            {
                if (entry.Value.VirtualHandle == virtualHandle)
                {
                    tokenIds.Add(entry.Key);
                }
            }

            for (int index = 0; index < tokenIds.Count; index++)
            {
                ReleaseVirtualToken(tokenIds[index]);
            }

            AdvanceOwnershipEpoch();
            return true;
        }

        public bool TryGetVirtualHandle(
            ulong virtualHandle,
            out Lane7VirtualHandle handle) =>
            _handlesByValue.TryGetValue(virtualHandle, out handle);

        public bool TryFindVirtualHandle(
            ushort executionDomainTag,
            ushort ownerVirtualThreadId,
            AcceleratorDeviceId acceleratorId,
            out Lane7VirtualHandle handle)
        {
            if (_handleByOwner.TryGetValue(
                    (executionDomainTag, ownerVirtualThreadId, acceleratorId),
                    out ulong virtualHandle))
            {
                return _handlesByValue.TryGetValue(virtualHandle, out handle);
            }

            handle = default;
            return false;
        }

        public bool TryBindBackend(
            ulong virtualHandle,
            ulong backendGeneration,
            out Lane7BackendBinding binding,
            bool available = true,
            bool requiresRebind = false)
        {
            binding = default;
            if (!_handlesByValue.TryGetValue(virtualHandle, out Lane7VirtualHandle handle))
            {
                LastFault = Lane7Fault.Abort(
                    Lane7FaultKind.InvalidVirtualHandle,
                    "Lane7 backend binding requires an existing VM-local virtual accelerator handle.");
                return false;
            }

            if (!HostEvidence.TryBindBackend(
                handle.ExecutionDomainTag,
                handle.OwnerVirtualThreadId,
                virtualHandle,
                backendGeneration,
                available,
                requiresRebind,
                out binding))
            {
                LastFault = Lane7Fault.Abort(
                    Lane7FaultKind.BackendUnavailable,
                    "Lane7 backend binding evidence epoch is exhausted and cannot be reused.");
                return false;
            }

            return true;
        }

        public void DiscardBackendBindings() => HostEvidence.DiscardBackendBindings();

        public bool TryRebuildBackendBinding(ulong virtualHandle, ulong backendGeneration = 0) =>
            TryBindBackend(
                virtualHandle,
                backendGeneration,
                out _);

        public bool MarkBackendUnavailable(ulong virtualHandle) =>
            TryBindBackend(virtualHandle, backendGeneration: 0, out _, available: false);

        public bool MarkBackendRebindRequired(ulong virtualHandle) =>
            TryBindBackend(virtualHandle, backendGeneration: 0, out _, available: true, requiresRebind: true);

        public bool TryValidateGuestSubmit(
            AcceleratorCommandDescriptor descriptor,
            ushort executionDomainTag,
            ushort addressSpaceTag,
            out Lane7VirtualHandle handle,
            out Lane7Fault fault)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            handle = default;
            if (!ValidateOwnership(executionDomainTag, addressSpaceTag, descriptor.OwnerBinding.OwnerVirtualThreadId, out fault))
            {
                return false;
            }

            if (!TryFindVirtualHandle(
                    executionDomainTag,
                    descriptor.OwnerBinding.OwnerVirtualThreadId,
                    descriptor.AcceleratorId,
                    out handle))
            {
                fault = RecordFault(Lane7Fault.Abort(
                    Lane7FaultKind.InvalidVirtualHandle,
                    "Guest Lane7 submit requires a VM-local virtual accelerator handle bound to the descriptor owner."));
                return false;
            }

            if ((handle.Capabilities & Lane7VirtualCapability.Submit) == 0)
            {
                fault = RecordFault(Lane7Fault.Abort(
                    Lane7FaultKind.CapabilityDenied,
                    "Guest Lane7 submit is denied by the filtered virtual capability mask."));
                return false;
            }

            if (!TryValidateBackendUsable(handle.Value, out fault))
            {
                return false;
            }

            if (_tokensByVirtualValue.Count >= QuotaPolicy.MaxInflightTokens)
            {
                fault = RecordFault(Lane7Fault.Replay(
                    Lane7FaultKind.QuotaExceeded,
                    "Guest Lane7 submit hit transient VM-local token quota pressure."));
                ObservePressure(handle.OwnerVirtualThreadId, quotaExceeded: true);
                return false;
            }

            fault = Lane7Fault.None;
            return true;
        }

        public bool TryMapHostToken(
            Lane7VirtualHandle handle,
            ushort addressSpaceTag,
            AcceleratorToken token,
            out Lane7VirtualToken virtualToken,
            out Lane7Fault fault)
        {
            ArgumentNullException.ThrowIfNull(token);
            virtualToken = default;
            if (!handle.IsValid ||
                !_handlesByValue.ContainsKey(handle.Value))
            {
                fault = RecordFault(Lane7Fault.Abort(
                    Lane7FaultKind.InvalidVirtualHandle,
                    "Lane7 token virtualization requires a valid VM-local accelerator handle."));
                return false;
            }

            if (_tokensByVirtualValue.Count >= QuotaPolicy.MaxInflightTokens)
            {
                fault = RecordFault(Lane7Fault.Replay(
                    Lane7FaultKind.QuotaExceeded,
                    "Lane7 token virtualization quota is exhausted."));
                ObservePressure(handle.OwnerVirtualThreadId, quotaExceeded: true);
                return false;
            }

            ulong virtualTokenId = AllocateVirtualTokenValue();
            ulong tokenEpoch = AdvanceTokenEpoch();
            virtualToken = new Lane7VirtualToken(
                handle.ExecutionDomainTag,
                addressSpaceTag,
                handle.OwnerVirtualThreadId,
                handle.Value,
                virtualTokenId,
                tokenEpoch,
                AcceleratorTokenStatusWord.FromToken(token),
                CompletionEpoch);
            _tokensByVirtualValue[virtualTokenId] = virtualToken;
            if (!HostEvidence.TryBindToken(virtualToken, token.Handle))
            {
                _tokensByVirtualValue.Remove(virtualTokenId);
                fault = RecordFault(Lane7Fault.Abort(
                    Lane7FaultKind.InvalidVirtualToken,
                    "Lane7 host-owned token evidence store denied native token binding."));
                return false;
            }

            fault = Lane7Fault.None;
            return true;
        }

        public bool TryResolveHostToken(
            ulong virtualTokenId,
            ushort executionDomainTag,
            ushort addressSpaceTag,
            ushort ownerVirtualThreadId,
            out AcceleratorTokenHandle hostHandle,
            out Lane7VirtualToken virtualToken,
            out Lane7Fault fault)
        {
            hostHandle = AcceleratorTokenHandle.Invalid;
            virtualToken = default;
            if (!_tokensByVirtualValue.TryGetValue(virtualTokenId, out virtualToken) ||
                virtualToken.ExecutionDomainTag != executionDomainTag ||
                (virtualToken.AddressSpaceTag != 0 && virtualToken.AddressSpaceTag != addressSpaceTag) ||
                virtualToken.OwnerVirtualThreadId != ownerVirtualThreadId)
            {
                fault = RecordFault(Lane7Fault.Abort(
                    Lane7FaultKind.InvalidVirtualToken,
                    "Guest Lane7 token lookup requires a virtual token owned by the current domain/address-space/VT."));
                return false;
            }

            if (!HostEvidence.TryResolveHostToken(virtualTokenId, out hostHandle))
            {
                fault = RecordFault(Lane7Fault.Abort(
                    Lane7FaultKind.InvalidVirtualToken,
                    "Lane7 native token evidence is not materialized and must be rebuilt by host runtime."));
                return false;
            }

            fault = Lane7Fault.None;
            return true;
        }

        public bool TryResolveVirtualTokenForHost(
            AcceleratorTokenHandle hostHandle,
            out Lane7VirtualToken virtualToken)
        {
            return HostEvidence.TryResolveVirtualTokenForHost(
                hostHandle,
                _tokensByVirtualValue,
                out virtualToken);
        }

        public bool UpdateVirtualTokenStatus(
            AcceleratorToken token,
            out Lane7VirtualToken virtualToken)
        {
            ArgumentNullException.ThrowIfNull(token);
            if (!TryResolveVirtualTokenForHost(token.Handle, out virtualToken))
            {
                return false;
            }

            virtualToken = virtualToken with
            {
                Status = AcceleratorTokenStatusWord.FromToken(token),
                CompletionEpoch = CompletionEpoch,
            };
            _tokensByVirtualValue[virtualToken.VirtualTokenId] = virtualToken;
            AdvanceTokenEpoch();
            return true;
        }

        public bool ReleaseVirtualToken(ulong virtualTokenId)
        {
            if (!_tokensByVirtualValue.Remove(virtualTokenId, out Lane7VirtualToken token))
            {
                return false;
            }

            HostEvidence.ReleaseToken(virtualTokenId);

            AdvanceTokenEpoch();
            return token.IsValid;
        }

        public bool TryBuildCompletion(
            AcceleratorToken token,
            ushort sourceOpcode,
            ulong runtimeQueueSequence,
            out LaneCompletionDescriptor completion,
            out Lane7Fault fault)
        {
            ArgumentNullException.ThrowIfNull(token);
            completion = default;
            if (!TryResolveVirtualTokenForHost(token.Handle, out Lane7VirtualToken virtualToken))
            {
                fault = RecordFault(Lane7Fault.Abort(
                    Lane7FaultKind.InvalidVirtualToken,
                    "Lane7 completion routing requires a VM-local virtual token mapping."));
                return false;
            }

            UpdateVirtualTokenStatus(token, out virtualToken);
            ulong completionEpoch = AdvanceCompletionEpoch();
            AcceleratorCommandDescriptor descriptor = token.Descriptor;
            completion = new LaneCompletionDescriptor(
                LaneCompletionSourceKind.ExternalAcceleratorLane7,
                LaneIndex: 7,
                sourceOpcode,
                virtualToken.OwnerVirtualThreadId,
                virtualToken.ExecutionDomainTag,
                virtualToken.AddressSpaceTag,
                virtualToken.VirtualTokenId,
                virtualToken.TokenEpoch,
                descriptor.Identity.DescriptorIdentityHash,
                descriptor.Identity.NormalizedFootprintHash,
                descriptor.OwnerBinding.DomainTag,
                runtimeQueueSequence,
                (ulong)(byte)token.State,
                token.State == AcceleratorTokenState.DeviceComplete,
                completionEpoch);
            fault = Lane7Fault.None;
            return true;
        }

        public ulong BuildFilteredCapabilityWord(Lane7VirtualHandle handle)
        {
            ulong packed = 0;
            packed |= 1UL;
            packed |= ((ulong)handle.OwnerVirtualThreadId & 0xFFFFUL) << 16;
            packed |= ((ulong)handle.Capabilities & 0xFFFF_FFFFUL) << 32;
            return packed;
        }

        public Lane7PressureSnapshot ObserveSubmitPollPressure(
            ushort ownerVirtualThreadId)
        {
            return HostEvidence.ObserveSubmitPollPressure(
                ExecutionDomainTag,
                AddressSpaceTag,
                ownerVirtualThreadId,
                _tokensByVirtualValue.Count,
                QuotaPolicy);
        }

        public void ResetPressureWindow()
        {
            HostEvidence.ResetSchedulerWindow();
            ObservePressure(ownerVirtualThreadId: 0, quotaExceeded: false);
        }

        public Lane7HostEvidenceRestoreResult PrepareHostEvidenceForRestore(
            EvidencePolicyDescriptor evidencePolicy) =>
            HostEvidence.PrepareForRestore(evidencePolicy);

        public Lane7HostEvidenceRestoreResult RebuildHostTokenAfterRestore(
            Lane7VirtualToken virtualToken,
            AcceleratorTokenHandle hostHandle) =>
            HostEvidence.RebuildTokenAfterRestore(virtualToken, hostHandle);

        public Lane7HostEvidenceRestoreResult RebuildBackendBindingAfterRestore(
            ulong virtualHandle,
            ulong backendGeneration,
            out Lane7BackendBinding binding)
        {
            binding = default;
            if (!_handlesByValue.TryGetValue(virtualHandle, out Lane7VirtualHandle handle))
            {
                return Lane7HostEvidenceRestoreResult.Rejected(
                    "Lane7 backend evidence rebuild requires an existing VM-local virtual accelerator handle.");
            }

            return HostEvidence.RebuildBackendAfterRestore(
                handle.ExecutionDomainTag,
                handle.OwnerVirtualThreadId,
                virtualHandle,
                backendGeneration,
                out binding);
        }

        private bool TryValidateBackendUsable(ulong virtualHandle, out Lane7Fault fault)
        {
            if (!HostEvidence.TryResolveBackendBinding(virtualHandle, out Lane7BackendBinding binding))
            {
                fault = RecordFault(Lane7Fault.Fallback(
                    Lane7FaultKind.FirstUseBindingRequired,
                    "Lane7 backend binding is not materialized; runtime fallback must bind or reject."));
                return false;
            }

            if (binding.RequiresRebind)
            {
                fault = RecordFault(Lane7Fault.Fallback(
                    Lane7FaultKind.BackendRebindRequired,
                    "Lane7 backend binding is stale after migration/rebind epoch change."));
                return false;
            }

            if (!binding.Available)
            {
                fault = RecordFault(Lane7Fault.Fallback(
                    Lane7FaultKind.BackendUnavailable,
                    "Lane7 backend is unavailable for the VM-local virtual accelerator handle."));
                return false;
            }

            fault = Lane7Fault.None;
            return true;
        }

        private bool ValidateEnabled(out Lane7Fault fault)
        {
            if (!VirtualizationEnabled || ExecutionDomainTag == 0)
            {
                fault = RecordFault(Lane7Fault.Abort(
                    Lane7FaultKind.VirtualizationDisabled,
                    "Lane7 domain virtualization is disabled or lacks nonzero execution-domain ownership."));
                return false;
            }

            fault = Lane7Fault.None;
            return true;
        }

        private bool ValidateOwnership(
            ushort executionDomainTag,
            ushort addressSpaceTag,
            ushort ownerVirtualThreadId,
            out Lane7Fault fault)
        {
            if (!ValidateEnabled(out fault))
            {
                return false;
            }

            if (executionDomainTag == 0 ||
                executionDomainTag != ExecutionDomainTag ||
                (AddressSpaceTag != 0 && addressSpaceTag != AddressSpaceTag))
            {
                fault = RecordFault(Lane7Fault.Abort(
                    Lane7FaultKind.OwnershipMismatch,
                    "Guest Lane7 operation domain/address-space tags do not match Lane7 ownership."));
                return false;
            }

            if (ownerVirtualThreadId > byte.MaxValue)
            {
                fault = RecordFault(Lane7Fault.Abort(
                    Lane7FaultKind.OwnershipMismatch,
                    "Guest Lane7 owner VT exceeds the VM-local routable VT namespace."));
                return false;
            }

            fault = Lane7Fault.None;
            return true;
        }

        private Lane7PressureSnapshot ObservePressure(
            ushort ownerVirtualThreadId,
            bool quotaExceeded)
        {
            return HostEvidence.ObserveSchedulerPressure(
                ExecutionDomainTag,
                AddressSpaceTag,
                ownerVirtualThreadId,
                _tokensByVirtualValue.Count,
                QuotaPolicy,
                quotaExceeded);
        }

        private Lane7Fault RecordFault(Lane7Fault fault)
        {
            LastFault = fault;
            return fault;
        }

        private ulong AllocateVirtualHandleValue()
        {
            while (true)
            {
                ulong value = _nextVirtualHandle++;
                if (_nextVirtualHandle == 0)
                {
                    _nextVirtualHandle = 0x7000_0000_0000_0001UL;
                }

                if (value != 0 && !_handlesByValue.ContainsKey(value))
                {
                    return value;
                }
            }
        }

        private ulong AllocateVirtualTokenValue()
        {
            while (true)
            {
                ulong value = _nextVirtualToken++;
                if (_nextVirtualToken == 0)
                {
                    _nextVirtualToken = 0x7100_0000_0000_0001UL;
                }

                if (value != 0 && !_tokensByVirtualValue.ContainsKey(value))
                {
                    return value;
                }
            }
        }

        private ulong AdvanceOwnershipEpoch()
        {
            unchecked
            {
                OwnershipEpoch++;
                if (OwnershipEpoch == 0)
                {
                    OwnershipEpoch = 1;
                }
            }

            return OwnershipEpoch;
        }

        private ulong AdvanceTokenEpoch()
        {
            unchecked
            {
                TokenEpoch++;
                if (TokenEpoch == 0)
                {
                    TokenEpoch = 1;
                }
            }

            return TokenEpoch;
        }

        private ulong AdvanceCompletionEpoch()
        {
            unchecked
            {
                CompletionEpoch++;
                if (CompletionEpoch == 0)
                {
                    CompletionEpoch = 1;
                }
            }

            return CompletionEpoch;
        }

    }
}
