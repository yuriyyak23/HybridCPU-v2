using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Conflicts;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Telemetry;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

public sealed class AcceleratorTokenStore
{
    private const ulong HandleSalt = 0xA7C3_5D19_E2B4_6F80UL;
    private const ulong HandleStride = 0x9E37_79B9_7F4A_7C15UL;

    private readonly Dictionary<ulong, AcceleratorToken> _tokens = new();
    private readonly AcceleratorTelemetry? _telemetry;
    private readonly ExternalAcceleratorFeatureSwitch _featureSwitch;
    private ulong _nextTokenId = 1;
    private ulong _handleGeneration = 1;

    public AcceleratorTokenStore(
        AcceleratorTelemetry? telemetry = null,
        ExternalAcceleratorFeatureSwitch? featureSwitch = null)
    {
        _telemetry = telemetry;
        _featureSwitch = featureSwitch ?? ExternalAcceleratorFeatureSwitch.Enabled;
    }

    public int Count => _tokens.Count;

    public AcceleratorTokenAdmissionResult Create(
        AcceleratorCommandDescriptor descriptor,
        AcceleratorCapabilityAcceptanceResult capabilityAcceptance,
        AcceleratorGuardEvidence? submitGuardEvidence,
        AcceleratorTokenAdmissionRejectPolicy rejectPolicy =
            AcceleratorTokenAdmissionRejectPolicy.NonTrappingReject,
        ExternalAcceleratorConflictManager? conflictManager = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(capabilityAcceptance);

        if (!AcceleratorOwnerDomainGuard.Default.IsDescriptorGuardBacked(
                descriptor,
                out string descriptorGuardMessage))
        {
            return RecordSubmitResult(
                AcceleratorTokenAdmissionResult.Reject(
                    AcceleratorTokenFaultCode.DescriptorNotGuardBacked,
                    descriptorGuardMessage,
                    descriptor.OwnerGuardDecision,
                    rejectPolicy));
        }

        if (!IsCapabilityGuardBackedForDescriptor(
                capabilityAcceptance,
                descriptor,
                out string capabilityMessage))
        {
            return RecordSubmitResult(
                AcceleratorTokenAdmissionResult.Reject(
                    capabilityAcceptance.IsAccepted
                        ? AcceleratorTokenFaultCode.CapabilityNotAccepted
                        : AcceleratorTokenFaultCode.CapabilityRejected,
                    capabilityMessage,
                    capabilityAcceptance.GuardDecision,
                    rejectPolicy));
        }

        AcceleratorGuardDecision submitGuardDecision =
            AcceleratorOwnerDomainGuard.Default.EnsureBeforeSubmit(
                descriptor,
                submitGuardEvidence);
        if (!submitGuardDecision.IsAllowed)
        {
            return RecordSubmitResult(
                AcceleratorTokenAdmissionResult.Reject(
                    MapGuardFault(submitGuardDecision.Fault),
                    submitGuardDecision.Message,
                    submitGuardDecision,
                    rejectPolicy));
        }

        if (!_featureSwitch.SubmitAdmissionEnabled)
        {
            return RecordSubmitResult(
                AcceleratorTokenAdmissionResult.Reject(
                    AcceleratorTokenFaultCode.SubmitAdmissionRejected,
                    "L7-SDC submit admission is disabled by rollback feature switch after descriptor, capability, and guard validation; no token was created.",
                    submitGuardDecision,
                    rejectPolicy));
        }

        ulong tokenId = AllocateTokenId();
        AcceleratorTokenHandle handle = AllocateHandle(tokenId);
        var token = new AcceleratorToken(
            tokenId,
            handle,
            descriptor,
            capabilityAcceptance,
            submitGuardDecision,
            _telemetry);
        _tokens.Add(handle.Value, token);

        if (conflictManager is not null)
        {
            AcceleratorConflictDecision reservation =
                conflictManager.TryReserveOnSubmit(
                    token,
                    submitGuardEvidence);
            if (reservation.IsRejected)
            {
                _tokens.Remove(handle.Value);
                return RecordSubmitResult(
                    AcceleratorTokenAdmissionResult.Reject(
                        reservation.TokenFaultCode == AcceleratorTokenFaultCode.None
                            ? AcceleratorTokenFaultCode.ConflictRejected
                            : reservation.TokenFaultCode,
                        reservation.Message,
                        reservation.GuardDecision,
                        rejectPolicy));
            }
        }

        return RecordSubmitResult(AcceleratorTokenAdmissionResult.Accepted(token));
    }

