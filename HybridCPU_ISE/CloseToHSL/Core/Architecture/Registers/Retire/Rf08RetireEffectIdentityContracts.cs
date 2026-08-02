using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution;

namespace YAKSys_Hybrid_CPU.Core.Registers.Retire;

/// <summary>
/// Closed RF-08 effect-family vocabulary. This classifies an existing retire-visible
/// effect; it does not select retirement or publish architectural state.
/// </summary>
public enum RetireVisibleEffectKind : byte
{
    RegisterWrite = 1,
    PcWrite = 2,
    CsrWrite = 3,
    VectorConfigWrite = 4,
    DeferredStoreCommit = 5,
    ScalarMemoryStoreCommit = 6,
    AtomicCommit = 7,
    SystemCommit = 8,
    VmxCommit = 9,
    TrapCommit = 10,
    PipelineEventPublication = 11,
    PredicateStateWrite = 12,
    VectorStreamDirty = 13,
    MatrixTileCommit = 14,
    AcceleratorCommit = 15,
}

/// <summary>
/// Immutable identity projection for one retire-visible effect of one existing issued
/// attempt. It creates no operation ID and owns no retire selection, publication,
/// physical-register, rename, commit-map, free-list, checkpoint, squash, or recovery
/// authority.
/// </summary>
public sealed class RetireVisibleEffectIdentity
{
    private RetireVisibleEffectIdentity(
        ExecutionRecord executionRecord,
        RetireVisibleEffectKind effectKind,
        int effectOrdinal,
        int? architecturalRegisterId)
    {
        ExecutionRecord = executionRecord;
        ScheduledOperation = executionRecord.ScheduledOperation;
        OperationId = executionRecord.OperationId;
        GeneratedBinding = executionRecord.GeneratedBinding;
        SourceProvenance = ScheduledOperation.Admission.SourceProvenance;
        EffectKind = effectKind;
        EffectOrdinal = effectOrdinal;
        ArchitecturalRegisterId = architecturalRegisterId;
    }

    public ExecutionRecord ExecutionRecord { get; }
    public ScheduledOperation ScheduledOperation { get; }
    public VliwOperationId OperationId { get; }
    public GeneratedStaticBinding GeneratedBinding { get; }
    public SourceOperationProvenance SourceProvenance { get; }
    public RetireVisibleEffectKind EffectKind { get; }
    public int EffectOrdinal { get; }
    public int? ArchitecturalRegisterId { get; }
    public int VirtualThreadId => OperationId.VirtualThreadId;
    public ulong WorkingBundleSequence => OperationId.WorkingBundleSequence;
    public int WorkingSlotIndex => OperationId.WorkingSlotIndex;
    public ulong OperationAttempt => OperationId.OperationAttempt;
    public int PhysicalLaneIndex => ScheduledOperation.PhysicalLane;
    public ulong SourceBundleSerial => SourceProvenance.SourceBundleSerial;
    public int SourceSlotIndex => SourceProvenance.SourceSlotIndex;

