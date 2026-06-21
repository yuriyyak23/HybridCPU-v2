using System;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

public enum MatrixTileReplayRollbackLifecycle : byte
{
    None = 0,
    Retired = 1,
    FaultRetired = 2,
    RolledBack = 3,
    Replayed = 4,
    FaultReplayed = 5,
    Discarded = 6,
}

public readonly record struct MatrixTileReplayIdentity(
    uint CoreId,
    int OwnerThreadId,
    uint Opcode,
    MatrixTileProjectedOperationKind OperationKind,
    ulong CaptureOrdinal,
    ulong CaptureFingerprint,
    ulong DecodedInstructionFingerprint,
    ulong MaterializedInstructionFingerprint,
    ulong DescriptorFingerprint,
    MatrixTileRuntimeResourceClass ResourceClass,
    SlotClass SlotClass,
    ulong ResourceContourFingerprint,
    ulong StreamTransferFingerprint,
    ulong NumericPolicyFingerprint,
    ulong LayoutPolicyFingerprint,
    ulong PolicyIdentityFingerprint,
    ulong ReplayEpoch,
    ulong DependencyFingerprint,
    MatrixTileRetirePublicationKind PublicationSurface,
    ulong CheckpointOrdinal)
{
    public bool IsValid =>
        OwnerThreadId is >= 0 and < Processor.CPU_Core.SmtWays &&
        Opcode != 0 &&
        OperationKind != MatrixTileProjectedOperationKind.Unspecified &&
        CaptureOrdinal != 0 &&
        CaptureFingerprint != 0 &&
        DecodedInstructionFingerprint != 0 &&
        MaterializedInstructionFingerprint != 0 &&
        DescriptorFingerprint != 0 &&
        ResourceClass != MatrixTileRuntimeResourceClass.None &&
        ResourceContourFingerprint != 0 &&
        (ResourceClass != MatrixTileRuntimeResourceClass.MatrixTileMemory ||
            StreamTransferFingerprint != 0) &&
        PolicyIdentityFingerprint != 0 &&
        ReplayEpoch != 0 &&
        DependencyFingerprint != 0 &&
        PublicationSurface != MatrixTileRetirePublicationKind.None &&
        (OperationKind != MatrixTileProjectedOperationKind.Macc ||
            (NumericPolicyFingerprint != 0 && LayoutPolicyFingerprint != 0)) &&
        (OperationKind != MatrixTileProjectedOperationKind.Transpose ||
            LayoutPolicyFingerprint != 0) &&
        CheckpointOrdinal != 0;
}

public readonly record struct MatrixTileRollbackOutcome(
    MatrixTileReplayIdentity ReplayIdentity,
    bool RestoredTileState,
    bool RestoredMemory,
    bool FaultOnlyRollback,
    MatrixTileReplayRollbackLifecycle Lifecycle);

public sealed class MatrixTileReplayRollbackValidationException : InvalidOperationException
{
    public MatrixTileReplayRollbackValidationException(string message)
        : base(message)
    {
    }
}

public sealed class MatrixTileReplayRollbackJournal
{
    internal MatrixTileReplayRollbackJournal(
        MatrixTileReplayIdentity replayIdentity,
        MatrixTileExecutionCaptureRecord capture,
        MatrixTileRetireOutcome retireOutcome,
        bool hadTileCheckpoint,
        MatrixTileTileImage tileCheckpoint,
        MatrixTileCapturedMemoryWrite[] memoryCheckpoint)
    {
        ReplayIdentity = replayIdentity;
        Capture = capture.DeepClone();
        OriginalRetireOutcome = retireOutcome;
        HadTileCheckpoint = hadTileCheckpoint;
        TileCheckpoint = tileCheckpoint.DeepClone();
        MemoryCheckpoint = CloneWrites(memoryCheckpoint);
        Lifecycle = retireOutcome.FaultRetired
            ? MatrixTileReplayRollbackLifecycle.FaultRetired
            : MatrixTileReplayRollbackLifecycle.Retired;
    }

    public MatrixTileReplayIdentity ReplayIdentity { get; }

    public MatrixTileRetireOutcome OriginalRetireOutcome { get; }

    public MatrixTileRetireOutcome? ReplayOutcome { get; internal set; }

    public MatrixTileReplayRollbackLifecycle Lifecycle { get; internal set; }