    public AcceleratorTokenLookupResult TryLookup(
        AcceleratorTokenHandle handle,
        AcceleratorGuardEvidence? currentGuardEvidence,
        AcceleratorTokenLookupIntent intent = AcceleratorTokenLookupIntent.Observe)
    {
        if (!handle.IsValid)
        {
            return RecordLookupResult(AcceleratorTokenLookupResult.Rejected(
                intent,
                AcceleratorTokenState.Faulted,
                AcceleratorTokenFaultCode.InvalidHandle,
                "L7-SDC token handle zero is invalid and cannot authorize lookup."));
        }

        if (!_tokens.TryGetValue(handle.Value, out AcceleratorToken? token))
        {
            return RecordLookupResult(AcceleratorTokenLookupResult.Rejected(
                intent,
                AcceleratorTokenState.Faulted,
                AcceleratorTokenFaultCode.InvalidHandle,
                "L7-SDC token handle is unknown, stale, or forged."));
        }

        AcceleratorGuardDecision guardDecision =
            AcceleratorOwnerDomainGuard.Default.ValidateMappingEpoch(
                token.SubmitGuardDecision,
                currentGuardEvidence);
        if (!guardDecision.IsAllowed)
        {
            return RecordLookupResult(AcceleratorTokenLookupResult.Rejected(
                intent,
                token.State,
                MapGuardFault(guardDecision.Fault),
                "L7-SDC token lookup requires current guard-plane owner/domain and epoch authority. " +
                guardDecision.Message,
                token,
                guardDecision));
        }

        return AcceleratorTokenLookupResult.Allowed(
            intent,
            token,
            guardDecision,
            "L7-SDC token lookup accepted after owner/domain and mapping/IOMMU epoch revalidation.");
    }

    public AcceleratorTokenLookupResult TryPoll(
        AcceleratorTokenHandle handle,
        AcceleratorGuardEvidence? currentGuardEvidence) =>
        TryLookup(handle, currentGuardEvidence, AcceleratorTokenLookupIntent.Observe);

    public AcceleratorTokenLookupResult Poll(
        AcceleratorTokenHandle handle,
        AcceleratorGuardEvidence? currentGuardEvidence) =>
        TryPoll(handle, currentGuardEvidence);

