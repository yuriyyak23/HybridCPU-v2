using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Telemetry;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Queues;

public interface IAcceleratorDevice
{
    string DeviceId { get; }

    bool IsAvailable { get; }
}

public interface IAcceleratorCommandQueue
{
    int Capacity { get; }

    int Count { get; }

    IAcceleratorDevice Device { get; }

    int QueueFullRejectCount { get; }

    int DeviceBusyRejectCount { get; }

    int ModelRejectCount { get; }

    AcceleratorQueueAdmissionResult TryEnqueue(
        AcceleratorQueueAdmissionRequest request,
        AcceleratorGuardEvidence? currentGuardEvidence);

    bool TryDequeueReady(
        AcceleratorGuardEvidence? currentGuardEvidence,
        out AcceleratorQueuedCommand? command,
        out AcceleratorQueueAdmissionResult result);
}

public sealed class AcceleratorDevice : IAcceleratorDevice
{
    public AcceleratorDevice(string deviceId, bool isAvailable = true)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("Device id is required.", nameof(deviceId));
        }

        DeviceId = deviceId;
        IsAvailable = isAvailable;
    }

    public string DeviceId { get; }

    public bool IsAvailable { get; set; }
}

public sealed record AcceleratorQueueAdmissionRequest
{
    public required AcceleratorCommandDescriptor Descriptor { get; init; }

    public required AcceleratorCapabilityAcceptanceResult CapabilityAcceptance { get; init; }

    public required AcceleratorTokenAdmissionResult TokenAdmission { get; init; }

    // Placeholder evidence for this explicit queue model only; not a global
    // CPU load/store conflict-manager acceptance signal.
    public bool ConflictAccepted { get; init; } = true;

    public string ConflictEvidenceMessage { get; init; } =
        "Phase 07 carries explicit placeholder conflict acceptance evidence; Phase 10 conflict manager is not implemented.";
}

public sealed record AcceleratorQueuedCommand
{
    internal AcceleratorQueuedCommand(
        ulong queueSequence,
        AcceleratorQueueAdmissionRequest request,
        AcceleratorToken token,
        AcceleratorGuardDecision queueGuardDecision)
    {
        QueueSequence = queueSequence;
        Descriptor = request.Descriptor;
        CapabilityAcceptance = request.CapabilityAcceptance;
        TokenAdmission = request.TokenAdmission;
        Token = token;
        QueueGuardDecision = queueGuardDecision;
        ConflictAccepted = request.ConflictAccepted;
        ConflictEvidenceMessage = request.ConflictEvidenceMessage;
    }

    public ulong QueueSequence { get; }

    public AcceleratorCommandDescriptor Descriptor { get; }

    public AcceleratorCapabilityAcceptanceResult CapabilityAcceptance { get; }

    public AcceleratorTokenAdmissionResult TokenAdmission { get; }

    public AcceleratorToken Token { get; }

    public AcceleratorGuardDecision QueueGuardDecision { get; }

    public bool ConflictAccepted { get; }

    public string ConflictEvidenceMessage { get; }

    public bool CanPublishArchitecturalMemory => false;

    public bool CanPublishException => false;
}

public sealed record AcceleratorQueueAdmissionResult
{
    private AcceleratorQueueAdmissionResult(
        bool isAccepted,
        AcceleratorQueuedCommand? command,
        AcceleratorTokenFaultCode faultCode,
        AcceleratorGuardDecision? guardDecision,
        int queueFullRejectCount,
        int deviceBusyRejectCount,
        int modelRejectCount,
        string message)
    {
        IsAccepted = isAccepted;
        Command = command;
        FaultCode = faultCode;
        GuardDecision = guardDecision;
        QueueFullRejectCount = queueFullRejectCount;
        DeviceBusyRejectCount = deviceBusyRejectCount;
        ModelRejectCount = modelRejectCount;
        Message = message;
    }

    public bool IsAccepted { get; }

    public bool IsRejected => !IsAccepted;

    public AcceleratorQueuedCommand? Command { get; }