    public bool HasTileCheckpoint =>
        Capture.OperationKind is MatrixTileProjectedOperationKind.Load
            or MatrixTileProjectedOperationKind.Macc
            or MatrixTileProjectedOperationKind.Transpose &&
        !Capture.HasFault;

    public bool HadArchitecturalTileBeforeRetire => HadTileCheckpoint;

    public int MemoryCheckpointRowCount => MemoryCheckpoint.Length;

    public bool IsFaultOnly => OriginalRetireOutcome.FaultRetired;

    internal MatrixTileExecutionCaptureRecord Capture { get; }

    internal bool HadTileCheckpoint { get; }

    internal MatrixTileTileImage TileCheckpoint { get; }

    internal MatrixTileCapturedMemoryWrite[] MemoryCheckpoint { get; }

    private static MatrixTileCapturedMemoryWrite[] CloneWrites(
        MatrixTileCapturedMemoryWrite[] writes)
    {
        var clone = new MatrixTileCapturedMemoryWrite[writes.Length];
        for (int index = 0; index < writes.Length; index++)
        {
            clone[index] = writes[index].DeepClone();
        }

        return clone;
    }
}

public static class MatrixTileReplayRollbackAbi
{
    public const string ReplayRollbackDecision = "ClosedMatrixTileReplayRollbackConformance";
    public const string DecodedInstructionIdentityDecision = "ReplayStableDecodedInstructionFingerprint";
    public const string MaterializedInstructionIdentityDecision = "ReplayStableMaterializedInstructionFingerprint";
    public const string TileDescriptorIdentityDecision = "ReplayStableCanonicalTileDescriptorFingerprint";
    public const string ResourceContourIdentityDecision = "ReplayStableMatrixTileResourceContourFingerprint";
    public const string StreamTransferIdentityDecision = "ReplayStableTypedStreamTransferFingerprint";
    public const string PolicyBoundIdentityDecision = "ReplayStableNumericLayoutPolicyDependencyAndEpochFingerprint";
    public const string TileRollbackDecision = "CoreOwnedTileCheckpointRestore";
    public const string MemoryRollbackDecision = "CoreOwnedAllOrNoneStoreCheckpointRestore";
    public const string AccumulatorRollbackDecision = "CoreOwnedAccumulatorCheckpointRestore";
    public const string MemoryFaultReplayDecision = "DeterministicCapturedAndRetireMemoryFaultReplay";
    public const string DescriptorFaultReplayDecision = "DeterministicDescriptorAndSemanticFaultReplay";
    public const string DuplicateReplayDecision = "RollbackBeforeReplayAndConsumeReplayAuthorityOnce";
    public const string StaleCheckpointDecision = "StalePublishedOrPreRetireStateFailsClosed";
    public const string OwnershipDecision = "CoreRegistersRetireOwnedMatrixTileReplayJournal";
    public const string BypassDecision = "ReplayRequiresRegisteredRetiredCaptureJournal";
    public const string FallbackDecision = "NoScalarVectorBaseMemoryDotDscLane7VmxBackendOrCompilerFallback";

    public const bool HasReplayRollbackConformance = true;
    public const bool HasDecodedInstructionReplayIdentity = true;
    public const bool HasMaterializedInstructionReplayIdentity = true;
    public const bool HasTileDescriptorReplayIdentity = true;
    public const bool HasResourceContourReplayIdentity = true;
    public const bool HasStreamTransferReplayIdentity = true;
    public const bool HasPolicyBoundReplayIdentity = true;
    public const bool HasPendingTileWriteRollback = true;
    public const bool HasPendingMemoryStoreRollback = true;
    public const bool HasAccumulatorRollback = true;
    public const bool HasDeterministicReplayAfterMemoryFault = true;
    public const bool HasDeterministicReplayAfterDescriptorFault = true;
    public const bool HasLegalIllegalConformanceVectors = true;
    public const bool BlocksReplayWithoutRetirePublication = true;
    public const bool BlocksCaptureRecordIdentityBypass = true;
    public const bool RejectsDuplicateReplay = true;
    public const bool RejectsStaleRollback = true;
    public const bool KeepsHostOwnedEvidenceNonArchitectural = true;
    public const bool KeepsCompilerScopeClosed = true;
    public const bool UsesFallbackPath = false;

