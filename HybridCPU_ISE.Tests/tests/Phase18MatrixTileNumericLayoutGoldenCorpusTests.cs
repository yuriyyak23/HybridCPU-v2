using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class MatrixTileNumericLayoutGoldenCorpusTests
{
    private const string CorpusRelativePath =
        "Documentation/Stream WhiteBook/03_MatrixTile/Golden/matrix_tile_numeric_layout_golden_v1.json";

    [Fact]
    public void CorpusSchema_IsVersionedRuntimeOwnedAndCompilerIndependent()
    {
        MatrixTileGoldenCorpus corpus = LoadCorpus();

        Assert.Equal(1, corpus.SchemaVersion);
        Assert.Equal(MatrixTileNumericPolicyAbi.CurrentAbiVersion, corpus.NumericPolicyAbiVersion);
        Assert.Equal(MatrixTileLayoutPolicyAbi.CurrentAbiVersion, corpus.LayoutPolicyAbiVersion);
        Assert.Equal(
            "ClosedMachineReadableMatrixTileNumericAndLayoutGoldenCorpus",
            corpus.CorpusDecision);
        Assert.False(corpus.UsesCompilerOutput);
        Assert.False(corpus.UsesPrivateArithmeticOracle);
        Assert.Contains(corpus.Vectors, static vector => vector.Id == "macc_s8_i32_identity_2x2");
        Assert.Contains(corpus.Vectors, static vector => vector.Id == "macc_s16_i32_identity_2x2");
        Assert.Contains(corpus.Vectors, static vector => vector.Id == "macc_s32_i64_identity_2x2");
        Assert.Contains(corpus.Vectors, static vector => vector.Id == "macc_u8_u32_boundary_2x2");
        Assert.Contains(corpus.Vectors, static vector => vector.Id == "macc_binary32_separate_rounding_1x1");
        Assert.Contains(corpus.Vectors, static vector => vector.Id == "macc_binary64_basic_1x1");
        Assert.Contains(corpus.Vectors, static vector => vector.Id == "transpose_i8_basic_2x2");
        Assert.Contains(corpus.Vectors, static vector => vector.Id == "macc_u64_overflow_fault_1x1");
        Assert.Contains(corpus.Vectors, static vector => vector.Id == "transpose_i8_nonsquare_in_place_reject");
        Assert.Contains(corpus.Vectors, static vector => vector.Id == "macc_i8_with_unsigned_policy_reject");
    }

    [Fact]
    public void PositiveVectors_LoadFromJsonAndPassProductionExecuteRetireReplayPath()
    {
        foreach (MatrixTileGoldenVector vector in LoadCorpus().Vectors.Where(IsPositive))
        {
            Processor.CPU_Core core = CreateCore(out _);
            MatrixTileMicroOp microOp = CreateMicroOp(vector);
            AssertDescriptors(vector, microOp);
            SeedVector(ref core, microOp, vector);

            Assert.True(microOp.Execute(ref core));
            MatrixTileExecutionCaptureRecord capture = AssertCapture(microOp, vector);
            Assert.Equal(ParseHex(vector.ExpectedStagedResultHex), capture.ResultImage.Data);
            Assert.Equal(
                vector.ExpectedReplayEpoch,
                capture.PolicyIdentity.ReplayEpoch);
            Assert.True(
                capture.PolicyIdentity.IdentityFingerprint ==
                    vector.ExpectedPolicyIdentityFingerprint,
                $"{vector.Id}: expected policy identity fingerprint {vector.ExpectedPolicyIdentityFingerprint}, actual {capture.PolicyIdentity.IdentityFingerprint}.");

            MatrixTileRetireOutcome retire =
                microOp.RetireCapturedResult(ref core, capture);
            Assert.True(retire.IsSuccess, vector.Id);
            Assert.Equal(ParsePublicationKind(vector.ExpectedRetirePublication), retire.PublicationKind);
            AssertPublishedState(ref core, microOp, vector, vector.ExpectedStagedResultHex);

            MatrixTileReplayRollbackJournal journal = AssertJournal(microOp);
            Assert.Equal(
                vector.ExpectedPolicyIdentityFingerprint,
                journal.ReplayIdentity.PolicyIdentityFingerprint);
            Assert.Equal(
                vector.ExpectedReplayEpoch,
                journal.ReplayIdentity.ReplayEpoch);

            microOp.RollbackRetiredResult(ref core, journal.ReplayIdentity);
            AssertPublishedState(ref core, microOp, vector, vector.ExpectedRollbackHex);

            MatrixTileRetireOutcome replay =
                microOp.ReplayRolledBackResult(ref core, journal.ReplayIdentity);
            Assert.Equal(retire, replay);
            AssertPublishedState(ref core, microOp, vector, vector.ExpectedStagedResultHex);
        }
    }

    [Fact]
    public void ExecuteFaultVectors_RetireAndReplayTypedFaultWithoutPublication()
    {
        foreach (MatrixTileGoldenVector vector in LoadCorpus().Vectors.Where(IsExecuteFault))
        {
            Processor.CPU_Core core = CreateCore(out _);
            MatrixTileMicroOp microOp = CreateMicroOp(vector);
            SeedVector(ref core, microOp, vector);
            byte[] before = ParseHex(vector.AccumulatorBeforeHex);

            Assert.True(microOp.Execute(ref core));
            MatrixTileExecutionCaptureRecord capture = AssertCapture(microOp, vector);
            Assert.True(capture.HasFault);
            Assert.Equal(ParseExecutionFault(vector.ExpectedExecutionFault), capture.FaultKind);
            Assert.True(
                capture.PolicyIdentity.IdentityFingerprint ==
                    vector.ExpectedPolicyIdentityFingerprint,
                $"{vector.Id}: expected policy identity fingerprint {vector.ExpectedPolicyIdentityFingerprint}, actual {capture.PolicyIdentity.IdentityFingerprint}.");

            MatrixTileRetireOutcome retire =
                microOp.RetireCapturedResult(ref core, capture);
            Assert.True(retire.FaultRetired);
            Assert.Equal(ParseRetireFault(vector.ExpectedRetireFault), retire.RetireFaultKind);
            AssertTile(ref core, vector.DestinationTileId, microOp.ResultTileDescriptor, before);

            MatrixTileReplayRollbackJournal journal = AssertJournal(microOp);
            MatrixTileRollbackOutcome rollback =
                microOp.RollbackRetiredResult(ref core, journal.ReplayIdentity);
            MatrixTileRetireOutcome replay =
                microOp.ReplayRolledBackResult(ref core, journal.ReplayIdentity);
            Assert.True(rollback.FaultOnlyRollback);
            Assert.Equal(retire, replay);
            AssertTile(ref core, vector.DestinationTileId, microOp.ResultTileDescriptor, before);
        }
    }

    [Fact]
    public void ProjectionFaultVectors_FailClosedBeforeTypedExecution()
    {
        foreach (MatrixTileGoldenVector vector in LoadCorpus().Vectors.Where(IsProjectionFault))
        {
            MatrixTileInstructionIrProjection projection = Project(vector);

            Assert.False(projection.IsRuntimeLegal);
            Assert.Equal(
                ParseProjectionFault(vector.ExpectedProjectionFault),
                projection.FaultKind);
            if (!string.IsNullOrEmpty(vector.ExpectedSemanticFault))
            {
                Assert.Equal(
                    ParseSemanticFault(vector.ExpectedSemanticFault),
                    projection.SemanticValidation!.Value.FaultKind);
            }

            if (!string.IsNullOrEmpty(vector.ExpectedNumericPolicyFault))
            {
                Assert.Equal(
                    ParseNumericPolicyFault(vector.ExpectedNumericPolicyFault),
                    projection.NumericPolicyValidation!.Value.FaultKind);
            }
        }
    }

    [Fact]
    public void DeclaredNegativeOutcomes_FailClosedOnProductionReplayAndRetireBoundaries()
    {
        MatrixTileGoldenVector[] vectors =
            LoadCorpus().Vectors.Where(IsPositive).ToArray();
        foreach (MatrixTileGoldenVector vector in vectors)
        {
            foreach (string outcome in vector.NegativeOutcomes ?? Array.Empty<string>())
            {
                switch (outcome)
                {
                    case "wrongOwner":
                        AssertWrongOwnerRejected(vector);
                        break;
                    case "wrongPolicy":
                        AssertWrongPolicyRejected(vector);
                        break;
                    case "wrongDtype":
                        AssertWrongDtypeReplayRejected(vector);
                        break;
                    case "wrongResource":
                        AssertWrongResourceReplayRejected(vector);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"{vector.Id} declares unknown negative outcome {outcome}.");
                }
            }
        }
    }

    private static void AssertWrongOwnerRejected(MatrixTileGoldenVector vector)
    {
        Processor.CPU_Core core = CreateCore(out _);
        MatrixTileMicroOp microOp = CreateMicroOp(vector);
        SeedVector(ref core, microOp, vector);
        Assert.True(microOp.Execute(ref core));
        MatrixTileExecutionCaptureRecord capture = AssertCapture(microOp, vector);
        MatrixTileExecutionCaptureRecord wrongOwner = capture with
        {
            CaptureIdentity = capture.CaptureIdentity with { OwnerThreadId = 1 }
        };

        Assert.Throws<MatrixTileRetireValidationException>(
            () => microOp.RetireCapturedResult(ref core, wrongOwner));
        AssertPublishedState(ref core, microOp, vector, vector.ExpectedRollbackHex);
    }

    private static void AssertWrongPolicyRejected(MatrixTileGoldenVector vector)
    {
        Processor.CPU_Core core = CreateCore(out _);
        MatrixTileMicroOp microOp = CreateMicroOp(vector);
        SeedVector(ref core, microOp, vector);
        Assert.True(microOp.Execute(ref core));
        MatrixTileExecutionCaptureRecord capture = AssertCapture(microOp, vector);
        MatrixTileExecutionCaptureRecord tampered = capture;
        if (capture.NumericPolicy.HasValue)
        {
            tampered = tampered with
            {
                NumericPolicy = capture.NumericPolicy.Value with
                {
                    Fingerprint = capture.NumericPolicy.Value.Fingerprint ^ 0x100UL
                }
            };
        }
        else
        {
            tampered = tampered with
            {
                LayoutPolicy = capture.LayoutPolicy!.Value with
                {
                    Fingerprint = capture.LayoutPolicy.Value.Fingerprint ^ 0x200UL
                }
            };
        }

        Assert.Throws<MatrixTileRetireValidationException>(
            () => microOp.RetireCapturedResult(ref core, tampered));
        AssertPublishedState(ref core, microOp, vector, vector.ExpectedRollbackHex);
    }

    private static void AssertWrongDtypeReplayRejected(MatrixTileGoldenVector vector)
    {
        Processor.CPU_Core core = CreateCore(out _);
        MatrixTileMicroOp microOp = CreateMicroOp(vector);
        SeedVector(ref core, microOp, vector);
        MatrixTileReplayRollbackJournal journal =
            ExecuteRetireAndRollback(ref core, microOp);
        MatrixTileMicroOp alternate = CreateMicroOp(vector with
        {
            DataType = vector.DataType == "INT8" ? "UINT8" : "INT8",
            NumericProfile = vector.DataType == "INT8"
                ? "UnsignedInt8ToUInt32"
                : "SignedInt8ToInt32",
            SourceHex = "01020304",
            SecondaryHex = "01000001",
            AccumulatorBeforeHex = "00000000000000000000000000000000",
            ExpectedStagedResultHex = "01000000020000000300000004000000"
        });

        Assert.Throws<MatrixTileReplayRollbackValidationException>(
            () => MatrixTileReplayRollbackAbi.Replay(
                ref core,
                alternate.MaterializedInstruction,
                journal,
                journal.ReplayIdentity));
        Assert.Equal(MatrixTileReplayRollbackLifecycle.RolledBack, journal.Lifecycle);
    }

    private static void AssertWrongResourceReplayRejected(MatrixTileGoldenVector vector)
    {
        Processor.CPU_Core core = CreateCore(out _);
        MatrixTileMicroOp microOp = CreateMicroOp(vector);
        SeedVector(ref core, microOp, vector);
        MatrixTileReplayRollbackJournal journal =
            ExecuteRetireAndRollback(ref core, microOp);
        MatrixTileGoldenVector alternateVector =
            vector.Operation == "MTRANSPOSE"
                ? vector with
                {
                    Operation = "MTILE_MACC",
                    NumericProfile = "SignedInt8ToInt32",
                    LayoutProfile = "MaccCanonicalRowMajorAscendingK",
                    PrimaryPointer = 1,
                    SecondaryPointer = 196610,
                    Immediate = 2,
                    StreamLength = 4,
                    Stride = 1,
                    RowStride = 2,
                    SourceHex = "01020304",
                    SecondaryHex = "01000001",
                    AccumulatorBeforeHex = "00000000000000000000000000000000",
                    ExpectedStagedResultHex = "01000000020000000300000004000000"
                }
                : vector with
                {
                    Operation = "MTRANSPOSE",
                    NumericProfile = null,
                    LayoutProfile = "TransposeCanonicalRowMajor",
                    PrimaryPointer = 1,
                    SecondaryPointer = 2,
                    Immediate = 2,
                    StreamLength = 4,
                    Stride = 1,
                    RowStride = 2,
                    SourceHex = "01020304",
                    SecondaryHex = null,
                    AccumulatorBeforeHex = "00000000",
                    ExpectedStagedResultHex = "01030204"
                };
        MatrixTileMicroOp alternate = CreateMicroOp(alternateVector);

        Assert.Throws<MatrixTileReplayRollbackValidationException>(
            () => MatrixTileReplayRollbackAbi.Replay(
                ref core,
                alternate.MaterializedInstruction,
                journal,
                journal.ReplayIdentity));
        Assert.Equal(MatrixTileReplayRollbackLifecycle.RolledBack, journal.Lifecycle);
    }

    private static MatrixTileReplayRollbackJournal ExecuteRetireAndRollback(
        ref Processor.CPU_Core core,
        MatrixTileMicroOp microOp)
    {
        Assert.True(microOp.Execute(ref core));
        MatrixTileExecutionCaptureRecord capture =
            Assert.IsType<MatrixTileExecutionCaptureRecord>(
                microOp.LastExecutionCapture!.Value);
        MatrixTileRetireOutcome retire =
            microOp.RetireCapturedResult(ref core, capture);
        Assert.True(retire.IsSuccess);
        MatrixTileReplayRollbackJournal journal = AssertJournal(microOp);
        microOp.RollbackRetiredResult(ref core, journal.ReplayIdentity);
        return journal;
    }

    private static MatrixTileMicroOp CreateMicroOp(MatrixTileGoldenVector vector)
    {
        var context = new DecoderContext
        {
            OpCode = (uint)ParseOpcode(vector.Operation),
            Immediate = checked((ushort)vector.Immediate),
            HasImmediate = true,
            DataType = (byte)ParseDataType(vector.DataType),
            HasDataType = true,
            VectorPrimaryPointer = vector.PrimaryPointer,
            VectorSecondaryPointer = vector.SecondaryPointer,
            VectorStreamLength = vector.StreamLength,
            VectorStride = checked((ushort)vector.Stride),
            VectorRowStride = checked((ushort)vector.RowStride),
            MatrixTileNumericPolicy = CreateNumericPolicy(vector.NumericProfile),
            MatrixTileLayoutPolicy = CreateLayoutPolicy(vector.LayoutProfile),
            HasVectorPayload = true,
            HasVectorAddressingContour = true,
            Is2DAddressing = true,
            IndexedAddressing = false,
            PredicateMask = 0
        };

        return Assert.IsAssignableFrom<MatrixTileMicroOp>(
            InstructionRegistry.CreateMicroOp(
                (uint)ParseOpcode(vector.Operation),
                context));
    }

    private static MatrixTileInstructionIrProjection Project(MatrixTileGoldenVector vector)
    {
        VectorInstructionPayload payload = new(
            PrimaryPointer: vector.PrimaryPointer,
            SecondaryPointer: vector.SecondaryPointer,
            StreamLength: vector.StreamLength,
            Stride: checked((ushort)vector.Stride),
            RowStride: checked((ushort)vector.RowStride),
            Indexed: false,
            Is2D: true,
            TailAgnostic: false,
            MaskAgnostic: false,
            Saturating: false,
            PredicateMask: 0,
            DataType: (byte)ParseDataType(vector.DataType))
        {
            MatrixTileNumericPolicy = CreateNumericPolicy(vector.NumericProfile),
            MatrixTileLayoutPolicy = CreateLayoutPolicy(vector.LayoutProfile)
        };

        return MatrixTileIrProjectionAndMaterializer.ProjectDecodedVectorPayload(
            ParseOpcode(vector.Operation),
            payload,
            vector.Immediate,
            requireExplicitNumericPolicy: true);
    }

    private static void SeedVector(
        ref Processor.CPU_Core core,
        MatrixTileMicroOp microOp,
        MatrixTileGoldenVector vector)
    {
        if (!string.IsNullOrWhiteSpace(vector.SourceHex))
        {
            core.SeedMatrixTileForRuntime(
                0,
                vector.SourceTileId,
                microOp.TileDescriptor,
                ParseHex(vector.SourceHex));
        }

        if (!string.IsNullOrWhiteSpace(vector.SecondaryHex))
        {
            core.SeedMatrixTileForRuntime(
                0,
                vector.SecondaryTileId,
                microOp.SecondaryTileDescriptor,
                ParseHex(vector.SecondaryHex));
        }

        if (!string.IsNullOrWhiteSpace(vector.AccumulatorBeforeHex))
        {
            core.SeedMatrixTileForRuntime(
                0,
                vector.DestinationTileId,
                microOp.ResultTileDescriptor,
                ParseHex(vector.AccumulatorBeforeHex));
        }
    }

    private static MatrixTileExecutionCaptureRecord AssertCapture(
        MatrixTileMicroOp microOp,
        MatrixTileGoldenVector vector)
    {
        MatrixTileExecutionCaptureRecord capture =
            Assert.IsType<MatrixTileExecutionCaptureRecord>(
                microOp.LastExecutionCapture!.Value);
        Assert.True(capture.HasRetireCorrelation, vector.Id);
        Assert.True(capture.PolicyIdentity.IsValid, vector.Id);
        Assert.Equal(ParseOperationKind(vector.Operation), capture.OperationKind);
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

    private static void AssertDescriptors(
        MatrixTileGoldenVector vector,
        MatrixTileMicroOp microOp)
    {
        DataTypeEnum dataType = ParseDataType(vector.DataType);
        ushort elementSize = checked((ushort)DataTypeUtils.SizeOf(dataType));
        ushort rows = checked((ushort)(vector.StreamLength / (uint)vector.Immediate));
        ushort columns = checked((ushort)vector.Immediate);
        MatrixTileCanonicalDescriptorAbi source =
            MatrixTileCanonicalDescriptorAbi.Create(
                rows,
                columns,
                elementSize,
                checked((uint)vector.RowStride));
        Assert.Equal(source, microOp.TileDescriptor);

        if (vector.Operation == "MTRANSPOSE")
        {
            MatrixTileCanonicalDescriptorAbi destination =
                MatrixTileCanonicalDescriptorAbi.Create(
                    columns,
                    rows,
                    elementSize,
                    checked((uint)(rows * elementSize)));
            Assert.Equal(destination, microOp.ResultTileDescriptor);
            return;
        }

        MatrixTileNumericPolicy numeric =
            MatrixTileNumericPolicyAbi.CreateSupportedPolicy(
                ParseNumericProfile(vector.NumericProfile!));
        ushort accumulatorElementSize = checked((ushort)
            DataTypeUtils.SizeOf(numeric.AccumulatorType));
        MatrixTileCanonicalDescriptorAbi accumulator =
            MatrixTileCanonicalDescriptorAbi.Create(
                rows,
                columns,
                accumulatorElementSize,
                checked((uint)(columns * accumulatorElementSize)));
        Assert.Equal(source, microOp.SecondaryTileDescriptor);
        Assert.Equal(accumulator, microOp.ResultTileDescriptor);
    }

    private static void AssertPublishedState(
        ref Processor.CPU_Core core,
        MatrixTileMicroOp microOp,
        MatrixTileGoldenVector vector,
        string expectedHex)
    {
        AssertTile(
            ref core,
            vector.DestinationTileId,
            microOp.ResultTileDescriptor,
            ParseHex(expectedHex));
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
        out Processor.MainMemoryArea memory)
    {
        memory = new Processor.MainMemoryArea();
        memory.SetLength(0x1000);
        CpuCorePlatformContext context =
            CpuCorePlatformContext.CreateFixed(memory, ProcessorMode.Emulation);
        return new Processor.CPU_Core(0, context);
    }

    private static MatrixTileGoldenCorpus LoadCorpus()
    {
        string path = Path.Combine(
            CompatFreezeScanner.FindRepoRoot(),
            CorpusRelativePath.Replace('/', Path.DirectorySeparatorChar));
        string json = File.ReadAllText(path);
        MatrixTileGoldenCorpus? corpus =
            JsonSerializer.Deserialize<MatrixTileGoldenCorpus>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        return corpus ?? throw new InvalidOperationException(
            $"Unable to load MatrixTile golden corpus {path}.");
    }

    private static bool IsPositive(MatrixTileGoldenVector vector) =>
        vector.Kind == "positive";

    private static bool IsExecuteFault(MatrixTileGoldenVector vector) =>
        vector.Kind == "executeFault";

    private static bool IsProjectionFault(MatrixTileGoldenVector vector) =>
        vector.Kind == "projectionFault";

    private static InstructionsEnum ParseOpcode(string operation) =>
        Enum.Parse<InstructionsEnum>(operation);

    private static MatrixTileProjectedOperationKind ParseOperationKind(
        string operation) =>
        operation switch
        {
            "MTILE_LOAD" => MatrixTileProjectedOperationKind.Load,
            "MTILE_STORE" => MatrixTileProjectedOperationKind.Store,
            "MTILE_MACC" => MatrixTileProjectedOperationKind.Macc,
            "MTRANSPOSE" => MatrixTileProjectedOperationKind.Transpose,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };

    private static DataTypeEnum ParseDataType(string dataType) =>
        Enum.Parse<DataTypeEnum>(dataType);

    private static MatrixTileNumericProfileId ParseNumericProfile(string profile) =>
        Enum.Parse<MatrixTileNumericProfileId>(profile);

    private static MatrixTileNumericPolicy? CreateNumericPolicy(string? profile) =>
        string.IsNullOrWhiteSpace(profile)
            ? null
            : MatrixTileNumericPolicyAbi.CreateSupportedPolicy(
                ParseNumericProfile(profile));

    private static MatrixTileLayoutPolicy? CreateLayoutPolicy(string? profile) =>
        profile switch
        {
            null or "" => null,
            "MaccCanonicalRowMajorAscendingK" =>
                MatrixTileLayoutPolicyAbi.CreateMaccPolicy(),
            "TransposeCanonicalRowMajor" =>
                MatrixTileLayoutPolicyAbi.CreateTransposePolicy(),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null)
        };

    private static MatrixTileRetirePublicationKind ParsePublicationKind(
        string publicationKind) =>
        Enum.Parse<MatrixTileRetirePublicationKind>(publicationKind);

    private static MatrixTileExecutionFaultKind ParseExecutionFault(
        string faultKind) =>
        Enum.Parse<MatrixTileExecutionFaultKind>(faultKind);

    private static MatrixTileRetireFaultKind ParseRetireFault(
        string faultKind) =>
        Enum.Parse<MatrixTileRetireFaultKind>(faultKind);

    private static MatrixTileIrProjectionFaultKind ParseProjectionFault(
        string faultKind) =>
        Enum.Parse<MatrixTileIrProjectionFaultKind>(faultKind);

    private static MatrixTileSemanticFaultKind ParseSemanticFault(
        string faultKind) =>
        Enum.Parse<MatrixTileSemanticFaultKind>(faultKind);

    private static MatrixTileNumericPolicyFaultKind ParseNumericPolicyFault(
        string faultKind) =>
        Enum.Parse<MatrixTileNumericPolicyFaultKind>(faultKind);

    private static byte[] ParseHex(string? hex) =>
        Convert.FromHexString(hex ?? string.Empty);

    private sealed record MatrixTileGoldenCorpus(
        int SchemaVersion,
        ushort NumericPolicyAbiVersion,
        ushort LayoutPolicyAbiVersion,
        string CorpusDecision,
        bool UsesCompilerOutput,
        bool UsesPrivateArithmeticOracle,
        MatrixTileGoldenVector[] Vectors);

    private sealed record MatrixTileGoldenVector(
        string Id,
        string Kind,
        string Operation,
        string DataType,
        string? NumericProfile,
        string? LayoutProfile,
        ulong PrimaryPointer,
        ulong SecondaryPointer,
        int Immediate,
        uint StreamLength,
        int Stride,
        int RowStride,
        ushort SourceTileId,
        ushort SecondaryTileId,
        ushort DestinationTileId,
        string? SourceHex,
        string? SecondaryHex,
        string? AccumulatorBeforeHex,
        string? ExpectedStagedResultHex,
        string? ExpectedRetirePublication,
        string? ExpectedRollbackHex,
        ulong ExpectedReplayEpoch,
        ulong ExpectedPolicyIdentityFingerprint,
        string? ExpectedExecutionFault,
        string? ExpectedRetireFault,
        string? ExpectedProjectionFault,
        string? ExpectedSemanticFault,
        string? ExpectedNumericPolicyFault,
        string[]? NegativeOutcomes);
}