    public AcceleratorTokenLookupResult TryWait(
        AcceleratorTokenHandle handle,
        AcceleratorGuardEvidence? currentGuardEvidence,
        AcceleratorWaitPolicy waitPolicy)
    {
        AcceleratorTokenLookupResult lookup =
            TryLookup(handle, currentGuardEvidence, AcceleratorTokenLookupIntent.Wait);
        if (!lookup.IsAllowed || lookup.Token is null)
        {
            return lookup;
        }

        AcceleratorToken token = lookup.Token;
        if (token.IsTerminal ||
            token.State is AcceleratorTokenState.DeviceComplete
                or AcceleratorTokenState.CommitPending)
        {
            return AcceleratorTokenLookupResult.Allowed(
                AcceleratorTokenLookupIntent.Wait,
                token,
                lookup.GuardDecision!.Value,
                "ACCEL_WAIT observed a final or device-complete token state after fresh guard revalidation; no staged write was published.");
        }

        if (!waitPolicy.TimeoutExpired)
        {
            return AcceleratorTokenLookupResult.Allowed(
                AcceleratorTokenLookupIntent.Wait,
                token,
                lookup.GuardDecision!.Value,
                "ACCEL_WAIT observed a non-final token state after fresh guard revalidation; no direct device write was published.");
        }

        if (waitPolicy.MarkTokenTimedOutOnTimeout)
        {
            AcceleratorTokenTransition timeout =
                token.MarkTimedOut(currentGuardEvidence!);
            if (timeout.Rejected)
            {
                return AcceleratorTokenLookupResult.Rejected(
                    AcceleratorTokenLookupIntent.Wait,
                    token.State,
                    timeout.FaultCode,
                    timeout.Message,
                    token,
                    lookup.GuardDecision);
            }

            return AcceleratorTokenLookupResult.AllowedWithStatus(
                AcceleratorTokenLookupIntent.Wait,
                token,
                lookup.GuardDecision!.Value,
                AcceleratorTokenStatusWord.FromToken(token),
                AcceleratorTokenFaultCode.TimedOut,
                sideEffectsAllowed: true,
                $"ACCEL_WAIT timeout policy marked the guarded token TimedOut after {waitPolicy.TimeoutTicks} tick(s); no commit authority was granted.");
        }

        AcceleratorTokenStatusWord timeoutStatus = new(
            token.State,
            AcceleratorTokenFaultCode.TimedOut,
            AcceleratorTokenStatusFlags.ModelOnly |
            AcceleratorTokenStatusFlags.TimeoutObserved,
            token.StatusSequence);
        return AcceleratorTokenLookupResult.AllowedWithStatus(
            AcceleratorTokenLookupIntent.Wait,
            token,
            lookup.GuardDecision!.Value,
            timeoutStatus,
            AcceleratorTokenFaultCode.TimedOut,
            sideEffectsAllowed: false,
            $"ACCEL_WAIT observed timeout after {waitPolicy.TimeoutTicks} tick(s); token state was not promoted and staged writes remain invisible.");
    }

    public AcceleratorTokenLookupResult Wait(
        AcceleratorTokenHandle handle,
        AcceleratorGuardEvidence? currentGuardEvidence) =>
        TryWait(handle, currentGuardEvidence, AcceleratorWaitPolicy.ObserveOnly);

    public AcceleratorTokenLookupResult Fence(
        AcceleratorTokenHandle handle,
        AcceleratorGuardEvidence? currentGuardEvidence) =>
        TryLookup(handle, currentGuardEvidence, AcceleratorTokenLookupIntent.Fence);