    internal static MatrixTileReplayRollbackJournal RetireWithCheckpoint(
        ref Processor.CPU_Core core,
        in MatrixTileMaterializedInstruction materializedInstruction,
        in MatrixTileExecutionCaptureRecord capture,
        MatrixTileCaptureIdentity expectedCaptureIdentity,
        ulong checkpointOrdinal)
    {
        MatrixTileRetirePublicationAbi.ValidateForRetire(
            core,
            capture,
            expectedCaptureIdentity);

        MatrixTileReplayIdentity replayIdentity = CreateReplayIdentity(
            materializedInstruction,
            capture,
            checkpointOrdinal);
        ValidateReplayIdentity(core, materializedInstruction, capture, replayIdentity);

        CapturePreRetireState(
            ref core,
            capture,
            out bool hadTileCheckpoint,
            out MatrixTileTileImage tileCheckpoint,
            out MatrixTileCapturedMemoryWrite[] memoryCheckpoint);

        MatrixTileRetireOutcome retireOutcome =
            MatrixTileRetirePublicationAbi.Retire(
                ref core,
                capture,
                expectedCaptureIdentity);

        var journal = new MatrixTileReplayRollbackJournal(
            replayIdentity,
            capture,
            retireOutcome,
            hadTileCheckpoint,
            tileCheckpoint,
            memoryCheckpoint);
        core.RegisterMatrixTileReplayJournal(journal);
        return journal;
    }

    public static MatrixTileRollbackOutcome Rollback(
        ref Processor.CPU_Core core,
        MatrixTileReplayRollbackJournal journal,
        MatrixTileReplayIdentity expectedIdentity)
    {
        ValidateOwnedJournal(core, journal, expectedIdentity);
        if (journal.Lifecycle is not (
                MatrixTileReplayRollbackLifecycle.Retired or
                MatrixTileReplayRollbackLifecycle.FaultRetired))
        {
            throw Validation(
                $"MTILE rollback requires a retired journal, not {journal.Lifecycle}.");
        }

        bool restoredTile = false;
        bool restoredMemory = false;
        if (!journal.OriginalRetireOutcome.FaultRetired)
        {
            MatrixTileExecutionCaptureRecord capture = journal.Capture;
            switch (capture.OperationKind)
            {
                case MatrixTileProjectedOperationKind.Load:
                case MatrixTileProjectedOperationKind.Macc:
                case MatrixTileProjectedOperationKind.Transpose:
                    RequireCurrentTileImage(
                        ref core,
                        capture.CaptureIdentity.OwnerThreadId,
                        capture.ResultImage,
                        "rollback");
                    core.RestoreRetiredMatrixTileCheckpoint(
                        capture.CaptureIdentity.OwnerThreadId,
                        capture.DestinationTileId,
                        journal.HadTileCheckpoint,
                        journal.TileCheckpoint);
                    restoredTile = true;
                    break;

                case MatrixTileProjectedOperationKind.Store:
                    RequireCurrentMemoryRows(
                        ref core,
                        capture.PendingStoreWrites,
                        "rollback");
                    core.RestoreRetiredMatrixTileStoreAllOrNone(
                        journal.MemoryCheckpoint);
                    restoredMemory = true;
                    break;

                default:
                    throw Validation(
                        $"MTILE rollback rejected unsupported operation {capture.OperationKind}.");
            }
        }

        journal.Lifecycle = MatrixTileReplayRollbackLifecycle.RolledBack;
        return new MatrixTileRollbackOutcome(
            journal.ReplayIdentity,
            restoredTile,
            restoredMemory,
            journal.OriginalRetireOutcome.FaultRetired,
            journal.Lifecycle);
    }

    public static MatrixTileRetireOutcome Replay(
        ref Processor.CPU_Core core,
        in MatrixTileMaterializedInstruction materializedInstruction,
        MatrixTileReplayRollbackJournal journal,
        MatrixTileReplayIdentity expectedIdentity)
    {
        ValidateOwnedJournal(core, journal, expectedIdentity);
        ValidateReplayIdentity(
            core,
            materializedInstruction,
            journal.Capture,
            journal.ReplayIdentity);
        if (journal.Lifecycle != MatrixTileReplayRollbackLifecycle.RolledBack)
        {
            throw Validation(
                $"MTILE replay requires one completed rollback, not {journal.Lifecycle}.");
        }

        ValidatePreRetireState(ref core, journal);

        MatrixTileRetireOutcome replayOutcome;
        if (journal.OriginalRetireOutcome.FaultRetired)
        {
            replayOutcome = journal.OriginalRetireOutcome;
        }
        else
        {
            replayOutcome = MatrixTileRetirePublicationAbi.Retire(
                ref core,
                journal.Capture,
                journal.Capture.CaptureIdentity);
            if (replayOutcome != journal.OriginalRetireOutcome)
            {
                throw Validation(
                    $"{journal.Capture.Mnemonic} replay outcome diverged from its retired outcome.");
            }
        }

        journal.ReplayOutcome = replayOutcome;
        journal.Lifecycle = replayOutcome.FaultRetired
            ? MatrixTileReplayRollbackLifecycle.FaultReplayed
            : MatrixTileReplayRollbackLifecycle.Replayed;
        core.ReleaseMatrixTileReplayJournal(journal);
        return replayOutcome;
    }