    public AcceleratorTokenFaultCode FaultCode { get; }

    public AcceleratorGuardDecision? GuardDecision { get; }

    public int QueueFullRejectCount { get; }

    public int DeviceBusyRejectCount { get; }

    public int ModelRejectCount { get; }

    public string Message { get; }

    public bool CanPublishArchitecturalMemory => false;

    public bool CanPublishException => false;

    public bool UserVisiblePublicationAllowed => false;

    public static AcceleratorQueueAdmissionResult Accepted(
        AcceleratorQueuedCommand command,
        AcceleratorGuardDecision guardDecision,
        int queueFullRejectCount,
        int deviceBusyRejectCount,
        int modelRejectCount)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new AcceleratorQueueAdmissionResult(
            isAccepted: true,
            command,
            AcceleratorTokenFaultCode.None,
            guardDecision,
            queueFullRejectCount,
            deviceBusyRejectCount,
            modelRejectCount,
            "L7-SDC queue admission accepted after descriptor, capability, token, owner/domain, epoch, capacity, and device availability checks.");
    }

    public static AcceleratorQueueAdmissionResult Reject(
        AcceleratorTokenFaultCode faultCode,
        AcceleratorGuardDecision? guardDecision,
        int queueFullRejectCount,
        int deviceBusyRejectCount,
        int modelRejectCount,
        string message)
    {
        if (faultCode == AcceleratorTokenFaultCode.None)
        {
            throw new ArgumentException(
                "Rejected L7-SDC queue admission requires a fault code.",
                nameof(faultCode));
        }

        return new AcceleratorQueueAdmissionResult(
            isAccepted: false,
            command: null,
            faultCode,
            guardDecision,
            queueFullRejectCount,
            deviceBusyRejectCount,
            modelRejectCount,
            message);
    }
}

public sealed class AcceleratorCommandQueue : IAcceleratorCommandQueue
{
    private readonly Queue<AcceleratorQueuedCommand> _commands = new();
    private readonly AcceleratorTelemetry? _telemetry;
    private ulong _nextQueueSequence = 1;

