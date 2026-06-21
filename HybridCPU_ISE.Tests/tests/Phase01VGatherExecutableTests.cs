using HybridCPU_ISE.Arch;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HybridCPU.Compiler.Core.API.Facade;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using HybridCPU_ISE.CloseToRTL.Memory.MMU;

namespace HybridCPU_ISE.Tests;

public sealed class VGatherExecutableTests
{
    [Fact]
    public void VGather_StatusClassifierDecoderProjectionAndMaterializer_AreExecutableForIndexedReadOnly()
    {
        const ulong descriptorAddress = 0x200UL;
        const ulong sourceBase = 0x400UL;
        const ulong indexBase = 0x600UL;
        const ulong destinationBase = 0x800UL;

        InitializeMemorySubsystem();
        SeedVectorWordMemory(sourceBase, 10U, 20U, 30U, 40U);
        SeedIndexMemory(indexBase, 2U, 0U, 3U, 1U);
        WriteIndexedDescriptor(descriptorAddress, sourceBase, indexBase, indexStride: 4, indexType: 0, indexIsByteOffset: 0);

        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("VGATHER");
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.True(status.IsExecutableClaim);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);

        Assert.Equal(InstructionClass.Memory, InstructionClassifier.GetClass(InstructionsEnum.VGATHER));
        Assert.Equal(SerializationClass.Free, InstructionClassifier.GetSerializationClass(InstructionsEnum.VGATHER));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.VGATHER));

        VLIW_Instruction instruction = CreateVGatherInstruction(destinationBase, descriptorAddress, streamLength: 4);
        (InstructionIR ir, MicroOp microOp) = DecodeAndMaterialize(instruction);

        Assert.Equal((ushort)InstructionsEnum.VGATHER, ir.CanonicalOpcode.Value);
        Assert.NotNull(ir.VectorPayload);
        VectorInstructionPayload payload = ir.VectorPayload!.Value;
        Assert.Equal(destinationBase, payload.PrimaryPointer);
        Assert.Equal(descriptorAddress, payload.SecondaryPointer);
        Assert.Equal(4U, payload.StreamLength);
        Assert.True(payload.Indexed);
        Assert.False(payload.Is2D);
        Assert.Equal((byte)DataTypeEnum.UINT32, payload.DataType);

        GatherMicroOp gather = Assert.IsType<GatherMicroOp>(microOp);
        Assert.Equal(InstructionClass.Memory, gather.InstructionClass);
        Assert.Equal(SerializationClass.Free, gather.SerializationClass);
        Assert.Equal(MicroOpClass.Lsu, gather.Class);
        Assert.Equal(SlotClass.LsuClass, gather.Placement.RequiredSlotClass);
        Assert.Contains(gather.ReadMemoryRanges, range => range.Address == descriptorAddress && range.Length == 32);
        Assert.Contains(gather.ReadMemoryRanges, range => range.Address == indexBase && range.Length == 16);
        Assert.Contains(gather.ReadMemoryRanges, range => range.Address == sourceBase && range.Length == 16);
        Assert.Contains(gather.WriteMemoryRanges, range => range.Address == destinationBase && range.Length == 16);
    }

    [Fact]
    public void VGather_ExecuteStagesIndexedReadsAndPublishesDestinationOnlyAtWriteBack()
    {
        const ulong descriptorAddress = 0x200UL;
        const ulong sourceBase = 0x400UL;
        const ulong indexBase = 0x600UL;
        const ulong destinationBase = 0x800UL;

        InitializeMemorySubsystem();
        SeedVectorWordMemory(sourceBase, 10U, 20U, 30U, 40U);
        SeedVectorWordMemory(destinationBase, 0xAAAA_0001U, 0xAAAA_0002U, 0xAAAA_0003U, 0xAAAA_0004U);
        SeedIndexMemory(indexBase, 3U, 1U, 0U, 2U);
        WriteIndexedDescriptor(descriptorAddress, sourceBase, indexBase, indexStride: 4, indexType: 0, indexIsByteOffset: 0);

        VLIW_Instruction instruction = CreateVGatherInstruction(destinationBase, descriptorAddress, streamLength: 4);
        GatherMicroOp gather = Assert.IsType<GatherMicroOp>(DecodeAndMaterialize(instruction).MicroOp);

        var core = new Processor.CPU_Core(0);
        Assert.True(gather.Execute(ref core));

        Assert.Equal(new[] { 0xAAAA_0001U, 0xAAAA_0002U, 0xAAAA_0003U, 0xAAAA_0004U }, ReadVectorWords(destinationBase, 4));
        Assert.Equal(new[] { 40U, 20U, 10U, 30U }, ReadWordsFromBuffer(gather.GetLoadedBuffer(), 4));
        Assert.Equal(new ulong[] { 3UL, 1UL, 0UL, 2UL }, gather.GetIndices());

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        gather.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);

        Assert.Equal(0, retireRecordCount);
        Assert.Equal(new[] { 40U, 20U, 10U, 30U }, ReadVectorWords(destinationBase, 4));
    }

    [Fact]
    public void VGather_MaskedLanesRemainUndisturbedUntilWriteBackPublication()
    {
        const ulong descriptorAddress = 0x220UL;
        const ulong sourceBase = 0x420UL;
        const ulong indexBase = 0x620UL;
        const ulong destinationBase = 0x820UL;

        InitializeMemorySubsystem();
        SeedVectorWordMemory(sourceBase, 11U, 22U, 33U, 44U);
        SeedVectorWordMemory(destinationBase, 100U, 101U, 102U, 103U);
        SeedIndexMemory(indexBase, 0U, 1U, 2U, 3U);
        WriteIndexedDescriptor(descriptorAddress, sourceBase, indexBase, indexStride: 4, indexType: 0, indexIsByteOffset: 0);

        VLIW_Instruction instruction = CreateVGatherInstruction(destinationBase, descriptorAddress, streamLength: 4, predicateMask: 1);
        GatherMicroOp gather = Assert.IsType<GatherMicroOp>(DecodeAndMaterialize(instruction).MicroOp);

        var core = new Processor.CPU_Core(0);
        core.SetPredicateRegister(1, 0b0101UL);
        Assert.True(gather.Execute(ref core));

        Assert.Equal(new[] { 100U, 101U, 102U, 103U }, ReadVectorWords(destinationBase, 4));

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        gather.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);

        Assert.Equal(new[] { 11U, 101U, 33U, 103U }, ReadVectorWords(destinationBase, 4));
    }

    [Fact]
    public void VGather_WhenIndexedSourceFaults_ThenDestinationRemainsUnpublished()
    {
        const ulong descriptorAddress = 0x240UL;
        const ulong sourceBase = 0x0FFF_FFFCUL;
        const ulong indexBase = 0x640UL;
        const ulong destinationBase = 0x840UL;

        InitializeMemorySubsystem(mappedBytes: 0x1000UL);
        SeedVectorWordMemory(destinationBase, 7U, 8U);
        SeedIndexMemory(indexBase, 0U, 1U);
        WriteIndexedDescriptor(descriptorAddress, sourceBase, indexBase, indexStride: 4, indexType: 0, indexIsByteOffset: 0);

        VLIW_Instruction instruction = CreateVGatherInstruction(destinationBase, descriptorAddress, streamLength: 2);
        GatherMicroOp gather = Assert.IsType<GatherMicroOp>(DecodeAndMaterialize(instruction).MicroOp);

        var core = new Processor.CPU_Core(0);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => gather.Execute(ref core));

        Assert.Contains("GatherMicroOp.Execute", exception.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { 7U, 8U }, ReadVectorWords(destinationBase, 2));
    }

    [Fact]
    public void VGather_ReplayDiscardedStagedResult_DoesNotPublishBeforeFreshWriteBack()
    {
        const ulong descriptorAddress = 0x260UL;
        const ulong sourceBase = 0x460UL;
        const ulong indexBase = 0x660UL;
        const ulong destinationBase = 0x860UL;

        InitializeMemorySubsystem();
        SeedVectorWordMemory(sourceBase, 11U, 22U);
        SeedVectorWordMemory(destinationBase, 0U, 0U);
        SeedIndexMemory(indexBase, 0U, 1U);
        WriteIndexedDescriptor(descriptorAddress, sourceBase, indexBase, indexStride: 4, indexType: 0, indexIsByteOffset: 0);

        var core = new Processor.CPU_Core(0);
        VLIW_Instruction instruction = CreateVGatherInstruction(destinationBase, descriptorAddress, streamLength: 2);
        GatherMicroOp discardedGather = Assert.IsType<GatherMicroOp>(DecodeAndMaterialize(instruction).MicroOp);
        Assert.True(discardedGather.Execute(ref core));
        Assert.Equal(new[] { 0U, 0U }, ReadVectorWords(destinationBase, 2));

        SeedIndexMemory(indexBase, 1U, 0U);
        GatherMicroOp replayedGather = Assert.IsType<GatherMicroOp>(DecodeAndMaterialize(instruction).MicroOp);
        Assert.True(replayedGather.Execute(ref core));

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        replayedGather.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);

        Assert.Equal(new[] { 22U, 11U }, ReadVectorWords(destinationBase, 2));
    }

    [Fact]
    public void VGather_StaleIndexReplayEvidenceInvalidatesAndExecuteRereadsCurrentIndex()
    {
        const ulong descriptorAddress = 0x280UL;
        const ulong sourceBase = 0x480UL;
        const ulong indexBase = 0x680UL;
        const ulong destinationBase = 0x880UL;

        InitializeMemorySubsystem();
        SeedVectorWordMemory(sourceBase, 100U, 200U, 300U, 400U);
        SeedVectorWordMemory(destinationBase, 0U, 0U);
        SeedIndexMemory(indexBase, 0U, 1U);
        WriteIndexedDescriptor(descriptorAddress, sourceBase, indexBase, indexStride: 4, indexType: 0, indexIsByteOffset: 0);

        VLIW_Instruction instruction = CreateVGatherInstruction(destinationBase, descriptorAddress, streamLength: 2);
        GatherMicroOp gather = Assert.IsType<GatherMicroOp>(DecodeAndMaterialize(instruction).MicroOp);

        Assert.Contains(gather.ReadMemoryRanges, range => range.Address == indexBase && range.Length == 8);

        var indexWriter = new MemoryWriterEvidenceMicroOp(indexBase, 8);
        Assert.True(SafetyVerifier.TryClassifyMemoryFootprintInvalidation(
            indexWriter,
            gather,
            out ReplayPhaseInvalidationReason reason));
        Assert.Equal(ReplayPhaseInvalidationReason.MemoryFootprintOverlap, reason);

        SeedIndexMemory(indexBase, 3U, 2U);

        var core = new Processor.CPU_Core(0);
        Assert.True(gather.Execute(ref core));
        Assert.Equal(new[] { 400U, 300U }, ReadWordsFromBuffer(gather.GetLoadedBuffer(), 2));
        Assert.Equal(new ulong[] { 3UL, 2UL }, gather.GetIndices());
        Assert.Equal(new[] { 0U, 0U }, ReadVectorWords(destinationBase, 2));

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        gather.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);

        Assert.Equal(new[] { 400U, 300U }, ReadVectorWords(destinationBase, 2));
    }

    [Fact]
    public void VGather_AdjacentContoursRemainFailClosed()
    {
        InstructionSupportStatus scatterStatus = InstructionSupportStatusCatalog.GetStatus("VSCATTER");
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, scatterStatus.Status);
        Assert.True(scatterStatus.IsExecutableClaim);
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.VSCATTER));

        Assert.Equal(
            VectorContourLegalityStatus.Executable,
            VectorLegalityMatrix.GetAddressingStatus(InstructionsEnum.VGATHER, indexed: true, is2D: false));
        Assert.Equal(
            VectorContourLegalityStatus.FailClosed,
            VectorLegalityMatrix.GetAddressingStatus(InstructionsEnum.VGATHER, indexed: false, is2D: true));
        Assert.Equal(
            VectorContourLegalityStatus.FailClosed,
            VectorLegalityMatrix.GetAddressingStatus(InstructionsEnum.VGATHER, indexed: true, is2D: true));
        Assert.True(VectorLegalityMatrix.AllowsAddressingExecution(InstructionsEnum.VSCATTER, indexed: true, is2D: false));

        Assert.Contains(
            "non-indexed",
            Assert.Throws<DecodeProjectionFaultException>(
                () => InstructionRegistry.CreateMicroOp(
                    (uint)InstructionsEnum.VGATHER,
                    CreateVectorContext(InstructionsEnum.VGATHER, indexed: false, is2D: false))).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "2D",
            Assert.Throws<DecodeProjectionFaultException>(
                () => InstructionRegistry.CreateMicroOp(
                    (uint)InstructionsEnum.VGATHER,
                    CreateVectorContext(InstructionsEnum.VGATHER, indexed: false, is2D: true))).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "indexed+2D",
            Assert.Throws<DecodeProjectionFaultException>(
                () => InstructionRegistry.CreateMicroOp(
                    (uint)InstructionsEnum.VGATHER,
                    CreateVectorContext(InstructionsEnum.VGATHER, indexed: true, is2D: true))).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void VGather_CompilerSurfaceRemainsNoEmissionForTypedGatherHelpers()
    {
        Type[] publicCompilerSurfaceTypes =
        [
            typeof(HybridCpuThreadCompilerContext),
            typeof(IPlatformAsmFacade),
            typeof(PlatformAsmFacade)
        ];

        string[] publicMethodNames = publicCompilerSurfaceTypes
            .SelectMany(type => type.GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.DeclaredOnly))
            .Select(method => method.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Contains("VectorOp", publicMethodNames);
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Gather", StringComparison.Ordinal));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Scatter", StringComparison.Ordinal));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("VectorIndexed", StringComparison.Ordinal));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Vector2D", StringComparison.Ordinal));

        string compilerSource = CompilerSourceScanner.ReadCompilerEmissionSurfaceSource();
        Assert.DoesNotContain("VGATHER", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("VSCATTER", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("EncodeVectorIndexed", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("EncodeVector2D", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain(" Gather(", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain(" Scatter(", compilerSource, StringComparison.Ordinal);
    }

    private static (InstructionIR Ir, MicroOp MicroOp) DecodeAndMaterialize(
        VLIW_Instruction instruction,
        int slotIndex = 4)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[slotIndex] = instruction;

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x2400, bundleSerial: 73);
        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        InstructionIR ir = canonicalBundle.GetDecodedSlot(slotIndex).RequireInstruction();
        return (ir, Assert.IsAssignableFrom<MicroOp>(carrierBundle[slotIndex]));
    }

    private static DecoderContext CreateVectorContext(
        InstructionsEnum opcode,
        bool indexed,
        bool is2D)
    {
        return new DecoderContext
        {
            OpCode = (uint)opcode,
            HasDataType = true,
            DataType = (byte)DataTypeEnum.UINT32,
            HasVectorAddressingContour = true,
            IndexedAddressing = indexed,
            Is2DAddressing = is2D,
            HasVectorPayload = true,
            VectorPrimaryPointer = 0x800,
            VectorSecondaryPointer = 0x200,
            VectorStreamLength = 4,
            VectorStride = 4,
            PredicateMask = 0
        };
    }

    private static VLIW_Instruction CreateVGatherInstruction(
        ulong destinationBase,
        ulong descriptorAddress,
        uint streamLength,
        byte predicateMask = 0)
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeVectorIndexed(
            (uint)InstructionsEnum.VGATHER,
            DataTypeEnum.UINT32,
            destinationBase,
            descriptorAddress,
            streamLength,
            predicateMask);
        instruction.Stride = 4;
        return instruction;
    }

    private static void InitializeMemorySubsystem(ulong mappedBytes = 0x4000UL)
    {
        Processor.MainMemory = new Processor.MultiBankMemoryArea(4, 0x4000000UL);
        IOMMU.Initialize();
        IOMMU.RegisterDevice(0);
        IOMMU.Map(
            deviceID: 0,
            ioVirtualAddress: 0,
            physicalAddress: 0,
            size: mappedBytes,
            permissions: IOMMUAccessPermissions.ReadWrite);

        Processor proc = default;
        Processor.Memory = new MemorySubsystem(ref proc);
    }

    private static void SeedVectorWordMemory(ulong address, params uint[] values)
    {
        byte[] data = new byte[values.Length * sizeof(uint)];
        for (int i = 0; i < values.Length; i++)
        {
            BitConverter.GetBytes(values[i]).CopyTo(data, i * sizeof(uint));
        }

        Processor.MainMemory.WriteToPosition(data, address);
    }

    private static void SeedIndexMemory(ulong address, params uint[] indices)
    {
        SeedVectorWordMemory(address, indices);
    }

    private static void WriteIndexedDescriptor(
        ulong descriptorAddress,
        ulong sourceBase,
        ulong indexBase,
        ushort indexStride,
        byte indexType,
        byte indexIsByteOffset)
    {
        byte[] descriptor = new byte[32];
        BitConverter.GetBytes(sourceBase).CopyTo(descriptor, 0);
        BitConverter.GetBytes(indexBase).CopyTo(descriptor, 8);
        BitConverter.GetBytes(indexStride).CopyTo(descriptor, 16);
        descriptor[18] = indexType;
        descriptor[19] = indexIsByteOffset;
        Processor.MainMemory.WriteToPosition(descriptor, descriptorAddress);
    }

    private static uint[] ReadVectorWords(ulong address, int count)
    {
        byte[] bytes = Processor.MainMemory.ReadFromPosition(new byte[count * sizeof(uint)], address, (ulong)(count * sizeof(uint)));
        return ReadWordsFromBuffer(bytes, count);
    }

    private static uint[] ReadWordsFromBuffer(byte[]? bytes, int count)
    {
        Assert.NotNull(bytes);
        return Enumerable.Range(0, count)
            .Select(index => BitConverter.ToUInt32(bytes!, index * sizeof(uint)))
            .ToArray();
    }

    private sealed class MemoryWriterEvidenceMicroOp : MicroOp
    {
        public MemoryWriterEvidenceMicroOp(ulong address, ulong length)
        {
            IsMemoryOp = true;
            HasSideEffects = true;
            InstructionClass = InstructionClass.Memory;
            SerializationClass = SerializationClass.MemoryOrdered;
            WriteMemoryRanges = [(address, length)];
            ReadMemoryRanges = Array.Empty<(ulong Address, ulong Length)>();
            SetClassFlexiblePlacement(SlotClass.LsuClass);
        }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.SelfPublishes;

        public override bool Execute(ref Processor.CPU_Core core) => true;

        public override string GetDescription() => "Phase01 memory writer replay evidence";
    }
}
