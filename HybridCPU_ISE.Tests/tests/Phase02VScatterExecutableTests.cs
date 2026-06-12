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

namespace HybridCPU_ISE.Tests.Phase02;

public sealed class Phase02VScatterExecutableTests
{
    [Fact]
    public void VScatter_StatusClassifierDecoderProjectionAndMaterializer_AreExecutableForIndexedWriteOnly()
    {
        const ulong descriptorAddress = 0x200UL;
        const ulong sourceDataBase = 0x400UL;
        const ulong indexBase = 0x600UL;
        const ulong targetBase = 0x800UL;

        InitializeMemorySubsystem();
        SeedVectorWordMemory(sourceDataBase, 10U, 20U, 30U, 40U);
        SeedIndexMemory(indexBase, 2U, 0U, 3U, 1U);
        WriteIndexedDescriptor(descriptorAddress, targetBase, indexBase, indexStride: 4, indexType: 0, indexIsByteOffset: 0);

        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("VSCATTER");
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.True(status.IsExecutableClaim);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);

        Assert.Equal(InstructionClass.Memory, InstructionClassifier.GetClass(InstructionsEnum.VSCATTER));
        Assert.Equal(SerializationClass.MemoryOrdered, InstructionClassifier.GetSerializationClass(InstructionsEnum.VSCATTER));
        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.VSCATTER));

        VLIW_Instruction instruction = CreateVScatterInstruction(sourceDataBase, descriptorAddress, streamLength: 4);
        (InstructionIR ir, MicroOp microOp) = DecodeAndMaterialize(instruction);

        Assert.Equal((ushort)InstructionsEnum.VSCATTER, ir.CanonicalOpcode.Value);
        Assert.NotNull(ir.VectorPayload);
        VectorInstructionPayload payload = ir.VectorPayload!.Value;
        Assert.Equal(sourceDataBase, payload.PrimaryPointer);
        Assert.Equal(descriptorAddress, payload.SecondaryPointer);
        Assert.Equal(4U, payload.StreamLength);
        Assert.True(payload.Indexed);
        Assert.False(payload.Is2D);
        Assert.Equal((byte)DataTypeEnum.UINT32, payload.DataType);

        StoreScatterMicroOp scatter = Assert.IsType<StoreScatterMicroOp>(microOp);
        Assert.Equal(InstructionClass.Memory, scatter.InstructionClass);
        Assert.Equal(SerializationClass.MemoryOrdered, scatter.SerializationClass);
        Assert.Equal(MicroOpClass.Lsu, scatter.Class);
        Assert.Equal(SlotClass.LsuClass, scatter.Placement.RequiredSlotClass);
        Assert.Contains(scatter.ReadMemoryRanges, range => range.Address == descriptorAddress && range.Length == 32);
        Assert.Contains(scatter.ReadMemoryRanges, range => range.Address == indexBase && range.Length == 16);
        Assert.Contains(scatter.ReadMemoryRanges, range => range.Address == sourceDataBase && range.Length == 16);
        Assert.Contains(scatter.WriteMemoryRanges, range => range.Address == targetBase + 8 && range.Length == 4);
        Assert.Contains(scatter.WriteMemoryRanges, range => range.Address == targetBase && range.Length == 4);
        Assert.Contains(scatter.WriteMemoryRanges, range => range.Address == targetBase + 12 && range.Length == 4);
        Assert.Contains(scatter.WriteMemoryRanges, range => range.Address == targetBase + 4 && range.Length == 4);
    }

    [Fact]
    public void VScatter_ExecuteStagesIndexedWritesAndPublishesMemoryOnlyAtWriteBack()
    {
        const ulong descriptorAddress = 0x220UL;
        const ulong sourceDataBase = 0x420UL;
        const ulong indexBase = 0x620UL;
        const ulong targetBase = 0x820UL;

        InitializeMemorySubsystem();
        SeedVectorWordMemory(sourceDataBase, 10U, 20U, 30U, 40U);
        SeedVectorWordMemory(targetBase, 0xAAAA_0001U, 0xAAAA_0002U, 0xAAAA_0003U, 0xAAAA_0004U);
        SeedIndexMemory(indexBase, 3U, 1U, 0U, 2U);
        WriteIndexedDescriptor(descriptorAddress, targetBase, indexBase, indexStride: 4, indexType: 0, indexIsByteOffset: 0);

        VLIW_Instruction instruction = CreateVScatterInstruction(sourceDataBase, descriptorAddress, streamLength: 4);
        StoreScatterMicroOp scatter = Assert.IsType<StoreScatterMicroOp>(DecodeAndMaterialize(instruction).MicroOp);

        var core = new Processor.CPU_Core(0);
        Assert.True(scatter.Execute(ref core));

        Assert.Equal(new[] { 0xAAAA_0001U, 0xAAAA_0002U, 0xAAAA_0003U, 0xAAAA_0004U }, ReadVectorWords(targetBase, 4));
        Assert.Equal(new ulong[] { 3UL, 1UL, 0UL, 2UL }, scatter.GetIndices());
        Assert.Equal(
            new[] { targetBase + 12, targetBase + 4, targetBase, targetBase + 8 },
            scatter.GetStagedWrites().Select(write => write.Address).ToArray());

        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        scatter.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);

        Assert.Equal(0, retireRecordCount);
        Assert.Equal(new[] { 30U, 20U, 40U, 10U }, ReadVectorWords(targetBase, 4));
    }

    [Fact]
    public void VScatter_DuplicateAndOverlapAddressesCommitInElementOrder()
    {
        const ulong descriptorAddress = 0x240UL;
        const ulong sourceDataBase = 0x440UL;
        const ulong indexBase = 0x640UL;
        const ulong targetBase = 0x840UL;

        InitializeMemorySubsystem();
        SeedVectorWordMemory(sourceDataBase, 11U, 22U, 33U);
        SeedVectorWordMemory(targetBase, 0U, 0U, 0U);
        SeedIndexMemory(indexBase, 1U, 1U, 1U);
        WriteIndexedDescriptor(descriptorAddress, targetBase, indexBase, indexStride: 4, indexType: 0, indexIsByteOffset: 0);

        VLIW_Instruction duplicateInstruction = CreateVScatterInstruction(sourceDataBase, descriptorAddress, streamLength: 3);
        StoreScatterMicroOp duplicateScatter =
            Assert.IsType<StoreScatterMicroOp>(DecodeAndMaterialize(duplicateInstruction).MicroOp);

        var core = new Processor.CPU_Core(0);
        Assert.True(duplicateScatter.Execute(ref core));
        Publish(duplicateScatter, ref core);

        Assert.Equal(new[] { 0U, 33U, 0U }, ReadVectorWords(targetBase, 3));

        const ulong overlapDescriptorAddress = 0x280UL;
        const ulong overlapSourceBase = 0x480UL;
        const ulong overlapIndexBase = 0x680UL;
        const ulong overlapTargetBase = 0x880UL;

        SeedVectorWordMemory(overlapSourceBase, 0x1122_3344U, 0xAABB_CCDDU);
        WriteBytes(overlapTargetBase, Enumerable.Repeat((byte)0xCC, 8).ToArray());
        SeedIndexMemory(overlapIndexBase, 0U, 2U);
        WriteIndexedDescriptor(overlapDescriptorAddress, overlapTargetBase, overlapIndexBase, indexStride: 4, indexType: 0, indexIsByteOffset: 1);

        VLIW_Instruction overlapInstruction = CreateVScatterInstruction(overlapSourceBase, overlapDescriptorAddress, streamLength: 2);
        StoreScatterMicroOp overlapScatter =
            Assert.IsType<StoreScatterMicroOp>(DecodeAndMaterialize(overlapInstruction).MicroOp);

        Assert.True(overlapScatter.Execute(ref core));
        Publish(overlapScatter, ref core);

        Assert.Equal(
            new byte[] { 0x44, 0x33, 0xDD, 0xCC, 0xBB, 0xAA, 0xCC, 0xCC },
            ReadBytes(overlapTargetBase, 8));
    }

    [Fact]
    public void VScatter_WhenSourceOrTargetFaults_ThenNoPartialMemoryVisibilityLeaks()
    {
        const ulong descriptorAddress = 0x200UL;
        const ulong sourceDataBase = 0x1FFF_FFFCUL;
        const ulong indexBase = 0x300UL;
        const ulong targetBase = 0x500UL;

        InitializeMemorySubsystem();
        SeedVectorWordMemory(targetBase, 7U, 8U);
        SeedIndexMemory(indexBase, 0U, 1U);
        WriteIndexedDescriptor(descriptorAddress, targetBase, indexBase, indexStride: 4, indexType: 0, indexIsByteOffset: 0);

        VLIW_Instruction sourceFaultInstruction = CreateVScatterInstruction(sourceDataBase, descriptorAddress, streamLength: 2);
        StoreScatterMicroOp sourceFaultScatter =
            Assert.IsType<StoreScatterMicroOp>(DecodeAndMaterialize(sourceFaultInstruction).MicroOp);

        var core = new Processor.CPU_Core(0);
        InvalidOperationException sourceException =
            Assert.Throws<InvalidOperationException>(() => sourceFaultScatter.Execute(ref core));

        Assert.Contains("StoreScatterMicroOp.Execute", sourceException.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { 7U, 8U }, ReadVectorWords(targetBase, 2));

        const ulong targetFaultDescriptorAddress = 0x100UL;
        const ulong targetFaultSourceBase = 0x200UL;
        const ulong targetFaultIndexBase = 0x300UL;
        const ulong targetFaultBase = 0xFFCUL;

        InitializeMemorySubsystem(mainMemoryBytes: 0x400UL, mappedBytes: 0x1000UL);
        SeedVectorWordMemory(targetFaultSourceBase, 11U, 22U);
        SeedIndexMemory(targetFaultIndexBase, 0U, 1U);
        WriteIndexedDescriptor(targetFaultDescriptorAddress, targetFaultBase, targetFaultIndexBase, indexStride: 4, indexType: 0, indexIsByteOffset: 0);
        WriteBytes(targetFaultBase, new byte[] { 0xAA, 0xAA, 0xAA, 0xAA });

        VLIW_Instruction targetFaultInstruction = CreateVScatterInstruction(targetFaultSourceBase, targetFaultDescriptorAddress, streamLength: 2);
        StoreScatterMicroOp targetFaultScatter =
            Assert.IsType<StoreScatterMicroOp>(DecodeAndMaterialize(targetFaultInstruction).MicroOp);

        var targetFaultCore = new Processor.CPU_Core(0);
        InvalidOperationException targetException =
            Assert.Throws<InvalidOperationException>(() => targetFaultScatter.Execute(ref targetFaultCore));

        Assert.Contains("StoreScatterMicroOp.Execute", targetException.Message, StringComparison.Ordinal);
        Assert.Equal(new byte[] { 0xAA, 0xAA, 0xAA, 0xAA }, ReadBytes(targetFaultBase, 4));
    }

    [Fact]
    public void VScatter_RollbackDiscardedStagedWrites_DoNotPublishBeforeFreshReplayCommit()
    {
        const ulong descriptorAddress = 0x2A0UL;
        const ulong sourceDataBase = 0x4A0UL;
        const ulong indexBase = 0x6A0UL;
        const ulong targetBase = 0x8A0UL;

        InitializeMemorySubsystem();
        SeedVectorWordMemory(sourceDataBase, 11U, 22U);
        SeedVectorWordMemory(targetBase, 0U, 0U);
        SeedIndexMemory(indexBase, 0U, 1U);
        WriteIndexedDescriptor(descriptorAddress, targetBase, indexBase, indexStride: 4, indexType: 0, indexIsByteOffset: 0);

        var core = new Processor.CPU_Core(0);
        VLIW_Instruction instruction = CreateVScatterInstruction(sourceDataBase, descriptorAddress, streamLength: 2);
        StoreScatterMicroOp discardedScatter =
            Assert.IsType<StoreScatterMicroOp>(DecodeAndMaterialize(instruction).MicroOp);
        Assert.True(discardedScatter.Execute(ref core));
        Assert.Equal(new[] { 0U, 0U }, ReadVectorWords(targetBase, 2));

        SeedVectorWordMemory(sourceDataBase, 33U, 44U);
        SeedIndexMemory(indexBase, 1U, 0U);

        StoreScatterMicroOp replayedScatter =
            Assert.IsType<StoreScatterMicroOp>(DecodeAndMaterialize(instruction).MicroOp);
        Assert.True(replayedScatter.Execute(ref core));
        Publish(replayedScatter, ref core);

        Assert.Equal(new[] { 44U, 33U }, ReadVectorWords(targetBase, 2));
    }

    [Fact]
    public void VScatter_AdjacentContoursRemainFailClosedAndCompilerSurfaceRemainsNoEmission()
    {
        Assert.Equal(
            VectorContourLegalityStatus.Executable,
            VectorLegalityMatrix.GetAddressingStatus(InstructionsEnum.VSCATTER, indexed: true, is2D: false));
        Assert.Equal(
            VectorContourLegalityStatus.FailClosed,
            VectorLegalityMatrix.GetAddressingStatus(InstructionsEnum.VSCATTER, indexed: false, is2D: false));
        Assert.Equal(
            VectorContourLegalityStatus.FailClosed,
            VectorLegalityMatrix.GetAddressingStatus(InstructionsEnum.VSCATTER, indexed: false, is2D: true));
        Assert.Equal(
            VectorContourLegalityStatus.FailClosed,
            VectorLegalityMatrix.GetAddressingStatus(InstructionsEnum.VSCATTER, indexed: true, is2D: true));

        Assert.Contains(
            "non-indexed",
            Assert.Throws<DecodeProjectionFaultException>(
                () => InstructionRegistry.CreateMicroOp(
                    (uint)InstructionsEnum.VSCATTER,
                    CreateVectorContext(InstructionsEnum.VSCATTER, indexed: false, is2D: false))).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "2D",
            Assert.Throws<DecodeProjectionFaultException>(
                () => InstructionRegistry.CreateMicroOp(
                    (uint)InstructionsEnum.VSCATTER,
                    CreateVectorContext(InstructionsEnum.VSCATTER, indexed: false, is2D: true))).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "indexed+2D",
            Assert.Throws<DecodeProjectionFaultException>(
                () => InstructionRegistry.CreateMicroOp(
                    (uint)InstructionsEnum.VSCATTER,
                    CreateVectorContext(InstructionsEnum.VSCATTER, indexed: true, is2D: true))).Message,
            StringComparison.Ordinal);

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
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x2800, bundleSerial: 74);
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

    private static VLIW_Instruction CreateVScatterInstruction(
        ulong sourceDataBase,
        ulong descriptorAddress,
        uint streamLength,
        byte predicateMask = 0)
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeVectorIndexed(
            (uint)InstructionsEnum.VSCATTER,
            DataTypeEnum.UINT32,
            sourceDataBase,
            descriptorAddress,
            streamLength,
            predicateMask);
        instruction.Stride = 4;
        return instruction;
    }

    private static void Publish(StoreScatterMicroOp scatter, ref Processor.CPU_Core core)
    {
        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        scatter.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);
        Assert.Equal(0, retireRecordCount);
    }

    private static void InitializeMemorySubsystem(
        ulong mainMemoryBytes = 0x4000000UL,
        ulong mappedBytes = 0x4000UL)
    {
        Processor.MainMemory = new Processor.MultiBankMemoryArea(4, mainMemoryBytes);
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
        ulong memoryBase,
        ulong indexBase,
        ushort indexStride,
        byte indexType,
        byte indexIsByteOffset)
    {
        byte[] descriptor = new byte[32];
        BitConverter.GetBytes(memoryBase).CopyTo(descriptor, 0);
        BitConverter.GetBytes(indexBase).CopyTo(descriptor, 8);
        BitConverter.GetBytes(indexStride).CopyTo(descriptor, 16);
        descriptor[18] = indexType;
        descriptor[19] = indexIsByteOffset;
        Processor.MainMemory.WriteToPosition(descriptor, descriptorAddress);
    }

    private static uint[] ReadVectorWords(ulong address, int count)
    {
        byte[] bytes = ReadBytes(address, count * sizeof(uint));
        return Enumerable.Range(0, count)
            .Select(index => BitConverter.ToUInt32(bytes, index * sizeof(uint)))
            .ToArray();
    }

    private static byte[] ReadBytes(ulong address, int length)
    {
        return Processor.MainMemory.ReadFromPosition(new byte[length], address, (ulong)length);
    }

    private static void WriteBytes(ulong address, byte[] bytes)
    {
        Processor.MainMemory.WriteToPosition(bytes, address);
    }

}
