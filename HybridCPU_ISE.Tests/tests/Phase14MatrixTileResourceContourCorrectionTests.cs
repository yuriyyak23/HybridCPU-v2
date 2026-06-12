using System;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase14;

public sealed class Phase14MatrixTileResourceContourCorrectionTests
{
    [Theory]
    [InlineData(InstructionsEnum.MTILE_LOAD, MatrixTileRuntimeResourceClass.MatrixTileMemory, SlotClass.MatrixTileStreamClass, MicroOpClass.MatrixTileMemory)]
    [InlineData(InstructionsEnum.MTILE_STORE, MatrixTileRuntimeResourceClass.MatrixTileMemory, SlotClass.MatrixTileStreamClass, MicroOpClass.MatrixTileMemory)]
    [InlineData(InstructionsEnum.MTILE_MACC, MatrixTileRuntimeResourceClass.MatrixTileCompute, SlotClass.AluClass, MicroOpClass.MatrixTileCompute)]
    [InlineData(InstructionsEnum.MTRANSPOSE, MatrixTileRuntimeResourceClass.MatrixTileCompute, SlotClass.AluClass, MicroOpClass.MatrixTileCompute)]
    public void RuntimeClassificationPrecedesGenericSemanticClassMapping(
        InstructionsEnum opcode,
        MatrixTileRuntimeResourceClass expectedResourceClass,
        SlotClass expectedSlotClass,
        MicroOpClass expectedMicroOpClass)
    {
        MatrixTileMicroOp microOp = CreateMicroOp(opcode);

        Assert.Equal(expectedResourceClass, MatrixTileResourceContour.Classify((uint)opcode));
        Assert.Equal(expectedResourceClass, microOp.RuntimeResourceClass);
        Assert.Equal(expectedSlotClass, microOp.Placement.RequiredSlotClass);
        Assert.Equal(expectedMicroOpClass, microOp.Class);
        Assert.Equal(
            expectedResourceClass == MatrixTileRuntimeResourceClass.MatrixTileMemory,
            MatrixTileResourceContour.IsMatrixTileMemoryOpcode((uint)opcode));
        Assert.Equal(
            expectedResourceClass == MatrixTileRuntimeResourceClass.MatrixTileCompute,
            MatrixTileResourceContour.IsMatrixTileComputeOpcode((uint)opcode));

        if (expectedResourceClass == MatrixTileRuntimeResourceClass.MatrixTileMemory)
        {
            Assert.Equal(InstructionClass.Memory, microOp.InstructionClass);
            Assert.NotEqual(SlotClass.LsuClass, microOp.Placement.RequiredSlotClass);
            Assert.NotEqual(SlotClass.AluClass, microOp.Placement.RequiredSlotClass);
            Assert.Equal((byte)0b_0100_0000, microOp.SchedulerLaneMask);
        }
        else
        {
            Assert.Equal(InstructionClass.ScalarAlu, microOp.InstructionClass);
            Assert.Equal((byte)0b_0000_1111, microOp.SchedulerLaneMask);
        }
    }

    [Fact]
    public void MatrixTileLane6ClassIsCapacityOneAndPhysicallyAliasesDscWithoutAbiMixing()
    {
        Assert.Equal(
            SlotClassLaneMap.GetLaneMask(SlotClass.DmaStreamClass),
            SlotClassLaneMap.GetLaneMask(SlotClass.MatrixTileStreamClass));
        Assert.Equal(1, SlotClassLaneMap.GetClassCapacity(SlotClass.MatrixTileStreamClass));
        Assert.Contains(
            SlotClass.DmaStreamClass,
            SlotClassLaneMap.GetAliasedClasses(SlotClass.MatrixTileStreamClass).ToArray());

        var capacity = new SlotClassCapacityState();
        capacity.InitializeFromLaneMap();
        capacity.IncrementOccupancy(SlotClass.MatrixTileStreamClass);
        Assert.False(capacity.HasFreeCapacity(SlotClass.MatrixTileStreamClass));
        Assert.False(capacity.HasFreeCapacity(SlotClass.DmaStreamClass));

        MatrixTileMicroOp load = CreateMicroOp(InstructionsEnum.MTILE_LOAD);
        var bundle = new MicroOp?[BundleMetadata.BundleSlotCount];
        bundle[6] = load;
        TypedSlotBundleFacts facts = TypedSlotBundleFacts.FromBundle(bundle);
        Assert.Equal(1, facts.MatrixTileStreamCount);
        Assert.Equal(0, facts.DmaStreamCount);
        Assert.IsNotType<DmaStreamComputeMicroOp>(load);

        var conflictingBundle = new MicroOp?[BundleMetadata.BundleSlotCount];
        conflictingBundle[0] = load;
        conflictingBundle[1] = new DmaLaneClaimMicroOp();
        TypedSlotBundleFacts conflictingFacts =
            TypedSlotBundleFacts.FromBundle(conflictingBundle);
        Assert.Equal(1, conflictingFacts.MatrixTileStreamCount);
        Assert.Equal(1, conflictingFacts.DmaStreamCount);
        Assert.False(SafetyVerifier.ValidateTypedSlotFacts(
            conflictingFacts,
            conflictingBundle));
    }

