using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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

namespace HybridCPU_ISE.Tests.Phase12;

public sealed class Phase12MatrixTilePositiveGoldenArtifactTests
{
    [Fact]
    public void ManifestPublishesCompleteRuntimeOwnedPhase12ArtifactSurface()
    {
        Assert.Equal(
            "ClosedMatrixTilePositiveExecutableGoldenArtifacts",
            MatrixTilePositiveGoldenArtifactManifest.ManifestDecision);
        Assert.Equal(
            "ClosedRuntimeNoFallbackNoHiddenLoweringRegressionEvidence",
            MatrixTilePositiveGoldenArtifactManifest.NoFallbackDecision);
        Assert.True(MatrixTilePositiveGoldenArtifactManifest.HasPositiveExecutableGoldenArtifacts);
        Assert.True(MatrixTilePositiveGoldenArtifactManifest.HasRuntimeNoFallbackNoHiddenLoweringRegressionEvidence);
        Assert.True(MatrixTilePositiveGoldenArtifactManifest.HasLegalDecodeEncodeRoundTripVectors);
        Assert.True(MatrixTilePositiveGoldenArtifactManifest.HasLegalIrMaterializerProjectionVectors);
        Assert.True(MatrixTilePositiveGoldenArtifactManifest.HasLegalExecuteRetireVectors);
        Assert.True(MatrixTilePositiveGoldenArtifactManifest.HasMemoryFaultVectors);
        Assert.True(MatrixTilePositiveGoldenArtifactManifest.HasDescriptorFaultVectors);
        Assert.True(MatrixTilePositiveGoldenArtifactManifest.HasAccumulatorVectors);
        Assert.True(MatrixTilePositiveGoldenArtifactManifest.HasTransposeVectors);
        Assert.True(MatrixTilePositiveGoldenArtifactManifest.HasReplayRollbackVectors);
        Assert.True(MatrixTilePositiveGoldenArtifactManifest.HasNegativeReservedCarrierVectors);
        Assert.False(MatrixTilePositiveGoldenArtifactManifest.UsesCompilerGeneratedInputs);
        Assert.False(MatrixTilePositiveGoldenArtifactManifest.UsesHostOwnedArchitecturalEvidence);
        Assert.False(MatrixTilePositiveGoldenArtifactManifest.UsesFallbackPath);

        Assert.Equal(4, MatrixTilePositiveGoldenArtifactManifest.ExecutionVectors.Length);
        Assert.Equal(2, MatrixTilePositiveGoldenArtifactManifest.MemoryFaultVectors.Length);
        Assert.Equal(4, MatrixTilePositiveGoldenArtifactManifest.DescriptorFaultVectors.Length);
        Assert.Equal(4, MatrixTilePositiveGoldenArtifactManifest.ReservedCarrierVectors.Length);
    }

    [Fact]
    public void LegalGoldenCarriersRoundTripThroughEncoderSerializationAndCanonicalDecoder()
    {
        var bytes = new byte[32];
        foreach (MatrixTileExecutionGoldenVector vector in
                 MatrixTilePositiveGoldenArtifactManifest.ExecutionVectors)
        {
            VLIW_Instruction encoded = InstructionEncoder.EncodeVector2D(
                (uint)vector.Opcode,
                DataTypeEnum.INT8,
                vector.Carrier.Word1,
                vector.Carrier.Word2,
                streamLength: 4,
                colStride: 1,
                rowStride: 2,
                rowLength: 2);

            AssertCarrier(vector.Carrier, encoded);

            bytes.AsSpan().Clear();
            Assert.True(encoded.TryWriteBytes(bytes));
            var restored = new VLIW_Instruction();
            Assert.True(restored.TryReadBytes(bytes));
            AssertCarrier(vector.Carrier, restored);

            InstructionIR ir = new VliwDecoderV4().Decode(in restored, slotIndex: 0);
            Assert.Equal(vector.Opcode, ir.CanonicalOpcode.ToInstructionsEnum());
            Assert.True(ir.VectorPayload.HasValue);
            Assert.True(ir.MatrixTileProjection.HasValue);
            Assert.Equal(vector.OperationKind, ir.MatrixTileProjection.Value.OperationKind);
            Assert.Equal(vector.TileDescriptor, ir.MatrixTileProjection.Value.TileDescriptor);
            Assert.Equal(
                vector.SecondaryTileDescriptor,
                ir.MatrixTileProjection.Value.SecondaryTileDescriptor);
            Assert.Equal(
                vector.ResultTileDescriptor,
                ir.MatrixTileProjection.Value.ResultTileDescriptor);
            Assert.Equal(MatrixTileIrProjectionFaultKind.None, ir.MatrixTileProjection.Value.FaultKind);
            Assert.False(ir.MatrixTileProjection.Value.UsesFallbackPath);
        }
    }