    public static void Discard(
        ref Processor.CPU_Core core,
        MatrixTileReplayRollbackJournal journal,
        MatrixTileReplayIdentity expectedIdentity)
    {
        ValidateOwnedJournal(core, journal, expectedIdentity);
        if (journal.Lifecycle is not (
                MatrixTileReplayRollbackLifecycle.Retired or
                MatrixTileReplayRollbackLifecycle.FaultRetired))
        {
            throw Validation(
                $"MTILE replay journal discard rejected lifecycle {journal.Lifecycle}.");
        }

        journal.Lifecycle = MatrixTileReplayRollbackLifecycle.Discarded;
        core.ReleaseMatrixTileReplayJournal(journal);
    }

    private static MatrixTileReplayIdentity CreateReplayIdentity(
        in MatrixTileMaterializedInstruction materializedInstruction,
        in MatrixTileExecutionCaptureRecord capture,
        ulong checkpointOrdinal)
    {
        MatrixTileCaptureIdentity captureIdentity = capture.CaptureIdentity;
        MatrixTilePolicyBoundCaptureIdentity policyIdentity = capture.PolicyIdentity;
        return new MatrixTileReplayIdentity(
            captureIdentity.CoreId,
            captureIdentity.OwnerThreadId,
            captureIdentity.Opcode,
            captureIdentity.OperationKind,
            captureIdentity.CaptureOrdinal,
            captureIdentity.CaptureFingerprint,
            ComputeDecodedInstructionFingerprint(materializedInstruction.Projection),
            ComputeMaterializedInstructionFingerprint(materializedInstruction),
            ComputeDescriptorFingerprint(materializedInstruction),
            MatrixTileResourceContour.Classify(captureIdentity.Opcode),
            MatrixTileResourceContour.ResolveSlotClass(
                MatrixTileResourceContour.Classify(captureIdentity.Opcode)),
            ComputeResourceContourFingerprint(captureIdentity.Opcode),
            ComputeStreamTransferIdentityFingerprint(capture),
            policyIdentity.NumericPolicyFingerprint,
            policyIdentity.LayoutPolicyFingerprint,
            policyIdentity.IdentityFingerprint,
            policyIdentity.ReplayEpoch,
            policyIdentity.DependencyFingerprint,
            policyIdentity.PublicationSurface,
            checkpointOrdinal);
    }

    private static void CapturePreRetireState(
        ref Processor.CPU_Core core,
        in MatrixTileExecutionCaptureRecord capture,
        out bool hadTileCheckpoint,
        out MatrixTileTileImage tileCheckpoint,
        out MatrixTileCapturedMemoryWrite[] memoryCheckpoint)
    {
        hadTileCheckpoint = false;
        tileCheckpoint = default;
        memoryCheckpoint = Array.Empty<MatrixTileCapturedMemoryWrite>();
        if (capture.HasFault)
        {
            return;
        }

        if (capture.OperationKind == MatrixTileProjectedOperationKind.Store)
        {
            if (!core.TryCaptureMatrixTileStoreRollbackImage(
                    capture.PendingStoreWrites,
                    out memoryCheckpoint,
                    out string failureMessage))
            {
                throw Validation(failureMessage);
            }

            return;
        }

        hadTileCheckpoint = core.TryCaptureAnyMatrixTileSnapshot(
            capture.CaptureIdentity.OwnerThreadId,
            capture.DestinationTileId,
            out tileCheckpoint);
    }

