using System;
using System.Buffers.Binary;
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

public sealed class MatrixTilePolicyBoundIdentityTests
{
    [Fact]
    public void Execute_BindsPolicyIdentityIntoCaptureAndReplayIdentity()
    {
        Processor.CPU_Core core = CreateCore(out _);
        MatrixTileMicroOp microOp = CreateMaccMicroOp(
            DataTypeEnum.INT8,
            MatrixTileNumericProfileId.SignedInt8ToInt32);
        SeedInt8Macc(ref core, microOp);

        Assert.True(microOp.Execute(ref core));
        MatrixTileExecutionCaptureRecord capture = AssertCapture(microOp);

        Assert.True(capture.PolicyIdentity.IsValid);
        Assert.True(capture.NumericPolicy.HasValue);
        Assert.True(capture.LayoutPolicy.HasValue);
        Assert.True(MatrixTilePolicyBoundIdentityAbi.ValidateCapture(
            capture,
            core.GetMatrixTileReplayInvalidationEpoch()));
        Assert.Equal(0, capture.PolicyIdentity.OwnerThreadId);
        Assert.Equal(0, capture.PolicyIdentity.MemoryDomainId);
        Assert.Equal((uint)InstructionsEnum.MTILE_MACC, capture.PolicyIdentity.Opcode);
        Assert.Equal(
            capture.NumericPolicy.Value.Fingerprint,
            capture.PolicyIdentity.NumericPolicyFingerprint);
        Assert.Equal(
            capture.LayoutPolicy.Value.Fingerprint,
            capture.PolicyIdentity.LayoutPolicyFingerprint);
        Assert.Equal(
            MatrixTileRetirePublicationKind.Accumulator,
            capture.PolicyIdentity.PublicationSurface);

        MatrixTileRetireOutcome outcome =
            microOp.RetireCapturedResult(ref core, capture);
        MatrixTileReplayRollbackJournal journal = AssertJournal(microOp);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(
            capture.PolicyIdentity.NumericPolicyFingerprint,
            journal.ReplayIdentity.NumericPolicyFingerprint);
        Assert.Equal(
            capture.PolicyIdentity.LayoutPolicyFingerprint,
            journal.ReplayIdentity.LayoutPolicyFingerprint);
        Assert.Equal(
            capture.PolicyIdentity.IdentityFingerprint,
            journal.ReplayIdentity.PolicyIdentityFingerprint);
        Assert.Equal(
            capture.PolicyIdentity.ReplayEpoch,
            journal.ReplayIdentity.ReplayEpoch);
        Assert.Equal(
            capture.PolicyIdentity.DependencyFingerprint,
            journal.ReplayIdentity.DependencyFingerprint);
        Assert.Equal(
            capture.PolicyIdentity.PublicationSurface,
            journal.ReplayIdentity.PublicationSurface);
    }

    [Fact]
    public void RetireRejectsTamperedNumericPolicyWithoutPublication()
    {
        Processor.CPU_Core core = CreateCore(out _);
        MatrixTileMicroOp microOp = CreateMaccMicroOp(
            DataTypeEnum.INT8,
            MatrixTileNumericProfileId.SignedInt8ToInt32);
        byte[] initialAccumulator = SeedInt8Macc(ref core, microOp);
        Assert.True(microOp.Execute(ref core));
        MatrixTileExecutionCaptureRecord capture = AssertCapture(microOp);
        MatrixTileNumericPolicy tamperedPolicy = capture.NumericPolicy!.Value with
        {
            Fingerprint = capture.NumericPolicy.Value.Fingerprint ^ 0x10UL
        };
        MatrixTileExecutionCaptureRecord tampered = capture with
        {
            NumericPolicy = tamperedPolicy
        };

        Assert.Throws<MatrixTileRetireValidationException>(
            () => microOp.RetireCapturedResult(ref core, tampered));
        AssertTile(ref core, 3, microOp.ResultTileDescriptor, initialAccumulator);
    }

    [Fact]
    public void RetireRejectsTamperedLayoutPolicyWithoutPublication()
    {
        Processor.CPU_Core core = CreateCore(out _);
        MatrixTileMicroOp microOp = CreateMaccMicroOp(
            DataTypeEnum.INT8,
            MatrixTileNumericProfileId.SignedInt8ToInt32);
        byte[] initialAccumulator = SeedInt8Macc(ref core, microOp);
        Assert.True(microOp.Execute(ref core));
        MatrixTileExecutionCaptureRecord capture = AssertCapture(microOp);
        MatrixTileLayoutPolicy tamperedPolicy = capture.LayoutPolicy!.Value with
        {
            Fingerprint = capture.LayoutPolicy.Value.Fingerprint ^ 0x20UL
        };
        MatrixTileExecutionCaptureRecord tampered = capture with
        {
            LayoutPolicy = tamperedPolicy
        };

        Assert.Throws<MatrixTileRetireValidationException>(
            () => microOp.RetireCapturedResult(ref core, tampered));
        AssertTile(ref core, 3, microOp.ResultTileDescriptor, initialAccumulator);
    }