    public static RetireVisibleEffectIdentity Freeze(
        ExecutionRecord executionRecord,
        RetireVisibleEffectKind effectKind,
        int effectOrdinal,
        int effectVirtualThreadId,
        int? architecturalRegisterId = null)
    {
        ArgumentNullException.ThrowIfNull(executionRecord);
        if (!Enum.IsDefined(effectKind))
        {
            throw Violation($"Unknown retire-visible effect kind value {(byte)effectKind}.");
        }

        ScheduledOperation scheduledOperation = executionRecord.ScheduledOperation;
        if (scheduledOperation is null)
        {
            throw Violation("Retire-visible effect identity requires an exact ScheduledOperation.");
        }

        if (executionRecord.OperationId != scheduledOperation.OperationId)
        {
            throw Violation("ExecutionRecord operation identity does not match ScheduledOperation.");
        }

        if (!ReferenceEquals(
                executionRecord.GeneratedBinding,
                scheduledOperation.Admission.ExecutionContract.GeneratedBinding))
        {
            throw Violation("ExecutionRecord does not carry the exact frozen GeneratedStaticBinding from ScheduledOperation.");
        }

        VliwOperationId operationId = executionRecord.OperationId;
        if (operationId.OperationAttempt == 0 ||
            (uint)operationId.WorkingSlotIndex >= BundleMetadata.BundleSlotCount ||
            (uint)scheduledOperation.PhysicalLane >= BundleMetadata.BundleSlotCount ||
            (uint)scheduledOperation.Admission.SourceProvenance.SourceSlotIndex >= BundleMetadata.BundleSlotCount)
        {
            throw Violation("Retire-visible effect identity is missing a valid attempt, bundle slot, source slot, or physical lane.");
        }

        if (effectVirtualThreadId != operationId.VirtualThreadId)
        {
            throw Violation(
                $"Retire-visible effect VT{effectVirtualThreadId} does not match issued attempt VT{operationId.VirtualThreadId}.");
        }

        if (!scheduledOperation.Admission.IsRetireVisible ||
            !scheduledOperation.Admission.ExecutionContract.IsRetireVisible ||
            scheduledOperation.Admission.ExecutionContract.IsAssist)
        {
            throw Violation("A non-retire-visible or assist operation cannot create retire-visible effect identity.");
        }

        if (executionRecord.State != ExecutionRecordState.Terminal ||
            executionRecord.Outcome is not { Kind: ExecutionOutcomeKind.Completed, Result: not null } completed)
        {
            throw Violation("Retire-visible effect identity requires one terminal Completed execution outcome.");
        }

        if (effectOrdinal < 0 || effectOrdinal >= completed.Result.ArchitecturalEffectCount)
        {
            throw Violation(
                $"Effect ordinal {effectOrdinal} is outside completed effect count {completed.Result.ArchitecturalEffectCount}.");
        }

        if (effectKind == RetireVisibleEffectKind.RegisterWrite)
        {
            if (architecturalRegisterId is null)
            {
                throw Violation("RegisterWrite identity requires an architectural register.");
            }

            if (architecturalRegisterId.Value == 0)
            {
                throw Violation("Architectural x0 mutation is forbidden.");
            }

            if ((uint)architecturalRegisterId.Value >= RenameMap.ArchRegs)
            {
                throw Violation($"Architectural register {architecturalRegisterId.Value} is outside the live rename namespace.");
            }
        }
        else if (architecturalRegisterId is not null)
        {
            throw Violation($"Effect kind {effectKind} cannot claim an architectural-register mutation.");
        }

        return new RetireVisibleEffectIdentity(
            executionRecord,
            effectKind,
            effectOrdinal,
            architecturalRegisterId);
    }

    private static RetireEffectIdentityContractViolationException Violation(string message) =>
        new(message);
}

/// <summary>
/// Additive projection binding the existing RetireRecord payload to one frozen effect
/// identity. Production retirement does not consume this projection in RF-08.0.
/// </summary>
public sealed class RetireRecordIdentityProjection
{
    private RetireRecordIdentityProjection(
        RetireRecord retireRecord,
        RetireVisibleEffectIdentity identity)
    {
        RetireRecord = retireRecord;
        Identity = identity;
    }

    public RetireRecord RetireRecord { get; }
    public RetireVisibleEffectIdentity Identity { get; }

    public static RetireRecordIdentityProjection Create(
        in RetireRecord retireRecord,
        RetireVisibleEffectIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (retireRecord.VtId != identity.VirtualThreadId)
        {
            throw new RetireEffectIdentityContractViolationException(
                $"RetireRecord VT{retireRecord.VtId} does not match ScheduledOperation VT{identity.VirtualThreadId}.");
        }

        RetireVisibleEffectKind expectedKind = retireRecord.Kind switch
        {
            RetireRecordKind.RegisterWrite => RetireVisibleEffectKind.RegisterWrite,
            RetireRecordKind.PcWrite => RetireVisibleEffectKind.PcWrite,
            _ => throw new RetireEffectIdentityContractViolationException(
                $"Unsupported RetireRecord kind {retireRecord.Kind}."),
        };
        if (identity.EffectKind != expectedKind)
        {
            throw new RetireEffectIdentityContractViolationException(
                $"RetireRecord kind {retireRecord.Kind} does not match effect identity {identity.EffectKind}.");
        }

        if (retireRecord.Kind == RetireRecordKind.RegisterWrite &&
            retireRecord.ArchReg != identity.ArchitecturalRegisterId)
        {
            throw new RetireEffectIdentityContractViolationException(
                $"RetireRecord architectural register {retireRecord.ArchReg} does not match effect identity register {identity.ArchitecturalRegisterId}.");
        }

        return new RetireRecordIdentityProjection(retireRecord, identity);
    }
}