    private static void ValidatePreRetireState(
        ref Processor.CPU_Core core,
        MatrixTileReplayRollbackJournal journal)
    {
        MatrixTileExecutionCaptureRecord capture = journal.Capture;
        if (journal.OriginalRetireOutcome.FaultRetired)
        {
            return;
        }

        if (capture.OperationKind == MatrixTileProjectedOperationKind.Store)
        {
            RequireCurrentMemoryRows(
                ref core,
                journal.MemoryCheckpoint,
                "replay");
            return;
        }

        bool hasCurrent = core.TryCaptureAnyMatrixTileSnapshot(
            capture.CaptureIdentity.OwnerThreadId,
            capture.DestinationTileId,
            out MatrixTileTileImage current);
        if (journal.HadTileCheckpoint)
        {
            if (!hasCurrent || !TileImagesEqual(current, journal.TileCheckpoint))
            {
                throw Validation(
                    $"{capture.Mnemonic} replay rejected stale pre-retire tile state.");
            }
        }
        else if (hasCurrent)
        {
            throw Validation(
                $"{capture.Mnemonic} replay expected an absent pre-retire destination tile.");
        }
    }

    private static void RequireCurrentTileImage(
        ref Processor.CPU_Core core,
        int ownerThreadId,
        MatrixTileTileImage expected,
        string operation)
    {
        if (!core.TryCaptureAnyMatrixTileSnapshot(
                ownerThreadId,
                expected.TileId,
                out MatrixTileTileImage current) ||
            !TileImagesEqual(current, expected))
        {
            throw Validation(
                $"MTILE {operation} rejected a stale or replaced architectural tile publication.");
        }
    }

    private static void RequireCurrentMemoryRows(
        ref Processor.CPU_Core core,
        MatrixTileCapturedMemoryWrite[] expected,
        string operation)
    {
        if (!core.MatrixTileMemoryRowsMatch(expected))
        {
            throw Validation(
                $"MTILE_STORE {operation} rejected stale or replaced committed memory.");
        }
    }

    private static void ValidateOwnedJournal(
        in Processor.CPU_Core core,
        MatrixTileReplayRollbackJournal journal,
        MatrixTileReplayIdentity expectedIdentity)
    {
        ArgumentNullException.ThrowIfNull(journal);
        if (!expectedIdentity.IsValid ||
            journal.ReplayIdentity != expectedIdentity ||
            expectedIdentity.CoreId != core.CoreID ||
            !core.OwnsMatrixTileReplayJournal(journal))
        {
            throw Validation(
                "MTILE replay/rollback rejected a missing, mismatched, released, or wrong-core journal.");
        }
    }

    private static void ValidateReplayIdentity(
        in Processor.CPU_Core core,
        in MatrixTileMaterializedInstruction materializedInstruction,
        in MatrixTileExecutionCaptureRecord capture,
        MatrixTileReplayIdentity replayIdentity)
    {
        MatrixTileCaptureIdentity captureIdentity = capture.CaptureIdentity;
        MatrixTilePolicyBoundCaptureIdentity policyIdentity = capture.PolicyIdentity;
        if (!replayIdentity.IsValid ||
            replayIdentity.CoreId != core.CoreID ||
            replayIdentity.CoreId != captureIdentity.CoreId ||
            replayIdentity.OwnerThreadId != captureIdentity.OwnerThreadId ||
            replayIdentity.Opcode != captureIdentity.Opcode ||
            replayIdentity.OperationKind != captureIdentity.OperationKind ||
            replayIdentity.CaptureOrdinal != captureIdentity.CaptureOrdinal ||
            replayIdentity.CaptureFingerprint != captureIdentity.CaptureFingerprint ||
            replayIdentity.DecodedInstructionFingerprint !=
                ComputeDecodedInstructionFingerprint(materializedInstruction.Projection) ||
            replayIdentity.MaterializedInstructionFingerprint !=
                ComputeMaterializedInstructionFingerprint(materializedInstruction) ||
            replayIdentity.DescriptorFingerprint !=
                ComputeDescriptorFingerprint(materializedInstruction) ||
            replayIdentity.ResourceClass !=
                MatrixTileResourceContour.Classify(captureIdentity.Opcode) ||
            replayIdentity.SlotClass !=
                MatrixTileResourceContour.ResolveSlotClass(replayIdentity.ResourceClass) ||
            replayIdentity.ResourceContourFingerprint !=
                ComputeResourceContourFingerprint(captureIdentity.Opcode) ||
            replayIdentity.StreamTransferFingerprint !=
                ComputeStreamTransferIdentityFingerprint(capture) ||
            replayIdentity.NumericPolicyFingerprint !=
                policyIdentity.NumericPolicyFingerprint ||
            replayIdentity.LayoutPolicyFingerprint !=
                policyIdentity.LayoutPolicyFingerprint ||
            replayIdentity.PolicyIdentityFingerprint !=
                policyIdentity.IdentityFingerprint ||
            replayIdentity.ReplayEpoch != policyIdentity.ReplayEpoch ||
            replayIdentity.ReplayEpoch !=
                core.GetMatrixTileReplayInvalidationEpoch() ||
            replayIdentity.DependencyFingerprint !=
                policyIdentity.DependencyFingerprint ||
            replayIdentity.DependencyFingerprint !=
                MatrixTilePolicyBoundIdentityAbi.ComputeDependencyFingerprint(
                    materializedInstruction) ||
            replayIdentity.PublicationSurface !=
                policyIdentity.PublicationSurface ||
            !MatrixTilePolicyBoundIdentityAbi.ValidateCapture(
                capture,
                core.GetMatrixTileReplayInvalidationEpoch()) ||
            !MatrixTilePolicyBoundIdentityAbi.MatchesMaterializedInstruction(
                policyIdentity,
                materializedInstruction))
        {
            throw Validation(
                "MTILE replay identity does not match its decoded, materialized, descriptor, policy-bound, and capture correlation.");
        }
    }