    public AcceleratorTokenLookupResult TryCancel(
        AcceleratorTokenHandle handle,
        AcceleratorGuardEvidence? currentGuardEvidence,
        AcceleratorCancelPolicy cancelPolicy)
    {
        AcceleratorTokenLookupResult lookup =
            TryLookup(handle, currentGuardEvidence, AcceleratorTokenLookupIntent.Cancel);
        if (!lookup.IsAllowed || lookup.Token is null)
        {
            return lookup;
        }

        AcceleratorToken token = lookup.Token;
        if (token.IsTerminal)
        {
            return AcceleratorTokenLookupResult.Allowed(
                AcceleratorTokenLookupIntent.Cancel,
                token,
                lookup.GuardDecision!.Value,
                "ACCEL_CANCEL observed an already-terminal token after guard revalidation; no commit authority was granted.");
        }

        if (token.State is AcceleratorTokenState.DeviceComplete
            or AcceleratorTokenState.CommitPending)
        {
            return AcceleratorTokenLookupResult.Rejected(
                AcceleratorTokenLookupIntent.Cancel,
                token.State,
                AcceleratorTokenFaultCode.CommitCoordinatorRequired,
                "ACCEL_CANCEL cannot discard DeviceComplete or CommitPending staged-write obligations; Phase 08 commit coordinator or a guarded fault path must resolve them.",
                token,
                lookup.GuardDecision);
        }

        if (token.State == AcceleratorTokenState.Running)
        {
            if (cancelPolicy.RunningDisposition == AcceleratorRunningCancelDisposition.Reject)
            {
                return AcceleratorTokenLookupResult.Rejected(
                    AcceleratorTokenLookupIntent.Cancel,
                    token.State,
                    AcceleratorTokenFaultCode.CancelRejected,
                    "ACCEL_CANCEL rejected a running token under non-cooperative cancel policy; cancellation did not grant commit authority.",
                    token,
                    lookup.GuardDecision);
            }

            if (cancelPolicy.RunningDisposition == AcceleratorRunningCancelDisposition.Fault)
            {
                AcceleratorTokenTransition faulted =
                    token.MarkFaulted(
                        AcceleratorTokenFaultCode.CancelRejected,
                        currentGuardEvidence!);
                return faulted.Succeeded
                    ? AcceleratorTokenLookupResult.AllowedWithStatus(
                        AcceleratorTokenLookupIntent.Cancel,
                        token,
                        lookup.GuardDecision!.Value,
                        AcceleratorTokenStatusWord.FromToken(token),
                        AcceleratorTokenFaultCode.CancelRejected,
                        sideEffectsAllowed: true,
                        "ACCEL_CANCEL faulted a running token under explicit guarded policy; no staged writes were committed.")
                    : AcceleratorTokenLookupResult.Rejected(
                        AcceleratorTokenLookupIntent.Cancel,
                        token.State,
                        faulted.FaultCode,
                        faulted.Message,
                        token,
                        lookup.GuardDecision);
            }
        }

        AcceleratorTokenTransition transition =
            token.MarkCanceled(currentGuardEvidence!);
        return transition.Succeeded
            ? AcceleratorTokenLookupResult.Allowed(
                AcceleratorTokenLookupIntent.Cancel,
                token,
                lookup.GuardDecision!.Value,
                transition.Message)
            : AcceleratorTokenLookupResult.Rejected(
                AcceleratorTokenLookupIntent.Cancel,
                token.State,
                transition.FaultCode,
                transition.Message,
                token,
                lookup.GuardDecision);
    }

    public AcceleratorTokenLookupResult TryCancel(
        AcceleratorTokenHandle handle,
        AcceleratorGuardEvidence? currentGuardEvidence) =>
        TryCancel(handle, currentGuardEvidence, AcceleratorCancelPolicy.Cooperative);

    public AcceleratorTokenLookupResult Cancel(
        AcceleratorTokenHandle handle,
        AcceleratorGuardEvidence? currentGuardEvidence) =>
        TryCancel(handle, currentGuardEvidence, AcceleratorCancelPolicy.Cooperative);

    public AcceleratorTokenLookupResult TryCommitPublication(
        AcceleratorTokenHandle handle,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        AcceleratorTokenLookupResult lookup =
            TryLookup(handle, currentGuardEvidence, AcceleratorTokenLookupIntent.CommitPublication);
        if (!lookup.IsAllowed || lookup.Token is null)
        {
            return lookup;
        }

        AcceleratorTokenTransition transition =
            lookup.Token.MarkCommitted(currentGuardEvidence!);
        return AcceleratorTokenLookupResult.Rejected(
            AcceleratorTokenLookupIntent.CommitPublication,
            lookup.Token.State,
            transition.FaultCode == AcceleratorTokenFaultCode.None
                ? AcceleratorTokenFaultCode.MemoryPublicationForbidden
                : transition.FaultCode,
            transition.Message,
            lookup.Token,
            lookup.GuardDecision);
    }