    [Fact]
    public void RetireRejectsStaleReplayEpochWithoutPublication()
    {
        Processor.CPU_Core core = CreateCore(out _);
        MatrixTileMicroOp microOp = CreateMaccMicroOp(
            DataTypeEnum.INT8,
            MatrixTileNumericProfileId.SignedInt8ToInt32);
        byte[] initialAccumulator = SeedInt8Macc(ref core, microOp);
        Assert.True(microOp.Execute(ref core));
        MatrixTileExecutionCaptureRecord capture = AssertCapture(microOp);
        ulong capturedEpoch = capture.PolicyIdentity.ReplayEpoch;

        ulong advancedEpoch = core.AdvanceMatrixTileReplayInvalidationEpochForTesting();

        Assert.NotEqual(capturedEpoch, advancedEpoch);
        Assert.Throws<MatrixTileRetireValidationException>(
            () => microOp.RetireCapturedResult(ref core, capture));
        AssertTile(ref core, 3, microOp.ResultTileDescriptor, initialAccumulator);
    }

    [Fact]
    public void ReplayRejectsMaterializedNumericPolicyMismatch()
    {
        Processor.CPU_Core core = CreateCore(out _);
        MatrixTileMicroOp signedMicroOp = CreateMaccMicroOp(
            DataTypeEnum.INT8,
            MatrixTileNumericProfileId.SignedInt8ToInt32);
        byte[] initialAccumulator = SeedInt8Macc(ref core, signedMicroOp);
        ExecuteAndRetire(ref core, signedMicroOp);
        MatrixTileReplayRollbackJournal journal = AssertJournal(signedMicroOp);
        signedMicroOp.RollbackRetiredResult(ref core, journal.ReplayIdentity);

        MatrixTileMicroOp unsignedMicroOp = CreateMaccMicroOp(
            DataTypeEnum.UINT8,
            MatrixTileNumericProfileId.UnsignedInt8ToUInt32);

        Assert.Throws<MatrixTileReplayRollbackValidationException>(
            () => MatrixTileReplayRollbackAbi.Replay(
                ref core,
                unsignedMicroOp.MaterializedInstruction,
                journal,
                journal.ReplayIdentity));
        Assert.Equal(MatrixTileReplayRollbackLifecycle.RolledBack, journal.Lifecycle);
        AssertTile(ref core, 3, signedMicroOp.ResultTileDescriptor, initialAccumulator);
    }

    [Fact]
    public void Binary32Macc_RollbackReplayPreservesPolicyBoundIdentity()
    {
        Processor.CPU_Core core = CreateCore(out _);
        MatrixTileMicroOp microOp = CreateMaccMicroOp(
            DataTypeEnum.FLOAT32,
            MatrixTileNumericProfileId.Binary32ToBinary32);
        byte[] initialAccumulator = SeedBinary32Macc(ref core, microOp);

        MatrixTileExecutionCaptureRecord capture = ExecuteAndRetire(ref core, microOp);
        MatrixTileReplayRollbackJournal journal = AssertJournal(microOp);
        Assert.NotEqual(0UL, journal.ReplayIdentity.NumericPolicyFingerprint);
        Assert.NotEqual(0UL, journal.ReplayIdentity.LayoutPolicyFingerprint);
        Assert.NotEqual(0UL, journal.ReplayIdentity.PolicyIdentityFingerprint);

        microOp.RollbackRetiredResult(ref core, journal.ReplayIdentity);
        AssertTile(ref core, 3, microOp.ResultTileDescriptor, initialAccumulator);

        MatrixTileRetireOutcome replay =
            microOp.ReplayRolledBackResult(ref core, journal.ReplayIdentity);

        Assert.Equal(journal.OriginalRetireOutcome, replay);
        AssertTile(ref core, 3, microOp.ResultTileDescriptor, capture.ResultImage.Data);
    }

    [Fact]
    public void AbiPublishesClosedBoundaryConstants()
    {
        Assert.True(MatrixTilePolicyBoundIdentityAbi.UsesExistingRetirePublication);
        Assert.True(MatrixTilePolicyBoundIdentityAbi.UsesExistingReplayRollbackJournal);
        Assert.False(MatrixTilePolicyBoundIdentityAbi.UsesSrfOrHostEvidenceAsAuthority);
        Assert.True(MatrixTileReplayRollbackAbi.HasPolicyBoundReplayIdentity);
        Assert.Contains("Policy", MatrixTilePolicyBoundIdentityAbi.CaptureIdentityDecision);
        Assert.Contains("Policy", MatrixTileReplayRollbackAbi.PolicyBoundIdentityDecision);
    }