    [Fact]
    public void GoldenIrProjectionMaterializesOnlyTypedMatrixTileMicroOps()
    {
        foreach (MatrixTileExecutionGoldenVector vector in
                 MatrixTilePositiveGoldenArtifactManifest.ExecutionVectors)
        {
            (InstructionIR ir, MatrixTileMicroOp microOp) = Materialize(vector.Carrier);

            Assert.True(InstructionRegistry.TryCreateMatrixTileRuntimeObject(
                ir,
                out MatrixTileMaterializedInstruction materialized,
                out MatrixTileIrProjectionFaultKind faultKind));
            Assert.Equal(MatrixTileIrProjectionFaultKind.None, faultKind);
            Assert.Equal(vector.Opcode, materialized.Opcode);
            Assert.True(materialized.IsRuntimeLegal);
            Assert.False(materialized.UsesFallbackPath);
            Assert.Equal(vector.OperationKind, microOp.OperationKind);
            Assert.Equal(ExpectedMicroOpType(vector.Opcode), microOp.GetType());
            Assert.True(microOp.PublishesTypedTileMicroOp);
            Assert.True(microOp.PublishesSchedulerLaneBinding);
            Assert.True(microOp.PublishesExecutionCaptureSemantics);
            Assert.False(microOp.UsesFallbackPath);
        }
    }

    [Fact]
    public void ExecuteRetireRollbackReplayMatchesAllPositiveGoldenVectors()
    {
        foreach (MatrixTileExecutionGoldenVector vector in
                 MatrixTilePositiveGoldenArtifactManifest.ExecutionVectors)
        {
            (_, MatrixTileMicroOp microOp) = Materialize(vector.Carrier);
            Processor.CPU_Core core = CreateCore(out Processor.MainMemoryArea memory);
            SeedGoldenInputs(ref core, memory, microOp, vector);
            AssertPreRetireState(ref core, memory, microOp, vector);

            Assert.True(microOp.Execute(ref core));
            MatrixTileExecutionCaptureRecord capture =
                Assert.IsType<MatrixTileExecutionCaptureRecord>(
                    microOp.LastExecutionCapture!.Value);
            Assert.False(capture.UsesFallbackPath);
            Assert.Equal(vector.OperationKind, capture.OperationKind);
            AssertPreRetireState(ref core, memory, microOp, vector);

            MatrixTileRetireOutcome retire =
                microOp.RetireCapturedResult(ref core, capture);
            Assert.True(retire.IsSuccess, vector.Id);
            Assert.Equal(vector.PublicationKind, retire.PublicationKind);
            AssertRetiredState(ref core, memory, microOp, vector);

            MatrixTileReplayRollbackJournal journal =
                Assert.IsType<MatrixTileReplayRollbackJournal>(
                    microOp.LastReplayRollbackJournal);
            MatrixTileRollbackOutcome rollback =
                microOp.RollbackRetiredResult(ref core, journal.ReplayIdentity);
            Assert.Equal(MatrixTileReplayRollbackLifecycle.RolledBack, rollback.Lifecycle);
            AssertRollbackState(ref core, memory, microOp, vector);

            MatrixTileRetireOutcome replay =
                microOp.ReplayRolledBackResult(ref core, journal.ReplayIdentity);
            Assert.Equal(retire, replay);
            AssertRetiredState(ref core, memory, microOp, vector);
        }
    }

