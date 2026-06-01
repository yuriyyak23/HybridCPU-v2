using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Backends;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Commit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Conflicts;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Fences;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Queues;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Telemetry;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators;

public sealed record ExternalAcceleratorRuntimeCommandResult
{
    public required SystemDeviceCommandKind CommandKind { get; init; }

    public required AcceleratorRegisterAbiResult RegisterAbi { get; init; }

    public AcceleratorCapabilityAcceptanceResult? CapabilityAcceptance { get; init; }

    public AcceleratorTokenAdmissionResult? SubmitAdmission { get; init; }

    public AcceleratorTokenLookupResult? TokenLookup { get; init; }

    public AcceleratorFenceResult? FenceResult { get; init; }

    public AcceleratorBackendResult? BackendSubmitResult { get; init; }

    public AcceleratorBackendResult? BackendTickResult { get; init; }

    public AcceleratorCommitResult? CommitResult { get; init; }
}

/// <summary>
/// Per-core runtime owner for the Phase 08 current L7-SDC executable contour.
/// Token handles remain evidence only; the guard-plane evidence is retained here
/// and revalidated on each command before status, cancel, fence, or commit.
/// </summary>
public sealed class ExternalAcceleratorRuntime
{
    private const string ReferenceAcceleratorId = MatMulCapabilityProvider.AcceleratorId;

    private readonly Processor.MainMemoryArea _mainMemory;
    private readonly AcceleratorTelemetry _telemetry;
    private readonly AcceleratorCapabilityRegistry _capabilities;
    private readonly AcceleratorTokenStore _tokens;
    private readonly AcceleratorCommandQueue _queue;
    private readonly IExternalAcceleratorBackend _backend;
    private readonly MainMemoryReadOnlyAcceleratorMemoryPortal _memoryPortal;
    private readonly AcceleratorStagingBuffer _stagingBuffer;
    private readonly AcceleratorFenceCoordinator _fenceCoordinator;
    private readonly AcceleratorCommitCoordinator _commitCoordinator;
    private readonly ExternalAcceleratorConflictManager _conflictManager;
    private readonly Dictionary<ulong, AcceleratorGuardEvidence> _guardEvidenceByHandle = new();

    public ExternalAcceleratorRuntime(
        Processor.MainMemoryArea mainMemory,
        IExternalAcceleratorBackend? backend = null,
        AcceleratorTelemetry? telemetry = null)
    {
        _mainMemory = mainMemory ?? throw new ArgumentNullException(nameof(mainMemory));
        _telemetry = telemetry ?? new AcceleratorTelemetry();
        _capabilities = new AcceleratorCapabilityRegistry(_telemetry);
        _capabilities.RegisterProvider(new MatMulCapabilityProvider());
        _tokens = new AcceleratorTokenStore(_telemetry);
        _queue = new AcceleratorCommandQueue(
            capacity: 16,
            new AcceleratorDevice(ReferenceAcceleratorId),
            _telemetry);
        _backend = backend ?? new ReferenceExternalAcceleratorBackend(telemetry: _telemetry);
        _memoryPortal = new MainMemoryReadOnlyAcceleratorMemoryPortal(_mainMemory);
        _stagingBuffer = new AcceleratorStagingBuffer();
        _fenceCoordinator = new AcceleratorFenceCoordinator();
        _commitCoordinator = new AcceleratorCommitCoordinator(_telemetry);
        _conflictManager = new ExternalAcceleratorConflictManager(_telemetry);
    }

    public AcceleratorTokenStore Tokens => _tokens;

    public AcceleratorCapabilityRegistry Capabilities => _capabilities;

    public AcceleratorStagingBuffer StagingBuffer => _stagingBuffer;

    public ExternalAcceleratorConflictManager ConflictManager => _conflictManager;

    public ExternalAcceleratorRuntimeCommandResult QueryCaps(
        AcceleratorOwnerBinding ownerBinding,
        AcceleratorGuardEvidence guardEvidence)
    {
        ArgumentNullException.ThrowIfNull(ownerBinding);
        ArgumentNullException.ThrowIfNull(guardEvidence);

        AcceleratorGuardDecision descriptorGuard =
            AcceleratorOwnerDomainGuard.Default.EnsureBeforeDescriptorAcceptance(
                ownerBinding,
                guardEvidence);
        AcceleratorCapabilityAcceptanceResult capability =
            _capabilities.AcceptCapability(
                ReferenceAcceleratorId,
                ownerBinding,
                descriptorGuard);

        return new ExternalAcceleratorRuntimeCommandResult
        {
            CommandKind = SystemDeviceCommandKind.QueryCaps,
            RegisterAbi = AcceleratorRegisterAbi.FromCapabilityQuery(capability),
            CapabilityAcceptance = capability
        };
    }

    public ExternalAcceleratorRuntimeCommandResult Submit(
        AcceleratorCommandDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        AcceleratorGuardEvidence? evidence = descriptor.OwnerGuardDecision.Evidence;
        AcceleratorCapabilityAcceptanceResult capability =
            _capabilities.AcceptCapability(
                ReferenceAcceleratorId,
                descriptor.OwnerBinding,
                descriptor.OwnerGuardDecision);
        AcceleratorTokenAdmissionResult admission =
            _tokens.Create(
                descriptor,
                capability,
                evidence,
                conflictManager: _conflictManager);

        AcceleratorBackendResult? backendSubmit = null;
        AcceleratorBackendResult? backendTick = null;
        if (admission.IsAccepted && admission.Token is not null)
        {
            _guardEvidenceByHandle[admission.Handle.Value] = evidence!;
            var request = new AcceleratorQueueAdmissionRequest
            {
                Descriptor = descriptor,
                CapabilityAcceptance = capability,
                TokenAdmission = admission,
                ConflictAccepted = true,
                ConflictEvidenceMessage =
                    "Phase 08 runtime-owned submit accepted conflict reservation through ExternalAcceleratorConflictManager."
            };
            backendSubmit = _backend.TrySubmit(
                request,
                _queue,
                evidence);
            if (backendSubmit.IsAccepted)
            {
                backendTick = _backend.Tick(
                    _queue,
                    _memoryPortal,
                    _stagingBuffer,
                    evidence);
            }
        }

        return new ExternalAcceleratorRuntimeCommandResult
        {
            CommandKind = SystemDeviceCommandKind.Submit,
            RegisterAbi = AcceleratorRegisterAbi.FromSubmitAdmission(admission),
            CapabilityAcceptance = capability,
            SubmitAdmission = admission,
            BackendSubmitResult = backendSubmit,
            BackendTickResult = backendTick
        };
    }

