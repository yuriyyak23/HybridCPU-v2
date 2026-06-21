using System;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class MatrixTileReplayRollbackConformanceTests
{
    [Fact]
    public void MtileLoad_RollbackRestoresTileAndReplayRepublishesCapturedImage()
    {
        Processor.CPU_Core core = CreateCore(out Processor.MainMemoryArea memory);
        Assert.True(memory.TryWritePhysicalRange(0x100, new byte[] { 1, 2 }));
        Assert.True(memory.TryWritePhysicalRange(0x102, new byte[] { 3, 4 }));
        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTILE_LOAD,
            primaryPointer: 0x100,
            secondaryPointer: 2);
        byte[] initial = [9, 8, 7, 6];
        core.SeedMatrixTileForRuntime(0, 2, microOp.ResultTileDescriptor, initial);

        MatrixTileExecutionCaptureRecord capture = ExecuteAndRetire(ref core, microOp);
        MatrixTileReplayRollbackJournal journal = AssertJournal(microOp);
        AssertReplayIdentity(journal.ReplayIdentity, capture.CaptureIdentity);
        AssertTile(ref core, 2, microOp.ResultTileDescriptor, [1, 2, 3, 4]);

        MatrixTileRollbackOutcome rollback =
            microOp.RollbackRetiredResult(ref core, journal.ReplayIdentity);

        Assert.True(rollback.RestoredTileState);
        AssertTile(ref core, 2, microOp.ResultTileDescriptor, initial);

        MatrixTileRetireOutcome replay =
            microOp.ReplayRolledBackResult(ref core, journal.ReplayIdentity);

        Assert.Equal(journal.OriginalRetireOutcome, replay);
        Assert.Equal(MatrixTileReplayRollbackLifecycle.Replayed, journal.Lifecycle);
        AssertTile(ref core, 2, microOp.ResultTileDescriptor, capture.ResultImage.Data);
    }

    [Fact]
    public void MtileLoad_RollbackRemovesDestinationThatWasAbsentBeforeRetire()
    {
        Processor.CPU_Core core = CreateCore(out Processor.MainMemoryArea memory);
        Assert.True(memory.TryWritePhysicalRange(0x100, new byte[] { 1, 2 }));
        Assert.True(memory.TryWritePhysicalRange(0x102, new byte[] { 3, 4 }));
        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTILE_LOAD,
            primaryPointer: 0x100,
            secondaryPointer: 5);

        ExecuteAndRetire(ref core, microOp);
        MatrixTileReplayRollbackJournal journal = AssertJournal(microOp);
        Assert.False(journal.HadArchitecturalTileBeforeRetire);

        microOp.RollbackRetiredResult(ref core, journal.ReplayIdentity);

        Assert.False(core.TryCaptureAnyMatrixTileSnapshot(0, 5, out _));
    }

    [Fact]
    public void MtileStore_RollbackRestoresMemoryAndReplayRecommitsCapturedRows()
    {
        Processor.CPU_Core core = CreateCore(out Processor.MainMemoryArea memory);
        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTILE_STORE,
            primaryPointer: 0x180,
            secondaryPointer: 2);
        byte[] source = [5, 6, 7, 8];
        byte[] initial = [21, 22, 23, 24];
        core.SeedMatrixTileForRuntime(0, 2, microOp.TileDescriptor, source);
        WriteRows(memory, 0x180, initial);

        ExecuteAndRetire(ref core, microOp);
        MatrixTileReplayRollbackJournal journal = AssertJournal(microOp);
        Assert.Equal(2, journal.MemoryCheckpointRowCount);
        Assert.Equal(source, ReadRows(memory, 0x180));

        MatrixTileRollbackOutcome rollback =
            microOp.RollbackRetiredResult(ref core, journal.ReplayIdentity);

        Assert.True(rollback.RestoredMemory);
        Assert.Equal(initial, ReadRows(memory, 0x180));

        MatrixTileRetireOutcome replay =
            microOp.ReplayRolledBackResult(ref core, journal.ReplayIdentity);

        Assert.True(replay.IsSuccess);
        Assert.Equal(source, ReadRows(memory, 0x180));
    }

    [Fact]
    public void MtileMacc_RollbackRestoresAccumulatorAndReplayIsDeterministic()
    {
        Processor.CPU_Core core = CreateCore(out _);
        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTILE_MACC,
            primaryPointer: 1,
            secondaryPointer: (3UL << 16) | 2UL);
        core.SeedMatrixTileForRuntime(0, 1, microOp.TileDescriptor, [1, 2, 3, 4]);
        core.SeedMatrixTileForRuntime(0, 2, microOp.SecondaryTileDescriptor, [1, 0, 0, 1]);
        byte[] initial = new byte[
            MatrixTileExecuteCaptureAbi.GetPackedByteLength(
                microOp.ResultTileDescriptor)];
        core.SeedMatrixTileForRuntime(0, 3, microOp.ResultTileDescriptor, initial);

        MatrixTileExecutionCaptureRecord capture = ExecuteAndRetire(ref core, microOp);
        MatrixTileReplayRollbackJournal journal = AssertJournal(microOp);
        AssertTile(ref core, 3, microOp.ResultTileDescriptor, capture.ResultImage.Data);

        microOp.RollbackRetiredResult(ref core, journal.ReplayIdentity);
        AssertTile(ref core, 3, microOp.ResultTileDescriptor, initial);

        microOp.ReplayRolledBackResult(ref core, journal.ReplayIdentity);
        AssertTile(ref core, 3, microOp.ResultTileDescriptor, capture.ResultImage.Data);
    }

    [Fact]
    public void Mtranspose_RollbackRestoresDestinationAndReplayIsDeterministic()
    {
        Processor.CPU_Core core = CreateCore(out _);
        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTRANSPOSE,
            primaryPointer: 1,
            secondaryPointer: 2);
        core.SeedMatrixTileForRuntime(0, 1, microOp.TileDescriptor, [1, 2, 3, 4]);
        byte[] initial = [9, 9, 9, 9];
        core.SeedMatrixTileForRuntime(0, 2, microOp.ResultTileDescriptor, initial);

        ExecuteAndRetire(ref core, microOp);
        MatrixTileReplayRollbackJournal journal = AssertJournal(microOp);
        AssertTile(ref core, 2, microOp.ResultTileDescriptor, [1, 3, 2, 4]);

        microOp.RollbackRetiredResult(ref core, journal.ReplayIdentity);
        AssertTile(ref core, 2, microOp.ResultTileDescriptor, initial);

        microOp.ReplayRolledBackResult(ref core, journal.ReplayIdentity);
        AssertTile(ref core, 2, microOp.ResultTileDescriptor, [1, 3, 2, 4]);
    }

    [Fact]
    public void CapturedMemoryFault_RollbackAndReplayPreserveExactFaultWithoutPublication()
    {
        Processor.CPU_Core core = CreateCore(out _);
        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTILE_LOAD,
            primaryPointer: 0x1000,
            secondaryPointer: 7);

        MatrixTileExecutionCaptureRecord capture = ExecuteAndRetire(ref core, microOp);
        MatrixTileReplayRollbackJournal journal = AssertJournal(microOp);
        Assert.True(capture.HasFault);
        Assert.True(journal.OriginalRetireOutcome.FaultRetired);

        MatrixTileRollbackOutcome rollback =
            microOp.RollbackRetiredResult(ref core, journal.ReplayIdentity);
        MatrixTileRetireOutcome replay =
            microOp.ReplayRolledBackResult(ref core, journal.ReplayIdentity);

        Assert.True(rollback.FaultOnlyRollback);
        Assert.Equal(journal.OriginalRetireOutcome, replay);
        Assert.Equal(MatrixTileReplayRollbackLifecycle.FaultReplayed, journal.Lifecycle);
        Assert.False(core.TryCaptureAnyMatrixTileSnapshot(0, 7, out _));
    }

    [Fact]
    public void DescriptorFault_RetiresRollsBackAndReplaysDeterministically()
    {
        Processor.CPU_Core core = CreateCore(out _);
        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTILE_LOAD,
            primaryPointer: 0x100,
            secondaryPointer: 7);
        MatrixTileMemoryShapeContract contract =
            MatrixTileMemoryShapeAndFaultAbi.CreateLoadContract(
                microOp.TileDescriptor,
                0x100);
        MatrixTileExecutionCaptureRecord capture =
            MatrixTileExecuteCaptureAbi.CaptureLoad(
                MtileLoadInstruction.Mnemonic,
                MatrixTileProjectedOperationKind.Load,
                sourceTileId: 0,
                secondaryTileId: 7,
                destinationTileId: 7,
                microOp.TileDescriptor,
                microOp.SecondaryTileDescriptor,
                microOp.ResultTileDescriptor,
                contract,
                MatrixTileMemoryShapeValidationResult.Fault(
                    MatrixTileMemoryFaultKind.StrideTooSmall),
                (_, _) => null);
        capture = MatrixTilePolicyBoundIdentityAbi.Bind(
            capture,
            microOp.MaterializedInstruction,
            microOp.DependencyMetadata,
            ownerThreadId: 0,
            memoryDomainId: 0,
            core.GetMatrixTileReplayInvalidationEpoch());
        MatrixTileCaptureIdentity captureIdentity =
            MatrixTileRetirePublicationAbi.CreateCaptureIdentity(
                core.CoreID,
                ownerThreadId: 0,
                IsaOpcodeValues.MTILE_LOAD,
                MatrixTileProjectedOperationKind.Load,
                core.AllocateMatrixTileCaptureOrdinal(),
                capture);
        capture = capture with { CaptureIdentity = captureIdentity };

        MatrixTileReplayRollbackJournal journal =
            MatrixTileReplayRollbackAbi.RetireWithCheckpoint(
                ref core,
                microOp.MaterializedInstruction,
                capture,
                captureIdentity,
                core.AllocateMatrixTileReplayCheckpointOrdinal());

        MatrixTileReplayRollbackAbi.Rollback(
            ref core,
            journal,
            journal.ReplayIdentity);
        MatrixTileRetireOutcome replay =
            MatrixTileReplayRollbackAbi.Replay(
                ref core,
                microOp.MaterializedInstruction,
                journal,
                journal.ReplayIdentity);

        Assert.True(replay.FaultRetired);
        Assert.Equal(MatrixTileMemoryFaultKind.StrideTooSmall, replay.MemoryFaultKind);
        Assert.False(core.TryCaptureAnyMatrixTileSnapshot(0, 7, out _));
    }

    [Fact]
    public void MaccSemanticFault_RetiresRollsBackAndReplaysDeterministically()
    {
        Processor.CPU_Core core = CreateCore(out _);
        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTILE_MACC,
            primaryPointer: 1,
            secondaryPointer: (3UL << 16) | 2UL);
        MatrixTileExecutionCaptureRecord capture =
            MatrixTileExecuteCaptureAbi.CaptureMacc(
                MtileMaccInstruction.Mnemonic,
                MatrixTileProjectedOperationKind.Macc,
                sourceTileId: 1,
                secondaryTileId: 2,
                destinationTileId: 3,
                microOp.TileDescriptor,
                microOp.SecondaryTileDescriptor,
                microOp.ResultTileDescriptor,
                MatrixTileSemanticValidationResult.Fault(
                    MatrixTileSemanticFaultKind.MaccInnerDimensionMismatch),
                maccContract: null,
                leftSnapshot: default,
                rightSnapshot: default,
                accumulatorSnapshot: default);
        capture = MatrixTilePolicyBoundIdentityAbi.Bind(
            capture,
            microOp.MaterializedInstruction,
            microOp.DependencyMetadata,
            ownerThreadId: 0,
            memoryDomainId: 0,
            core.GetMatrixTileReplayInvalidationEpoch());
        MatrixTileCaptureIdentity captureIdentity =
            MatrixTileRetirePublicationAbi.CreateCaptureIdentity(
                core.CoreID,
                ownerThreadId: 0,
                IsaOpcodeValues.MTILE_MACC,
                MatrixTileProjectedOperationKind.Macc,
                core.AllocateMatrixTileCaptureOrdinal(),
                capture);
        capture = capture with { CaptureIdentity = captureIdentity };

        MatrixTileReplayRollbackJournal journal =
            MatrixTileReplayRollbackAbi.RetireWithCheckpoint(
                ref core,
                microOp.MaterializedInstruction,
                capture,
                captureIdentity,
                core.AllocateMatrixTileReplayCheckpointOrdinal());

        MatrixTileRollbackOutcome rollback =
            MatrixTileReplayRollbackAbi.Rollback(
                ref core,
                journal,
                journal.ReplayIdentity);
        MatrixTileRetireOutcome replay =
            MatrixTileReplayRollbackAbi.Replay(
                ref core,
                microOp.MaterializedInstruction,
                journal,
                journal.ReplayIdentity);

        Assert.True(rollback.FaultOnlyRollback);
        Assert.Equal(journal.OriginalRetireOutcome, replay);
        Assert.Equal(
            MatrixTileSemanticFaultKind.MaccInnerDimensionMismatch,
            replay.SemanticFaultKind);
        Assert.False(core.TryCaptureAnyMatrixTileSnapshot(0, 3, out _));
    }

    [Fact]
    public void WrongCoreWrongIdentityDuplicateRollbackAndDuplicateReplayFailClosed()
    {
        Processor.CPU_Core core = CreateCore(out Processor.MainMemoryArea memory);
        Assert.True(memory.TryWritePhysicalRange(0x100, new byte[] { 1, 2 }));
        Assert.True(memory.TryWritePhysicalRange(0x102, new byte[] { 3, 4 }));
        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTILE_LOAD,
            primaryPointer: 0x100,
            secondaryPointer: 2);
        ExecuteAndRetire(ref core, microOp);
        MatrixTileReplayRollbackJournal journal = AssertJournal(microOp);

        Assert.Throws<MatrixTileReplayRollbackValidationException>(
            () => microOp.ReplayRolledBackResult(ref core, journal.ReplayIdentity));

        MatrixTileReplayIdentity wrongOwner =
            journal.ReplayIdentity with { OwnerThreadId = 1 };
        Assert.Throws<MatrixTileReplayRollbackValidationException>(
            () => microOp.RollbackRetiredResult(ref core, wrongOwner));

        MatrixTileReplayIdentity wrongInstruction =
            journal.ReplayIdentity with { Opcode = IsaOpcodeValues.MTILE_STORE };
        Assert.Throws<MatrixTileReplayRollbackValidationException>(
            () => microOp.RollbackRetiredResult(ref core, wrongInstruction));

        Processor.CPU_Core wrongCore = CreateCore(coreId: 1, out _);
        Assert.Throws<MatrixTileReplayRollbackValidationException>(
            () => MatrixTileReplayRollbackAbi.Rollback(
                ref wrongCore,
                journal,
                journal.ReplayIdentity));

        microOp.RollbackRetiredResult(ref core, journal.ReplayIdentity);
        Assert.Throws<MatrixTileReplayRollbackValidationException>(
            () => microOp.RollbackRetiredResult(ref core, journal.ReplayIdentity));

        microOp.ReplayRolledBackResult(ref core, journal.ReplayIdentity);
        Assert.Throws<MatrixTileReplayRollbackValidationException>(
            () => microOp.ReplayRolledBackResult(ref core, journal.ReplayIdentity));
    }

    [Fact]
    public void StaleTilePublicationAndUnregisteredHostJournalFailClosed()
    {
        Processor.CPU_Core core = CreateCore(out Processor.MainMemoryArea memory);
        Assert.True(memory.TryWritePhysicalRange(0x100, new byte[] { 1, 2 }));
        Assert.True(memory.TryWritePhysicalRange(0x102, new byte[] { 3, 4 }));
        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTILE_LOAD,
            primaryPointer: 0x100,
            secondaryPointer: 2);
        MatrixTileExecutionCaptureRecord capture = ExecuteAndRetire(ref core, microOp);
        MatrixTileReplayRollbackJournal journal = AssertJournal(microOp);

        byte[] replacement = [8, 8, 8, 8];
        core.SeedMatrixTileForRuntime(
            0,
            2,
            microOp.ResultTileDescriptor,
            replacement);
        Assert.Throws<MatrixTileReplayRollbackValidationException>(
            () => microOp.RollbackRetiredResult(ref core, journal.ReplayIdentity));
        AssertTile(ref core, 2, microOp.ResultTileDescriptor, replacement);

        var hostJournal = new MatrixTileReplayRollbackJournal(
            journal.ReplayIdentity with { CheckpointOrdinal = 999 },
            capture,
            journal.OriginalRetireOutcome,
            hadTileCheckpoint: false,
            tileCheckpoint: default,
            memoryCheckpoint: Array.Empty<MatrixTileCapturedMemoryWrite>());
        Assert.Throws<MatrixTileReplayRollbackValidationException>(
            () => MatrixTileReplayRollbackAbi.Rollback(
                ref core,
                hostJournal,
                hostJournal.ReplayIdentity));
    }

    [Fact]
    public void StoreRollbackFailureRestoresCommittedImageAndKeepsJournalRetired()
    {
        var memory = new FailSecondWriteMemory();
        memory.SetLength(0x400);
        Processor.CPU_Core core = CreateCore(memory);
        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTILE_STORE,
            primaryPointer: 0x180,
            secondaryPointer: 2);
        byte[] source = [5, 6, 7, 8];
        core.SeedMatrixTileForRuntime(0, 2, microOp.TileDescriptor, source);
        WriteRows(memory, 0x180, [21, 22, 23, 24]);
        ExecuteAndRetire(ref core, microOp);
        MatrixTileReplayRollbackJournal journal = AssertJournal(microOp);

        memory.Arm();
        Assert.Throws<InvalidOperationException>(
            () => microOp.RollbackRetiredResult(ref core, journal.ReplayIdentity));

        Assert.Equal(source, ReadRows(memory, 0x180));
        Assert.Equal(MatrixTileReplayRollbackLifecycle.Retired, journal.Lifecycle);
        microOp.RollbackRetiredResult(ref core, journal.ReplayIdentity);
        Assert.Equal([21, 22, 23, 24], ReadRows(memory, 0x180));
    }

    [Fact]
    public void DiscardReleasesReplayAuthorityAndBlocksLaterRollbackOrReplay()
    {
        Processor.CPU_Core core = CreateCore(out Processor.MainMemoryArea memory);
        Assert.True(memory.TryWritePhysicalRange(0x100, new byte[] { 1, 2 }));
        Assert.True(memory.TryWritePhysicalRange(0x102, new byte[] { 3, 4 }));
        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTILE_LOAD,
            primaryPointer: 0x100,
            secondaryPointer: 2);
        ExecuteAndRetire(ref core, microOp);
        MatrixTileReplayRollbackJournal journal = AssertJournal(microOp);

        microOp.DiscardReplayRollbackAuthority(ref core, journal.ReplayIdentity);

        Assert.Equal(
            MatrixTileReplayRollbackLifecycle.Discarded,
            journal.Lifecycle);
        Assert.Throws<MatrixTileReplayRollbackValidationException>(
            () => MatrixTileReplayRollbackAbi.Rollback(
                ref core,
                journal,
                journal.ReplayIdentity));
        Assert.Throws<MatrixTileReplayRollbackValidationException>(
            () => MatrixTileReplayRollbackAbi.Replay(
                ref core,
                microOp.MaterializedInstruction,
                journal,
                journal.ReplayIdentity));
    }

    [Fact]
    public void KeepsFallbackStatusCatalogAndCompilerBoundariesClosed()
    {
        Assert.True(MatrixTileReplayRollbackAbi.HasReplayRollbackConformance);
        Assert.True(MatrixTileReplayRollbackAbi.BlocksReplayWithoutRetirePublication);
        Assert.True(MatrixTileReplayRollbackAbi.BlocksCaptureRecordIdentityBypass);
        Assert.True(MatrixTileReplayRollbackAbi.KeepsHostOwnedEvidenceNonArchitectural);
        Assert.True(MatrixTileReplayRollbackAbi.KeepsCompilerScopeClosed);
        Assert.False(MatrixTileReplayRollbackAbi.UsesFallbackPath);

        foreach (string mnemonic in new[]
                 {
                     "MTILE_LOAD",
                     "MTILE_STORE",
                     "MTILE_MACC",
                     "MTRANSPOSE"
                 })
        {
            InstructionSupportStatus status =
                InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
            Assert.True(status.HasExecutionSemantics);
            Assert.True(status.IsExecutableClaim);
        }
    }

    private static MatrixTileExecutionCaptureRecord ExecuteAndRetire(
        ref Processor.CPU_Core core,
        MatrixTileMicroOp microOp)
    {
        Assert.True(microOp.Execute(ref core));
        MatrixTileExecutionCaptureRecord capture =
            Assert.IsType<MatrixTileExecutionCaptureRecord>(
                microOp.LastExecutionCapture!.Value);
        MatrixTileRetireOutcome outcome =
            microOp.RetireCapturedResult(ref core, capture);
        Assert.Equal(capture.HasFault, outcome.FaultRetired);
        return capture;
    }

    private static MatrixTileReplayRollbackJournal AssertJournal(
        MatrixTileMicroOp microOp)
    {
        MatrixTileReplayRollbackJournal journal =
            Assert.IsType<MatrixTileReplayRollbackJournal>(
                microOp.LastReplayRollbackJournal);
        Assert.True(journal.ReplayIdentity.IsValid);
        return journal;
    }

    private static void AssertReplayIdentity(
        MatrixTileReplayIdentity replayIdentity,
        MatrixTileCaptureIdentity captureIdentity)
    {
        Assert.Equal(captureIdentity.CoreId, replayIdentity.CoreId);
        Assert.Equal(captureIdentity.OwnerThreadId, replayIdentity.OwnerThreadId);
        Assert.Equal(captureIdentity.Opcode, replayIdentity.Opcode);
        Assert.Equal(captureIdentity.OperationKind, replayIdentity.OperationKind);
        Assert.Equal(captureIdentity.CaptureOrdinal, replayIdentity.CaptureOrdinal);
        Assert.Equal(captureIdentity.CaptureFingerprint, replayIdentity.CaptureFingerprint);
        Assert.NotEqual(0UL, replayIdentity.DecodedInstructionFingerprint);
        Assert.NotEqual(0UL, replayIdentity.MaterializedInstructionFingerprint);
        Assert.NotEqual(0UL, replayIdentity.DescriptorFingerprint);
        Assert.NotEqual(0UL, replayIdentity.CheckpointOrdinal);
    }

    private static void AssertTile(
        ref Processor.CPU_Core core,
        ushort tileId,
        MatrixTileCanonicalDescriptorAbi descriptor,
        byte[] expected)
    {
        Assert.True(core.TryCaptureMatrixTileSnapshot(
            0,
            tileId,
            descriptor,
            out MatrixTileTileImage snapshot));
        Assert.Equal(expected, snapshot.Data);
    }

    private static Processor.CPU_Core CreateCore(
        out Processor.MainMemoryArea memory) =>
        CreateCore(coreId: 0, out memory);

    private static Processor.CPU_Core CreateCore(
        ushort coreId,
        out Processor.MainMemoryArea memory)
    {
        memory = new Processor.MainMemoryArea();
        memory.SetLength(0x400);
        return CreateCore(memory, coreId);
    }

    private static Processor.CPU_Core CreateCore(
        Processor.MainMemoryArea memory,
        ushort coreId = 0)
    {
        CpuCorePlatformContext context =
            CpuCorePlatformContext.CreateFixed(memory, ProcessorMode.Emulation);
        return new Processor.CPU_Core(coreId, context);
    }

    private static MatrixTileMicroOp CreateMicroOp(
        InstructionsEnum opcode,
        ulong primaryPointer,
        ulong secondaryPointer)
    {
        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            Immediate = 2,
            HasImmediate = true,
            DataType = (byte)DataTypeEnum.INT8,
            HasDataType = true,
            VectorPrimaryPointer = primaryPointer,
            VectorSecondaryPointer = secondaryPointer,
            VectorStreamLength = 4,
            VectorStride = 1,
            VectorRowStride = 2,
            MatrixTileNumericPolicy = opcode == InstructionsEnum.MTILE_MACC
                ? MatrixTileNumericPolicyAbi.CreateSupportedPolicy(
                    MatrixTileNumericProfileId.SignedInt8ToInt32)
                : null,
            MatrixTileLayoutPolicy = opcode switch
            {
                InstructionsEnum.MTILE_MACC => MatrixTileLayoutPolicyAbi.CreateMaccPolicy(),
                InstructionsEnum.MTRANSPOSE => MatrixTileLayoutPolicyAbi.CreateTransposePolicy(),
                _ => null
            },
            HasVectorPayload = true,
            HasVectorAddressingContour = true,
            Is2DAddressing = true,
            IndexedAddressing = false,
            PredicateMask = 0
        };

        return Assert.IsAssignableFrom<MatrixTileMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)opcode, context));
    }

    private static void WriteRows(
        Processor.MainMemoryArea memory,
        ulong baseAddress,
        byte[] packed)
    {
        Assert.True(memory.TryWritePhysicalRange(baseAddress, packed.AsSpan(0, 2)));
        Assert.True(memory.TryWritePhysicalRange(baseAddress + 2, packed.AsSpan(2, 2)));
    }

    private static byte[] ReadRows(
        Processor.MainMemoryArea memory,
        ulong baseAddress)
    {
        byte[] packed = new byte[4];
        Assert.True(memory.TryReadPhysicalRange(baseAddress, packed.AsSpan(0, 2)));
        Assert.True(memory.TryReadPhysicalRange(baseAddress + 2, packed.AsSpan(2, 2)));
        return packed;
    }

    private sealed class FailSecondWriteMemory : Processor.MainMemoryArea
    {
        private bool _armed;
        private int _writeCount;

        public void Arm()
        {
            _armed = true;
            _writeCount = 0;
        }

        public override bool TryWritePhysicalRange(
            ulong physicalAddress,
            ReadOnlySpan<byte> buffer)
        {
            if (_armed && ++_writeCount == 2)
            {
                _armed = false;
                return false;
            }

            return base.TryWritePhysicalRange(physicalAddress, buffer);
        }
    }
}