    [Fact]
    public void LoadAndStoreMemoryFaultGoldensRetireAndReplayWithoutPartialState()
    {
        foreach (MatrixTileMemoryFaultGoldenVector vector in
                 MatrixTilePositiveGoldenArtifactManifest.MemoryFaultVectors)
        {
            Processor.MainMemoryArea memory = vector.Opcode == InstructionsEnum.MTILE_STORE
                ? new FailSecondWriteMemory()
                : new Processor.MainMemoryArea();
            memory.SetLength(0x400);
            Processor.CPU_Core core = CreateCore(memory);
            (_, MatrixTileMicroOp microOp) = Materialize(vector.Carrier);

            byte[] original = [0x21, 0x22, 0x23, 0x24];
            if (vector.Opcode == InstructionsEnum.MTILE_STORE)
            {
                core.SeedMatrixTileForRuntime(
                    0,
                    vector.SourceTileId,
                    microOp.TileDescriptor,
                    ParseHex(vector.SourceTileHex));
                WritePackedRows(
                    memory,
                    vector.Carrier.Word1,
                    microOp.TileDescriptor,
                    original);
            }

            Assert.True(microOp.Execute(ref core));
            MatrixTileExecutionCaptureRecord capture =
                Assert.IsType<MatrixTileExecutionCaptureRecord>(
                    microOp.LastExecutionCapture!.Value);
            Assert.Equal(vector.ExpectedExecutionFault, capture.FaultKind);
            Assert.Equal(vector.ExpectedMemoryFault, capture.MemoryFaultKind);

            if (memory is FailSecondWriteMemory failingMemory)
            {
                failingMemory.Arm();
            }

            MatrixTileRetireOutcome retire =
                microOp.RetireCapturedResult(ref core, capture);
            Assert.True(retire.FaultRetired);
            Assert.Equal(vector.ExpectedRetireFault, retire.RetireFaultKind);
            Assert.Equal(vector.ExpectedExecutionFault, retire.ExecutionFaultKind);
            Assert.Equal(vector.ExpectedMemoryFault, retire.MemoryFaultKind);
            Assert.Equal(vector.ExpectedFaultPoint, retire.HasFaultPoint);
            if (vector.ExpectedFaultPoint)
            {
                Assert.Equal(vector.ExpectedFaultRow, retire.FaultPoint.Row);
                Assert.Equal(vector.ExpectedFaultAddress, retire.FaultPoint.Address);
            }

            if (vector.Opcode == InstructionsEnum.MTILE_STORE)
            {
                Assert.True(core.TryCaptureAnyMatrixTileSnapshot(
                    0,
                    vector.SourceTileId,
                    out MatrixTileTileImage sourceSnapshot));
                Assert.Equal(
                    ParseHex(vector.SourceTileHex),
                    sourceSnapshot.Data.ToArray());
                Assert.Equal(
                    original,
                    ReadPackedRows(
                        memory,
                        vector.Carrier.Word1,
                        microOp.TileDescriptor));
            }
            else
            {
                Assert.False(core.TryCaptureAnyMatrixTileSnapshot(
                    0,
                    vector.DestinationTileId,
                    out _));
            }

            MatrixTileReplayRollbackJournal journal =
                Assert.IsType<MatrixTileReplayRollbackJournal>(
                    microOp.LastReplayRollbackJournal);
            MatrixTileRollbackOutcome rollback =
                microOp.RollbackRetiredResult(ref core, journal.ReplayIdentity);
            MatrixTileRetireOutcome replay =
                microOp.ReplayRolledBackResult(ref core, journal.ReplayIdentity);
            Assert.True(rollback.FaultOnlyRollback);
            Assert.Equal(retire, replay);
        }
    }