    private static ulong ComputeDecodedInstructionFingerprint(
        in MatrixTileInstructionIrProjection projection)
    {
        ulong hash = BeginHash();
        AddUInt32(ref hash, (uint)projection.Opcode);
        AddString(ref hash, projection.Mnemonic);
        AddByte(ref hash, (byte)projection.OperationKind);
        VectorInstructionPayload payload = projection.SourcePayload;
        AddUInt64(ref hash, payload.PrimaryPointer);
        AddUInt64(ref hash, payload.SecondaryPointer);
        AddUInt32(ref hash, payload.StreamLength);
        AddUInt16(ref hash, payload.Stride);
        AddUInt16(ref hash, payload.RowStride);
        AddByte(ref hash, payload.Indexed ? (byte)1 : (byte)0);
        AddByte(ref hash, payload.Is2D ? (byte)1 : (byte)0);
        AddByte(ref hash, payload.TailAgnostic ? (byte)1 : (byte)0);
        AddByte(ref hash, payload.MaskAgnostic ? (byte)1 : (byte)0);
        AddByte(ref hash, payload.Saturating ? (byte)1 : (byte)0);
        AddByte(ref hash, payload.PredicateMask);
        AddByte(ref hash, payload.DataType);
        AddNumericPolicyIdentity(ref hash, payload.MatrixTileNumericPolicy);
        AddLayoutPolicyIdentity(ref hash, payload.MatrixTileLayoutPolicy);
        return FinishHash(hash);
    }

    private static ulong ComputeMaterializedInstructionFingerprint(
        in MatrixTileMaterializedInstruction materializedInstruction)
    {
        ulong hash = BeginHash();
        AddUInt32(ref hash, (uint)materializedInstruction.Opcode);
        AddString(ref hash, materializedInstruction.Mnemonic);
        AddByte(ref hash, (byte)materializedInstruction.OperationKind);
        AddUInt64(
            ref hash,
            ComputeDecodedInstructionFingerprint(materializedInstruction.Projection));
        AddByte(ref hash, materializedInstruction.IsTypedCloseToRtlRuntimeObject ? (byte)1 : (byte)0);
        AddByte(ref hash, materializedInstruction.PublishesTypedTileMicroOp ? (byte)1 : (byte)0);
        AddByte(ref hash, materializedInstruction.OpensExecution ? (byte)1 : (byte)0);
        AddByte(ref hash, materializedInstruction.UsesFallbackPath ? (byte)1 : (byte)0);
        return FinishHash(hash);
    }

    private static ulong ComputeDescriptorFingerprint(
        in MatrixTileMaterializedInstruction materializedInstruction)
    {
        ulong hash = BeginHash();
        AddDescriptor(ref hash, materializedInstruction.TileDescriptor);
        AddDescriptor(ref hash, materializedInstruction.SecondaryTileDescriptor);
        AddDescriptor(ref hash, materializedInstruction.ResultTileDescriptor);
        return FinishHash(hash);
    }