    [Fact]
    public void MemoryResourceMasksCarryOrderingStreamSrfAndTileChannelsWithoutLsuPlacement()
    {
        MatrixTileMicroOp load = CreateMicroOp(InstructionsEnum.MTILE_LOAD);
        MatrixTileMicroOp store = CreateMicroOp(InstructionsEnum.MTILE_STORE);

        AssertMaskContains(load.ResourceMask, ResourceMaskBuilder.ForLoad());
        AssertMaskContains(load.ResourceMask, ResourceMaskBuilder.ForStreamEngine(0));
        AssertMaskContains(load.ResourceMask, ResourceMaskBuilder.ForMatrixTileStreamWindow());
        AssertMaskContains(load.ResourceMask, ResourceMaskBuilder.ForMatrixTileIngress());
        AssertMaskContains(load.ResourceMask, ResourceMaskBuilder.ForMatrixTileStateWrite());
        AssertMaskContains(store.ResourceMask, ResourceMaskBuilder.ForStore());
        AssertMaskContains(store.ResourceMask, ResourceMaskBuilder.ForStreamEngine(0));
        AssertMaskContains(store.ResourceMask, ResourceMaskBuilder.ForMatrixTileStreamWindow());
        AssertMaskContains(store.ResourceMask, ResourceMaskBuilder.ForMatrixTileEgress());
        AssertMaskContains(store.ResourceMask, ResourceMaskBuilder.ForMatrixTileStateRead());
        Assert.Equal(SlotClass.MatrixTileStreamClass, load.Placement.RequiredSlotClass);
        Assert.Equal(SlotClass.MatrixTileStreamClass, store.Placement.RequiredSlotClass);
    }

    [Fact]
    public void ComputeResourceMasksExcludeLsuAndStreamOwnership()
    {
        MatrixTileMicroOp macc = CreateMicroOp(InstructionsEnum.MTILE_MACC);
        MatrixTileMicroOp transpose = CreateMicroOp(InstructionsEnum.MTRANSPOSE);
        ResourceBitset forbidden =
            ResourceMaskBuilder.ForLoad() |
            ResourceMaskBuilder.ForStore() |
            ResourceMaskBuilder.ForStreamEngine(0) |
            ResourceMaskBuilder.ForMatrixTileStreamWindow();

        Assert.True((macc.ResourceMask & forbidden).IsZero);
        Assert.True((transpose.ResourceMask & forbidden).IsZero);
        AssertMaskContains(macc.ResourceMask, ResourceMaskBuilder.ForMatrixTileAccumulatorRead());
        AssertMaskContains(macc.ResourceMask, ResourceMaskBuilder.ForMatrixTileAccumulatorWrite());
        AssertMaskContains(transpose.ResourceMask, ResourceMaskBuilder.ForMatrixTileTransposePolicy());
        AssertMaskContains(transpose.ResourceMask, ResourceMaskBuilder.ForMatrixTileStateRead());
        AssertMaskContains(transpose.ResourceMask, ResourceMaskBuilder.ForMatrixTileStateWrite());
    }