    internal static AcceleratorTokenFaultCode MapGuardFault(
        AcceleratorGuardFault guardFault)
    {
        return guardFault switch
        {
            AcceleratorGuardFault.None => AcceleratorTokenFaultCode.None,
            AcceleratorGuardFault.MissingGuardEvidence => AcceleratorTokenFaultCode.MissingGuardEvidence,
            AcceleratorGuardFault.MappingEpochDrift => AcceleratorTokenFaultCode.MappingEpochDrift,
            AcceleratorGuardFault.IommuDomainEpochDrift => AcceleratorTokenFaultCode.IommuDomainEpochDrift,
            AcceleratorGuardFault.EvidenceSourceNotAuthority
                => AcceleratorTokenFaultCode.TokenHandleNotAuthority,
            AcceleratorGuardFault.CapabilityGuardMissing
                => AcceleratorTokenFaultCode.CapabilityNotAccepted,
            AcceleratorGuardFault.OwnerMismatch or
            AcceleratorGuardFault.DomainMismatch or
            AcceleratorGuardFault.DescriptorOwnerBindingMismatch or
            AcceleratorGuardFault.RejectedGuard or
            AcceleratorGuardFault.InvalidOwnerCompletion
                => AcceleratorTokenFaultCode.OwnerDomainRejected,
            _ => AcceleratorTokenFaultCode.Unknown
        };
    }

    private static bool IsCapabilityGuardBackedForDescriptor(
        AcceleratorCapabilityAcceptanceResult capabilityAcceptance,
        AcceleratorCommandDescriptor descriptor,
        out string message)
    {
        if (!capabilityAcceptance.IsAccepted)
        {
            message = string.IsNullOrWhiteSpace(capabilityAcceptance.RejectReason)
                ? "L7-SDC capability acceptance rejected; registry metadata cannot authorize token creation."
                : capabilityAcceptance.RejectReason;
            return false;
        }

        AcceleratorGuardDecision guardDecision = capabilityAcceptance.GuardDecision;
        if (!guardDecision.IsAllowed ||
            guardDecision.Evidence?.Source != AcceleratorGuardEvidenceSource.GuardPlane ||
            guardDecision.LegalityDecision.AuthoritySource != LegalityAuthoritySource.GuardPlane ||
            guardDecision.LegalityDecision.AttemptedReplayCertificateReuse)
        {
            message =
                "L7-SDC capability acceptance must be guard-backed; metadata-only registry success is not token authority.";
            return false;
        }

        if (guardDecision.DescriptorOwnerBinding is null ||
            !guardDecision.DescriptorOwnerBinding.Equals(descriptor.OwnerBinding))
        {
            message =
                "L7-SDC capability guard decision does not match the descriptor owner binding.";
            return false;
        }

        message = "L7-SDC capability acceptance is guard-backed for token admission.";
        return true;
    }

    private ulong AllocateTokenId()
    {
        ulong tokenId = _nextTokenId++;
        if (_nextTokenId == 0)
        {
            _nextTokenId = 1;
        }

        return tokenId;
    }

    private AcceleratorTokenHandle AllocateHandle(ulong tokenId)
    {
        while (true)
        {
            ulong generation = _handleGeneration++;
            ulong value = unchecked(
                HandleSalt ^
                (tokenId * HandleStride) ^
                (generation << 17) ^
                (generation >> 11));
            if (value != 0 && !_tokens.ContainsKey(value))
            {
                return AcceleratorTokenHandle.FromOpaqueValue(value);
            }
        }
    }

    private AcceleratorTokenAdmissionResult RecordSubmitResult(
        AcceleratorTokenAdmissionResult result)
    {
        _telemetry?.RecordSubmit(
            result.IsAccepted,
            result.FaultCode,
            result.GuardDecision,
            result.Message);
        if (result.IsAccepted && result.Token is not null)
        {
            _telemetry?.RecordTokenTransition(
                result.Token,
                AcceleratorTokenState.Created,
                AcceleratorTokenState.Created,
                AcceleratorTokenFaultCode.None,
                "L7-SDC token was created by guarded submit admission.");
        }

        return result;
    }

    private AcceleratorTokenLookupResult RecordLookupResult(
        AcceleratorTokenLookupResult result)
    {
        if (result.IsRejected)
        {
            _telemetry?.RecordGuardReject(result.GuardDecision, result.Message);
        }

        return result;
    }
}