    [Fact]
    public void MalformedDescriptorGoldensFailBeforeTypedExecution()
    {
        foreach (MatrixTileDescriptorFaultGoldenVector vector in
                 MatrixTilePositiveGoldenArtifactManifest.DescriptorFaultVectors)
        {
            VLIW_Instruction instruction = vector.Carrier.CreateInstruction();
            InstructionIR ir = new VliwDecoderV4().Decode(in instruction, slotIndex: 0);

            Assert.True(ir.MatrixTileProjection.HasValue);
            Assert.Equal(
                vector.ExpectedProjectionFault,
                ir.MatrixTileProjection.Value.FaultKind);
            Assert.False(ir.MatrixTileProjection.Value.IsRuntimeLegal);
            Assert.False(InstructionRegistry.TryCreateMatrixTileRuntimeObject(
                ir,
                out _,
                out MatrixTileIrProjectionFaultKind faultKind));
            Assert.Equal(vector.ExpectedProjectionFault, faultKind);

            var slots = new VLIW_Instruction[8];
            slots[0] = instruction;
            DecodedInstructionBundle bundle =
                new VliwDecoderV4().DecodeInstructionBundle(
                    slots,
                    bundleAddress: 0x1200,
                    bundleSerial: 12);
            MicroOp?[] carriers =
                DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(
                    slots,
                    bundle);
            Assert.IsType<TrapMicroOp>(carriers[0]);
        }
    }