    [Fact]
    public void TypedLoadIngressIsArchitecturallyInvisibleUntilRetire()
    {
        Processor.CPU_Core core = CreateCore(out Processor.MainMemoryArea memory);
        Assert.True(memory.TryWritePhysicalRange(0x100, new byte[] { 1, 2, 3, 4 }));
        MatrixTileMicroOp load = CreateMicroOp(InstructionsEnum.MTILE_LOAD);

        Assert.True(load.Execute(ref core));
        MatrixTileExecutionCaptureRecord capture = AssertCapture(load);
        Assert.True(capture.StreamTransfer.IsTypedTransport);
        Assert.True(capture.StreamTransfer.Completed);
        Assert.Equal(MatrixTileStreamTransferDirection.MemoryIngress, capture.StreamTransfer.Direction);
        Assert.Equal(2, capture.StreamTransfer.Windows.Length);
        Assert.False(capture.StreamTransfer.PublishesArchitecturalTileState);
        Assert.False(capture.StreamTransfer.UsesDmaStreamComputeAuthority);
        Assert.False(capture.StreamTransfer.UsesGenericStreamExecutionAuthority);
        Assert.False(core.TryCaptureAnyMatrixTileSnapshot(0, 2, out _));

        MatrixTileRetireOutcome outcome = load.RetireCapturedResult(ref core, capture);
        Assert.True(outcome.IsSuccess);
        Assert.True(core.TryCaptureAnyMatrixTileSnapshot(0, 2, out MatrixTileTileImage image));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, image.Data);
    }

    [Fact]
    public void TypedStoreEgressDoesNotWriteBeforeRetireAndInvalidatesOverlappingSrfWindows()
    {
        Processor.CPU_Core core = CreateCore(out Processor.MainMemoryArea memory);
        MatrixTileMicroOp store = CreateMicroOp(InstructionsEnum.MTILE_STORE);
        core.SeedMatrixTileForRuntime(0, 2, store.TileDescriptor, [5, 6, 7, 8]);

        Assert.True(store.Execute(ref core));
        MatrixTileExecutionCaptureRecord capture = AssertCapture(store);
        Assert.True(capture.StreamTransfer.Completed);
        Assert.Equal(MatrixTileStreamTransferDirection.TileEgress, capture.StreamTransfer.Direction);
        Assert.Equal(new byte[4], Read(memory, 0x100, 4));
        ulong invalidationsBefore = core.MatrixTileStreamInvalidationCount;

        MatrixTileRetireOutcome outcome = store.RetireCapturedResult(ref core, capture);
        Assert.True(outcome.IsSuccess);
        Assert.Equal(new byte[] { 5, 6, 7, 8 }, Read(memory, 0x100, 4));
        Assert.True(core.MatrixTileStreamInvalidationCount > invalidationsBefore);
    }

    [Fact]
    public void WrongResourceTransferFailsClosedAndReplayBindsContourIdentity()
    {
        Processor.CPU_Core core = CreateCore(out Processor.MainMemoryArea memory);
        Assert.True(memory.TryWritePhysicalRange(0x100, new byte[] { 1, 2, 3, 4 }));
        MatrixTileMicroOp load = CreateMicroOp(InstructionsEnum.MTILE_LOAD);
        Assert.True(load.Execute(ref core));
        MatrixTileExecutionCaptureRecord capture = AssertCapture(load);
        MatrixTileExecutionCaptureRecord tampered = capture with
        {
            StreamTransfer = capture.StreamTransfer with
            {
                ResourceClass = MatrixTileRuntimeResourceClass.MatrixTileCompute
            }
        };
        Assert.Throws<MatrixTileRetireValidationException>(
            () => load.RetireCapturedResult(ref core, tampered));

        MatrixTileRetireOutcome outcome = load.RetireCapturedResult(ref core, capture);
        Assert.True(outcome.IsSuccess);
        MatrixTileReplayRollbackJournal journal = Assert.IsType<MatrixTileReplayRollbackJournal>(
            load.LastReplayRollbackJournal);
        Assert.Equal(
            MatrixTileRuntimeResourceClass.MatrixTileMemory,
            journal.ReplayIdentity.ResourceClass);
        Assert.Equal(SlotClass.MatrixTileStreamClass, journal.ReplayIdentity.SlotClass);
        Assert.Equal(
            capture.StreamTransfer.TransferFingerprint,
            journal.ReplayIdentity.StreamTransferFingerprint);
        Assert.NotEqual(0UL, journal.ReplayIdentity.ResourceContourFingerprint);
    }

    [Fact]
    public void FinalPackageOpensOnlyAfterPhase14ResourceAndGoldenGates()
    {
        Assert.Equal("Phase14", MatrixTileRuntimeIsaPackageContract.Phase);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase14WasFailClosedDuringCorrection);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase14SupersedesAllAluSchedulerClaim);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase14RegeneratedPlacementSensitiveGoldenEvidence);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesGoldenArtifacts);
        Assert.True(MatrixTileRuntimeIsaPackageContract.IsReadyForPositiveCompilerEmissionHandoff);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasCurrentCompilerImplementation);
        Assert.False(MatrixTileCompilerEmissionHandoffPackage.ModifiesCompilerCode);
    }

    private static MatrixTileMicroOp CreateMicroOp(InstructionsEnum opcode)
    {
        ulong secondaryPointer = opcode == InstructionsEnum.MTILE_MACC
            ? (3UL << 16) | 2UL
            : 2UL;
        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            Immediate = 2,
            HasImmediate = true,
            DataType = (byte)DataTypeEnum.INT8,
            HasDataType = true,
            VectorPrimaryPointer = opcode is InstructionsEnum.MTILE_LOAD or InstructionsEnum.MTILE_STORE
                ? 0x100UL
                : 1UL,
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

    private static Processor.CPU_Core CreateCore(out Processor.MainMemoryArea memory)
    {
        memory = new Processor.MainMemoryArea();
        memory.SetLength(0x400);
        CpuCorePlatformContext context =
            CpuCorePlatformContext.CreateFixed(memory, ProcessorMode.Emulation);
        return new Processor.CPU_Core(0, context);
    }

    private static MatrixTileExecutionCaptureRecord AssertCapture(MatrixTileMicroOp microOp)
    {
        Assert.True(microOp.LastExecutionCapture.HasValue);
        return microOp.LastExecutionCapture.Value;
    }

    private static void AssertMaskContains(ResourceBitset actual, ResourceBitset expected)
    {
        Assert.Equal(expected, actual & expected);
    }

    private static byte[] Read(
        Processor.MainMemoryArea memory,
        ulong address,
        int length)
    {
        byte[] data = new byte[length];
        Assert.True(memory.TryReadPhysicalRange(address, data));
        return data;
    }

    private sealed class DmaLaneClaimMicroOp : MicroOp
    {
        public DmaLaneClaimMicroOp()
        {
            SetHardPinnedPlacement(SlotClass.DmaStreamClass, 6);
        }

        public override bool Execute(ref Processor.CPU_Core core) => true;

        public override string GetDescription() => "TestOnlyDmaLane6Claim";
    }
}