    public AcceleratorCommandQueue(
        int capacity,
        IAcceleratorDevice device,
        AcceleratorTelemetry? telemetry = null)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                "L7-SDC accelerator command queue capacity must be positive.");
        }

        Capacity = capacity;
        Device = device ?? throw new ArgumentNullException(nameof(device));
        _telemetry = telemetry;
    }

    public int Capacity { get; }

    public int Count => _commands.Count;

    public IAcceleratorDevice Device { get; }

    public int QueueFullRejectCount { get; private set; }

    public int DeviceBusyRejectCount { get; private set; }

    public int ModelRejectCount { get; private set; }

    public AcceleratorQueueAdmissionResult TryEnqueue(
        AcceleratorQueueAdmissionRequest request,
        AcceleratorGuardEvidence? currentGuardEvidence)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryValidateAdmissionRequest(
                request,
                currentGuardEvidence,
                out AcceleratorToken? token,
                out AcceleratorGuardDecision guardDecision,
                out AcceleratorQueueAdmissionResult? reject))
        {
            return reject!;
        }

        if (!Device.IsAvailable)
        {
            DeviceBusyRejectCount++;
            AcceleratorQueueAdmissionResult deviceBusy = Reject(
                AcceleratorTokenFaultCode.DeviceBusy,
                guardDecision,
                $"L7-SDC queue admission rejected because device '{Device.DeviceId}' is busy after guard-backed admission checks.");
            _telemetry?.RecordDeviceBusyReject(deviceBusy.FaultCode, deviceBusy.Message);
            return deviceBusy;
        }

        if (_commands.Count >= Capacity)
        {
            QueueFullRejectCount++;
            AcceleratorQueueAdmissionResult queueFull = Reject(
                AcceleratorTokenFaultCode.QueueFull,
                guardDecision,
                "L7-SDC queue admission rejected because the accelerator command queue is full after guard-backed admission checks.");
            _telemetry?.RecordQueueFullReject(queueFull.FaultCode, queueFull.Message);
            return queueFull;
        }

        if (token!.State == AcceleratorTokenState.Created)
        {
            AcceleratorTokenTransition validated =
                token.MarkValidated(currentGuardEvidence!);
            if (validated.Rejected)
            {
                return Reject(
                    validated.FaultCode,
                    guardDecision,
                    validated.Message);
            }
        }

        if (token.State != AcceleratorTokenState.Validated)
        {
            return Reject(
                AcceleratorTokenFaultCode.QueueAdmissionRejected,
                guardDecision,
                $"L7-SDC queue admission requires token Created or Validated, but token is {token.State}.");
        }

        AcceleratorTokenTransition queued =
            token.MarkQueued(currentGuardEvidence!);
        if (queued.Rejected)
        {
            return Reject(
                queued.FaultCode,
                guardDecision,
                queued.Message);
        }

        var queuedCommand = new AcceleratorQueuedCommand(
            AllocateQueueSequence(),
            request,
            token,
            guardDecision);
        _commands.Enqueue(queuedCommand);
        return AcceleratorQueueAdmissionResult.Accepted(
            queuedCommand,
            guardDecision,
            QueueFullRejectCount,
            DeviceBusyRejectCount,
            ModelRejectCount);
    }

    public bool TryDequeueReady(
        AcceleratorGuardEvidence? currentGuardEvidence,
        out AcceleratorQueuedCommand? command,
        out AcceleratorQueueAdmissionResult result)
    {
        command = null;
        if (_commands.Count == 0)
        {
            result = Reject(
                AcceleratorTokenFaultCode.BackendRejected,
                guardDecision: null,
                "L7-SDC queue has no ready command.");
            return false;
        }

        AcceleratorQueuedCommand next = _commands.Peek();
        AcceleratorGuardDecision guardDecision =
            AcceleratorOwnerDomainGuard.Default.ValidateMappingEpoch(
                next.Token.SubmitGuardDecision,
                currentGuardEvidence);
        if (!guardDecision.IsAllowed)
        {
            result = Reject(
                AcceleratorTokenStore.MapGuardFault(guardDecision.Fault),
                guardDecision,
                "L7-SDC queue dequeue requires fresh owner/domain and epoch authority. " +
                guardDecision.Message);
            return false;
        }

        command = _commands.Dequeue();
        result = AcceleratorQueueAdmissionResult.Accepted(
            command,
            guardDecision,
            QueueFullRejectCount,
            DeviceBusyRejectCount,
            ModelRejectCount);
        return true;
    }

    private bool TryValidateAdmissionRequest(
        AcceleratorQueueAdmissionRequest request,
        AcceleratorGuardEvidence? currentGuardEvidence,
        out AcceleratorToken? token,
        out AcceleratorGuardDecision guardDecision,
        out AcceleratorQueueAdmissionResult? reject)
    {
        token = null;
        guardDecision = default;
        reject = null;

        if (!AcceleratorOwnerDomainGuard.Default.IsDescriptorGuardBacked(
                request.Descriptor,
                out string descriptorMessage))
        {
            reject = Reject(
                AcceleratorTokenFaultCode.DescriptorNotGuardBacked,
                request.Descriptor.OwnerGuardDecision,
                descriptorMessage);
            return false;
        }

        if (!IsCapabilityGuardBacked(
                request.CapabilityAcceptance,
                request.Descriptor,
                out string capabilityMessage))
        {
            reject = Reject(
                request.CapabilityAcceptance.IsAccepted
                    ? AcceleratorTokenFaultCode.CapabilityNotAccepted
                    : AcceleratorTokenFaultCode.CapabilityRejected,
                request.CapabilityAcceptance.GuardDecision,
                capabilityMessage);
            return false;
        }

        if (!request.TokenAdmission.IsAccepted || request.TokenAdmission.Token is null)
        {
            reject = Reject(
                request.TokenAdmission.FaultCode == AcceleratorTokenFaultCode.None
                    ? AcceleratorTokenFaultCode.QueueAdmissionRejected
                    : request.TokenAdmission.FaultCode,
                request.TokenAdmission.GuardDecision,
                "L7-SDC queue admission requires an accepted Phase 06 token admission result.");
            return false;
        }

        token = request.TokenAdmission.Token;
        if (!ReferenceEquals(token.Descriptor, request.Descriptor) &&
            !token.Descriptor.Equals(request.Descriptor))
        {
            reject = Reject(
                AcceleratorTokenFaultCode.QueueAdmissionRejected,
                token.SubmitGuardDecision,
                "L7-SDC queue admission token descriptor does not match the guarded descriptor evidence.");
            return false;
        }

        if (!ReferenceEquals(token.CapabilityAcceptance, request.CapabilityAcceptance))
        {
            reject = Reject(
                AcceleratorTokenFaultCode.QueueAdmissionRejected,
                token.SubmitGuardDecision,
                "L7-SDC queue admission token capability evidence does not match the accepted capability evidence.");
            return false;
        }

        if (!request.ConflictAccepted)
        {
            reject = Reject(
                AcceleratorTokenFaultCode.ConflictRejected,
                token.SubmitGuardDecision,
                string.IsNullOrWhiteSpace(request.ConflictEvidenceMessage)
                    ? "L7-SDC Phase 07 placeholder conflict evidence rejected admission."
                    : request.ConflictEvidenceMessage);
            return false;
        }

        guardDecision =
            AcceleratorOwnerDomainGuard.Default.EnsureBeforeSubmit(
                request.Descriptor,
                currentGuardEvidence);
        if (!guardDecision.IsAllowed)
        {
            reject = Reject(
                AcceleratorTokenStore.MapGuardFault(guardDecision.Fault),
                guardDecision,
                "L7-SDC queue admission requires fresh guarded submit authority. " +
                guardDecision.Message);
            return false;
        }

        AcceleratorGuardDecision tokenGuardDecision =
            AcceleratorOwnerDomainGuard.Default.ValidateMappingEpoch(
                token.SubmitGuardDecision,
                currentGuardEvidence);
        if (!tokenGuardDecision.IsAllowed)
        {
            reject = Reject(
                AcceleratorTokenStore.MapGuardFault(tokenGuardDecision.Fault),
                tokenGuardDecision,
                "L7-SDC queue admission requires token-bound owner/domain and epoch revalidation. " +
                tokenGuardDecision.Message);
            return false;
        }

        return true;
    }

    private AcceleratorQueueAdmissionResult Reject(
        AcceleratorTokenFaultCode faultCode,
        AcceleratorGuardDecision? guardDecision,
        string message)
    {
        ModelRejectCount++;
        _telemetry?.RecordGuardReject(guardDecision, message);
        return AcceleratorQueueAdmissionResult.Reject(
            faultCode,
            guardDecision,
            QueueFullRejectCount,
            DeviceBusyRejectCount,
            ModelRejectCount,
            message);
    }

    private ulong AllocateQueueSequence()
    {
        ulong sequence = _nextQueueSequence++;
        if (_nextQueueSequence == 0)
        {
            _nextQueueSequence = 1;
        }

        return sequence;
    }

    private static bool IsCapabilityGuardBacked(
        AcceleratorCapabilityAcceptanceResult capabilityAcceptance,
        AcceleratorCommandDescriptor descriptor,
        out string message)
    {
        if (!capabilityAcceptance.IsAccepted)
        {
            message = string.IsNullOrWhiteSpace(capabilityAcceptance.RejectReason)
                ? "L7-SDC capability acceptance rejected before queue admission."
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
                "L7-SDC queue admission requires guard-backed capability acceptance; registry metadata is not authority.";
            return false;
        }

        if (guardDecision.DescriptorOwnerBinding is null ||
            !guardDecision.DescriptorOwnerBinding.Equals(descriptor.OwnerBinding))
        {
            message =
                "L7-SDC queue admission capability guard does not match descriptor owner binding.";
            return false;
        }

        message = "L7-SDC capability acceptance is guard-backed for queue admission.";
        return true;
    }
}