    [Fact]
    public void ReservedCarrierGoldensFailClosedAtCanonicalDecode()
    {
        foreach (MatrixTileReservedCarrierGoldenVector vector in
                 MatrixTilePositiveGoldenArtifactManifest.ReservedCarrierVectors)
        {
            VLIW_Instruction instruction = vector.Carrier.CreateInstruction();
            InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(
                () => new VliwDecoderV4().Decode(in instruction, slotIndex: 0));

            Assert.Equal((uint)vector.Opcode, instruction.OpCode);
            Assert.NotEqual((byte)0, instruction.Reserved);
            Assert.Contains("reserved", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("ReservedWord0BitsRejected", vector.ExpectedDecodeDecision);
        }
    }

    [Fact]
    public void RuntimeIlCallTargetsContainNoFallbackOrHiddenLoweringFamilies()
    {
        Type[] auditedTypes =
        [
            typeof(MatrixTileIrProjectionAndMaterializer),
            typeof(MatrixTileExecuteCaptureAbi),
            typeof(MatrixTileRetirePublicationAbi),
            typeof(MatrixTileReplayRollbackAbi),
            typeof(MatrixTileMicroOp),
            typeof(MtileLoadMicroOp),
            typeof(MtileStoreMicroOp),
            typeof(MtileMaccMicroOp),
            typeof(MtransposeMicroOp)
        ];

        var callTargets = new List<MethodBase>();
        foreach (Type type in auditedTypes)
        {
            callTargets.AddRange(ReadDirectCallTargets(type));
        }

        Assert.NotEmpty(callTargets);
        foreach (MethodBase target in callTargets)
        {
            string identity =
                $"{target.Module.Assembly.GetName().Name}:{target.DeclaringType?.FullName}.{target.Name}";
            foreach (string forbidden in
                     MatrixTileNoFallbackEvidenceContract.ForbiddenCallTargetFragments)
            {
                Assert.DoesNotContain(forbidden, identity, StringComparison.OrdinalIgnoreCase);
            }
        }

        Assert.DoesNotContain(
            callTargets,
            static target =>
                string.Equals(
                    target.Module.Assembly.GetName().Name,
                    "HybridCPU_Compiler",
                    StringComparison.Ordinal));
        Assert.True(MatrixTileNoFallbackEvidenceContract.HasIlCallTargetAudit);
        Assert.True(MatrixTileNoFallbackEvidenceContract.HasTypedCarrierAudit);
        Assert.True(MatrixTileNoFallbackEvidenceContract.HasRuntimeOwnedMemoryAudit);
        Assert.False(MatrixTileNoFallbackEvidenceContract.UsesFallbackPath);

        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string tileStatePath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "CloseToRTL",
            "Core",
            "Architecture",
            "State",
            "Architectural",
            "CPU_Core.MatrixTileState.cs");
        string tileStateSource = File.ReadAllText(tileStatePath);
        Assert.Contains("TryReadMatrixTileMemoryExact", tileStateSource, StringComparison.Ordinal);
        Assert.Contains("TryCommitRetiredMatrixTileStoreAllOrNone", tileStateSource, StringComparison.Ordinal);
        foreach (string forbidden in new[]
                 {
                     "ScalarMicroOp",
                     "VectorMicroOp",
                     "DotProduct",
                     "Lane06",
                     "Lane07",
                     "StreamEngine",
                     "ExternalAccelerator",
                     "HybridCPU_Compiler"
                 })
        {
            Assert.DoesNotContain(forbidden, tileStateSource, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Phase12ManifestIsRegeneratedButPhase14OwnsPromotionAndHandoff()
    {
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

        Assert.False(MatrixTilePositiveGoldenArtifactManifest.KeepsStatusCatalogOptionalDisabled);
        Assert.False(MatrixTilePositiveGoldenArtifactManifest.KeepsPositiveCompilerEmissionBlocked);
        Assert.True(MatrixTileCompilerEmissionHandoffPackage.HasPositiveStatusCatalogPromotion);
        Assert.True(MatrixTileCompilerEmissionHandoffPackage.HasPositiveCompilerEmissionHandoffPackage);
        Assert.True(MatrixTileCompilerEmissionHandoffPackage.CurrentCompilerImplementationExists);
    }

    private static (InstructionIR Ir, MatrixTileMicroOp MicroOp) Materialize(
        MatrixTileGoldenCarrier carrier)
    {
        var slots = new VLIW_Instruction[8];
        slots[0] = carrier.CreateInstruction();
        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle bundle = decoder.DecodeInstructionBundle(
            slots,
            bundleAddress: 0x1000,
            bundleSerial: 11);
        InstructionIR ir = bundle.GetDecodedSlot(0).RequireInstruction();
        MicroOp?[] carriers =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(
                slots,
                bundle);
        MatrixTileMicroOp microOp =
            Assert.IsAssignableFrom<MatrixTileMicroOp>(carriers[0]);
        return (ir, microOp);
    }

    private static Type ExpectedMicroOpType(InstructionsEnum opcode)
    {
        return opcode switch
        {
            InstructionsEnum.MTILE_LOAD => typeof(MtileLoadMicroOp),
            InstructionsEnum.MTILE_STORE => typeof(MtileStoreMicroOp),
            InstructionsEnum.MTILE_MACC => typeof(MtileMaccMicroOp),
            InstructionsEnum.MTRANSPOSE => typeof(MtransposeMicroOp),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };
    }

    private static void SeedGoldenInputs(
        ref Processor.CPU_Core core,
        Processor.MainMemoryArea memory,
        MatrixTileMicroOp microOp,
        MatrixTileExecutionGoldenVector vector)
    {
        if (!string.IsNullOrEmpty(vector.SourceTileHex))
        {
            core.SeedMatrixTileForRuntime(
                0,
                vector.SourceTileId,
                vector.TileDescriptor,
                ParseHex(vector.SourceTileHex));
        }

        if (!string.IsNullOrEmpty(vector.SecondaryTileHex))
        {
            core.SeedMatrixTileForRuntime(
                0,
                vector.SecondaryTileId,
                vector.SecondaryTileDescriptor,
                ParseHex(vector.SecondaryTileHex));
        }

        if (!string.IsNullOrEmpty(vector.InitialDestinationHex))
        {
            core.SeedMatrixTileForRuntime(
                0,
                vector.DestinationTileId,
                vector.ResultTileDescriptor,
                ParseHex(vector.InitialDestinationHex));
        }

        if (!string.IsNullOrEmpty(vector.InitialMemoryHex))
        {
            WritePackedRows(
                memory,
                vector.Carrier.Word1,
                microOp.TileDescriptor,
                ParseHex(vector.InitialMemoryHex));
        }
    }

    private static void AssertPreRetireState(
        ref Processor.CPU_Core core,
        Processor.MainMemoryArea memory,
        MatrixTileMicroOp microOp,
        MatrixTileExecutionGoldenVector vector)
    {
        if (vector.OperationKind == MatrixTileProjectedOperationKind.Store)
        {
            Assert.Equal(
                ParseHex(vector.InitialMemoryHex),
                ReadPackedRows(
                    memory,
                    vector.Carrier.Word1,
                    microOp.TileDescriptor));
            return;
        }

        AssertTile(
            ref core,
            vector.DestinationTileId,
            vector.ResultTileDescriptor,
            ParseHex(vector.InitialDestinationHex));
    }

    private static void AssertRetiredState(
        ref Processor.CPU_Core core,
        Processor.MainMemoryArea memory,
        MatrixTileMicroOp microOp,
        MatrixTileExecutionGoldenVector vector)
    {
        if (vector.OperationKind == MatrixTileProjectedOperationKind.Store)
        {
            Assert.Equal(
                ParseHex(vector.ExpectedRetiredHex),
                ReadPackedRows(
                    memory,
                    vector.Carrier.Word1,
                    microOp.TileDescriptor));
            return;
        }

        AssertTile(
            ref core,
            vector.DestinationTileId,
            vector.ResultTileDescriptor,
            ParseHex(vector.ExpectedRetiredHex));
    }

    private static void AssertRollbackState(
        ref Processor.CPU_Core core,
        Processor.MainMemoryArea memory,
        MatrixTileMicroOp microOp,
        MatrixTileExecutionGoldenVector vector)
    {
        if (vector.OperationKind == MatrixTileProjectedOperationKind.Store)
        {
            Assert.Equal(
                ParseHex(vector.ExpectedRollbackHex),
                ReadPackedRows(
                    memory,
                    vector.Carrier.Word1,
                    microOp.TileDescriptor));
            return;
        }

        AssertTile(
            ref core,
            vector.DestinationTileId,
            vector.ResultTileDescriptor,
            ParseHex(vector.ExpectedRollbackHex));
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
        memory.SetLength(0x400);
        return CreateCore(memory);
    }

    private static Processor.CPU_Core CreateCore(Processor.MainMemoryArea memory)
    {
        CpuCorePlatformContext context =
            CpuCorePlatformContext.CreateFixed(memory, ProcessorMode.Emulation);
        return new Processor.CPU_Core(0, context);
    }

    private static void WritePackedRows(
        Processor.MainMemoryArea memory,
        ulong baseAddress,
        MatrixTileCanonicalDescriptorAbi descriptor,
        byte[] packed)
    {
        int rowBytes = checked(descriptor.Columns * descriptor.ElementSizeBytes);
        Assert.Equal(checked(rowBytes * descriptor.Rows), packed.Length);
        for (ushort row = 0; row < descriptor.Rows; row++)
        {
            Assert.True(memory.TryWritePhysicalRange(
                baseAddress + (ulong)row * descriptor.StrideBytes,
                packed.AsSpan(row * rowBytes, rowBytes)));
        }
    }

    private static byte[] ReadPackedRows(
        Processor.MainMemoryArea memory,
        ulong baseAddress,
        MatrixTileCanonicalDescriptorAbi descriptor)
    {
        int rowBytes = checked(descriptor.Columns * descriptor.ElementSizeBytes);
        byte[] packed = new byte[checked(rowBytes * descriptor.Rows)];
        for (ushort row = 0; row < descriptor.Rows; row++)
        {
            Assert.True(memory.TryReadPhysicalRange(
                baseAddress + (ulong)row * descriptor.StrideBytes,
                packed.AsSpan(row * rowBytes, rowBytes)));
        }

        return packed;
    }

    private static byte[] ParseHex(string hex)
    {
        return Convert.FromHexString(hex);
    }

    private static void AssertCarrier(
        MatrixTileGoldenCarrier expected,
        VLIW_Instruction actual)
    {
        Assert.Equal(expected.Word0, actual.Word0);
        Assert.Equal(expected.Word1, actual.Word1);
        Assert.Equal(expected.Word2, actual.Word2);
        Assert.Equal(expected.Word3, actual.Word3);
    }

    private static IEnumerable<MethodBase> ReadDirectCallTargets(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Static |
            BindingFlags.Instance |
            BindingFlags.DeclaredOnly;

        foreach (MethodBase method in type.GetMethods(flags).Cast<MethodBase>()
                     .Concat(type.GetConstructors(flags)))
        {
            MethodBody? body = method.GetMethodBody();
            byte[]? il = body?.GetILAsByteArray();
            if (il is null)
            {
                continue;
            }

            foreach (MethodBase target in ReadMethodTokens(method, il))
            {
                yield return target;
            }
        }

        foreach (Type nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (MethodBase target in ReadDirectCallTargets(nested))
            {
                yield return target;
            }
        }
    }

    private static IEnumerable<MethodBase> ReadMethodTokens(
        MethodBase owner,
        byte[] il)
    {
        int offset = 0;
        while (offset < il.Length)
        {
            OpCode opcode = ReadOpcode(il, ref offset);
            if (opcode.OperandType is OperandType.InlineMethod or OperandType.InlineTok)
            {
                int token = BitConverter.ToInt32(il, offset);
                MethodBase? resolved = TryResolveMethod(owner, token);
                if (resolved is not null)
                {
                    yield return resolved;
                }
            }

            offset += OperandSize(opcode.OperandType, il, offset);
        }
    }

    private static OpCode ReadOpcode(byte[] il, ref int offset)
    {
        byte first = il[offset++];
        if (first != 0xFE)
        {
            return SingleByteOpCodes[first];
        }

        return MultiByteOpCodes[il[offset++]];
    }

    private static int OperandSize(
        OperandType operandType,
        byte[] il,
        int operandOffset)
    {
        return operandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or
            OperandType.ShortInlineI or
            OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineBrTarget or
            OperandType.InlineField or
            OperandType.InlineI or
            OperandType.InlineMethod or
            OperandType.InlineSig or
            OperandType.InlineString or
            OperandType.InlineTok or
            OperandType.InlineType or
            OperandType.ShortInlineR => 4,
            OperandType.InlineI8 or
            OperandType.InlineR => 8,
            OperandType.InlineSwitch =>
                4 + BitConverter.ToInt32(il, operandOffset) * 4,
            _ => throw new InvalidOperationException(
                $"Unsupported IL operand type {operandType}.")
        };
    }

    private static MethodBase? TryResolveMethod(
        MethodBase owner,
        int metadataToken)
    {
        try
        {
            Type[]? declaringArguments =
                owner.DeclaringType?.IsGenericType == true
                    ? owner.DeclaringType.GetGenericArguments()
                    : null;
            Type[]? methodArguments =
                owner.IsGenericMethod
                    ? owner.GetGenericArguments()
                    : null;
            return owner.Module.ResolveMethod(
                metadataToken,
                declaringArguments,
                methodArguments);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static readonly OpCode[] SingleByteOpCodes = BuildOpcodeTable(false);

    private static readonly OpCode[] MultiByteOpCodes = BuildOpcodeTable(true);

    private static OpCode[] BuildOpcodeTable(bool multiByte)
    {
        var table = new OpCode[256];
        foreach (FieldInfo field in typeof(OpCodes).GetFields(
                     BindingFlags.Public | BindingFlags.Static))
        {
            var opcode = (OpCode)field.GetValue(null)!;
            ushort value = unchecked((ushort)opcode.Value);
            if ((!multiByte && value <= byte.MaxValue) ||
                (multiByte && (value & 0xFF00) == 0xFE00))
            {
                table[value & 0xFF] = opcode;
            }
        }

        return table;
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