/// <summary>
/// Evidence-only RF-08.0 coherence checks. They validate a proposed publication
/// transition but do not perform prevalidation, mutate the backend, or publish effects.
/// </summary>
public static class RetireVisibleEffectCoherence
{
    public static void ValidatePublicationClaim(
        RetireVisibleEffectIdentity identity,
        ScheduledOperation scheduledOperation,
        ExecutionRecord executionRecord,
        GeneratedStaticBinding generatedBinding,
        bool prevalidationComplete,
        bool selectedByRetireProtocol,
        bool requestsStoreVisibility)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(scheduledOperation);
        ArgumentNullException.ThrowIfNull(executionRecord);
        ArgumentNullException.ThrowIfNull(generatedBinding);

        if (!ReferenceEquals(identity.ScheduledOperation, scheduledOperation) ||
            !ReferenceEquals(identity.ExecutionRecord, executionRecord) ||
            identity.OperationId != scheduledOperation.OperationId ||
            executionRecord.OperationId != identity.OperationId)
        {
            throw Violation("Publication claim does not carry the exact ScheduledOperation/ExecutionRecord/VliwOperationId attempt.");
        }

        if (!ReferenceEquals(identity.GeneratedBinding, generatedBinding) ||
            !ReferenceEquals(executionRecord.GeneratedBinding, generatedBinding))
        {
            throw Violation("Publication claim reconstructed or mismatched the frozen GeneratedStaticBinding.");
        }

        if (!prevalidationComplete)
        {
            throw Violation("Retire-visible publication cannot precede complete bounded-window prevalidation.");
        }

        bool isStore = identity.EffectKind is
            RetireVisibleEffectKind.DeferredStoreCommit or
            RetireVisibleEffectKind.ScalarMemoryStoreCommit;
        if (requestsStoreVisibility && (!isStore || !selectedByRetireProtocol))
        {
            throw Violation("Store visibility is permitted only for a store effect selected by the retire protocol.");
        }

        if (!requestsStoreVisibility && isStore && selectedByRetireProtocol)
        {
            return;
        }

        if (!selectedByRetireProtocol)
        {
            throw Violation("Architectural-effect publication requires the selected retire protocol.");
        }
    }

    public static void ValidateDistinctTerminalEffects(
        IEnumerable<RetireVisibleEffectIdentity> identities)
    {
        ArgumentNullException.ThrowIfNull(identities);
        var seen = new HashSet<(VliwOperationId OperationId, int EffectOrdinal)>();
        foreach (RetireVisibleEffectIdentity? identity in identities)
        {
            if (identity is null)
            {
                throw Violation("Retire-visible effect set contains missing identity.");
            }

            if (!seen.Add((identity.OperationId, identity.EffectOrdinal)))
            {
                throw Violation(
                    $"Issued attempt {identity.OperationId} contains duplicate terminal effect ordinal {identity.EffectOrdinal}.");
            }
        }
    }

    private static RetireEffectIdentityContractViolationException Violation(string message) =>
        new(message);
}

/// <summary>
/// Fail-closed programming/invariant error. It is not an architectural fault and
/// cannot be converted into retry/not-ready state.
/// </summary>
public sealed class RetireEffectIdentityContractViolationException : InvalidOperationException
{
    public RetireEffectIdentityContractViolationException(string message)
        : base(message)
    {
    }
}
