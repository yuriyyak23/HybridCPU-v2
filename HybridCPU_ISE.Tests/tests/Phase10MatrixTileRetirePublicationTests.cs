using System;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase10;

public sealed class Phase10MatrixTileRetirePublicationTests
{
    [Fact]
    public void MtileLoad_CaptureIsInvisibleUntilRetire()
    {
        Processor.CPU_Core core = CreateCore(out Processor.MainMemoryArea memory);
        byte[] packed = [1, 2, 3, 4];
        Assert.True(memory.TryWritePhysicalRange(0x100, packed.AsSpan(0, 2)));
        Assert.True(memory.TryWritePhysicalRange(0x102, packed.AsSpan(2, 2)));

        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTILE_LOAD,
            primaryPointer: 0x100,
            secondaryPointer: 2);
        Assert.True(microOp.Execute(ref core));

        MatrixTileExecutionCaptureRecord capture = AssertCapture(microOp);
        Assert.False(core.TryCaptureMatrixTileSnapshot(
            ownerThreadId: 0,
            tileId: 2,
            capture.ResultTileDescriptor,
            out _));

        MatrixTileRetireOutcome outcome =
            microOp.RetireCapturedResult(ref core, capture);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(MatrixTileRetirePublicationKind.TileState, outcome.PublicationKind);
        Assert.True(core.TryCaptureMatrixTileSnapshot(
            ownerThreadId: 0,
            tileId: 2,
            capture.ResultTileDescriptor,
            out MatrixTileTileImage published));
        Assert.Equal(packed, published.Data);
    }

    [Fact]
    public void MtileStore_DoesNotWriteMemoryBeforeRetire()
    {
        Processor.CPU_Core core = CreateCore(out Processor.MainMemoryArea memory);
        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTILE_STORE,
            primaryPointer: 0x180,
            secondaryPointer: 2);
        byte[] packed = [5, 6, 7, 8];
        core.SeedMatrixTileForRuntime(
            ownerThreadId: 0,
            tileId: 2,
            microOp.TileDescriptor,
            packed);

        Assert.True(microOp.Execute(ref core));
        MatrixTileExecutionCaptureRecord capture = AssertCapture(microOp);
        Assert.Equal(new byte[4], ReadRows(memory, 0x180));

        MatrixTileRetireOutcome outcome =
            microOp.RetireCapturedResult(ref core, capture);

        Assert.True(outcome.IsSuccess);
        Assert.True(outcome.CommittedMemory);
        Assert.Equal(packed, ReadRows(memory, 0x180));
    }

    [Fact]
    public void MtileMacc_AccumulatorIsPublishedOnlyAtRetire()
    {
        Processor.CPU_Core core = CreateCore(out _);
        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTILE_MACC,
            primaryPointer: 1,
            secondaryPointer: (3UL << 16) | 2UL);
        core.SeedMatrixTileForRuntime(0, 1, microOp.TileDescriptor, [1, 2, 3, 4]);
        core.SeedMatrixTileForRuntime(0, 2, microOp.SecondaryTileDescriptor, [1, 0, 0, 1]);
        byte[] initialAccumulator = new byte[
            MatrixTileExecuteCaptureAbi.GetPackedByteLength(microOp.ResultTileDescriptor)];
        core.SeedMatrixTileForRuntime(0, 3, microOp.ResultTileDescriptor, initialAccumulator);

        Assert.True(microOp.Execute(ref core));
        MatrixTileExecutionCaptureRecord capture = AssertCapture(microOp);
        Assert.True(core.TryCaptureMatrixTileSnapshot(
            0,
            3,
            microOp.ResultTileDescriptor,
            out MatrixTileTileImage beforeRetire));
        Assert.Equal(initialAccumulator, beforeRetire.Data);

        MatrixTileRetireOutcome outcome =
            microOp.RetireCapturedResult(ref core, capture);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(MatrixTileRetirePublicationKind.Accumulator, outcome.PublicationKind);
        Assert.True(core.TryCaptureMatrixTileSnapshot(
            0,
            3,
            microOp.ResultTileDescriptor,
            out MatrixTileTileImage afterRetire));
        Assert.Equal(capture.ResultImage.Data, afterRetire.Data);
        Assert.NotEqual(initialAccumulator, afterRetire.Data);
    }

    [Fact]
    public void Mtranspose_DestinationIsPublishedOnlyAtRetire()
    {
        Processor.CPU_Core core = CreateCore(out _);
        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTRANSPOSE,
            primaryPointer: 1,
            secondaryPointer: 2);
        core.SeedMatrixTileForRuntime(0, 1, microOp.TileDescriptor, [1, 2, 3, 4]);
        byte[] initialDestination = [0, 0, 0, 0];
        core.SeedMatrixTileForRuntime(
            0,
            2,
            microOp.ResultTileDescriptor,
            initialDestination);

        Assert.True(microOp.Execute(ref core));
        MatrixTileExecutionCaptureRecord capture = AssertCapture(microOp);
        Assert.True(core.TryCaptureMatrixTileSnapshot(
            0,
            2,
            microOp.ResultTileDescriptor,
            out MatrixTileTileImage beforeRetire));
        Assert.Equal(initialDestination, beforeRetire.Data);

        MatrixTileRetireOutcome outcome =
            microOp.RetireCapturedResult(ref core, capture);

        Assert.True(outcome.IsSuccess);
        Assert.True(core.TryCaptureMatrixTileSnapshot(
            0,
            2,
            microOp.ResultTileDescriptor,
            out MatrixTileTileImage afterRetire));
        Assert.Equal([1, 3, 2, 4], afterRetire.Data);
    }

    [Fact]
    public void WriteBackRetireHook_OwnsSuccessfulCapturePublication()
    {
        Processor.CPU_Core core = CreateCore(out Processor.MainMemoryArea memory);
        Assert.True(memory.TryWritePhysicalRange(0x100, new byte[] { 1, 2 }));
        Assert.True(memory.TryWritePhysicalRange(0x102, new byte[] { 3, 4 }));
        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTILE_LOAD,
            primaryPointer: 0x100,
            secondaryPointer: 6);

        Assert.True(microOp.Execute(ref core));
        MatrixTileExecutionCaptureRecord capture = AssertCapture(microOp);
        Assert.False(core.TryCaptureMatrixTileSnapshot(
            0,
            6,
            capture.ResultTileDescriptor,
            out _));

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(
            ref core,
            retireRecords,
            ref retireRecordCount);

        Assert.Equal(0, retireRecordCount);
        Assert.Equal(MatrixTileCaptureLifecycle.Retired, microOp.CaptureLifecycle);
        Assert.True(microOp.LastRetireOutcome!.Value.IsSuccess);
        Assert.True(core.TryCaptureMatrixTileSnapshot(
            0,
            6,
            capture.ResultTileDescriptor,
            out MatrixTileTileImage published));
        Assert.Equal([1, 2, 3, 4], published.Data);
        Assert.Throws<MatrixTileRetireValidationException>(
            () => EmitWriteBackAgain(ref core, microOp));
    }

    [Fact]
    public void WriteBackRetireHook_RetiresFaultWithoutPublication()
    {
        Processor.CPU_Core core = CreateCore(out _);
        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTILE_LOAD,
            primaryPointer: 0x1000,
            secondaryPointer: 7);

        Assert.True(microOp.Execute(ref core));
        MatrixTileExecutionCaptureRecord capture = AssertCapture(microOp);
        MatrixTileRetireFaultException exception =
            Assert.Throws<MatrixTileRetireFaultException>(
                () => EmitWriteBackAgain(ref core, microOp));

        Assert.True(exception.Outcome.FaultRetired);
        Assert.Equal(MatrixTileCaptureLifecycle.FaultRetired, microOp.CaptureLifecycle);
        Assert.False(core.TryCaptureMatrixTileSnapshot(
            0,
            7,
            capture.ResultTileDescriptor,
            out _));
    }

    [Fact]
    public void FaultedCapture_RetiresWithoutArchitecturalPublication()
    {
        Processor.CPU_Core core = CreateCore(out _);
        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTILE_LOAD,
            primaryPointer: 0x1000,
            secondaryPointer: 4);

        Assert.True(microOp.Execute(ref core));
        MatrixTileExecutionCaptureRecord capture = AssertCapture(microOp);
        Assert.True(capture.HasFault);
        Assert.Equal(MatrixTileMemoryFaultKind.PartialMemoryFault, capture.MemoryFaultKind);

        MatrixTileRetireOutcome outcome =
            microOp.RetireCapturedResult(ref core, capture);

        Assert.True(outcome.FaultRetired);
        Assert.False(outcome.PublishedArchitecturalState);
        Assert.False(outcome.CommittedMemory);
        Assert.False(core.TryCaptureMatrixTileSnapshot(
            0,
            4,
            microOp.ResultTileDescriptor,
            out _));
    }

    [Fact]
    public void DescriptorShapeFaultCapture_RetiresDeterministicallyWithoutPublication()
    {
        Processor.CPU_Core core = CreateCore(out _);
        MatrixTileCanonicalDescriptorAbi descriptor =
            MatrixTileCanonicalDescriptorAbi.Create(2, 2, 1, 2);
        MatrixTileMemoryShapeContract contract =
            MatrixTileMemoryShapeAndFaultAbi.CreateLoadContract(descriptor, 0x100);
        MatrixTileExecutionCaptureRecord capture =
            MatrixTileExecuteCaptureAbi.CaptureLoad(
                MtileLoadInstruction.Mnemonic,
                MatrixTileProjectedOperationKind.Load,
                sourceTileId: 0,
                secondaryTileId: 7,
                destinationTileId: 7,
                descriptor,
                secondaryTileDescriptor: default,
                resultTileDescriptor: descriptor,
                contract,
                MatrixTileMemoryShapeValidationResult.Fault(
                    MatrixTileMemoryFaultKind.StrideTooSmall),
                (_, _) => null);
        MatrixTileCaptureIdentity identity =
            MatrixTileRetirePublicationAbi.CreateCaptureIdentity(
                core.CoreID,
                ownerThreadId: 0,
                IsaOpcodeValues.MTILE_LOAD,
                MatrixTileProjectedOperationKind.Load,
                captureOrdinal: 1,
                capture);
        capture = capture with { CaptureIdentity = identity };

        MatrixTileRetireOutcome outcome =
            MatrixTileRetirePublicationAbi.Retire(ref core, capture, identity);

        Assert.True(outcome.FaultRetired);
        Assert.Equal(MatrixTileMemoryFaultKind.StrideTooSmall, outcome.MemoryFaultKind);
        Assert.False(core.TryCaptureMatrixTileSnapshot(0, 7, descriptor, out _));
    }

    [Fact]
    public void MtileStore_PartialCommitFailureRollsBackEveryRow()
    {
        var memory = new FailSecondWriteMemory();
        memory.SetLength(0x400);
        Processor.CPU_Core core = CreateCore(memory);
        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTILE_STORE,
            primaryPointer: 0x180,
            secondaryPointer: 2);
        core.SeedMatrixTileForRuntime(
            0,
            2,
            microOp.TileDescriptor,
            [9, 10, 11, 12]);
        byte[] original = [21, 22, 23, 24];
        Assert.True(memory.TryWritePhysicalRange(0x180, original.AsSpan(0, 2)));
        Assert.True(memory.TryWritePhysicalRange(0x182, original.AsSpan(2, 2)));

        Assert.True(microOp.Execute(ref core));
        MatrixTileExecutionCaptureRecord capture = AssertCapture(microOp);
        memory.Arm();

        MatrixTileRetireOutcome outcome =
            microOp.RetireCapturedResult(ref core, capture);

        Assert.True(outcome.FaultRetired);
        Assert.Equal(MatrixTileRetireFaultKind.MemoryCommitFault, outcome.RetireFaultKind);
        Assert.Equal(original, ReadRows(memory, 0x180));
    }

    [Fact]
    public void MissingMismatchedDuplicateCancelledAndWrongInstructionRetireFailClosed()
    {
        Processor.CPU_Core core = CreateCore(out Processor.MainMemoryArea memory);
        Assert.True(memory.TryWritePhysicalRange(0x100, new byte[] { 1, 2 }));
        Assert.True(memory.TryWritePhysicalRange(0x102, new byte[] { 3, 4 }));

        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTILE_LOAD,
            primaryPointer: 0x100,
            secondaryPointer: 2);
        Assert.Throws<MatrixTileRetireValidationException>(
            () => RetireDefault(ref core, microOp));

        Assert.True(microOp.Execute(ref core));
        MatrixTileExecutionCaptureRecord capture = AssertCapture(microOp);
        MatrixTileExecutionCaptureRecord wrongOwner = capture with
        {
            CaptureIdentity = capture.CaptureIdentity with { OwnerThreadId = 1 }
        };
        Assert.Throws<MatrixTileRetireValidationException>(
            () => microOp.RetireCapturedResult(ref core, wrongOwner));

        MatrixTileExecutionCaptureRecord wrongInstruction = capture with
        {
            Mnemonic = MtileStoreInstruction.Mnemonic
        };
        Assert.Throws<MatrixTileRetireValidationException>(
            () => microOp.RetireCapturedResult(ref core, wrongInstruction));

        Assert.True(microOp.RetireCapturedResult(ref core, capture).IsSuccess);
        Assert.Throws<MatrixTileRetireValidationException>(
            () => microOp.RetireCapturedResult(ref core, capture));

        MatrixTileMicroOp cancelled = CreateMicroOp(
            InstructionsEnum.MTILE_LOAD,
            primaryPointer: 0x100,
            secondaryPointer: 3);
        Assert.True(cancelled.Execute(ref core));
        MatrixTileExecutionCaptureRecord cancelledCapture = AssertCapture(cancelled);
        cancelled.CancelCapturedResult();
        Assert.Throws<MatrixTileRetireValidationException>(
            () => cancelled.RetireCapturedResult(ref core, cancelledCapture));
    }

    [Fact]
    public void HostOwnedOrTamperedEvidenceCannotBecomeGuestTileState()
    {
        Processor.CPU_Core core = CreateCore(out Processor.MainMemoryArea memory);
        Assert.True(memory.TryWritePhysicalRange(0x100, new byte[] { 1, 2 }));
        Assert.True(memory.TryWritePhysicalRange(0x102, new byte[] { 3, 4 }));
        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTILE_LOAD,
            primaryPointer: 0x100,
            secondaryPointer: 5);
        Assert.True(microOp.Execute(ref core));
        MatrixTileExecutionCaptureRecord capture = AssertCapture(microOp);
        byte[] tamperedData = (byte[])capture.ResultImage.Data.Clone();
        tamperedData[0] ^= 0x7F;
        MatrixTileExecutionCaptureRecord tampered = capture with
        {
            ResultImage = capture.ResultImage with { Data = tamperedData }
        };

        Assert.Throws<MatrixTileRetireValidationException>(
            () => microOp.RetireCapturedResult(ref core, tampered));
        Assert.False(core.TryCaptureMatrixTileSnapshot(
            0,
            5,
            microOp.ResultTileDescriptor,
            out _));
    }

    [Fact]
    public void InvalidArchitecturalOwnerCannotAliasGuestThreadZero()
    {
        MatrixTileMicroOp microOp = CreateMicroOp(
            InstructionsEnum.MTILE_LOAD,
            primaryPointer: 0x100,
            secondaryPointer: 5);
        var registerFile = new MatrixTileArchitecturalTileRegisterFile();
        byte[] packed = [1, 2, 3, 4];
        registerFile.WriteTileForRuntimeSeed(
            ownerThreadId: 0,
            tileId: 5,
            microOp.ResultTileDescriptor,
            packed);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => registerFile.WriteTileForRuntimeSeed(
                ownerThreadId: -1,
                tileId: 5,
                microOp.ResultTileDescriptor,
                packed));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => registerFile.TryCaptureAnySnapshot(
                Processor.CPU_Core.SmtWays,
                tileId: 5,
                out _));

        Assert.True(registerFile.TryCaptureAnySnapshot(0, 5, out MatrixTileTileImage snapshot));
        Assert.Equal(packed, snapshot.Data);
    }

    [Fact]
    public void IdenticalCaptureFromDifferentMicroOpCannotRetireThroughWrongOwner()
    {
        Processor.CPU_Core core = CreateCore(out Processor.MainMemoryArea memory);
        Assert.True(memory.TryWritePhysicalRange(0x100, new byte[] { 1, 2 }));
        Assert.True(memory.TryWritePhysicalRange(0x102, new byte[] { 3, 4 }));
        MatrixTileMicroOp first = CreateMicroOp(
            InstructionsEnum.MTILE_LOAD,
            primaryPointer: 0x100,
            secondaryPointer: 5);
        MatrixTileMicroOp second = CreateMicroOp(
            InstructionsEnum.MTILE_LOAD,
            primaryPointer: 0x100,
            secondaryPointer: 5);

        Assert.True(first.Execute(ref core));
        Assert.True(second.Execute(ref core));
        MatrixTileExecutionCaptureRecord firstCapture = AssertCapture(first);
        MatrixTileExecutionCaptureRecord secondCapture = AssertCapture(second);
        Assert.NotEqual(
            firstCapture.CaptureIdentity.CaptureOrdinal,
            secondCapture.CaptureIdentity.CaptureOrdinal);

        Assert.Throws<MatrixTileRetireValidationException>(
            () => second.RetireCapturedResult(ref core, firstCapture));
        Assert.False(core.TryCaptureMatrixTileSnapshot(
            0,
            5,
            firstCapture.ResultTileDescriptor,
            out _));
    }

    [Fact]
    public void Phase10RuntimePathKeepsFallbackAndPromotionBoundariesClosed()
    {
        Assert.False(MatrixTileRetirePublicationAbi.UsesFallbackPath);
        Assert.True(MatrixTileRetirePublicationAbi.KeepsHostOwnedEvidenceNonArchitectural);
        Assert.True(MatrixTileRetirePublicationAbi.KeepsReplayRollbackNonAuthority);
        Assert.True(MatrixTileRetirePublicationAbi.KeepsCompilerScopeClosed);

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

    private static MatrixTileExecutionCaptureRecord AssertCapture(
        MatrixTileMicroOp microOp)
    {
        MatrixTileExecutionCaptureRecord? capture = microOp.LastExecutionCapture;
        Assert.True(capture.HasValue);
        Assert.True(capture.Value.HasRetireCorrelation);
        Assert.Equal(MatrixTileCaptureLifecycle.Captured, microOp.CaptureLifecycle);
        return capture.Value;
    }

    private static void RetireDefault(
        ref Processor.CPU_Core core,
        MatrixTileMicroOp microOp)
    {
        microOp.RetireCapturedResult(ref core, default);
    }

    private static void EmitWriteBackAgain(
        ref Processor.CPU_Core core,
        MatrixTileMicroOp microOp)
    {
        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(
            ref core,
            retireRecords,
            ref retireRecordCount);
    }

    private static Processor.CPU_Core CreateCore(
        out Processor.MainMemoryArea memory)
    {
        memory = new Processor.MainMemoryArea();
        memory.SetLength(0x400);
        return CreateCore(memory);
    }

    private static Processor.CPU_Core CreateCore(
        Processor.MainMemoryArea memory)
    {
        CpuCorePlatformContext context =
            CpuCorePlatformContext.CreateFixed(memory, ProcessorMode.Emulation);
        return new Processor.CPU_Core(0, context);
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
            HasVectorPayload = true,
            HasVectorAddressingContour = true,
            Is2DAddressing = true,
            IndexedAddressing = false,
            PredicateMask = 0
        };

        return Assert.IsAssignableFrom<MatrixTileMicroOp>(
            InstructionRegistry.CreateMicroOp((uint)opcode, context));
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
                return false;
            }

            return base.TryWritePhysicalRange(physicalAddress, buffer);
        }
    }
}
