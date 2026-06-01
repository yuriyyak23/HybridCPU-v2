using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Legality;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class DmaStreamComputeQueryCapsPhase07ATests
{
    [Fact]
    public void Phase07A_DscQueryCaps_OpcodeDecoderRegistryAndProjectionPublishLane6Carrier()
    {
        Assert.Equal(247, (ushort)InstructionsEnum.DSC_QUERY_CAPS);
        Assert.Equal((ushort)InstructionsEnum.DSC_QUERY_CAPS, IsaOpcodeValues.DSC_QUERY_CAPS);

        OpcodeInfo? maybeInfo = OpcodeRegistry.GetInfo((uint)InstructionsEnum.DSC_QUERY_CAPS);
        Assert.True(maybeInfo.HasValue);
        OpcodeInfo info = maybeInfo.Value;
        Assert.Equal("DSC_QUERY_CAPS", info.Mnemonic);
        Assert.Equal(InstructionClass.Memory, info.InstructionClass);
        Assert.Equal(SerializationClass.MemoryOrdered, info.SerializationClass);
        Assert.Equal(
            (InstructionClass.Memory, SerializationClass.MemoryOrdered),
            InstructionClassifier.Classify(InstructionsEnum.DSC_QUERY_CAPS));

        InstructionSupportStatus support =
            InstructionSupportStatusCatalog.GetStatus("DSC_QUERY_CAPS");
        Assert.True(support.IsExecutableClaim);
        Assert.Equal("Lane6DscQuery", support.ExtensionName);

        var decoder = new VliwDecoderV4();
        VLIW_Instruction raw = CreateQueryCapsInstruction(QueryRegister);
        InstructionIR ir = decoder.Decode(in raw, slotIndex: 6);
        Assert.Equal(InstructionsEnum.DSC_QUERY_CAPS, (InstructionsEnum)ir.CanonicalOpcode.Value);
        Assert.Equal(QueryRegister, ir.Rd);
        Assert.Equal(0, ir.Rs1);
        Assert.Equal(0, ir.Rs2);
        Assert.Null(ir.DmaStreamComputeDescriptor);
        Assert.False(ir.DmaStreamComputeDescriptorReference.HasValue);

        VLIW_Instruction sourceRegister = InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.DSC_QUERY_CAPS,
            DataTypeEnum.UINT64,
            QueryRegister,
            4,
            0);
        InvalidOpcodeException sourceFault = Assert.Throws<InvalidOpcodeException>(
            () => decoder.Decode(in sourceRegister, slotIndex: 6));
        Assert.Contains("x0, x0", sourceFault.Message, StringComparison.OrdinalIgnoreCase);

        MicroOp registryMicroOp = InstructionRegistry.CreateMicroOp(
            (uint)InstructionsEnum.DSC_QUERY_CAPS,
            new DecoderContext
            {
                OpCode = (uint)InstructionsEnum.DSC_QUERY_CAPS,
                Reg1ID = QueryRegister,
                Reg2ID = 0,
                Reg3ID = 0
            });
        Assert.IsType<DmaStreamComputeQueryCapsMicroOp>(registryMicroOp);

        Assert.Throws<DecodeProjectionFaultException>(() =>
            InstructionRegistry.CreateMicroOp(
                (uint)InstructionsEnum.DSC_QUERY_CAPS,
                new DecoderContext
                {
                    OpCode = (uint)InstructionsEnum.DSC_QUERY_CAPS,
                    Reg1ID = QueryRegister,
                    Reg2ID = 4,
                    Reg3ID = 0
                }));

        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[6] = raw;
        DecodedInstructionBundle decoded =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x7A00);
        BundleLegalityDescriptor legality = new BundleLegalityAnalyzer().Analyze(decoded);
        Assert.Equal(0b_0100_0000, legality.TypedSlotFacts.DmaStreamClassMask);
        Assert.Equal(0, legality.TypedSlotFacts.LsuClassMask);
        Assert.Equal(0b_0100_0000, legality.TypedSlotFacts.PinnedSlotMask);
        Assert.Equal(0, legality.TypedSlotFacts.FlexibleSlotMask);
        Assert.Equal(SlotClass.DmaStreamClass, legality.GetSlotLegality(6).SlotClass);
        Assert.False(legality.HasMemoryOps);
        Assert.True(legality.DependencySummary.HasValue);
        Assert.Equal(0UL, legality.DependencySummary.Value.ReadRegisterMask);
        Assert.Equal(1UL << (int)QueryRegister, legality.DependencySummary.Value.WriteRegisterMask);

        MicroOp?[] carriers =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, decoded);
        DmaStreamComputeQueryCapsMicroOp projected =
            Assert.IsType<DmaStreamComputeQueryCapsMicroOp>(carriers[6]);
        Assert.Equal(SlotClass.DmaStreamClass, projected.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, projected.Placement.PinningKind);
        Assert.Equal(6, projected.Placement.PinnedLaneId);
        Assert.False(projected.IsMemoryOp);
        Assert.True(projected.WritesRegister);
        Assert.Empty(projected.ReadRegisters);
        Assert.Equal(new[] { (int)QueryRegister }, projected.WriteRegisters);
    }

    [Fact]
    public void Phase07A_DscQueryCaps_ExecuteCaptureRetireWritebackPublishesBoundedCapabilityWord()
    {
        var core = new Processor.CPU_Core(0);
        DmaStreamComputeQueryCapsMicroOp query = CreateQueryCapsMicroOp();

        Assert.True(query.Execute(ref core));

        DmaStreamComputeCapabilityQueryResult result = query.LastQueryResult!;
        ulong expectedWord = ExpectedCapabilityWord;
        Assert.True(result.IsAccepted);
        Assert.Equal(expectedWord, result.EncodedCapabilityWord);
        Assert.False(result.CanIssueToken);
        Assert.False(result.CanPublishMemory);
        Assert.False(result.CanProductionLower);
        Assert.True(query.LastQueryReplayEvidence.IsComplete);
        Assert.False(query.UsedStreamEngineFallback);
        Assert.False(query.UsedDmaControllerFallback);
        Assert.Equal(0UL, core.ReadArch(0, QueryRegister));

        RetireRecord[] records = new RetireRecord[1];
        int recordCount = 0;
        query.EmitWriteBackRetireRecords(ref core, records, ref recordCount);
        Assert.Equal(1, recordCount);
        Assert.Equal(0UL, core.ReadArch(0, QueryRegister));

        core.RetireCoordinator.Retire(records.AsSpan(0, recordCount));

        Assert.Equal(expectedWord, core.ReadArch(0, QueryRegister));
    }

    [Fact]
    public void Phase07A_DscQueryCaps_DoesNotIssueTokensPublishMemoryOrChangeAcrossReplayRollback()
    {
        DmaStreamComputeDescriptor descriptor = CreateOwnedCopyDescriptor();
        Processor.CPU_Core core = CreateCoreWithStagedCopy(descriptor, out DmaStreamComputeMicroOp compute);

        DmaStreamComputeQueryCapsMicroOp before = CreateQueryCapsMicroOp(descriptor);
        Assert.True(before.Execute(ref core));
        DmaStreamComputeCapabilityQueryReplayEvidence beforeEvidence = before.LastQueryReplayEvidence;

        Assert.Equal(ExpectedCapabilityWord, before.LastQueryResult!.EncodedCapabilityWord);
        Assert.True(beforeEvidence.IsComplete);
        Assert.Equal(1, compute.ActiveTokenCount);
        Assert.Equal(DmaStreamComputeTokenState.CommitPending, compute.LastExecutionToken!.State);
        Assert.Equal(DmaStreamComputeTelemetryTests.Fill(0xEE, 16), DmaStreamComputeTelemetryTests.ReadMemory(0x9000, 16));

        compute.LastExecutionToken.Cancel(DmaStreamComputeTokenCancelReason.ReplayDiscard);

        DmaStreamComputeQueryCapsMicroOp after = CreateQueryCapsMicroOp(descriptor);
        Assert.True(after.Execute(ref core));
        DmaStreamComputeCapabilityQueryReplayEvidence afterEvidence = after.LastQueryReplayEvidence;

        Assert.Equal(ExpectedCapabilityWord, after.LastQueryResult!.EncodedCapabilityWord);
        Assert.Equal(beforeEvidence.EvidenceHash, afterEvidence.EvidenceHash);
        Assert.Equal(DmaStreamComputeTokenState.Canceled, compute.LastExecutionToken.State);
        Assert.Equal(0, compute.LastExecutionToken.StagedWriteCount);
        Assert.Equal(DmaStreamComputeTelemetryTests.Fill(0xEE, 16), DmaStreamComputeTelemetryTests.ReadMemory(0x9000, 16));
    }

    [Fact]
    public void Phase07A_DscQueryCaps_AdjacentDsc2ShapeQueryAndCompilerBoundariesStayClosed()
    {
        var decoder = new VliwDecoderV4();
        var wrongSlot = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        wrongSlot[5] = CreateQueryCapsInstruction(QueryRegister);
        InvalidOpcodeException laneFault = Assert.Throws<InvalidOpcodeException>(
            () => decoder.DecodeInstructionBundle(wrongSlot, bundleAddress: 0x7A80));
        Assert.Contains("lane6", laneFault.Message, StringComparison.OrdinalIgnoreCase);

        foreach (string mnemonic in new[] { "DSC_QUERY_BACKEND", "DSC_QUERY_SHAPE" })
        {
            InstructionSupportStatus support = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.Equal(IsaInstructionStatus.Reserved, support.Status);
            Assert.Equal(RuntimeInstructionEvidence.None, support.RuntimeEvidence);
            Assert.False(support.HasNumericOpcode);
            Assert.False(Enum.TryParse<InstructionsEnum>(mnemonic, ignoreCase: false, out _));
        }

        Assert.Equal(IsaInstructionStatus.ParserOnly, InstructionSupportStatusCatalog.GetStatus("DSC2").Status);
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, InstructionSupportStatusCatalog.GetStatus("DSC_STATUS").Status);

        string[] threadMethods = typeof(HybridCpuThreadCompilerContext)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName)
            .Select(static method => method.Name)
            .ToArray();
        Assert.DoesNotContain(threadMethods, name => name.Contains("Dsc", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("QueryCaps", StringComparison.Ordinal));

        string repoRoot = ResolveRepoRoot();
        string compilerText = File.ReadAllText(Path.Combine(
            repoRoot,
            "HybridCPU_Compiler",
            "API",
            "Threading",
            "HybridCpuThreadCompilerContext.cs"));
        Assert.DoesNotContain("DSC_QUERY_CAPS", compilerText, StringComparison.Ordinal);
        Assert.DoesNotContain("DmaStreamComputeQueryCaps", compilerText, StringComparison.Ordinal);

        string runtimeText = File.ReadAllText(Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "NonRTL",
            "Core",
            "Execution",
            "DmaStreamCompute",
            "DmaStreamComputeRuntime.cs"));
        Assert.DoesNotContain("ParseDsc2", runtimeText, StringComparison.Ordinal);
    }

    private const byte QueryRegister = 6;
    private const ulong OwnerDomainTag = 0x70AUL;

    private static ulong ExpectedCapabilityWord =>
        (ulong)(DmaStreamComputeCapabilityFlags.Lane6DmaStreamBackend |
                DmaStreamComputeCapabilityFlags.CurrentDsc1Production |
                DmaStreamComputeCapabilityFlags.DscStatusQuery |
                DmaStreamComputeCapabilityFlags.Dsc2ParserFootprintFoundation |
                DmaStreamComputeCapabilityFlags.DscQueryCaps);

    private static VLIW_Instruction CreateQueryCapsInstruction(byte destinationRegister)
    {
        return InstructionEncoder.EncodeScalar(
            (uint)InstructionsEnum.DSC_QUERY_CAPS,
            DataTypeEnum.UINT64,
            destinationRegister,
            0,
            0);
    }

    private static Processor.CPU_Core CreateCoreWithStagedCopy(
        DmaStreamComputeDescriptor descriptor,
        out DmaStreamComputeMicroOp compute)
    {
        DmaStreamComputeTelemetryTests.InitializeMainMemory(0x10000);
        DmaStreamComputeTelemetryTests.WriteMemory(0x1000, DmaStreamComputeTelemetryTests.Fill(0x11, 16));
        DmaStreamComputeTelemetryTests.WriteMemory(0x9000, DmaStreamComputeTelemetryTests.Fill(0xEE, 16));

        var core = new Processor.CPU_Core(0);
        compute = new DmaStreamComputeMicroOp(descriptor);
        Assert.True(compute.Execute(ref core));
        Assert.Equal(DmaStreamComputeTokenState.CommitPending, compute.LastExecutionToken!.State);
        return core;
    }

    private static DmaStreamComputeQueryCapsMicroOp CreateQueryCapsMicroOp(
        DmaStreamComputeDescriptor? descriptor = null)
    {
        var microOp = new DmaStreamComputeQueryCapsMicroOp(QueryRegister);
        if (descriptor is not null)
        {
            microOp.VirtualThreadId = descriptor.OwnerBinding.OwnerVirtualThreadId;
            microOp.OwnerThreadId = descriptor.OwnerBinding.OwnerVirtualThreadId;
            microOp.OwnerContextId = (int)descriptor.OwnerBinding.OwnerContextId;
            microOp.Placement = new SlotPlacementMetadata
            {
                RequiredSlotClass = SlotClass.DmaStreamClass,
                PinningKind = SlotPinningKind.HardPinned,
                PinnedLaneId = 6,
                DomainTag = descriptor.OwnerBinding.OwnerDomainTag
            };
        }

        microOp.RefreshOwnerDependentMetadata();
        return microOp;
    }

    private static DmaStreamComputeDescriptor CreateOwnedCopyDescriptor()
    {
        byte[] descriptorBytes =
            DmaStreamComputeTestDescriptorFactory.BuildDescriptor(
                DmaStreamComputeOperationKind.Copy);
        WriteUInt16(descriptorBytes, 60, 0);
        WriteUInt32(descriptorBytes, 64, 0);
        WriteUInt32(descriptorBytes, 68, 0);
        WriteUInt32(descriptorBytes, 72, 0);
        WriteUInt64(descriptorBytes, 80, OwnerDomainTag);

        DmaStreamComputeDescriptorReference reference =
            DmaStreamComputeTestDescriptorFactory.CreateReference(descriptorBytes);
        DmaStreamComputeValidationResult validation =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                DmaStreamComputeTestDescriptorFactory.CreateGuardDecision(descriptorBytes, reference),
                reference);
        Assert.True(validation.IsValid, validation.Message);
        return validation.RequireDescriptorForAdmission();
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value) =>
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteUInt64(byte[] bytes, int offset, ulong value) =>
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset), value);

    private static string ResolveRepoRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            if (File.Exists(Path.Combine(directory, "HybridCPU_ISE.sln")) ||
                Directory.Exists(Path.Combine(directory, "HybridCPU_ISE")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException("Unable to resolve repository root for Phase 07A boundary test.");
    }
}