    private static MatrixTileExecutionCaptureRecord ExecuteAndRetire(
        ref Processor.CPU_Core core,
        MatrixTileMicroOp microOp)
    {
        Assert.True(microOp.Execute(ref core));
        MatrixTileExecutionCaptureRecord capture = AssertCapture(microOp);
        MatrixTileRetireOutcome outcome =
            microOp.RetireCapturedResult(ref core, capture);
        Assert.Equal(capture.HasFault, outcome.FaultRetired);
        return capture;
    }

    private static MatrixTileExecutionCaptureRecord AssertCapture(
        MatrixTileMicroOp microOp)
    {
        MatrixTileExecutionCaptureRecord? capture = microOp.LastExecutionCapture;
        Assert.True(capture.HasValue);
        Assert.True(capture.Value.HasRetireCorrelation);
        Assert.True(capture.Value.PolicyIdentity.IsValid);
        return capture.Value;
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

    private static Processor.CPU_Core CreateCore(
        out Processor.MainMemoryArea memory)
    {
        memory = new Processor.MainMemoryArea();
        memory.SetLength(0x400);
        CpuCorePlatformContext context =
            CpuCorePlatformContext.CreateFixed(memory, ProcessorMode.Emulation);
        return new Processor.CPU_Core(0, context);
    }

    private static MatrixTileMicroOp CreateMaccMicroOp(
        DataTypeEnum dataType,
        MatrixTileNumericProfileId profileId)
    {
        var context = new DecoderContext
        {
            OpCode = (uint)InstructionsEnum.MTILE_MACC,
            Immediate = 2,
            HasImmediate = true,
            DataType = (byte)dataType,
            HasDataType = true,
            VectorPrimaryPointer = 1,
            VectorSecondaryPointer = (3UL << 16) | 2UL,
            VectorStreamLength = 4,
            VectorStride = (ushort)MatrixTileNumericPolicyAbi.GetElementSizeBytes(dataType),
            VectorRowStride = checked((ushort)
                (2 * MatrixTileNumericPolicyAbi.GetElementSizeBytes(dataType))),
            MatrixTileNumericPolicy =
                MatrixTileNumericPolicyAbi.CreateSupportedPolicy(profileId),
            MatrixTileLayoutPolicy = MatrixTileLayoutPolicyAbi.CreateMaccPolicy(),
            HasVectorPayload = true,
            HasVectorAddressingContour = true,
            Is2DAddressing = true,
            IndexedAddressing = false,
            PredicateMask = 0
        };

        return Assert.IsAssignableFrom<MatrixTileMicroOp>(
            InstructionRegistry.CreateMicroOp(
                (uint)InstructionsEnum.MTILE_MACC,
                context));
    }

    private static byte[] SeedInt8Macc(
        ref Processor.CPU_Core core,
        MatrixTileMicroOp microOp)
    {
        core.SeedMatrixTileForRuntime(
            0,
            1,
            microOp.TileDescriptor,
            [1, 2, 3, 4]);
        core.SeedMatrixTileForRuntime(
            0,
            2,
            microOp.SecondaryTileDescriptor,
            [1, 0, 0, 1]);
        byte[] initialAccumulator = new byte[
            MatrixTileExecuteCaptureAbi.GetPackedByteLength(
                microOp.ResultTileDescriptor)];
        core.SeedMatrixTileForRuntime(
            0,
            3,
            microOp.ResultTileDescriptor,
            initialAccumulator);
        return initialAccumulator;
    }

    private static byte[] SeedBinary32Macc(
        ref Processor.CPU_Core core,
        MatrixTileMicroOp microOp)
    {
        core.SeedMatrixTileForRuntime(
            0,
            1,
            microOp.TileDescriptor,
            PackUInt32(0x3F800000, 0x00000000, 0x00000000, 0x3F800000));
        core.SeedMatrixTileForRuntime(
            0,
            2,
            microOp.SecondaryTileDescriptor,
            PackUInt32(0x40000000, 0x00000000, 0x00000000, 0x40400000));
        byte[] initialAccumulator = new byte[
            MatrixTileExecuteCaptureAbi.GetPackedByteLength(
                microOp.ResultTileDescriptor)];
        core.SeedMatrixTileForRuntime(
            0,
            3,
            microOp.ResultTileDescriptor,
            initialAccumulator);
        return initialAccumulator;
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

    private static byte[] PackUInt32(params uint[] values)
    {
        byte[] data = new byte[values.Length * sizeof(uint)];
        for (int index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(
                data.AsSpan(index * sizeof(uint), sizeof(uint)),
                values[index]);
        }

        return data;
    }
}