    public ExternalAcceleratorRuntimeCommandResult Poll(AcceleratorTokenHandle handle)
    {
        AcceleratorTokenLookupResult lookup =
            _tokens.Poll(handle, ResolveGuardEvidence(handle));
        return FromLookup(SystemDeviceCommandKind.Poll, lookup);
    }

    public ExternalAcceleratorRuntimeCommandResult Status(AcceleratorTokenHandle handle)
    {
        AcceleratorTokenLookupResult lookup =
            _tokens.Status(handle, ResolveGuardEvidence(handle));
        return FromLookup(SystemDeviceCommandKind.Status, lookup);
    }

    public ExternalAcceleratorRuntimeCommandResult Wait(
        AcceleratorTokenHandle handle,
        AcceleratorWaitPolicy? waitPolicy = null)
    {
        AcceleratorTokenLookupResult lookup =
            _tokens.TryWait(
                handle,
                ResolveGuardEvidence(handle),
                waitPolicy ?? AcceleratorWaitPolicy.ObserveOnly);
        return FromLookup(SystemDeviceCommandKind.Wait, lookup);
    }

    public ExternalAcceleratorRuntimeCommandResult Cancel(AcceleratorTokenHandle handle)
    {
        AcceleratorBackendResult backendCancel =
            _backend.TryCancel(_tokens, handle, ResolveGuardEvidence(handle));
        AcceleratorTokenLookupResult lookup =
            backendCancel.TokenLookupResult ??
            _tokens.TryLookup(
                handle,
                ResolveGuardEvidence(handle),
                AcceleratorTokenLookupIntent.Cancel);
        return new ExternalAcceleratorRuntimeCommandResult
        {
            CommandKind = SystemDeviceCommandKind.Cancel,
            RegisterAbi = AcceleratorRegisterAbi.FromStatusLookup(lookup),
            TokenLookup = lookup,
            BackendSubmitResult = backendCancel
        };
    }

    public ExternalAcceleratorRuntimeCommandResult FenceObserve(
        AcceleratorTokenHandle handle)
    {
        AcceleratorFenceResult fence =
            _fenceCoordinator.TryFence(
                _tokens,
                AcceleratorFenceScope.ForToken(
                    handle,
                    commitCompletedTokens: false),
                ResolveGuardEvidence(handle),
                conflictManager: _conflictManager);

        return FromFence(fence, commitResult: null);
    }

    public ExternalAcceleratorRuntimeCommandResult FenceCommit(
        AcceleratorTokenHandle handle)
    {
        AcceleratorFenceResult fence =
            _fenceCoordinator.TryFence(
                _tokens,
                AcceleratorFenceScope.ForToken(
                    handle,
                    commitCompletedTokens: true),
                ResolveGuardEvidence(handle),
                _stagingBuffer,
                _mainMemory,
                _commitCoordinator,
                AcceleratorCommitInvalidationPlan.None,
                _conflictManager);

        AcceleratorCommitResult? commitResult =
            fence.CommitResults.Count == 0 ? null : fence.CommitResults[^1];
        return FromFence(fence, commitResult);
    }

    private AcceleratorGuardEvidence? ResolveGuardEvidence(
        AcceleratorTokenHandle handle)
    {
        return handle.IsValid &&
               _guardEvidenceByHandle.TryGetValue(handle.Value, out AcceleratorGuardEvidence? evidence)
            ? evidence
            : null;
    }

    private static ExternalAcceleratorRuntimeCommandResult FromLookup(
        SystemDeviceCommandKind commandKind,
        AcceleratorTokenLookupResult lookup)
    {
        return new ExternalAcceleratorRuntimeCommandResult
        {
            CommandKind = commandKind,
            RegisterAbi = AcceleratorRegisterAbi.FromStatusLookup(lookup),
            TokenLookup = lookup
        };
    }

    private static ExternalAcceleratorRuntimeCommandResult FromFence(
        AcceleratorFenceResult fence,
        AcceleratorCommitResult? commitResult)
    {
        AcceleratorRegisterAbiResult abi = fence.Succeeded
            ? AcceleratorRegisterAbiResult.Write(
                fence.PackedFenceStatus,
                "ACCEL_FENCE rd receives packed fence status after guarded scoped-token serialization and optional commit.")
            : AcceleratorRegisterAbiResult.NoWriteRejected(
                fence.FaultCode == AcceleratorTokenFaultCode.None
                    ? AcceleratorTokenFaultCode.FenceRejected
                    : fence.FaultCode,
                "ACCEL_FENCE rd is written only after the scoped token set serializes without rejection.");

        return new ExternalAcceleratorRuntimeCommandResult
        {
            CommandKind = SystemDeviceCommandKind.Fence,
            RegisterAbi = abi,
            FenceResult = fence,
            TokenLookup = fence.TokenResults.Count == 0 ? null : fence.TokenResults[^1],
            CommitResult = commitResult
        };
    }
}