    private static ulong ComputeResourceContourFingerprint(uint opcode)
    {
        MatrixTileRuntimeResourceClass resourceClass =
            MatrixTileResourceContour.Classify(opcode);
        SlotClass slotClass =
            MatrixTileResourceContour.ResolveSlotClass(resourceClass);
        ulong hash = BeginHash();
        AddUInt32(ref hash, opcode);
        AddByte(ref hash, (byte)resourceClass);
        AddByte(ref hash, (byte)slotClass);
        AddByte(ref hash, SlotClassLaneMap.GetLaneMask(slotClass));
        AddByte(
            ref hash,
            resourceClass == MatrixTileRuntimeResourceClass.MatrixTileMemory
                ? (byte)MatrixTileResourceContour.StreamEngineChannel
                : byte.MaxValue);
        return FinishHash(hash);
    }

    private static ulong ComputeStreamTransferIdentityFingerprint(
        in MatrixTileExecutionCaptureRecord capture)
    {
        if (capture.StreamTransfer.TransferFingerprint != 0)
        {
            return capture.StreamTransfer.TransferFingerprint;
        }

        ulong hash = BeginHash();
        AddUInt32(ref hash, capture.CaptureIdentity.Opcode);
        AddByte(ref hash, (byte)capture.OperationKind);
        AddByte(ref hash, (byte)capture.FaultKind);
        AddByte(ref hash, (byte)capture.MemoryFaultKind);
        AddByte(ref hash, capture.HasFault ? (byte)1 : (byte)0);
        return FinishHash(hash);
    }

    private static bool TileImagesEqual(
        MatrixTileTileImage left,
        MatrixTileTileImage right)
    {
        if (left.TileId != right.TileId ||
            !left.Descriptor.Equals(right.Descriptor) ||
            left.Data == null ||
            right.Data == null ||
            left.Data.Length != right.Data.Length)
        {
            return false;
        }

        return left.Data.AsSpan().SequenceEqual(right.Data);
    }

    private static ulong BeginHash() => 14695981039346656037UL;

    private static ulong FinishHash(ulong hash) => hash == 0 ? 1UL : hash;

    private static void AddByte(ref ulong hash, byte value)
    {
        hash ^= value;
        hash *= 1099511628211UL;
    }

    private static void AddUInt16(ref ulong hash, ushort value)
    {
        AddByte(ref hash, (byte)value);
        AddByte(ref hash, (byte)(value >> 8));
    }

    private static void AddUInt32(ref ulong hash, uint value)
    {
        for (int shift = 0; shift < 32; shift += 8)
        {
            AddByte(ref hash, (byte)(value >> shift));
        }
    }

    private static void AddUInt64(ref ulong hash, ulong value)
    {
        for (int shift = 0; shift < 64; shift += 8)
        {
            AddByte(ref hash, (byte)(value >> shift));
        }
    }

    private static void AddString(ref ulong hash, string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            AddUInt16(ref hash, value[index]);
        }

        AddByte(ref hash, 0);
    }

    private static void AddDescriptor(
        ref ulong hash,
        MatrixTileCanonicalDescriptorAbi descriptor)
    {
        AddUInt16(ref hash, descriptor.Rows);
        AddUInt16(ref hash, descriptor.Columns);
        AddUInt16(ref hash, descriptor.ElementSizeBytes);
        AddUInt32(ref hash, descriptor.StrideBytes);
        AddByte(ref hash, (byte)descriptor.Layout);
    }

    private static void AddNumericPolicyIdentity(
        ref ulong hash,
        MatrixTileNumericPolicy? policy)
    {
        AddByte(ref hash, policy.HasValue ? (byte)1 : (byte)0);
        if (!policy.HasValue)
        {
            return;
        }

        AddUInt16(ref hash, policy.Value.AbiVersion);
        AddUInt64(ref hash, policy.Value.Fingerprint);
    }

    private static void AddLayoutPolicyIdentity(
        ref ulong hash,
        MatrixTileLayoutPolicy? policy)
    {
        AddByte(ref hash, policy.HasValue ? (byte)1 : (byte)0);
        if (!policy.HasValue)
        {
            return;
        }

        AddUInt16(ref hash, policy.Value.AbiVersion);
        AddUInt64(ref hash, policy.Value.Fingerprint);
    }

    private static MatrixTileReplayRollbackValidationException Validation(
        string message) =>
        new(message);
}
